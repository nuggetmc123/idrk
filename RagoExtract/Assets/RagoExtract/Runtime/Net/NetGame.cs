using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rago
{
    /// <summary>Hooks gameplay code calls; forwarded to NetGame when a session is live.</summary>
    public static class NetEvents
    {
        public static void ItemTaken(Item it) { if (NetGame.Live) NetGame.I.OnItemTaken(it); }
        public static void ItemDropped(Item it) { if (NetGame.Live) NetGame.I.OnItemDropped(it); }
        public static void ItemStored(Item it) { if (NetGame.Live) NetGame.I.OnItemTaken(it); }
    }

    /// <summary>
    /// Game-level replication on top of any INetBackend: player avatars (head/hands/body), gunshots,
    /// player damage, deaths with lootable bodies, world item pickups/drops, host-authoritative NPCs,
    /// vault openings and extractions. Messages are small binary packets relayed by the host.
    /// </summary>
    public class NetGame : MonoBehaviour
    {
        public static NetGame I;
        public static bool Live => I != null && NetHub.Backend != null && NetHub.Backend.InSession && I._active;
        public static bool IsHost => !Live || NetHub.Backend.IsHost;
        public readonly Dictionary<ulong, RemotePlayer> Remotes = new Dictionary<ulong, RemotePlayer>();
        public readonly Dictionary<int, Item> Items = new Dictionary<int, Item>();
        bool _active;
        float _poseT, _npcT, _voiceT, _gearT, _npcFullT;
        string _lastGear;
        // last raider state sent, per raider: only changes go out (everything is on the reliable channel, which backs up
        // - and lags the whole game - if it's fed more than it can carry)
        readonly Dictionary<int, (Vector3 pos, float yaw, byte st, Vector3 aim)> _npcSent = new Dictionary<int, (Vector3, float, byte, Vector3)>();
        const float NetStep = 0.03f;   // 3 cm grid for positions in raider updates
        static void P3(BinaryWriter w, Vector3 v) { w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v.x / NetStep), -32767, 32767)); w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v.y / NetStep), -32767, 32767)); w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v.z / NetStep), -32767, 32767)); }
        static Vector3 P3(BinaryReader r) => new Vector3(r.ReadInt16() * NetStep, r.ReadInt16() * NetStep, r.ReadInt16() * NetStep);
        // offsets from the body (head, hands) to the millimetre, velocity to the cm/s: half the size of three floats
        static void S3(BinaryWriter w, Vector3 v, float step) { w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v.x / step), -32767, 32767)); w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v.y / step), -32767, 32767)); w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v.z / step), -32767, 32767)); }
        static Vector3 S3(BinaryReader r, float step) => new Vector3(r.ReadInt16() * step, r.ReadInt16() * step, r.ReadInt16() * step);
        int _dropCounter, _deaths;
        public bool InRaid;
        readonly HashSet<ulong> _helloSent = new HashSet<ulong>();
        bool _wasHost;

        /// <summary>Microphone audio from VoiceChat (8 kHz u-law frames).</summary>
        public void SendVoice(byte[] frame)
        {
            if (!Live || Spectator.Active) return;
            if (NetHub.Backend.Rtt > 0.45f) return;   // the line is backed up: voice is the biggest thing we send, skip it until it clears
            // voice is by far the biggest thing we send: only when someone is close enough to hear it (raids are huge)
            if (InRaid)
            {
                var me = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.transform.position : Vector3.zero;
                bool near = false;
                foreach (var kv in Remotes) if (kv.Value != null && ((kv.Value.transform.position - me).sqrMagnitude < 50f * 50f || NetHub.Backend.IsTeammate(kv.Key))) { near = true; break; }   // your squad hears you on the radio at any distance
                if (!near) return;
            }
            var w = W(Msg.Voice);
            w.Write((short)frame.Length); w.Write(frame);
            Send(false);
        }

        enum Msg : byte { Hello = 1, Pose, Fire, HitPlayer, Died, ItemTaken, ItemSpawned, NpcStates, NpcHit, NpcFire, NpcDied, Vault, Extracted, Bye, Voice, Blast, NpcVoice, Hidden, NpcSpawnAsk, NpcSpawned, LightState, AirDrop, CrateOpen, ItemRest, WindowBroken, Feed, LockBroken }

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("NetGame");
            DontDestroyOnLoad(go);
            I = go.AddComponent<NetGame>();
        }

        public void Begin(bool raid)
        {
            End();
            var b = NetHub.Backend;
            if (b == null || !b.InSession) return;
            _active = true;
            InRaid = raid;
            _lastRx = Time.time;
            _wasHost = !raid || b.IsHost;
            b.OnData += OnData;
            b.OnPeerJoined += OnPeerJoined;
            b.OnPeerLeft += OnPeerLeft;
            b.OnDisconnected += OnDisconnected;
            Gun.OnLocalFire = LocalFire;
            Vitals.OnLocalDamaged = null;
            Ballistics.OnHitActor = OnHitActor;
            SendHello(ulong.MaxValue);
            if (NetHub.Voice != null) NetHub.Voice.Join("rago_" + b.SessionId, true);
        }

        public void End()
        {
            var b = NetHub.Backend;
            if (_active && b != null)
            {
                b.OnData -= OnData; b.OnPeerJoined -= OnPeerJoined; b.OnPeerLeft -= OnPeerLeft; b.OnDisconnected -= OnDisconnected;
            }
            _active = false;
            foreach (var r in Remotes.Values) if (r != null) Destroy(r.gameObject);
            Remotes.Clear();
            Items.Clear();
            _taken.Clear();
            _flying.Clear();
            _helloSent.Clear();
            Gun.OnLocalFire = null;
            Ballistics.OnHitActor = null;
            if (NetHub.Voice != null && NetHub.Voice.Ready) NetHub.Voice.Leave();
        }

        void OnDisconnected()
        {
            bool raid = InRaid;
            End();
            // Photon dropped us mid-raid: you stay right where you are and keep playing (the raiders carry on on your
            // machine) while the game quietly gets you back into the same raid - no trip to the bunker, no reload
            if (raid && RaidManager.I != null && !RaidManager.I.Done) { NpcDirector.I?.BecomeAuthority(); StartCoroutine(QuietRejoin()); return; }
            RagoGame.I?.Player?.Toast("Lost connection to the other players");
        }

        System.Collections.IEnumerator QuietRejoin()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (RaidManager.I == null || RaidManager.I.Done || NetHub.Backend == null) yield break;
                bool? result = null;
                if (NetHub.Backend.CanReconnect) NetHub.Backend.Reconnect(ok => result = ok); else result = false;
                float until = Time.realtimeSinceStartup + 25f;
                while (result == null && Time.realtimeSinceStartup < until) yield return null;
                if (result == true && RaidManager.I != null && !RaidManager.I.Done)
                {
                    Begin(true);
                    Debug.Log("[Rago] back in the raid after a dropped connection");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(4f);
            }
            Debug.Log("[Rago] couldn't get back into the raid - carrying on alone");
        }

        float _lastRx, _staleT, _helloT = 4f;
        /// <summary>Players we stopped hearing from (crashed, or their connection died without Photon noticing yet):
        /// take their frozen body out of the world. They come back on their next update.</summary>
        void CheckStale()
        {
            _staleT -= Time.deltaTime;
            if (_staleT > 0f) return;
            _staleT = 1f;
            List<ulong> gone = null;
            foreach (var kv in Remotes)
                if (kv.Value == null || (Time.time - kv.Value.LastHeard > 30f && !kv.Value.IsDead)) (gone ??= new List<ulong>()).Add(kv.Key);
            if (gone != null) foreach (var id in gone) { if (Remotes[id] != null) Destroy(Remotes[id].gameObject); Remotes.Remove(id); }
            // say who we are every few seconds: someone still loading when the first hello went out missed it
            // (they saw you as "Operator"), and a player who reconnects needs it again
            _helloT -= 1f;
            if (_helloT <= 0f) { _helloT = 4f; SendHello(ulong.MaxValue); }
        }

        void OnPeerJoined(ulong id) { SendHello(id); }
        void OnPeerLeft(ulong id) { if (Remotes.TryGetValue(id, out var r)) { if (r != null) Destroy(r.gameObject); Remotes.Remove(id); } }

        // -------------------------------------------------------------------------------- send helpers
        static MemoryStream _ms = new MemoryStream(1024);
        static BinaryWriter _w = new BinaryWriter(_ms);
        static BinaryWriter W(Msg m) { _ms.SetLength(0); _w.Write((byte)m); return _w; }
        static void Send(bool reliable, ulong target = ulong.MaxValue) { if (NetHub.Backend != null && NetHub.Backend.InSession) NetHub.Backend.Send(_ms.ToArray(), reliable, target); }
        static void V3(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        static Vector3 V3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        static void Q(BinaryWriter w, Quaternion q) { w.Write((short)(q.x * 32000)); w.Write((short)(q.y * 32000)); w.Write((short)(q.z * 32000)); w.Write((short)(q.w * 32000)); }
        static Quaternion Q(BinaryReader r) { var q = new Quaternion(r.ReadInt16() / 32000f, r.ReadInt16() / 32000f, r.ReadInt16() / 32000f, r.ReadInt16() / 32000f); float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w); if (m < 0.001f) return Quaternion.identity; return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m); }

        /// <summary>"helmet|vest|pack" ids of what the local player is wearing.</summary>
        static string GearKey(PlayerRig p)
        {
            var g = p.Gear;
            var pack = g.PackSlot.item as Backpack;
            return (g.Helmet != null ? g.Helmet.id : "") + "|" + (g.Vest != null ? g.Vest.id : "") + "|" + (pack != null ? pack.def.id : "");
        }

        /// <summary>Introduce yourself to everyone (after you stop spectating).</summary>
        public void Announce() { if (Live) SendHello(ulong.MaxValue); }

        void SendHello(ulong to)
        {
            if (Spectator.Active) return;   // spectating: nobody gets told you're here
            var w = W(Msg.Hello);
            w.Write(Profile.Current.DisplayName);
            w.Write(Profile.Current.outfitSeed);
            Send(true, to);
        }

        void Update()
        {
            if (!Live) return;
            CheckStale();
            if (!Live) return;
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null) return;
            _poseT -= Time.deltaTime;
            if (_poseT <= 0 && !Spectator.Active)   // spectating: no body is sent, so nobody sees you
            {
                // 15 a second normally; fewer when the line to the other players is backing up (it only ever needs the latest)
                float rtt = NetHub.Backend.Rtt;
                float step = rtt < 0.25f ? 1f / 15f : rtt < 0.5f ? 1f / 10f : 1f / 6f;
                int others = NetHub.Backend.Peers.Count;
                if (others >= 3) step = Mathf.Max(step, 1f / 12f);   // a big group: a little less often (voice and raider updates were cut instead)
                _poseT = step;
                var w = W(Msg.Pose);
                var root = p.transform.position;
                // what each hand is holding (the gun itself, with its attachments), where it is, and whether the light /
                // laser is on - so other players see your gun exactly where it is, your hands on its grips, and its beam
                var il = PrimaryHeld(p.HandL); var ir = PrimaryHeld(p.HandR);
                var sideGun = (ir as Gun) ?? (il as Gun);
                byte f = 0;
                if (p.Dead) f |= 1;
                if (il != null) f |= 8;
                if (ir != null) f |= 16;
                if (p.HandL.Held != null && p.HandL.HeldAsSecondary) f |= 32;
                if (p.HandR.Held != null && p.HandR.HeldAsSecondary) f |= 64;
                if (sideGun != null && sideGun.SideOn && (sideGun.Attached(AttachSlot.Side) != null)) f |= 128;
                string kl = HeldKey(p.HandL), kr = HeldKey(p.HandR), gk = GearKey(p);
                _gearT -= step;
                bool keys = kl != _lastKl || kr != _lastKr || _gearT <= 0f, gear = gk != _lastGear || _gearT <= 0f;
                if (_gearT <= 0f) _gearT = 2f;
                byte f2 = (byte)((keys ? 1 : 0) | (gear ? 2 : 0));
                w.Write(f); w.Write(f2);
                w.Write(Time.time);   // our clock: the others show us smoothly at a fixed delay (no jumping when packets bunch up)
                V3(w, root);
                S3(w, p.Head.position - root, 0.001f); Q(w, p.Head.rotation);
                S3(w, p.HandL.Visual.position - root, 0.001f); Q(w, p.HandL.Visual.rotation);
                S3(w, p.HandR.Visual.position - root, 0.001f); Q(w, p.HandR.Visual.rotation);
                S3(w, p.MoveVelocity, 0.01f);
                if (il != null) { S3(w, il.transform.position - root, 0.001f); Q(w, il.transform.rotation); }
                if (ir != null) { S3(w, ir.transform.position - root, 0.001f); Q(w, ir.transform.rotation); }
                if (keys) { w.Write(kl); w.Write(kr); _lastKl = kl; _lastKr = kr; }
                if (gear) { w.Write(gk); _lastGear = gk; }
                Send(false);
            }
            if (_flying.Count > 0) TrackFlying();
            if (InRaid && NpcDirector.I != null)
            {
                // no host: every player runs its share of the raiders and tells the others where they are
                _ownT -= Time.deltaTime;
                if (_ownT <= 0f) { _ownT = 0.5f; ShareOutNpcs(); }
                _npcT -= Time.deltaTime;
                if (_npcT <= 0)
                {
                    float rtt = NetHub.Backend.Rtt; int others = NetHub.Backend.Peers.Count;
                    _npcT = (rtt < 0.3f ? 0.15f : rtt < 0.6f ? 0.3f : 0.5f) * (others >= 3 ? 1.3f : 1f);
                    SendNpcStates(_npcT);
                }
            }
            _voiceT -= Time.deltaTime;
            if (_voiceT <= 0 && NetHub.Voice != null && NetHub.Voice.Ready) { _voiceT = 0.25f; NetHub.Voice.UpdatePosition(p.Head); }
        }

        /// <summary>The bodies this machine shows (menu > RECENT, for testing).</summary>
        public string DebugInfo()
        {
            var sb = new System.Text.StringBuilder("BODIES (" + Remotes.Count + ")" + (InRaid ? "  raiders I run: " + MyNpcs() : "") + "\n");
            foreach (var kv in Remotes)
                if (kv.Value != null) sb.Append(kv.Key).Append(" ").Append(kv.Value.DisplayName).Append("  moved ").Append((Time.time - kv.Value.LastHeard).ToString("0.0")).Append("s ago\n");
            return sb.ToString();
        }
        int MyNpcs() { int c = 0; if (NpcDirector.I != null) foreach (var n in NpcDirector.I.Npcs) if (n != null && n.Alive && !n.Puppet) c++; return c; }

        bool NearAnyPlayer(Vector3 p, float r)
        {
            var me = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.transform.position : p;
            if ((me - p).sqrMagnitude < r * r) return true;
            foreach (var rp in Remotes.Values) if (rp != null && (rp.transform.position - p).sqrMagnitude < r * r) return true;
            return false;
        }

        float _ownT;
        static readonly List<ulong> _everyone = new List<ulong>();
        /// <summary>Which player runs this raider's brain. Nobody is in charge: each raider is given to one of the
        /// players by a hash of (raider, player) - every machine works out the same answer, and when a player leaves
        /// only their raiders move to someone else.</summary>
        public static ulong OwnerOf(int npcId)
        {
            var b = NetHub.Backend;
            if (!Live || b == null) return 0;
            ulong best = b.LocalId; uint bestH = Mix(npcId, b.LocalId);
            foreach (var p in b.Peers) { uint h = Mix(npcId, p); if (h > bestH || (h == bestH && p < best)) { bestH = h; best = p; } }
            return best;
        }
        static uint Mix(int npc, ulong player)
        {
            unchecked
            {
                uint x = (uint)npc * 0x9E3779B1u ^ (uint)player * 0x85EBCA77u ^ (uint)(player >> 32);
                x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15; x *= 0x846CA68Bu; x ^= x >> 16;
                return x;
            }
        }
        /// <summary>This player runs this raider (offline: always).</summary>
        public static bool Mine(int npcId) => !Live || NetHub.Backend == null || OwnerOf(npcId) == NetHub.Backend.LocalId;
        /// <summary>One player, the same on every machine, for one-off jobs (spawning the air drop's guards).</summary>
        public static bool FirstPlayer
        {
            get
            {
                var b = NetHub.Backend;
                if (!Live || b == null) return true;
                foreach (var p in b.Peers) if (p < b.LocalId) return false;
                return true;
            }
        }

        void ShareOutNpcs()
        {
            foreach (var n in NpcDirector.I.Npcs)
            {
                if (n == null || !n.Alive) continue;
                bool mine = Mine(n.Id);
                if (mine && n.Puppet) n.BecomeAuthority();
                else if (!mine && !n.Puppet) n.MakePuppet();
            }
        }

        string _lastKl, _lastKr;
        static Item PrimaryHeld(Hand h) => h.Held != null && !h.HeldAsSecondary && h.Held.def != null ? h.Held : null;
        /// <summary>"id" or, for a gun, "id;sight;muzzle;side;grip" - enough for others to build the same gun.</summary>
        static string HeldKey(Hand h)
        {
            var it = PrimaryHeld(h);
            if (it == null) return "";
            if (it is Gun g) { var d = g.Save(); return d.id + ";" + d.sight + ";" + d.muzzle + ";" + d.side + ";" + d.gripAtt; }
            return it.def.id;
        }

        // -------------------------------------------------------------------------------- gameplay hooks
        void LocalFire(Gun g, Vector3 origin, Vector3 dir)
        {
            var w = W(Msg.Fire);
            w.Write(g.Quiet && !g.g.suppressed ? g.g.id + "~s" : g.g.id); V3(w, origin); V3(w, dir);   // ~s: a suppressor is screwed on
            Send(true);
        }

        /// <summary>A grenade we threw went off: everyone else shows it (and works out their own flash).</summary>
        public void SendBlast(Vector3 p, bool flash)
        {
            var w = W(Msg.Blast);
            V3(w, p); w.Write(flash);
            Send(true);
        }

        void OnHitActor(HitInfo h, HitZone z)
        {
            if (h.attacker != 0)
            {
                // host simulates NPC bullets: forward their damage to the player that got hit
                return; // only report our own hits (raider shots are worked out by each player against themselves)
            }
            if (z.owner is RemotePlayer && h.gunId == "frag") { }   // grenades: each player works out their own shrapnel (Msg.Blast)
            else if (z.owner is RemotePlayer mrp)
            {
                // you hit another player on YOUR screen: they take it (bullets, punches, knives). Working shots out on
                // the target's machine missed whenever they were moving - they'd already moved on from where you saw them.
                var w = W(Msg.HitPlayer);
                w.Write(mrp.NetId); w.Write(h.damage); w.Write((byte)h.part); V3(w, h.point); V3(w, h.dir); w.Write(h.force);
                w.Write(h.gunId ?? "");
                Send(true, mrp.NetId);
            }
            else if (z.owner is Npc npc)
            {
                // to everyone (not just the host): each copy of the raider keeps the same health, and whoever's shot
                // takes them to zero kills them for all
                var w = W(Msg.NpcHit);
                w.Write(npc.Id); w.Write(h.damage); w.Write((byte)h.part); V3(w, h.point); V3(w, h.dir); w.Write(h.force);
                Send(true);
            }
        }

        public void LocalDied(Vector3 pos, List<ItemData> items)
        {
            if (!Live) return;
            var w = W(Msg.Died);
            V3(w, pos);
            w.Write(items.Count);
            int baseId = 50000000 + (int)(NetHub.Backend.LocalId % 1000) * 1000 + (++_deaths % 10) * 100;
            for (int i = 0; i < items.Count; i++) { w.Write(baseId + i); w.Write(JsonUtility.ToJson(items[i])); }
            Send(true);
        }

        public int NewDropId() => 200000000 + (int)(NetHub.Backend.LocalId % 1000) * 100000 + (++_dropCounter % 90000);

        public void OnItemTaken(Item it)
        {
            if (it.netId == 0 || !InRaid) return;
            var w = W(Msg.ItemTaken); w.Write(it.netId); Send(true);
            Items.Remove(it.netId);
            it.netId = 0;
        }

        public void OnItemDropped(Item it)
        {
            if (!InRaid || it is Magazine || it is Backpack || it.Consumed) return;
            it.netId = NewDropId();
            Items[it.netId] = it;
            var w = W(Msg.ItemSpawned);
            w.Write(it.netId); w.Write(JsonUtility.ToJson(it.Save())); V3(w, it.transform.position); Q(w, it.transform.rotation);
            // how it was thrown: everyone sees it fly the same way, and its resting spot is sent when it stops
            V3(w, it.rb != null ? it.rb.Vel() : Vector3.zero); V3(w, it.rb != null ? it.rb.angularVelocity : Vector3.zero);
            Send(true);
            _flying.Add((it, Time.time));
        }

        readonly HashSet<int> _taken = new HashSet<int>();
        /// <summary>Has another player already picked up the loot with this id? (Crates and bodies roll the same loot on every
        /// machine; without this, a crate you opened after someone emptied it was full again.)</summary>
        public static bool Taken(int id) => I != null && Live && id != 0 && I._taken.Contains(id);

        public void Register(Item it)
        {
            if (it == null || it.netId == 0) return;
            if (Taken(it.netId)) { var s = it.slot; if (s != null) s.Release(it); it.gameObject.SetActive(false); Destroy(it.gameObject); return; }
            Items[it.netId] = it;
        }

        /// <summary>Tell everyone (the host runs the raiders) whether raiders should ignore you (tester tool).</summary>
        public void SendHidden(bool hidden)
        {
            if (!Live) return;
            var w = W(Msg.Hidden); w.Write(hidden); Send(true);
        }

        public void WindowBroken(int id) { if (!Live) return; var w = W(Msg.WindowBroken); w.Write(id); Send(true); }

        public void CrateOpened(int index) { if (!Live || !InRaid) return; var w = W(Msg.CrateOpen); w.Write(index); Send(true); }

        // items we dropped / threw: once they come to rest, everyone puts them in the same final spot
        readonly List<(Item it, float t)> _flying = new List<(Item, float)>();
        void TrackFlying()
        {
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var (it, t) = _flying[i];
                if (it == null || it.netId == 0 || it.Held || it.slot != null) { _flying.RemoveAt(i); continue; }
                bool rest = it.rb == null || it.rb.isKinematic || it.rb.IsSleeping() || it.rb.Vel().sqrMagnitude < 0.01f && Time.time - t > 0.6f;
                if (!rest && Time.time - t < 6f) continue;
                _flying.RemoveAt(i);
                var w = W(Msg.ItemRest); w.Write(it.netId); V3(w, it.transform.position); Q(w, it.transform.rotation); Send(true);
            }
        }

        /// <summary>A radio air drop called in by us: everyone sees the same plane and crate.</summary>
        public void SendAirDrop(Vector3 target, float heading, bool boss, int index)
        {
            if (!Live) return;
            var w = W(Msg.AirDrop); V3(w, target); w.Write(heading); w.Write(boss); w.Write(index); Send(true);
        }

        /// <summary>Tester: spawn a raider in an online raid. The host makes it and tells everyone (with its id).</summary>
        public void RequestNpc(int kind, Vector3 pos, float yaw)
        {
            HostSpawnNpc(kind, pos, yaw);   // no host: whoever spawns it makes it and tells everyone (with its id)
        }

        void HostSpawnNpc(int kind, Vector3 pos, float yaw)
        {
            if (NpcDirector.I == null) return;
            // each player's spawns get their own id block, so two players spawning at once never clash
            int id = Mathf.Max(NpcDirector.I.NextTesterId(), 9000 + (int)(NetHub.Backend.LocalId % 40) * 500), seed = Random.Range(1, 99999);
            NpcDirector.I.SpawnNet(id, pos, yaw, seed, kind >= 1, Tester.BossFor(kind), false);
            var w = W(Msg.NpcSpawned); w.Write(id); w.Write((byte)kind); V3(w, pos); w.Write(yaw); w.Write(seed); Send(true);
        }

        public void NpcVoice(int id, byte line, byte idx)
        {
            if (!Live) return;
            var w = W(Msg.NpcVoice); w.Write(id); w.Write(line); w.Write(idx); Send(false);
        }

        public void NpcFire(int id, Vector3 origin, Vector3 dir)
        {
            if (!Live) return;
            var w = W(Msg.NpcFire); w.Write(id); V3(w, origin); V3(w, dir); Send(false);
        }

        public void NpcDied(int id, Vector3 point, Vector3 impulse, int killer = 0, bool head = false)
        {
            if (!Live) return;
            var w = W(Msg.NpcDied); w.Write(id); V3(w, point); V3(w, impulse); w.Write(killer); w.Write(head); Send(true);
        }

        public void LockBroken(int doorId) { if (!Live) return; var w = W(Msg.LockBroken); w.Write(doorId); Send(true); }

        public void VaultOpened(int id) { if (!Live) return; var w = W(Msg.Vault); w.Write(id); Send(true); }

        public void NpcDamageToRemote(ulong target, HitInfo h)
        {
            var w = W(Msg.HitPlayer);
            w.Write(target); w.Write(h.damage); w.Write((byte)h.part); V3(w, h.point); V3(w, h.dir); w.Write(h.force);
            Send(true, target);
        }

        void SendNpcStates(float dt)
        {
            var list = NpcDirector.I.Npcs;
            _npcFullT -= dt;
            bool full = _npcFullT <= 0f;
            if (full) _npcFullT = 3f;   // everyone, now and then (someone joining, a lost update)
            var send = new List<Npc>();
            foreach (var n in list)
            {
                if (n == null || n.Puppet) continue;   // only the raiders this player runs
                if (!NearAnyPlayer(n.transform.position, 300f)) continue;   // nobody's anywhere near it: nothing to send
                byte st = (byte)((byte)n.State | (n.Alive ? 0x80 : 0));
                if (!full && _npcSent.TryGetValue(n.Id, out var last) && last.st == st && (last.pos - n.transform.position).sqrMagnitude < 0.0036f
                    && Mathf.Abs(Mathf.DeltaAngle(last.yaw, n.Yaw)) < 3f && (last.aim - n.AimPoint).sqrMagnitude < 0.04f) continue;
                _npcSent[n.Id] = (n.transform.position, n.Yaw, st, n.AimPoint);
                send.Add(n);
            }
            if (send.Count == 0) return;
            var w = W(Msg.NpcStates);
            w.Write((short)send.Count);
            foreach (var n in send)
            {
                w.Write((short)n.Id);
                P3(w, n.transform.position);
                w.Write((byte)Mathf.RoundToInt(Mathf.Repeat(n.Yaw, 360f) / 360f * 255f));
                w.Write((byte)((byte)n.State | (n.Alive ? 0x80 : 0)));
                P3(w, n.AimPoint);
            }
            Send(false);
        }

        // -------------------------------------------------------------------------------- receive
        void OnData(ulong from, byte[] data)
        {
            if (data == null || data.Length == 0) return;
            _lastRx = Time.time;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var r = new BinaryReader(ms))
                {
                    var m = (Msg)r.ReadByte();
                    switch (m)
                    {
                        case Msg.Hello:
                            {
                                string name = r.ReadString(); int seed = r.ReadInt32();
                                var me = Profile.Current;
                                if (me != null && name == me.DisplayName && from != NetHub.Backend.LocalId && NetHub.Backend.LocalId > from)
                                {
                                    // two operators with the same callsign + tag: the newer one (higher id) gets a fresh tag
                                    me.tag = Profile.NewTagFor(me.tag); me.Save();
                                    RagoGame.I?.Player?.Toast("Another operator is " + name + " - your tag is now #" + me.tag.ToString("0000"));
                                    SendHello(ulong.MaxValue);
                                }
                                // the same operator under another id (they reconnected, or an old relayed copy): one body only
                                if (!string.IsNullOrEmpty(name))
                                {
                                    List<ulong> dup = null;
                                    foreach (var kv in Remotes) if (kv.Key != from && kv.Value != null && kv.Value.DisplayName == name) (dup ??= new List<ulong>()).Add(kv.Key);
                                    if (dup != null) foreach (var d in dup) { Destroy(Remotes[d].gameObject); Remotes.Remove(d); }
                                }
                                var rp = GetRemote(from, seed);
                                if (rp.DisplayName != name && !string.IsNullOrEmpty(name)) RagoGame.I?.Player?.Toast(name + " is here");
                                rp.DisplayName = name;
                                if (_helloSent.Add(from)) SendHello(from); // introduce ourselves back
                                break;
                            }
                        case Msg.Pose:
                            {
                                var rp = GetRemote(from, (int)from * 31);
                                rp.LastHeard = Time.time;
                                if (_helloSent.Add(from)) SendHello(from);   // their movement got here before their hello: introduce ourselves
                                byte f = r.ReadByte(), f2 = r.ReadByte();
                                float st = r.ReadSingle();
                                var root = V3(r); var hp = root + S3(r, 0.001f); var hr = Q(r); var lp = root + S3(r, 0.001f); var lr = Q(r); var rpp = root + S3(r, 0.001f); var rr = Q(r); var vel = S3(r, 0.01f);
                                Vector3 glp = default, grp = default; Quaternion glr = Quaternion.identity, grr = Quaternion.identity;
                                if ((f & 8) != 0) { glp = root + S3(r, 0.001f); glr = Q(r); }
                                if ((f & 16) != 0) { grp = root + S3(r, 0.001f); grr = Q(r); }
                                if ((f2 & 1) != 0) rp.ApplyHeld(r.ReadString(), r.ReadString());
                                if ((f2 & 2) != 0) rp.ApplyGear(r.ReadString());
                                rp.ApplyPose((f & 1) != 0, root, hp, hr, lp, lr, rpp, rr, vel);
                                rp.ApplyHeldPose((f & 8) != 0, glp, glr, (f & 16) != 0, grp, grr, (f & 32) != 0, (f & 64) != 0, (f & 128) != 0);
                                rp.CommitSnap(st);
                                break;
                            }
                        case Msg.Blast:
                            {
                                var bp = V3(r); bool fl = r.ReadBoolean();
                                Blast.Detonate(bp, fl, (int)from + 1, false);
                                break;
                            }
                        case Msg.Fire:
                            {
                                string gid = r.ReadString(); bool sup = gid.EndsWith("~s"); if (sup) gid = gid.Substring(0, gid.Length - 2);
                                var g = Db.Get<GunDef>(gid); var o = V3(r); var d = V3(r);
                                if (g == null) break;
                                if (sup) g = g.Quiet;
                                Remotes.TryGetValue(from, out var rp);
                                // another player's shot: it can hurt us here (we're the only ones who know where we really are)
                                // just the shot you see and hear - the shooter tells whoever they hit (Msg.HitPlayer)
                                Ballistics.Fire(o, d, g, (int)from + 1, rp != null ? rp.transform : null, rp, true, g.cls == GunClass.LMG, false);
                                Sfx.Gunshot(g, o, false);
                                if (rp != null) rp.MuzzleFlash(g); else Fx.TracerLine(o, o + d * 50f);
                                break;
                            }
                        case Msg.HitPlayer:
                            {
                                ulong target = r.ReadUInt64(); float dmg = r.ReadSingle(); var part = (BodyPart)r.ReadByte(); var pt = V3(r); var dir = V3(r); float force = r.ReadSingle();
                                string hg = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
                                // it was sent only to us, so it's ours (not checking the id: a mismatch must never make you bulletproof)
                                if (RagoGame.I == null || RagoGame.I.Player == null || RagoGame.I.Player.Dead) break;
                                var v = RagoGame.I.Player.Vitals;
                                v.TakeHit(new HitInfo { damage = dmg, part = part, point = pt, dir = dir, attacker = (int)from + 1, fromNet = true, force = force, gunId = hg }, null);
                                break;
                            }
                        case Msg.Died:
                            {
                                var pos = V3(r);
                                int n = r.ReadInt32();
                                if (Remotes.TryGetValue(from, out var rp)) rp.Die();
                                for (int i = 0; i < n; i++)
                                {
                                    int id = r.ReadInt32(); var d = JsonUtility.FromJson<ItemData>(r.ReadString());
                                    var it = ItemFactory.Spawn(d, pos + Vector3.up * (0.4f + i * 0.08f) + Random.insideUnitSphere * 0.3f, Random.rotation);
                                    if (it != null) { it.netId = id; Items[id] = it; }
                                }
                                break;
                            }
                        case Msg.ItemTaken:
                            {
                                int id = r.ReadInt32();
                                _taken.Add(id);   // someone has it: if this crate / body hasn't been opened here yet, it won't appear when it is
                                if (Items.TryGetValue(id, out var it) && it != null && !it.Held)
                                {
                                    if (it.slot != null) it.slot.Release(it);
                                    Destroy(it.gameObject);
                                }
                                Items.Remove(id);
                                break;
                            }
                        case Msg.ItemSpawned:
                            {
                                int id = r.ReadInt32(); var d = JsonUtility.FromJson<ItemData>(r.ReadString()); var pos = V3(r); var rot = Q(r);
                                Vector3 vel = Vector3.zero, ang = Vector3.zero;
                                if (r.BaseStream.Position < r.BaseStream.Length) { vel = V3(r); ang = V3(r); }
                                if (Items.ContainsKey(id)) break;
                                var it = ItemFactory.Spawn(d, pos, rot);
                                if (it != null)
                                {
                                    it.netId = id; Items[id] = it;
                                    if (it.rb != null) { it.rb.isKinematic = false; it.SetCollidersTrigger(false); it.rb.SetVel(vel); it.rb.angularVelocity = ang; }
                                }
                                break;
                            }
                        case Msg.NpcStates:
                            {
                                if (NpcDirector.I == null) break;
                                int n = r.ReadInt16();
                                for (int i = 0; i < n; i++)
                                {
                                    int id = r.ReadInt16(); var pos = P3(r); float yaw = r.ReadByte() / 255f * 360f; byte sb = r.ReadByte(); var aim = P3(r);
                                    var sn = NpcDirector.I.Get(id);
                                    if (sn != null && sn.Puppet) sn.ApplyNetState(pos, yaw, (NpcState)(sb & 0x7f), aim, (sb & 0x80) != 0);   // ones we run ourselves: ours wins
                                }
                                break;
                            }
                        case Msg.NpcHit:
                            {
                                int id = r.ReadInt32(); float dmg = r.ReadSingle(); var part = (BodyPart)r.ReadByte(); var pt = V3(r); var dir = V3(r); float force = r.ReadSingle();
                                if (NpcDirector.I == null) break;
                                var npc = NpcDirector.I.Get(id);
                                npc?.TakeHit(new HitInfo { damage = dmg, part = part, point = pt, dir = dir, attacker = (int)from + 1, fromNet = true, force = force }, null);
                                break;
                            }
                        case Msg.Hidden:
                            {
                                bool hid = r.ReadBoolean();
                                if (Remotes.TryGetValue(from, out var hp) && hp != null) hp.HiddenFromAi = hid;   // a tester made themselves invisible to raiders
                                break;
                            }
                        case Msg.LockBroken:
                            {
                                int did = r.ReadInt32();
                                var door = SwingDoor.All.Find(x => x != null && x.Id == did);
                                if (door != null) door.BreakLock(false);
                                break;
                            }
                        case Msg.WindowBroken:
                            {
                                int wid = r.ReadInt32();
                                var win = global::Rago.Window.All.Find(x => x != null && x.Id == wid);
                                if (win != null) win.Break();
                                break;
                            }
                        case Msg.CrateOpen:
                            {
                                int ix = r.ReadInt32();
                                if (LootCrate.ByIndex.TryGetValue(ix, out var cr) && cr != null) cr.Open(false);
                                break;
                            }
                        case Msg.ItemRest:
                            {
                                int id = r.ReadInt32(); var pos = V3(r); var rot = Q(r);
                                if (Items.TryGetValue(id, out var it) && it != null && !it.Held && it.slot == null)
                                {
                                    if (it.rb != null) { it.rb.SetVel(Vector3.zero); it.rb.angularVelocity = Vector3.zero; it.rb.position = pos; it.rb.rotation = rot; }
                                    it.transform.SetPositionAndRotation(pos, rot);
                                }
                                break;
                            }
                        case Msg.AirDrop:
                            {
                                var t = V3(r); float hd = r.ReadSingle(); bool boss = r.ReadBoolean(); int ix = r.ReadInt32();
                                if (InRaid) global::Rago.AirDrop.Call(t, hd, boss, false, ix);
                                break;
                            }
                        case Msg.NpcSpawnAsk:
                            {
                                int kind = r.ReadByte(); var pos = V3(r); float yaw = r.ReadSingle();
                                HostSpawnNpc(kind, pos, yaw);   // an older client asking: make it for them
                                break;
                            }
                        case Msg.NpcSpawned:
                            {
                                int id = r.ReadInt32(); int kind = r.ReadByte(); var pos = V3(r); float yaw = r.ReadSingle(); int seed = r.ReadInt32();
                                if (NpcDirector.I != null) NpcDirector.I.SpawnNet(id, pos, yaw, seed, kind >= 1, Tester.BossFor(kind), !Mine(id));
                                break;
                            }
                        case Msg.NpcVoice:
                            {
                                int id = r.ReadInt32(); byte l = r.ReadByte(); byte ix = r.ReadByte();
                                NpcDirector.I?.Get(id)?.RemoteVoice(l, ix);
                                break;
                            }
                        case Msg.NpcFire:
                            {
                                int id = r.ReadInt32(); var o = V3(r); var d = V3(r);
                                NpcDirector.I?.Get(id)?.RemoteFire(o, d);
                                break;
                            }
                        case Msg.NpcDied:
                            {
                                int id = r.ReadInt32(); var pt = V3(r); var imp = V3(r);
                                int killer = r.BaseStream.Position < r.BaseStream.Length ? r.ReadInt32() : 0;
                                bool head = r.BaseStream.Position < r.BaseStream.Length && r.ReadBoolean();
                                var dn = NpcDirector.I?.Get(id);
                                if (dn != null && dn.Alive) { if (killer != 0) { dn.KillerId = killer; dn.KillerHeadshot = head; } dn.Die(pt, imp, true); }
                                break;
                            }
                        case Msg.Voice:
                            {
                                int n = r.ReadInt16();
                                var frame = r.ReadBytes(n);
                                if (Remotes.TryGetValue(from, out var rp) && rp != null)
                                {
                                    // a squad mate further than shouting distance comes over the radio; anyone else only in earshot
                                    var me = RagoGame.I != null && RagoGame.I.Player != null ? RagoGame.I.Player.transform.position : rp.transform.position;
                                    bool far = (rp.transform.position - me).sqrMagnitude > 25f * 25f;
                                    bool squad = InRaid && NetHub.Backend.IsTeammate(from);
                                    if (!far || squad) rp.PlayVoice(frame, far && squad);
                                }
                                break;
                            }
                        case Msg.Vault:
                            {
                                int id = r.ReadInt32();
                                Vault.Find(id)?.Open(false);
                                break;
                            }
                    }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Rago] bad packet: " + e.Message); }
        }

        RemotePlayer GetRemote(ulong id, int seed)
        {
            if (Remotes.TryGetValue(id, out var rp) && rp != null) return rp;
            rp = RemotePlayer.Create(id, seed);
            rp.LastHeard = Time.time;
            Remotes[id] = rp;
            return rp;
        }
    }
}

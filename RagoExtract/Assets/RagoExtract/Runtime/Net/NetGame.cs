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
        float _poseT, _npcT, _voiceT;
        int _dropCounter;
        public bool InRaid;
        readonly HashSet<ulong> _helloSent = new HashSet<ulong>();
        bool _wasHost;

        /// <summary>Microphone audio from VoiceChat (8 kHz u-law frames).</summary>
        public void SendVoice(byte[] frame)
        {
            if (!Live) return;
            var w = W(Msg.Voice);
            w.Write((short)frame.Length); w.Write(frame);
            Send(false);
        }

        enum Msg : byte { Hello = 1, Pose, Fire, HitPlayer, Died, ItemTaken, ItemSpawned, NpcStates, NpcHit, NpcFire, NpcDied, Vault, Extracted, Bye, Voice }

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
            _helloSent.Clear();
            Gun.OnLocalFire = null;
            Ballistics.OnHitActor = null;
            if (NetHub.Voice != null && NetHub.Voice.Ready) NetHub.Voice.Leave();
        }

        void OnDisconnected()
        {
            // the host left: keep playing locally, NPCs fall back to local AI
            foreach (var r in Remotes.Values) if (r != null) Destroy(r.gameObject);
            Remotes.Clear();
            _active = false;
            NpcDirector.I?.BecomeAuthority();
            RagoGame.I?.Player?.Toast("Lost connection - continuing offline");
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

        void SendHello(ulong to)
        {
            var w = W(Msg.Hello);
            w.Write(Profile.Current.name);
            w.Write(Profile.Current.outfitSeed);
            Send(true, to);
        }

        void Update()
        {
            if (!Live) return;
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null) return;
            _poseT -= Time.deltaTime;
            if (_poseT <= 0)
            {
                _poseT = 1f / 15f;
                var w = W(Msg.Pose);
                w.Write(p.Dead);
                V3(w, p.transform.position);
                V3(w, p.Head.position); Q(w, p.Head.rotation);
                V3(w, p.HandL.transform.position); Q(w, p.HandL.transform.rotation);
                V3(w, p.HandR.transform.position); Q(w, p.HandR.transform.rotation);
                V3(w, p.MoveVelocity);
                w.Write(HeldId(p.HandL)); w.Write(HeldId(p.HandR));
                w.Write(GearKey(p));
                Send(false);
            }
            if (InRaid && NetHub.Backend.IsHost && NpcDirector.I != null)
            {
                if (!_wasHost) { _wasHost = true; NpcDirector.I.BecomeAuthority(); } // host migrated to us: run the NPCs
                _npcT -= Time.deltaTime;
                if (_npcT <= 0) { _npcT = 0.1f; SendNpcStates(); }
            }
            _voiceT -= Time.deltaTime;
            if (_voiceT <= 0 && NetHub.Voice != null && NetHub.Voice.Ready) { _voiceT = 0.25f; NetHub.Voice.UpdatePosition(p.Head); }
        }

        static string HeldId(Hand h) => h.Held != null && !h.HeldAsSecondary && h.Held.def != null ? h.Held.def.id : "";

        // -------------------------------------------------------------------------------- gameplay hooks
        void LocalFire(Gun g, Vector3 origin, Vector3 dir)
        {
            var w = W(Msg.Fire);
            w.Write(g.g.id); V3(w, origin); V3(w, dir);
            Send(true);
        }

        void OnHitActor(HitInfo h, HitZone z)
        {
            if (h.attacker != 0)
            {
                // host simulates NPC bullets: forward their damage to the player that got hit
                if (h.attacker >= 1000 && z.owner is RemotePlayer victim && NetHub.Backend.IsHost) NpcDamageToRemote(victim.NetId, h);
                return; // otherwise only report our own hits
            }
            if (z.owner is RemotePlayer rp)
            {
                var w = W(Msg.HitPlayer);
                w.Write(rp.NetId); w.Write(h.damage); w.Write((byte)h.part); V3(w, h.point); V3(w, h.dir); w.Write(h.force);
                Send(true, rp.NetId);
            }
            else if (z.owner is Npc npc && !NetHub.Backend.IsHost)
            {
                var w = W(Msg.NpcHit);
                w.Write(npc.Id); w.Write(h.damage); w.Write((byte)h.part); V3(w, h.point); V3(w, h.dir); w.Write(h.force);
                Send(true, NetHub.Backend.HostId); // to the host
            }
        }

        public void LocalDied(Vector3 pos, List<ItemData> items)
        {
            if (!Live) return;
            var w = W(Msg.Died);
            V3(w, pos);
            w.Write(items.Count);
            int baseId = (int)(NetHub.Backend.LocalId % 1000) * 100000 + 90000;
            for (int i = 0; i < items.Count; i++) { w.Write(baseId + i); w.Write(JsonUtility.ToJson(items[i])); }
            Send(true);
        }

        public int NewDropId() => (int)(NetHub.Backend.LocalId % 1000 + 1) * 100000 + (++_dropCounter % 80000);

        public void OnItemTaken(Item it)
        {
            if (it.netId == 0 || !InRaid) return;
            var w = W(Msg.ItemTaken); w.Write(it.netId); Send(true);
            Items.Remove(it.netId);
            it.netId = 0;
        }

        public void OnItemDropped(Item it)
        {
            if (!InRaid || it is Magazine || it is Backpack) return;
            it.netId = NewDropId();
            Items[it.netId] = it;
            var w = W(Msg.ItemSpawned);
            w.Write(it.netId); w.Write(JsonUtility.ToJson(it.Save())); V3(w, it.transform.position); Q(w, it.transform.rotation);
            Send(true);
        }

        public void Register(Item it) { if (it != null && it.netId != 0) Items[it.netId] = it; }

        public void NpcFire(int id, Vector3 origin, Vector3 dir)
        {
            if (!Live || !NetHub.Backend.IsHost) return;
            var w = W(Msg.NpcFire); w.Write(id); V3(w, origin); V3(w, dir); Send(false);
        }

        public void NpcDied(int id, Vector3 point, Vector3 impulse)
        {
            if (!Live || !NetHub.Backend.IsHost) return;
            var w = W(Msg.NpcDied); w.Write(id); V3(w, point); V3(w, impulse); Send(true);
        }

        public void VaultOpened(int id) { if (!Live) return; var w = W(Msg.Vault); w.Write(id); Send(true); }

        public void NpcDamageToRemote(ulong target, HitInfo h)
        {
            var w = W(Msg.HitPlayer);
            w.Write(target); w.Write(h.damage); w.Write((byte)h.part); V3(w, h.point); V3(w, h.dir); w.Write(h.force);
            Send(true, target);
        }

        void SendNpcStates()
        {
            var list = NpcDirector.I.Npcs;
            var w = W(Msg.NpcStates);
            w.Write((short)list.Count);
            foreach (var n in list)
            {
                w.Write(n.Id);
                V3(w, n.transform.position);
                w.Write(n.Yaw);
                w.Write((byte)n.State);
                V3(w, n.AimPoint);
                w.Write(n.Alive);
            }
            Send(false);
        }

        // -------------------------------------------------------------------------------- receive
        void OnData(ulong from, byte[] data)
        {
            if (data == null || data.Length == 0) return;
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
                                var rp = GetRemote(from, seed);
                                rp.DisplayName = name;
                                if (_helloSent.Add(from)) SendHello(from); // introduce ourselves back
                                break;
                            }
                        case Msg.Pose:
                            {
                                var rp = GetRemote(from, (int)from * 31);
                                bool dead = r.ReadBoolean();
                                var root = V3(r); var hp = V3(r); var hr = Q(r); var lp = V3(r); var lr = Q(r); var rpp = V3(r); var rr = Q(r); var vel = V3(r);
                                string hl = r.ReadString(), hrr = r.ReadString();
                                rp.ApplyPose(dead, root, hp, hr, lp, lr, rpp, rr, vel, hl, hrr);
                                if (r.BaseStream.Position < r.BaseStream.Length) rp.ApplyGear(r.ReadString());
                                break;
                            }
                        case Msg.Fire:
                            {
                                var g = Db.Get<GunDef>(r.ReadString()); var o = V3(r); var d = V3(r);
                                if (g == null) break;
                                Remotes.TryGetValue(from, out var rp);
                                Ballistics.Fire(o, d, g, (int)from + 1, rp != null ? rp.transform : null, rp, true, g.cls == GunClass.LMG);
                                Sfx.Gunshot(g, o, false);
                                if (rp != null) rp.MuzzleFlash(g); else Fx.TracerLine(o, o + d * 50f);
                                break;
                            }
                        case Msg.HitPlayer:
                            {
                                ulong target = r.ReadUInt64(); float dmg = r.ReadSingle(); var part = (BodyPart)r.ReadByte(); var pt = V3(r); var dir = V3(r); float force = r.ReadSingle();
                                if (target != NetHub.Backend.LocalId) break;
                                var v = RagoGame.I.Player.Vitals;
                                v.TakeHit(new HitInfo { damage = dmg, part = part, point = pt, dir = dir, attacker = (int)from + 1, fromNet = true, force = force }, null);
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
                                if (Items.ContainsKey(id)) break;
                                var it = ItemFactory.Spawn(d, pos, rot);
                                if (it != null) { it.netId = id; Items[id] = it; }
                                break;
                            }
                        case Msg.NpcStates:
                            {
                                if (NetHub.Backend.IsHost || NpcDirector.I == null) break;
                                int n = r.ReadInt16();
                                for (int i = 0; i < n; i++)
                                {
                                    int id = r.ReadInt32(); var pos = V3(r); float yaw = r.ReadSingle(); var st = (NpcState)r.ReadByte(); var aim = V3(r); bool alive = r.ReadBoolean();
                                    NpcDirector.I.Get(id)?.ApplyNetState(pos, yaw, st, aim, alive);
                                }
                                break;
                            }
                        case Msg.NpcHit:
                            {
                                int id = r.ReadInt32(); float dmg = r.ReadSingle(); var part = (BodyPart)r.ReadByte(); var pt = V3(r); var dir = V3(r); float force = r.ReadSingle();
                                if (!NetHub.Backend.IsHost || NpcDirector.I == null) break;
                                var npc = NpcDirector.I.Get(id);
                                npc?.TakeHit(new HitInfo { damage = dmg, part = part, point = pt, dir = dir, attacker = (int)from + 1, fromNet = true, force = force }, null);
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
                                NpcDirector.I?.Get(id)?.Die(pt, imp, true);
                                break;
                            }
                        case Msg.Voice:
                            {
                                int n = r.ReadInt16();
                                var frame = r.ReadBytes(n);
                                if (Remotes.TryGetValue(from, out var rp) && rp != null) rp.PlayVoice(frame);
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
            Remotes[id] = rp;
            return rp;
        }
    }
}

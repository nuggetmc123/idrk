using UnityEngine;

namespace Flopzone
{
    /// <summary>Pose layout sent ~15 times a second by each player (floats).</summary>
    public static class PoseData
    {
        public const int FeetX = 0, Yaw = 3, Head = 4, HeadRot = 7, LHand = 11, LRot = 14, RHand = 18, RRot = 21, Flags = 25, LGun = 26, RGun = 27, Speed = 28;
        /// <summary>The outfit, split in two halves (each stored +1 so 0 means "not sent", as with bots).</summary>
        public const int LookLo = 29, LookHi = 30;
        public const int Length = 31;

        public static void PutLook(float[] p, int code) { p[LookLo] = (code & 0x1FFF) + 1; p[LookHi] = (code >> 13) + 1; }
        public static int GetLook(float[] p)
        {
            if (p.Length <= LookHi || p[LookLo] < 0.5f || p[LookHi] < 0.5f) return -1;
            return (Mathf.RoundToInt(p[LookLo]) - 1) | (Mathf.RoundToInt(p[LookHi]) - 1) << 13;
        }
        public const int FlagCrouch = 1, FlagSprint = 2, FlagGrounded = 4;

        public static Vector3 V(float[] p, int i) => new Vector3(p[i], p[i + 1], p[i + 2]);
        public static Quaternion Q(float[] p, int i)
        {
            var q = new Quaternion(p[i], p[i + 1], p[i + 2], p[i + 3]);
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m < 0.001f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }
        public static void Put(float[] p, int i, Vector3 v) { p[i] = v.x; p[i + 1] = v.y; p[i + 2] = v.z; }
        public static void Put(float[] p, int i, Quaternion q) { p[i] = q.x; p[i + 1] = q.y; p[i + 2] = q.z; p[i + 3] = q.w; }
    }

    /// <summary>Another player (or a bot) as seen on this headset.</summary>
    public class RemotePlayer : MonoBehaviour
    {
        public string id;
        public string playerName;
        public int team = -1;
        public bool isBot;
        public bool alive = true;
        public int hp = 100;
        public Avatar avatar;
        int look;
        bool lobbyHidden;

        TextMesh nameTag;
        Transform hpBar;
        Transform tagRoot;
        TextMesh chatBubble;
        float chatUntil;
        float[] from, to, shown;
        float lerpT = 1f, lerpDur = 1f / 15f;
        float lastPoseTime;
        readonly GunModel[] heldGuns = new GunModel[2];
        readonly int[] heldGunIdx = { -1, -1 };

        public Vector3 FeetPos => shown != null ? PoseData.V(shown, PoseData.FeetX) : transform.position;
        public Vector3 HeadPos => avatar ? avatar.head.position : transform.position + Vector3.up * 1.6f;
        public Vector3 ChestPos => avatar ? avatar.torso.position : transform.position + Vector3.up * 1.2f;

        public static RemotePlayer Create(Transform parent, string id, string name, int team, bool bot)
        {
            var go = new GameObject("player:" + name);
            go.transform.SetParent(parent, false);
            var rp = go.AddComponent<RemotePlayer>();
            rp.id = id;
            rp.isBot = bot;
            rp.look = bot ? -1 : 0;   // bots dress at random; people's outfits arrive with their pose
            rp.playerName = name;
            rp.Rebuild(team);
            return rp;
        }

        public void Rebuild(int newTeam)
        {
            team = newTeam;
            if (avatar) Destroy(avatar.gameObject);
            if (tagRoot) Destroy(tagRoot.gameObject);
            heldGuns[0] = heldGuns[1] = null;
            heldGunIdx[0] = heldGunIdx[1] = -1;
            avatar = Avatar.Build(transform, id, Avatar.TeamColor(team), look);
            lobbyHidden = false;   // worked out again next frame
            foreach (var c in avatar.GetComponentsInChildren<Collider>())
            {
                var hb = c.gameObject.AddComponent<Hitbox>();
                hb.owner = this;
                hb.head = c.transform == avatar.head;
            }
            tagRoot = Prims.Empty("tag", transform).transform;
            nameTag = UIKit.Text(tagRoot, playerName, Vector3.zero, 0.09f, team < 0 ? Color.white : Color.Lerp(Avatar.TeamColor(team), Color.white, 0.5f));
            var barBack = UIKit.Quad(tagRoot, new Vector3(0, -0.08f, 0.001f), new Vector2(0.4f, 0.035f), new Color(0, 0, 0, 0.6f));
            barBack.name = "hpBack";
            hpBar = UIKit.Quad(tagRoot, new Vector3(0, -0.08f, 0), new Vector2(0.38f, 0.022f), new Color(0.3f, 0.9f, 0.35f, 0.95f), 2997).transform;
            chatBubble = UIKit.Text(tagRoot, "", new Vector3(0, 0.16f, 0), 0.07f, Color.white);
            if (shown != null) ApplyPose(shown, true);
            else avatar.SetVisible(false);   // shown once the first pose arrives
        }

        public void SetName(string n)
        {
            playerName = n;
            if (nameTag) nameTag.text = n;
            gameObject.name = "player:" + n;
        }

        public void ApplyPose(float[] p, bool snap = false)
        {
            if (p == null || p.Length < PoseData.Length) return;
            if (shown == null && avatar != null && alive) avatar.SetVisible(true);
            if (shown == null || snap)
            {
                if (shown == null || shown.Length != p.Length) { shown = new float[p.Length]; from = new float[p.Length]; to = new float[p.Length]; }
                System.Array.Copy(p, shown, p.Length);
                System.Array.Copy(p, from, p.Length);
                System.Array.Copy(p, to, p.Length);
                lerpT = 1f;
            }
            else
            {
                from = (float[])shown.Clone();
                to = (float[])p.Clone();
                float gap = Time.time - lastPoseTime;
                lerpDur = Mathf.Clamp(gap, 0.03f, 0.25f);
                lerpT = 0f;
                // a big jump (respawn, teleport) shouldn't slide across the map
                if (Vector3.Distance(PoseData.V(from, 0), PoseData.V(to, 0)) > 4f) from = (float[])to.Clone();
            }
            lastPoseTime = Time.time;
            int newLook = PoseData.GetLook(p);
            if (newLook >= 0 && newLook != look) { look = newLook; if (avatar) avatar.ApplyLook(look); }
            SetGun(0, Mathf.RoundToInt(p[PoseData.LGun]));
            SetGun(1, Mathf.RoundToInt(p[PoseData.RGun]));
        }

        void SetGun(int h, int idx)
        {
            if (heldGunIdx[h] == idx) return;
            heldGunIdx[h] = idx;
            if (heldGuns[h]) Destroy(heldGuns[h].gameObject);
            heldGuns[h] = null;
            var def = GunCatalog.Get(idx);
            if (def == null || avatar == null) return;
            heldGuns[h] = GunModel.Build(def, avatar.hand[h]);
            var mag = GunModel.BuildMag(def, heldGuns[h].magwell);
            mag.name = "mag";
        }

        public GunModel HeldGun(int h) => heldGuns[h];

        void Update()
        {
            if (shown == null || avatar == null) return;
            if (lerpT < 1f)
            {
                lerpT = Mathf.Min(1f, lerpT + Time.deltaTime / lerpDur);
                for (int i = 0; i < shown.Length && i < to.Length; i++) shown[i] = Mathf.LerpUnclamped(from[i], to[i], lerpT);
                shown[PoseData.Yaw] = Mathf.LerpAngle(from[PoseData.Yaw], to[PoseData.Yaw], lerpT);
            }
            if (alive)
            {
                avatar.Pose(PoseData.V(shown, PoseData.FeetX), shown[PoseData.Yaw],
                    PoseData.V(shown, PoseData.Head), PoseData.Q(shown, PoseData.HeadRot),
                    PoseData.V(shown, PoseData.LHand), PoseData.Q(shown, PoseData.LRot),
                    PoseData.V(shown, PoseData.RHand), PoseData.Q(shown, PoseData.RRot),
                    shown[PoseData.Speed], ((int)shown[PoseData.Flags] & PoseData.FlagCrouch) != 0);
            }
            // inside the lobby helicopter you only see your own party (everyone gets their own private cabin)
            bool hide = Game.I != null && Game.I.HiddenInLobby(this, PoseData.V(shown, PoseData.FeetX));
            if (hide != lobbyHidden)
            {
                lobbyHidden = hide;
                avatar.SetVisible(!hide && alive);
            }
            // name tag faces the camera
            var cam = Camera.main;
            if (cam && tagRoot)
            {
                tagRoot.position = HeadPos + Vector3.up * 0.32f;
                tagRoot.rotation = Quaternion.LookRotation(tagRoot.position - cam.transform.position);
                float k = Mathf.Clamp01(hp / 100f);
                hpBar.localScale = new Vector3(0.38f * k, 0.022f, 1f);
                hpBar.localPosition = new Vector3(-0.19f * (1 - k), -0.08f, 0);
                hpBar.GetComponent<Renderer>().sharedMaterial.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.35f), k);
                tagRoot.gameObject.SetActive(alive && !lobbyHidden);
                if (chatBubble.text.Length > 0 && Time.time > chatUntil) chatBubble.text = "";
            }
        }

        public void ShowChat(string msg)
        {
            if (!chatBubble) return;
            chatBubble.text = UIKit.Wrap(msg, 1.4f, 0.07f);
            chatUntil = Time.time + 6f;
        }

        /// <summary>Muzzle flash, sound and tracer for a shot this player fired.</summary>
        public void PlayShot(int hand, Vector3 from, Vector3 to, int gunIdx)
        {
            var def = GunCatalog.Get(gunIdx);
            var gm = heldGuns[Mathf.Clamp(hand, 0, 1)];
            if (gm != null) Fx.I.MuzzleFlash(gm.muzzle, def != null && def.cls == GunClass.Shotgun ? 1.5f : 1f);
            if (def != null) AudioManager.Sfx(def.sound, from, 1f, def.pitch);
            Fx.I.Tracer(from, to);
        }

        public void Die(Vector3 dir, bool headshot)
        {
            if (!alive) return;
            alive = false;
            hp = 0;
            var vel = Vector3.zero;
            if (to != null && from != null) vel = (PoseData.V(to, 0) - PoseData.V(from, 0)) / Mathf.Max(0.05f, lerpDur);
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
            avatar.SpawnRagdoll(Vector3.ClampMagnitude(vel, 6f), dir.normalized * (headshot ? 45f : 30f) + Vector3.up * 8f, headshot);
            if (headshot) Fx.I.Blood(avatar.head.position, dir.normalized, 2f);
            avatar.SetVisible(false);
            for (int h = 0; h < 2; h++) if (heldGuns[h]) heldGuns[h].gameObject.SetActive(false);
        }

        public void Revive()
        {
            alive = true;
            hp = 100;
            avatar.SetVisible(!lobbyHidden);
            for (int h = 0; h < 2; h++) if (heldGuns[h]) heldGuns[h].gameObject.SetActive(true);
            if (to != null) { shown = (float[])to.Clone(); from = (float[])to.Clone(); lerpT = 1f; }
        }
    }
}

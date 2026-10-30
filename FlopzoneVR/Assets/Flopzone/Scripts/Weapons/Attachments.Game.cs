using UnityEngine;

namespace Flopzone
{
    /// <summary>Which attachments fit which gun, what they do, and what you've put on each of your guns.</summary>
    public static partial class Attachments
    {
        /// <summary>Can this gun take that option? (Option 0 always fits.)</summary>
        public static bool Fits(GunDef def, Slot s, int v)
        {
            if (def == null) return false;
            if (v == 0) return true;
            bool shotgun = def.cls == GunClass.Shotgun;
            switch (s)
            {
                case Slot.Optic:
                    return def.cls != GunClass.Sniper && def.cls != GunClass.Pistol && GunShapes.HasPoint(def.id, "optic");
                case Slot.Muzzle:
                    // revolvers and shotguns can't, and these two have their suppressor built in
                    return !def.revolver && !shotgun && def.id != "whisper" && def.id != "spitfire";
                case Slot.Under:
                    if (!GunShapes.HasPoint(def.id, "under")) return false;
                    return def.cls != GunClass.Pistol || v == 2;   // a pistol takes a laser, not a grip
                case Slot.Mag:
                    return !def.revolver && !shotgun && def.id != "hmr" && def.id != "bullhorn";
            }
            return false;
        }

        /// <summary>Drop anything that doesn't fit this gun (or isn't unlocked, when <paramref name="checkUnlocks"/>).</summary>
        public static int Clean(GunDef def, int code, bool checkUnlocks = false)
        {
            if (code <= 0 || code >= MaxCode) return 0;
            for (int s = 0; s < 4; s++)
            {
                int v = Get(code, (Slot)s);
                if (v >= Options((Slot)s) || !Fits(def, (Slot)s, v) || (checkUnlocks && !Progression.HasAttachment((Slot)s, v)))
                    code = With(code, (Slot)s, 0);
            }
            return code;
        }

        /* ---------------- what they do ---------------- */

        public static int MagSize(GunDef def, int code) =>
            Get(code, Slot.Mag) == 1 ? Mathf.RoundToInt(def.magSize * 1.5f) : def.magSize;

        public static float RecoilMult(int code) =>
            (Get(code, Slot.Muzzle) == 2 ? 0.6f : 1f) * (Get(code, Slot.Under) == 1 ? 0.8f : 1f);

        public static float SpreadMult(int code) =>
            Get(code, Slot.Under) == 1 ? 0.85f : Get(code, Slot.Under) == 2 ? 0.75f : 1f;

        public static bool Suppressed(GunDef def, int code) =>
            Get(code, Slot.Muzzle) == 1 || def.id == "whisper";

        public static string Describe(Slot s, int v)
        {
            switch (s)
            {
                case Slot.Optic: return v == 0 ? "the gun's own sight" : v == 3 ? "zooms in" : "a clear dot to aim with";
                case Slot.Muzzle: return v == 1 ? "quiet shots, no flash" : v == 2 ? "40% less kick" : "nothing on the barrel";
                case Slot.Under: return v == 1 ? "less kick and spread" : v == 2 ? "tighter spread, shows a beam" : "nothing under the barrel";
                default: return v == 1 ? "50% more rounds" : "normal size";
            }
        }

        /* ---------------- saved per gun ---------------- */

        /// <summary>What's on this gun of yours (only what fits and is unlocked).</summary>
        public static int For(int gunIndex)
        {
            var a = Save.Settings.attach;
            var def = GunCatalog.Get(gunIndex);
            if (def == null || a == null || gunIndex >= a.Length) return 0;
            return Clean(def, a[gunIndex], true);
        }

        public static void Set(int gunIndex, int code)
        {
            var st = Save.Settings;
            if (st.attach == null || st.attach.Length < GunCatalog.All.Length)
            {
                var n = new int[GunCatalog.All.Length];
                if (st.attach != null) System.Array.Copy(st.attach, n, st.attach.Length);
                st.attach = n;
            }
            st.attach[gunIndex] = code;
            Save.Store();
        }

        /* ---------------- over the network: one number for gun + attachments ---------------- */

        public static int Pack(int gunIndex, int code) => gunIndex < 0 ? -1 : gunIndex + 100 * code;

        public static int GunOf(int packed) => packed < 0 ? -1 : packed % 100;

        public static int CodeOf(int packed) => packed < 0 ? 0 : packed / 100;
    }
}

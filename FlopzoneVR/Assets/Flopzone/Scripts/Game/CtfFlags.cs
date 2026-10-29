using UnityEngine;

namespace Flopzone
{
    /// <summary>The two flags in Capture the Flag: where they are, and touching them.</summary>
    public class CtfFlags : MonoBehaviour
    {
        readonly Transform[] flags = new Transform[2];
        readonly Transform[] cloth = new Transform[2];
        float nextSend;

        public static CtfFlags Create(World w)
        {
            var go = new GameObject("CtfFlags");
            go.transform.SetParent(w.dynamicRoot, false);
            var c = go.AddComponent<CtfFlags>();
            for (int t = 0; t < 2; t++)
            {
                var root = Prims.Empty("flag" + t, go.transform, w.flagBase[t]).transform;
                var col = Avatar.TeamColor(t);
                Prims.Cyl(root, new Vector3(0, 1.2f, 0), 0.06f, 2.4f, Prims.Lit(new Color(0.85f, 0.85f, 0.85f), 0.7f, 0.8f), Vector3.up);
                var cl = Prims.Empty("cloth", root, new Vector3(0.45f, 2.05f, 0)).transform;
                Prims.Box(cl, Vector3.zero, new Vector3(0.9f, 0.55f, 0.03f), Prims.Emissive(col, 0.6f), false);
                Prims.Sphere(root, new Vector3(0, 2.45f, 0), 0.12f, Prims.Emissive(Color.white, 1.5f));
                c.flags[t] = root;
                c.cloth[t] = cl;
            }
            return c;
        }

        void Update()
        {
            var g = Game.I;
            var s = g.session;
            if (s.Flags == null || s.Flags.Length < 2 || g.world == null) return;
            var rig = PlayerRig.I;
            for (int t = 0; t < 2; t++)
            {
                var f = s.Flags[t];
                Vector3 pos = g.world.flagBase[t];
                if (f.state == "carried" && f.carrier != null)
                {
                    if (f.carrier == s.MyId && rig != null) pos = rig.body.position - rig.body.forward * 0.25f - Vector3.up * 0.4f;
                    else
                    {
                        var rp = g.FindRemote(f.carrier);
                        if (rp != null) pos = rp.ChestPos - rp.avatar.torso.forward * 0.25f - Vector3.up * 0.9f;
                    }
                }
                else if (f.state == "dropped" && f.pos.HasValue) pos = g.world.Ground(f.pos.Value);
                flags[t].position = Vector3.Lerp(flags[t].position, pos, f.state == "carried" ? 1f : Time.deltaTime * 10f);
                cloth[t].localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 3f + t) * 15f, 0);
            }

            // touching flags
            if (rig == null || !rig.Alive || s.State != "live" || s.MyTeam < 0 || Time.time < nextSend) return;
            var feet = rig.transform.position;
            int mine = s.MyTeam, enemy = 1 - s.MyTeam;
            var ef = s.Flags[enemy];
            var mf = s.Flags[mine];
            if ((ef.state == "home" || ef.state == "dropped") && Flat(flags[enemy].position - feet) < 1.4f) Send("touch", enemy);
            else if (mf.state == "dropped" && Flat(flags[mine].position - feet) < 1.4f) Send("touch", mine);
            else if (ef.state == "carried" && ef.carrier == s.MyId && mf.state == "home" && Flat(g.world.flagBase[mine] - feet) < 2.5f) Send("cap", mine);
        }

        void Send(string a, int team)
        {
            nextSend = Time.time + 0.5f;
            Game.I.session.SendFlag(a, team);
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
    }
}

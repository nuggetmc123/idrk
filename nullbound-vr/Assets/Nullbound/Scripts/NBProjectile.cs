using System.Collections.Generic;
using UnityEngine;

namespace Nullbound
{
    /// A shot. Every machine flies its own copy for the visuals; only the server's copy deals damage.
    public class NBProjectile : MonoBehaviour
    {
        static readonly Dictionary<int, NBProjectile> s_Live = new Dictionary<int, NBProjectile>();

        NBPlayer m_Owner;
        MoveDef m_Def;
        int m_Id;
        bool m_Sim;
        Vector3 m_Dir;
        float m_Travelled;
        Color m_Color;

        public static void Spawn(NBPlayer owner, MoveDef def, Color color, int id, Vector3 pos, Vector3 dir, bool simulate)
        {
            var g = new GameObject("Projectile");
            g.transform.position = pos;
            g.transform.rotation = Quaternion.LookRotation(dir);
            var p = g.AddComponent<NBProjectile>();
            p.m_Owner = owner; p.m_Def = def; p.m_Id = id; p.m_Sim = simulate; p.m_Dir = dir.normalized; p.m_Color = color;
            float r = Mathf.Max(0.15f, def.radius);
            var shape = def.radius >= 1.3f ? PrimitiveType.Cube : PrimitiveType.Sphere;
            NBMat.Prim(shape, g.transform, Vector3.zero, Vector3.one * r * 1.1f, NBMat.Lit(color, 0.4f, 1.5f));
            NBMat.Prim(PrimitiveType.Sphere, g.transform, Vector3.zero, Vector3.one * r * 2f, NBMat.Glow(new Color(color.r, color.g, color.b, 0.3f)));
            NBMat.Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 0, -r * 1.5f), new Vector3(r * 0.6f, r * 0.6f, r * 3f), NBMat.Glow(new Color(color.r, color.g, color.b, 0.25f)));
            s_Live[id] = p;
        }

        public static void End(int id, Vector3 pos, float size, Color c)
        {
            if (s_Live.TryGetValue(id, out var p) && p != null)
            {
                s_Live.Remove(id);
                Destroy(p.gameObject);
            }
            NBFX.Burst(pos, c, Mathf.Max(0.8f, size * 0.5f), 10);
            if (size > 1.5f) { NBFX.Ring(pos, size, c); NBAudio.Play(NBAudio.Boom, pos, 0.8f, 1.2f); }
        }

        void OnDestroy()
        {
            if (s_Live.TryGetValue(m_Id, out var p) && p == this) s_Live.Remove(m_Id);
        }

        void Update()
        {
            float step = m_Def.speed * Time.deltaTime;
            var from = transform.position;
            var to = from + m_Dir * step;
            m_Travelled += step;
            transform.Rotate(0, 0, 540f * Time.deltaTime, Space.Self);

            if (m_Sim && m_Owner != null && m_Owner.IsSpawned)
            {
                // fighters
                foreach (var p in m_Owner.Enemies())
                {
                    // check a few points along the step so fast shots don't skip people
                    for (int i = 0; i <= 2; i++)
                    {
                        var pt = Vector3.Lerp(from, to, i / 2f);
                        if (p.DistToBody(pt) <= m_Def.radius)
                        {
                            Impact(pt, p);
                            return;
                        }
                    }
                }
                // walls / ground
                if (Physics.SphereCast(from, Mathf.Min(0.3f, m_Def.radius * 0.5f), m_Dir, out var hit, step, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    Impact(hit.point, null);
                    return;
                }
                if (m_Travelled > m_Def.range)
                {
                    Impact(to, null);
                    return;
                }
            }
            else if (m_Travelled > m_Def.range + 6f)
            {
                Destroy(gameObject);
                return;
            }
            transform.position = to;
        }

        void Impact(Vector3 pos, NBPlayer direct)
        {
            var push = m_Dir; push.y = 0; push = push.sqrMagnitude < 1e-4f ? Vector3.forward : push.normalized;
            if (direct != null)
                NBPlayer.ApplyHit(direct, m_Owner, m_Def.damage, push * m_Def.knock + Vector3.up * m_Def.lift, m_Def.stun, m_Def.unblockable, m_Def.guardBreak, m_Def.damage >= 8f, pos);
            if (m_Def.explode > 0f)
            {
                foreach (var p in new List<NBPlayer>(m_Owner.Enemies()))
                {
                    if (p == direct || p.DistToBody(pos) > m_Def.explode) continue;
                    var away = p.Foot - pos; away.y = 0; away = away.sqrMagnitude < 0.01f ? push : away.normalized;
                    NBPlayer.ApplyHit(p, m_Owner, m_Def.damage * 0.7f, away * m_Def.knock + Vector3.up * m_Def.lift, m_Def.stun, m_Def.unblockable, false, true, p.Center);
                }
            }
            m_Owner.ProjectileEndRpc(m_Id, pos, m_Def.explode);
            Destroy(gameObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// CONCERT live event: the Market's garage door rolls up and the stage is in there - a DJ on the decks, a flashing LED
    /// wall, speaker stacks, moving spotlights with beams and sweeping lasers, all on the beat of a looping dance track.
    /// The music runs off the world clock, so everyone in the Market hears (and sees) the same beat.
    /// </summary>
    public class ConcertStage : MonoBehaviour
    {
        static ConcertStage _i;
        public const float Bpm = 124f;

        /// <summary>Up while a concert is on and you're in the Market; gone otherwise.</summary>
        public static void Tick()
        {
            var g = RagoGame.I;
            bool want = LiveEvent.Is(LiveEvent.Concert) && g != null && g.Map is MarketMap;
            if (want && _i == null) Build(g.Map.transform);
            else if (!want && _i != null) { Compat.KillWithMeshes(_i.gameObject); _i = null; }
        }

        AudioSource _music;
        readonly List<Renderer> _tiles = new List<Renderer>();
        Material[] _pal;
        readonly List<Transform> _heads = new List<Transform>(), _lasers = new List<Transform>();
        readonly List<Light> _spots = new List<Light>();
        readonly List<Renderer> _beams = new List<Renderer>();
        Material[] _beamMats;
        Transform _deckL, _deckR;
        HumanRig _dj;
        int _lastBeat = -1;

        static void Build(Transform map)
        {
            var go = new GameObject("ConcertStage");
            go.transform.SetParent(map, false);
            go.transform.localPosition = new Vector3(-2f, 0f, -16.4f);   // in the garage behind the roller door (it rolls up for the show), facing the hall (+z)
            _i = go.AddComponent<ConcertStage>();
            _i.Make();
        }

        void Make()
        {
            var t = transform;
            var metal = Mats.Lit("stage_metal", Color.white, 0.8f, 0.5f, Mats.Metal, null, 0.2f, 0.2f);
            var black = Mats.Lit("stage_black", Color.white, 0.1f, 0.3f, Mats.Noise, null, 0.2f, 0.1f);
            // the deck: a raised platform you can climb onto, steps at the front
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 0.35f, 0), new Vector3(7.4f, 0.7f, 3.4f), new Color(0.12f, 0.12f, 0.13f), 0.02f);
            mb.Sub(0).Box(new Vector3(0, 0.15f, 1.95f), new Vector3(1.6f, 0.3f, 0.5f), new Color(0.15f, 0.15f, 0.16f), 0.01f);
            // truss: two towers and a top beam
            foreach (float x in new[] { -3.6f, 3.6f })
                for (int k = 0; k < 4; k++) mb.Sub(1).Box(new Vector3(x + (k % 2 == 0 ? -0.15f : 0.15f), 3.2f, -1.4f + (k < 2 ? -0.15f : 0.15f)), new Vector3(0.05f, 5f, 0.05f), new Color(0.7f, 0.72f, 0.75f), 0f);
            for (int k = 0; k < 4; k++) mb.Sub(1).Box(new Vector3(0, 5.55f + (k % 2) * 0.3f, -1.55f + (k / 2) * 0.3f), new Vector3(7.6f, 0.05f, 0.05f), new Color(0.7f, 0.72f, 0.75f), 0f);
            // speaker stacks either side, on the floor
            foreach (float x in new[] { -4.4f, 4.4f })
                for (int k = 0; k < 3; k++)
                {
                    mb.Sub(0).Box(new Vector3(x, 0.45f + k * 0.9f, 0.6f), new Vector3(1.0f, 0.86f, 0.8f), new Color(0.05f, 0.05f, 0.06f), 0.01f);
                    mb.Sub(1).Cyl(new Vector3(x, 0.45f + k * 0.9f, 1.0f), new Vector3(x, 0.45f + k * 0.9f, 1.02f), 0.3f, 0.3f, 20, new Color(0.2f, 0.2f, 0.22f));
                }
            // the DJ table
            mb.Sub(0).Box(new Vector3(0, 1.15f, 0.3f), new Vector3(2.2f, 0.9f, 0.8f), new Color(0.08f, 0.08f, 0.09f), 0.01f);
            mb.Build("Stage", t, black, metal);
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.35f, 0); col.size = new Vector3(7.4f, 0.7f, 3.4f);
            var steps = gameObject.AddComponent<BoxCollider>(); steps.center = new Vector3(0, 0.15f, 1.95f); steps.size = new Vector3(1.6f, 0.3f, 0.5f);
            foreach (float x in new[] { -4.4f, 4.4f }) { var sc = gameObject.AddComponent<BoxCollider>(); sc.center = new Vector3(x, 1.35f, 0.6f); sc.size = new Vector3(1f, 2.7f, 0.8f); }
            var tc = gameObject.AddComponent<BoxCollider>(); tc.center = new Vector3(0, 1.15f, 0.3f); tc.size = new Vector3(2.2f, 0.9f, 0.8f);
            // decks (they spin) with a glowing trim
            var deckMat = Mats.Lit("deck", Color.white, 0.5f, 0.7f, null, null, 0f, 0f, false, 0.5f, new Color(0.2f, 0.6f, 1.2f));
            _deckL = Deck(new Vector3(-0.55f, 1.61f, 0.3f), deckMat); _deckR = Deck(new Vector3(0.55f, 1.61f, 0.3f), deckMat);
            // the LED wall behind the DJ: 10 x 5 tiles, each flips colour on the beat
            _pal = new[] {
                Mats.Unlit("led_0", new Color(1f, 0.1f, 0.6f), null, Mats.BlendKind.Alpha, true), Mats.Unlit("led_1", new Color(0.1f, 0.8f, 1f), null, Mats.BlendKind.Alpha, true),
                Mats.Unlit("led_2", new Color(0.6f, 0.2f, 1f), null, Mats.BlendKind.Alpha, true), Mats.Unlit("led_3", new Color(1f, 0.85f, 0.1f), null, Mats.BlendKind.Alpha, true),
                Mats.Unlit("led_4", new Color(0.04f, 0.04f, 0.06f), null, Mats.BlendKind.Alpha, true), Mats.Unlit("led_5", new Color(1f, 1f, 1f), null, Mats.BlendKind.Alpha, true) };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 10; x++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(tile.GetComponent<Collider>());
                    tile.transform.SetParent(t, false);
                    tile.transform.localPosition = new Vector3(-2.97f + x * 0.66f, 1.95f + y * 0.66f, -1.62f);
                    tile.transform.localScale = new Vector3(0.63f, 0.63f, 1f);
                    var r = tile.GetComponent<Renderer>(); r.sharedMaterial = _pal[4]; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _tiles.Add(r);
                }
            // moving heads on the top beam: a spotlight each, with a visible beam
            _beamMats = new[] { Mats.Unlit("beam_0", new Color(1f, 0.2f, 0.7f, 0.12f), null, Mats.BlendKind.Additive), Mats.Unlit("beam_1", new Color(0.2f, 0.8f, 1f, 0.12f), null, Mats.BlendKind.Additive),
                Mats.Unlit("beam_2", new Color(0.7f, 0.3f, 1f, 0.12f), null, Mats.BlendKind.Additive), Mats.Unlit("beam_3", new Color(1f, 0.9f, 0.3f, 0.12f), null, Mats.BlendKind.Additive) };
            for (int k = 0; k < 4; k++)
            {
                var head = new GameObject("MovingHead" + k).transform;
                head.SetParent(t, false); head.localPosition = new Vector3(-2.7f + k * 1.8f, 5.35f, -1.3f);
                new MB().Box(Vector3.zero, new Vector3(0.22f, 0.3f, 0.22f), new Color(0.06f, 0.06f, 0.07f), 0.02f).Build("Body", head, black);
                var beam = new MB().Cyl(Vector3.zero, new Vector3(0, 0, 9f), 0.08f, 1.3f, 16, Color.white, false).Build("Beam", head, _beamMats[k]);
                var br = beam.GetComponent<Renderer>(); br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _beams.Add(br);
                var l = head.gameObject.AddComponent<Light>(); l.type = LightType.Spot; l.range = 18f; l.spotAngle = 22f; l.intensity = 3f; l.shadows = LightShadows.None;
                _heads.Add(head); _spots.Add(l);
            }
            // lasers fanning out from the front of the stage
            var laser = Mats.Unlit("laser_green", new Color(0.2f, 1f, 0.3f, 0.6f), null, Mats.BlendKind.Additive);
            for (int k = 0; k < 6; k++)
            {
                var p = new GameObject("Laser" + k).transform;
                p.SetParent(t, false); p.localPosition = new Vector3(-1.5f + k * 0.6f, 0.75f, 1.6f);
                new MB().Box(new Vector3(0, 0, 9f), new Vector3(0.012f, 0.012f, 18f), Color.white).Build("Beam", p, laser).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lasers.Add(p);
            }
            // the DJ
            try { _dj = HumanRig.Build(Outfit.Fence, 4242, t); } catch (System.Exception e) { Debug.LogWarning("[Rago] concert DJ: " + e.Message); }
            // the music: one looping track, started at the world clock's place in it so everyone's in step
            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = Synth.ConcertLoop(); _music.loop = true;
            _music.spatialBlend = 0.6f; _music.minDistance = 8f; _music.maxDistance = 60f; _music.volume = 0.8f; _music.dopplerLevel = 0f;
            _music.transform.localPosition = Vector3.zero;
            _music.time = (float)(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 % _music.clip.length);
            _music.Play();
        }

        Transform Deck(Vector3 at, Material m)
        {
            var d = new MB().Cyl(Vector3.zero, new Vector3(0, 0.02f, 0), 0.17f, 0.17f, 20, new Color(0.1f, 0.1f, 0.12f)).Box(new Vector3(0.12f, 0.025f, 0), new Vector3(0.03f, 0.01f, 0.01f), Color.white).Build("Deck", transform, m).transform;
            d.localPosition = at;
            return d;
        }

        void Update()
        {
            if (_music == null || _music.clip == null) return;
            float beatLen = 60f / Bpm;
            float bt = _music.time / beatLen;   // beats into the track
            int beat = Mathf.FloorToInt(bt);
            float ph = bt - beat;
            float tt = Time.time;
            // LED wall: a new pattern every beat
            if (beat != _lastBeat)
            {
                _lastBeat = beat;
                int mode = (beat / 8) % 4;
                for (int i = 0; i < _tiles.Count; i++)
                {
                    int x = i % 10, y = i / 10, c;
                    switch (mode)
                    {
                        case 0: c = (x + y + beat) % 4; break;                                     // diagonal stripes marching
                        case 1: c = Mathf.Abs(x - 4.5f) < (beat % 5) + 0.6f ? beat % 4 : 4; break; // a bar opening out from the middle
                        case 2: c = ((x / 2 + y + beat) % 2 == 0) ? (beat / 2) % 4 : 4; break;     // checkerboard
                        default: c = y <= (beat * 3 + x) % 6 ? (x + beat) % 4 : 4; break;          // equaliser bars
                    }
                    if (beat % 16 == 15) c = 5;   // white flash going into the next phrase
                    _tiles[i].sharedMaterial = _pal[c];
                }
                for (int k = 0; k < _spots.Count; k++) { int c = (k + beat / 4) % 4; _spots[k].color = _pal[c].color; _beams[k].sharedMaterial = _beamMats[c]; }
            }
            // moving heads sweep the crowd, punching on the beat
            for (int k = 0; k < _heads.Count; k++)
            {
                float s = Mathf.Sin(tt * 0.9f + k * 1.4f), u = Mathf.Sin(tt * 0.6f + k);
                _heads[k].localRotation = Quaternion.Euler(28f + u * 18f, s * 50f, 0);
                _spots[k].intensity = 1.2f + 3f * Mathf.Exp(-ph * 5f);
            }
            for (int k = 0; k < _lasers.Count; k++)
                _lasers[k].localRotation = Quaternion.Euler(-8f + Mathf.Sin(tt * 1.3f + k * 0.5f) * 10f, (k - 2.5f) * 14f + Mathf.Sin(tt * 2f) * 25f, 0);
            if (_deckL != null) { _deckL.Rotate(0, 200f * Time.deltaTime, 0, Space.Self); _deckR.Rotate(0, 200f * Time.deltaTime, 0, Space.Self); }
            // the DJ bobs on the beat, a hand on each deck - and throws a hand up every few bars
            if (_dj != null)
            {
                var root = transform.TransformPoint(new Vector3(0, 0.7f, -0.35f));
                var q = transform.rotation;
                float bob = Mathf.Abs(Mathf.Sin(bt * Mathf.PI)) * 0.06f;
                var head = root + Vector3.up * (1.62f - bob) + q * new Vector3(0, 0, 0.08f);
                var hq = q * Quaternion.Euler(12f + bob * 200f, Mathf.Sin(bt * 0.5f) * 12f, 0);
                bool up = (beat / 4) % 4 == 3;
                var hl = up ? head + q * new Vector3(-0.25f, 0.45f + Mathf.Sin(bt * Mathf.PI * 2f) * 0.05f, 0.1f) : _deckL.position + Vector3.up * 0.05f;
                var hr = _deckR.position + Vector3.up * 0.05f + q * new Vector3(Mathf.Sin(tt * 6f) * 0.05f, 0, 0);
                _dj.Pose(root, transform.eulerAngles.y, head, hq, hl, q * Quaternion.Euler(up ? -60f : 60f, 0, 0), hr, q * Quaternion.Euler(60f, 0, 0), Vector3.zero, Time.deltaTime);
            }
        }
    }
}

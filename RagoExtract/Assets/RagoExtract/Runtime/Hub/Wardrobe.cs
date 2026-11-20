using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The WARDROBE in your bunker: pick a shirt, trousers, a hat and something for your face, each in a colour, with
    /// the &lt; &gt; buttons. The mannequin next to the board wears your choice straight away, and so do you - under any
    /// armour you put on. Other players see your clothes too.
    /// </summary>
    public class Wardrobe : MonoBehaviour
    {
        Transform _panel, _mannequinRoot;
        HumanRig _mannequin;
        readonly TextMesh[] _values = new TextMesh[7];
        float _rebuildT = -1f;

        public static Wardrobe Create(Transform parent, Vector3 boardPos, float yaw, Vector3 mannequinPos, float mannequinYaw)
        {
            var go = new GameObject("Wardrobe");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = boardPos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var w = go.AddComponent<Wardrobe>();
            w._mannequinRoot = new GameObject("WardrobeMannequin").transform;
            w._mannequinRoot.SetParent(parent, false);
            w._mannequinRoot.localPosition = mannequinPos;
            w._mannequinRoot.localRotation = Quaternion.Euler(0, mannequinYaw, 0);
            w.Build();
            return w;
        }

        void Build()
        {
            _panel = UI.Panel(transform, "Board", Vector3.zero, Quaternion.identity, new Vector2(1.0f, 0.92f), null, true, Skin.Steel);
            UI.Text(_panel, "WARDROBE", new Vector3(0, 0.38f, -0.003f), 0.05f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            string[] labels = { "SHIRT", "COLOUR", "TROUSERS", "COLOUR", "HAT", "COLOUR", "FACE" };
            for (int i = 0; i < 7; i++)
            {
                int row = i;
                float y = 0.27f - i * 0.1f;
                UI.Text(_panel, labels[i], new Vector3(-0.44f, y, -0.003f), 0.026f, i % 2 == 1 && i < 6 ? UI.Dim : Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Button(_panel, "<", new Vector3(-0.1f, y, -0.003f), new Vector2(0.07f, 0.07f), () => Step(row, -1));
                _values[i] = UI.Text(_panel, "", new Vector3(0.12f, y, -0.003f), 0.024f, Color.white, TextAnchor.MiddleCenter);
                UI.Button(_panel, ">", new Vector3(0.34f, y, -0.003f), new Vector2(0.07f, 0.07f), () => Step(row, 1));
            }
            UI.Text(_panel, "worn under your armour - other players see it too", new Vector3(0, -0.42f, -0.003f), 0.016f, UI.Dim, TextAnchor.MiddleCenter);
            Refresh();
            BuildMannequin();
        }

        static int Wrap(int v, int n) => ((v % n) + n) % n;

        void Step(int row, int dir)
        {
            var prof = Profile.Current; if (prof == null) return;
            var l = Cosmetics.Decode(prof.look);
            int nc = Cosmetics.Colors.Length;
            switch (row)
            {
                case 0: l.shirt = Wrap(l.shirt + dir, Cosmetics.Shirts.Length); break;
                case 1: l.shirtCol = Wrap(l.shirtCol + dir, nc); break;
                case 2: l.pants = Wrap(l.pants + dir, Cosmetics.Pants.Length); break;
                case 3: l.pantsCol = Wrap(l.pantsCol + dir, nc); break;
                case 4: l.hat = Wrap(l.hat + dir, Cosmetics.Hats.Length); break;
                case 5: l.hatCol = Wrap(l.hatCol + dir, nc); break;
                default: l.face = Wrap(l.face + dir, Cosmetics.Faces.Length); break;
            }
            prof.look = Cosmetics.Encode(l);
            prof.Save();
            Sfx.UI(Synth.UiClick(), 0.4f);
            Refresh();
            _rebuildT = 0.25f;   // rebuild the bodies a moment after the last click (clicking through colours stays smooth)
        }

        void Refresh()
        {
            var l = Cosmetics.Decode(Cosmetics.Mine);
            string C(int i) => Cosmetics.Colors[i].name;
            _values[0].text = Cosmetics.Shirts[l.shirt]; _values[1].text = l.shirt == 0 ? "-" : C(l.shirtCol);
            _values[2].text = Cosmetics.Pants[l.pants]; _values[3].text = l.pants == 0 ? "-" : C(l.pantsCol);
            _values[4].text = Cosmetics.Hats[l.hat]; _values[5].text = l.hat == 0 ? "-" : C(l.hatCol);
            _values[6].text = Cosmetics.Faces[l.face];
            _values[1].color = l.shirt == 0 ? UI.Dim : Color.Lerp(Cosmetics.Col(l.shirtCol), Color.white, 0.25f);
            _values[3].color = l.pants == 0 ? UI.Dim : Color.Lerp(Cosmetics.Col(l.pantsCol), Color.white, 0.25f);
            _values[5].color = l.hat == 0 ? UI.Dim : Color.Lerp(Cosmetics.Col(l.hatCol), Color.white, 0.25f);
        }

        void Update()
        {
            if (_rebuildT < 0f) return;
            _rebuildT -= Time.deltaTime;
            if (_rebuildT > 0f) return;
            _rebuildT = -1f;
            BuildMannequin();
            var pl = RagoGame.I != null ? RagoGame.I.Player : null;
            if (pl != null && pl.Body != null && !pl.Dead) pl.Body.Revive();   // you, in your new clothes
            if (NetGame.Live) NetGame.I.Announce();                           // and everyone else sees them
        }

        void BuildMannequin()
        {
            if (_mannequin != null && _mannequin.root != null) Destroy(_mannequin.root);
            int seed = Profile.Current != null ? Profile.Current.outfitSeed : 7;
            _mannequin = HumanRig.Build(Outfit.Operator, seed, _mannequinRoot, false, Cosmetics.Mine);
            // standing straight, arms relaxed at its sides
            Vector3 root = _mannequinRoot.position; float yaw = _mannequinRoot.eulerAngles.y;
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 head = root + Vector3.up * 1.64f + q * new Vector3(0, 0, 0.06f);
            for (int i = 0; i < 3; i++)
                _mannequin.Pose(root, yaw, head, q, root + q * new Vector3(-0.24f, 0.86f, 0.02f), q * Quaternion.Euler(90, 0, 0),
                    root + q * new Vector3(0.24f, 0.86f, 0.02f), q * Quaternion.Euler(90, 0, 0), Vector3.zero, 0.02f);
        }
    }
}

using System;
using UnityEngine;

namespace Flopzone
{
    /// <summary>On-screen keyboard for names and chat.</summary>
    public class UIKeyboard : MonoBehaviour
    {
        public string Value { get; private set; } = "";
        public int maxLength = 16;
        public bool allowSpaces;
        public Action<string> onChanged;
        public Action<string> onSubmit;
        bool shift = true;
        UIPanel panel;
        TextMesh preview;
        string placeholder = "";

        static readonly string[] Rows =
        {
            "1234567890",
            "qwertyuiop",
            "asdfghjkl_",
            "zxcvbnm.,?",
        };

        public static UIKeyboard Create(Transform parent, Vector3 localPos, string placeholder, int maxLength, bool allowSpaces, string initial = "")
        {
            var panel = UIPanel.Create("keyboard", new Vector2(0.62f, 0.34f), UIKit.PanelColor, parent);
            panel.transform.localPosition = localPos;
            var kb = panel.gameObject.AddComponent<UIKeyboard>();
            kb.panel = panel;
            kb.maxLength = maxLength;
            kb.allowSpaces = allowSpaces;
            kb.placeholder = placeholder;
            kb.Value = initial ?? "";
            kb.shift = kb.Value.Length == 0;
            kb.Build();
            return kb;
        }

        void Build()
        {
            panel.Clear();
            var bar = UIKit.Quad(panel.content, new Vector3(0, 0.135f, -0.002f), new Vector2(0.58f, 0.045f), new Color(0, 0, 0, 0.6f), 2994);
            bar.name = "field";
            preview = panel.Text("", new Vector2(-0.28f, 0.135f), 0.028f, Color.white, TextAnchor.MiddleLeft);
            Refresh();
            const float key = 0.054f, gap = 0.006f;
            for (int r = 0; r < Rows.Length; r++)
            {
                string row = Rows[r];
                float width = row.Length * (key + gap) - gap;
                for (int i = 0; i < row.Length; i++)
                {
                    char c = row[i];
                    string label = shift ? char.ToUpperInvariant(c).ToString() : c.ToString();
                    float x = -width / 2 + key / 2 + i * (key + gap);
                    float y = 0.085f - r * (key + gap);
                    panel.Button(label, new Vector2(x, y), new Vector2(key, key), () => Type(label), 0.026f);
                }
            }
            float by = 0.085f - 4 * (key + gap);
            panel.Button(shift ? "abc" : "ABC", new Vector2(-0.24f, by), new Vector2(0.1f, key), () => { shift = !shift; Build(); }, 0.022f);
            if (allowSpaces) panel.Button("space", new Vector2(-0.065f, by), new Vector2(0.22f, key), () => Type(" "), 0.022f);
            panel.Button("<- del", new Vector2(0.115f, by), new Vector2(0.11f, key), Backspace, 0.022f);
            var ok = panel.Button("Done", new Vector2(0.245f, by), new Vector2(0.1f, key), () => onSubmit?.Invoke(Value), 0.022f);
            ok.SetColors(new Color(0.2f, 0.55f, 0.25f), UIKit.Good);
        }

        void Type(string s)
        {
            if (Value.Length >= maxLength) { AudioManager.Ui("dry"); return; }
            Value += s;
            if (shift && Value.Length == 1 && !allowSpaces) { shift = false; Build(); }
            Refresh();
            onChanged?.Invoke(Value);
        }

        void Backspace()
        {
            if (Value.Length == 0) return;
            Value = Value.Substring(0, Value.Length - 1);
            Refresh();
            onChanged?.Invoke(Value);
        }

        public void SetValue(string v)
        {
            Value = v ?? "";
            Refresh();
        }

        void Refresh()
        {
            if (preview == null) return;
            bool empty = Value.Length == 0;
            preview.text = empty ? placeholder : Value + "_";
            preview.color = empty ? UIKit.Muted : Color.white;
        }
    }
}

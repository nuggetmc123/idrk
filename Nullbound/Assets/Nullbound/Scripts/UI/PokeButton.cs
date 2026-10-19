using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Nullbound
{
    /// <summary>
    /// A physical 3D button you press by touching it with your VR hand
    /// (or by left-clicking it on desktop). No UI raycasters or event systems needed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PokeButton : MonoBehaviour
    {
        public UnityEvent onPressed = new UnityEvent();
        public float cooldown = 0.35f;
        public TextMeshPro label;

        float nextPress;
        Vector3 restScale;
        float pressedUntil;
        Renderer rend;
        Material mat;
        Color baseColor;
        bool wasDown, wasHover;
        float hoverUntil;

        /// <summary>The laser pointer is on this button (call every frame while it is).</summary>
        public void Hover() => hoverUntil = Time.time + 0.05f;

        void Awake()
        {
            restScale = transform.localScale;
            rend = GetComponent<Renderer>();
            if (rend)
            {
                mat = rend.material; // per-button instance, recolored in place
                baseColor = mat.color;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            // Only actual hands, not other buttons attached to a hand.
            var hand = other.GetComponent<HandPoker>();
            if (hand == null) return;
            if (Press()) hand.Buzz();
        }

        public bool Press()
        {
            if (Time.time < nextPress) return false;
            nextPress = Time.time + cooldown;
            pressedUntil = Time.time + 0.15f;
            AudioManager.Play(AudioManager.Sfx.Click);
            onPressed.Invoke();
            return true;
        }

        public void SetLabel(string text)
        {
            if (label) label.text = text;
        }

        public void SetColor(Color c)
        {
            baseColor = c;
            if (mat) NBUI.SetColor(mat, c);
        }

        void Update()
        {
            bool down = Time.time < pressedUntil;
            bool hover = Time.time < hoverUntil;
            Vector3 target = down ? new Vector3(restScale.x, restScale.y, restScale.z * 0.5f) : restScale;
            transform.localScale = Vector3.Lerp(transform.localScale, target, 0.5f);
            if (mat && (down != wasDown || hover != wasHover))
            {
                wasDown = down;
                wasHover = hover;
                NBUI.SetColor(mat, down ? Color.Lerp(baseColor, Color.white, 0.5f) : hover ? Color.Lerp(baseColor, Color.white, 0.25f) : baseColor);
            }
        }

        /// <summary>Builds a button: a box with a label on its front face (the side facing -Z).</summary>
        public static PokeButton Create(Transform parent, string name, string text, Vector3 localPos, Vector3 size,
            Color color, Action onPress, float fontSize = 0.25f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<Collider>().isTrigger = true;
            NBUI.Paint(go.GetComponent<Renderer>(), color);

            var button = go.AddComponent<PokeButton>();
            if (onPress != null) button.onPressed.AddListener(() => onPress());

            // Label is a sibling-sized child; undo the button's scale so text isn't stretched.
            var tmp = NBUI.Label(go.transform, text, new Vector3(0f, 0f, -0.51f), fontSize, Color.black,
                new Vector2(size.x * 0.95f, size.y * 0.95f));
            tmp.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
            tmp.rectTransform.sizeDelta = new Vector2(size.x * 0.95f, size.y * 0.95f);
            button.label = tmp;
            return button;
        }
    }
}

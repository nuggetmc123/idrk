using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Flopzone
{
    /// <summary>A UI root with a background, built and cleared in code.</summary>
    public class UIPanel : MonoBehaviour
    {
        public Vector2 size;
        public Transform content;

        public static UIPanel Create(string name, Vector2 size, Color bg, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            var p = go.AddComponent<UIPanel>();
            p.size = size;
            var back = UIKit.Quad(go.transform, new Vector3(0, 0, 0.004f), size, bg, 2990);
            back.name = "background";
            // lets lasers land on the panel (and stops a stray trigger pull firing your gun)
            back.layer = Layers.UI;
            var hit = back.AddComponent<BoxCollider>();
            hit.size = new Vector3(1f, 1f, 0.01f);
            hit.isTrigger = true;   // lasers see it, bodies walk through it
            var border = UIKit.Quad(go.transform, new Vector3(0, 0, 0.006f), size + new Vector2(0.012f, 0.012f), UIKit.Accent * 0.8f, 2989);
            border.name = "border";
            p.content = Prims.Empty("content", go.transform).transform;
            return p;
        }

        public void Clear()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        }

        public TextMesh Text(string text, Vector2 pos, float h, Color? color = null, TextAnchor anchor = TextAnchor.MiddleCenter)
            => UIKit.Text(content, text, new Vector3(pos.x, pos.y, -0.003f), h, color ?? Color.white, anchor);

        public UIButton Button(string text, Vector2 pos, Vector2 size, Action click, float textHeight = 0f)
            => UIButton.Create(content, text, new Vector3(pos.x, pos.y, -0.004f), size, click, textHeight);

        /// <summary>Put the panel in front of the player's eyes, facing them.</summary>
        public void PlaceInFront(Transform head, float distance, float down = 0.1f)
        {
            var fwd = head.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 0.01f) fwd = head.up;
            fwd.Normalize();
            transform.position = head.position + fwd * distance - Vector3.up * down;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The lift between your bunker and the Market.
    /// Press CALL: the doors slide open. Walk in: the doors close behind you and the car goes.
    /// You arrive inside the other lift, the bell rings and the doors open.
    /// Local space: origin on the wall line at floor level, the room is towards -z, the car is behind the wall (+z).
    /// </summary>
    public class Elevator : MonoBehaviour
    {
        enum State { Closed, Opening, Open, Closing, Departing, Arriving }

        public static readonly List<Elevator> All = new List<Elevator>();
        const float DoorTime = 0.9f, Inside = 0.45f, HalfW = 1.15f, Depth = 2.4f;

        State _state = State.Closed;
        float _door, _t, _openFor;     // 0 closed .. 1 open
        Transform _left, _right;
        TextMesh _indicator, _status;
        System.Action _go;
        bool _needExit;                // just arrived: don't ride back until you've stepped out once
        string _dest;

        public Vector3 ArrivePosition => transform.TransformPoint(new Vector3(0, 0.05f, Depth * 0.55f));
        public float ArriveYaw => transform.eulerAngles.y + 180f; // facing out through the doors

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static Elevator Create(WorldBuilder b, Transform parent, Vector3 pos, float yaw, string here, string dest, bool goingUp, System.Action go)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            var g = new GameObject("Elevator_" + here);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localRotation = q;
            var e = g.AddComponent<Elevator>();
            e._go = go; e._dest = dest;

            Vector3 W(Vector3 local) => pos + q * local;
            var steel = Mats.Lit("lift_steel", new Color(0.55f, 0.56f, 0.57f), 0.85f, 0.6f, Mats.Metal, Mats.MetalN, 0.25f, 0.4f);
            var dark = Mats.Lit("lift_dark", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.5f, Mats.Metal, null, 0.3f, 0.3f);
            var tread = Mats.Lit("lift_floor", new Color(0.35f, 0.36f, 0.37f), 0.8f, 0.45f, Mats.Metal, Mats.MetalN, 0.4f, 0.4f, true, 0.6f);
            float H = 2.6f;
            // the car: floor, ceiling, walls, handrail, light
            b.Box(W(new Vector3(0, -0.1f, 0.15f + Depth / 2)), new Vector3(HalfW * 2 + 0.3f, 0.2f, Depth + 0.1f), tread, Color.white, q, true, Synth.Surface.Metal);
            b.Box(W(new Vector3(0, H + 0.1f, 0.15f + Depth / 2)), new Vector3(HalfW * 2 + 0.3f, 0.2f, Depth + 0.1f), dark, Color.white, q, true, Synth.Surface.Metal);
            b.Box(W(new Vector3(-HalfW - 0.05f, H / 2, 0.15f + Depth / 2)), new Vector3(0.1f, H, Depth + 0.1f), steel, Color.white, q, true, Synth.Surface.Metal);
            b.Box(W(new Vector3(HalfW + 0.05f, H / 2, 0.15f + Depth / 2)), new Vector3(0.1f, H, Depth + 0.1f), steel, Color.white, q, true, Synth.Surface.Metal);
            b.Box(W(new Vector3(0, H / 2, 0.2f + Depth)), new Vector3(HalfW * 2 + 0.2f, H, 0.1f), steel, Color.white, q, true, Synth.Surface.Metal);
            // brushed panels + handrail
            for (int i = 0; i < 3; i++) b.Box(W(new Vector3(-0.75f + i * 0.75f, 1.35f, 0.14f + Depth)), new Vector3(0.68f, 2.2f, 0.01f), dark, new Color(0.6f, 0.6f, 0.6f), q, false);
            b.Box(W(new Vector3(0, 0.95f, 0.08f + Depth)), new Vector3(HalfW * 1.6f, 0.04f, 0.04f), steel, Color.white, q, false);
            foreach (var sx in new[] { -1, 1 }) b.Box(W(new Vector3(sx * (HalfW - 0.04f), 0.95f, 0.15f + Depth / 2)), new Vector3(0.04f, 0.04f, Depth * 0.8f), steel, Color.white, q, false);
            b.Box(W(new Vector3(0, H - 0.03f, 0.15f + Depth / 2)), new Vector3(1.2f, 0.04f, 1.2f), Mats.Lit("lift_lamp", new Color(1f, 0.97f, 0.9f), 0, 0.5f, null, null, 0, 0, false, 0.5f, new Color(2.4f, 2.3f, 2f)), Color.white, q, false);
            b.AddLight(W(new Vector3(0, H - 0.35f, 0.15f + Depth / 2)), new Color(1f, 0.95f, 0.85f), 4f, 1.2f);
            // landing frame on the room side
            foreach (var sx in new[] { -1, 1 }) b.Box(W(new Vector3(sx * 1.0f, 1.25f, -0.27f)), new Vector3(0.18f, 2.5f, 0.1f), steel, Color.white, q, true, Synth.Surface.Metal);
            b.Box(W(new Vector3(0, 2.6f, -0.27f)), new Vector3(2.2f, 0.32f, 0.1f), steel, Color.white, q, true, Synth.Surface.Metal);
            b.Box(W(new Vector3(0, 0.01f, -0.45f)), new Vector3(1.9f, 0.02f, 0.5f), Mats.Lit("lift_sill", new Color(0.95f, 0.75f, 0.1f), 0.3f, 0.4f), Color.white, q, false); // yellow sill

            // sliding landing doors (they slide in front of the wall)
            e._left = e.Door("DoorL"); e._right = e.Door("DoorR");
            e.SetDoors(0);

            // floor indicator above the doors
            var ind = new GameObject("Indicator").transform; ind.SetParent(g.transform, false); ind.localPosition = new Vector3(0, 2.6f, -0.33f);
            e._indicator = UI.Text(ind, here.ToUpper(), Vector3.zero, 0.06f, new Color(1f, 0.55f, 0.15f), TextAnchor.MiddleCenter, FontStyle.Bold);

            // CALL button next to the doors (room side)
            var call = new GameObject("CallPanel").transform; call.SetParent(g.transform, false); call.localPosition = new Vector3(1.55f, 1.25f, -0.34f);
            var cp = UI.Panel(call, "Panel", Vector3.zero, Quaternion.identity, new Vector2(0.42f, 0.5f));
            UI.Text(cp, "LIFT", new Vector3(0, 0.18f, 0), 0.03f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Text(cp, (goingUp ? "up to the " : "down to your ") + dest, new Vector3(0, 0.11f, 0), 0.016f, UI.Dim);
            UI.Button(cp, goingUp ? "CALL (UP)" : "CALL (DOWN)", new Vector3(0, -0.02f, 0), new Vector2(0.3f, 0.12f), e.Call, new Color(0.15f, 0.4f, 0.2f));
            e._status = UI.Text(cp, "", new Vector3(0, -0.16f, 0), 0.016f, Color.white);

            // inside: a button to go now
            var ip = new GameObject("CarPanel").transform; ip.SetParent(g.transform, false); ip.localPosition = new Vector3(0.75f, 1.3f, 0.12f + Depth);
            var p = UI.Panel(ip, "Panel", Vector3.zero, Quaternion.identity, new Vector2(0.4f, 0.4f));
            UI.Text(p, dest.ToUpper(), new Vector3(0, 0.13f, 0), 0.024f, UI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UI.Button(p, goingUp ? "GO UP" : "GO DOWN", new Vector3(0, -0.03f, 0), new Vector2(0.28f, 0.12f), e.GoNow, new Color(0.15f, 0.3f, 0.5f));
            return e;
        }

        Transform Door(string name)
        {
            var mb = new MB(2);
            mb.Sub(0).Box(new Vector3(0, 1.2f, 0), new Vector3(0.92f, 2.4f, 0.05f), new Color(0.62f, 0.63f, 0.64f), 0.006f);
            for (int i = 0; i < 6; i++) mb.Sub(1).Box(new Vector3(0, 0.3f + i * 0.36f, -0.027f), new Vector3(0.8f, 0.004f, 0.004f), new Color(0.35f, 0.35f, 0.36f), 0f);
            var go = mb.Build(name, transform, Mats.Lit("lift_door", Color.white, 0.9f, 0.7f, Mats.Metal, Mats.MetalN, 0.15f, 0.3f), Mats.Lit("lift_dark", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.5f));
            var c = go.AddComponent<BoxCollider>(); c.center = new Vector3(0, 1.2f, 0); c.size = new Vector3(0.92f, 2.4f, 0.06f);
            go.AddComponent<SurfaceTag>().surface = Synth.Surface.Metal;
            return go.transform;
        }

        void SetDoors(float t)
        {
            float s = Mathf.SmoothStep(0, 1, t);
            _left.localPosition = new Vector3(-Mathf.Lerp(0.46f, 1.4f, s), 0, -0.18f);
            _right.localPosition = new Vector3(Mathf.Lerp(0.46f, 1.4f, s), 0, -0.18f);
        }

        // ------------------------------------------------------------------------------------- control
        public void Call()
        {
            if (_state == State.Closed || _state == State.Closing) { Open(); return; }
            if (_state == State.Open) _openFor = 0;
        }

        public void GoNow()
        {
            if (!PlayerInside()) { RagoGame.I?.Player?.Toast("Step into the lift first"); return; }
            if (_state == State.Open || _state == State.Opening) { _needExit = false; Close(); }
        }

        /// <summary>The player just rode here: start inside with the doors shut, then ding and open.</summary>
        public void Arrive()
        {
            _state = State.Arriving; _t = 0; _door = 0; _needExit = true; SetDoors(0);
        }

        void Open()
        {
            _state = State.Opening; _openFor = 0;
            Sfx.Play(Synth.Ding(), transform.position + transform.up * 2.3f, 0.45f, 1f, 0.6f, 10f);
            Sfx.Play(Synth.SlideBack(), transform.position + transform.up * 1.2f, 0.35f, 0.45f, 0.8f, 10f);
        }

        void Close()
        {
            _state = State.Closing;
            Sfx.Play(Synth.SlideForward(), transform.position + transform.up * 1.2f, 0.35f, 0.45f, 0.8f, 10f);
        }

        bool PlayerInside()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null || p.Dead) return false;
            var l = transform.InverseTransformPoint(p.transform.position);
            return Mathf.Abs(l.x) < HalfW && l.z > Inside && l.z < Depth + 0.3f && Mathf.Abs(l.y) < 1.5f;
        }

        bool PlayerInDoorway()
        {
            var p = RagoGame.I != null ? RagoGame.I.Player : null;
            if (p == null) return false;
            var l = transform.InverseTransformPoint(p.transform.position);
            return Mathf.Abs(l.x) < 1.0f && l.z > -0.6f && l.z <= Inside && Mathf.Abs(l.y) < 1.5f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool inside = PlayerInside();
            if (!inside && _state != State.Arriving) _needExit = false;
            switch (_state)
            {
                case State.Opening:
                    _door = Mathf.MoveTowards(_door, 1, dt / DoorTime);
                    if (_door >= 1) { _state = State.Open; _openFor = 0; }
                    break;
                case State.Open:
                    _openFor += dt;
                    // you walked in: the doors close behind you
                    if (inside && !_needExit && _openFor > 0.8f && !PlayerInDoorway()) Close();
                    // nobody came: close again after a while
                    else if (!inside && !PlayerInDoorway() && _openFor > 10f) Close();
                    break;
                case State.Closing:
                    if (PlayerInDoorway()) { _state = State.Opening; break; } // safety edge: don't shut on you
                    _door = Mathf.MoveTowards(_door, 0, dt / DoorTime);
                    if (_door <= 0)
                    {
                        if (inside) { _state = State.Departing; _t = 0; Sfx.Play(Synth.Beep(), transform.position + transform.up * 1.3f, 0.3f, 0.7f, 0.5f, 6f); }
                        else _state = State.Closed;
                    }
                    break;
                case State.Departing:
                    _t += dt;
                    if (!inside) { _state = State.Closed; break; }
                    if (_t > 0.6f) { _state = State.Closed; RagoGame.I.ElevatorArrival = true; _go?.Invoke(); }
                    break;
                case State.Arriving:
                    _t += dt;
                    if (_t > 1.4f) Open();
                    break;
            }
            SetDoors(_door);
            if (_status != null)
                _status.text = _state == State.Open ? (inside ? "doors closing..." : "step in") : _state == State.Departing ? "going to the " + _dest : _state == State.Closed ? "" : "";
        }
    }
}

using TMPro;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// The steering-wheel display and shift lights: gear, speed, an RPM bar, fuel, engine temperature, battery,
    /// brake bias, lap delta, position and lap count, plus a row of shift LEDs (green, red, blue) that flash on the
    /// limiter. Updated at 20 Hz (it's text, no need for more) so it costs nothing.
    /// </summary>
    public class Dashboard : MonoBehaviour
    {
        public Car car;
        TextMeshPro gear, speed, unit, left1, left2, right1, right2, bottom, top;
        MeshRenderer rpmFill;
        MeshRenderer[] leds;
        MaterialPropertyBlock mpb;
        float timer;
        public string deltaText = "", posText = "", lapText = "";
        public Color deltaColor = Color.white;
        public string message;
        public float messageUntil;

        public void Build(Car c)
        {
            car = c;
            var anchor = c.parts.displayAnchor;
            bool round = c.spec.body == BodyStyle.Classic || c.spec.body == BodyStyle.Hillclimb;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            mpb = new MaterialPropertyBlock();
            float w = round ? 0.11f : 0.115f, h = round ? 0.05f : 0.058f;
            // Screen glass.
            var screen = VUI.Quad(transform, "Screen", Vector2.zero, new Vector2(w, h), new Color(0.01f, 0.012f, 0.015f, 1f), 0.0005f, 0.004f);
            screen.gameObject.layer = Layers.Scenery;
            float s = 0.11f; // font scale
            gear = Txt("8", new Vector2(0f, 0.004f), 0.62f * s / 0.11f * 0.11f, Color.white, TextAlignmentOptions.Midline, true);
            speed = Txt("0", new Vector2(0f, -0.019f), 0.13f, Color.white, TextAlignmentOptions.Midline, true);
            unit = Txt(Settings.SpeedUnit, new Vector2(0f, -0.0255f), 0.05f, VUI.TextDim, TextAlignmentOptions.Midline, false);
            left1 = Txt("", new Vector2(-w * 0.46f, 0.012f), 0.075f, Color.white, TextAlignmentOptions.MidlineLeft, true);
            left2 = Txt("", new Vector2(-w * 0.46f, -0.006f), 0.065f, VUI.TextDim, TextAlignmentOptions.MidlineLeft, false);
            right1 = Txt("", new Vector2(w * 0.46f, 0.012f), 0.075f, Color.white, TextAlignmentOptions.MidlineRight, true);
            right2 = Txt("", new Vector2(w * 0.46f, -0.006f), 0.065f, VUI.TextDim, TextAlignmentOptions.MidlineRight, false);
            bottom = Txt("", new Vector2(0f, -h * 0.5f + 0.004f), 0.05f, VUI.TextDim, TextAlignmentOptions.Midline, false);
            top = Txt("", new Vector2(0f, h * 0.5f - 0.009f), 0.07f, VUI.Warn, TextAlignmentOptions.Midline, true);
            // RPM bar along the top of the screen.
            VUI.Quad(transform, "RpmTrack", new Vector2(0f, h * 0.5f - 0.0035f), new Vector2(w * 0.94f, 0.003f), new Color(1f, 1f, 1f, 0.08f), -0.0005f, 0.001f).gameObject.layer = Layers.Scenery;
            rpmFill = VUI.Quad(transform, "RpmFill", new Vector2(0f, h * 0.5f - 0.0035f), new Vector2(w * 0.94f, 0.003f), new Color(0.3f, 0.9f, 0.4f), -0.001f, 0.001f);
            rpmFill.gameObject.layer = Layers.Scenery;
            // Shift LEDs above the screen.
            if (c.spec.hasShiftLights)
            {
                leds = new MeshRenderer[15];
                for (int i = 0; i < leds.Length; i++)
                {
                    float x = Mathf.Lerp(-w * 0.45f, w * 0.45f, i / (leds.Length - 1f));
                    leds[i] = VUI.Quad(transform, "LED", new Vector2(x, h * 0.5f + 0.006f), new Vector2(0.0045f, 0.0045f), Color.black, 0f, 0.0022f);
                    leds[i].gameObject.layer = Layers.Scenery;
                }
            }
            Layers.SetRecursive(gameObject, Layers.Scenery);
        }

        TextMeshPro Txt(string t, Vector2 p, float size, Color c, TextAlignmentOptions a, bool disp)
        {
            var tm = VUI.Label(transform, t, p, size, c, a, disp, 0f, -0.0008f);
            return tm;
        }

        void Update()
        {
            if (car == null) return;
            UpdateLeds();
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.05f;
            gear.text = car.Gear == -1 ? "R" : car.Gear == 0 ? "N" : car.Gear.ToString();
            gear.color = car.Limiter ? VUI.Accent : Color.white;
            speed.text = Settings.Speed(car.SpeedMs);
            unit.text = Settings.SpeedUnit;
            float rpmT = Mathf.Clamp01(car.Rpm / car.spec.limiter);
            rpmFill.transform.localScale = new Vector3(Mathf.Max(0.01f, rpmT), 1f, 1f);
            rpmFill.transform.localPosition = new Vector3(-(1f - rpmT) * 0.5f * 0.94f * 0.115f, rpmFill.transform.localPosition.y, -0.001f);
            VUI.SetColor(rpmFill, rpmT > 0.93f ? new Color(0.35f, 0.5f, 1f) : rpmT > 0.82f ? VUI.Accent : new Color(0.3f, 0.9f, 0.4f));
            left1.text = string.IsNullOrEmpty(posText) ? "" : posText;
            left2.text = lapText;
            right1.text = deltaText;
            right1.color = deltaColor;
            // Bottom line: fuel, temperature, battery, bias, tyre temps.
            float fuelPct = car.Fuel / car.spec.fuelCapacity * 100f;
            string temp = car.EngineTemp > 120f ? "<color=#FF5533>" + car.EngineTemp.ToString("0") + "C</color>" : car.EngineTemp.ToString("0") + "C";
            string batt = car.spec.hybridKw > 0f ? "  ERS " + (car.Battery * 100f).ToString("0") + "%" + (car.Deploying ? "+" : "") : "";
            bottom.text = "FUEL " + fuelPct.ToString("0") + "%  ENG " + temp + batt + "  BB " + (car.setup.Bias(car.spec) * 100f).ToString("0.0");
            float tyre = 0f; foreach (var w in car.wheels) tyre += w.surfaceTemp; tyre /= 4f;
            right2.text = "TYRE " + tyre.ToString("0") + "C";
            if (Time.time < messageUntil && !string.IsNullOrEmpty(message)) top.text = message;
            else if (car.damage != null && car.damage.TotalDamage > 0.15f) { top.text = "DAMAGE"; top.color = VUI.Accent; }
            else if (car.Fuel < car.spec.fuelCapacity * 0.04f && Settings.FuelUse) { top.text = "LOW FUEL"; top.color = VUI.Warn; }
            else top.text = "";
        }

        public void Flash(string msg, float seconds = 2.5f)
        {
            message = msg;
            messageUntil = Time.time + seconds;
            if (top != null) top.color = VUI.Warn;
        }

        void UpdateLeds()
        {
            if (leds == null) return;
            float start = car.spec.redline * 0.78f;
            float t = Mathf.InverseLerp(start, car.spec.redline * 0.985f, car.Rpm);
            int lit = Mathf.RoundToInt(t * leds.Length);
            bool flash = car.Rpm > car.spec.redline * 0.985f && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f;
            for (int i = 0; i < leds.Length; i++)
            {
                Color c;
                if (flash) c = new Color(0.25f, 0.45f, 1f) * 2f;
                else if (i < lit) c = i < 5 ? new Color(0.2f, 1f, 0.3f) * 1.6f : i < 10 ? new Color(1f, 0.15f, 0.1f) * 1.8f : new Color(0.3f, 0.45f, 1f) * 1.8f;
                else c = new Color(0.05f, 0.05f, 0.06f, 1f);
                VUI.SetColor(leds[i], c);
            }
        }
    }

    /// <summary>
    /// Rear-view mirrors: one low-resolution rear camera rendered every other frame into a texture both mirror glasses
    /// show (left half / right half, mirrored). Turned off or lowered in Settings for performance.
    /// </summary>
    public class Mirrors : MonoBehaviour
    {
        Car car;
        Camera rear;
        RenderTexture rt;
        int frame;
        Material[] mats = new Material[2];

        public void Build(Car c)
        {
            car = c;
            int q = Settings.Mirrors;
            if (q == 0 || c.parts == null || c.parts.mirrorGlass == null) return;
            rt = new RenderTexture(q == 1 ? 384 : 640, q == 1 ? 96 : 160, 16) { name = "Mirrors" };
            rt.antiAliasing = 1;
            var go = new GameObject("RearCamera");
            go.transform.SetParent(c.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.95f, -0.2f);
            go.transform.localRotation = Quaternion.Euler(6f, 180f, 0f);
            rear = go.AddComponent<Camera>();
            rear.targetTexture = rt;
            rear.fieldOfView = 26f;
            rear.aspect = 4f;
            rear.nearClipPlane = 0.5f;
            rear.farClipPlane = q == 1 ? 260f : 500f;
            rear.cullingMask = ~((1 << Layers.UI));
            rear.enabled = false;
            rear.stereoTargetEye = StereoTargetEyeMask.None;
            rear.allowMSAA = false;
            for (int i = 0; i < 2; i++)
            {
                var r = c.parts.mirrorGlass[i].GetComponent<Renderer>();
                var shader = Shader.Find("Unlit/Texture");
                mats[i] = new Material(shader) { mainTexture = rt };
                // The rear camera sees the car's left on the right of its image. The glass quads face backwards, which
                // already flips them left-right for the driver, so the left glass simply shows the image's right half.
                mats[i].mainTextureScale = new Vector2(0.5f, 1f);
                mats[i].mainTextureOffset = new Vector2(i == 0 ? 0.5f : 0f, 0f);
                r.sharedMaterial = mats[i];
            }
        }

        void LateUpdate()
        {
            if (rear == null) return;
            frame++;
            if (frame % 2 == 0) rear.Render();
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
            foreach (var m in mats) if (m != null) Destroy(m);
        }
    }
}

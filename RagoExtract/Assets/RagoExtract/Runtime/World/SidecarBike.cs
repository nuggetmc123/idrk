using System.Collections.Generic;
using UnityEngine;

namespace Rago
{
    /// <summary>
    /// The sidecar motorcycle: drives exactly like a golf cart (same physics, getting in and out, multiplayer), but the
    /// rider steers with handlebars and the passenger sits in a sidecar behind a mounted machine gun.
    /// Layout (local): the bike runs down x = -0.3, the sidecar sits at x = +0.5, forward is +z.
    /// </summary>
    public partial class GolfCart
    {
        const float BikeX = -0.3f, CarX = 0.5f;

        /// <summary>The bike and sidecar body (without the road wheels, bars or the gun): 0 paint, 1 dark metal / rubber, 2 leather, 3 chrome.</summary>
        public static MB BikeModel()
        {
            Color paint = new Color(0.24f, 0.29f, 0.2f), dark = new Color(0.09f, 0.09f, 0.1f), leather = new Color(0.3f, 0.19f, 0.11f), chrome = new Color(0.8f, 0.8f, 0.82f);
            var mb = new MB(4);
            float x = BikeX;
            // ---- the motorcycle
            mb.Sub(0).Capsule(new Vector3(x, 0.9f, 0.42f), new Vector3(x, 0.92f, 0.05f), 0.13f, 14, paint);                    // fuel tank
            mb.Sub(3).Cyl(new Vector3(x, 1.02f, 0.3f), new Vector3(x, 1.06f, 0.3f), 0.035f, 0.03f, 10, chrome);              // filler cap
            mb.Sub(1).Box(new Vector3(x, 0.55f, 0.08f), new Vector3(0.28f, 0.32f, 0.42f), dark, 0.04f, 2);                    // engine block
            for (int i = 0; i < 5; i++) mb.Sub(3).Box(new Vector3(x, 0.62f + i * 0.035f, 0.08f), new Vector3(0.34f, 0.012f, 0.3f), chrome * 0.8f, 0.004f);   // cooling fins
            foreach (float s in new[] { -1f, 1f }) mb.Sub(3).Cyl(new Vector3(x + s * 0.17f, 0.62f, 0.12f), new Vector3(x + s * 0.17f, 0.62f, 0.02f), 0.07f, 0.07f, 14, chrome);   // cylinder heads
            mb.Sub(1).Cyl(new Vector3(x, 0.72f, 0.55f), new Vector3(x, 0.45f, -0.6f), 0.035f, 0.035f, 8, dark);            // down tube / backbone
            mb.Sub(1).Cyl(new Vector3(x, 0.85f, 0.45f), new Vector3(x, 0.85f, -0.55f), 0.03f, 0.03f, 8, dark);
            // rider's saddle: a sculpted sprung seat - wide at the back, narrowing to the tank - on chrome coil springs,
            // piped edge, then a pillion pad with a grab strap and a chrome grab rail behind it
            Color pipe = leather * 0.6f; pipe.a = 1f;
            mb.Sub(1).Box(new Vector3(x, 0.86f, -0.28f), new Vector3(0.2f, 0.025f, 0.36f), dark, 0.01f);   // seat pan
            mb.Sub(2).Sphere(new Vector3(x, 0.92f, -0.33f), new Vector3(0.16f, 0.065f, 0.17f), 16, leather);   // the wide rear
            mb.Sub(2).Capsule(new Vector3(x, 0.915f, -0.2f), new Vector3(x, 0.905f, -0.06f), 0.075f, 0.05f, 12, leather);   // the nose, down to the tank
            mb.Sub(2).Sphere(new Vector3(x, 0.955f, -0.36f), new Vector3(0.13f, 0.03f, 0.12f), 14, leather * 1.08f);   // the dished top where you sit
            mb.Sub(2).Push(new Vector3(x, 0.905f, -0.33f), Quaternion.identity, new Vector3(1f, 1f, 1.06f));
            mb.Sub(2).Tube(new Vector3(0, -0.004f, 0), new Vector3(0, 0.004f, 0), 0.163f, 0.155f, 20, pipe);   // piping round the edge
            mb.Pop();
            foreach (float s2 in new[] { -1f, 1f })
                for (int k = 0; k < 4; k++) mb.Sub(3).Tube(new Vector3(x + s2 * 0.08f, 0.75f + k * 0.022f, -0.42f), new Vector3(x + s2 * 0.08f, 0.762f + k * 0.022f, -0.42f), 0.024f, 0.017f, 10, chrome);   // coil springs
            mb.Sub(2).Box(new Vector3(x, 0.92f, -0.62f), new Vector3(0.22f, 0.07f, 0.24f), leather, 0.035f, 2);   // pillion pad
            mb.Sub(2).Box(new Vector3(x, 0.957f, -0.62f), new Vector3(0.2f, 0.006f, 0.03f), pipe, 0.002f);         // its strap
            mb.Sub(3).Cyl(new Vector3(x - 0.12f, 0.93f, -0.5f), new Vector3(x - 0.12f, 0.98f, -0.78f), 0.012f, 0.012f, 8, chrome);   // grab rail
            mb.Sub(3).Cyl(new Vector3(x + 0.12f, 0.93f, -0.5f), new Vector3(x + 0.12f, 0.98f, -0.78f), 0.012f, 0.012f, 8, chrome);
            mb.Sub(3).Cyl(new Vector3(x - 0.12f, 0.98f, -0.78f), new Vector3(x + 0.12f, 0.98f, -0.78f), 0.012f, 0.012f, 8, chrome);
            // footboards for the rider, footpegs for a pillion
            foreach (float s2 in new[] { -1f, 1f })
            {
                mb.Sub(1).Box(new Vector3(x + s2 * 0.24f, 0.36f, 0.1f), new Vector3(0.12f, 0.02f, 0.3f), dark, 0.006f);
                for (int k = 0; k < 4; k++) mb.Sub(1).Box(new Vector3(x + s2 * 0.24f, 0.373f, -0.02f + k * 0.08f), new Vector3(0.11f, 0.006f, 0.02f), new Color(0.2f, 0.2f, 0.2f), 0.002f);   // rubber ribs
                mb.Sub(3).Cyl(new Vector3(x + s2 * 0.1f, 0.45f, -0.55f), new Vector3(x + s2 * 0.2f, 0.45f, -0.55f), 0.012f, 0.012f, 8, chrome);
            }
            foreach (float zc in new[] { 0.78f, -0.72f })
                for (int i = 0; i < 9; i++)
                {
                    float a0 = Mathf.Lerp(15f, 165f, i / 9f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(15f, 165f, (i + 1) / 9f) * Mathf.Deg2Rad, am = (a0 + a1) / 2f;
                    Vector3 c = new Vector3(x, 0.32f, zc), radial = new Vector3(0, Mathf.Sin(am), Mathf.Cos(am)), tangent = new Vector3(0, Mathf.Cos(am), -Mathf.Sin(am));
                    mb.Sub(0).Box(c + radial * 0.4f, new Vector3(0.16f, 0.012f, 0.4f * (a1 - a0) + 0.01f), paint, 0.004f, 1, Quaternion.LookRotation(tangent, radial));
                }
            // exhaust: down the right side, under the sidecar strut, out the back
            mb.Sub(3).Cyl(new Vector3(x + 0.12f, 0.5f, 0.2f), new Vector3(x + 0.14f, 0.32f, -0.1f), 0.03f, 0.03f, 10, chrome);
            mb.Sub(3).Cyl(new Vector3(x + 0.14f, 0.32f, -0.1f), new Vector3(x + 0.14f, 0.36f, -0.85f), 0.035f, 0.045f, 12, chrome);
            // headlamp
            mb.Sub(1).Box(new Vector3(x, 0.62f, -1.02f), new Vector3(0.12f, 0.05f, 0.04f), new Color(0.8f, 0.1f, 0.08f), 0.01f, 1, Quaternion.Euler(-45f, 0, 0));   // tail light, on the rear fender
            // ---- everything that holds it together
            foreach (float s2 in new[] { -1f, 1f })
            {
                // swingarm from behind the gearbox to the rear axle, a shock absorber up to the subframe
                mb.Sub(1).Cyl(new Vector3(x + s2 * 0.08f, 0.42f, -0.18f), new Vector3(x + s2 * 0.08f, 0.32f, -0.72f), 0.022f, 0.02f, 10, dark);
                mb.Sub(3).Cyl(new Vector3(x + s2 * 0.09f, 0.36f, -0.6f), new Vector3(x + s2 * 0.09f, 0.82f, -0.68f), 0.02f, 0.02f, 10, chrome);
                for (int k = 0; k < 6; k++) mb.Sub(3).Tube(new Vector3(x + s2 * 0.09f, 0.46f + k * 0.045f, -0.62f - k * 0.008f), new Vector3(x + s2 * 0.09f, 0.475f + k * 0.045f, -0.622f - k * 0.008f), 0.034f, 0.024f, 10, new Color(0.75f, 0.12f, 0.08f));   // shock spring
                // rear subframe: under the saddle springs and the pillion, out to the fender
                mb.Sub(1).Cyl(new Vector3(x + s2 * 0.08f, 0.5f, -0.15f), new Vector3(x + s2 * 0.08f, 0.84f, -0.7f), 0.018f, 0.018f, 8, dark);
                mb.Sub(1).Cyl(new Vector3(x + s2 * 0.08f, 0.84f, -0.7f), new Vector3(x + s2 * 0.08f, 0.7f, -0.98f), 0.016f, 0.016f, 8, dark);   // fender stay
                mb.Sub(1).Cyl(new Vector3(x + s2 * 0.08f, 0.32f, -0.72f), new Vector3(x + s2 * 0.08f, 0.62f, -0.98f), 0.014f, 0.014f, 8, dark);   // lower fender stay
                // footboard brackets down from the frame
                mb.Sub(1).Cyl(new Vector3(x + s2 * 0.12f, 0.42f, 0.1f), new Vector3(x + s2 * 0.2f, 0.36f, 0.1f), 0.015f, 0.015f, 8, dark);
                // front fender stays to the fork legs
                mb.Sub(1).Cyl(new Vector3(x + s2 * 0.085f, 0.32f, 0.78f), new Vector3(x + s2 * 0.085f, 0.62f, 0.95f), 0.008f, 0.008f, 6, chrome);
            }
            mb.Sub(1).Cyl(new Vector3(x, 0.84f, -0.35f), new Vector3(x, 0.84f, -0.85f), 0.016f, 0.016f, 8, dark);   // seat rail under the springs
            mb.Sub(1).Box(new Vector3(x, 0.76f, -0.42f), new Vector3(0.22f, 0.02f, 0.06f), dark, 0.005f);          // spring mounts on the subframe
            // ---- the sidecar: a rounded tub on a chassis, a windscreen, the gunner's seat
            float cx = CarX;
            mb.Sub(1).Cyl(new Vector3(x + 0.1f, 0.4f, 0.45f), new Vector3(cx + 0.1f, 0.4f, 0.45f), 0.03f, 0.03f, 8, dark);   // chassis struts
            mb.Sub(1).Cyl(new Vector3(x + 0.1f, 0.4f, -0.45f), new Vector3(cx + 0.1f, 0.4f, -0.45f), 0.03f, 0.03f, 8, dark);
            mb.Sub(1).Cyl(new Vector3(cx, 0.38f, 0.55f), new Vector3(cx, 0.38f, -0.6f), 0.03f, 0.03f, 8, dark);
            // the tub: an open, rounded bowl (painted outside, dark inside) with a rolled rim
            mb.Sub(0).Sphere(new Vector3(cx, 0.72f, -0.05f), new Vector3(0.36f, 0.34f, 0.76f), 24, paint, 0.5f, 1f);
            mb.Sub(1).Push(new Vector3(cx, 0.72f, -0.05f), Quaternion.identity, new Vector3(-0.96f, 0.95f, 0.97f));   // mirrored: faces inwards
            mb.Sub(1).Sphere(Vector3.zero, new Vector3(0.36f, 0.34f, 0.76f), 24, new Color(0.14f, 0.14f, 0.13f), 0.5f, 1f);
            mb.Pop();
            mb.Sub(1).Push(new Vector3(cx, 0.72f, -0.05f), Quaternion.identity, new Vector3(1f, 1f, 0.76f / 0.36f));
            mb.Sub(1).Tube(new Vector3(0, -0.012f, 0), new Vector3(0, 0.014f, 0), 0.372f, 0.34f, 28, dark);   // rolled rim
            mb.Pop();
            // the sidecar seat: a deep padded bench with piping, a tall buttoned backrest, padded sides, a footwell
            mb.Sub(2).Box(new Vector3(cx, 0.58f, -0.3f), new Vector3(0.48f, 0.12f, 0.44f), leather, 0.05f, 2);      // cushion
            mb.Sub(2).Box(new Vector3(cx, 0.645f, -0.3f), new Vector3(0.44f, 0.012f, 0.4f), leather * 1.08f, 0.006f);
            mb.Sub(2).Box(new Vector3(cx, 0.585f, -0.075f), new Vector3(0.46f, 0.012f, 0.01f), pipe, 0.003f);       // piping
            mb.Sub(2).Push(new Vector3(cx, 0.86f, -0.56f), Quaternion.Euler(-12f, 0, 0));
            mb.Sub(2).Box(Vector3.zero, new Vector3(0.46f, 0.44f, 0.09f), leather, 0.045f, 2);                      // backrest
            for (int r = 0; r < 2; r++) for (int k = 0; k < 3; k++) mb.Sub(2).Sphere(new Vector3(-0.13f + k * 0.13f, -0.08f + r * 0.16f, 0.046f), new Vector3(0.012f, 0.012f, 0.006f), 6, pipe);   // buttons
            for (int k = 0; k < 2; k++) mb.Sub(2).Box(new Vector3(-0.065f + k * 0.13f, 0, 0.046f), new Vector3(0.004f, 0.38f, 0.004f), pipe, 0f);   // pleats
            mb.Pop();
            foreach (float s2 in new[] { -1f, 1f }) mb.Sub(2).Capsule(new Vector3(cx + s2 * 0.25f, 0.76f, -0.5f), new Vector3(cx + s2 * 0.25f, 0.76f, -0.12f), 0.045f, 10, leather);   // padded sides
            mb.Sub(1).Box(new Vector3(cx, 0.48f, 0.15f), new Vector3(0.4f, 0.02f, 0.45f), dark, 0.006f);   // footwell floor
            mb.Sub(3).Box(new Vector3(cx + 0.36f, 0.66f, -0.05f), new Vector3(0.02f, 0.03f, 0.9f), chrome, 0.005f);   // side rub strip
            mb.Sub(3).Cyl(new Vector3(cx + 0.33f, 0.74f, 0.32f), new Vector3(cx + 0.33f, 0.8f, 0.12f), 0.012f, 0.012f, 6, chrome);   // grab handle
            // the gun's pintle post, rising from the sidecar's nose
            mb.Sub(1).Cyl(new Vector3(cx, 0.5f, 0.3f), new Vector3(cx, 1.18f, 0.3f), 0.032f, 0.032f, 10, dark);
            mb.Sub(1).Cyl(new Vector3(cx, 1.17f, 0.3f), new Vector3(cx, 1.2f, 0.3f), 0.05f, 0.05f, 12, dark);
            // the sidecar wheel's swing arm (from the chassis rail out to its hub) and a mudguard arched over it on stays
            mb.Sub(1).Cyl(new Vector3(cx, 0.38f, 0.3f), new Vector3(cx + 0.41f, 0.28f, -0.05f), 0.025f, 0.022f, 10, dark);
            mb.Sub(1).Cyl(new Vector3(cx, 0.38f, -0.35f), new Vector3(cx + 0.41f, 0.28f, -0.05f), 0.025f, 0.022f, 10, dark);
            mb.Sub(1).Cyl(new Vector3(cx + 0.38f, 0.28f, -0.05f), new Vector3(cx + 0.53f, 0.28f, -0.05f), 0.018f, 0.018f, 8, dark);   // stub axle
            for (int i = 0; i < 8; i++)
            {
                float a0 = Mathf.Lerp(25f, 155f, i / 8f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(25f, 155f, (i + 1) / 8f) * Mathf.Deg2Rad, am = (a0 + a1) / 2f;
                Vector3 c = new Vector3(cx + 0.47f, 0.28f, -0.05f), radial = new Vector3(0, Mathf.Sin(am), Mathf.Cos(am)), tangent = new Vector3(0, Mathf.Cos(am), -Mathf.Sin(am));
                mb.Sub(0).Box(c + radial * 0.36f, new Vector3(0.15f, 0.012f, 0.36f * (a1 - a0) + 0.01f), paint, 0.004f, 1, Quaternion.LookRotation(tangent, radial));
            }
            mb.Sub(1).Cyl(new Vector3(cx + 0.33f, 0.62f, -0.05f), new Vector3(cx + 0.41f, 0.64f, -0.05f), 0.012f, 0.012f, 6, dark);   // guard stay to the tub
            // spare jerrycan on the back of the sidecar
            mb.Sub(0).Box(new Vector3(cx, 0.66f, -0.86f), new Vector3(0.3f, 0.34f, 0.12f), new Color(0.2f, 0.25f, 0.16f), 0.02f, 2);
            mb.Sub(1).Box(new Vector3(cx, 0.5f, -0.84f), new Vector3(0.34f, 0.02f, 0.16f), dark, 0.004f);   // its carrier
            mb.Sub(1).Cyl(new Vector3(cx, 0.38f, -0.6f), new Vector3(cx, 0.5f, -0.84f), 0.015f, 0.015f, 6, dark);
            mb.Sub(1).Box(new Vector3(cx, 0.75f, -0.86f), new Vector3(0.32f, 0.02f, 0.13f), dark, 0.003f);   // strap
            return mb;
        }

        public static Material[] BikeMats => new[]
        {
            Mats.Lit("bike_paint", Color.white, 0.25f, 0.5f, Mats.Noise, null, 0.3f, 0.2f), Mats.Lit("cart_dark", Color.white, 0.1f, 0.3f, Mats.Noise, null, 0.2f, 0.1f),
            Mats.Lit("bike_leather", Color.white, 0f, 0.35f, Mats.Fabric, null, 0.3f, 0f), Mats.Lit("bike_chrome", Color.white, 0.95f, 0.85f, Mats.Metal, null, 0.05f, 0.3f)
        };

        static void Wheel(MB mb, float r, float w, Color tyre)
        {
            mb.Sub(0).Cyl(new Vector3(-w / 2, 0, 0), new Vector3(w / 2, 0, 0), r, r, 22, tyre);
            mb.Sub(1).Cyl(new Vector3(-w / 2 - 0.004f, 0, 0), new Vector3(w / 2 + 0.004f, 0, 0), r * 0.7f, r * 0.7f, 20, new Color(0.55f, 0.55f, 0.57f));
            for (int i = 0; i < 12; i++) { float a = i * 30f; mb.Sub(1).Box(Vector3.zero, new Vector3(0.006f, r * 1.36f, 0.006f), new Color(0.75f, 0.75f, 0.78f), 0f, 1, Quaternion.Euler(a, 0, 0)); }   // spokes
            mb.Sub(1).Cyl(new Vector3(-w / 2 - 0.02f, 0, 0), new Vector3(w / 2 + 0.02f, 0, 0), 0.05f, 0.05f, 12, new Color(0.3f, 0.3f, 0.32f));   // hub
        }

        /// <summary>The front end that turns: forks, a wide tubular handlebar on risers with rubber grips, levers, mirrors,
        /// a speedo and the headlamp. Origin = the head stock (local (BikeX, 1.1, 0.56) on the bike). 0 rubber / dark, 1 chrome, 2 lamp glass.</summary>
        public static MB BarsModel()
        {
            var bm = new MB(3);
            Color dark = new Color(0.09f, 0.09f, 0.1f), chrome = new Color(0.8f, 0.8f, 0.82f);
            const float R = 0.33f;
            foreach (float s in new[] { -1f, 1f })
            {
                bm.Sub(1).Cyl(new Vector3(s * 0.08f, -0.05f, 0.02f), new Vector3(s * 0.08f, -0.78f, 0.22f), 0.03f, 0.026f, 12, chrome);   // fork legs down to the axle
                bm.Sub(0).Cyl(new Vector3(s * 0.08f, -0.55f, 0.155f), new Vector3(s * 0.08f, -0.78f, 0.22f), 0.036f, 0.036f, 12, dark);   // fork sliders
                bm.Sub(1).Cyl(new Vector3(s * 0.06f, -0.02f, 0.0f), new Vector3(s * 0.07f, 0.08f, -0.04f), 0.02f, 0.02f, 10, chrome);   // risers
            }
            bm.Sub(1).Box(new Vector3(0, -0.04f, 0.02f), new Vector3(0.22f, 0.04f, 0.07f), chrome, 0.012f);   // top yoke
            // the bar: straight through the middle, sweeping back and down a little to the grips
            bm.Sub(1).Cyl(new Vector3(-0.14f, 0.09f, -0.05f), new Vector3(0.14f, 0.09f, -0.05f), 0.016f, 0.016f, 10, chrome);
            foreach (float s in new[] { -1f, 1f })
            {
                bm.Sub(1).Cyl(new Vector3(s * 0.14f, 0.09f, -0.05f), new Vector3(s * (R - 0.11f), 0.075f, -0.1f), 0.016f, 0.016f, 10, chrome);
                bm.Sub(0).Cyl(new Vector3(s * (R - 0.12f), 0.075f, -0.1f), new Vector3(s * (R + 0.01f), 0.075f, -0.1f), 0.022f, 0.022f, 12, dark);   // rubber grip
                for (int k = 0; k < 5; k++) { float gx = s * (R - 0.1f + k * 0.022f); bm.Sub(0).Cyl(new Vector3(gx - 0.004f, 0.075f, -0.1f), new Vector3(gx + 0.004f, 0.075f, -0.1f), 0.024f, 0.024f, 12, dark * 1.6f); }   // grip ribs
                bm.Sub(1).Cyl(new Vector3(s * (R + 0.01f), 0.075f, -0.1f), new Vector3(s * (R + 0.025f), 0.075f, -0.1f), 0.019f, 0.019f, 10, chrome);   // bar end
                bm.Sub(1).Box(new Vector3(s * (R - 0.14f), 0.085f, -0.1f), new Vector3(0.03f, 0.03f, 0.03f), chrome, 0.006f);   // lever clamp
                bm.Sub(1).Cyl(new Vector3(s * (R - 0.14f), 0.09f, -0.11f), new Vector3(s * (R - 0.01f), 0.085f, -0.17f), 0.007f, 0.005f, 6, chrome);   // lever
                bm.Sub(1).Cyl(new Vector3(s * 0.18f, 0.1f, -0.06f), new Vector3(s * 0.22f, 0.26f, -0.06f), 0.007f, 0.007f, 6, chrome);   // mirror stalk
                bm.Sub(1).Cyl(new Vector3(s * 0.22f, 0.27f, -0.075f), new Vector3(s * 0.22f, 0.27f, -0.05f), 0.045f, 0.045f, 14, chrome);
                bm.Sub(2).Cyl(new Vector3(s * 0.22f, 0.27f, -0.0765f), new Vector3(s * 0.22f, 0.27f, -0.076f), 0.04f, 0.04f, 14, new Color(0.55f, 0.6f, 0.65f));   // mirror glass
            }
            bm.Sub(0).Cyl(new Vector3(0, 0.12f, -0.04f), new Vector3(0, 0.15f, -0.07f), 0.045f, 0.045f, 16, dark);   // speedo
            bm.Sub(2).Cyl(new Vector3(0, 0.15f, -0.07f), new Vector3(0, 0.152f, -0.072f), 0.038f, 0.038f, 16, new Color(0.9f, 0.88f, 0.8f));
            // the headlamp, on the forks (it turns with them)
            bm.Sub(1).Sphere(new Vector3(0, -0.12f, 0.17f), new Vector3(0.1f, 0.1f, 0.09f), 16, chrome, 0f, 0.5f);
            bm.Sub(1).Cyl(new Vector3(0, -0.12f, 0.12f), new Vector3(0, -0.12f, 0.18f), 0.1f, 0.1f, 18, chrome);
            bm.Sub(2).Cyl(new Vector3(0, -0.12f, 0.18f), new Vector3(0, -0.12f, 0.19f), 0.088f, 0.085f, 18, new Color(1f, 0.96f, 0.8f));
            foreach (float s in new[] { -1f, 1f }) bm.Sub(1).Cyl(new Vector3(s * 0.08f, -0.12f, 0.1f), new Vector3(s * 0.1f, -0.12f, 0.14f), 0.01f, 0.01f, 6, chrome);   // lamp brackets
            return bm;
        }

        public static Material[] BarsMats => new[] { Mats.Rubber, Mats.Lit("bike_chrome", Color.white, 0.95f, 0.85f, Mats.Metal, null, 0.05f, 0.3f), Mats.Lit("bike_glass", Color.white, 0.2f, 0.95f, null, null, 0.02f, 0.02f, false, 1f, new Color(0.3f, 0.28f, 0.2f)) };

        void BuildBike()
        {
            MaxSpeed = 20f; MaxReverse = 4f; _wheelR = 0.32f;
            // suspension rays: front and back of the bike, front and back of the sidecar wheel (four, like the cart, for stability)
            Corners = new[] { new Vector3(BikeX, 0.45f, 0.78f), new Vector3(CarX + 0.45f, 0.45f, 0.2f), new Vector3(BikeX, 0.45f, -0.72f), new Vector3(CarX + 0.45f, 0.45f, -0.3f) };
            BikeModel().Build("Body", transform, BikeMats);
            var mats = new[] { Mats.Rubber, Mats.Lit("bike_spokes", Color.white, 0.8f, 0.6f, Mats.Metal, null, 0.1f, 0.2f) };
            Color tyre = new Color(0.08f, 0.08f, 0.08f);
            // road wheels: 0 front (steers), 1 rear, 2 sidecar
            var wp = new[] { new Vector3(BikeX, 0.32f, 0.78f), new Vector3(BikeX, 0.32f, -0.72f), new Vector3(CarX + 0.47f, 0.28f, -0.05f) };
            for (int i = 0; i < 3; i++)
            {
                var wt = new GameObject("Wheel" + i).transform; wt.SetParent(transform, false); wt.localPosition = wp[i];
                var wm = new MB(2); Wheel(wm, i == 2 ? 0.28f : 0.32f, i == 2 ? 0.1f : 0.11f, tyre); wm.Build("Tyre", wt, mats);
                _wheels[i] = wt;
            }
            // front forks + handlebars: they turn together with the front wheel
            var bars = new GameObject("Handlebars").transform; bars.SetParent(transform, false); bars.localPosition = new Vector3(BikeX, 1.1f, 0.56f);
            var bm = BarsModel();
            bm.Build("Bars", bars, BarsMats);
            _steerWheel = bars; WheelGrab = bars;
            // seats: the rider on the saddle, the gunner down in the sidecar
            Seat = new GameObject("DriverSeat").transform; Seat.SetParent(transform, false); Seat.localPosition = new Vector3(BikeX, 0.98f, -0.3f);
            PassSeat = new GameObject("PassengerSeat").transform; PassSeat.SetParent(transform, false); PassSeat.localPosition = new Vector3(CarX, 0.65f, -0.3f);
            // grab spots to climb on (like the cart's door handles): the saddle's side and the sidecar's grab handle
            for (int i = 0; i < 2; i++)
            {
                var h = new GameObject(i == 0 ? "GetOnBike" : "GetInSidecar").transform; h.SetParent(transform, false);
                h.localPosition = i == 0 ? new Vector3(BikeX - 0.15f, 0.92f, -0.3f) : new Vector3(CarX + 0.33f, 0.78f, 0.22f);
                _handles[i] = h;
            }
            // the machine gun on the sidecar
            var tp = new GameObject("Turret").transform; tp.SetParent(transform, false); tp.localPosition = new Vector3(CarX, 1.24f, 0.3f);   // the bore at a seated gunner's eye line
            Turret = tp.gameObject.AddComponent<Turret>(); Turret.Init(this);
            // physics: the bike's body and the sidecar tub
            var col = gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(BikeX, 0.7f, 0f); col.size = new Vector3(0.38f, 0.55f, 1.9f);
            var tub = gameObject.AddComponent<BoxCollider>(); tub.center = new Vector3(CarX, 0.55f, -0.05f); tub.size = new Vector3(0.7f, 0.34f, 1.45f);
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 320f; rb.SetDamping(0.05f, 0.6f);
            rb.centerOfMass = new Vector3(0.05f, 0.1f, 0);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _motor = gameObject.AddComponent<AudioSource>();
            _motor.clip = Synth.CartMotor(); _motor.loop = true; _motor.spatialBlend = 1f; _motor.minDistance = 3f; _motor.maxDistance = 45f; _motor.volume = 0f;
        }
    }
}

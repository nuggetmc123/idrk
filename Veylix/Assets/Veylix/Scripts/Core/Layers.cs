using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// Physics layers by number (no names needed in the Tag Manager). Collisions between them are set up in code once at
    /// start, so a fresh copy of the project behaves the same without touching Project Settings.
    /// </summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int UI = 5;
        public const int Car = 8;        // car bodies
        public const int CarWheel = 9;   // wheel bumpers: touch other cars and walls, never the road (the suspension does that)
        public const int Road = 10;      // drivable surfaces (road, kerbs, run-off)
        public const int Barrier = 11;   // walls, armco, tyre stacks, buildings
        public const int Terrain = 12;
        public const int Debris = 13;    // bits that fall off cars
        public const int Scenery = 14;   // render-only things (no colliders)

        static bool done;

        public static void Setup()
        {
            if (done) return;
            done = true;
            Physics.IgnoreLayerCollision(CarWheel, Road, true);
            Physics.IgnoreLayerCollision(CarWheel, Terrain, true);
            Physics.IgnoreLayerCollision(CarWheel, CarWheel, false);
            Physics.IgnoreLayerCollision(Debris, CarWheel, true);
            Physics.IgnoreLayerCollision(Debris, Debris, false);
            Physics.IgnoreLayerCollision(UI, Car, true);
            Physics.IgnoreLayerCollision(UI, CarWheel, true);
            Physics.IgnoreLayerCollision(UI, Debris, true);
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(Scenery, i, true);
        }

        public static void SetRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetRecursive(t.gameObject, layer);
        }
    }
}

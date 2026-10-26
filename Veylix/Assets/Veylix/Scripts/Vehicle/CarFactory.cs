using System.Collections.Generic;
using UnityEngine;

namespace Veylix
{
    /// <summary>Puts a complete car together: body, physics, damage, model, effects and sound.</summary>
    public static class CarFactory
    {
        public static readonly List<Car> All = new List<Car>();

        public static Car Spawn(CarSpec spec, CarSetup setup, bool isPlayer, string driver, int slot, Vector3 pos, Quaternion rot, DriverLook look = null)
        {
            Layers.Setup();
            var go = new GameObject(isPlayer ? "PlayerCar" : "Car " + driver);
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var car = go.AddComponent<Car>();
            car.isPlayer = isPlayer;
            car.driverName = driver;
            car.carIndex = slot;
            car.driverLook = look ?? (isPlayer ? SaveData.Profile : null);
            car.damage = go.AddComponent<CarDamage>();
            car.Init(spec, setup);
            car.damage.car = car;
            CarModel.Build(car, isPlayer);
            car.damage.parts = car.parts;
            car.damage.Repair();

            var fx = go.AddComponent<CarEffects>();
            fx.Init(car);

            var audioGo = new GameObject("Engine");
            audioGo.transform.SetParent(go.transform, false);
            audioGo.transform.localPosition = new Vector3(0f, 0.5f, -0.8f);
            audioGo.AddComponent<AudioSource>();
            var au = audioGo.AddComponent<CarAudio>();
            au.Configure(car, isPlayer);
            if (!isPlayer) au.enabled = true;

            if (isPlayer)
            {
                // Headlight beam for night driving (one real light; the AI cars use emissive lamps only).
                var lgo = new GameObject("HeadSpot");
                lgo.transform.SetParent(go.transform, false);
                lgo.transform.localPosition = new Vector3(0f, 0.45f, spec.wheelbase * 0.5f + 0.8f);
                lgo.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 70f;
                l.innerSpotAngle = 35f;
                l.range = 90f;
                l.intensity = 0f;
                l.color = new Color(1f, 0.97f, 0.92f);
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                car.parts.headSpot = l;
            }
            car.abs = isPlayer ? Settings.Abs : true;
            car.tractionControl = isPlayer ? Settings.TractionControl : true;
            car.autoGears = isPlayer ? Settings.AutoGears : true;
            All.Add(car);
            return car;
        }

        public static void Despawn(Car car)
        {
            if (car == null) return;
            All.Remove(car);
            Fx.StopAttached(car.transform);
            Object.Destroy(car.gameObject);
        }

        public static void DespawnAll()
        {
            foreach (var c in All.ToArray()) if (c != null) { Fx.StopAttached(c.transform); Object.Destroy(c.gameObject); }
            All.Clear();
        }
    }
}

using Fusion;
using UnityEngine;

namespace Veylix
{
    /// <summary>
    /// One player's car on the network (Photon Fusion 2, Shared Mode). The owner simulates their own car with full
    /// physics and publishes its state here; everyone else shows a smoothed, kinematic copy with the same car and
    /// livery, and reads the owner's lap count and timing for the leaderboard.
    /// </summary>
    public class NetCar : NetworkBehaviour
    {
        [Networked] public int CarIndex { get; set; }
        [Networked] public int Livery { get; set; }
        [Networked] public int Number { get; set; }
        [Networked] public int Primary { get; set; }
        [Networked] public int Secondary { get; set; }
        [Networked] public int Accent { get; set; }
        [Networked] public NetworkString<_16> Driver { get; set; }
        [Networked] public Vector3 Pos { get; set; }
        [Networked] public Quaternion Rot { get; set; }
        [Networked] public Vector3 Vel { get; set; }
        [Networked] public float Steer { get; set; }
        [Networked] public float Rpm { get; set; }
        [Networked] public int Lap { get; set; }
        [Networked] public int NextCheckpoint { get; set; }
        [Networked] public float Along { get; set; }
        [Networked] public float BestLap { get; set; }
        [Networked] public float FinishTime { get; set; }
        [Networked] public NetworkBool Finished { get; set; }
        [Networked] public NetworkBool Ready { get; set; }

        public Car local;      // set on the owner
        public Car proxy;      // the visual copy on other players' screens
        public Entrant entrant;
        float lastSend;

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                local = Game.I != null ? Game.I.playerCar : null;
                if (local != null)
                {
                    CarIndex = Game.I.config.car;
                    var st = local.setup;
                    Livery = st.Livery(local.spec);
                    Number = st.Number(local.spec);
                    Primary = Pack(st.Primary(local.spec)); Secondary = Pack(st.Secondary(local.spec)); Accent = Pack(st.Accent(local.spec));
                }
                Driver = Championship.PlayerName.Length > 16 ? Championship.PlayerName.Substring(0, 16) : Championship.PlayerName;
                Ready = true;
            }
            Online.Cars.Add(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Online.Cars.Remove(this);
            if (proxy != null) CarFactory.Despawn(proxy);
            if (entrant != null && RaceSession.I != null) RaceSession.I.entrants.Remove(entrant);
        }

        static int Pack(Color c) => ((int)(c.r * 255f) << 16) | ((int)(c.g * 255f) << 8) | (int)(c.b * 255f);
        static Color Unpack(int v) => new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (local == null && Game.I != null) local = Game.I.playerCar;
            if (local == null) return;
            Pos = local.transform.position;
            Rot = local.transform.rotation;
            Vel = local.Body != null ? local.Body.linearVelocity : Vector3.zero;
            Steer = local.input.steer;
            Rpm = local.Rpm;
            var s = RaceSession.I;
            if (s != null && s.player != null)
            {
                Lap = s.player.lap;
                NextCheckpoint = s.player.nextCheckpoint;
                Along = s.player.along;
                BestLap = s.player.bestLap < float.MaxValue ? s.player.bestLap : 0f;
                Finished = s.player.finished;
                FinishTime = s.player.finishTime;
            }
        }

        public override void Render()
        {
            if (Object.HasStateAuthority) return;
            if (World.Current == null || !World.Current.Ready || RaceSession.I == null) return;
            if (proxy == null) CreateProxy();
            if (proxy == null) return;
            // Smooth towards the latest state, extrapolated a little to hide the network delay.
            Vector3 target = Pos + Vel * 0.08f;
            float k = 1f - Mathf.Exp(-Time.deltaTime * 14f);
            if ((proxy.transform.position - target).sqrMagnitude > 400f) proxy.transform.position = target;
            proxy.transform.SetPositionAndRotation(Vector3.Lerp(proxy.transform.position, target, k), Quaternion.Slerp(proxy.transform.rotation, Rot, k));
            if (entrant != null)
            {
                entrant.lap = Lap;
                entrant.nextCheckpoint = NextCheckpoint;
                entrant.bestLap = BestLap > 0f ? BestLap : float.MaxValue;
                entrant.finished = Finished;
                entrant.finishTime = FinishTime;
                entrant.name = Driver.ToString();
            }
        }

        void CreateProxy()
        {
            var spec = CarCatalog.Get(CarIndex);
            var setup = new CarSetup { livery = Livery, number = Number, primary = CarSetup.Hex(Unpack(Primary)), secondary = CarSetup.Hex(Unpack(Secondary)), accent = CarSetup.Hex(Unpack(Accent)) };
            var w = World.Current;
            proxy = CarFactory.Spawn(spec, setup, false, Driver.ToString(), 50 + (Online.Cars.IndexOf(this) % 20), Pos, Rot);
            proxy.Freeze(true); // kinematic: moved by the network, still solid for collisions
            entrant = new Entrant { car = proxy, remote = true, name = Driver.ToString(), team = "Online", number = Number };
            RaceSession.I.entrants.Add(entrant);
        }
    }
}

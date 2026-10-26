using System;
using UnityEngine;

namespace Veylix
{
    public enum EngineLayout { V6Hybrid, V8, V10, V12Classic, I4Turbo, V8Classic }
    public enum BodyStyle { Modern, Midera, Classic, Junior, Hillclimb }
    public enum TireCompound { Soft, Medium, Hard, Intermediate, Wet }
    public enum BrakeType { Carbon, Steel }

    /// <summary>
    /// Everything that makes one car drive and look different. All numbers are SI (kg, m, N, Nm, rad/s) unless the
    /// name says otherwise. The seven cars are fictional; their figures are in the ballpark of the real eras they
    /// borrow from, so they feel right without copying anybody's car.
    /// </summary>
    [Serializable]
    public class CarSpec
    {
        public string id, name, maker, era, blurb;
        public BodyStyle body;
        public EngineLayout engine;
        public int tier; // 1 = fastest

        // Mass and geometry
        public float mass = 798f, cogHeight = 0.27f, wheelbase = 3.6f, trackFront = 1.6f, trackRear = 1.55f, weightFront = 0.45f;
        public float length = 5.6f, width = 2.0f;

        // Engine
        public float idleRpm = 4000f, redline = 12500f, limiter = 13000f, peakTorque = 600f, peakTorqueRpm = 10500f;
        public float torqueFalloff = 0.35f; // fraction of peak torque lost by the limiter
        public float lowEndTorque = 0.55f;  // fraction of peak torque at idle
        public float hybridKw = 120f;       // electric boost (0 = none)
        public float engineBrake = 70f;     // Nm of drag on lift-off
        public float engineInertia = 0.09f;

        // Transmission
        public float[] gears = { 3.0f, 2.45f, 2.05f, 1.76f, 1.53f, 1.35f, 1.2f, 1.08f };
        public float finalDrive = 3.3f, shiftTime = 0.04f;

        // Tyres (radius in m)
        public float wheelRadiusFront = 0.36f, wheelRadiusRear = 0.36f, tireWidthFront = 0.305f, tireWidthRear = 0.405f;
        public float gripMu = 1.75f, loadSensitivity = 0.12f, peakSlipAngle = 7f, peakSlipRatio = 0.1f;
        public float wheelInertia = 1.1f;

        // Aerodynamics (coefficient * area, m^2)
        public float downforceClA = 4.2f, dragCdA = 1.25f, aeroBalance = 0.42f;

        // Brakes
        public float brakeTorque = 5200f, brakeBias = 0.57f;

        // Suspension
        public float springFront = 230000f, springRear = 190000f, damperFront = 9000f, damperRear = 8000f;
        public float arbFront = 90000f, arbRear = 60000f, rideHeight = 0.04f, suspensionTravel = 0.07f;

        // Steering
        public float maxSteerAngle = 21f, wheelLock = 180f;

        public bool hasHandbrake, hasHalo = true, hasShiftLights = true;
        public float fuelCapacity = 110f, fuelPerLapRate = 1f;

        // Look
        public Color defaultPrimary = Color.white, defaultSecondary = Color.black, defaultAccent = Color.red;
        public int defaultLivery, defaultNumber = 1;

        public float MaxRpm => limiter;
        public int GearCount => gears.Length;
    }

    /// <summary>A player's changes to a car in the Garage. Saved per car as JSON.</summary>
    [Serializable]
    public class CarSetup
    {
        public string primary = "", secondary = "", accent = "";
        public int livery = -1, number = -1, wheelStyle, rimColor;
        public int wingLevel = 1;                 // 0 low, 1 medium, 2 high downforce
        public TireCompound compound = TireCompound.Medium;
        public BrakeType brakeType = BrakeType.Carbon;
        public float brakeBias = -1f;             // -1 = car default
        public float brakePressure = 1f;          // 0.8..1
        public float springs = 0.5f, rideHeight = 0.5f, antiRoll = 0.5f, dampers = 0.5f; // 0..1 around the default

        public Color Primary(CarSpec s) => Parse(primary, s.defaultPrimary);
        public Color Secondary(CarSpec s) => Parse(secondary, s.defaultSecondary);
        public Color Accent(CarSpec s) => Parse(accent, s.defaultAccent);
        public int Livery(CarSpec s) => livery < 0 ? s.defaultLivery : livery;
        public int Number(CarSpec s) => number < 0 ? s.defaultNumber : number;
        public float Bias(CarSpec s) => brakeBias < 0f ? s.brakeBias : brakeBias;

        static Color Parse(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : fallback;

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public CarSetup Clone() => (CarSetup)MemberwiseClone();
    }

    public static class CarCatalog
    {
        public static readonly CarSpec[] All = Build();
        public static int Count => All.Length;
        public static CarSpec Get(int i) => All[Mathf.Clamp(i, 0, All.Length - 1)];

        public static readonly string[] LiveryNames = { "Classic Stripe", "Arrowhead", "Split Tone", "Fade", "Twin Stripes", "Chevron", "Heritage Band", "Shard" };
        public static readonly string[] WheelNames = { "Monoblock 10", "Forged 5", "Turbine", "Classic 8", "Aero Disc" };
        public static readonly string[] RimColorNames = { "Gloss Black", "Gunmetal", "Gold", "Silver", "Body Colour" };
        public static readonly string[] WingNames = { "Low (Monza)", "Medium", "High (Monaco)" };
        public static readonly string[] CompoundNames = { "Soft", "Medium", "Hard", "Intermediate", "Full Wet" };
        public static readonly Color[] CompoundColors =
        {
            new Color(0.86f, 0.12f, 0.12f), new Color(0.98f, 0.82f, 0.1f), new Color(0.92f, 0.92f, 0.92f),
            new Color(0.2f, 0.72f, 0.25f), new Color(0.12f, 0.42f, 0.9f),
        };

        public static readonly Color[] PaintSwatches =
        {
            Hex("F4F4F2"), Hex("101114"), Hex("2B2E33"), Hex("8A8F96"), Hex("C8102E"), Hex("E8501C"), Hex("F2A900"),
            Hex("E0D24B"), Hex("1E7B4A"), Hex("0B5D3B"), Hex("0E8C8C"), Hex("1C4F9C"), Hex("0B1F4D"), Hex("5B2A86"),
            Hex("8E1B3A"), Hex("D6B98C"), Hex("B87333"), Hex("63C5DA"), Hex("FF5FA2"), Hex("7A0019"),
        };

        static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        static CarSpec[] Build()
        {
            return new[]
            {
                new CarSpec
                {
                    id = "vx26", name = "Veylix VX-26", maker = "Veylix Works", era = "Current hybrid era", tier = 1,
                    blurb = "1.6 litre turbo V6 hybrid. Huge downforce, brutal braking and an electric shove out of every corner.",
                    body = BodyStyle.Modern, engine = EngineLayout.V6Hybrid,
                    mass = 798f, cogHeight = 0.27f, wheelbase = 3.6f, length = 5.6f, width = 2.0f, weightFront = 0.45f,
                    idleRpm = 4000f, redline = 11800f, limiter = 12500f, peakTorque = 560f, peakTorqueRpm = 10500f, hybridKw = 120f,
                    gears = new[] { 2.95f, 2.4f, 2.0f, 1.72f, 1.5f, 1.33f, 1.19f, 1.07f }, finalDrive = 3.35f, shiftTime = 0.03f,
                    gripMu = 1.78f, downforceClA = 4.4f, dragCdA = 1.28f, aeroBalance = 0.43f, brakeTorque = 5600f, brakeBias = 0.56f,
                    maxSteerAngle = 21f, wheelLock = 180f, wheelRadiusFront = 0.36f, wheelRadiusRear = 0.36f,
                    defaultPrimary = Hex("101114"), defaultSecondary = Hex("63C5DA"), defaultAccent = Hex("F4F4F2"), defaultLivery = 1, defaultNumber = 7,
                },
                new CarSpec
                {
                    id = "s24", name = "Strahl S-24", maker = "Strahl Grand Prix", era = "Current hybrid era", tier = 1,
                    blurb = "Low drag, nervous rear, ridiculous top speed. Rewards a smooth right foot.",
                    body = BodyStyle.Modern, engine = EngineLayout.V6Hybrid,
                    mass = 798f, cogHeight = 0.265f, wheelbase = 3.55f, length = 5.5f, width = 2.0f, weightFront = 0.44f,
                    idleRpm = 4000f, redline = 12000f, limiter = 12600f, peakTorque = 550f, peakTorqueRpm = 10800f, hybridKw = 120f,
                    gears = new[] { 2.9f, 2.35f, 1.96f, 1.68f, 1.46f, 1.29f, 1.15f, 1.03f }, finalDrive = 3.35f, shiftTime = 0.03f,
                    gripMu = 1.76f, downforceClA = 4.05f, dragCdA = 1.12f, aeroBalance = 0.41f, brakeTorque = 5500f, brakeBias = 0.57f,
                    springRear = 210000f, arbRear = 75000f, maxSteerAngle = 21f, wheelLock = 180f,
                    defaultPrimary = Hex("C8102E"), defaultSecondary = Hex("F4F4F2"), defaultAccent = Hex("101114"), defaultLivery = 0, defaultNumber = 16,
                },
                new CarSpec
                {
                    id = "cv10", name = "Corvane CV10", maker = "Corvane Corse", era = "V10 era", tier = 2,
                    blurb = "3.0 litre V10 that screams to 19,000 rpm. Light, narrow and twitchy over the kerbs.",
                    body = BodyStyle.Midera, engine = EngineLayout.V10, hasHalo = false,
                    mass = 605f, cogHeight = 0.28f, wheelbase = 3.1f, length = 4.6f, width = 1.8f, trackFront = 1.46f, trackRear = 1.42f, weightFront = 0.46f,
                    idleRpm = 5000f, redline = 18800f, limiter = 19200f, peakTorque = 370f, peakTorqueRpm = 16500f, hybridKw = 0f, lowEndTorque = 0.45f,
                    gears = new[] { 3.2f, 2.55f, 2.1f, 1.78f, 1.55f, 1.37f, 1.23f }, finalDrive = 4.2f, shiftTime = 0.04f,
                    wheelRadiusFront = 0.33f, wheelRadiusRear = 0.33f, tireWidthFront = 0.27f, tireWidthRear = 0.355f,
                    gripMu = 1.68f, downforceClA = 3.6f, dragCdA = 1.1f, aeroBalance = 0.44f, brakeTorque = 4300f, brakeBias = 0.58f,
                    springFront = 200000f, springRear = 170000f, maxSteerAngle = 20f, wheelLock = 160f, fuelCapacity = 95f,
                    defaultPrimary = Hex("C8102E"), defaultSecondary = Hex("F2A900"), defaultAccent = Hex("101114"), defaultLivery = 6, defaultNumber = 1,
                },
                new CarSpec
                {
                    id = "m8", name = "Meridian M8", maker = "Meridian Racing", era = "V8 era", tier = 2,
                    blurb = "2.4 litre V8 with a blown diffuser. Planted at speed, a handful in slow corners.",
                    body = BodyStyle.Midera, engine = EngineLayout.V8, hasHalo = false,
                    mass = 640f, cogHeight = 0.28f, wheelbase = 3.3f, length = 5.0f, width = 1.8f, trackFront = 1.47f, trackRear = 1.43f, weightFront = 0.46f,
                    idleRpm = 4500f, redline = 17500f, limiter = 18000f, peakTorque = 320f, peakTorqueRpm = 15000f, hybridKw = 60f,
                    gears = new[] { 3.15f, 2.5f, 2.08f, 1.77f, 1.54f, 1.36f, 1.22f }, finalDrive = 4.1f, shiftTime = 0.04f,
                    wheelRadiusFront = 0.33f, wheelRadiusRear = 0.33f, tireWidthFront = 0.27f, tireWidthRear = 0.36f,
                    gripMu = 1.7f, downforceClA = 3.9f, dragCdA = 1.15f, aeroBalance = 0.45f, brakeTorque = 4400f, brakeBias = 0.57f,
                    springFront = 210000f, springRear = 175000f, maxSteerAngle = 20f, wheelLock = 160f, fuelCapacity = 100f,
                    defaultPrimary = Hex("0B1F4D"), defaultSecondary = Hex("B87333"), defaultAccent = Hex("F4F4F2"), defaultLivery = 2, defaultNumber = 4,
                },
                new CarSpec
                {
                    id = "h72", name = "Halvorsen H72", maker = "Halvorsen Engineering", era = "1970s", tier = 3,
                    blurb = "Cosworth-style 3.0 V8, fat slicks, barely any downforce. Drives on the throttle; slides beautifully.",
                    body = BodyStyle.Classic, engine = EngineLayout.V8Classic, hasHalo = false, hasShiftLights = false,
                    mass = 575f, cogHeight = 0.33f, wheelbase = 2.55f, length = 4.3f, width = 1.9f, trackFront = 1.45f, trackRear = 1.55f, weightFront = 0.38f,
                    idleRpm = 2500f, redline = 10500f, limiter = 11000f, peakTorque = 340f, peakTorqueRpm = 8500f, hybridKw = 0f, lowEndTorque = 0.6f,
                    gears = new[] { 2.8f, 2.05f, 1.6f, 1.3f, 1.1f }, finalDrive = 4.3f, shiftTime = 0.12f,
                    wheelRadiusFront = 0.29f, wheelRadiusRear = 0.33f, tireWidthFront = 0.26f, tireWidthRear = 0.43f,
                    gripMu = 1.45f, peakSlipAngle = 8.5f, downforceClA = 1.4f, dragCdA = 1.05f, aeroBalance = 0.36f, brakeTorque = 2800f, brakeBias = 0.6f,
                    springFront = 70000f, springRear = 80000f, damperFront = 4000f, damperRear = 4400f, arbFront = 25000f, arbRear = 15000f,
                    rideHeight = 0.07f, suspensionTravel = 0.11f, maxSteerAngle = 24f, wheelLock = 300f, fuelCapacity = 180f,
                    defaultPrimary = Hex("0B5D3B"), defaultSecondary = Hex("F2A900"), defaultAccent = Hex("F4F4F2"), defaultLivery = 6, defaultNumber = 11,
                },
                new CarSpec
                {
                    id = "ft4", name = "Formula Trainer FT-4", maker = "Veylix Academy", era = "Junior single-seater", tier = 4,
                    blurb = "1.4 turbo four, steel brakes, forgiving grip and a handbrake for practice. Where every career starts.",
                    body = BodyStyle.Junior, engine = EngineLayout.I4Turbo, hasHandbrake = true, hasShiftLights = true,
                    mass = 570f, cogHeight = 0.3f, wheelbase = 2.75f, length = 4.3f, width = 1.75f, trackFront = 1.42f, trackRear = 1.4f, weightFront = 0.42f,
                    idleRpm = 1500f, redline = 7200f, limiter = 7500f, peakTorque = 250f, peakTorqueRpm = 4500f, hybridKw = 0f, lowEndTorque = 0.7f, torqueFalloff = 0.25f,
                    gears = new[] { 2.92f, 2.05f, 1.6f, 1.32f, 1.13f, 0.98f }, finalDrive = 4.0f, shiftTime = 0.07f,
                    wheelRadiusFront = 0.3f, wheelRadiusRear = 0.31f, tireWidthFront = 0.2f, tireWidthRear = 0.24f,
                    gripMu = 1.35f, peakSlipAngle = 8f, downforceClA = 1.2f, dragCdA = 0.85f, aeroBalance = 0.42f, brakeTorque = 2100f, brakeBias = 0.6f,
                    springFront = 90000f, springRear = 85000f, damperFront = 4500f, damperRear = 4300f, arbFront = 30000f, arbRear = 22000f,
                    rideHeight = 0.05f, suspensionTravel = 0.08f, maxSteerAngle = 24f, wheelLock = 240f, fuelCapacity = 45f,
                    defaultPrimary = Hex("F4F4F2"), defaultSecondary = Hex("1C4F9C"), defaultAccent = Hex("E8501C"), defaultLivery = 3, defaultNumber = 21,
                },
                new CarSpec
                {
                    id = "hx", name = "Hillclimb Special HX", maker = "Ardent Motorsport", era = "Hill climb", tier = 3,
                    blurb = "A formula car built for mountain roads: long-travel suspension, a 4.0 V8, a handbrake and a lot of attitude.",
                    body = BodyStyle.Hillclimb, engine = EngineLayout.V8, hasHandbrake = true, hasHalo = false,
                    mass = 640f, cogHeight = 0.34f, wheelbase = 2.8f, length = 4.4f, width = 1.9f, trackFront = 1.55f, trackRear = 1.55f, weightFront = 0.42f,
                    idleRpm = 2000f, redline = 9200f, limiter = 9500f, peakTorque = 520f, peakTorqueRpm = 7000f, hybridKw = 0f, lowEndTorque = 0.65f,
                    gears = new[] { 2.7f, 2.0f, 1.6f, 1.33f, 1.13f, 0.99f }, finalDrive = 3.9f, shiftTime = 0.06f,
                    wheelRadiusFront = 0.32f, wheelRadiusRear = 0.33f, tireWidthFront = 0.27f, tireWidthRear = 0.33f,
                    gripMu = 1.55f, peakSlipAngle = 8f, downforceClA = 2.3f, dragCdA = 1.15f, aeroBalance = 0.42f, brakeTorque = 3300f, brakeBias = 0.58f,
                    springFront = 75000f, springRear = 80000f, damperFront = 5200f, damperRear = 5400f, arbFront = 28000f, arbRear = 20000f,
                    rideHeight = 0.09f, suspensionTravel = 0.15f, maxSteerAngle = 26f, wheelLock = 300f, fuelCapacity = 70f,
                    defaultPrimary = Hex("E8501C"), defaultSecondary = Hex("2B2E33"), defaultAccent = Hex("F4F4F2"), defaultLivery = 5, defaultNumber = 33,
                },
            };
        }
    }
}

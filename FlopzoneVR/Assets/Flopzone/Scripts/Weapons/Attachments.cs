namespace Flopzone
{
    public enum Slot { Optic, Muzzle, Under, Mag }

    /// <summary>
    /// What's fitted to a gun, packed in one small number (the "code"):
    /// optic (0-3) + 4 × muzzle (0-2) + 16 × under-barrel (0-2) + 64 × mag (0-1). 0 = the gun as it comes.
    /// This part is pure C# so the preview tool can draw guns with attachments too.
    /// </summary>
    public static partial class Attachments
    {
        public static readonly string[][] Names =
        {
            new[] { "Stock Sight", "Red Dot", "Holo Sight", "4x ACOG" },
            new[] { "None", "Suppressor", "Compensator" },
            new[] { "None", "Vertical Grip", "Laser" },
            new[] { "Standard Mag", "Extended Mag" },
        };

        static readonly int[] Shift = { 1, 4, 16, 64 }, Size = { 4, 4, 4, 2 };

        public static int Get(int code, Slot s) => code / Shift[(int)s] % Size[(int)s];

        public static int With(int code, Slot s, int v) => code - Get(code, s) * Shift[(int)s] + v * Shift[(int)s];

        public static int Options(Slot s) => Names[(int)s].Length;

        public static string Name(Slot s, int v) => Names[(int)s][v];

        public const int MaxCode = 128;
    }
}

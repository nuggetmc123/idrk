using UnityEngine;

namespace Veylix
{
    /// <summary>The fictional teams and drivers of the Veylix series. No real teams, sponsors or drivers.</summary>
    public static class Teams
    {
        public struct Team
        {
            public string name, sponsor, sponsor2;
            public Color primary, secondary, accent;
            public int livery, car;
            public string[] drivers;
            public int[] numbers;
            public float pace; // 0.97..1.0 relative car performance in AI hands
        }

        static Color H(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        public static readonly Team[] All =
        {
            new Team { name = "Veylix Works", sponsor = "VEYLIX", sponsor2 = "NOVACOM", primary = H("101114"), secondary = H("63C5DA"), accent = H("F4F4F2"), livery = 1, car = 0, drivers = new[] { "Mateo Ruiz", "Lena Hartmann" }, numbers = new[] { 7, 27 }, pace = 1f },
            new Team { name = "Strahl Grand Prix", sponsor = "STRAHL", sponsor2 = "KESTREL", primary = H("C8102E"), secondary = H("F4F4F2"), accent = H("101114"), livery = 0, car = 1, drivers = new[] { "Oskar Lindqvist", "Ayaan Mehra" }, numbers = new[] { 16, 61 }, pace = 0.998f },
            new Team { name = "Halcyon Racing", sponsor = "HALCYON", sponsor2 = "ORBIS", primary = H("0B5D3B"), secondary = H("D6B98C"), accent = H("F2A900"), livery = 6, car = 0, drivers = new[] { "Thomas Whitfield", "Noa Kaplan" }, numbers = new[] { 3, 33 }, pace = 0.994f },
            new Team { name = "Ardent GP", sponsor = "ARDENT", sponsor2 = "LUMEN", primary = H("E8501C"), secondary = H("0B1F4D"), accent = H("F4F4F2"), livery = 2, car = 1, drivers = new[] { "Rafael Costa", "Mei Tanaka" }, numbers = new[] { 11, 22 }, pace = 0.99f },
            new Team { name = "Norvik Motorsport", sponsor = "NORVIK", sponsor2 = "CALDER", primary = H("1C4F9C"), secondary = H("F2A900"), accent = H("F4F4F2"), livery = 4, car = 0, drivers = new[] { "Jonas Berg", "Elif Demir" }, numbers = new[] { 5, 55 }, pace = 0.987f },
            new Team { name = "Brava Corse", sponsor = "BRAVA", sponsor2 = "VANTOR", primary = H("8E1B3A"), secondary = H("F4F4F2"), accent = H("D6B98C"), livery = 3, car = 1, drivers = new[] { "Luca Ferretti", "Ines Moreau" }, numbers = new[] { 14, 41 }, pace = 0.984f },
            new Team { name = "Tessaro F1", sponsor = "TESSARO", sponsor2 = "MERIDIAN", primary = H("F4F4F2"), secondary = H("0E8C8C"), accent = H("101114"), livery = 5, car = 0, drivers = new[] { "Kai Andersen", "Sofia Alves" }, numbers = new[] { 8, 18 }, pace = 0.981f },
            new Team { name = "Kaito Racing", sponsor = "KAITO", sponsor2 = "SOLENNE", primary = H("2B2E33"), secondary = H("FF5FA2"), accent = H("F4F4F2"), livery = 7, car = 1, drivers = new[] { "Haruto Mori", "Clara Novak" }, numbers = new[] { 20, 2 }, pace = 0.978f },
            new Team { name = "Solenne Engineering", sponsor = "SOLENNE", sponsor2 = "ARC LINE", primary = H("5B2A86"), secondary = H("E0D24B"), accent = H("F4F4F2"), livery = 1, car = 0, drivers = new[] { "Victor Lemaire", "Amara Okafor" }, numbers = new[] { 9, 99 }, pace = 0.975f },
            new Team { name = "Calder Autosport", sponsor = "CALDER", sponsor2 = "HALVORSEN", primary = H("0B1F4D"), secondary = H("C8102E"), accent = H("F4F4F2"), livery = 0, car = 1, drivers = new[] { "Finn O'Brien", "Yara Haddad" }, numbers = new[] { 12, 21 }, pace = 0.972f },
        };

        /// <summary>Grid slot i (0..19) -> team and driver within it.</summary>
        public static Team TeamForSlot(int i, out int driverIndex)
        {
            driverIndex = (i / All.Length) % 2;
            return All[i % All.Length];
        }

        public static string SponsorFor(Car car)
        {
            if (car.isPlayer) return "VEYLIX";
            var t = TeamForSlot(car.carIndex, out _);
            return t.sponsor;
        }

        public static string Sponsor2For(Car car)
        {
            if (car.isPlayer) return "KESTREL";
            var t = TeamForSlot(car.carIndex, out _);
            return t.sponsor2;
        }

        /// <summary>The livery an AI car runs: team colours on the car its team uses (or the session's car class).</summary>
        public static CarSetup SetupFor(int slot)
        {
            var t = TeamForSlot(slot, out int d);
            return new CarSetup
            {
                primary = CarSetup.Hex(t.primary), secondary = CarSetup.Hex(t.secondary), accent = CarSetup.Hex(t.accent),
                livery = t.livery, number = t.numbers[d], wheelStyle = (slot % 3 == 0) ? 0 : 4, rimColor = slot % 2,
            };
        }

        public static string DriverName(int slot)
        {
            var t = TeamForSlot(slot, out int d);
            return t.drivers[d];
        }

        public static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return "---";
            var parts = name.Split(' ');
            string last = parts[parts.Length - 1];
            return (last.Length >= 3 ? last.Substring(0, 3) : last).ToUpperInvariant();
        }
    }
}

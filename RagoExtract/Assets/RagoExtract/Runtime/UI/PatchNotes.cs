namespace Rago
{
    /// <summary>What changed in each update (menu > NOTES). Newest first - add new updates at the top.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("1.9", "Less lag, no kick-outs", new[] {
                "Lost connection mid-raid? You stay where you are and keep playing while the game quietly gets you back in - no bunker trip, no reload.",
                "Players who stutter or load for a moment no longer vanish and 'join back'.",
                "Much less network data: voice is half the size and only goes to players within 50 m; fewer movement updates in big raids; raider updates only near players.",
                "Sniper rifles kick much harder (marksman rifles too) - bring the scope back down between shots.",
                "Patch notes added to the menu (this page).",
            }),
            ("1.8", "Matchmaking & editor errors", new[] {
                "Fixed parties (and the 3rd player) going into 'Solo deployment': everyone queueing the same map now lands in the same raid.",
                "Party members spawn side by side; everyone else gets their own spawn.",
                "Players-online count fixed (party members were counted twice).",
                "Fixed the Mono.Cecil / import worker errors after updating (close Unity before unzipping an update).",
            }),
            ("1.7", "Lighting", new[] {
                "Leaning your head into a wall fades to black instead of showing the glitchy inside of the level.",
                "Fixed distant walls and floors flickering through each other.",
                "Bunker and Market lights no longer snap dark as you turn your head.",
                "No more black flickering shadow patches on Quest.",
            }),
            ("1.6", "Generator & recoil", new[] {
                "Generator exhaust and cables rebuilt properly (no more floating pipes and cables through the wall).",
                "Realistic recoil: every shot walks the muzzle up and to the side; full auto keeps climbing until you pull it down. Shouldered with both hands is controllable, one-handed climbs hard.",
            }),
            ("1.5", "3+ player multiplayer", new[] {
                "Fixed everyone seeing ONE frozen player with the same name when 3 or more were in a raid.",
                "Parties hold up to 3 players; raids up to 16.",
                "Network info panel next to the menu's RECENT tab (for testing).",
                "Callsigns re-sent every few seconds (no more 'Operator' names).",
                "Punching other players works again.",
            }),
            ("1.4", "Fair fights & walls", new[] {
                "No host: raiders, hits and loot are shared out between players.",
                "Shots on other players land when you hit them on your screen.",
                "No shooting, grabbing, punching or dropping things through walls.",
                "Frozen players who left are removed; dead raiders stay dead when you rejoin.",
            }),
            ("1.3", "Weather & night", new[] {
                "Rain and fog can roll in on any raid; pick DAY or NIGHT on the raid terminal.",
                "Night vision goggles in Mara's shop (wear them, then hand at forehead + trigger).",
                "Air drops are bigger; boxes no longer spawn inside floors or furniture.",
                "Kill feed removed.",
            }),
            ("1.2", "Bigger maps", new[] {
                "Outskirts: a huge map with hills, cliffs, more houses and cover.",
                "Raids take up to 16 players. Each player keeps one colour.",
                "Stamina drains slower. Punch with your fists.",
                "Scavs stop chasing you forever and don't keep coming after a kill.",
            }),
            ("1.1", "Air drops & bosses", new[] {
                "Air drops (15% of raids) with a plane, red smoke and The Courier parachuting in with the Thompson.",
                "Military radios call in air drops. Keycards and radios have their own spawn spots.",
                "Breakable windows, realistic blood, grenade sounds; flashbangs and grenades hit raiders.",
                "Kill and headshot XP go to whoever got the kill. DIED IN ACTION screen shows who killed you and with what.",
            }),
        };
    }
}

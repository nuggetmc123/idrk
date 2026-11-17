namespace Rago
{
    /// <summary>What the latest update added and fixed (menu > NOTES). Replace it with each new update.</summary>
    public static class PatchNotes
    {
        public static readonly (string version, string title, string[] lines)[] All =
        {
            ("2.6", "Spawn fix, leaving raids, locks, footsteps", new[] {
                "FIXED: party members (and friends) spawning a kilometre apart, so nobody ever saw each other - each party now always lands together again.",
                "The raid start banner lists everyone in the raid with you, so you can tell straight away you're in the same raid.",
                "Leaving the game mid-raid now counts as dying: everything you took in (and your bag) is lost.",
                "Padlocks break much more easily: a hit on the door near the lock counts, rifle rounds through the door count, and you can punch them off.",
                "Scavs hear your footsteps (walking ~8 m, running ~16 m) - crouch to sneak up silently. They hear other players too.",
                "Other players' footsteps are louder.",
                "From 2.5: Anzio 20mm, AP / incendiary ammo (switch at your mag pouch with B / Y), wall-banging, burning, guns can't go through walls.",
            }),
        };
    }
}

namespace Nullbound
{
    /// <summary>
    /// Moderator super powers, switched in the Mod Menu: INVINCIBLE (nothing hurts you), INVISIBLE (nobody else can
    /// see or hit you; you still see your own gloves) and every fighter unlocked. Only work for someone in the
    /// moderation list; other players' games also check the mod proved their ID before hiding them.
    /// </summary>
    public static class ModPowers
    {
        public static bool Invincible, Invisible;

        public static bool InvincibleNow => Invincible && Moderation.IAmMod;
        public static bool InvisibleNow => Invisible && Moderation.IAmMod;

        public static void Reset() { Invincible = Invisible = false; }
    }
}

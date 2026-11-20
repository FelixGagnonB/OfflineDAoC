using System.Linq;
using DOL.Database;
using DOL.GS.Styles;

namespace DOL.GS
{
    /// <summary>
    /// A fighter whose damage is a two-handed weapon and who trained Shields for Slam carries both
    /// and swaps between them, as players of those builds did. The owner, 2026-11-19: the Hero is
    /// 50 Large Weapons and 42 Shields, opens with Slam and then uses the two-hander's styles.
    /// Until then the Hero was built on Blades and never left its shield, and the Armsman never
    /// left its polearm: no Slam in 908 of its recorded hits.
    /// </summary>
    public static class PvpWeaponSwap
    {
        /// <summary>
        /// True for the shield, false for the two-hander. With a target: the shield only for the
        /// Slam, so while it would land and there is endurance for it. With none: the shield for
        /// a fighter that guards somebody, because guard blocks with it.
        /// </summary>
        public static bool ShieldOut(bool hasTarget, bool guards, bool slamWouldLand, bool enduranceForSlam) =>
            hasTarget ? slamWouldLand && enduranceForSlam : guards;

        /// <summary>The stun a target would take now: it is alive and neither stunned nor immune to stuns.</summary>
        /// <param name="observer">The fighter, which does not know the instant an immunity ends (PvpNotice); null for what is true.</param>
        public static bool WouldLand(GameLiving target, GameLiving observer = null) => target is { IsAlive: true, IsStunned: false } &&
            !(observer != null ? PvpNotice.ThinksImmune(observer, target, eEffect.StunImmunity)
                : target.effectListComponent.ContainsEffectForEffectType(eEffect.StunImmunity)) &&
            !target.effectListComponent.ContainsEffectForEffectType(eEffect.NPCStunImmunity);

        /// <summary>The build's best shield style that stuns; null when it has none.</summary>
        public static Style ShieldStun(GameBot bot) => bot?.Styles?.OfType<Style>()
            .Where(style => style.WeaponTypeRequirement == (int)eObjectType.Shield && style.Level <= bot.Level && PvpStylePolicy.Stuns(style))
            .OrderByDescending(style => style.GrowthRate).FirstOrDefault();

        /// <summary>
        /// True for an 8v8 fighter that swaps: a two-handed build with a two-hander, a one-hander
        /// and a shield it can use, and a shield style that stuns.
        /// </summary>
        public static bool Applies(GameBot bot, out Style slam)
        {
            slam = null;
            if (bot?.IsPvpBot != true || bot.PvpBuild?.TwoHanded != true || bot.Inventory == null)
                return false;
            DbInventoryItem shield = bot.Inventory.GetItem(eInventorySlot.LeftHandWeapon);
            if (shield?.Object_Type != (int)eObjectType.Shield ||
                !BotWeaponStats.FitsConfiguredSlot(bot, bot.Inventory.GetItem(eInventorySlot.RightHandWeapon), eInventorySlot.RightHandWeapon) ||
                !BotWeaponStats.FitsConfiguredSlot(bot, bot.Inventory.GetItem(eInventorySlot.TwoHandWeapon), eInventorySlot.TwoHandWeapon))
                return false;
            slam = ShieldStun(bot);
            return slam != null;
        }

        /// <summary>The Slam to throw on this swing: the fighter swaps and has its shield out. Null otherwise.</summary>
        public static Style SlamNow(GameBot bot) =>
            bot.ActiveWeaponSlot == eActiveWeaponSlot.Standard && Applies(bot, out Style slam) ? slam : null;

        /// <summary>
        /// True when a fighter should walk behind its target: the target is stunned, so it will
        /// not turn, the fighter has a style that only works from behind for the weapon in its
        /// hands, it is in reach, and it is not behind already. The game counts the 60 degrees
        /// at the target's back (StyleProcessor.CanUseStyle); <paramref name="angle"/> is where
        /// the fighter stands, in degrees clockwise from where the target faces.
        /// </summary>
        public static bool StepsBehind(bool targetStunned, bool hasBackStyle, float angle, int distance, int reach) =>
            targetStunned && hasBackStyle && distance <= reach && !IsBehind(angle);

        public static bool IsBehind(float angle) => angle is > 150 and < 210;

        /// <summary>The hands for this moment; null for a fighter that does not swap.</summary>
        public static eActiveWeaponSlot? Slot(GameBot bot, GameLiving target, bool guards)
        {
            if (!Applies(bot, out Style slam))
                return null;
            bool endurance = StyleProcessor.CheckEnduranceCost(bot, bot.Inventory.GetItem(eInventorySlot.LeftHandWeapon), slam);
            return ShieldOut(target != null, guards, WouldLand(target, bot), endurance) ? eActiveWeaponSlot.Standard : eActiveWeaponSlot.TwoHanded;
        }
    }
}

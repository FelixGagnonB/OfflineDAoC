using System.Linq;
using DOL.GS.Styles;

namespace DOL.GS
{
    /// <summary>
    /// Which melee styles an 8v8 bot may use. The shared bot policy was written for monsters; the
    /// first recorded scrims (2026-11-16) showed what that does against players: most styled
    /// swings were taunts, which do nothing to a player, and shield users threw Slam fourteen
    /// times a fight, mostly into stun immunity.
    /// </summary>
    public static class PvpStylePolicy
    {
        /// <summary>
        /// Taunts and detaunts never; a stun style only on a target it can actually stun.
        /// Positional styles are allowed: the game itself refuses them from the wrong side.
        /// </summary>
        public static bool Allows(bool taunt, bool detaunt, bool stuns, bool targetStunnedOrImmune) =>
            !taunt && !detaunt && !(stuns && targetStunnedOrImmune);

        public static bool Stuns(Style style) => style.Procs.Any(proc => proc.Spell?.SpellType == eSpellType.StyleStun);

        public static bool Snares(Style style) => style.Procs.Any(proc => proc.Spell?.SpellType == eSpellType.StyleSpeedDecrease);

        /// <summary>
        /// How much a style does to stop an enemy: a stun first, then a snare on a target that is
        /// not slowed yet; 0 for a style that only hurts. (A stun that would not land is refused
        /// by <see cref="Allows(bool, bool, bool, bool)"/> before this is asked.)
        /// </summary>
        public static int ControlRank(bool stuns, bool snares, bool targetSlowed) => stuns ? 2 : snares && !targetSlowed ? 1 : 0;

        /// <summary>
        /// The style for a fighter whose target should be stopped, among those it can use right
        /// now; null when none of them stuns or snares. The owner, 2026-11-18: when kiting, the
        /// fighters snare or slam whoever follows.
        /// </summary>
        public static Style ControlStyle(GameBot bot, System.Collections.Generic.IEnumerable<Style> usable)
        {
            bool slowed = TargetSlowed(bot);
            return usable.Select(style => (Style: style, Rank: ControlRank(Stuns(style), Snares(style), slowed)))
                .Where(entry => entry.Rank > 0)
                .OrderByDescending(entry => entry.Rank).ThenByDescending(entry => entry.Style.GrowthRate)
                .Select(entry => entry.Style).FirstOrDefault();
        }

        private static bool TargetSlowed(GameBot bot) => bot.TargetObject is GameLiving target &&
            (target.effectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedDebuff) ||
             target.effectListComponent.ContainsEffectForEffectType(eEffect.Snare));

        /// <summary>True for a style that would stop the fighter's target: a stun, or a snare on one that is not slowed yet.</summary>
        public static bool Stops(GameBot bot, Style style) =>
            style != null && ControlRank(Stuns(style), Snares(style), TargetSlowed(bot)) > 0;

        public static bool Allows(GameBot bot, Style style)
        {
            bool stuns = style.Procs.Any(proc => proc.Spell?.SpellType == eSpellType.StyleStun);
            bool blocked = stuns && bot.TargetObject is GameLiving target &&
                (target.IsStunned || target.effectListComponent.ContainsEffectForEffectType(eEffect.StunImmunity) ||
                 target.effectListComponent.ContainsEffectForEffectType(eEffect.NPCStunImmunity));
            return Allows(bot.StylesTaunt?.Contains(style) == true, bot.StylesDetaunt?.Contains(style) == true, stuns, blocked);
        }
    }
}

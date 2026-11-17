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

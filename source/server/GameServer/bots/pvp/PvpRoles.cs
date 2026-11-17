using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>What a character is in the group for (docs/PVP-8V8-ROADMAP.md, M5.3).</summary>
    public enum PvpRole
    {
        /// <summary>Kills the called target.</summary>
        Assist,
        /// <summary>Mezzes, roots and stuns the enemies that are not being killed.</summary>
        Control,
        /// <summary>Keeps enemy healers and casters from casting.</summary>
        Interrupt,
        /// <summary>Keeps the group alive.</summary>
        Heal,
        /// <summary>A shield fighter: guards a healer or caster and goes for the enemies on them (bots/pvp/PvpPeel.cs).</summary>
        Peel,
    }

    /// <summary>
    /// Roles by class, and what the two non-assist roles aim at. The owner's rule (2026-11-16):
    /// the Sorcerer is Albion's main crowd controller and interrupter and the Theurgist keeps
    /// its pets on enemy healers and casters; both join the kill only when it is critical.
    /// </summary>
    public static class PvpRoles
    {
        /// <summary>A controller or interrupter joins the kill when the called target is this low.</summary>
        public const int CriticalHealthPercent = 35;

        public static PvpRole Of(eCharacterClass characterClass) => characterClass switch
        {
            eCharacterClass.Cleric or eCharacterClass.Druid or eCharacterClass.Healer or eCharacterClass.Shaman or
                eCharacterClass.Friar => PvpRole.Heal,
            eCharacterClass.Sorcerer or eCharacterClass.Bard => PvpRole.Control,
            eCharacterClass.Theurgist => PvpRole.Interrupt,
            // The groups' shield fighters. An Armsman with a polearm carries no shield and assists.
            eCharacterClass.Paladin or eCharacterClass.Warrior or eCharacterClass.Hero => PvpRole.Peel,
            _ => PvpRole.Assist,
        };

        /// <summary>True when a controller or interrupter should drop its own job and help kill.</summary>
        public static bool JoinsKill(PvpRole role, int killTargetHealthPercent) =>
            role == PvpRole.Assist || role is PvpRole.Control or PvpRole.Interrupt && killTargetHealthPercent <= CriticalHealthPercent;
    }
}

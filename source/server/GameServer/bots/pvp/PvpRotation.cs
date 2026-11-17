using System.Collections.Generic;

namespace DOL.GS
{
    /// <summary>Whom one step of a rotation is cast on.</summary>
    public enum PvpAim
    {
        /// <summary>The group's kill target.</summary>
        Kill,
        /// <summary>The kill target first, then any enemy that is awake and lacks the effect.</summary>
        KillThenAwake,
        /// <summary>An enemy healer or caster that is awake and lacks the effect.</summary>
        FreeCaster,
        /// <summary>The enemy this bot was told to keep interrupted.</summary>
        Assigned,
    }

    /// <summary>
    /// One step: cast the best spell of these types on that aim. <paramref name="Spam"/> casts
    /// even when the target already has the effect or is immune, because the cast itself
    /// interrupts (the owner's example: a Shaman rooting a root-immune caster).
    /// </summary>
    public sealed record PvpRotationStep(PvpAim Aim, bool Spam, params eSpellType[] Types);

    /// <summary>
    /// What each caster does with its turn in an 8v8 fight, in order; the first step that can be
    /// cast is cast (docs/PVP-8V8-ROADMAP.md, M6.4). It replaces the old brain's random pick,
    /// under which the Shaman cast no disease and the Sorcerer cast stat debuffs until it was out
    /// of power. The orders are the owner's, given on 2026-11-16:
    ///   Shaman      disease on everything, then root as an interrupt.
    ///   Eldritch    nearsight on casters, then disease, then direct damage; and round again.
    ///   Cabalist    disease, then damage on the kill target (its pet is still to be handled).
    ///   Theurgist   pets on the enemy it is keeping interrupted, then damage on it.
    /// Crowd control (mez) is decided before this, by BotBrain.PvpCrowdControl.
    /// </summary>
    public static class PvpRotation
    {
        private static readonly eSpellType[] Damage = [eSpellType.DirectDamage, eSpellType.Lifedrain, eSpellType.DirectDamageWithDebuff];

        private static readonly Dictionary<eCharacterClass, PvpRotationStep[]> Steps = new()
        {
            [eCharacterClass.Shaman] =
            [
                new(PvpAim.KillThenAwake, false, eSpellType.Disease),
                new(PvpAim.FreeCaster, true, eSpellType.SpeedDecrease),
            ],
            [eCharacterClass.Eldritch] =
            [
                new(PvpAim.FreeCaster, false, eSpellType.Nearsight),
                new(PvpAim.KillThenAwake, false, eSpellType.Disease),
                new(PvpAim.Kill, false, Damage),
            ],
            [eCharacterClass.Cabalist] =
            [
                new(PvpAim.KillThenAwake, false, eSpellType.Disease),
                new(PvpAim.Kill, false, Damage),
            ],
            [eCharacterClass.Runemaster] =
            [
                new(PvpAim.FreeCaster, false, eSpellType.Nearsight),
                new(PvpAim.Kill, false, Damage),
            ],
            [eCharacterClass.Theurgist] =
            [
                new(PvpAim.Assigned, true, eSpellType.SummonTheurgistPet),
                new(PvpAim.Assigned, false, Damage),
            ],
            [eCharacterClass.Sorcerer] = [new(PvpAim.Kill, false, Damage)],
            [eCharacterClass.Wizard] = [new(PvpAim.Kill, false, Damage)],
            [eCharacterClass.Enchanter] = [new(PvpAim.Kill, false, Damage)],
            [eCharacterClass.Mentalist] = [new(PvpAim.Kill, false, Damage)],
            [eCharacterClass.Spiritmaster] = [new(PvpAim.Kill, false, Damage)],
        };

        public static IReadOnlyList<PvpRotationStep> For(eCharacterClass characterClass) =>
            Steps.TryGetValue(characterClass, out PvpRotationStep[] steps) ? steps : [];
    }
}

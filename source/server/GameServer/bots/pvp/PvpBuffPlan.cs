using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>The concentration buffs an 8v8 group hands out before a fight.</summary>
    public enum PvpBuffKind
    {
        SpecDexQui,
        SpecStrCon,
        Acuity,
        Haste,
        SpecArmor,
        BaseStrength,
        BaseDexterity,
        BaseConstitution,
        BaseArmor,
    }

    /// <summary>What a group member fights with, which decides the buffs it needs first.</summary>
    public enum PvpBuffArchetype { Melee, Caster, Healer }

    /// <summary>A group member and the concentration it can still spend on others.</summary>
    public readonly record struct PvpBuffMember(int Id, PvpBuffArchetype Archetype, int Concentration);

    /// <summary>The best buff of one kind a member can cast, with its value and concentration cost.</summary>
    public readonly record struct PvpBuffOffer(int Caster, PvpBuffKind Kind, int Value, int Concentration);

    public readonly record struct PvpBuffAssignment(int Caster, int Target, PvpBuffKind Kind, int Value);

    public sealed record PvpBuffPlanResult(
        IReadOnlyList<PvpBuffAssignment> Assignments,
        IReadOnlyList<(int Target, PvpBuffKind Kind)> Unfilled);

    /// <summary>
    /// Decides who buffs whom in a group that has no buffbot (docs/PVP-8V8-ROADMAP.md, M1.7).
    /// Two rules: a buff is only ever cast by a member who has the group's strongest version of
    /// it, so nobody ends up holding a weak one; and concentration is spent in order of need, so
    /// when it runs out the buffs that go missing are the least useful ones. The owner's target
    /// is Dex/Qui and Str/Con on every member, so both sit in everyone's first two priorities.
    /// </summary>
    public static class PvpBuffPlan
    {
        // Lower is needed sooner. A kind missing from a list is not cast on that archetype at all.
        private static readonly Dictionary<PvpBuffArchetype, (PvpBuffKind Kind, int Priority)[]> Needs = new()
        {
            // The owner's rule (2026-11-16): every character gets Str/Con and Dex/Qui, and nobody
            // is given base armour.
            [PvpBuffArchetype.Melee] =
            [
                (PvpBuffKind.SpecStrCon, 1), (PvpBuffKind.SpecDexQui, 1), (PvpBuffKind.BaseStrength, 2),
                (PvpBuffKind.Haste, 2), (PvpBuffKind.SpecArmor, 3), (PvpBuffKind.BaseConstitution, 3),
                (PvpBuffKind.BaseDexterity, 3),
            ],
            [PvpBuffArchetype.Caster] =
            [
                (PvpBuffKind.SpecDexQui, 1), (PvpBuffKind.SpecStrCon, 1), (PvpBuffKind.Acuity, 2),
                (PvpBuffKind.BaseDexterity, 3), (PvpBuffKind.BaseConstitution, 3),
            ],
            [PvpBuffArchetype.Healer] =
            [
                (PvpBuffKind.SpecDexQui, 1), (PvpBuffKind.SpecStrCon, 1), (PvpBuffKind.Acuity, 2),
                (PvpBuffKind.BaseDexterity, 3), (PvpBuffKind.BaseConstitution, 3),
            ],
        };

        /// <summary>
        /// A buff on the whole group is cast by the member that has the group's strongest version,
        /// and by nobody else; of two with the same, the one whose name sorts first.
        /// </summary>
        public static bool CastsGroupBuff(double ownValue, string ownName, IEnumerable<(double Value, string Name)> others) =>
            !others.Any(other => other.Value > ownValue || other.Value == ownValue && string.CompareOrdinal(other.Name, ownName) < 0);

        public static IReadOnlyList<PvpBuffKind> NeedsOf(PvpBuffArchetype archetype) =>
            Needs[archetype].OrderBy(need => need.Priority).Select(need => need.Kind).ToList();

        public static PvpBuffArchetype ArchetypeOf(eCharacterClass characterClass) => characterClass switch
        {
            eCharacterClass.Cleric or eCharacterClass.Druid or eCharacterClass.Healer or eCharacterClass.Shaman or
            eCharacterClass.Bard or eCharacterClass.Friar => PvpBuffArchetype.Healer,
            eCharacterClass.Sorcerer or eCharacterClass.Theurgist or eCharacterClass.Cabalist or
            eCharacterClass.Wizard or eCharacterClass.Necromancer or eCharacterClass.Runemaster or
            eCharacterClass.Spiritmaster or eCharacterClass.Bonedancer or eCharacterClass.Eldritch or
            eCharacterClass.Enchanter or eCharacterClass.Mentalist or eCharacterClass.Animist => PvpBuffArchetype.Caster,
            _ => PvpBuffArchetype.Melee,
        };

        /// <summary>
        /// Spells no 8v8 bot casts, and no pet of one. The owner, 2026-11-20: "General rule for
        /// all 3 realms, do not cast damage shield." Until then a Healer put one on itself, an
        /// Eldritch and a Druid on the group, and a Cabalist's simulacrum on itself (52, 46, 11
        /// and 63 casts in the last sixty recordings of that day).
        /// </summary>
        public static bool NeverCast(eSpellType type) => type == eSpellType.DamageShield;

        /// <summary>
        /// The planned kind of a spell, or null for anything the plan leaves alone: self buffs,
        /// group resists, songs and chants, pet buffs. Planned buffs are the single-target ones
        /// that hold concentration.
        /// </summary>
        public static PvpBuffKind? KindOf(eSpellType type, eSpellTarget target, int concentration)
        {
            if (target != eSpellTarget.REALM || concentration <= 0)
                return null;
            return type switch
            {
                eSpellType.DexterityQuicknessBuff => PvpBuffKind.SpecDexQui,
                eSpellType.StrengthConstitutionBuff => PvpBuffKind.SpecStrCon,
                eSpellType.AcuityBuff => PvpBuffKind.Acuity,
                eSpellType.CombatSpeedBuff => PvpBuffKind.Haste,
                eSpellType.SpecArmorFactorBuff => PvpBuffKind.SpecArmor,
                eSpellType.StrengthBuff => PvpBuffKind.BaseStrength,
                eSpellType.DexterityBuff => PvpBuffKind.BaseDexterity,
                eSpellType.ConstitutionBuff => PvpBuffKind.BaseConstitution,
                eSpellType.BaseArmorFactorBuff => PvpBuffKind.BaseArmor,
                _ => null,
            };
        }

        /// <param name="covered">True when a member already holds a buff of that kind, from anyone:
        /// the plan then leaves it alone. With each member's free concentration passed in, the plan
        /// only ever hands out what is missing, so it gives the same answer every time it is rebuilt.</param>
        public static PvpBuffPlanResult Build(IReadOnlyList<PvpBuffMember> members, IReadOnlyList<PvpBuffOffer> offers,
            Func<int, PvpBuffKind, bool> covered = null)
        {
            // Only the strongest version of each kind in the group may be cast.
            Dictionary<PvpBuffKind, int> best = offers.GroupBy(offer => offer.Kind)
                .ToDictionary(group => group.Key, group => group.Max(offer => offer.Value));
            Dictionary<PvpBuffKind, List<PvpBuffOffer>> casters = offers
                .Where(offer => offer.Value == best[offer.Kind])
                .GroupBy(offer => offer.Kind)
                .ToDictionary(group => group.Key, group => group.ToList());
            Dictionary<int, int> remaining = members.ToDictionary(member => member.Id, member => member.Concentration);

            // Within a priority every member gets its own first need before anyone gets a second
            // one. Sorting by buff kind instead spent a whole pool on Dex/Qui for all eight and
            // left the melee without Str/Con.
            var wants = members
                .SelectMany(member => Needs[member.Archetype]
                    .Select((need, order) => (member.Id, need.Kind, need.Priority, Order: order)))
                .OrderBy(want => want.Priority).ThenBy(want => want.Order).ThenBy(want => want.Id)
                .Select(want => (want.Id, want.Kind, want.Priority));

            var assignments = new List<PvpBuffAssignment>();
            var unfilled = new List<(int, PvpBuffKind)>();
            foreach ((int target, PvpBuffKind kind, _) in wants)
            {
                if (!casters.TryGetValue(kind, out List<PvpBuffOffer> able))
                    continue;   // nobody in the group has this buff: not a shortage
                if (covered != null && covered(target, kind))
                    continue;
                PvpBuffOffer? chosen = null;
                foreach (PvpBuffOffer offer in able)
                {
                    if (!remaining.TryGetValue(offer.Caster, out int left) || left < offer.Concentration)
                        continue;
                    if (chosen == null || left > remaining[chosen.Value.Caster])
                        chosen = offer;
                }
                if (chosen == null)
                {
                    unfilled.Add((target, kind));
                    continue;
                }
                remaining[chosen.Value.Caster] -= chosen.Value.Concentration;
                assignments.Add(new(chosen.Value.Caster, target, kind, chosen.Value.Value));
            }
            return new(assignments, unfilled);
        }
    }
}

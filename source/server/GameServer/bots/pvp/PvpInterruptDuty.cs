using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>How a class keeps an enemy from casting.</summary>
    public enum PvpInterruptMeans
    {
        None,
        /// <summary>Pets sent at the enemy (Theurgist).</summary>
        Pets,
        /// <summary>Spells cast at it between the caster's other work: nearsight, disease, root.</summary>
        Spells,
        /// <summary>Runs to it and hits it, with instant shouts on the way (Skald, Minstrel). It leaves the kill for that.</summary>
        Melee,
    }

    /// <summary>One interrupter and one enemy healer or caster it could be given.</summary>
    /// <param name="Free">Nothing is stopping the enemy from casting right now.</param>
    /// <param name="IsCurrent">This interrupter has that enemy already.</param>
    /// <param name="IsController">The enemy is one of the other group's mezzers.</param>
    public readonly record struct PvpDutyPair(int Interrupter, int Enemy, PvpBuffArchetype Archetype, bool Casting, bool Free,
        bool IsCurrent, int Distance, bool IsController = false);

    /// <summary>Part of <see cref="PvpWeights"/>: who interrupts, by what means, and how the pairs are weighed.</summary>
    public sealed class PvpInterruptWeights
    {
        /// <summary>
        /// The owner's list of 2026-11-16: any caster, Skald, Minstrel, the Theurgist's pets, Shaman.
        /// A class left out, or set to "None", has no interrupt duty.
        /// </summary>
        public Dictionary<string, string> Means { get; set; } = new()
        {
            ["Theurgist"] = "Pets",
            ["Skald"] = "Melee",
            ["Minstrel"] = "Melee",
            ["Shaman"] = "Spells",
            ["Cabalist"] = "Spells",
            ["Runemaster"] = "Spells",
            ["Eldritch"] = "Spells",
            ["Enchanter"] = "Spells",
            ["Wizard"] = "Spells",
            ["Spiritmaster"] = "Spells",
            ["Bonedancer"] = "Spells",
            ["Mentalist"] = "Spells",
        };

        public int OnHealer { get; set; } = 40;
        public int OnCaster { get; set; } = 30;
        /// <summary>
        /// The enemy is one of the other group's mezzers. Left alone, a mezzer got six or seven
        /// mezzes off in the first ten seconds (recordings of 2026-11-17). 0 until a comparison
        /// against the weights without it says what it is worth.
        /// </summary>
        public int OnController { get; set; } = 0;
        public int Casting { get; set; } = 30;
        public int Free { get; set; } = 15;
        /// <summary>Keeps an interrupter on the enemy it has, so the pairs do not reshuffle every second.</summary>
        public int IsCurrent { get; set; } = 20;
        public double PerHundredUnits { get; set; } = -2;
        /// <summary>A pair scoring less is not made; that interrupter does its ordinary work.</summary>
        public int MinScore { get; set; } = 20;
        /// <summary>Enemies farther than this from an interrupter are not given to it.</summary>
        public int Range { get; set; } = 2_500;
        /// <summary>
        /// A healer's pet (a Druid's) is sent at an enemy healer or caster (PvpInterruptDuty.PetOfAHealer).
        /// Until 2026-11-26 nothing sent it anywhere and it defended its owner. The owner, of a
        /// fight against Midgard: "Druid had their pets on fighter". The two pets hit the
        /// Berserker, the Savage, the Warrior and the Skald 57 times for 130 damage, and a
        /// caster or a healer twice. False: as it was.
        /// </summary>
        public bool HealersPetsInterrupt { get; set; } = true;
    }

    /// <summary>
    /// Interrupt duty (docs/PVP-8V8-ROADMAP.md, M6.2): each enemy healer and caster that is not
    /// being killed and not asleep gets one interrupter of its own, so that the group's
    /// interrupts are spread over the enemy's backline. Before this only the Theurgist had a target, and in
    /// the ten-match runs of 2026-11-17 nothing at all landed on Albion's Sorcerer.
    /// </summary>
    public static class PvpInterruptDuty
    {
        public static PvpInterruptMeans MeansOf(eCharacterClass characterClass, PvpInterruptWeights weights) =>
            weights.Means.TryGetValue(characterClass.ToString(), out string means) &&
            System.Enum.TryParse(means, true, out PvpInterruptMeans parsed) ? parsed : PvpInterruptMeans.None;

        /// <summary>
        /// Where a healer's pet goes: the enemy healer or caster an interrupter would be given
        /// (in range, scoring enough), one that has no interrupter yet before one that has, and
        /// the best score among those. The caller leaves out fighters, the kill target and
        /// whoever is asleep or being put to sleep. -1: none, and the pet stays with its owner.
        /// </summary>
        public static int PetOfAHealer(IEnumerable<(PvpDutyPair Pair, bool HasAnInterrupter)> open, PvpInterruptWeights weights) =>
            open.Where(one => one.Pair.Distance <= weights.Range && Score(one.Pair, weights) >= weights.MinScore)
                .OrderBy(one => one.HasAnInterrupter ? 1 : 0).ThenByDescending(one => Score(one.Pair, weights)).ThenBy(one => one.Pair.Enemy)
                .Select(one => one.Pair.Enemy).DefaultIfEmpty(-1).First();

        public static double Score(PvpDutyPair pair, PvpInterruptWeights weights) =>
            (pair.Archetype == PvpBuffArchetype.Healer ? weights.OnHealer : weights.OnCaster)
            + (pair.IsController ? weights.OnController : 0)
            + (pair.Casting ? weights.Casting : 0)
            + (pair.Free ? weights.Free : 0)
            + (pair.IsCurrent ? weights.IsCurrent : 0)
            + pair.Distance / 100.0 * weights.PerHundredUnits;

        /// <summary>
        /// Interrupter to enemy. The best-scoring pair is made first, then the best among the
        /// interrupters and enemies still free, and so on: no enemy gets two interrupters while
        /// another has none. The caller leaves out fighters, the kill target and mezzed enemies.
        /// </summary>
        public static Dictionary<int, int> Assign(IEnumerable<PvpDutyPair> pairs, PvpInterruptWeights weights)
        {
            var duty = new Dictionary<int, int>();
            var taken = new HashSet<int>();
            foreach (PvpDutyPair pair in pairs
                         .Where(pair => pair.Distance <= weights.Range && Score(pair, weights) >= weights.MinScore)
                         .OrderByDescending(pair => Score(pair, weights)).ThenBy(pair => pair.Interrupter).ThenBy(pair => pair.Enemy))
            {
                if (duty.ContainsKey(pair.Interrupter) || !taken.Add(pair.Enemy))
                    continue;
                duty[pair.Interrupter] = pair.Enemy;
            }
            return duty;
        }
    }
}

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
    public readonly record struct PvpDutyPair(int Interrupter, int Enemy, PvpBuffArchetype Archetype, bool Casting, bool Free,
        bool IsCurrent, int Distance);

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
            ["Mentalist"] = "Spells",
        };

        public int OnHealer { get; set; } = 40;
        public int OnCaster { get; set; } = 30;
        public int Casting { get; set; } = 30;
        public int Free { get; set; } = 15;
        /// <summary>Keeps an interrupter on the enemy it has, so the pairs do not reshuffle every second.</summary>
        public int IsCurrent { get; set; } = 20;
        public double PerHundredUnits { get; set; } = -2;
        /// <summary>A pair scoring less is not made; that interrupter does its ordinary work.</summary>
        public int MinScore { get; set; } = 20;
        /// <summary>Enemies farther than this from an interrupter are not given to it.</summary>
        public int Range { get; set; } = 2_500;
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

        public static double Score(PvpDutyPair pair, PvpInterruptWeights weights) =>
            (pair.Archetype == PvpBuffArchetype.Healer ? weights.OnHealer : weights.OnCaster)
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

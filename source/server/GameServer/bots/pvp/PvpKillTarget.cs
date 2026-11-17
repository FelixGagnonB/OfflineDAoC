using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>What a group knows about one enemy when it picks whom to kill.</summary>
    public readonly record struct PvpTargetView(int Id, PvpBuffArchetype Archetype, int HealthPercent, bool Mezzed, int Distance);

    /// <summary>
    /// The group's kill target (docs/PVP-8V8-ROADMAP.md, M5.5): one enemy that every damage
    /// dealer attacks until it is dead. The recorded old-brain fights had each fighter on seven to
    /// nine targets in two and a half minutes and nobody died.
    /// </summary>
    public static class PvpKillTarget
    {
        /// <summary>The current target is kept unless another scores this much higher.</summary>
        public const int SwitchMargin = 60;
        public const int MaxDistance = 3_500;

        /// <summary>
        /// Higher is a better kill. Healers first, then casters: they are the softest and matter
        /// most. A wounded enemy is closer to dead. A mezzed one is left alone while there is
        /// anyone else, because hitting it wakes it. Far ones cost running time.
        /// </summary>
        public static int Score(PvpTargetView enemy) =>
            (enemy.Archetype switch { PvpBuffArchetype.Healer => 200, PvpBuffArchetype.Caster => 150, _ => 50 })
            + (100 - enemy.HealthPercent)
            - (enemy.Mezzed ? 120 : 0)
            - enemy.Distance / 40;

        /// <summary>The enemy to kill now, given the one being killed already (-1 for none).</summary>
        public static int Choose(IReadOnlyList<PvpTargetView> enemies, int currentId)
        {
            List<PvpTargetView> reachable = enemies.Where(enemy => enemy.Distance <= MaxDistance).ToList();
            if (reachable.Count == 0)
                return -1;
            PvpTargetView best = reachable.OrderByDescending(Score).ThenBy(enemy => enemy.Id).First();
            int current = reachable.FindIndex(enemy => enemy.Id == currentId);
            return current >= 0 && Score(best) < Score(reachable[current]) + SwitchMargin ? currentId : best.Id;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>Part of <see cref="PvpWeights"/>: where a group's healers and casters stand in a fight.</summary>
    public sealed class PvpPositionWeights
    {
        /// <summary>A healer or caster wants every enemy fighter at least this far away. 0 turns positioning off.</summary>
        public int SafeDistance { get; set; } = 900;
        /// <summary>
        /// And it wants every enemy of any kind at least this far away, as long as its work stays
        /// in reach: it stands at the edge of its range, not in the middle of it. The owner,
        /// watching on 2026-11-18: "The fight is way too tight. There are casters in the middle of
        /// melee." Recordings of that evening: the middles of the two groups' healers and casters
        /// were 1,870 units apart five seconds after contact, 1,125 at twenty and 850 at forty-five;
        /// casters began their casts from a median of 740 to 925 units with spells that reach
        /// 1,500. Nothing moved a caster back: only an enemy fighter within
        /// <see cref="SafeDistance"/> made it step. 0 = as before.
        /// </summary>
        public int EdgeDistance { get; set; } = 1_200;
        /// <summary>What a unit inside that distance costs, against a unit gained from an enemy fighter.</summary>
        public double PerUnitInsideEdge { get; set; } = 0.75;
        /// <summary>
        /// How much staying in range of the group's kill target counts when a caster chooses where
        /// to stand, against 1 for the enemy it is itself casting on, and the same for the enemy
        /// it has interrupt duty for. At 1 (as it was) being in range of a kill target in the
        /// enemy's back row outweighed the edge four to one and stood the casters among the
        /// enemy's front casters.
        /// </summary>
        public double KillTargetRangeWeight { get; set; } = 0.25;
        public double DutyTargetRangeWeight { get; set; } = 0.5;
        /// <summary>How far one step is.</summary>
        public int Step { get; set; } = 350;
        /// <summary>
        /// A longer step, taken when it is the better one: in the first runs a third of all steps
        /// were followed by another step, a caster walking away 350 units at a time and casting
        /// nothing in between. 0 leaves only the short step.
        /// </summary>
        public int LongStep { get; set; } = 700;
        /// <summary>A step is only taken when it leaves the bot this much better off (in units of distance).</summary>
        public int MinGain { get; set; } = 150;
        /// <summary>A caster stays within this of the enemy it is working on (its spells reach 1,500).</summary>
        public int WorkRange { get; set; } = 1_350;
        /// <summary>A healer stays within this of the members it heals, and everyone within this of their healers (heals reach 2,000).</summary>
        public int HealRange { get; set; } = 1_700;
        /// <summary>
        /// A free enemy fighter on the bot and this close pins it: both run at the same speed, so
        /// walking away gains nothing until the fighter is mezzed, stunned, rooted or slowed. The
        /// first recorded run (2026-11-18, pinned only inside 250) had 144 such steps: the fighter
        /// was farther away two seconds later after 43% of them, 7 units nearer at the median.
        /// </summary>
        public int PinnedWithin { get; set; } = 600;
        /// <summary>What a unit outside work or heal range costs, against a unit gained from an enemy fighter.</summary>
        public double PerUnitOutOfRange { get; set; } = 2;
        /// <summary>What a unit inside the keep-apart distance of a groupmate costs.</summary>
        public double PerUnitCrowded { get; set; } = 1;
        /// <summary>A healer does not step away while a member in its reach is below this health, unless the enemies on it are held.</summary>
        public int HealerStaysIfMemberBelow { get; set; } = 60;
        /// <summary>
        /// After a step the bot works from its new place for at least this long before the next
        /// one. A member running from fighters that are held does not wait.
        /// </summary>
        public int StepEveryMilliseconds { get; set; } = 4_000;
        /// <summary>
        /// Under Extend a place is worth this much more for each unit it lies farther along the
        /// group's line of retreat (straight away from the enemy), so that the healers and casters
        /// fall back the same way. Playbook, section 3: "a retreat is one group moving one way,
        /// not eight paths". 0 = each steps wherever it is safest for itself.
        /// </summary>
        public double PerUnitAlongRetreat { get; set; } = 0.3;
        /// <summary>
        /// A caster or control healer whose scored choice finds nothing worth casting stands where
        /// it is. Otherwise the old brain takes the turn and walks it to spell range of its attack
        /// target, which for a kill target in the enemy's back row means into the enemy: in the
        /// 150 matches of 2026-11-18 casters stood 41 to 56% of the way to where the enemy group
        /// had been, the Bard and the Pacification Healer died first, and with them the only cure
        /// for their healers' mez. It still walks when a cast that needs a walk is worth it.
        /// </summary>
        public bool CastersStandWithNothingToCast { get; set; } = true;
        /// <summary>
        /// On incoming a healer that only heals (Cleric, Druid, Mending Healer) walks no closer to
        /// the nearest enemy than this: outside the longest mez (1,875 and the width of its area),
        /// so the opening mez cannot catch it. 0 = it walks in with the others. The owner,
        /// 2026-11-17: a healer may run off on the incoming and come back into range afterwards,
        /// so as not to be targeted or mezzed.
        /// </summary>
        public int HealerHangsBackAt { get; set; } = 2_300;
        /// <summary>It comes in this long after the first blow: the fight has settled and the opening mezzes are spent.</summary>
        public int HealerHangsBackSeconds { get; set; } = 5;

        /// <summary>
        /// True while such a healer should stay where it is. <paramref name="secondsSinceContact"/>
        /// is negative before the first blow.
        /// </summary>
        public bool HealerHangsBack(bool onlyHeals, int nearestEnemy, double secondsSinceContact) =>
            onlyHeals && HealerHangsBackAt > 0 && nearestEnemy < HealerHangsBackAt && secondsSinceContact < HealerHangsBackSeconds;
    }

    /// <summary>An enemy fighter as a healer or caster sees it when choosing where to stand.</summary>
    public readonly record struct PvpThreat(Vector2 At, bool Held, bool Slowed, bool OnMe);

    /// <summary>Something a bot must stay in range of: an enemy it works on, a member it heals, its own healer.</summary>
    public readonly record struct PvpWorkPoint(Vector2 At, int Range, double Weight = 1);

    /// <summary>
    /// Where healers and casters stand (docs/PVP-8V8-PLAYBOOK.md, sections 4 and 5). The owner,
    /// 2026-11-18: casters and healers stand near the edge of their range and away from enemy
    /// fighters; a trained member, once the enemies on it are peeled, mezzed or stunned, runs to
    /// get distance. Recordings of that day: a healer or caster had an enemy fighter within 500
    /// units 57% of the time, and for two thirds of that time the fighter was on somebody else or
    /// was itself mezzed or stunned, so stepping away was free.
    /// This is a weighing, not a rule: a step is scored by the distance it gains from enemy
    /// fighters against what it costs in range to the bot's work and in crowding its groupmates.
    /// </summary>
    public static class PvpPositioning
    {
        private const int Directions = 16;

        /// <summary>Why a bot is not stepping, for the recorder and the tests.</summary>
        public enum Verdict { Step, Safe, Pinned, NothingBetter }

        /// <summary>
        /// The places worth stepping to, best first; empty when the bot is better off where it
        /// stands. <paramref name="safeDistance"/> is the weights' distance as the group's order
        /// has scaled it.
        /// </summary>
        /// <param name="retreat">The group's line of retreat as a vector of length one; zero when it is not falling back.</param>
        /// <param name="others">The enemy's healers and casters, the ones awake.</param>
        /// <param name="edge">How far the bot wants every enemy, as the group's order has scaled it; 0 = only fighters count.</param>
        public static List<Vector2> Steps(Vector2 self, IReadOnlyList<PvpThreat> threats, IReadOnlyList<PvpWorkPoint> work,
            IReadOnlyList<Vector2> mates, int minSpacing, int safeDistance, PvpPositionWeights weights, out Verdict verdict, Vector2 retreat = default,
            IReadOnlyList<Vector2> others = null, int edge = 0)
        {
            verdict = Verdict.Safe;
            bool fighterNear = threats.Count > 0 && threats.Min(threat => Vector2.Distance(self, threat.At)) < safeDistance;
            bool insideEdge = edge > 0 && NearestEnemy(self, threats, others) < edge;
            if (safeDistance <= 0 || !fighterNear && !insideEdge)
                return [];
            // A fighter that can follow is on it and close: running gains nothing.
            if (threats.Any(threat => threat.OnMe && !threat.Held && !threat.Slowed && Vector2.Distance(self, threat.At) <= weights.PinnedWithin))
            {
                verdict = Verdict.Pinned;
                return [];
            }
            double here = Score(self, threats, work, mates, minSpacing, safeDistance, weights, others, edge);
            double Along(Vector2 point) => Vector2.Dot(point - self, retreat) * weights.PerUnitAlongRetreat;
            var better = new List<(Vector2 Point, double Score)>();
            foreach (int length in weights.LongStep > weights.Step ? new[] { weights.Step, weights.LongStep } : [weights.Step])
            {
                for (int i = 0; i < Directions; i++)
                {
                    double angle = 2 * Math.PI * i / Directions;
                    Vector2 point = self + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * length;
                    double score = Score(point, threats, work, mates, minSpacing, safeDistance, weights, others, edge) + Along(point);
                    if (score - here >= weights.MinGain)
                        better.Add((point, score));
                }
            }
            verdict = better.Count > 0 ? Verdict.Step : Verdict.NothingBetter;
            return better.OrderByDescending(entry => entry.Score).Select(entry => entry.Point).ToList();
        }

        /// <summary>The distance to the nearest enemy of any kind; very far when there is none.</summary>
        public static double NearestEnemy(Vector2 point, IReadOnlyList<PvpThreat> threats, IReadOnlyList<Vector2> others)
        {
            double nearest = double.MaxValue;
            foreach (PvpThreat threat in threats)
                nearest = Math.Min(nearest, Vector2.Distance(point, threat.At));
            if (others != null)
            {
                foreach (Vector2 other in others)
                    nearest = Math.Min(nearest, Vector2.Distance(point, other));
            }
            return nearest;
        }

        /// <summary>
        /// How good a place is: distance from the nearest enemy fighter (no better than safe) and,
        /// for less, from the nearest enemy of any kind (no better than the edge), less what it costs.
        /// </summary>
        public static double Score(Vector2 point, IReadOnlyList<PvpThreat> threats, IReadOnlyList<PvpWorkPoint> work,
            IReadOnlyList<Vector2> mates, int minSpacing, int safeDistance, PvpPositionWeights weights,
            IReadOnlyList<Vector2> others = null, int edge = 0)
        {
            double score = threats.Count == 0 ? safeDistance : Math.Min(safeDistance, threats.Min(threat => Vector2.Distance(point, threat.At)));
            if (edge > 0)
                score += weights.PerUnitInsideEdge * Math.Min(edge, NearestEnemy(point, threats, others));
            foreach (PvpWorkPoint item in work)
                score -= Math.Max(0, Vector2.Distance(point, item.At) - item.Range) * weights.PerUnitOutOfRange * item.Weight;
            foreach (Vector2 mate in mates)
                score -= Math.Max(0, minSpacing - Vector2.Distance(point, mate)) * weights.PerUnitCrowded;
            return score;
        }
    }
}

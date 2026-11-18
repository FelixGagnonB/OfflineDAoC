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
        public static List<Vector2> Steps(Vector2 self, IReadOnlyList<PvpThreat> threats, IReadOnlyList<PvpWorkPoint> work,
            IReadOnlyList<Vector2> mates, int minSpacing, int safeDistance, PvpPositionWeights weights, out Verdict verdict)
        {
            verdict = Verdict.Safe;
            if (safeDistance <= 0 || threats.Count == 0 || threats.Min(threat => Vector2.Distance(self, threat.At)) >= safeDistance)
                return [];
            // A fighter that can follow is on it and close: running gains nothing.
            if (threats.Any(threat => threat.OnMe && !threat.Held && !threat.Slowed && Vector2.Distance(self, threat.At) <= weights.PinnedWithin))
            {
                verdict = Verdict.Pinned;
                return [];
            }
            double here = Score(self, threats, work, mates, minSpacing, safeDistance, weights);
            var better = new List<(Vector2 Point, double Score)>();
            foreach (int length in weights.LongStep > weights.Step ? new[] { weights.Step, weights.LongStep } : [weights.Step])
            {
                for (int i = 0; i < Directions; i++)
                {
                    double angle = 2 * Math.PI * i / Directions;
                    Vector2 point = self + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * length;
                    double score = Score(point, threats, work, mates, minSpacing, safeDistance, weights);
                    if (score - here >= weights.MinGain)
                        better.Add((point, score));
                }
            }
            verdict = better.Count > 0 ? Verdict.Step : Verdict.NothingBetter;
            return better.OrderByDescending(entry => entry.Score).Select(entry => entry.Point).ToList();
        }

        /// <summary>How good a place is: distance from the nearest enemy fighter (no better than safe), less what it costs.</summary>
        public static double Score(Vector2 point, IReadOnlyList<PvpThreat> threats, IReadOnlyList<PvpWorkPoint> work,
            IReadOnlyList<Vector2> mates, int minSpacing, int safeDistance, PvpPositionWeights weights)
        {
            double score = threats.Count == 0 ? safeDistance : Math.Min(safeDistance, threats.Min(threat => Vector2.Distance(point, threat.At)));
            foreach (PvpWorkPoint item in work)
                score -= Math.Max(0, Vector2.Distance(point, item.At) - item.Range) * weights.PerUnitOutOfRange * item.Weight;
            foreach (Vector2 mate in mates)
                score -= Math.Max(0, minSpacing - Vector2.Distance(point, mate)) * weights.PerUnitCrowded;
            return score;
        }
    }
}

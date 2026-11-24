using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>
    /// Part of <see cref="PvpWeights"/>: how a group gives ground as one (docs/PVP-8V8-ROADMAP.md,
    /// M12). The owner, 2026-11-24, after an elite eight had lost to two bad groups where it
    /// stood: "individually, the bots are OK for now. I guess it's time to work on the group
    /// reaction, awareness and cohesion." That eight had counted sixteen enemies, had been under
    /// the order Extend for 96 of 210 seconds, and was never more than 2,200 units from where the
    /// fight began: Extend moved its healers and casters a little farther from a fighter, each
    /// for itself, and moved the group nowhere.
    /// </summary>
    public sealed class PvpKiteWeights
    {
        /// <summary>False: no group falls back as one by its own choice.</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>
        /// A group has more on it than it can stand against when the enemies that aim at it are
        /// this many to a hundred of its own side (its living members and those of its realm
        /// fighting beside it). An even fight has five or six of the enemy's eight aiming at the
        /// group, the rest looking at their own; two groups on one have ten to twelve.
        /// </summary>
        public int OutnumberedPercent { get; set; } = 125;
        /// <summary>And it is pressed while this many free enemies are within <see cref="PressedWithin"/> of one of its healers and casters.</summary>
        public int PressedBy { get; set; } = 2;
        public int PressedWithin { get; set; } = 1_500;
        /// <summary>
        /// How far the group's place goes back at a time, and the least time from one leg to the
        /// next: the walk, and then a stand to work on whoever followed. First run at 700 units
        /// every 5.5 seconds (an evening of 36 teams, 2026-11-24): thirty kites that carried a
        /// group 500 to 4,000 units, and nothing gained by them. A group that fell back took 221
        /// damage a second against 239 for one that stood with as many on it, and dealt 66 against
        /// 94; fourteen of the sixteen enemies stayed within 2,500 units of it all the way, and an
        /// enemy fighter on it was 1,466 units from its nearest healer, well inside the reach of
        /// a heal. Everybody runs at the same speed, so short legs shake nobody off.
        /// </summary>
        public int Leg { get; set; } = 1_800;
        public int LegEveryMilliseconds { get; set; } = 14_000;
        /// <summary>A leg starts from the group's place, or from where the group really is when it has dropped this far behind it.</summary>
        public int ArrivedWithin { get; set; } = 450;
        /// <summary>The kite stands this long after its last leg; then the group decides for itself again.</summary>
        public int StandsSeconds { get; set; } = 16;
        /// <summary>A healer or caster keeps the place it had in the group, but no farther than this from the group's place.</summary>
        public int PlaceRadius { get; set; } = 600;
        /// <summary>A member within this of its place has arrived.</summary>
        public int AtItsPlaceWithin { get; set; } = 150;
        /// <summary>The fighters stand this far on the enemy's side of the group's place, this far from each other.</summary>
        public int Screen { get; set; } = 300;
        public int ScreenSpacing { get; set; } = 250;
        /// <summary>A fighter goes for an enemy only within this of the middle of its healers and casters, wherever they have got to: for whoever followed.</summary>
        public int FightersReach { get; set; } = 1_100;
        /// <summary>And the group's kill target is called among the enemies within this of them.</summary>
        public int KillWithin { get; set; } = 1_500;
        /// <summary>With no open ground behind it the group stands for this long before it looks again.</summary>
        public int CorneredSeconds { get; set; } = 10;
    }

    /// <summary>
    /// The kite (docs/PVP-8V8-PLAYBOOK.md, section 3: "a retreat is one group moving one way, not
    /// eight paths"). The group has a place, which its caller moves back a leg at a time while
    /// the group has more on it than it can stand against; its healers and casters keep their
    /// places around it, its fighters stand between it and the enemy and take whoever follows.
    /// </summary>
    public static class PvpKite
    {
        /// <summary>True for a group with more enemies aiming at it than it can stand against (<see cref="PvpKiteWeights.OutnumberedPercent"/>).</summary>
        /// <param name="ownSide">Its living members and those of its realm fighting beside it.</param>
        /// <param name="onIt">The free enemies that aim at one of them.</param>
        public static bool Outnumbered(int ownSide, int onIt, PvpKiteWeights weights) =>
            weights.Enabled && ownSide > 0 && onIt * 100 >= ownSide * Math.Max(100, weights.OutnumberedPercent);

        /// <summary>
        /// Whether the group's place goes back another leg now. The first leg is taken once the
        /// group has been outnumbered and pressed for as long as its caller takes to call it
        /// (PvpHumanWeights.KiteCalledAfterMinMilliseconds); the next ones as long as it still is.
        /// </summary>
        public static bool TakesLeg(bool outnumbered, int pressedBy, long millisecondsOutnumberedAndPressed, int calledAfterMilliseconds,
            bool kiting, long millisecondsSinceItsLastLeg, PvpKiteWeights weights) =>
            weights.Enabled && outnumbered && pressedBy >= weights.PressedBy &&
            (kiting || millisecondsOutnumberedAndPressed >= calledAfterMilliseconds) &&
            millisecondsSinceItsLastLeg >= weights.LegEveryMilliseconds;

        private static Vector2 Turned(Vector2 way, double degrees)
        {
            double angle = degrees * Math.PI / 180;
            float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
            return new Vector2(way.X * cos - way.Y * sin, way.X * sin + way.Y * cos);
        }

        /// <summary>
        /// The ways the group may fall back, best first, each of length one: straight away from
        /// the enemy, then 30, 60 and 90 degrees to either side, the side nearer its home first.
        /// A group that is already falling back keeps near the way it was going, so that it does
        /// not turn at every leg. Empty when the enemy stands where the group does.
        /// </summary>
        /// <param name="away">From the middle of the enemy to the middle of the group.</param>
        /// <param name="before">The way of its last leg; zero for the first.</param>
        /// <param name="towardHome">From the group to where it came into the zone; zero when it has no such place.</param>
        public static List<Vector2> Ways(Vector2 away, Vector2 before, Vector2 towardHome)
        {
            if (away.LengthSquared() < 1)
                return [];
            Vector2 straight = Vector2.Normalize(away);
            if (before.LengthSquared() > 0.01f && Vector2.Dot(Vector2.Normalize(before), straight) > -0.1f)
                straight = Vector2.Normalize(Vector2.Normalize(before) + straight);
            var ways = new List<Vector2> { straight };
            Vector2 home = towardHome.LengthSquared() < 1 ? default : Vector2.Normalize(towardHome);
            foreach (int degrees in new[] { 30, 60, 90 })
            {
                Vector2 left = Turned(straight, degrees), right = Turned(straight, -degrees);
                if (Vector2.Dot(right, home) > Vector2.Dot(left, home))
                    (left, right) = (right, left);
                ways.Add(left);
                ways.Add(right);
            }
            return ways;
        }

        /// <summary>Where a healer or caster stands from the group's place: where it stood from the middle of them, no farther than the radius.</summary>
        public static Vector2 Offset(Vector2 member, Vector2 middle, int radius)
        {
            Vector2 from = member - middle;
            float length = from.Length();
            return length <= radius || length < 1 ? from : from / length * radius;
        }

        /// <summary>Where the fighter with this number of <paramref name="count"/> stands: in a row across the way, on the enemy's side of the place.</summary>
        public static Vector2 ScreenPlace(Vector2 place, Vector2 way, int index, int count, PvpKiteWeights weights)
        {
            var across = new Vector2(-way.Y, way.X);
            return place - way * weights.Screen + across * ((index - (count - 1) / 2f) * weights.ScreenSpacing);
        }

        /// <summary>A fighter of a group that falls back goes for this enemy: it is near enough to the group's healers and casters.</summary>
        public static bool FighterTakes(float enemyFromItsBackLine, PvpKiteWeights weights) => enemyFromItsBackLine <= weights.FightersReach;
    }
}

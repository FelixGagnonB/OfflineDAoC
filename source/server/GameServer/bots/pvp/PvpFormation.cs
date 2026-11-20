using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>
    /// The shapes a group takes off the fight (docs/PVP-8V8-ROADMAP.md, M8.4): a loose clump while
    /// it travels, and rows around its leader once the enemy is in sight. The owner, 2026-11-19,
    /// after playing: "we are way too tight on initial contact" (every companion stood within 160
    /// units of the player eight seconds before the first blow, all inside one area mez), and
    /// "it's funny to see the opposite team running as a two-lane rectangle".
    /// </summary>
    public static class PvpFormation
    {
        /// <summary>The enemy is seen from this far in the open basin, to form up and to turn towards it.</summary>
        public const int FormRange = 6_500;
        /// <summary>
        /// Neighbours in the travelling clump are about this far apart. Loose on purpose: at 130 the
        /// first recorded match had both groups still in a ball three seconds before the first blow
        /// (four or five members inside one area mez), with no time left to take their rows.
        /// </summary>
        public const int TravelSpacing = 300;
        /// <summary>The travelling clump aims this far ahead of where the group is.</summary>
        public const int TravelLead = 3_000;

        /// <summary>
        /// Where the members of a row stand across the line of approach, neighbours
        /// <paramref name="spacing"/> apart, in order from one end to the other. With the group's
        /// leader in the row the middle is the leader's, and the members stand to either side.
        /// </summary>
        public static float[] RowOffsets(int members, bool leaderInRow, float spacing)
        {
            if (members <= 0)
                return [];
            if (!leaderInRow)
                return Enumerable.Range(0, members).Select(slot => (slot - (members - 1) / 2f) * spacing).ToArray();
            var offsets = new List<float>();
            for (int step = 1; offsets.Count < members; step++)
            {
                offsets.Add(-step * spacing);
                if (offsets.Count < members)
                    offsets.Add(step * spacing);
            }
            return offsets.OrderBy(offset => offset).ToArray();
        }

        /// <summary>
        /// A member's place in the travelling clump, as (ahead, across) of its middle: the points
        /// of a sunflower head, which fill a disc evenly without rows or columns. The first
        /// places are the inner ones.
        /// </summary>
        public static Vector2 TravelOffset(int index, float spacing)
        {
            double radius = spacing * Math.Sqrt(index + 0.5), angle = index * 2.39996323;
            return new Vector2((float)(radius * Math.Cos(angle)), (float)(radius * Math.Sin(angle)));
        }

        /// <summary>
        /// Who drives a group on the road: the member with the speed song (Bard, Skald, Minstrel),
        /// as in the owner's groups ("the Sorc, Pac healer or Skald and Bard driving... I would
        /// stick to the Bard and it would roam around until contact", 2026-11-19); else the first
        /// that is no fighter; else whoever is there.
        /// </summary>
        public static GameBot DriverOf(IReadOnlyList<GameBot> members)
        {
            List<GameBot> alive = members.Where(member => member.IsAlive).ToList();
            return alive.FirstOrDefault(member => DrivesByClass(PvpMember.ClassOf(member)))
                   ?? alive.FirstOrDefault(member => !PvpMember.IsFighter(member))
                   ?? alive.FirstOrDefault();
        }

        public static bool DrivesByClass(eCharacterClass characterClass) =>
            characterClass is eCharacterClass.Bard or eCharacterClass.Skald or eCharacterClass.Minstrel;

        /// <summary>
        /// The points a roaming group goes between in a box: three by three, a margin in from its
        /// edges. The box is open ground; a point off the walkable ground is taken to the nearest
        /// place on it by the caller.
        /// </summary>
        public static (int X, int Y)[] RoamPoints(int minX, int minY, int maxX, int maxY, int margin)
        {
            var points = new List<(int X, int Y)>();
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    points.Add((minX + margin + (maxX - minX - 2 * margin) * column / 2, minY + margin + (maxY - minY - 2 * margin) * row / 2));
            }
            return points.ToArray();
        }

        /// <summary>The next point of a roam: any but the one the group is at, by a draw.</summary>
        public static int NextRoamPoint(int current, int count, int draw) =>
            count <= 1 ? 0 : (current + 1 + Math.Abs(draw % (count - 1))) % count;

        /// <summary>The middle of the travelling clump: ahead of the group on the way to its goal, and no farther than the goal.</summary>
        public static Vector2 TravelAnchor(Vector2 centre, Vector2 goal, float lead)
        {
            Vector2 way = goal - centre;
            float distance = way.Length();
            return distance < 1 ? goal : centre + way / distance * Math.Min(lead, distance);
        }
    }
}

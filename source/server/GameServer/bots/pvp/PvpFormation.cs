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

        /// <summary>The middle of the travelling clump: ahead of the group on the way to its goal, and no farther than the goal.</summary>
        public static Vector2 TravelAnchor(Vector2 centre, Vector2 goal, float lead)
        {
            Vector2 way = goal - centre;
            float distance = way.Length();
            return distance < 1 ? goal : centre + way / distance * Math.Min(lead, distance);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// The shapes a group takes off the fight (docs/PVP-8V8-ROADMAP.md, M8.4): on the road it
    /// follows its driver, close behind, and once the enemy is in sight it takes rows (around
    /// its leader, when a player leads). The owner, 2026-11-19, after playing: "we are way too
    /// tight on initial contact" (every companion stood within 160 units of the player eight
    /// seconds before the first blow, all inside one area mez), and "it's funny to see the
    /// opposite team running as a two-lane rectangle"; and on 2026-11-20, of a group set 300 apart
    /// around a point ahead of it: "Follow is not really working. Team is moving as a rectangle".
    /// </summary>
    public static class PvpFormation
    {
        /// <summary>The enemy is seen from this far in the open basin, to form up and to turn towards it.</summary>
        public const int FormRange = 6_500;

        /// <summary>
        /// How far behind the driver a member follows on the road: each a little farther than the
        /// one before, so that the group strings out behind it as players on /stick do.
        /// </summary>
        public static int FollowDistance(int index) => 90 + 35 * Math.Max(0, index);

        /// <summary>
        /// A follow ends by itself when its leader is farther away than the distance it is given
        /// (NpcMovementComponent.FollowTick), so a follower is given one it cannot fall behind by.
        /// It was given its own distance and 120: the driver set off first, was more than that
        /// ahead within a second, and the seven others stood where they were. The owner,
        /// 2026-11-20: "Warrior would be alone. We kill him, then 5-10 seconds later we see the 7
        /// other Mids" (in the recordings the lone one is the driver, the Skald, 8,500 to 10,300
        /// units ahead of the rest in two sessions of four). It is squared as an int there, so it
        /// has to stay under 46,340: at 60,000 the square overflowed, every follow ended at once,
        /// and with the driver waiting for its group the whole group stood at its start (the
        /// session of 11:36).
        /// </summary>
        public const int FollowGivesUpBeyond = 40_000;

        /// <summary>The driver stands and waits when a living member is this far behind it.</summary>
        public const int DriverWaitsBeyond = 700;
        /// <summary>And goes on when the farthest is this near again.</summary>
        public const int DriverGoesOnWithin = 400;

        /// <summary>Whether a driver waits for its group, given how far its farthest living member is.</summary>
        public static bool DriverWaits(int farthestBehind, bool waiting) =>
            farthestBehind > (waiting ? DriverGoesOnWithin : DriverWaitsBeyond);

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
        /// Who drives a group on the road: the member with the speed song (Bard, Skald, Minstrel),
        /// as in the owner's groups ("the Sorc, Pac healer or Skald and Bard driving... I would
        /// stick to the Bard and it would roam around until contact", 2026-11-19); else the first
        /// that is no fighter; else whoever is there.
        /// </summary>
        public static GameBot DriverOf(IReadOnlyList<GameBot> members)
        {
            // (In the owner's order since 2026-11-25: the Bard; the Sorcerer, then the Minstrel; the Pacification Healer, then the Skald. PvpDriver.Rank.)
            List<GameBot> alive = members.Where(member => member.IsAlive).ToList();
            return alive.Where(member => PvpDriver.Rank(PvpMember.ClassOf(member), member.PvpBuild?.Id) >= 0)
                       .OrderBy(member => PvpDriver.Rank(PvpMember.ClassOf(member), member.PvpBuild?.Id)).FirstOrDefault()
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
    }
}

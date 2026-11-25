using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DOL.GS
{
    /// <summary>
    /// An 8v8 bot on ground it cannot leave is put on the ground beside it (docs/PVP-8V8-ROADMAP.md,
    /// M13.21). The owner, 2026-11-25, in Thidranki: "I think I found a bug. My latest fight vs
    /// hibs. 2 druids are stuck in a tree and my alb team still consider being in fight".
    ///
    /// The recording and the navmesh of the place (18979, 30483): a patch of walkable ground some
    /// hundred units across with no way off it in any direction, round a tree. A Druid and a
    /// Warden of a bad Hibernian group stood on it for the whole fight, three and a half minutes;
    /// from outside the way to them ends 29 to 48 units short, and the navmesh's line of sight
    /// does not cross the gap, so the owner's group did not see them, had no target, and stood
    /// 1,100 to 1,600 units off while the two raised three of their dead beside them.
    ///
    /// Every few seconds a bot that has not moved since the last look is asked whether it can
    /// walk anywhere: some distance in any of eight directions. One that cannot is trapped. The
    /// way from open ground (its team's start) toward it ends beside the patch; when that end
    /// is near, the bot is put there. The move is of a few dozen units.
    /// </summary>
    public static class PvpTrapped
    {
        /// <summary>How far a bot is asked to walk in each direction, and how far it must get in one of them not to be trapped.</summary>
        public const int Reach = 600, Least = 150;
        /// <summary>The ground beside the patch must be within this of the bot for it to be put there; a bot is looked at every so often.</summary>
        public const int WayOutWithin = 400, EveryMilliseconds = 5_000;
        /// <summary>A bot that has moved this far since the last look is not asked.</summary>
        public const int HasMoved = 60;

        /// <summary>Trapped: it got nowhere in any direction it was asked to walk.</summary>
        public static bool IsTrapped(IReadOnlyList<int> walkedInEachDirection) =>
            walkedInEachDirection.Count > 0 && walkedInEachDirection.All(walked => walked < Least);

        /// <summary>Whether the ground beside the patch is near enough to put the bot there.</summary>
        public static bool IsNear(double fromTheBot) => fromTheBot <= WayOutWithin;

        /// <summary>Walks from one point toward another as far as the navmesh goes; where that ends.</summary>
        public static Vector3 WalkToward(Zone zone, Vector3 from, Vector3 to, int hops = 80)
        {
            var nav = PathfindingProvider.Instance;
            Vector3 at = from;
            Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[256];
            for (int hop = 0; hop < hops && Vector3.Distance(at, to) > 30; hop++)
            {
                PathfindingResult result = nav.GetPathStraight(zone, at, to, nav.DefaultFilters, nodes);
                if (result.NodeCount <= 0 || result.Status is PathfindingStatus.NoPathFound or PathfindingStatus.NavmeshUnavailable or PathfindingStatus.NotSet)
                    break;
                Vector3 before = at;
                at = nodes[Math.Min(result.NodeCount, nodes.Length) - 1].Position;
                if (Vector3.Distance(before, at) < 20)
                    break;
            }
            return at;
        }

        /// <summary>How far one gets from a point in each of eight directions.</summary>
        public static List<int> Walks(Zone zone, Vector3 at)
        {
            var walked = new List<int>(8);
            for (int direction = 0; direction < 8; direction++)
            {
                double angle = direction * Math.PI / 4;
                var to = new Vector3(at.X + (float)(Reach * Math.Cos(angle)), at.Y + (float)(Reach * Math.Sin(angle)), at.Z);
                Vector3 end = WalkToward(zone, at, to, 4);
                walked.Add((int)Vector2.Distance(new Vector2(at.X, at.Y), new Vector2(end.X, end.Y)));
            }
            return walked;
        }

        /// <summary>
        /// The ground beside a patch that cannot be left: where the way from open ground toward
        /// the point ends. Null when the point is not trapped, or that end is not near it.
        /// </summary>
        public static Vector3? WayOut(Zone zone, Vector3 at, Vector3 openGround)
        {
            if (zone == null || !IsTrapped(Walks(zone, at)))
                return null;
            Vector3 beside = WalkToward(zone, openGround, at);
            return IsNear(Vector3.Distance(beside, at)) && Vector3.Distance(beside, at) > 1 ? beside : null;
        }

        private sealed class Seen
        {
            public long At;
            public Vector3 Where;
        }

        private static readonly ConditionalWeakTable<GameBot, Seen> Looked = new();

        /// <summary>
        /// Looks at a bot when its time has come: one that has not moved and is trapped is put
        /// on the ground beside. True when it was moved. Never throws: a fault of the navmesh
        /// must not stop an evening.
        /// </summary>
        public static bool Free(GameBot bot, (int X, int Y) openGround, long now)
        {
            try
            {
                if (bot is not { IsAlive: true } || bot.ObjectState != GameObject.eObjectState.Active || bot.CurrentZone is not { } zone)
                    return false;
                Seen seen = Looked.GetOrCreateValue(bot);
                var here = new Vector3(bot.X, bot.Y, bot.Z);
                if (now - seen.At < EveryMilliseconds)
                    return false;
                bool stood = seen.At != 0 && Vector3.Distance(seen.Where, here) < HasMoved;
                (seen.At, seen.Where) = (now, here);
                if (!stood)
                    return false;
                var nav = PathfindingProvider.Instance;
                if (nav.GetFloorBeneath(zone, new Vector3(openGround.X, openGround.Y, 20_000), 20_000, nav.DefaultFilters) is not { } open || open.Z >= 19_999)
                    return false;
                if (WayOut(zone, here, open) is not { } beside)
                    return false;
                return bot.MoveTo(bot.CurrentRegionID, (int)beside.X, (int)beside.Y, (int)beside.Z, bot.Heading);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

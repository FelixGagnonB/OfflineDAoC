using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>
    /// Sight over the ground (docs/PVP-8V8-ROADMAP.md, M4.2 and M8.4): a rise between two
    /// characters hides one from the other. The navmesh's own line of sight only knows where
    /// nobody can walk, so a hill anybody can walk over hid nothing, and two groups always saw
    /// each other at the same distance. The owner, 2026-11-19: "LoS for initial contact would be
    /// really cool". Used for spotting only: what a group sees before and during a fight. A
    /// cast is not checked against it.
    /// </summary>
    public static class PvpSight
    {
        /// <summary>How high above the ground a character sees from, and is seen at.</summary>
        public const int EyeHeight = 60;
        /// <summary>The ground is looked at every so many units along the line.</summary>
        public const int Step = 250;

        // The height of the ground, by zone and by cell of 128 units: the ground does not change.
        private static readonly ConcurrentDictionary<(ushort Zone, int X, int Y), float> Ground = new();

        /// <summary>
        /// True when the ground somewhere between the two rises above the straight line from one
        /// character's eyes to the other's. <paramref name="ground"/> holds the height of the
        /// ground at evenly spaced points strictly between them, in order; a point where the
        /// height is not known (NaN) hides nothing.
        /// </summary>
        public static bool Blocked(IReadOnlyList<float> ground, float fromZ, float toZ, int eyeHeight = EyeHeight)
        {
            float from = fromZ + eyeHeight, to = toZ + eyeHeight;
            for (int i = 0; i < ground.Count; i++)
            {
                if (float.IsNaN(ground[i]))
                    continue;
                float line = from + (to - from) * (i + 1) / (ground.Count + 1);
                if (ground[i] > line)
                    return true;
            }
            return false;
        }

        /// <summary>How many points of the ground to look at between two characters this far apart.</summary>
        public static int Samples(float distance, int step = Step) => Math.Max(0, (int)(distance / step) - 1);

        /// <summary>True when no rise of the ground stands between the two points.</summary>
        public static bool Clear(Zone zone, Vector3 from, Vector3 to)
        {
            if (zone == null)
                return true;
            float distance = Vector2.Distance(new Vector2(from.X, from.Y), new Vector2(to.X, to.Y));
            int samples = Samples(distance);
            if (samples == 0)
                return true;
            var ground = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float along = (i + 1) / (float)(samples + 1);
                ground[i] = HeightAt(zone, from.X + (to.X - from.X) * along, from.Y + (to.Y - from.Y) * along);
            }
            return !Blocked(ground, from.Z, to.Z);
        }

        /// <summary>
        /// How far a character sees an enemy that stands at this angle from the way it faces (0 is
        /// straight ahead): its whole range inside the arc in front of it, a share of it outside.
        /// A character that stands still looks about and sees its whole range all round. The
        /// owner, 2026-11-19, on how a fight starts: "it really depends on if we sneaked in, heads
        /// on, angle on contact... real humans would be panning to avoid being sneaked on, so in
        /// that case angle matters."
        /// </summary>
        public static int RangeAt(float angle, bool moving, int range, int frontArc, int behindPercent) =>
            !moving || frontArc >= 360 || behindPercent >= 100 || angle < frontArc / 2f || angle >= 360 - frontArc / 2f
                ? range : range * Math.Max(0, behindPercent) / 100;

        /// <summary>
        /// True when a bot sees an enemy: near enough for the way it faces, and no rise of the
        /// ground between them (each as the weights have it).
        /// </summary>
        public static bool Sees(GameLiving from, GameObject to, int range, PvpWeights weights)
        {
            if (from == null || to == null || from.CurrentRegionID != to.CurrentRegionID)
                return false;
            int reach = RangeAt(from.GetAngle(to), from.IsMoving, range, weights.SightFrontArc, weights.SightBehindPercent);
            return from.IsWithinRadius(to, reach) && (!weights.SightOverGround || Clear(from, to));
        }

        /// <summary>True when one living thing can see the other over the ground.</summary>
        public static bool Clear(GameObject from, GameObject to) =>
            from == null || to == null || from.CurrentRegionID != to.CurrentRegionID ||
            Clear(from.CurrentZone, new Vector3(from.X, from.Y, from.Z), new Vector3(to.X, to.Y, to.Z));

        /// <summary>The height of the ground at a point, from the navmesh; NaN where there is none to stand on.</summary>
        public static float HeightAt(Zone zone, float x, float y)
        {
            var key = (zone.ID, (int)x >> 7, (int)y >> 7);
            if (Ground.TryGetValue(key, out float known))
                return known;
            var nav = PathfindingProvider.Instance;
            // The middle of the cell, so that the same cell always gives the same height.
            float cx = (key.Item2 << 7) + 64, cy = (key.Item3 << 7) + 64;
            float height = nav.GetFloorBeneath(zone, new Vector3(cx, cy, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999
                ? floor.Z : float.NaN;
            Ground[key] = height;
            return height;
        }
    }
}

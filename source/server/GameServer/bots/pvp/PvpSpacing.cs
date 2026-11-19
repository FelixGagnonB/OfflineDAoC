using System;
using System.Collections.Generic;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>Part of <see cref="PvpWeights"/>: how far apart a group's healers and casters keep.</summary>
    public sealed class PvpSpreadWeights
    {
        /// <summary>Healers and casters keep at least this far from one another; an area mez reaches 350. 0 turns spreading off.</summary>
        public int MinSpacing { get; set; } = 400;
        /// <summary>
        /// The group's healers keep at least this far from one another, so that one enemy running
        /// in does not have both in front of it. Heals reach 2,000: each still reaches the other
        /// and the group. The owner, watching Hibernia against Midgard on 2026-11-18: "Both Druids
        /// are way too close to each other. The Skald ran in, instant mezzed one and the tanks
        /// trained the other." Until then two healers kept 400 apart like any two casters.
        /// 0 = as before.
        /// </summary>
        public int HealerSpacing { get; set; } = 1_000;
        /// <summary>How far one step apart is.</summary>
        public int Step { get; set; } = 200;
        /// <summary>A caster walking into range stops this far inside it.</summary>
        public int RangeMargin { get; set; } = 50;
    }

    /// <summary>
    /// Keeping apart (docs/PVP-8V8-ROADMAP.md, M7.4). The owner, 2026-11-17: a group that sees
    /// another spreads out at once, and it is rare for a mezzer to catch seven of eight on first
    /// contact. The recorded bots did the opposite: the formation ended when they got their
    /// targets, they all walked at the same few enemies, and gaps of 450 units were about 200 by
    /// the time they were in casting range. One area mez caught all eight.
    /// </summary>
    public static class PvpSpacing
    {
        /// <summary>
        /// Where to stand to cast at <paramref name="aim"/> from <paramref name="range"/>: the nearest
        /// such place on the caster's own side, moved along the circle of that range away from
        /// groupmates standing closer to it than <paramref name="minSpacing"/>.
        /// </summary>
        public static Vector2 ApproachPoint(Vector2 self, Vector2 aim, int range, IReadOnlyList<Vector2> mates, int minSpacing)
        {
            Vector2 bearing = self - aim;
            bearing = bearing.LengthSquared() < 1 ? Vector2.UnitX : Vector2.Normalize(bearing);
            Vector2 point = aim + bearing * range;
            if (minSpacing <= 0)
                return point;
            Vector2 push = Vector2.Zero;
            foreach (Vector2 mate in mates)
            {
                Vector2 away = point - mate;
                float distance = away.Length();
                if (distance >= minSpacing)
                    continue;
                // Standing on the very spot: go sideways.
                Vector2 direction = distance < 1 ? new Vector2(-bearing.Y, bearing.X) : away / distance;
                push += direction * (minSpacing - distance);
            }
            if (push == Vector2.Zero)
                return point;
            // Back onto the circle, so the place is still in range.
            Vector2 moved = point + push - aim;
            return moved.LengthSquared() < 1 ? point : aim + Vector2.Normalize(moved) * range;
        }

        /// <summary>
        /// A step away from the groupmates standing closer than <paramref name="minSpacing"/>, or
        /// null when nobody is. The step never leads toward <paramref name="enemyCentre"/>: of the
        /// way out, the part that points at the enemy is dropped.
        /// </summary>
        public static Vector2? StepApart(Vector2 self, IReadOnlyList<Vector2> mates, Vector2 enemyCentre, int minSpacing, int step)
        {
            if (minSpacing <= 0)
                return null;
            Vector2 push = Vector2.Zero;
            bool crowded = false;
            foreach (Vector2 mate in mates)
            {
                Vector2 away = self - mate;
                float distance = away.Length();
                if (distance >= minSpacing)
                    continue;
                crowded = true;
                push += distance < 1 ? Vector2.Zero : away / distance * (minSpacing - distance);
            }
            if (!crowded)
                return null;
            Vector2 toEnemy = enemyCentre - self;
            toEnemy = toEnemy.LengthSquared() < 1 ? Vector2.UnitX : Vector2.Normalize(toEnemy);
            float forward = Vector2.Dot(push, toEnemy);
            if (forward > 0)
                push -= toEnemy * forward;
            // Nothing left (the mate is straight behind, or on the same spot): go sideways.
            if (push.LengthSquared() < 1)
                push = new Vector2(-toEnemy.Y, toEnemy.X);
            return self + Vector2.Normalize(push) * step;
        }
    }
}

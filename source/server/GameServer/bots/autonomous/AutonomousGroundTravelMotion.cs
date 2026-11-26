using System;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>Ground travel kinematics only. Does not choose or alter a route.</summary>
    public static class AutonomousGroundTravelMotion
    {
        // ObjectUpdate encodes vertical velocity in seven bits, in units of four.
        // Never normalize a tiny XY / large Z segment into an overflowing packet.
        public const float MaximumVerticalSpeed = 508;

        public readonly record struct Motion(Vector3 Velocity, double HorizontalSpeed, int ArrivalMilliseconds);

        public static bool Applies(GameNPC owner, bool fixedSpeed, GameObject followTarget) =>
            owner is GameBot { IsOnStableMasterRoute: false } bot &&
            AppliesTo(bot.IsAutonomousWorldBot && !bot.IsTemporaryGroupHelper && !bot.IsPlayerLedGroup, bot.PvpBuild != null, bot.IsAttacking,
                followTarget != null) &&
            !fixedSpeed &&
            (owner.Flags & (GameNPC.eFlags.FLYING | GameNPC.eFlags.SWIMMING)) == 0;

        /// <summary>
        /// Which bots keep their speed over the ground on a slope, as a player's character does: a gamebot that travels the
        /// world on its own and neither attacks nor follows, and every 8v8 bot whatever it does (a group's members follow
        /// their driver: with the driver alone as fast as a player on a hill, they would drop behind it). Any other walker keeps its speed along the slope, so it
        /// is slower over the ground the steeper the hill. For the 8v8 bots since 2026-11-25: the owner, on Agramon behind
        /// the Bard that drove its group, "stutter at various places on the island", and alone "It was smoother". The Bard
        /// ran 385 units a second on the flat and 250 to 300 on the hills (385 along the slope each time); its player, on
        /// /stick and as fast as ever over the ground, ran up to it and was held back, hill after hill.
        /// </summary>
        public static bool AppliesTo(bool travelsTheWorldOnItsOwn, bool isAn8v8Bot, bool attacking, bool follows) =>
            isAn8v8Bot || travelsTheWorldOnItsOwn && !attacking && !follows;

        public static bool TryCalculate(Vector3 direction, short speed, out Motion motion)
        {
            motion = default;
            if (speed <= 0 || !float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z))
                return false;
            double horizontal = Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y);
            double vertical = Math.Abs((double)direction.Z);
            if (horizontal == 0 && vertical == 0) return false;

            // Ordinary ground movement keeps its requested XY speed, including
            // short steps/height noise on visually flat ground. Pure vertical
            // moves keep their old speed, rather than dividing by zero.
            double distance = horizontal > 0.01 ? horizontal : Math.Sqrt(horizontal * horizontal + vertical * vertical);
            distance = Math.Max(distance, vertical * speed / MaximumVerticalSpeed);
            double scale = speed / distance;
            Vector3 velocity = new((float)(direction.X * scale), (float)(direction.Y * scale),
                Math.Clamp((float)(direction.Z * scale), -MaximumVerticalSpeed, MaximumVerticalSpeed));
            double wireSpeed = horizontal * scale;
            // Avoid 190.99999 -> 190 truncation in the NPC packet on flat ground.
            if (Math.Abs(wireSpeed - speed) < 0.001) wireSpeed = speed;
            int arrival = (int)Math.Clamp(Math.Ceiling(distance * 1000 / speed), 1, int.MaxValue);
            motion = new(velocity, wireSpeed, arrival);
            return true;
        }
    }
}

using System;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>
    /// An 8v8 bot in water swims, as a player's character does.
    /// <para>The owner, 2026-11-25, in Thidranki: "Bots are running on water like on ground but not me", and a minute
    /// later, "they just flew and abandonned me". The navigation mesh has the water's surface as ground a bot may cross,
    /// and a bot crossed it at its full speed: its group at 382 to 386 units a second over the river, on their Bard's
    /// song, the owner's Hero in the same water at 190 with the song on it and 87 without (half of what it runs on land,
    /// 382 and 191). In 45 seconds the group was 9,700 units ahead.</para>
    /// <para>A bot on the water's surface now moves at half its speed (<see cref="SwimShare"/>), whatever song or
    /// sprint it has. Whether it is in water is asked of the navigation mesh, which marks water
    /// (<see cref="EDtPolyFlags.Swim"/>), once a second.</para>
    /// </summary>
    public static class PvpWater
    {
        /// <summary>The share of its speed on land that a swimmer has. Measured from the owner's character: 190 of 382, 87 of 191.</summary>
        public const double SwimShare = 0.5;

        /// <summary>How far above and below a bot's feet the surface it stands on is looked for.</summary>
        public const int Above = 40, Below = 120;

        /// <summary>The floor it stands on is the water's when both are found at the same height.</summary>
        public const int SameHeightWithin = 8;

        private static readonly EDtPolyFlags[] Water = [EDtPolyFlags.Swim, 0];

        /// <summary>
        /// Whether somebody stands on water: the nearest floor under its feet is one the mesh marks as water. (On a bridge
        /// the nearest floor is the deck, and the water is farther down.)
        /// </summary>
        public static bool IsOn(float? floorUnderIt, float? waterUnderIt) =>
            floorUnderIt is { } floor && waterUnderIt is { } water && Math.Abs(floor - water) <= SameHeightWithin;

        /// <summary>The height of the nearest floor under a place, of any kind and of water, or null where there is none.</summary>
        public static (float? Floor, float? Water) Under(Zone zone, Vector3 at)
        {
            var nav = PathfindingProvider.Instance;
            if (zone == null || nav == null || !nav.IsAvailable || !nav.HasNavmesh(zone))
                return (null, null);
            var from = new Vector3(at.X, at.Y, at.Z + Above);
            Vector3? floor = nav.GetFloorBeneath(zone, from, Above + Below, nav.DefaultFilters);
            Vector3? water = nav.GetFloorBeneath(zone, from, Above + Below, Water);
            return (floor?.Z, water?.Z);
        }

        /// <summary>Once a second for a bot: whether it is in water. True when that has changed.</summary>
        public static bool Note(GameBot bot)
        {
            if (bot == null || !bot.IsAlive || bot.ObjectState != GameObject.eObjectState.Active)
                return false;
            (float? floor, float? water) = Under(bot.CurrentZone, new Vector3(bot.X, bot.Y, bot.Z));
            bool inWater = IsOn(floor, water);
            if (inWater == bot.PvpInWater)
                return false;
            bot.PvpInWater = inWater;
            bot.OnMaxSpeedChange();   // on its way already: at its new speed from here
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.Database;

namespace DOL.GS
{
    /// <summary>
    /// An alchemist beside the buffbot of the player's group, with the potions for power and
    /// endurance (docs/PVP-8V8-ROADMAP.md, M13.54). The owner, 2026-11-27, on its Cleric ("i'm
    /// always oop"): "Would it be hard to create an npc, that spawn beside buffbot when we /8v8 go
    /// &lt;area&gt; or /release which would sell elixir of Instant power, draught of replenishment?
    /// Does alchemist also have the equivalent for endurance ?" The game has the potions and the
    /// recipes of Alchemy for them (408 to 1,000 of the skill), and potion merchants only in
    /// Cathal Valley and the tutorial, with the lesser ones. A character from /8v8 ready has no
    /// skill and may have no coin, so this one gives them for nothing.
    ///
    /// It comes and goes with the buffbot (PvpArena.SpawnCompanions, PvpArena.Remove): wherever
    /// the player's group is put in the world, at its start, after a release, in another zone.
    /// Nothing of it is saved. Only the player's group has one: a group of bots does not drink.
    /// </summary>
    public sealed class PvpPotionSeller : GameMerchant
    {
        /// <summary>The name of its list. No list of the database has it: the list is made here.</summary>
        public const string ListName = "Pvp8v8Potions";

        /// <summary>How far from the buffbot it stands.</summary>
        public const int BesideTheBuffbot = 120;

        /// <summary>
        /// It stands on ground no more than this much higher or lower than the buffbot's. On a slope of Agramon the
        /// ground 120 units to the east was 259 units higher and 196 lower (first test, 2026-11-27): a ledge the
        /// player may not reach.
        /// </summary>
        public const int LevelWithin = 60;

        /// <summary>How near the buffbot it stands where no ground around it is level: on its spot, all but.</summary>
        public const int OnItsSpot = 30;

        /// <summary>The places around the buffbot that are tried: eight, the first to the east.</summary>
        public const int PlacesAround = 8;

        /// <summary>
        /// Which of the places around the buffbot it takes, given the height of the ground at each (null: none
        /// there): the one most level with the buffbot, or -1 when none is level enough.
        /// </summary>
        public static int LevelPlace(IReadOnlyList<float?> ground, float buffbotZ)
        {
            int best = -1;
            float least = LevelWithin;
            for (int place = 0; place < ground.Count; place++)
            {
                if (ground[place] is { } z && Math.Abs(z - buffbotZ) <= least && (best < 0 || Math.Abs(z - buffbotZ) < least))
                    (best, least) = (place, Math.Abs(z - buffbotZ));
            }
            return best;
        }

        // Albion's; Midgard's and Hibernia's are the same with a 2 and a 3 after the name.
        //   Elixir of Instant Power          99 power at once, 10 charges, every 180 seconds
        //   Strong Draught of Replenishment  power regeneration 4 for 20 minutes, 9 charges
        //   Elixir of Instant Endurance      40 endurance at once, 10 charges, every 180 seconds
        //   Strong Draught of Invigoration   endurance regeneration 4 for 20 minutes, 9 charges
        private static readonly string[] Potions =
        [
            "Elixir_of_Instant_Power", "Strong_Draught_of_Replenishment", "Elixir_of_Instant_Endurance", "Strong_Draught_of_Invigoration",
        ];

        /// <summary>The potions it has for a player of a realm, in the order of its window.</summary>
        public static IReadOnlyList<string> ItemsFor(eRealm realm) => Potions.Select(potion => realm switch
        {
            eRealm.Midgard => potion + "2",
            eRealm.Hibernia => potion + "3",
            _ => potion,
        }).ToList();

        /// <summary>How it looks: as the potion merchant of that realm in Cathal Valley (Sabine, Salazo, Yoria).</summary>
        public static (ushort Model, string Equipment) LooksFor(eRealm realm) => realm switch
        {
            eRealm.Midgard => (144, "MidLeatherBlueCloak"),
            eRealm.Hibernia => (315, "Yori"),
            _ => (7, "AlbInstrumentsMerchant"),
        };

        private static readonly Dictionary<GameBot, PvpPotionSeller> Sellers = [];

        /// <summary>Puts one beside a buffbot. Null when it could not be: the group plays without it.</summary>
        public static PvpPotionSeller SpawnBeside(GameBot buffbot, eRealm realm)
        {
            if (buffbot?.CurrentRegion == null)
                return null;
            // One whose buffbot left the world by another way than PvpArena.Remove does not stay behind.
            List<GameBot> left;
            lock (Sellers)
                left = Sellers.Keys.Where(other => other.ObjectState != eObjectState.Active).ToList();
            foreach (GameBot gone in left)
                RemoveWith(gone);
            var list = new MerchantTradeItems(ListName);
            int slot = 0;
            foreach (string id in ItemsFor(realm))
            {
                // A copy that costs nothing; the game's own is left as it is.
                if (GameServer.Database.FindObjectByKey<DbItemTemplate>(id) is { } potion)
                    list.AddTradeItem(0, (eMerchantWindowSlot)slot++, new DbItemTemplate(potion) { Price = 0 });
            }
            if (slot == 0)
                return null;

            (ushort model, string equipment) = LooksFor(realm);
            // On level ground beside the buffbot; where there is none, on the buffbot's own spot.
            var nav = PathfindingProvider.Instance;
            var places = Enumerable.Range(0, PlacesAround).Select(place => 2 * Math.PI * place / PlacesAround)
                .Select(angle => (X: buffbot.X + (int)Math.Round(BesideTheBuffbot * Math.Cos(angle)), Y: buffbot.Y + (int)Math.Round(BesideTheBuffbot * Math.Sin(angle)))).ToList();
            List<float?> ground = places.Select(place => buffbot.CurrentRegion.GetZone(place.X, place.Y) is { } zone
                ? nav.GetFloorBeneath(zone, new Vector3(place.X, place.Y, buffbot.Z + 200), 400, nav.DefaultFilters)?.Z : null).ToList();
            int taken = LevelPlace(ground, buffbot.Z);
            (int x, int y, float z) = taken >= 0 ? (places[taken].X, places[taken].Y, ground[taken].Value) : (buffbot.X + OnItsSpot, buffbot.Y, buffbot.Z);
            var seller = new PvpPotionSeller
            {
                Name = "Alchemist",
                GuildName = "Potions",
                Realm = realm,
                Model = model,
                Size = 50,
                Level = 50,
                X = x,
                Y = y,
                Z = (int)Math.Round(z),
                Heading = buffbot.Heading,
                CurrentRegionID = buffbot.CurrentRegionID,
                LoadedFromScript = true,
                TradeItems = list,
            };
            seller.Flags |= eFlags.PEACE;
            seller.LoadEquipmentTemplateFromDatabase(equipment);
            if (!seller.AddToWorld())
            {
                seller.Delete();
                return null;
            }
            lock (Sellers)
                Sellers[buffbot] = seller;
            return seller;
        }

        /// <summary>Takes every one out of the world (PvpArena.Clear).</summary>
        public static void RemoveAll()
        {
            List<GameBot> all;
            lock (Sellers)
                all = Sellers.Keys.ToList();
            foreach (GameBot buffbot in all)
                RemoveWith(buffbot);
        }

        /// <summary>Takes the one of a buffbot out of the world with it.</summary>
        public static void RemoveWith(GameBot buffbot)
        {
            PvpPotionSeller seller;
            lock (Sellers)
            {
                if (!Sellers.Remove(buffbot, out seller))
                    return;
            }
            if (seller.ObjectState != eObjectState.Deleted)
            {
                seller.RemoveFromWorld();
                seller.Delete();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOL.AI.Brain;
using DOL.Events;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>Where a realm's teams start in a roam, and the points they walk between.</summary>
    /// <param name="SecondRoute">The way of the realm's second team, when it is not the first team's.</param>
    public sealed record PvpRoamStart(eRealm Realm, (int X, int Y) Point, (int X, int Y)[] Route, (int X, int Y)[] SecondRoute = null)
    {
        public (int X, int Y)[] RouteOf(int teamNumber) => teamNumber >= 2 && SecondRoute != null ? SecondRoute : Route;
    }

    /// <summary>A zone a roam is held in: its region and where each realm's teams start and walk.</summary>
    /// <param name="Ways">Which of its points a team can walk between directly; a team of a prime-time evening goes from point to point along them as it pleases.</param>
    /// <param name="Quiet">The points away from the road.</param>
    /// <param name="Places">What its points are called in realm chat ("south road").</param>
    public sealed record PvpRoamMap(string Id, string Name, ushort Region, IReadOnlyList<PvpRoamStart> Starts,
        IReadOnlyList<((int X, int Y) A, (int X, int Y) B)> Ways = null, IReadOnlyList<(int X, int Y)> Quiet = null,
        IReadOnlyList<((int X, int Y) Point, string Name)> Places = null)
    {
        public PvpRoamStart StartOf(eRealm realm) => Starts.First(start => start.Realm == realm);

        /// <summary>The zone as it is named in a line of chat: "Emain", "Odin's", "Hadrian's".</summary>
        public string Tag => Name.Split(' ')[0];

        /// <summary>What the nearest named point to a place is called; "near the X keep" within 6,000 of a realm's start; null on a map without names.</summary>
        public string PlaceOf(double x, double y)
        {
            double From((int X, int Y) point) => Math.Sqrt(Math.Pow(point.X - x, 2) + Math.Pow(point.Y - y, 2));
            if (Starts.Where(start => From(start.Point) < 6_000).OrderBy(start => From(start.Point)).FirstOrDefault() is { } keep)
                return $"the {PvpRoam.Short(keep.Realm, false)} keep";
            return Places is { Count: > 0 } ? Places.OrderBy(place => From(place.Point)).First().Name : null;
        }

        /// <summary>The points a team can walk to from one.</summary>
        public List<(int X, int Y)> From((int X, int Y) point) =>
            (Ways ?? []).Where(way => way.A == point || way.B == point).Select(way => way.A == point ? way.B : way.A).ToList();
    }

    /// <summary>One team of a roam.</summary>
    public sealed class PvpRoamTeam
    {
        public string Name;
        public PvpGroupTemplate Template;
        public eRealm Realm;
        public PvpRoamStart Start;
        /// <summary>The zone it is in. On a prime-time evening the teams may be spread over several (M11.3).</summary>
        public PvpRoamMap Map;
        public string Roster;
        /// <summary>On a prime-time evening (M11): the guild it is, and what its kind is like. Null for the realm's main groups of the roam as it was.</summary>
        public PvpGuildTeam Guild;
        public PvpTeamProfile Profile;
        /// <summary>The team it walks with: the head of its pack, or the friendly group it stays near. Null: it walks alone.</summary>
        public PvpRoamTeam Company;
        /// <summary>Where its eight come into the world, near its start; and the first point it walks to, when it goes from point to point as it pleases.</summary>
        public (int X, int Y) SpawnAt;
        public (int X, int Y)? Entry, CameFrom;
        /// <summary>When it reached the point it was walking to (0: on its way).</summary>
        public long ArrivedAt;
        /// <summary>On a prime-time evening: when it comes out (its state is "Away" until then), and when its evening is over (0: it stays).</summary>
        public long ComesOutAt, LogsOffAt;
        /// <summary>Which of its realm's teams it is at the start (its place there, and which first point it takes).</summary>
        public int Index;
        /// <summary>This evening: how often the player's group wiped it, how often it was in the fight that wiped the player's, and whether the player's group came into a fight of its own.</summary>
        public int WipedByPlayer, WipedPlayer;
        public bool AddedOnByPlayer;
        public readonly PvpTeam Team = new();
        /// <summary>Everything it put in the world: its eight and its buffbot.</summary>
        public readonly List<GameBot> All = [];
        public string State = "Buffing";
        public long StateSince, LeavesAt, ContactTick, LastEnemyNearTick;
        /// <summary>No enemy team near: the recorder writes it every fifth second only.</summary>
        public bool Quiet = true;
        /// <summary>Standing by a fight it has left to its realm-mate.</summary>
        public bool Waiting;
        /// <summary>It has had its rest since its last fight.</summary>
        public bool Rested;
        /// <summary>The place off its road it is walking to, to rest there; null when it rests where it is or not at all.</summary>
        public System.Numerics.Vector3? Hide;
        public long HideSince;
        /// <summary>Out of a fight with dead members since this tick (0: none); and gone back to its start to form up again.</summary>
        public long DeadSince;
        public bool Reforming;
        /// <summary>The enemy teams it is in a fight with now.</summary>
        public readonly HashSet<PvpRoamTeam> Fighting = [];
        /// <summary>Its draw for each enemy team its realm's other team is fighting: true, it joins.</summary>
        public readonly Dictionary<PvpRoamTeam, bool> Draws = [];
        public readonly HashSet<GameLiving> Paid = [];
        public int Wipes, Kills, Deaths, Fights, Joined, LeftAlone;
        public long Earned;

        /// <summary>The player at its head; null for a team of bots.</summary>
        public GamePlayer Leader => Team.Leader;
        public int Alive => Team.Members.Count(member => member.IsAlive);
        public (double X, double Y) Centre()
        {
            List<GameLiving> living = Team.Members.Where(member => member.IsAlive).ToList();
            if (living.Count == 0)
                living = Team.Members.ToList();
            return living.Count == 0 ? (Start.Point.X, Start.Point.Y) : (living.Average(member => member.X), living.Average(member => member.Y));
        }
    }

    /// <summary>
    /// The roam (docs/PVP-8V8-ROADMAP.md, M9.7): several teams in one zone, in a session that
    /// goes on. The owner, 2026-11-20: "There could be 2 group for each realm. PvP ruleset is not
    /// enable on this so they would be fighting any of the opposite realm team"; "We revive at
    /// our start point"; Emain Macha, "Albion would spawn (and release) at their principal keep.
    /// Same for midgard. Hibernia ... on the hill near the tower in front of Albion Main Gate."
    /// Bot teams alone (from the control channel, to be read from its status), or with a player
    /// at the head of the first team of its realm (<c>/8v8 roam</c>): seven companions, the
    /// rules of a real session (ranks as earned, realm points kept, no timer reset), and after a
    /// wipe the player stands up at the start with the companions, without sickness.
    /// </summary>
    public static class PvpRoam
    {
        private static readonly Logger Log = LoggerManager.Create(typeof(PvpRoam));

        public const ushort EmainRegion = 200;
        // The two ends of the open middle of Emain Macha, west of both portal keeps, 38,000 units
        // apart on foot (UT_PvpRoamStartRoutes): from the Midgard keep to the north end is 26,600,
        // from the Albion keep to the south end 35,500.
        public static readonly (int X, int Y) North = (449336, 299008), South = (445970, 334336);

        /// <summary>
        /// "The hill near the tower in front of Albion Main Gate": the owner stood there on
        /// 2026-11-20 and sent its place, 22984,51041 in the zone. It is 6,600 units on foot from
        /// the south end of the middle, 37,100 from the Albion keep and 42,700 from the north end.
        /// </summary>
        public static readonly (int X, int Y) HiberniaHill = (417792 + 22984, 286720 + 51041);

        /// <summary>
        /// Where Hibernia starts since 2026-11-21: in front of Dun Crauchon, its keep in the zone
        /// (the keep's middle is at 425877,318720), 2,400 units out on the way to the middle. The
        /// owner, after an afternoon of roams from the hill: "hibernia could start at Dun Crauchon
        /// to balance the starting location". From the hill the south end of the middle was 6,600
        /// units away, against 33,700 for Albion and 24,800 from Midgard's start to the north end,
        /// so Hibernia was at every first fight and was fought on its own doorstep. From here it
        /// is 26,400 on foot to the south end and 33,000 to the north end (the installed navmesh),
        /// and both are reached directly: its first team goes south first, its second north.
        /// </summary>
        public static readonly (int X, int Y) DunCrauchon = (427772, 320193);

        /// <summary>
        /// Four more points of Emain Macha to walk between (2026-11-22): the middle of the road
        /// between its two ends, and three in the open ground west of it, towards Dun Crauchon.
        /// With the two ends alone every team walked the one road, and in two roams of bots that
        /// day all six stood at its north end from the third minute on: one fight for fifteen
        /// minutes, and nobody ever out of reach of an enemy long enough to rest. Every leg is on
        /// the installed navmesh within 10% of the straight line: the road 20,600 and 18,600 from
        /// its middle to its ends, the west side 11,500 and 12,500 a leg, 13,700 from the north
        /// end and 10,900 from the south end to it, 10,400 across from its middle to the road's.
        /// East of the road the ground is mountain (a point 10,000 east is 47,000 on foot).
        /// A realm's first team goes round the six one way and its second the other.
        /// </summary>
        public static readonly (int X, int Y) Middle = (447500, 316500), WestNorth = (438000, 305000), WestMiddle = (438000, 316000), WestSouth = (438000, 328000);

        /// <summary>
        /// Albion and Midgard at their portal keeps, Hibernia at Dun Crauchon. "At" a keep is in front
        /// of its outer gate, some 1,800 units out on the way to the middle: the way from the
        /// middle of either keep goes through two keep doors (800 and 1,100 units from it), and in
        /// the first roam of 2026-11-20 the four teams that started inside stood there for eight
        /// minutes while Hibernia's two walked the middle. A bot does not open a keep door.
        /// </summary>
        public static readonly IReadOnlyList<PvpRoamStart> EmainStarts =
        [
            new(eRealm.Albion, (473395, 342169), [South, Middle, North, WestNorth, WestMiddle, WestSouth], [South, WestSouth, WestMiddle, WestNorth, North, Middle]),
            new(eRealm.Midgard, (472166, 297011), [North, Middle, South, WestSouth, WestMiddle, WestNorth], [North, WestNorth, WestMiddle, WestSouth, South, Middle]),
            new(eRealm.Hibernia, DunCrauchon, [WestSouth, South, Middle, North, WestNorth, WestMiddle], [WestNorth, North, Middle, South, WestSouth, WestMiddle]),
        ];

        // Thidranki (region 252, M9.7 step 5): three portal keeps round a middle, 33,700 to 40,900
        // units from each other on foot, and a neutral keep with its guards between them
        // (33089,38271). Each realm starts 2,500 units in front of its portal keep, on the way to
        // the middle. Three points to walk between, one between each two realms and none within
        // 8,500 of the neutral keep: HM between Hibernia and Midgard, HA between Hibernia and
        // Albion, MA between Midgard and Albion. A realm's first team goes round them one way and
        // its second the other, so that the six meet two by two: at their first points Hibernia's
        // first and Midgard's second (16,800 and 17,600 to walk), Hibernia's second and Albion's
        // first (15,400 and 20,300), Midgard's first and Albion's second (12,300 and 17,600).
        // Every leg checked on the installed navmesh, 2026-11-21: each is within 8% of the straight line.
        public static readonly (int X, int Y) ThidHM = (36207, 21468), ThidHA = (26000, 33500), ThidMA = (47000, 36500);

        public static readonly IReadOnlyList<PvpRoamStart> ThidrankiStarts =
        [
            new(eRealm.Albion, (36585, 49967), [ThidHA, ThidHM, ThidMA], [ThidMA, ThidHM, ThidHA]),
            new(eRealm.Midgard, (51955, 26040), [ThidMA, ThidHA, ThidHM], [ThidHM, ThidHA, ThidMA]),
            new(eRealm.Hibernia, (19844, 20271), [ThidHM, ThidMA, ThidHA], [ThidHA, ThidMA, ThidHM]),
        ];

        /// <summary>
        /// The legs of Emain Macha that were walked on the installed navmesh (see <see cref="Middle"/>):
        /// the road, the west side, the two ways between their ends and the way across. The west
        /// side is the quiet ground.
        /// </summary>
        public static readonly IReadOnlyList<((int X, int Y) A, (int X, int Y) B)> EmainWays =
            [(South, Middle), (Middle, North), (North, WestNorth), (WestNorth, WestMiddle), (WestMiddle, WestSouth), (WestSouth, South), (Middle, WestMiddle)];

        public static readonly IReadOnlyList<((int X, int Y) A, (int X, int Y) B)> ThidrankiWays = [(ThidHM, ThidHA), (ThidHA, ThidMA), (ThidMA, ThidHM)];

        public static readonly PvpRoamMap Emain = new("emain", "Emain Macha", EmainRegion, EmainStarts, EmainWays, [WestNorth, WestMiddle, WestSouth],
            [(South, "south road"), (Middle, "mid road"), (North, "north road"), (WestNorth, "north-west hills"), (WestMiddle, "west hills"), (WestSouth, "south-west hills")]);
        public static readonly PvpRoamMap Thidranki = new("thid", "Thidranki", 252, ThidrankiStarts, ThidrankiWays, null,
            [(ThidHM, "between hib and mid"), (ThidHA, "between hib and alb"), (ThidMA, "between mid and alb")]);

        // ---- Hadrian's Wall (region 1, zone 15; M11.3). Hibernia and Midgard at their portal keeps in the north-west and the
        // north-east, Albion at Caer Benowyc in the south-east, each some 2,500 units in front of its keep toward the middle of
        // the zone. Found on the installed navmesh, 2026-11-22 (source/tools/pvp_map_points.py, then each leg by itself): the
        // north half is cut in two between the portal keeps (no way from 629,500 to 636,500 east between 297,000 and 311,000
        // north), and the wall is crossed in the west and in the east. So the points make a U: down the west side, along the
        // south, up the east side. Every leg is within 13% of the straight line (9,900 to 17,700 units). Caer Benowyc opens to
        // the west: from Albion's start the south-east field is 25,800 on foot for 14,400, so Albion's first point is the road
        // west of the keep (18,500 on foot for 16,000). The names are mine.
        public static readonly (int X, int Y) HwNorthWest = (615516, 304220), HwWestGate = (608516, 318220), HwSouthWest = (615516, 332220),
            HwSouth = (629516, 332220), HwSouthEast = (643516, 332220), HwBenowycRoad = (636516, 339220), HwEastGate = (650516, 318220),
            HwNorthEast = (643516, 304220);

        public static readonly PvpRoamMap HadriansWall = new("hadrian", "Hadrian's Wall", 1,
            [
                new(eRealm.Albion, (651802, 343992), [HwBenowycRoad, HwSouth, HwSouthWest, HwWestGate, HwNorthWest], [HwBenowycRoad, HwSouthEast, HwEastGate, HwNorthEast]),
                new(eRealm.Midgard, (653567, 294974), [HwNorthEast, HwEastGate, HwSouthEast, HwSouth, HwSouthWest], [HwEastGate, HwSouthEast, HwBenowycRoad, HwSouth]),
                new(eRealm.Hibernia, (607339, 295574), [HwNorthWest, HwWestGate, HwSouthWest, HwSouth, HwSouthEast], [HwWestGate, HwSouthWest, HwSouth, HwBenowycRoad]),
            ],
            [(HwNorthWest, HwWestGate), (HwWestGate, HwSouthWest), (HwSouthWest, HwSouth), (HwSouth, HwSouthEast), (HwSouthEast, HwEastGate),
             (HwEastGate, HwNorthEast), (HwBenowycRoad, HwSouth), (HwBenowycRoad, HwSouthEast)],
            [HwSouthWest],
            [(HwNorthWest, "north-west field"), (HwWestGate, "west gate"), (HwSouthWest, "south-west field"), (HwSouth, "south field"),
             (HwSouthEast, "south-east field"), (HwBenowycRoad, "benowyc road"), (HwEastGate, "east gate"), (HwNorthEast, "north-east field")]);

        // ---- Odin's Gate (region 100, zone 115; M11.3). Hibernia and Albion at their portal keeps in the north-west and the
        // south-west, Midgard at Bledmeer Faste in the north-east. Found the same way: the west side is cut in two between the
        // two portal keeps (nothing crosses from 598,000 to 605,000 north west of 614,000 east), and the middle of the zone
        // between 614,000 and 628,000 east is not crossed between 598,000 and 612,000 north. So Hibernia goes east along the
        // north, Albion east along the south, and they meet Midgard in the east half. Every leg is within 13% of the straight
        // line (14,000 to 24,200 units). The names are mine.
        public static readonly (int X, int Y) OgNorthWest = (607324, 590940), OgNorthRoad = (628324, 583940), OgNorthEast = (642324, 590940),
            OgEast = (642324, 604940), OgMiddle = (628324, 604940), OgSouthEast = (642324, 618940), OgSouthRoad = (628324, 625940),
            OgSouthWest = (607324, 618940);

        public static readonly PvpRoamMap OdinsGate = new("odin", "Odin's Gate", 100,
            [
                new(eRealm.Albion, (598175, 629643), [OgSouthWest, OgSouthRoad, OgSouthEast, OgEast, OgNorthEast], [OgSouthWest, OgSouthRoad, OgMiddle, OgNorthRoad]),
                new(eRealm.Midgard, (646847, 585287), [OgNorthEast, OgEast, OgSouthEast, OgSouthRoad, OgSouthWest], [OgNorthRoad, OgMiddle, OgSouthRoad, OgSouthWest]),
                new(eRealm.Hibernia, (597987, 583313), [OgNorthWest, OgNorthRoad, OgNorthEast, OgEast, OgSouthEast], [OgNorthRoad, OgMiddle, OgSouthRoad, OgSouthWest]),
            ],
            [(OgNorthWest, OgNorthRoad), (OgNorthRoad, OgNorthEast), (OgNorthRoad, OgMiddle), (OgMiddle, OgEast), (OgEast, OgNorthEast), (OgEast, OgSouthEast),
             (OgSouthEast, OgMiddle), (OgSouthRoad, OgSouthEast), (OgSouthRoad, OgMiddle), (OgSouthWest, OgSouthRoad)],
            [OgSouthEast],
            [(OgNorthWest, "north-west field"), (OgNorthRoad, "north road"), (OgNorthEast, "north-east field"), (OgEast, "east field"), (OgMiddle, "middle"),
             (OgSouthEast, "south-east field"), (OgSouthRoad, "south road"), (OgSouthWest, "south-west field")]);

        /// <summary>The frontier zones an evening is spread over (M11.3). Zones whose points have been walked on the installed navmesh.</summary>
        public static readonly IReadOnlyList<PvpRoamMap> Frontiers = [Emain, OdinsGate, HadriansWall];

        /// <summary>The map a word stands for ("thid", "thidranki", "emain"); null for a word that names none.</summary>
        public static PvpRoamMap MapFor(string word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "thid" or "thidranki" => Thidranki,
            "emain" or "emainmacha" => Emain,
            "odin" or "odins" or "odin's" or "og" or "odinsgate" => OdinsGate,
            "hadrian" or "hadrians" or "hadrian's" or "hw" or "hadrianswall" => HadriansWall,
            _ => null,
        };

        /// <summary>
        /// Where a team of a prime-time evening goes from a point it has reached: to one of the
        /// points a way leads to, not back where it came from unless that is the only way. One
        /// that leans to the quiet ground (<paramref name="leaning"/> above 0) takes a quiet point
        /// four times as readily, one that leans to the road (below 0) a point on the road. The
        /// owner, 2026-11-22: "the unknown from roaming: which team will we face, how many teams,
        /// where, will we jump them or will we get jumped".
        /// </summary>
        /// <param name="roll">Any number that is not negative: the draw.</param>
        public static (int X, int Y) NextPoint(PvpRoamMap map, (int X, int Y) at, (int X, int Y)? cameFrom, int leaning, int roll)
        {
            List<(int X, int Y)> choices = map.From(at);
            if (choices.Count == 0)
                return at;
            if (cameFrom is { } back && choices.Count > 1)
                choices.Remove(back);
            List<int> weights = choices.Select(point =>
            {
                bool quiet = map.Quiet?.Contains(point) == true;
                return leaning > 0 && quiet || leaning < 0 && !quiet ? 4 : 1;
            }).ToList();
            int pick = Math.Abs(roll) % weights.Sum();
            for (int index = 0; index < choices.Count; index++)
            {
                if (pick < weights[index])
                    return choices[index];
                pick -= weights[index];
            }
            return choices[^1];
        }

        /// <summary>
        /// Where the teams of a realm come into the world when there are many: the first at the
        /// start, the others on rings of six around it, 450 units apart, so that eight groups
        /// do not stand in one another.
        /// </summary>
        public static (int X, int Y) SpawnPoint((int X, int Y) start, int index)
        {
            if (index <= 0)
                return start;
            int ring = (index - 1) / 6 + 1;
            double angle = Math.PI / 3 * ((index - 1) % 6) + Math.PI / 6 * ring;
            return ((int)(start.X + Math.Cos(angle) * 450 * ring), (int)(start.Y + Math.Sin(angle) * 450 * ring));
        }

        /// <summary>A realm's name. (The enum has two names for Albion and for Hibernia, and writes the other ones.)</summary>
        public static string NameOf(eRealm realm) => realm switch
        {
            eRealm.Albion => "Albion", eRealm.Midgard => "Midgard", eRealm.Hibernia => "Hibernia", _ => realm.ToString(),
        };

        /// <summary>A realm as it was written in chat: "albs", "mids", "hibs"; or, for a keep, "alb", "mid", "hib".</summary>
        public static string Short(eRealm realm, bool plural = true) =>
            (realm switch { eRealm.Albion => "alb", eRealm.Midgard => "mid", eRealm.Hibernia => "hib", _ => "?" }) + (plural ? "s" : string.Empty);

        /// <summary>
        /// What a group of the player's realm says in realm chat about enemies it sees: "2fg albs
        /// at south road", "fighting 1fg mids at mid road", "wiped at north road, 3fg mids". The
        /// owner, 2026-11-22, on the unknown from roaming: "which team will we face, how many
        /// teams (1 team, a zerg [many teams]), where". A realm only ever knew that from what
        /// its own groups called out.
        /// </summary>
        public static string SightingText(int groups, eRealm realm, string place, bool fightingThem, bool wiped = false)
        {
            string who = $"{Math.Max(1, groups)}fg {Short(realm)}", where = place == null ? string.Empty : $" at {place}";
            return wiped ? $"wiped{where}, {who}" : fightingThem ? $"fighting {who}{where}" : $"{who}{where}";
        }

        /// <summary>A place as it is called out on an evening over several zones: "south road (Emain)". With one zone, the place alone.</summary>
        private static string InZone(PvpRoamMap map, string place) =>
            _maps.Count <= 1 ? place : place == null ? map.Tag : $"{place} ({map.Tag})";

        /// <summary>How far a group sees enemies it then calls out, and how near the player must be for it to be no news.</summary>
        public const int SightingWithin = 5_500, NoNewsWithin = 3_500;

        /// <summary>
        /// Realm chat is not a flood: one call in eight seconds; and about one realm at one place
        /// one call, whoever makes it, until more groups have come there (then after half a
        /// minute) or three minutes have gone by. In the first evening of bots (2026-11-22)
        /// three groups in one fight each said "fighting 2fg mids at north-west hills" every 75
        /// seconds; in the second, with every change of the number, a line every 18 seconds.
        /// </summary>
        public static bool CallsOut(double secondsSinceAnyCall, double secondsSinceTheLastOfThatRealmThere, bool moreHaveCome) =>
            secondsSinceAnyCall >= 8 && secondsSinceTheLastOfThatRealmThere >= (moreHaveCome ? 30 : 180);

        /// <summary>
        /// When a guild comes out on a prime-time evening, in seconds from its start: a realm's
        /// teams half a minute after one another, and the realms ten seconds apart, so that no
        /// two come into the world in the same moment. All twelve of the first evening of bots
        /// (2026-11-22) were made in its first second, and the server stood still for 6.9 seconds.
        /// </summary>
        public static int ComesOutAfterSeconds(int botTeamsOfItsRealmBefore, int realmIndex, PvpRealWeights real) =>
            Math.Max(0, botTeamsOfItsRealmBefore) * real.NextTeamFollowsAfterSeconds + Math.Max(0, realmIndex) * 10;

        /// <summary>
        /// A guild's evening (M11.4): it logs off when it has been wiped three times, and when
        /// its time is up (25 to 50 minutes from its coming out) at its next wipe or when it is
        /// on the road with nobody about. Another guild of its realm then comes out, so that who
        /// is out changes through the evening. The owner, 2026-11-22: "the unknown from roaming:
        /// which team will we face".
        /// </summary>
        public static bool LogsOff(int wipes, bool itsTimeIsUp, bool wiped, bool onTheRoadWithNobodyAbout) =>
            wiped && wipes >= EnoughWipes || itsTimeIsUp && (wiped || onTheRoadWithNobodyAbout);

        public const int EnoughWipes = 3;

        /// <summary>How long a guild stays out, in seconds: 25 to 50 minutes. <paramref name="roll"/> is any number that is not negative.</summary>
        public static int EveningSeconds(int roll) => 25 * 60 + Math.Abs(roll) % (25 * 60 + 1);

        /// <summary>
        /// A guild's grudge (M11.6), this evening: one the player's group has wiped twice "comes
        /// looking" (it goes from point to point toward the player's group instead of as it
        /// pleases), and so does one the player's group has added on.
        /// </summary>
        public static bool ComesLooking(int wipedByThePlayerThisEvening, bool addedOnByThePlayer) => wipedByThePlayerThisEvening >= 2 || addedOnByThePlayer;

        /// <summary>
        /// "A team that was added on remembers": a guild the player's group has added on joins
        /// the next fight of the player's group it comes on, whatever its kind does otherwise.
        /// </summary>
        public static bool PaysBack(bool addedOnByThePlayer, bool thePlayersGroupIsInTheFight) => addedOnByThePlayer && thePlayersGroupIsInTheFight;

        /// <summary>
        /// The player's group has added on a team: it came into a fight that team was in with
        /// somebody else, and that team had not come into the fight by a draw of its own.
        /// </summary>
        public static bool WasAddedOn(bool thePlayersGroupIsNewInItsFight, int othersItWasFighting, bool itJoinedByItsOwnDraw) =>
            thePlayersGroupIsNewInItsFight && othersItWasFighting >= 1 && !itJoinedByItsOwnDraw;

        /// <summary>The point a way leads to from <paramref name="at"/> that is nearest to a place: where a team that comes looking goes next.</summary>
        public static (int X, int Y) PointToward(PvpRoamMap map, (int X, int Y) at, (double X, double Y) place)
        {
            List<(int X, int Y)> choices = map.From(at);
            return choices.Count == 0 ? at : choices.OrderBy(point => Math.Pow(point.X - place.X, 2) + Math.Pow(point.Y - place.Y, 2)).First();
        }

        /// <summary>A group that stays near others comes no nearer to them than this: it trails, it does not stand in them.</summary>
        public const int TrailsBehind = 1_800;

        /// <summary>
        /// How near a team keeps to the team it walks with: a pack within 2,000, a group that stays
        /// near others within 3,500. (4,500 at first: in an evening of 24 teams, 2026-11-22, the
        /// groups that stay near others were a middle 4,000 to 5,700 from theirs, mostly out of sight.)
        /// </summary>
        public static int KeepsWithin(PvpCompany company) => company == PvpCompany.Zerg ? 2_000 : 3_500;

        /// <summary>The head of a pack that has reached a point gives the teams behind it up to 25 seconds to come up.</summary>
        public static bool PackWaits(bool someoneOfItsPackIsBehind, double secondsAtThePoint) => someoneOfItsPackIsBehind && secondsAtThePoint < 25;

        /// <summary>How many groups of its realm a team has together, itself and those beside it.</summary>
        private static int Together(PvpRoamTeam team, List<PvpRoamTeam> teams) =>
            teams.Count(mate => mate.Realm == team.Realm && mate.Alive > 0 && (mate == team || Near(team, mate, BesideWithin)));

        private static volatile PvpRoamMap _map = Emain;
        // Every zone of the evening; the one above is where it began (the player's, when there is a player).
        private static volatile List<PvpRoamMap> _maps = [Emain];
        // The player's group is on its way to another zone since this tick (0: it is not): its seven are made anew there.
        private static long _travelling;

        /// <summary>
        /// Which zone a team that walks alone, or the head of a pack, takes on an evening over
        /// several: they take the zones in turn, so that each zone has its share of each realm.
        /// A team that walks with another goes where that one goes.
        /// </summary>
        public static int ZoneInTurn(int turn, int zones) => zones <= 0 ? 0 : Math.Abs(turn) % zones;

        /// <summary>
        /// A guild that walks alone and has been wiped ports to another frontier one time in
        /// three, when the evening has more than one zone: "Maps? I would assume more than just
        /// emain" (the owner, 2026-11-22). A pack and a group that stays near others stay where they are.
        /// </summary>
        public static bool PortsElsewhere(int zones, bool walksWithSomebody, bool somebodyWalksWithIt, int roll) =>
            zones > 1 && !walksWithSomebody && !somebodyWalksWithIt && roll % 3 == 0;

        /// <summary>Puts a team in a zone: at its realm's start there, with the place and the first point of the next team to come.</summary>
        private static void Place(PvpRoamTeam team, PvpRoamMap map, IEnumerable<PvpRoamTeam> teams)
        {
            PvpRoamStart start = map.StartOf(team.Realm);
            int index = teams.Count(other => other != team && other.Map == map && other.Realm == team.Realm);
            (team.Map, team.Start, team.Index, team.SpawnAt) = (map, start, index, SpawnPoint(start.Point, index));
            if (team.Leader != null)
                return;
            team.Entry = team.Guild != null && map.Ways != null ? start.RouteOf(index % 2 + 1)[0] : null;
            (team.Team.Route, team.Team.Waypoint, team.CameFrom, team.ArrivedAt) = (team.Entry is { } entry ? [entry] : start.RouteOf(index % 2 + 1), 0, null, 0);
        }
        // The pool the guild teams of a prime-time evening are from; null in the roam as it was.
        private static string _pool;
        // What was called out in realm chat, and when: by each group about each enemy realm, and the last call of all.
        private static readonly Dictionary<(eRealm Realm, string Place), (long When, int Groups)> CalledOut = [];
        private static long _lastCall;
        // The guilds that have logged off this evening: their records are written with the others', and they do not come out again.
        private static readonly List<PvpRoamTeam> Gone = [];
        // With no player: the realm whose chat is written to the log and the recording (roam-start Listener=hibernia), to read it by.
        private static eRealm _listener = eRealm.None;

        /// <summary>True on a prime-time evening (M11).</summary>
        public static bool Prime => _pool != null;

        public static void ListenTo(eRealm realm) => _listener = realm;
        private static volatile List<PvpRoamTeam> _teams = [];
        // No bot team comes in below this realm level: the average of the player's group, when there is a player.
        private static int _floor = 1;
        private static Timer _timer;
        private static int _ticking;
        private static long _started;
        private static string _lastError;
        private static readonly List<string> Events = [];
        private static PvpRecording _recording;

        public static bool Running => _teams.Count > 0;

        /// <summary>The names of a realm's teams: "mid-main" and "mid-main#2".</summary>
        public static string TeamName(string groupId, int number) => number <= 1 ? groupId : $"{groupId}#{number}";

        /// <summary>Whether a team that comes on a fight joins it: one draw, <paramref name="roll"/> from 0 to 99.</summary>
        public static bool Joins(int addChancePercent, int roll) => roll < addChancePercent;

        /// <summary>
        /// Whether an enemy team is in a fight this team has come on from outside, which is what
        /// the draw is for: it fights somebody else, this team is not fighting it, and that
        /// somebody is not one this team is fighting (then the enemy team came into this team's
        /// fight, and is simply an enemy). The somebody may be this team's realm-mate or a team
        /// of the third realm: "should we assjam or not an ongoing fight. Most of the respected
        /// 8v8 team were not." Until 2026-11-21 the draw was for a realm-mate's fight only, and
        /// in the first roam in Thidranki each of the two 8v8 fights had a team of the third
        /// realm in it within a minute.
        /// </summary>
        public static bool ComesOnAFight(bool theyFightSomebodyElse, bool thatSomebodyIsInOurFight, bool weFightThemAlready) =>
            theyFightSomebodyElse && !thatSomebodyIsInOurFight && !weFightThemAlready;

        /// <summary>
        /// A fight left alone is left alone by both sides: the team that drew "leaves them to it"
        /// does not attack, and the team it left to its fight does not turn on it for walking
        /// past, unless one of them does attack the other, or the player picks one of them as a
        /// target. Until 2026-11-21 only the first half held: in the owner's first roam the second
        /// Hibernian team left an Albion team to the owner's and was attacked by it eight seconds
        /// later, and the owner's companions went for a Midgard team that had left them alone.
        /// </summary>
        public static bool LeavesAlone(bool weLeftThemToIt, bool theyLeftUsToIt, bool theyAttackUs, bool ourPlayerAimsAtThem) =>
            (weLeftThemToIt || theyLeftUsToIt) && !theyAttackUs && !ourPlayerAimsAtThem;

        /// <summary>
        /// A team of bots that has left a fight near it to its realm-mate, and has nobody of its
        /// own to fight, waits where the draw fell (some 9,000 units off) until that fight is
        /// over. Its road is the fight's road: in the first roam with the half-minute gap
        /// (2026-11-21) the second Hibernian team drew "leaves them to it" at 142 s, walked on
        /// into the fight and was in it at 166 s, with the second Midgard team that had done the
        /// same from the other side.
        /// </summary>
        public static bool WaitsOutAFight(bool leftAFightNearToIt, int enemiesToFight, bool ledByAPlayer) =>
            leftAFightNearToIt && enemiesToFight == 0 && !ledByAPlayer;

        /// <summary>
        /// Which enemy teams the player's own group reckons with in a roam: one it is fighting,
        /// one that attacks it, and one the player has picked a member of as a target, within
        /// the range the group forms up at. Any other is passed by: the companions stay in the
        /// clump they travel in behind the player and the Bard keeps its speed song. The owner,
        /// 2026-11-21: "When /8v8 roam, group should not spread / bard play endurance if we do
        /// not engage". In a single fight (lane, basin) there is one enemy and it is the fight:
        /// there the group takes its rows on sight, as before.
        /// </summary>
        public static bool PlayerTakesItOn(bool fightingItAlready, bool itAttacksUs, bool playerAimsAtItInReach) =>
            fightingItAlready || itAttacksUs || playerAimsAtItInReach;

        /// <summary>
        /// A team of bots that is in a fight takes on nobody new: a group that walks by is left
        /// alone until it attacks. The owner, 2026-11-21: "Let say mids are fighting alb and I
        /// walk by. Neither mids or albs will try to attack my group, as they are already
        /// fighting. Only if I decide to add on their fight, they will need to react." Until then
        /// only a team of bots that had drawn "leaves them to it" was spared; the player's group,
        /// which makes no draw, was attacked by both.
        /// </summary>
        public static bool TooBusyFor(int teamsItIsFighting, bool fightingThatOne, bool thatOneAttacksIt) =>
            teamsItIsFighting > 0 && !fightingThatOne && !thatOneAttacksIt;

        /// <summary>
        /// A team of bots on its own does not start a fight with a team that has its realm-mate
        /// beside it: eight do not walk into sixteen. In a roam of 2026-11-22 a Hibernian team
        /// took on both Midgard teams, which had just finished a fight together, in the same
        /// second. If the two come for it, it answers.
        /// </summary>
        public static bool AvoidsTwo(bool itHasItsRealmMateBesideIt, bool weHaveOursBesideUs, bool fightingItAlready, bool itAttacksUs) =>
            itHasItsRealmMateBesideIt && !weHaveOursBesideUs && !fightingItAlready && !itAttacksUs;

        /// <summary>How near a realm-mate is "beside" a team.</summary>
        public const int BesideWithin = 3_500;

        /// <summary>
        /// The places a team may go to rest, in the order it tries them: straight off the road it
        /// is walking (<paramref name="toward"/> is where it was going), to one side and to the
        /// other, at each distance. <paramref name="leftFirst"/> is drawn, so the teams do not
        /// all go the same way.
        /// </summary>
        public static List<(int X, int Y)> HidePlaces((double X, double Y) at, (double X, double Y) toward, IEnumerable<int> distances, bool leftFirst)
        {
            double dx = toward.X - at.X, dy = toward.Y - at.Y, length = Math.Sqrt(dx * dx + dy * dy);
            var places = new List<(int X, int Y)>();
            if (length < 1)
                return places;
            (double X, double Y) side = (-dy / length, dx / length);
            foreach (int distance in distances)
            {
                foreach (int sign in leftFirst ? new[] { 1, -1 } : new[] { -1, 1 })
                    places.Add(((int)(at.X + side.X * distance * sign), (int)(at.Y + side.Y * distance * sign)));
            }
            return places;
        }

        /// <summary>A place off its road that the team can walk to without a long way round; null when the ground has none.</summary>
        private static System.Numerics.Vector3? PickHide(PvpRoamTeam team, PvpRealWeights real)
        {
            if (real.RestsOffTheRoadBy is not { Length: > 0 } distances || team.Team.Route is not { Length: > 0 } route ||
                WorldMgr.GetRegion(team.Map.Region) is not { } region || team.Team.Bots.FirstOrDefault(bot => bot.IsAlive) is not { } first)
                return null;
            (double x, double y) = team.Centre();
            (int X, int Y) next = route[team.Team.Waypoint % route.Length];
            var nav = PathfindingProvider.Instance;
            var from = new System.Numerics.Vector3((float)x, (float)y, first.Z);
            if (nav.GetFloorBeneath(first.CurrentZone, new System.Numerics.Vector3((float)x, (float)y, 20_000), 20_000, nav.DefaultFilters) is { } here && here.Z < 19_999)
                from = here;
            foreach ((int X, int Y) place in HidePlaces((x, y), (next.X, next.Y), distances, Random.Shared.Next(2) == 0))
            {
                Zone zone = region.GetZone(place.X, place.Y);
                if (zone == null || zone != first.CurrentZone ||
                    nav.GetFloorBeneath(zone, new System.Numerics.Vector3(place.X, place.Y, 20_000), 20_000, nav.DefaultFilters) is not { } floor || floor.Z >= 19_999)
                    continue;
                (bool arrives, int length) = PvpArena.Walk(zone, from, floor);
                if (arrives && length <= 1.8 * System.Numerics.Vector3.Distance(from, floor))
                    return floor;
            }
            return null;
        }

        /// <summary>What a team is doing, in a word or two, for <c>/8v8 status</c>.</summary>
        public static string Doing(string state, bool waiting, bool goingToRest, bool resting, bool seeingToItsDead, IReadOnlyCollection<string> fighting,
            int leavesInSeconds, int standsUpInSeconds) =>
            state == "Away" ? "not out yet"
            : state == "Down" ? standsUpInSeconds > 0 ? $"wiped, stands up at its start in {standsUpInSeconds} s" : "wiped, about to stand up at its start"
            : state == "Buffing" ? $"at its start, buffing (leaves in {Math.Max(0, leavesInSeconds)} s)"
            : fighting.Count > 0 ? "fighting " + string.Join(" and ", fighting)
            : resting ? "resting"
            : goingToRest ? "walking off its road to rest"
            : seeingToItsDead ? "raising its dead"
            : waiting ? "waiting by a fight"
            : "roaming";

        /// <summary>A line for each team of the roam, for a player (<c>/8v8 status</c>); empty when no roam runs.</summary>
        public static List<string> StatusLines(GamePlayer viewer)
        {
            List<PvpRoamTeam> teams = _teams;
            long now = GameLoop.GameLoopTime;
            PvpRealWeights real = PvpWeights.Current.Real;
            var lines = new List<string>();
            // On a prime-time evening a player knows what its own realm is doing, and of the others only what is called out.
            bool own = Prime && viewer != null;
            foreach (PvpRoamTeam team in teams.Where(team => !own || team.Realm == viewer.Realm && team.State != "Away"))
            {
                (double x, double y) = team.Centre();
                int away = viewer == null ? 0 : (int)Math.Sqrt(Math.Pow(x - viewer.X, 2) + Math.Pow(y - viewer.Y, 2));
                string doing = Doing(team.State, team.Waiting, team.Hide != null, team.Team.Squad?.Resting == true, team.DeadSince != 0 && !team.Reforming,
                    team.Fighting.Select(other => other.Name).ToList(), (int)((team.LeavesAt - now) / 1000),
                    (int)(real.WipeStandsUpAfterSeconds - (now - team.StateSince) / 1000));
                PvpRoamMap viewersZone = teams.FirstOrDefault(mine => mine.Leader == viewer && viewer != null)?.Map;
                lines.Add($"{team.Name}{(team.Leader != null ? " (yours)" : string.Empty)}: {doing}; {team.Alive} alive" +
                          (team.Leader != null || viewer == null ? string.Empty
                              : viewersZone != null && team.Map != viewersZone ? $", in {team.Map.Name}" : $", {away:N0} from you"));
            }
            if (own && lines.Count > 0)
                lines.Add(OutAgainst(teams.Where(team => team.Realm != viewer.Realm && team.State != "Away").GroupBy(team => team.Realm).Select(realm => (realm.Key, realm.Count())).ToList()));
            return lines;
        }

        /// <summary>The last line of what a player is told on a prime-time evening: how many groups the other realms have out, and no more.</summary>
        public static string OutAgainst(IReadOnlyList<(eRealm Realm, int Groups)> others) =>
            others.Count == 0 ? "Nobody else is out."
                : "Out against you: " + string.Join(" and ", others.Select(other => $"{other.Groups} {(other.Groups == 1 ? "group" : "groups")} of {NameOf(other.Realm)}")) +
                  ". Where they are, your realm says when it sees them.";

        /// <summary>
        /// <c>/8v8 who</c>: the guilds of the player's realm that are out, each with its rank and
        /// who is in it, as <c>/who</c> showed a realm its own; and how many groups the other
        /// realms have out. Empty when no roam runs.
        /// </summary>
        public static List<string> WhoLines(GamePlayer viewer)
        {
            List<PvpRoamTeam> teams = _teams;
            var lines = new List<string>();
            foreach (PvpRoamTeam team in teams.Where(team => team.State != "Away" && (viewer == null || team.Realm == viewer.Realm)))
            {
                string classes = string.Join(", ", team.Team.Bots.GroupBy(bot => PvpMember.ClassOf(bot)).OrderByDescending(group => group.Count())
                    .Select(group => group.Count() > 1 ? $"{group.Count()} {group.Key}" : group.Key.ToString()));
                string rank = PvpRealmPoints.RankName((int)Math.Round(team.Team.Bots.Select(bot => bot.RealmLevel).DefaultIfEmpty(1).Average()));
                lines.Add($"{team.Name}{(team.Leader != null ? " (yours)" : string.Empty)}, rank {rank}{(_maps.Count > 1 ? ", " + team.Map.Tag : string.Empty)}: {classes}" +
                          (team.Company != null ? $"; walks with {team.Company.Name}" : string.Empty));
            }
            if (viewer != null && lines.Count > 0)
                lines.Add(OutAgainst(teams.Where(team => team.Realm != viewer.Realm && team.State != "Away").GroupBy(team => team.Realm).Select(realm => (realm.Key, realm.Count())).ToList()));
            return lines;
        }

        /// <summary>
        /// <c>/8v8 rivals</c>: the guilds of the other realms the player's group has wiped or
        /// been wiped by, most met first, with this evening's numbers beside those of all
        /// evenings. Only what the player has seen happen: nothing about where anybody is.
        /// </summary>
        public static List<string> RivalLines(GamePlayer viewer)
        {
            List<PvpRoamTeam> teams = _teams;
            Dictionary<string, PvpRoamTeam> tonight = teams.Concat(Gone).Where(team => team.Guild != null)
                .GroupBy(team => team.Guild.Id).ToDictionary(same => same.Key, same => same.First());
            var lines = new List<string>();
            foreach (PvpGuildTeam guild in PvpTeamPool.Load(AppContext.BaseDirectory, _pool ?? PvpTeamPool.World)
                         .Where(guild => viewer == null || guild.ToRealm() != viewer.Realm))
            {
                tonight.TryGetValue(guild.Id, out PvpRoamTeam now);
                int wiped = guild.WipedByPlayer + (now?.WipedByPlayer ?? 0), wipedYou = guild.WipedPlayer + (now?.WipedPlayer ?? 0);
                if (wiped + wipedYou == 0)
                    continue;
                lines.Add(RivalLine(guild.Guild, NameOf(guild.ToRealm()), wiped, now?.WipedByPlayer ?? 0, wipedYou, now?.WipedPlayer ?? 0));
            }
            return lines.OrderByDescending(line => line.Length).ToList();
        }

        public static string RivalLine(string guild, string realm, int wiped, int wipedTonight, int wipedYou, int wipedYouTonight) =>
            $"{guild} <{realm}>: you wiped them {wiped} {(wiped == 1 ? "time" : "times")}{(wipedTonight > 0 ? $" ({wipedTonight} tonight)" : string.Empty)}, " +
            $"they were in on your wipe {wipedYou} {(wipedYou == 1 ? "time" : "times")}{(wipedYouTonight > 0 ? $" ({wipedYouTonight} tonight)" : string.Empty)}";

        /// <summary>A line of realm chat for the player, from a member of a group of its realm; written to the log and the recording too.</summary>
        private static void RealmChat(List<PvpRoamTeam> teams, PvpRoamTeam from, string text)
        {
            string speaker = from.Team.Bots.FirstOrDefault(bot => bot.IsAlive)?.Name ?? from.Team.Bots.FirstOrDefault()?.Name ?? from.Name;
            Say($"[{from.Name}] {speaker}: {text}", new { what = "chat", team = from.Name, speaker, text });
            foreach (PvpRoamTeam mine in teams.Where(team => team.Leader?.Client?.IsPlaying == true && team.Realm == from.Realm))
                mine.Leader.Out.SendMessage($"[Realm] {speaker} <{from.Name}>: {text}", PacketHandler.eChatType.CT_Alliance, PacketHandler.eChatLoc.CL_ChatWindow);
        }

        /// <summary>The realm whose chat is made: the player's, or the one listened to when there is no player.</summary>
        private static eRealm Hears(List<PvpRoamTeam> teams) => teams.FirstOrDefault(team => team.Leader != null)?.Realm ?? _listener;

        /// <summary>One group of the player's realm calls out the enemies it sees, if it has not just done so (<see cref="CallsOut"/>).</summary>
        private static void CallOut(List<PvpRoamTeam> teams, long now)
        {
            eRealm realm = Hears(teams);
            if (!Prime || realm == eRealm.None || now - _lastCall < 8_000)
                return;
            PvpRoamTeam mine = teams.FirstOrDefault(team => team.Leader != null);
            foreach (PvpRoamTeam friend in teams.Where(team => team != mine && team.Realm == realm && team.Alive > 0 && team.State is "Roaming" or "Fighting"))
            {
                foreach (IGrouping<eRealm, PvpRoamTeam> seen in teams.Where(other => other.Realm != realm && other.Alive > 0 && !other.Reforming &&
                             other.State is "Roaming" or "Fighting" && Near(friend, other, SightingWithin)).GroupBy(other => other.Realm))
                {
                    (double x, double y) = seen.First().Centre();
                    string place = InZone(friend.Map, friend.Map.PlaceOf(x, y));
                    bool known = CalledOut.TryGetValue((seen.Key, place ?? string.Empty), out (long When, int Groups) last);
                    if (!CallsOut((now - _lastCall) / 1000.0, known ? (now - last.When) / 1000.0 : double.MaxValue, known && seen.Count() > last.Groups))
                        continue;
                    // The player stands there: no news.
                    if (mine?.Leader is { } player && mine.Map == friend.Map && player.IsWithinRadius(new Point2D((int)x, (int)y), NoNewsWithin))
                        continue;
                    (CalledOut[(seen.Key, place ?? string.Empty)], _lastCall) = ((now, seen.Count()), now);
                    RealmChat(teams, friend, SightingText(seen.Count(), seen.Key, place, seen.Any(friend.Fighting.Contains)));
                    return;
                }
            }
        }

        /// <summary>
        /// A bot of a roam does not turn on somebody of the roam that its group is not fighting,
        /// whatever hit it: an area spell thrown in the fight next to its own. Counted in a roam
        /// of bots on 2026-11-22: a fight of two teams at the north end of Emain Macha, two
        /// teams waiting beside it that met and fought each other, and within twenty seconds all
        /// four in one fight that the two others then walked into; it went on for fifteen
        /// minutes. Each time, a bot hit from the fight beside it had taken its attacker as a
        /// target, which made the two teams "at each other", and "joins after all" did the rest.
        /// A team that does mean to attack it is among its group's enemies a second later.
        /// </summary>
        public static bool IgnoresHit(bool roamRuns, bool hasItsGroupsEnemies, bool attackerIsOfTheRoam, bool attackerIsAnEnemyOfItsGroup) =>
            roamRuns && hasItsGroupsEnemies && attackerIsOfTheRoam && !attackerIsAnEnemyOfItsGroup;

        /// <summary>True when a bot should not answer this attacker (<see cref="IgnoresHit"/>); a pet counts as its owner.</summary>
        public static bool IgnoresHit(GameBot bot, IReadOnlyList<GameLiving> itsGroupsEnemies, GameLiving attacker)
        {
            if (bot?.IsPvpBot != true || attacker == null || itsGroupsEnemies == null || !Running)
                return false;
            GameLiving who = attacker is GameNPC { Brain: IControlledBrain owned } ? owned.GetLivingOwner() ?? attacker : attacker;
            List<PvpRoamTeam> teams = _teams;
            return IgnoresHit(true, true, teams.Any(team => team.Team.Members.Contains(who)), itsGroupsEnemies.Contains(who));
        }

        /// <summary>A team's fight is over when it has had no enemy to reckon with for this long: twelve seconds.</summary>
        public const int FightOverAfterMilliseconds = 12_000;

        public static bool FightIsOver(long millisecondsWithNoEnemy) => millisecondsWithNoEnemy > FightOverAfterMilliseconds;

        /// <summary>
        /// A team that left a fair fight alone does not go on watching when the other side adds:
        /// with two teams of one realm on its realm-mate, it joins after all. In the roam of
        /// 2026-11-21 a Midgard team stood 8,000 units off while its realm-mate fought two
        /// Hibernian teams, the second of which had drawn "joins". One team of each other realm
        /// on its realm-mate is a three-way fight and nobody's sixteen against eight: in the
        /// second roam in Thidranki that was taken for an add, and the fight had four teams.
        /// </summary>
        /// <param name="itsRealmMateAddedItself">
        /// The realm-mate is in that fight by its own draw: it jumped two teams that were
        /// fighting, has two of one realm on it for that, and is not owed any help. In the
        /// owner's roam of 2026-11-21 this was most of the sixteen times the rule fired.
        /// </param>
        public static bool EvensTheOdds(int mostTeamsOfOneRealmOnItsRealmMate, bool itsRealmMateAddedItself = false) =>
            mostTeamsOfOneRealmOnItsRealmMate >= 2 && !itsRealmMateAddedItself;

        private static int MostOfOneRealm(IEnumerable<PvpRoamTeam> teams) =>
            teams.GroupBy(team => team.Realm).Select(realm => realm.Count()).DefaultIfEmpty(0).Max();

        /// <summary>The squad of the team a player leads in the roam; null for a player who leads none.</summary>
        public static PvpSquad SquadOf(GamePlayer player) =>
            player == null ? null : _teams.FirstOrDefault(team => team.Leader == player)?.Team.Squad;

        /// <summary>Where a team of this realm starts, on the ground.</summary>
        public static Point3D StartSpot(PvpRoamMap map, eRealm realm)
        {
            PvpRoamStart start = map.StartOf(realm);
            var nav = PathfindingProvider.Instance;
            Zone zone = WorldMgr.GetRegion(map.Region)?.GetZone(start.Point.X, start.Point.Y);
            System.Numerics.Vector3 point = zone != null && nav.GetFloorBeneath(zone, new System.Numerics.Vector3(start.Point.X, start.Point.Y, 20_000), 20_000, nav.DefaultFilters)
                is { } floor && floor.Z < 19_999 ? floor : new System.Numerics.Vector3(start.Point.X, start.Point.Y, 4_000);
            return new Point3D((int)point.X, (int)point.Y, (int)point.Z);
        }

        /// <summary>
        /// Where a player who releases goes: the start of the team it leads in a running roam
        /// ("Albion would spawn (and release) at their principal keep"); null for anybody else,
        /// who releases as the game has it. The bind point is in another region, and a player
        /// who left the zone was a player who had left the game: the roam ended.
        /// </summary>
        public static (ushort Region, Point3D Spot)? ReleasePoint(GamePlayer player) =>
            player != null && _teams.FirstOrDefault(team => team.Leader == player) is { } mine ? (mine.Map.Region, StartSpot(mine.Map, player.Realm)) : null;

        /// <summary>
        /// <c>/8v8 go</c>: on a prime-time evening over several zones the player's group ports to
        /// its realm's start in another, as a group did from its portal keep. Not out of a fight.
        /// The seven are made anew there, around the player.
        /// </summary>
        public static void GoTo(GamePlayer player, PvpRoamMap map, Action<string> say)
        {
            PvpRoamTeam mine = _teams.FirstOrDefault(team => team.Leader == player);
            if (mine == null || !Prime)
                throw new InvalidOperationException("You are not out on a prime-time evening. /8v8 prime starts one.");
            if (map == null || !_maps.Contains(map))
                throw new InvalidOperationException("This evening is on in: " + string.Join(", ", _maps.Select(zone => zone.Name)) + ". /8v8 go emain, odin or hadrian.");
            if (mine.Map == map)
                throw new InvalidOperationException($"You are in {map.Name}.");
            if (!player.IsAlive || player.InCombat || mine.ContactTick != 0 || mine.Fighting.Count > 0)
                throw new InvalidOperationException("Not out of a fight: when it is over.");
            _travelling = GameLoop.GameLoopTime;
            NpcService.Instance.Post(_ =>
            {
                PvpArena.Remove(mine.All);
                mine.All.Clear();
                mine.Team.Bots = [];
                PvpRecorder.Regroup(_recording, mine.Name, [player]);
            }, 0);
            PvpScrim.TravelThen(player, map.Region, StartSpot(map, player.Realm), say, () => NpcService.Instance.Post(_ =>
            {
                try
                {
                    if (!_teams.Contains(mine))
                        return;
                    long now = GameLoop.GameLoopTime;
                    Place(mine, map, _teams);
                    Spawn(mine, now);
                    PvpRecorder.Regroup(_recording, mine.Name, mine.Team.Members);
                    Say($"{mine.Name} ports to {map.Name}", new { what = "port", team = mine.Name, zone = map.Name });
                    say($"You are in {map.Name}, at your realm's start, with your group. A wipe or /release puts you back here.");
                }
                catch (Exception exception)
                {
                    say("Your group could not follow: " + exception.Message);
                }
                finally
                {
                    _travelling = 0;
                }
            }, 0), map.Name);
        }

        /// <summary>
        /// Whether a team is wiped: nobody alive, or, for the player's, every companion dead and
        /// the player dead or back at the start (released). A player who releases while
        /// companions still fight waits for them at the start; they come to the player if they win.
        /// </summary>
        public static bool IsWiped(int botsAlive, bool hasPlayer, bool playerAlive, bool playerAtStart) =>
            botsAlive == 0 && (!hasPlayer || !playerAlive || playerAtStart);

        /// <summary><c>/8v8 roam</c>: the player is taken to the start of its realm and leads the first team of it.</summary>
        /// <param name="own">The player's group when it is not the realm's main one (PvpBuildCatalog.Variants).</param>
        /// <param name="prime">Above 0: a prime-time evening (M11) with this many groups of each realm, the player's one of them, the others guilds of the world.</param>
        /// <param name="oneZone">A prime-time evening in the one zone, not over all the frontiers.</param>
        public static void StartFor(GamePlayer player, Action<string> say, PvpGroupTemplate own = null, PvpRoamMap map = null, int prime = 0, bool oneZone = false)
        {
            map ??= Emain;
            if (own != null && own.Realm != player.Realm)
                own = null;
            if (PvpScrim.Running)
                throw new InvalidOperationException("A fight or a roam is already running. /8v8 stop ends it.");
            // (The ways of Odin's Gate and Hadrian's Wall do not close into a round: a team of the roam of six walks a round.)
            if (prime <= 0 && map != Emain && map != Thidranki)
                throw new InvalidOperationException($"The roam of six is in Emain Macha or Thidranki. /8v8 prime {map.Id} begins a prime-time evening in {map.Name}.");
            if (PvpPlayerReady.EnterReal(player) is { } set)
                say(set);
            PvpScrim.TravelThen(player, map.Region, StartSpot(map, player.Realm), say, () =>
            {
                try
                {
                    if (prime > 0)
                    {
                        List<PvpGuildTeam> pool = PvpTeamPool.LoadOrCreate(AppContext.BaseDirectory, PvpTeamPool.World, Math.Max(16, prime * 2), Random.Shared);
                        // Over the frontiers, unless one zone was asked for; a battleground is always one zone.
                        List<PvpRoamMap> zones = oneZone || !Frontiers.Contains(map) ? null : Frontiers.ToList();
                        Start(2, player.InternalID, player, own, map, null, PvpTeamPool.Tonight(pool, prime, Random.Shared, player.Realm), PvpTeamPool.World, zones);
                        say($"Prime time{(zones is { Count: > 1 } ? " in " + string.Join(", ", zones.Select(zone => zone.Name)) + "; you are in " + map.Name : " in " + map.Name)}: " +
                            $"{prime} groups of each realm are out, yours one of them, the others guilds of every kind. " +
                            "Some walk in packs, some add, some do neither. Kills earn realm points that are kept, and a wipe puts your group " +
                            "back here (so does /release). /8v8 who, /8v8 status, /8v8 go <zone>, /8v8 rivals, /8v8 stop.");
                        return;
                    }
                    Start(2, player.InternalID, player, own, map);
                    say($"The roam is on: two groups of each realm in {map.Name}, yours with you here. Kills earn realm points that are kept, " +
                        "no timer is reset, and a wipe puts your group back here (so does /release). Your group travels behind you until you " +
                        "target an enemy within sight or one attacks you; then it takes its rows and fights. /8v8 ranks, /8v8 stop.");
                }
                catch (Exception exception)
                {
                    say("The roam could not start: " + exception.Message);
                }
            }, map.Name);
        }

        /// <param name="lineups">Groups to use for their realms' teams of bots in place of the main ones (a test of a line-up; null: the main ones).</param>
        /// <param name="guilds">A prime-time evening (M11): these teams are out, with the player's own if there is a player; <paramref name="teamsPerRealm"/> is not used.</param>
        /// <param name="pool">The pool the guilds are from: their rosters and their records are kept under it.</param>
        /// <param name="zones">A prime-time evening over several zones (M11.3): <paramref name="map"/>, where it begins, and these others. Null: that one zone.</param>
        public static object Start(int teamsPerRealm, string owner, GamePlayer leader = null, PvpGroupTemplate own = null, PvpRoamMap map = null,
            IReadOnlyList<PvpGroupTemplate> lineups = null, IReadOnlyList<PvpGuildTeam> guilds = null, string pool = null, IReadOnlyList<PvpRoamMap> zones = null)
        {
            map ??= Emain;
            if (PvpScrim.Running)
                throw new InvalidOperationException("A scrim or a session is running. Stop it first.");
            PvpArena.Clear();
            PvpWeights.Reload(AppContext.BaseDirectory);
            _map = map;
            List<PvpRoamMap> maps = guilds == null || zones == null ? [map] : zones.Prepend(map).Distinct().ToList();
            _maps = maps;
            _travelling = 0;
            foreach (PvpRoamMap zone in maps)
            {
                int cleared = PvpArena.ClearMonsters(zone.Region, zone.Starts.Select(start => start.Point));
                if (cleared > 0)
                    Log.Info($"8v8 roam: {cleared} monsters taken out of {zone.Name}");
            }
            var teams = new List<PvpRoamTeam>();
            long now = GameLoop.GameLoopTime;
            PvpRealWeights real = PvpWeights.Current.Real;
            // The player's own team first: the others are made no lower in rank than it is (PvpRealWeights.EnemyKeepsUp).
            foreach (PvpRoamStart start in map.Starts.OrderBy(entry => leader != null && entry.Realm == leader.Realm ? 0 : 1))
            {
                PvpGroupTemplate template = lineups?.FirstOrDefault(group => group.Realm == start.Realm) ?? PvpBuildCatalog.Groups.First(group => group.Realm == start.Realm);
                // Who starts here: the realm's main group once or twice; or, on a prime-time evening, the player's group and the guilds that are out.
                var entries = new List<(string Name, PvpGroupTemplate Template, bool Led, PvpGuildTeam Guild)>();
                if (guilds == null)
                {
                    for (int number = 1; number <= Math.Clamp(teamsPerRealm, 1, 2); number++)
                    {
                        bool first = leader != null && start.Realm == leader.Realm && number == 1;
                        entries.Add((TeamName(template.Id, number), first && own != null ? own : template, first, null));
                    }
                }
                else
                {
                    if (leader != null && start.Realm == leader.Realm)
                        entries.Add((TeamName(template.Id, 1), own ?? template, true, null));
                    entries.AddRange(guilds.Where(guild => guild.ToRealm() == start.Realm).Select(guild => (guild.Guild, guild.ToTemplate(), false, guild)));
                }
                for (int index = 0; index < entries.Count; index++)
                {
                    (string name, PvpGroupTemplate lineup, bool led, PvpGuildTeam guild) = entries[index];
                    var team = new PvpRoamTeam
                    {
                        Name = name, Template = lineup, Realm = start.Realm, Start = start, Map = map, Guild = guild, Profile = guild?.ToProfile(),
                        // The player's companions are kept under the player; every other team under its own name; a guild under its pool.
                        Roster = led ? owner : guild != null ? PvpRealmPoints.EnemyRoster(pool ?? PvpTeamPool.World, guild.Id) : PvpRealmPoints.EnemyRoster(owner, name),
                        // A realm's teams of bots set off one after the other; the player's own goes when the player does.
                        LeavesAt = now + 1000L * PvpRealWeights.SetsOffAfterSeconds(teams.Count(other => other.Realm == start.Realm && other.Leader == null), real),
                        SpawnAt = guilds == null ? start.Point : SpawnPoint(start.Point, index), Index = index,
                        // A guild goes from point to point as it pleases, where the map says which legs can be walked.
                        Entry = guild != null && map.Ways != null ? start.RouteOf(index % 2 + 1)[0] : null,
                    };
                    team.Team.Leader = led ? leader : null;
                    team.Team.Route = team.Entry is { } entry ? [entry] : start.RouteOf(index + 1);
                    // On a prime-time evening a team has the rank of its kind and of what it has earned, whatever the player's is.
                    _floor = guilds != null || leader == null || teams.Count == 0 ? 1 : PvpRealmPoints.EnemyLevel(1,
                        teams[0].Team.Bots.Select(bot => bot.RealmLevel).Append(leader.RealmLevel).ToList(), PvpWeights.Current.Real.EnemyKeepsUp);
                    // A guild comes out when its time has come (the first of a realm with the first tick); its eight are made then,
                    // in the zone it has been put in by then.
                    int before = teams.Count(other => other.Realm == start.Realm && other.Leader == null);
                    if (guild != null)
                    {
                        (team.State, team.ComesOutAt, team.StateSince) = ("Away", now + 1000L * ComesOutAfterSeconds(before, map.Starts.ToList().IndexOf(start), real), now);
                        team.Team.Bots = [];
                    }
                    else
                        Spawn(team, now);
                    if (guild != null)
                        team.LogsOffAt = Math.Max(now, team.ComesOutAt) + 1000L * EveningSeconds(Random.Shared.Next());
                    teams.Add(team);
                }
            }
            // The packs and the followers of the evening: the bad groups by two to five behind one of them, and each
            // group that stays near others beside a group of its realm that walks alone (the player's may be it).
            foreach (IGrouping<eRealm, PvpRoamTeam> realm in teams.Where(team => team.Profile != null).GroupBy(team => team.Realm).ToList())
            {
                // (A pack comes out together, three seconds apart.)
                var places = new Dictionary<PvpRoamTeam, int>();
                foreach ((PvpRoamTeam follower, PvpRoamTeam head) in PvpTeamPool.Packs(realm.Where(team => team.Profile.Company == PvpCompany.Zerg).ToList(), Random.Shared))
                {
                    places[head] = places.GetValueOrDefault(head) + 1;
                    follower.Company = head;
                    if (follower.State == "Away")
                        follower.ComesOutAt = (head.State == "Away" ? head.ComesOutAt : now) + 3_000L * places[head];
                }
                List<PvpRoamTeam> hosts = teams.Where(team => team.Realm == realm.Key &&
                    (team.Leader != null || team.Profile is { Company: PvpCompany.Alone, KeepsToQuietGround: false })).ToList();
                foreach (PvpRoamTeam near in realm.Where(team => team.Profile.Company == PvpCompany.Near && hosts.Count > 0))
                    near.Company = hosts[Random.Shared.Next(hosts.Count)];
                // The zones of the evening: those that walk alone and the heads of packs take them in turn, beginning
                // where the evening began; the others go where the team they walk with goes.
                var placed = teams.Where(team => team.Leader != null).ToList();
                int turn = 0;
                foreach (PvpRoamTeam team in realm.Where(team => team.Company == null).Concat(realm.Where(team => team.Company != null)))
                {
                    Place(team, team.Company?.Map ?? maps[ZoneInTurn(turn++, maps.Count)], placed);
                    placed.Add(team);
                }
            }
            _pool = guilds != null ? pool ?? PvpTeamPool.World : null;
            _teams = teams;
            _started = now;
            // One recording for the session (M9.7 step 4): every team under its own name.
            _recording = PvpRecorder.BeginRoam((guilds != null ? "prime" : "roam") + (leader != null ? "-play" : string.Empty) +
                                               (maps.Count > 1 ? "-" + maps.Count + "zones" : map == Emain ? string.Empty : "-" + map.Id),
                teams.Select(team => (team.Name, team.Template.Id, team.Realm, team.Team.Members)).ToList(),
                teams.Where(team => team.Profile != null).ToDictionary(team => team.Name, team => team.Profile.Kind.ToString()));
            foreach (PvpRoamTeam team in teams.Where(team => team.Company != null))
                Say($"{team.Name} walks with {team.Company.Name}", new { what = "company", team = team.Name, beside = team.Company.Name });
            lock (Events) Events.Clear();
            CalledOut.Clear();
            _lastCall = 0;
            Gone.Clear();
            _timer?.Dispose();
            _timer = new Timer(_ => NpcService.Instance.Post(Tick, 0), null, 1_000, 1_000);
            return Status();
        }

        public static void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            List<PvpRoamTeam> teams = _teams;
            _teams = [];
            // A test run may have switched the effect fault back on (roam-start StaleCopies=off): never past its own roam.
            EffectListComponent.RetireStaleCopies = true;
            PvpRecording recording = Interlocked.Exchange(ref _recording, null);
            recording?.Finish(new
            {
                k = "end", t = recording.Now, Seconds = (int)((GameLoop.GameLoopTime - _started) / 1000),
                teams = teams.Select(team => new { name = team.Name, team.Fights, team.Kills, team.Deaths, team.Wipes, team.Joined, team.LeftAlone, team.Earned }),
            });
            foreach (PvpRoamTeam team in teams)
                PvpArena.Remove(team.All);
            // What each guild did this evening goes on its record.
            if (Interlocked.Exchange(ref _pool, null) is { } pool)
                PvpTeamPool.Record(AppContext.BaseDirectory, pool, teams.Concat(Gone).Where(team => team.Guild != null && team.ComesOutAt <= GameLoop.GameLoopTime)
                    .GroupBy(team => team.Guild.Id).ToDictionary(same => same.Key, same => new PvpEvening(same.First().Fights, same.First().Kills,
                        same.First().Deaths, same.First().Wipes, same.First().WipedByPlayer, same.First().WipedPlayer)));
            foreach (PvpRoamTeam team in Gone)
                PvpArena.Remove(team.All);
            Gone.Clear();
        }

        /// <summary>A team of bots comes into the world near its start: a guild with the realm points of its kind and its name under each member.</summary>
        private static List<GameBot> SpawnBots(PvpRoamTeam team)
        {
            long seed = team.Guild == null ? 0 : PvpRealmPoints.PointsFor(team.Guild.SeedLevel);
            try
            {
                return PvpArena.SpawnGroup(team.Template, team.Map.Region, team.SpawnAt.X, team.SpawnAt.Y, 0, team.All, team.Roster, _floor, seed, team.Guild?.Guild).ToList();
            }
            catch (InvalidOperationException) when (team.SpawnAt != team.Start.Point)
            {
                // No ground to stand on beside the start: at the start itself, then.
                PvpArena.Remove(team.All);
                team.All.Clear();
                team.SpawnAt = team.Start.Point;
                return PvpArena.SpawnGroup(team.Template, team.Map.Region, team.SpawnAt.X, team.SpawnAt.Y, 0, team.All, team.Roster, _floor, seed, team.Guild?.Guild).ToList();
            }
        }

        private static void Spawn(PvpRoamTeam team, long now)
        {
            PvpArena.Remove(team.All);
            team.All.Clear();
            GamePlayer leader = team.Leader;
            // The player's seven are put around the player, who stands at the start; a team of bots around its start.
            team.Team.Bots = leader != null
                ? PvpArena.SpawnCompanions(leader, 0, team.All, team.Template, true).ToList()
                : SpawnBots(team);
            // A guild plays at the level of its kind (PvpSkills), not at the one chosen for "the others".
            if (team.Profile is { } profile)
            {
                foreach (GameBot bot in team.Team.Bots)
                    bot.PvpHuman = PvpSkills.WeightsFor(profile.Skill);
            }
            if (team.Entry is { } entry)
                (team.Team.Route, team.CameFrom, team.ArrivedAt) = ([entry], null, 0);
            team.Team.Home = team.Start.Point;
            team.Team.Waypoint = 0;
            team.Team.NewMatch(leader);
            team.Team.Squad = new PvpSquad(team.Team.Bots, leader != null ? [leader] : null);
            foreach (GameBot bot in team.Team.Bots)
            {
                if (bot.Brain is BotBrain brain)
                    brain.PvpSelfBuffsOnly = true;
            }
            team.Paid.Clear();
            team.Fighting.Clear();
            team.Draws.Clear();
            team.Waiting = false;
            (team.Hide, team.Rested) = (null, false);
            (team.DeadSince, team.Reforming) = (0, false);
            (team.State, team.StateSince, team.ContactTick, team.LastEnemyNearTick) = (leader != null ? "Roaming" : "Buffing", now, 0, now);
        }

        /// <summary>
        /// A wiped team stands up at its start: bots as a new group of the same eight; a player
        /// alive again there, without sickness, with the seven around.
        /// </summary>
        private static void StandUp(PvpRoamTeam team, long now)
        {
            if (team.Leader is { } leader)
            {
                Point3D spot = StartSpot(team.Map, team.Realm);
                if (!leader.IsAlive)
                {
                    leader.TempProperties.SetProperty(GamePlayer.RESURRECT_REZ_SICK_EFFECTIVENESS, 0.0);
                    leader.StopReleaseTimer();
                    leader.Notify(GamePlayerEvent.Revive, leader, null);
                }
                (leader.Health, leader.Mana, leader.Endurance) = (leader.MaxHealth, leader.MaxMana, leader.MaxEndurance);
                // A player who released is there already.
                if (!leader.IsWithinRadius(spot, 1_500))
                    leader.MoveTo(team.Map.Region, spot.X, spot.Y, spot.Z, leader.Heading);
                Tell(team, "Your group stands up at its start.");
            }
            Spawn(team, now);
            team.LeavesAt = now + 1000L * PvpWeights.Current.Real.BuffsAtStartSeconds;
            PvpRecorder.Regroup(_recording, team.Name, team.Team.Members);
        }

        private static void Tell(PvpRoamTeam team, string what)
        {
            if (team.Leader?.Client?.IsPlaying == true)
                team.Leader.Out.SendMessage("[8v8] " + what, PacketHandler.eChatType.CT_Important, PacketHandler.eChatLoc.CL_SystemWindow);
        }

        /// <summary>For the log and the status; <paramref name="row"/>, when given, is what the recording keeps of it.</summary>
        private static void Say(string what, object row = null)
        {
            Log.Info("8v8 roam: " + what);
            if (_recording is { } recording)
                recording.Line(new { k = "ev", t = recording.Now, text = what, ev = row });
            lock (Events)
            {
                Events.Add($"{(GameLoop.GameLoopTime - _started) / 1000,5}s {what}");
                if (Events.Count > 400)
                    Events.RemoveAt(0);
            }
        }

        private static void Tick(int _)
        {
            if (Interlocked.Exchange(ref _ticking, 1) != 0)
                return;
            try
            {
                List<PvpRoamTeam> teams = _teams;
                long now = GameLoop.GameLoopTime;
                PvpRealWeights real = PvpWeights.Current.Real;
                // (On its way to another zone the player is not in the world for a moment: that is not leaving. Two minutes at most.)
                bool travelling = _travelling != 0 && now - _travelling < 120_000;
                // The player has left the game: the roam is over.
                if (!travelling && teams.Any(team => team.Leader is { } leader && (leader.ObjectState != GameObject.eObjectState.Active || leader.Client?.IsPlaying != true)))
                {
                    Stop();
                    return;
                }
                foreach (PvpRoamTeam team in teams)
                {
                    if (travelling && team.Leader != null)
                        continue;
                    Step(team, teams, now, real);
                }
                CallOut(teams, now);
                if (Prime)
                    Rotate(teams, now);
                // A monster that was dead when the roam began has come back: once a minute.
                if ((now - _started) / 1000 % 60 == 59)
                {
                    foreach (PvpRoamMap zone in _maps)
                        PvpArena.ClearMonsters(zone.Region, zone.Starts.Select(start => start.Point));
                }
                if (_recording is { } recording)
                {
                    foreach (PvpRoamTeam team in teams)
                    {
                        if (team.Quiet && (now - _started) / 1000 % 5 != 0)
                            continue;
                        (double x, double y) = team.Centre();
                        recording.Line(new
                        {
                            k = "team", t = recording.Now, name = team.Name, state = team.State, x = (int)x, y = (int)y, alive = team.Alive,
                            wp = team.Team.Waypoint, fighting = team.Fighting.Select(other => other.Name).ToList(),
                        });
                    }
                    recording.Snapshot(name => teams.FirstOrDefault(team => team.Name == name)?.Quiet != false);
                }
            }
            catch (Exception exception)
            {
                _lastError = exception.ToString();
                Log.Error("8v8 roam tick failed", exception);
                Stop();
            }
            finally
            {
                Volatile.Write(ref _ticking, 0);
            }
        }

        /// <summary>True while a living team of another realm is within this of the team's start point.</summary>
        private static bool EnemyNearStart(PvpRoamTeam team, List<PvpRoamTeam> teams, int within) =>
            teams.Any(other => other.Realm != team.Realm && other.Map == team.Map && other.Team.Members.Any(member => member.IsAlive &&
                member.IsWithinRadius(new Point2D(team.Start.Point.X, team.Start.Point.Y), within)));

        /// <summary>
        /// Two teams are in a fight with each other when somebody of one is in combat and aimed
        /// at somebody of the other. Until 2026-11-21 it was enough for one of them to be in
        /// combat, with anybody, and the other to be within reach: a team standing by a fight was
        /// counted in it and paid for its deaths (the owner: "I think I am also gaining rps for
        /// just being close to the fight").
        /// </summary>
        private static bool AtEachOther(PvpRoamTeam a, PvpRoamTeam b)
        {
            IReadOnlyList<GameLiving> ours = a.Team.Members, theirs = b.Team.Members;
            return ours.Any(member => IsAt(member.IsAlive, member.InCombat, member.TargetObject is GameLiving aimed && theirs.Contains(aimed))) ||
                   theirs.Any(member => IsAt(member.IsAlive, member.InCombat, member.TargetObject is GameLiving aimed && ours.Contains(aimed)));
        }

        /// <summary>Whether a member is fighting the other team: alive, in combat, and its target one of theirs.</summary>
        public static bool IsAt(bool alive, bool inCombat, bool aimedAtOneOfTheirs) => alive && inCombat && aimedAtOneOfTheirs;

        private static bool Near(PvpRoamTeam a, PvpRoamTeam b, int within)
        {
            // Two zones are two worlds: nobody is near anybody in another.
            if (a.Map != b.Map)
                return false;
            (double ax, double ay) = a.Centre();
            (double bx, double by) = b.Centre();
            return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)) <= within;
        }

        private static void Step(PvpRoamTeam team, List<PvpRoamTeam> teams, long now, PvpRealWeights real)
        {
            // Not out yet: its eight come into the world when its time has come, and buff.
            if (team.State == "Away")
            {
                team.Quiet = true;
                if (now < team.ComesOutAt)
                    return;
                Spawn(team, now);
                team.LeavesAt = now + 1000L * real.BuffsAtStartSeconds;
                PvpRecorder.Regroup(_recording, team.Name, team.Team.Members);
                Say($"{team.Name} is out", new { what = "out", team = team.Name });
                return;
            }
            // Nobody left: it stands up at its start point after a wait, as a new group of the same eight.
            if (team.Reforming || IsWiped(team.Team.Bots.Count(bot => bot.IsAlive), team.Leader != null, team.Leader?.IsAlive == true,
                    team.Leader?.IsWithinRadius(new Point2D(team.Start.Point.X, team.Start.Point.Y), real.StartIsSafeWithin) == true))
            {
                if (team.State != "Down" && team.Reforming)
                {
                    // Gone from the field at once (nobody is near to see it), and back at its start after the wait of a wipe.
                    (team.State, team.StateSince) = ("Down", now);
                    team.Fighting.Clear();
                    PvpArena.Remove(team.All);
                }
                else if (team.State != "Down")
                {
                    (team.State, team.StateSince) = ("Down", now);
                    team.Wipes++;
                    // Its last to fall are paid for before the fight is forgotten.
                    Pay(team, real, now);
                    // Who wiped whom, for the guilds' records against the player and for their grudges.
                    foreach (PvpRoamTeam winner in team.Fighting)
                    {
                        if (team.Leader != null)
                            winner.WipedPlayer++;
                        else if (winner.Leader != null)
                            team.WipedByPlayer++;
                    }
                    // On a prime-time evening the realm hears of it.
                    if (Prime && team.Leader == null && team.Realm == Hears(teams) && team.Fighting.Count > 0)
                    {
                        IGrouping<eRealm, PvpRoamTeam> most = team.Fighting.GroupBy(other => other.Realm).OrderByDescending(realm => realm.Count()).First();
                        (double x, double y) = team.Centre();
                        RealmChat(teams, team, SightingText(most.Count(), most.Key, InZone(team.Map, team.Map.PlaceOf(x, y)), false, true));
                    }
                    team.Fighting.Clear();
                    Say($"{team.Name} is wiped (its {team.Wipes}.)", new { what = "wipe", team = team.Name });
                    Tell(team, team.Leader?.IsAlive == true
                        ? $"Your group is down. It stands up here in about {real.WipeStandsUpAfterSeconds} seconds."
                        : $"Your group is wiped. You stand up at your start in about {real.WipeStandsUpAfterSeconds} seconds, or release to go there now.");
                }
                else if (PvpRealWeights.StandsUp((now - team.StateSince) / 1000.0, EnemyNearStart(team, teams, real.StartIsClearBeyond), real,
                             team.Leader is { IsAlive: true }))
                {
                    if (team.Guild != null && PortsElsewhere(_maps.Count, team.Company != null, teams.Any(other => other.Company == team), Random.Shared.Next(300)))
                    {
                        List<PvpRoamMap> elsewhere = _maps.Where(zone => zone != team.Map).ToList();
                        Place(team, elsewhere[Random.Shared.Next(elsewhere.Count)], teams);
                        Say($"{team.Name} ports to {team.Map.Name}", new { what = "port", team = team.Name, zone = team.Map.Name });
                    }
                    StandUp(team, now);
                    Say($"{team.Name} stands up at its start", new { what = "standup", team = team.Name });
                }
                team.Quiet = true;
                return;
            }

            // The teams of another realm that are near, and which of them it is fighting.
            List<PvpRoamTeam> others = teams.Where(other => other.Realm != team.Realm && other.Alive > 0 && !other.Reforming && Near(team, other, 9_000)).ToList();
            team.Quiet = others.Count == 0 && team.ContactTick == 0;
            bool inCombat = team.Team.Members.Any(member => member.IsAlive && member.InCombat);
            team.Fighting.RemoveWhere(other => other.Alive == 0 || !Near(team, other, 6_000));
            foreach (PvpRoamTeam other in others)
            {
                // This enemy team is already in a fight, with this team's realm-mate or with a team of the
                // third realm: this team joins, or it leaves them to it and waits. One draw for the fight.
                List<PvpRoamTeam> theirFight = teams.Where(third => third != team && third != other && third.Alive > 0 &&
                    (other.Fighting.Contains(third) || third.Fighting.Contains(other))).ToList();
                bool onAFight = ComesOnAFight(theirFight.Count > 0, theirFight.Any(team.Fighting.Contains), team.Fighting.Contains(other));
                // (The player's own team does what the player does.)
                // (A team that is in a fight of its own has no eyes for another: it draws when it is free.)
                if (onAFight && team.Leader == null && team.Fighting.Count == 0 && !team.Draws.ContainsKey(other))
                {
                    // What it drew for another team of the same fight holds for this one.
                    bool? drawn = team.Draws.Where(draw => theirFight.Contains(draw.Key)).Select(draw => (bool?)draw.Value).FirstOrDefault();
                    // (A guild adds as its kind does: a bad group always, a decent one never.)
                    bool joins = drawn ?? (PaysBack(team.AddedOnByPlayer, other.Leader != null || theirFight.Any(third => third.Leader != null)) ||
                                           Joins(team.Profile?.AddChancePercent ?? real.AddChancePercent, Random.Shared.Next(100)));
                    team.Draws[other] = joins;
                    if (drawn == null)
                    {
                        // A guild on its own that leaves a fight alone does not stand and watch: it turns back the way it came.
                        if (!joins && TurnsAway(team))
                            Say($"{team.Name} turns away", new { what = "turn", team = team.Name });
                        if (joins) team.Joined++; else team.LeftAlone++;
                        PvpRoamTeam mate = theirFight.FirstOrDefault(third => third.Realm == team.Realm);
                        Say(mate != null
                                ? $"{team.Name} comes on {mate.Name} fighting {other.Name}: it {(joins ? "joins" : "leaves them to it")}"
                                : $"{team.Name} comes on {other.Name} fighting {string.Join(" and ", theirFight.Select(third => third.Name))}: it {(joins ? "joins" : "waits for it to end")}",
                            new { what = "draw", team = team.Name, mate = mate?.Name, other = other.Name, joins });
                    }
                }
                else if (theirFight.Count == 0)
                    team.Draws.Remove(other);
            }
            // It left a fair fight to its realm-mate; the other side has added, so it does too.
            // (Not a guild: one that does not add does not add, and with packs about, somebody always has two on it.)
            if (team.Leader == null && team.Profile == null && teams.FirstOrDefault(own => own != team && own.Realm == team.Realm && own.Alive > 0 &&
                    EvensTheOdds(MostOfOneRealm(own.Fighting), own.Draws.Any(draw => draw.Value && own.Fighting.Contains(draw.Key))) && own.Fighting.Any(enemy => team.Draws.TryGetValue(enemy, out bool joined) && !joined)) is { } pressed)
            {
                foreach (PvpRoamTeam enemy in pressed.Fighting)
                    team.Draws[enemy] = true;
                team.Joined++;
                Say($"{team.Name} joins {pressed.Name} after all: two teams of one realm are on it",
                    new { what = "evens", team = team.Name, mate = pressed.Name, others = pressed.Fighting.Select(enemy => enemy.Name).ToList() });
            }
            // Left alone, unless they come for it: by its draw, or because they stand at their own start.
            // (A player who only looks at one of them is not attacking: a player counts once it is in combat.)
            bool Attacks(PvpRoamTeam other) => other.Team.Members.Any(member => member.IsAlive && member.TargetObject is GameLiving aimed &&
                (member is not GamePlayer || member.InCombat) &&
                team.Team.Members.Contains(aimed) && member.IsWithinRadius(aimed, 2_000));
            double FromItsStart(PvpRoamTeam other)
            {
                (double x, double y) = other.Centre();
                return Math.Sqrt(Math.Pow(x - other.Start.Point.X, 2) + Math.Pow(y - other.Start.Point.Y, 2));
            }
            List<PvpRoamTeam> foes = others.Where(other =>
            {
                bool attacks = Attacks(other);
                if (LeavesAlone(team.Draws.TryGetValue(other, out bool joins) && !joins, other.Draws.TryGetValue(team, out bool theyJoin) && !theyJoin, attacks,
                        team.Leader?.TargetObject is GameLiving aimed && other.Team.Members.Contains(aimed)))
                    return false;
                // Already in a fight, a team of bots lets anybody else walk by.
                if (team.Leader == null && TooBusyFor(team.Fighting.Count, team.Fighting.Contains(other), attacks))
                    return false;
                // It does not start on more groups than it has together itself: eight do not walk into sixteen, nor sixteen
                // into twenty-four. (A bad group does not count: PvpTeamProfile.CountsHeads.)
                int theirs = Together(other, teams);
                if (team.Leader == null && team.Profile?.CountsHeads != false &&
                    AvoidsTwo(theirs >= 2, Together(team, teams) >= theirs, team.Fighting.Contains(other), attacks))
                    return false;
                if (team.Leader is { } leader)
                    return PlayerTakesItOn(team.Fighting.Contains(other), attacks,
                        leader.TargetObject is GameLiving picked && picked.IsAlive && other.Team.Members.Contains(picked) &&
                        leader.IsWithinRadius(picked, PvpFormation.FormRange));
                return !PvpRealWeights.LeftAtItsStart(FromItsStart(other), team.Fighting.Contains(other), attacks, real);
            }).ToList();
            List<GameLiving> enemies = foes.SelectMany(other => other.Team.Members).Where(member => member.IsAlive).ToList();
            foreach (PvpRoamTeam other in foes)
            {
                if (AtEachOther(team, other) && Near(team, other, PvpScrim.EngageRange) && team.Fighting.Add(other))
                {
                    team.Fights++;
                    if (team.Guild != null && !team.AddedOnByPlayer &&
                        WasAddedOn(other.Leader != null, team.Fighting.Count(third => third != other && third.Alive > 0), team.Draws.Any(draw => draw.Value)))
                    {
                        team.AddedOnByPlayer = true;
                        Say($"{team.Name} was added on by {other.Name}, and remembers", new { what = "addedon", team = team.Name, by = other.Name });
                    }
                    Say($"{team.Name} ({team.Alive}) fights {other.Name} ({other.Alive})",
                        new { what = "fight", team = team.Name, other = other.Name, alive = team.Alive, theirs = other.Alive });
                }
            }

            Pay(team, real, now);

            if (enemies.Count > 0)
                team.LastEnemyNearTick = now;
            if (team.ContactTick == 0 && inCombat && enemies.Count > 0)
            {
                team.ContactTick = now;
                team.Rested = false;
                team.State = "Fighting";
                team.Team.Squad.FightStarted = true;
                foreach (GameBot bot in team.Team.Bots)
                {
                    if (bot.Brain is BotBrain brain)
                        brain.PvpSelfBuffsOnly = false;
                }
            }
            // The fight is over when no enemy has been near for a while: a fresh squad for the next one.
            else if (team.ContactTick != 0 && !inCombat && FightIsOver(now - team.LastEnemyNearTick))
            {
                team.ContactTick = 0;
                team.State = "Roaming";
                team.Fighting.Clear();
                team.Team.Squad = new PvpSquad(team.Team.Bots, team.Leader != null ? [team.Leader] : null);
                team.Team.NewMatch(team.Leader);
            }

            if (team.State == "Buffing")
            {
                // A member that has buffed itself dry rests and goes on, as before a scrim.
                foreach (GameBot bot in team.Team.Bots)
                {
                    if (bot.IsAlive && !bot.IsCasting && bot.MaxMana > 0 && bot.ManaPercent < 40)
                        bot.Mana = bot.MaxMana;
                }
                if (now >= team.LeavesAt)
                {
                    foreach (GameBot bot in team.Team.Bots.Where(bot => bot.IsAlive))
                        (bot.Health, bot.Mana, bot.Endurance) = (bot.MaxHealth, bot.MaxMana, bot.MaxEndurance);
                    team.State = "Roaming";
                    Say($"{team.Name} sets off", new { what = "setoff", team = team.Name });
                }
            }

            if (enemies.Count > 0)
            {
                team.Team.Squad.Decide(enemies);
                team.Team.Engage(enemies);
            }
            // A lull is not the end of a fight: with nobody to reckon with for a moment (the player between two
            // targets, the enemy dead or asleep) the group's Bard went to its speed song and back, twice in the
            // owner's fight of 2026-11-21, each change a three-second cast and twenty seconds without the other song.
            else if (team.Leader != null && FightIsOver(now - team.LastEnemyNearTick))
                team.Team.Squad.LoseSight();
            // "/8v8 call retreat": the player's group fights nobody and runs with the player.
            if (team.Leader != null && team.Team.Squad.RetreatsTo != null)
            {
                team.Team.TravelWithLeader([]);
                return;
            }
            // After a fight a team of bots rests when somebody is low, before it walks on (PvpRealWeights.TeamRestsBelowPercent).
            // It first walks off its road to a place out of the way, where the ground has one (PvpRealWeights.RestsOffTheRoadBy).
            bool resting = false;
            if (team.Leader == null)
            {
                bool enemyNear = others.Any(other => Near(team, other, real.RestEndsWithEnemyWithin));
                if (inCombat || enemyNear || team.State != "Roaming")
                {
                    // Found, or back in a fight: it gets up and gives the place up.
                    if (team.Hide != null || team.Team.Squad.Resting)
                    {
                        team.Team.Squad.EndRest();
                        team.Hide = null;
                    }
                }
                else if (team.Hide is { } place)
                {
                    (double cx, double cy) = team.Centre();
                    bool there = Math.Sqrt(Math.Pow(cx - place.X, 2) + Math.Pow(cy - place.Y, 2)) < 700;
                    if (there || now - team.HideSince > 45_000)
                    {
                        team.Hide = null;
                        team.Team.Squad.RestAsATeam(real.TeamRestsAtMostSeconds);
                        Say($"{team.Name} rests{(there ? " off its road" : " where it has got to")}", new { what = "rest", team = team.Name, there });
                    }
                }
                else if (!team.Rested && enemies.Count == 0 && others.Count == 0)
                {
                    List<GameBot> up = team.Team.Bots.Where(bot => bot.IsAlive).ToList();
                    int lowest = up.Count == 0 ? 100 : up.Min(bot => Math.Min((int)bot.HealthPercent, bot.MaxMana > 0 ? (int)bot.ManaPercent : 100));
                    if (PvpRealWeights.TeamRests(lowest, real))
                    {
                        team.Rested = true;
                        team.Hide = PickHide(team, real);
                        team.HideSince = now;
                        if (team.Hide == null)
                        {
                            team.Team.Squad.RestAsATeam(real.TeamRestsAtMostSeconds);
                            Say($"{team.Name} rests where it is (its lowest member at {lowest}%)", new { what = "rest", team = team.Name, lowest });
                        }
                        else
                            Say($"{team.Name} walks off its road to rest (its lowest member at {lowest}%)", new { what = "hide", team = team.Name, lowest });
                    }
                }
                resting = team.Hide == null && team.Team.Squad.Resting;
            }
            // After a fight a team of bots sees to its dead before it walks on: it raises them, or goes back to its start.
            GameBot body = null;
            if (team.Leader == null && !inCombat && enemies.Count == 0 && team.Team.Bots.Any(bot => !bot.IsAlive))
            {
                if (team.DeadSince == 0)
                    team.DeadSince = now;
                List<GameBot> living = team.Team.Bots.Where(bot => bot.IsAlive).ToList();
                bool canRaise = living.Any(bot => bot.ResurrectionSpell != null);
                if (PvpRealWeights.Reforms(canRaise, (now - team.DeadSince) / 1000.0, real))
                {
                    team.Reforming = true;
                    Say($"{team.Name} goes back to its start to form up again ({team.Team.Bots.Count - living.Count} dead, {(canRaise ? "not raised in time" : "nobody to raise them")})",
                        new { what = "reform", team = team.Name, dead = team.Team.Bots.Count - living.Count, canRaise });
                    return;
                }
                (double x, double y) = team.Centre();
                body = team.Team.Bots.Where(bot => !bot.IsAlive).OrderBy(bot => (bot.X - x) * (bot.X - x) + (bot.Y - y) * (bot.Y - y)).First();
            }
            else
                team.DeadSince = 0;
            // The player's seven go where the player goes; a team of bots walks its route, or waits out a fight it has left alone.
            // (Once it waits it goes on waiting while that fight is within 12,000: at 9,000 it would walk and stop by turns.)
            // (A guild that goes from point to point does not wait: it has turned back, or it walks with its company, or, before
            // its first point, it walks on past. In an evening of 24 teams, 2026-11-22, three Albion teams that do not add stood
            // for minutes before the south end of the road, their only way in, where a fight never ended.)
            bool waits = WaitsOutAFight(team.Draws.Any(draw => !draw.Value && draw.Key.Alive > 0 && Near(team, draw.Key, team.Waiting ? 12_000 : 9_000)),
                enemies.Count, team.Leader != null || team.Entry != null);
            if (waits != team.Waiting)
            {
                team.Waiting = waits;
                if (waits)
                    Say($"{team.Name} waits where it is", new { what = "wait", team = team.Name });
            }
            if (team.Leader != null)
                team.Team.TravelWithLeader(enemies);
            else if (body != null)
                team.Team.GatherAt(body, 500);
            else if (team.Hide is { } hide && enemies.Count == 0)
                team.Team.GatherAt(hide, 500);
            else if (resting)
                team.Team.Halt();
            else if (waits)
                team.Team.Halt();
            else if (team.State != "Buffing")
            {
                // A prime-time evening: it walks with its company while that is about, and otherwise from point to point as it pleases.
                PvpRoamTeam lead = team.Company is { Reforming: false, State: "Roaming" or "Fighting" } company && company.Alive > 0 ? company : null;
                if (lead?.Team.Route != null)
                    (team.Team.Route, team.Team.Waypoint) = (lead.Team.Route, lead.Team.Waypoint);
                else if (lead == null && team.Entry != null && enemies.Count == 0 && Wander(team, teams, now))
                {
                    team.Team.Halt();
                    return;
                }
                if (lead != null && enemies.Count == 0 && !Near(team, lead, KeepsWithin(team.Profile.Company)) &&
                    lead.Team.Members.FirstOrDefault(member => member.IsAlive) is { } anchor)
                    team.Team.GatherAt(anchor, 600);
                // Near enough to the group it stays near: it lets that one go ahead.
                else if (lead != null && team.Profile.Company == PvpCompany.Near && enemies.Count == 0 && Near(team, lead, TrailsBehind))
                    team.Team.Halt();
                // Beside the player's group, which has no road: it stands until that has moved on.
                else if (lead != null && lead.Team.Route == null && enemies.Count == 0)
                    team.Team.Halt();
                else
                    team.Team.March(enemies, team.Start.Point, null, null, team.ContactTick);
            }
            if (enemies.Count > 0)
            {
                team.Team.Squad.ClearPets();
                team.Team.Squad.StandOff();
                team.Team.Squad.KeepApart();
            }
        }

        /// <summary>The groups of a realm that a group which stays near others may pick: the player's, and the guilds that walk alone on the open ground.</summary>
        private static List<PvpRoamTeam> Hosts(IEnumerable<PvpRoamTeam> teams, eRealm realm) =>
            teams.Where(team => team.Realm == realm && (team.Leader != null || team.Profile is { Company: PvpCompany.Alone, KeepsToQuietGround: false })).ToList();

        /// <summary>One guild whose evening is over logs off (<see cref="LogsOff"/>), and another of its realm comes out in its place within a minute.</summary>
        private static void Rotate(List<PvpRoamTeam> teams, long now)
        {
            PvpRoamTeam leaving = teams.FirstOrDefault(team => team.Guild != null && team.State != "Away" &&
                LogsOff(team.Wipes, team.LogsOffAt != 0 && now >= team.LogsOffAt, team.State == "Down",
                    team.State == "Roaming" && team.Quiet && team.Fighting.Count == 0 && team.ContactTick == 0));
            if (leaving == null)
                return;
            PvpArena.Remove(leaving.All);
            leaving.All.Clear();
            PvpRecorder.Regroup(_recording, leaving.Name, []);
            Gone.Add(leaving);
            List<PvpRoamTeam> next = teams.Where(team => team != leaving).ToList();
            foreach (PvpRoamTeam other in next)
            {
                other.Fighting.Remove(leaving);
                other.Draws.Remove(leaving);
            }
            Say($"{leaving.Name} logs off ({(leaving.Wipes >= EnoughWipes ? $"wiped {leaving.Wipes} times" : "its evening is over")})",
                new { what = "logoff", team = leaving.Name, wipes = leaving.Wipes });
            // Who walked with it: a pack goes on behind the first of its others, a group that stayed near it picks another.
            List<PvpRoamTeam> followers = next.Where(team => team.Company == leaving).ToList();
            PvpRoamTeam heir = followers.FirstOrDefault(team => team.Profile?.Company == PvpCompany.Zerg);
            foreach (PvpRoamTeam follower in followers)
            {
                List<PvpRoamTeam> hosts = Hosts(next, follower.Realm).Where(host => host.Map == follower.Map).ToList();
                follower.Company = follower == heir ? null
                    : follower.Profile?.Company == PvpCompany.Zerg ? heir
                    : hosts.Count > 0 ? hosts[Random.Shared.Next(hosts.Count)] : null;
            }
            // Somebody else of its realm comes out, at its place at the start.
            var taken = next.Concat(Gone).Where(team => team.Guild != null).Select(team => team.Guild.Id).ToHashSet();
            string pool = _pool;
            if (pool != null && PvpTeamPool.NextOut(PvpTeamPool.Load(AppContext.BaseDirectory, pool), leaving.Realm, leaving.Profile.Kind, taken, Random.Shared) is { } guild)
            {
                PvpTeamProfile profile = guild.ToProfile();
                var fresh = new PvpRoamTeam
                {
                    Name = guild.Guild, Template = guild.ToTemplate(), Realm = leaving.Realm, Start = leaving.Start, Guild = guild, Profile = profile,
                    Roster = PvpRealmPoints.EnemyRoster(pool, guild.Id), SpawnAt = leaving.SpawnAt, Index = leaving.Index, Map = leaving.Map,
                    Entry = leaving.Map.Ways != null ? leaving.Start.RouteOf(leaving.Index % 2 + 1)[0] : null,
                    State = "Away", StateSince = now, ComesOutAt = now + 1000L * (20 + Random.Shared.Next(41)),
                };
                fresh.LogsOffAt = fresh.ComesOutAt + 1000L * EveningSeconds(Random.Shared.Next());
                fresh.Team.Bots = [];
                fresh.Team.Route = fresh.Entry is { } entry ? [entry] : leaving.Start.RouteOf(leaving.Index + 1);
                // A bad group joins a pack of its realm that has room; a group that stays near others picks its group.
                if (profile.Company == PvpCompany.Zerg)
                    fresh.Company = next.FirstOrDefault(team => team.Realm == fresh.Realm && team.Map == fresh.Map && team.Company == null && team.Profile?.Company == PvpCompany.Zerg &&
                                                                next.Count(other => other.Company == team) < 4);
                else if (profile.Company == PvpCompany.Near && Hosts(next, fresh.Realm).Where(host => host.Map == fresh.Map).ToList() is { Count: > 0 } hosts)
                    fresh.Company = hosts[Random.Shared.Next(hosts.Count)];
                next.Add(fresh);
                Say($"{fresh.Name} will come out", new { what = "join", team = fresh.Name, kind = profile.Kind.ToString(), realm = NameOf(fresh.Realm), beside = fresh.Company?.Name });
            }
            _teams = next;
        }

        /// <summary>
        /// A guild that walks on its own turns back to the point it came from, when it has one
        /// (before its first point it has none, and waits as a team of the roam does). In the
        /// first evening of bots (2026-11-22) three of the six teams that do not add stood
        /// "waiting by a fight" for minutes: "Decent 8v8 teams that would not add" went
        /// somewhere else.
        /// </summary>
        private static bool TurnsAway(PvpRoamTeam team)
        {
            if (team.Entry == null || team.Company != null || team.CameFrom is not { } back || team.Team.Route is not { Length: 1 } route)
                return false;
            (team.Team.Route, team.CameFrom, team.Team.Waypoint, team.ArrivedAt) = ([back], route[0], 0, 0);
            return true;
        }

        /// <summary>
        /// A team that goes from point to point: at the point it was walking to, it picks the next
        /// (<see cref="NextPoint"/>). True while it stands there for its pack to come up.
        /// </summary>
        private static bool Wander(PvpRoamTeam team, List<PvpRoamTeam> teams, long now)
        {
            (int X, int Y) at = team.Team.Route[0];
            (double x, double y) = team.Centre();
            if (Math.Sqrt(Math.Pow(at.X - x, 2) + Math.Pow(at.Y - y, 2)) >= 900)
            {
                team.ArrivedAt = 0;
                return false;
            }
            if (team.ArrivedAt == 0)
                team.ArrivedAt = now;
            if (PackWaits(teams.Any(mate => mate.Company == team && mate.Profile?.Company == PvpCompany.Zerg && mate.Alive > 0 &&
                                            mate.State == "Roaming" && !Near(team, mate, 3_000)), (now - team.ArrivedAt) / 1000.0))
                return true;
            // The head of a pack keeps to the road, where the fights are; an elite group to the quiet ground.
            int leaning = team.Profile is { KeepsToQuietGround: true } ? 1 : team.Profile is { Company: PvpCompany.Zerg } ? -1 : 0;
            (int X, int Y) next = NextPoint(team.Map, at, team.CameFrom, leaning, Random.Shared.Next());
            // A guild with a grudge goes toward the player's group.
            // (In its own zone: it does not port after it.)
            PvpRoamTeam hunted = teams.FirstOrDefault(mine => mine.Leader != null && mine.Alive > 0 && mine.Map == team.Map);
            bool looking = hunted != null && ComesLooking(team.WipedByPlayer, team.AddedOnByPlayer);
            if (looking)
                next = PointToward(team.Map, at, hunted.Centre());
            Say($"{team.Name} goes on{(looking ? ", looking for the player's group" : string.Empty)}",
                new { what = "leg", team = team.Name, from = new[] { at.X, at.Y }, to = new[] { next.X, next.Y }, looking });
            (team.CameFrom, team.Team.Route, team.Team.Waypoint, team.ArrivedAt) = (at, [next], 0, 0);
            return false;
        }

        /// <summary>Every death of the team not yet paid for gives the teams it was fighting their realm points, into their rosters.</summary>
        private static void Pay(PvpRoamTeam team, PvpRealWeights real, long now)
        {
            foreach (GameLiving dead in team.Team.Members)
            {
                if (dead.IsAlive)
                {
                    team.Paid.Remove(dead);
                    continue;
                }
                if (!team.Paid.Add(dead))
                    continue;
                team.Deaths++;
                List<(PvpRoamTeam Team, GameLiving Member)> near = team.Fighting
                    .SelectMany(killers => killers.Team.Members.Where(member => member.IsWithinRadius(dead, real.ShareWithin)).Select(member => (killers, member))).ToList();
                long share = PvpRealmPoints.Share(PvpRealmPoints.ValueOf(dead.Level, PvpRealmPoints.RealmLevelOf(dead)), near.Count, real.RealmPointRate);
                foreach (IGrouping<PvpRoamTeam, (PvpRoamTeam Team, GameLiving Member)> byTeam in near.GroupBy(entry => entry.Team))
                {
                    byTeam.Key.Kills++;
                    byTeam.Key.Earned += share * byTeam.Count();
                    var earned = new Dictionary<(string, int), long>();
                    foreach ((_, GameLiving member) in byTeam)
                    {
                        if (member is GamePlayer player)
                        {
                            // On the character, and in the roster beside the companions.
                            player.GainRealmPoints(share, false);
                            earned[(PvpRealmPoints.PlayerBuild, 0)] = share;
                        }
                        else if (member is GameBot bot)
                        {
                            bot.PvpPoints += share;
                            earned[(bot.PvpBuild.Id.ToLowerInvariant(), bot.PvpRosterPlace)] = share;
                        }
                    }
                    PvpRoster.Credit(AppContext.BaseDirectory, byTeam.Key.Roster, earned);
                    if (_recording is { } recording)
                        recording.Line(new { k = "ev", t = recording.Now, ev = new { what = "pay", dead = dead.Name, team = team.Name, to = byTeam.Key.Name, share, members = byTeam.Count() } });
                    Tell(byTeam.Key, $"{dead.Name} of {team.Name} is down: {share} realm points each.");
                }
            }
        }

        public static object Status()
        {
            List<PvpRoamTeam> teams = _teams;
            long now = GameLoop.GameLoopTime;
            List<string> events;
            lock (Events) events = Events.TakeLast(60).ToList();
            return new
            {
                Running,
                Map = string.Join(", ", _maps.Select(zone => zone.Name)),
                Seconds = Running ? (now - _started) / 1000 : 0,
                Error = _lastError,
                Recording = _recording?.File,
                EffectListComponent.StaleCopiesRetired,
                // What a player is told by /8v8 status.
                Lines = StatusLines(null),
                // A check: effects on a member whose caster is in another team of the roam (a song is for its singer's group only).
                FromOtherTeams = teams.SelectMany(team => team.Team.Members.Where(member => member.IsAlive).SelectMany(member =>
                    member.effectListComponent.GetSpellEffects()
                        .Where(effect => effect?.SpellHandler?.Caster is GameLiving caster && !effect.SpellHandler.Spell.IsHarmful &&
                                         teams.Any(other => other != team && other.Realm == team.Realm && other.Team.Members.Contains(caster)))
                        .Select(effect => $"{member.Name} ({team.Name}) has {effect.SpellHandler.Spell.Name} of {effect.SpellHandler.Caster.Name}"))).Take(40).ToList(),
                Teams = teams.Select(team =>
                {
                    (double x, double y) = team.Centre();
                    return new
                    {
                        team.Name, Realm = team.Realm.ToString(), team.State, team.Waiting, team.Alive, At = $"{(int)x},{(int)y}",
                        Waypoint = team.Team.Waypoint, Moving = team.Team.Bots.Count(bot => bot.IsAlive && bot.IsMoving),
                        Fighting = team.Fighting.Select(other => other.Name).ToList(),
                        team.Fights, team.Kills, team.Deaths, team.Wipes, team.Joined, team.LeftAlone, team.Earned,
                        Rank = PvpRealmPoints.RankName((int)Math.Round(team.Team.Bots.Select(bot => bot.RealmLevel).DefaultIfEmpty(1).Average())),
                        Leader = team.Leader?.Name,
                        Kind = team.Profile?.Kind.ToString(), With = team.Company?.Name, Builds = team.Guild?.Builds, Zone = team.Map.Name,
                    };
                }).ToList(),
                Events = events,
            };
        }
    }
}

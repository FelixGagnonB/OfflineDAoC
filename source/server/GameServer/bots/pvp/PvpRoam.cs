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
    /// <param name="AtKeep">It is in front of a keep of the realm ("the alb keep" in realm chat). False: a place where the realm comes into a zone it has no keep in.</param>
    public sealed record PvpRoamStart(eRealm Realm, (int X, int Y) Point, (int X, int Y)[] Route, (int X, int Y)[] SecondRoute = null, bool AtKeep = true)
    {
        public (int X, int Y)[] RouteOf(int teamNumber) => teamNumber >= 2 && SecondRoute != null ? SecondRoute : Route;
    }

    /// <summary>
    /// What a zone is like on a prime-time evening (M11.8): where the packs and the groups that
    /// add are, where both kinds meet, and where the teams go that want a fight of two. The
    /// owner, 2026-11-22: "Elite 8v8 teams that would be only 8v8 in specific area to avoid not
    /// clean fights"; 2026-11-23: "adding more zones and spreading teams (and type of teams)
    /// across them all".
    /// </summary>
    public enum PvpZoneCrowd { Crowded, Mixed, Clean }

    /// <summary>A zone a roam is held in: its region and where each realm's teams start and walk.</summary>
    /// <param name="Ways">Which of its points a team can walk between directly; a team of a prime-time evening goes from point to point along them as it pleases.</param>
    /// <param name="Quiet">The points away from the road.</param>
    /// <param name="Places">What its points are called in realm chat ("south road").</param>
    /// <param name="Crowd">What it is like on a prime-time evening: which kinds of team lean to it.</param>
    /// <param name="Short">What it is called in a line of chat, when that is not the first word of its name ("Sauvage").</param>
    /// <param name="Zone">The game's number of the zone; 0 when it is not told.</param>
    public sealed record PvpRoamMap(string Id, string Name, ushort Region, IReadOnlyList<PvpRoamStart> Starts,
        IReadOnlyList<((int X, int Y) A, (int X, int Y) B)> Ways = null, IReadOnlyList<(int X, int Y)> Quiet = null,
        IReadOnlyList<((int X, int Y) Point, string Name)> Places = null, PvpZoneCrowd Crowd = PvpZoneCrowd.Mixed, string Short = null, ushort Zone = 0)
    {
        public PvpRoamStart StartOf(eRealm realm) => Starts.First(start => start.Realm == realm);

        /// <summary>The zone as it is named in a line of chat: "Emain", "Odin's", "Hadrian's", "Sauvage".</summary>
        public string Tag => Short ?? Name.Split(' ')[0];

        /// <summary>A spot farther than this from the nearest named point is called by its side of it: "north-west of middle".</summary>
        public const int AtAPointWithin = 3_000;

        /// <summary>
        /// What the nearest named point to a place is called; "the X keep" within 6,000 of a
        /// realm's start; null on a map without names. Farther than <see cref="AtAPointWithin"/>
        /// from the point, the side of it comes first ("south-west of Crimthain"), unless
        /// <paramref name="pointOnly"/>: the points of a back zone lie 15,000 to 18,000 units
        /// apart, and the owner, 2026-11-24, sent to "Crimthain" after two Midgard groups that
        /// were 11,000 and 15,000 units from it: "I don't see anyone and no one attacking us in
        /// Crimthain".
        /// </summary>
        public string PlaceOf(double x, double y, bool pointOnly = false)
        {
            double From((int X, int Y) point) => Math.Sqrt(Math.Pow(point.X - x, 2) + Math.Pow(point.Y - y, 2));
            if (Starts.Where(start => start.AtKeep && From(start.Point) < 6_000).OrderBy(start => From(start.Point)).FirstOrDefault() is { } keep)
                return $"the {PvpRoam.Short(keep.Realm, false)} keep";
            if (Places is not { Count: > 0 })
                return null;
            ((int X, int Y) point, string name) = Places.OrderBy(place => From(place.Point)).First();
            return pointOnly || From(point) <= AtAPointWithin ? name : $"{PvpRoam.Compass(x - point.X, y - point.Y)} of {name}";
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
        /// <summary>Since when it stands by that fight; and that it has given up waiting for it to end.</summary>
        public long WaitingSince;
        public bool GaveUpWaiting;
        /// <summary>When it gave up waiting by a fight, and on which side it goes round that fight (PvpRoam.AroundAFight; 0: not chosen yet).</summary>
        public long GaveUpAt;
        public int AroundSide;
        /// <summary>On the head of a pack: the team of the pack that the others walk behind now (the head itself, until it is down).</summary>
        public PvpRoamTeam PackLead;
        /// <summary>It has had its rest since its last fight.</summary>
        public bool Rested;
        /// <summary>The place off its road it is walking to, to rest there; null when it rests where it is or not at all.</summary>
        public System.Numerics.Vector3? Hide;
        public long HideSince;
        /// <summary>Out of a fight with dead members since this tick (0: none); and gone back to its start to form up again.</summary>
        public long DeadSince;
        public bool Reforming;
        /// <summary>The player, dead, has given the fight up (/8v8 call die): the group is down at once and stands up at its start.</summary>
        public bool GivenUp;
        /// <summary>When it broke off from a fight it had lost (0: it has not), and when an enemy team was last near it on its run.</summary>
        public long BrokeOffAt, LastFollowedTick;
        /// <summary>The enemy teams it is in a fight with now.</summary>
        public readonly HashSet<PvpRoamTeam> Fighting = [];
        /// <summary>Its draw for each enemy team its realm's other team is fighting: true, it joins.</summary>
        public readonly Dictionary<PvpRoamTeam, bool> Draws = [];
        /// <summary>The player's group: when it last said it saw groups of a realm and how many; the enemy teams of its fight it has told the player of; when it last called an add.</summary>
        public readonly Dictionary<eRealm, (long When, int Groups)> Sighted = [];
        public readonly HashSet<PvpRoamTeam> AddsCalled = [];
        public long AddCalledAt;
        /// <summary>A team of bots in a fight: the enemy teams it has seen coming for it (PvpRoam.SeesItComing).</summary>
        public readonly HashSet<PvpRoamTeam> ComingSeen = [];
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
            [(South, "south road"), (Middle, "mid road"), (North, "north road"), (WestNorth, "north-west hills"), (WestMiddle, "west hills"), (WestSouth, "south-west hills")],
            PvpZoneCrowd.Crowded, null, 214);
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
             (HwSouthEast, "south-east field"), (HwBenowycRoad, "benowyc road"), (HwEastGate, "east gate"), (HwNorthEast, "north-east field")],
            PvpZoneCrowd.Mixed, null, 15);

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
             (OgSouthEast, "south-east field"), (OgSouthRoad, "south road"), (OgSouthWest, "south-west field")],
            PvpZoneCrowd.Mixed, null, 115);

        /// <summary>
        /// The frontier zones an evening can be in (M11.3, M11.8): the three a realm ported to, and
        /// the nine behind them (<see cref="PvpRoamZones"/>). Zones whose points have been walked on
        /// the installed navmesh. An evening is in some of them (<see cref="ZonesTonight"/>).
        /// </summary>
        public static readonly IReadOnlyList<PvpRoamMap> Frontiers = [Emain, OdinsGate, HadriansWall, .. PvpRoamZones.All];

        /// <summary>The map a word stands for ("thid", "thidranki", "emain", "breifine"); null for a word that names none.</summary>
        public static PvpRoamMap MapFor(string word)
        {
            string key = (word ?? string.Empty).Trim().ToLowerInvariant();
            return key switch
            {
                "thid" or "thidranki" => Thidranki,
                "emain" or "emainmacha" => Emain,
                "odin" or "odins" or "odin's" or "og" or "odinsgate" => OdinsGate,
                "hadrian" or "hadrians" or "hadrian's" or "hw" or "hadrianswall" => HadriansWall,
                _ => PvpRoamZones.All.FirstOrDefault(zone => zone.Id == key || zone.Tag.ToLowerInvariant() == key ||
                                                             zone.Name.ToLowerInvariant().Replace(" ", string.Empty) == key),
            };
        }

        /// <summary>The place of a draw among weights: the first when the roll falls in its share, and so on; -1 when no weight is above 0.</summary>
        public static int Weighted(IReadOnlyList<int> weights, int roll)
        {
            int sum = weights.Sum(weight => Math.Max(0, weight));
            if (sum <= 0)
                return -1;
            int pick = Math.Abs(roll % sum);
            for (int index = 0; index < weights.Count; index++)
            {
                if (pick < Math.Max(0, weights[index]))
                    return index;
                pick -= Math.Max(0, weights[index]);
            }
            return weights.Count - 1;
        }

        /// <summary>
        /// How much a kind of team leans to a kind of zone (M11.8), 0 for "it does not go there".
        /// The packs and the groups that stay near others are where the crowd is and never in a
        /// clean zone; a group that adds goes where the realm points are, and now and then where
        /// it is not wanted; the teams that do not add are everywhere, the better the cleaner; an
        /// elite team is not in the crowd at all, unless it is there to kill it (the hunters).
        /// The numbers are a first guess, to be played.
        /// </summary>
        public static int Leans(PvpTeamKind kind, PvpZoneCrowd crowd) => (kind, crowd) switch
        {
            (PvpTeamKind.Bad, PvpZoneCrowd.Crowded) => 6, (PvpTeamKind.Bad, PvpZoneCrowd.Mixed) => 2, (PvpTeamKind.Bad, _) => 0,
            (PvpTeamKind.Clingy, PvpZoneCrowd.Crowded) => 4, (PvpTeamKind.Clingy, PvpZoneCrowd.Mixed) => 3, (PvpTeamKind.Clingy, _) => 0,
            (PvpTeamKind.Adder, PvpZoneCrowd.Crowded) => 4, (PvpTeamKind.Adder, PvpZoneCrowd.Mixed) => 4, (PvpTeamKind.Adder, _) => 1,
            (PvpTeamKind.Decent, PvpZoneCrowd.Crowded) => 2, (PvpTeamKind.Decent, PvpZoneCrowd.Mixed) => 4, (PvpTeamKind.Decent, _) => 3,
            (PvpTeamKind.Strong, PvpZoneCrowd.Crowded) => 1, (PvpTeamKind.Strong, PvpZoneCrowd.Mixed) => 3, (PvpTeamKind.Strong, _) => 5,
            (PvpTeamKind.Elite, PvpZoneCrowd.Crowded) => 0, (PvpTeamKind.Elite, PvpZoneCrowd.Mixed) => 1, (PvpTeamKind.Elite, _) => 6,
            // (The hunters are where their prey is.)
            (PvpTeamKind.Hunter, PvpZoneCrowd.Crowded) => 6, (PvpTeamKind.Hunter, PvpZoneCrowd.Mixed) => 2, (PvpTeamKind.Hunter, _) => 0,
            _ => 1,
        };

        /// <summary>
        /// Which zone of the evening a team that walks alone, or the head of a pack, takes: one
        /// its kind leans to (<see cref="Leans"/>), and of those rather one that has few groups of
        /// its realm, a zone that has none three times as readily: so a zone is not left to two
        /// realms. When its kind leans to none of the zones, the one with the fewest.
        /// </summary>
        /// <param name="groupsOfItsRealm">How many groups of its realm each zone has (a pack counts for all its groups).</param>
        public static int ZoneFor(PvpTeamKind kind, IReadOnlyList<PvpZoneCrowd> zones, IReadOnlyList<int> groupsOfItsRealm, int roll)
        {
            int pick = Weighted(zones.Select((crowd, zone) => Leans(kind, crowd) * 120 / (1 + groupsOfItsRealm[zone]) * (groupsOfItsRealm[zone] == 0 ? 3 : 1)).ToList(), roll);
            return pick >= 0 ? pick : ZoneWithFewest(groupsOfItsRealm, roll);
        }

        /// <summary>The order the teams of a realm take their zones in: the choosiest first, while every zone is still open to it.</summary>
        public static int Choosiness(PvpTeamKind kind) => kind switch
        {
            PvpTeamKind.Elite or PvpTeamKind.Hunter => 0, PvpTeamKind.Bad => 1, PvpTeamKind.Strong => 2, PvpTeamKind.Clingy => 3, PvpTeamKind.Adder => 4, _ => 5,
        };

        /// <summary>
        /// How many zones an evening is in: one for every three groups of a realm, which is some
        /// nine teams a zone (eight groups of a realm: three zones, as in the evenings of
        /// 2026-11-22, where 24 teams in three zones had a fight every other minute and 36 in one
        /// zone had one fight of eleven).
        /// </summary>
        public static int ZonesFor(int groupsPerRealm, int zonesThereAre) => Math.Clamp((groupsPerRealm + 1) / 3, 1, Math.Max(1, zonesThereAre));

        /// <summary>
        /// The zones of an evening (M11.8): where it begins, the crowded one (the crowd is always
        /// somewhere), and others drawn, a zone the realms ported to four times as readily as one
        /// behind it. So an evening of eight groups a realm that begins in Emain Macha is there,
        /// and in two more that are not the same every evening: which, the realm's chat and
        /// <c>/8v8 who</c> tell.
        /// </summary>
        public static List<PvpRoamMap> ZonesTonight(PvpRoamMap begins, int groupsPerRealm, IReadOnlyList<PvpRoamMap> all, Random random)
        {
            var zones = new List<PvpRoamMap> { begins };
            int count = ZonesFor(groupsPerRealm, all.Count);
            if (count > 1 && begins.Crowd != PvpZoneCrowd.Crowded && all.FirstOrDefault(zone => zone.Crowd == PvpZoneCrowd.Crowded) is { } crowded)
                zones.Add(crowded);
            while (zones.Count < count)
            {
                List<PvpRoamMap> left = all.Where(zone => !zones.Contains(zone)).ToList();
                int pick = Weighted(left.Select(zone => zone.Crowd switch { PvpZoneCrowd.Crowded => 6, PvpZoneCrowd.Mixed => 4, _ => 1 }).ToList(), random.Next());
                if (pick < 0)
                    break;
                zones.Add(left[pick]);
            }
            return zones;
        }

        /// <summary>Every map, as the tools read them (<c>roam-maps</c> on the control channel; source/tools/pvp_map_draw.py draws them).</summary>
        public static object Maps() => Frontiers.Append(Thidranki).Select(map =>
        {
            List<(int X, int Y)> points = (map.Places ?? []).Select(place => place.Point).ToList();
            Zone zone = map.Zone == 0 ? null : WorldMgr.GetZone(map.Zone);
            return new
            {
                map.Id, map.Name, map.Tag, map.Region, map.Zone, Crowd = map.Crowd.ToString(),
                Offset = zone == null ? null : new[] { zone.XOffset, zone.YOffset },
                Points = (map.Places ?? []).Select(place => new { X = place.Point.X, Y = place.Point.Y, place.Name, Quiet = map.Quiet?.Contains(place.Point) == true }).ToList(),
                Ways = (map.Ways ?? []).Select(way => new[] { points.IndexOf(way.A), points.IndexOf(way.B) }).ToList(),
                Starts = map.Starts.Select(start => new
                {
                    Realm = NameOf(start.Realm), X = start.Point.X, Y = start.Point.Y, start.AtKeep,
                    First = new[] { start.RouteOf(1)[0], start.RouteOf(2)[0] }.Distinct().Select(point => points.IndexOf(point)).ToList(),
                }).ToList(),
            };
        }).ToList();

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
            string who = $"{Math.Max(1, groups)}fg {Short(realm)}", where = At(place);
            return wiped ? $"wiped{where}, {who}" : fightingThem ? $"fighting {who}{where}" : $"{who}{where}";
        }

        /// <summary>
        /// What is said in realm chat when a group breaks off (bots/pvp/PvpKite.cs). By a group of
        /// the player's realm that runs: "running from 2fg albs at south road". By one that has
        /// beaten an enemy group which then runs: "4 mids on the run at south road", so that
        /// whoever is near may cut them off.
        /// </summary>
        public static string RunningText(int groups, eRealm from, string place) =>
            $"running from {Math.Max(1, groups)}fg {Short(from)}" + At(place);

        public static string OnTheRunText(int left, eRealm realm, string place) =>
            $"{Math.Max(1, left)} {Short(realm)} on the run" + At(place);

        /// <summary>" at south road", or " north of south road" for a spot called by its side of a point (PvpRoamMap.PlaceOf); nothing for no place.</summary>
        public static string At(string place) =>
            place == null ? string.Empty
            : place.Contains(" of ") && new[] { "north", "south", "east", "west" }.Any(place.StartsWith) ? $" {place}" : $" at {place}";

        /// <summary>
        /// The side something lies on, from how far east and how far south of you it is (in the
        /// game's world east is more X and south is more Y): "north", "north-east" and so on.
        /// </summary>
        public static string Compass(double east, double south)
        {
            string[] sides = ["east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east"];
            return sides[(int)Math.Round(Math.Atan2(-south, east) / (Math.PI / 4) + 8) % 8];
        }

        /// <summary>
        /// What the player is told with a line of realm chat about a spot in the zone it is in:
        /// "8,500 north-west of you", to the nearest 500. Realm chat told nobody that; a player
        /// of the time had the zone's map in the head, and the owner has not ("can't find
        /// them", 2026-11-24, having walked 7,800 units the wrong way in half a minute).
        /// </summary>
        public static string FromYouText(double east, double south)
        {
            double away = Math.Sqrt(east * east + south * south);
            return away < 750 ? "beside you" : $"{Math.Round(away / 500) * 500:N0} {Compass(east, south)} of you";
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

        /// <summary>
        /// A guild that does not add and walks alone stands by a fight it has come on for this
        /// long, for it to end and leave it somebody to fight; then it turns back the way it came
        /// (or, before its first point, walks on past). It turned back at once at first, and in
        /// an evening of 25 minutes (2026-11-22) a strong team walked 31 legs and fought nobody;
        /// before that it waited for as long as the fight lasted, and three teams stood for
        /// minutes before a fight that never ended.
        /// </summary>
        public static bool GivesUpWaiting(double secondsStandingByAFight) => secondsStandingByAFight > 45;

        /// <summary>How near to a fight it has left alone a team's road may pass, and how far from it the team goes round.</summary>
        public const int PassesAFightAt = 4_000, GoesRoundAFightAt = 4_500;
        /// <summary>For how long a team goes round a fight; after that (a wall, a gate, water in the way) it walks its road.</summary>
        public const int GoesRoundForSeconds = 75;

        /// <summary>
        /// Where a team that walks on past a fight it has left alone goes first: a point beside
        /// that fight, <see cref="GoesRoundAFightAt"/> from its middle, on the side its road
        /// already leans to. Null when its road does not pass within <see cref="PassesAFightAt"/>
        /// of the fight: then it walks its road. The owner, 2026-11-24, of a decent Albion group
        /// that stood by the owner's fight for 45 seconds and then walked on, its road straight
        /// through the owner's group: "I feel like it ran through us"; and, asked: "I'd go with
        /// making the group go around, if they decide not to add, instead of walking in middle."
        /// Looked at again every turn, with the side kept: as the team comes round, the point
        /// moves round with it, and once the rest of its road is clear there is none.
        /// </summary>
        /// <param name="side">The side it goes round on: chosen the first time (1 or -1), kept after.</param>
        public static (int X, int Y)? AroundAFight((double X, double Y) from, (double X, double Y) to, (double X, double Y) fight, ref int side)
        {
            (double dx, double dy) = (to.X - from.X, to.Y - from.Y);
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1)
                return null;
            // Where its road is nearest to the fight.
            double along = Math.Clamp(((fight.X - from.X) * dx + (fight.Y - from.Y) * dy) / (length * length), 0, 1);
            (double nearX, double nearY) = (from.X + dx * along, from.Y + dy * along);
            if (Math.Sqrt(Math.Pow(nearX - fight.X, 2) + Math.Pow(nearY - fight.Y, 2)) >= PassesAFightAt)
                return null;
            // The point it walks to is in the fight: it goes as near to that point as it may, and stands there.
            double toFight = Math.Sqrt(Math.Pow(to.X - fight.X, 2) + Math.Pow(to.Y - fight.Y, 2));
            if (toFight < PassesAFightAt)
            {
                (double outX, double outY) = toFight < 1 ? (-dx / length, -dy / length) : ((to.X - fight.X) / toFight, (to.Y - fight.Y) / toFight);
                // (From the far side of the fight that point is behind it: then round by the side first, as below.)
                if ((from.X - fight.X) * outX + (from.Y - fight.Y) * outY > 0)
                {
                    (double X, double Y) stand = (fight.X + outX * GoesRoundAFightAt, fight.Y + outY * GoesRoundAFightAt);
                    return Math.Sqrt(Math.Pow(stand.X - from.X, 2) + Math.Pow(stand.Y - from.Y, 2)) < 600 ? null : ((int)stand.X, (int)stand.Y);
                }
            }
            (double acrossX, double acrossY) = (-dy / length, dx / length);
            if (side == 0)
                side = (nearX - fight.X) * acrossX + (nearY - fight.Y) * acrossY < 0 ? -1 : 1;
            return ((int)(fight.X + acrossX * side * GoesRoundAFightAt), (int)(fight.Y + acrossY * side * GoesRoundAFightAt));
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
        /// several: the one that has the fewest groups of its realm so far (a pack counts for all
        /// its groups), any of them when several have as few. So each zone has its share of each
        /// realm, and a realm's pack is in one zone one evening and in another the next. A team
        /// that walks with another goes where that one goes. (Taking the zones in turn put every
        /// realm's pack in the first: of 24 guilds, twelve were in Emain Macha, 2026-11-22.)
        /// </summary>
        /// <param name="groupsOfItsRealm">How many groups of its realm each zone has.</param>
        public static int ZoneWithFewest(IReadOnlyList<int> groupsOfItsRealm, int roll)
        {
            if (groupsOfItsRealm.Count == 0)
                return 0;
            int fewest = groupsOfItsRealm.Min();
            List<int> zones = Enumerable.Range(0, groupsOfItsRealm.Count).Where(zone => groupsOfItsRealm[zone] == fewest).ToList();
            return zones[Math.Abs(roll) % zones.Count];
        }

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

        /// <summary>What the player's group is called on a prime-time evening: its guild's name, unless it has none or a guild of bots that is out has the same.</summary>
        public static string OwnTeamName(string guildOfThePlayer, string otherwise, IEnumerable<string> guildsOfBots) =>
            string.IsNullOrWhiteSpace(guildOfThePlayer) || guildsOfBots.Any(name => string.Equals(name, guildOfThePlayer, StringComparison.OrdinalIgnoreCase))
                ? otherwise : guildOfThePlayer;

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
        /// Whether the player has picked an enemy group for its own to take on. Out of a fight,
        /// by having one of its members as a target, as before. In a fight with somebody else a
        /// target alone picks nobody: the player has to have struck that member, or called its
        /// group on it (/8v8 call assist, /8v8 call control). The owner, 2026-11-24: "say we are
        /// in a fight and I click on a member of an idle team, does that trigger them to add on
        /// the fight?" It did, and so did a tab; asked, the owner picked "Attack or call only".
        /// </summary>
        public static bool PlayerPicks(bool targetIsOfThatGroup, bool groupFightsSomebodyElse, bool playerHasStruckIt, bool playerCalledOnIt) =>
            playerCalledOnIt || targetIsOfThatGroup && (!groupFightsSomebodyElse || playerHasStruckIt);

        /// <summary>For a player's call on its target: the target is a member of an enemy group of the roam the player's group is not fighting, within the range it forms up at.</summary>
        public static bool StandsBy(GamePlayer player, GameLiving target)
        {
            if (player == null || target is not { IsAlive: true } || !player.IsWithinRadius(target, PvpFormation.FormRange))
                return false;
            List<PvpRoamTeam> teams = _teams;
            PvpRoamTeam own = teams?.FirstOrDefault(team => team.Leader == player);
            return own != null && teams.Any(other => other != own && other.Realm != own.Realm && other.Alive > 0 && other.Team.Members.Contains(target));
        }

        /// <summary>"/8v8 call die" (PvpCalls.IsDieCall): what the player is told.</summary>
        public static string GiveUp(GamePlayer player)
        {
            PvpRoamTeam own = _teams?.FirstOrDefault(team => team.Leader == player);
            if (PvpCalls.CannotGiveUp(own != null, player.IsAlive, own is { State: "Down" }) is { } why)
                return why;
            own.GivenUp = true;
            return "Your group gives the fight up.";
        }

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

        /// <summary>How far off a team in a fight sees an enemy group that comes for it.</summary>
        public const int AddSeenWithin = 4_500;

        /// <summary>
        /// A team of bots in a fight sees an enemy group that comes for it before that group is
        /// on it: the group has decided to join the fight against it (its draw), is within
        /// <see cref="AddSeenWithin"/> and in sight of one of its members. From then it is one
        /// of its enemies: its mezzer may take it as it arrives, and an elite team, which keeps
        /// its fights clean, leaves before it lands and not after. Until 2026-11-24 "a new group
        /// arriving in a fight is counted the second its members aim at the group; nothing else
        /// is made of it" (M12.4): it was let walk by until one of its members aimed at the team
        /// from within 2,000 units, four or five seconds before it was in melee.
        /// </summary>
        public static bool SeesItComing(int teamsItIsFighting, bool fightingThatOne, bool thatOneHasDecidedToJoinAgainstIt, bool withinSight) =>
            teamsItIsFighting > 0 && !fightingThatOne && thatOneHasDecidedToJoinAgainstIt && withinSight;

        /// <summary>Whether somebody of a team sees somebody of another within a distance, as its members see when they march (PvpSight).</summary>
        private static bool Sees(PvpRoamTeam team, PvpRoamTeam other, int within)
        {
            List<GameBot> watchers = team.Team.Bots.Where(bot => bot.IsAlive).ToList();
            if (watchers.Count == 0 || !Near(team, other, within + 1_500))
                return false;
            PvpWeights sight = PvpWeights.Of(watchers[0]);
            return other.Team.Members.Any(member => member.IsAlive && watchers.Any(bot => PvpSight.Sees(bot, member, within, sight)));
        }

        /// <summary>
        /// A team of bots on its own does not start a fight with a team that has its realm-mate
        /// beside it: eight do not walk into sixteen. In a roam of 2026-11-22 a Hibernian team
        /// took on both Midgard teams, which had just finished a fight together, in the same
        /// second. If the two come for it, it answers.
        /// </summary>
        public static bool AvoidsTwo(bool itHasItsRealmMateBesideIt, bool weHaveOursBesideUs, bool fightingItAlready, bool itAttacksUs) =>
            itHasItsRealmMateBesideIt && !weHaveOursBesideUs && !fightingItAlready && !itAttacksUs;

        /// <summary>
        /// Where a team goes that has said it joins a fight and does not see it yet: to the
        /// nearest of the enemy teams it drew "joins" for; nothing when it drew that for none.
        /// A team marches at the enemies it sees and otherwise on along its road, and it sees
        /// 6,500 units over open ground, while the draw falls at 9,000: in the owner's evening
        /// of 2026-11-24 the group that walked with the owner's said it joined its fight from
        /// 7,200 units off, walked on north along the road it had, and stood 9,000 units away
        /// until the fight was over. Of the 237 times a team said "joins" that evening, eight
        /// were followed by no fight at all within a minute and a half, and 81 by a fight with
        /// some other enemy team first.
        /// </summary>
        public static (int X, int Y)? FightItJoined(IEnumerable<((double X, double Y) At, bool SaidItJoins)> enemyTeams, (double X, double Y) from) =>
            enemyTeams.Where(enemy => enemy.SaidItJoins)
                .OrderBy(enemy => Math.Pow(enemy.At.X - from.X, 2) + Math.Pow(enemy.At.Y - from.Y, 2))
                .Select(enemy => ((int X, int Y)?)((int)enemy.At.X, (int)enemy.At.Y)).FirstOrDefault();

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
                              : viewersZone != null && team.Map != viewersZone ? $", in {team.Map.Name}"
                              : away < 750 ? ", beside you" : $", {away:N0} {Compass(x - viewer.X, y - viewer.Y)} of you"));
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
        /// The first line of <c>/8v8 who</c> on an evening in several zones: which they are, what
        /// each is like (<see cref="PvpZoneCrowd"/>), and the words that take the player's group there.
        /// </summary>
        public static string ZonesLine(IReadOnlyList<PvpRoamMap> zones, PvpRoamMap yours)
        {
            string Like(PvpRoamMap zone) => string.Join("; ", new[]
            {
                zone.Crowd == PvpZoneCrowd.Crowded ? "the crowd" : zone.Crowd == PvpZoneCrowd.Clean ? "clean" : null, zone == yours ? "you are here" : null,
            }.Where(word => word != null));
            return "Tonight: " + string.Join(", ", zones.Select(zone => zone.Name + (Like(zone).Length > 0 ? $" ({Like(zone)})" : string.Empty))) +
                   ". /8v8 go " + string.Join(", ", zones.Where(zone => zone != yours).Select(zone => zone.Id)) + ".";
        }

        /// <summary>
        /// <c>/8v8 who</c>: the guilds of the player's realm that are out, each with its rank and
        /// who is in it, as <c>/who</c> showed a realm its own; and how many groups the other
        /// realms have out. Empty when no roam runs.
        /// </summary>
        public static List<string> WhoLines(GamePlayer viewer)
        {
            List<PvpRoamTeam> teams = _teams;
            List<PvpRoamMap> zones = _maps;
            if (teams.Count == 0)
                return [];
            PvpRoamMap here = viewer == null ? null : teams.FirstOrDefault(team => team.Leader == viewer)?.Map;
            List<string> lines = zones.Select(zone => WhoLine(zone.Name, zone == here,
                teams.Where(team => team.Map == zone && team.State != "Away")
                    .Select(team => (team.Realm, KindWord(team.Profile?.Kind), team.Leader != null && team.Leader == viewer)).ToList())).ToList();
            if (zones.Count > 1 && here != null)
                lines.Add("/8v8 go " + string.Join(", ", zones.Where(zone => zone != here).Select(zone => zone.Id)) + " takes your group there.");
            return lines;
        }

        /// <summary>A kind of group as <c>/8v8 who</c> names it.</summary>
        public static string KindWord(PvpTeamKind? kind) => kind switch
        {
            PvpTeamKind.Bad => "bad", PvpTeamKind.Clingy => "casual", PvpTeamKind.Adder => "casual 8v8", PvpTeamKind.Decent => "decent",
            PvpTeamKind.Strong => "strong", PvpTeamKind.Elite => "elite", PvpTeamKind.Hunter => "zerg hunters", _ => "other",
        };

        /// <summary>
        /// One zone of <c>/8v8 who</c>: how many groups each realm has in it and of what kinds.
        /// The owner, 2026-11-24, of what the command said until then (every guild of the
        /// player's realm with its rank and its eight classes, and a count of the others): "I
        /// would probably change /8v8 who. I don't really need group setup and RR. I guess the
        /// idea would be to know which zone has what kind of group and how many groups."
        /// </summary>
        public static string WhoLine(string zone, bool youAreHere, IReadOnlyList<(eRealm Realm, string Kind, bool Yours)> groups)
        {
            string head = zone + (youAreHere ? " (you are here)" : string.Empty) + ": ";
            if (groups.Count == 0)
                return head + "nobody.";
            return head + string.Join(". ", groups.GroupBy(group => group.Realm).OrderBy(realm => realm.Key).Select(realm =>
                $"{NameOf(realm.Key)} {realm.Count()}: " + string.Join(", ", (realm.Any(group => group.Yours) ? new[] { "yours" } : [])
                    .Concat(realm.Where(group => !group.Yours).GroupBy(group => group.Kind).OrderByDescending(kind => kind.Count()).ThenBy(kind => kind.Key, StringComparer.Ordinal)
                        .Select(kind => $"{kind.Count()} {kind.Key}"))))) + ".";
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
        /// <param name="at">The spot the line is about: a player in that zone is told how far and which way it is (<see cref="FromYouText"/>).</param>
        private static void RealmChat(List<PvpRoamTeam> teams, PvpRoamTeam from, string text, (double X, double Y)? at = null)
        {
            string speaker = from.Team.Bots.FirstOrDefault(bot => bot.IsAlive)?.Name ?? from.Team.Bots.FirstOrDefault()?.Name ?? from.Name;
            Say($"[{from.Name}] {speaker}: {text}", new { what = "chat", team = from.Name, speaker, text });
            foreach (PvpRoamTeam mine in teams.Where(team => team.Leader?.Client?.IsPlaying == true && team.Realm == from.Realm))
            {
                string fromYou = at is { } spot && mine.Map == from.Map ? $" [{FromYouText(spot.X - mine.Leader.X, spot.Y - mine.Leader.Y)}]" : string.Empty;
                mine.Leader.Out.SendMessage($"[Realm] {speaker} <{from.Name}>: {text}{fromYou}", PacketHandler.eChatType.CT_Alliance, PacketHandler.eChatLoc.CL_ChatWindow);
            }
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
                    // (One call about a realm at a named point, as before: its side of the point is in the words, not in what counts as news.)
                    string point = InZone(friend.Map, friend.Map.PlaceOf(x, y, true));
                    bool known = CalledOut.TryGetValue((seen.Key, point ?? string.Empty), out (long When, int Groups) last);
                    if (!CallsOut((now - _lastCall) / 1000.0, known ? (now - last.When) / 1000.0 : double.MaxValue, known && seen.Count() > last.Groups))
                        continue;
                    // The player stands there: no news.
                    if (mine?.Leader is { } player && mine.Map == friend.Map && player.IsWithinRadius(new Point2D((int)x, (int)y), NoNewsWithin))
                        continue;
                    (CalledOut[(seen.Key, point ?? string.Empty)], _lastCall) = ((now, seen.Count()), now);
                    RealmChat(teams, friend, SightingText(seen.Count(), seen.Key, place, seen.Any(friend.Fighting.Contains)), (x, y));
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
                throw new InvalidOperationException("This evening is on in: " + string.Join(", ", _maps.Select(zone => zone.Name)) + ". /8v8 go " +
                                                    string.Join(", ", _maps.Where(zone => zone != mine.Map).Select(zone => zone.Id)) + ".");
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
        /// <param name="oneZone">A prime-time evening in the one zone, not over several frontiers.</param>
        /// <param name="everyZone">A prime-time evening in every frontier zone there is, however few the groups.</param>
        /// <summary>
        /// The realm level the player's group has for this evening when the player picked one
        /// (/8v8 prime emain 8 rank 8); 0: what it has earned, as always. The owner, 2026-11-24,
        /// asked to try the groups that fall back and the kinds of team: "I might be needing to
        /// change my Realm Rank. Fighting a zerg at RR1 is impossible". On such an evening the
        /// player's group earns nothing: its ranks are as they were when it is over.
        /// </summary>
        private static int _trialLevel;

        /// <summary>The realm level a rank picked for an evening stands for (rank 8 is level 70); 0 for no rank or one that is not a rank.</summary>
        public static int TrialLevel(int rank) => rank is >= 1 and <= 13 ? (rank - 1) * 10 : 0;

        /// <summary>Whether a kill is written to a team's realm points: not for the player's group on an evening at a picked rank.</summary>
        public static bool Earns(bool ledByAPlayer, int trialLevel) => !(ledByAPlayer && trialLevel > 0);

        public static void StartFor(GamePlayer player, Action<string> say, PvpGroupTemplate own = null, PvpRoamMap map = null, int prime = 0, bool oneZone = false,
            bool everyZone = false, int trialRank = 0)
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
            // A rank picked for this evening (prime time only): the player is raised to it, as for a practice fight.
            int trial = prime > 0 ? TrialLevel(trialRank) : 0;
            if (trial > 0)
                say(PvpPlayerReady.RaiseRealmRank(player, trial) ??
                    $"You are at realm rank {PvpRealmPoints.RankName(player.RealmLevel)} already.");
            PvpScrim.TravelThen(player, map.Region, StartSpot(map, player.Realm), say, () =>
            {
                try
                {
                    _trialLevel = trial;
                    if (prime > 0)
                    {
                        List<PvpGuildTeam> pool = PvpTeamPool.LoadOrCreate(AppContext.BaseDirectory, PvpTeamPool.World, Math.Max(16, prime * 2), Random.Shared,
                            PvpWeights.Current.Real.GuildsBeginAtTheBottom);
                        // What the player's next evening begins with by itself (PvpEveningStart): this zone, this many groups, this line-up.
                        PvpPlayerSettings.Set(AppContext.BaseDirectory, player.InternalID, kept => kept with { Lineup = own?.Id, Zone = map.Id, Groups = prime });
                        // Over some of the frontiers (ZonesTonight), unless one zone or all of them were asked for; a battleground is always one zone.
                        List<PvpRoamMap> zones = oneZone || !Frontiers.Contains(map) ? null : everyZone ? Frontiers.ToList() : ZonesTonight(map, prime, Frontiers, Random.Shared);
                        Start(2, player.InternalID, player, own, map, null, PvpTeamPool.Tonight(pool, prime, Random.Shared, player.Realm), PvpTeamPool.World, zones);
                        say($"Prime time{(zones is { Count: > 1 } ? " in " + string.Join(", ", zones.Select(zone => zone.Name)) + "; you are in " + map.Name : " in " + map.Name)}: " +
                            $"{prime} groups of each realm are out, yours one of them, the others guilds of every kind. " +
                            "Some walk in packs, some add, some do neither. Kills earn realm points that are kept, and a wipe puts your group " +
                            "back here (so does /release). /8v8 who, /8v8 go <zone>, /8v8 rivals.");
                        if (own != null)
                            say($"Your group is the line-up {own.Id}.");
                        if (trial > 0)
                            say($"An evening to try things: your seven are at realm rank {PvpRealmPoints.RankName(trial)} and your group earns nothing. " +
                                "Your next real fight or evening puts you back at what you have earned.");
                        if (_maps.Count > 1)
                            say(ZonesLine(_maps, map));
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
            // (A rank picked for an evening holds for the player's own evening, set just before this by StartFor.)
            if (leader == null || guilds == null)
                _trialLevel = 0;
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
                    // The player's group goes by its guild when it has one (/8v8 guild), as every guild of bots does.
                    if (leader != null && start.Realm == leader.Realm)
                        entries.Add((OwnTeamName(leader.GuildName, TeamName(template.Id, 1), guilds.Select(guild => guild.Guild)), own ?? template, true, null));
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
                // The zones of the evening (M11.8). Those that walk alone and the heads of packs take a zone each, the
                // choosiest first: one their kind leans to, and of those rather one with few groups of their realm (ZoneFor).
                // Then each group that stays near others picks a group of its realm to walk with, rather one where the crowd
                // is and none in a clean zone while there is another. The others go where the team they walk with goes.
                var placed = teams.Where(team => team.Leader != null).ToList();
                List<PvpZoneCrowd> crowds = maps.Select(zone => zone.Crowd).ToList();
                // (How many groups of the realm a zone will have: those put there, and those that will follow them there.)
                int[] load = maps.Select(zone => placed.Count(team => team.Realm == realm.Key && team.Map == zone)).ToArray();
                void Takes(PvpRoamTeam team)
                {
                    int pick = ZoneFor(team.Profile.Kind, crowds, load, Random.Shared.Next());
                    load[pick] += 1 + realm.Count(follower => follower.Company == team);
                    Place(team, maps[pick], placed);
                    placed.Add(team);
                }
                foreach (PvpRoamTeam team in realm.Where(team => team.Company == null && team.Profile.Company != PvpCompany.Near).OrderBy(team => Choosiness(team.Profile.Kind)))
                    Takes(team);
                List<PvpRoamTeam> hosts = teams.Where(team => team.Realm == realm.Key &&
                    (team.Leader != null || team.Profile is { Company: PvpCompany.Alone, KeepsToQuietGround: false, HuntsPacks: false })).ToList();
                foreach (PvpRoamTeam near in realm.Where(team => team.Profile.Company == PvpCompany.Near))
                {
                    int host = Weighted(hosts.Select(team => Leans(PvpTeamKind.Clingy, team.Map.Crowd)).ToList(), Random.Shared.Next());
                    if (host < 0 && hosts.Count > 0)
                        host = Random.Shared.Next(hosts.Count);
                    if (host < 0)
                        Takes(near);
                    else
                        near.Company = hosts[host];
                }
                foreach (PvpRoamTeam team in realm.Where(team => team.Company != null))
                {
                    Place(team, team.Company.Map, placed);
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

        /// <summary>
        /// A team's squad for its next fight. A team of bots falls back as one by its own choice
        /// when it has more on it than it can stand against (bots/pvp/PvpKite.cs), toward where
        /// it came into the zone when the ground lets it; the player's group does what the
        /// player calls.
        /// </summary>
        private static PvpSquad NewSquad(PvpRoamTeam team) => new(team.Team.Bots, team.Leader != null ? [team.Leader] : null)
        {
            KitesOnItsOwn = team.Leader == null && team.Profile?.KitesAsOne != false,
            LeadsOnThePlayersWord = team.Leader != null,
            Home = team.Start.Point,
        };

        private static void Spawn(PvpRoamTeam team, long now)
        {
            PvpArena.Remove(team.All);
            team.All.Clear();
            GamePlayer leader = team.Leader;
            // The player's seven are put around the player, who stands at the start; a team of bots around its start.
            team.Team.Bots = leader != null
                ? PvpArena.SpawnCompanions(leader, _trialLevel, team.All, team.Template, _trialLevel == 0).ToList()
                : SpawnBots(team);
            // A guild plays at the level of its kind (PvpSkills), not at the one chosen for "the others".
            if (team.Profile is { } profile)
            {
                // And in the style of its kind (PvpStyles): whom it calls, where it stands, how it heals, controls, peels and interrupts.
                PvpWeights style = PvpStyles.For(profile.Kind, PvpWeights.Current);
                foreach (GameBot bot in team.Team.Bots)
                    (bot.PvpHuman, bot.PvpWeightsSet) = (PvpSkills.WeightsFor(profile.Skill), style);
            }
            if (team.Entry is { } entry)
                (team.Team.Route, team.CameFrom, team.ArrivedAt) = ([entry], null, 0);
            team.Team.Home = team.Start.Point;
            team.Team.Waypoint = 0;
            team.Team.NewMatch(leader);
            team.Team.Squad = NewSquad(team);
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
            (team.DeadSince, team.Reforming, team.GivenUp, team.BrokeOffAt) = (0, false, false, 0);
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
                    // The player has walked into another zone of the evening (the zones of a frontier lie side by side; the
                    // bots do not cross, their ground ends at the border): its group is of that zone now.
                    if (team.Leader is { CurrentZone: { } under } && team.Map.Zone != 0 && under.ID != team.Map.Zone && team.Fighting.Count == 0 &&
                        _maps.FirstOrDefault(zone => zone.Zone == under.ID && zone.Region == team.Map.Region) is { } entered)
                    {
                        Place(team, entered, teams);
                        Say($"{team.Name} walks into {entered.Name}", new { what = "port", team = team.Name, zone = entered.Name });
                        Tell(team, $"You are in {entered.Name}. A wipe or /release puts you at your realm's start here.");
                    }
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
                            wp = team.Team.Waypoint, fighting = team.Fighting.Select(other => other.Name).ToList(), zone = team.Map.Id,
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
            if (team.Reforming || team.GivenUp || IsWiped(team.Team.Bots.Count(bot => bot.IsAlive), team.Leader != null, team.Leader?.IsAlive == true,
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
                        RealmChat(teams, team, SightingText(most.Count(), most.Key, InZone(team.Map, team.Map.PlaceOf(x, y)), false, true), (x, y));
                    }
                    team.Fighting.Clear();
                    // Given up by its player: the ones still standing are gone from the field with the rest.
                    if (team.GivenUp)
                        PvpArena.Remove(team.All);
                    Say($"{team.Name} is wiped (its {team.Wipes}.){(team.GivenUp ? " It gave the fight up." : string.Empty)}",
                        new { what = "wipe", team = team.Name, gaveUp = team.GivenUp ? true : (bool?)null });
                    Tell(team, team.Leader?.IsAlive == true
                        ? $"Your group is down. It stands up here in about {real.WipeStandsUpAfterSeconds} seconds."
                        : $"Your group is wiped. You stand up at your start in about {real.WipeStandsUpAfterSeconds} seconds, or release to go there now.");
                }
                else if (PvpRealWeights.StandsUp((now - team.StateSince) / 1000.0, EnemyNearStart(team, teams, real.StartIsClearBeyond), real,
                             team.Leader is { IsAlive: true }))
                {
                    // (Elsewhere: a zone its kind goes to.)
                    List<PvpRoamMap> elsewhere = team.Profile == null ? [] : _maps.Where(zone => zone != team.Map && Leans(team.Profile.Kind, zone.Crowd) > 0).ToList();
                    if (team.Guild != null && PortsElsewhere(elsewhere.Count + 1, team.Company != null, teams.Any(other => other.Company == team), Random.Shared.Next(300)))
                    {
                        Place(team, elsewhere[ZoneFor(team.Profile.Kind, elsewhere.Select(zone => zone.Crowd).ToList(), elsewhere.Select(zone =>
                            teams.Count(other => other != team && other.Realm == team.Realm && other.Map == zone && other.State != "Away")).ToList(), Random.Shared.Next())], teams);
                        Say($"{team.Name} ports to {team.Map.Name}", new { what = "port", team = team.Name, zone = team.Map.Name });
                    }
                    // A group that stays near others does not cross the zone alone to the one it was with: it picks the nearest.
                    if (team.Profile?.Company == PvpCompany.Near)
                    {
                        PvpRoamTeam nearest = Hosts(teams, team.Realm).Where(host => host != team && host.Map == team.Map && host.Alive > 0)
                            .OrderBy(host => Math.Pow(host.Centre().X - team.Start.Point.X, 2) + Math.Pow(host.Centre().Y - team.Start.Point.Y, 2)).FirstOrDefault();
                        if (nearest != null && nearest != team.Company)
                        {
                            team.Company = nearest;
                            Say($"{team.Name} walks with {nearest.Name} now", new { what = "company", team = team.Name, beside = nearest.Name });
                        }
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
            team.ComingSeen.RemoveWhere(other => other.Alive == 0 || team.Fighting.Count == 0 || !Near(team, other, 9_000));
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
                    // (One that hunts packs does not add either: a pack somebody is on already is theirs.)
                    bool joins = drawn ?? (PaysBack(team.AddedOnByPlayer, other.Leader != null || theirFight.Any(third => third.Leader != null)) ||
                                           Joins(team.Profile?.AddChancePercent ?? real.AddChancePercent, Random.Shared.Next(100)));
                    team.Draws[other] = joins;
                    if (drawn == null)
                    {
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
            // (A player attacks a group when it has struck one of its members: a target alone is a look. PlayerPicks.)
            bool Attacks(PvpRoamTeam other) => other.Team.Members.Any(member => member.IsAlive && member.TargetObject is GameLiving aimed &&
                (member is not GamePlayer || aimed.attackComponent.AttackerTracker.ContainsAttacker(member)) &&
                team.Team.Members.Contains(aimed) && member.IsWithinRadius(aimed, 2_000));
            // The enemy group the player has picked for its own (PlayerPicks): its target, or the one it has called on.
            bool Picks(PvpRoamTeam other)
            {
                if (team.Leader is not { } player)
                    return false;
                GameLiving aimed = player.TargetObject as GameLiving;
                bool ofThatGroup = aimed is { IsAlive: true } && other.Team.Members.Contains(aimed) && player.IsWithinRadius(aimed, PvpFormation.FormRange);
                return PlayerPicks(ofThatGroup, team.Fighting.Any(fought => fought != other && fought.Alive > 0),
                    ofThatGroup && aimed.attackComponent.AttackerTracker.ContainsAttacker(player),
                    team.Team.Squad?.CalledByPlayer is { } called && other.Team.Members.Contains(called));
            }
            double FromItsStart(PvpRoamTeam other)
            {
                (double x, double y) = other.Centre();
                return Math.Sqrt(Math.Pow(x - other.Start.Point.X, 2) + Math.Pow(y - other.Start.Point.Y, 2));
            }
            List<PvpRoamTeam> foes = others.Where(other =>
            {
                // A team that has broken off is let go: a team of bots follows it only so far from where it broke off.
                if (team.Leader == null && other.Team.Squad is { Leaving: true } gone)
                {
                    (double x, double y) = other.Centre();
                    if (!PvpKite.Followed(Math.Sqrt(Math.Pow(x - gone.LeftFrom.X, 2) + Math.Pow(y - gone.LeftFrom.Y, 2)), PvpWeights.Of(team.Team.Bots.FirstOrDefault()).Kite))
                        return false;
                }
                bool attacks = Attacks(other);
                // An enemy group that comes for its fight is seen before it lands (SeesItComing), and is an enemy from then.
                if (team.Leader == null && !attacks && (team.ComingSeen.Contains(other) ||
                        SeesItComing(team.Fighting.Count, team.Fighting.Contains(other), other.Draws.TryGetValue(team, out bool comesForIt) && comesForIt,
                            Near(team, other, AddSeenWithin) && Sees(team, other, AddSeenWithin))))
                {
                    if (team.ComingSeen.Add(other))
                        Say($"{team.Name} sees {other.Name} coming for its fight", new { what = "addseen", team = team.Name, other = other.Name });
                    attacks = true;
                }
                if (LeavesAlone(team.Draws.TryGetValue(other, out bool joins) && !joins, other.Draws.TryGetValue(team, out bool theyJoin) && !theyJoin, attacks,
                        Picks(other)))
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
                    return PlayerTakesItOn(team.Fighting.Contains(other), attacks, Picks(other));
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

            // The player's own group says what it sees coming, and what comes into its fight.
            // (In an evening of bots with a realm listened to, one team of that realm stands in for it: StandsIn.)
            if (team.Leader != null || StandsIn(team, teams))
                TellOfIncoming(team, others, now);

            // An elite team keeps its fights clean: once a second enemy group is in its fight, it leaves (PvpTeamProfile.LeavesDirtyFights).
            if (team.Leader == null && team.Profile?.LeavesDirtyFights == true && team.ContactTick != 0 && !team.Team.Squad.Leaving &&
                PvpKite.FightIsDirty(team.Fighting.Count(other => other.Alive > 0) + team.ComingSeen.Count(other => other.Alive > 0 && !team.Fighting.Contains(other))) &&
                team.Team.Squad.LeaveNow(enemies))
                Say($"{team.Name} leaves a fight that is no longer clean", new { what = "dirty", team = team.Name, foes = team.Fighting.Count });

            Pay(team, real, now);

            // A team of bots that has lost breaks off (bots/pvp/PvpKite.cs): it runs. Once no enemy team is near it any more it
            // goes back to its start to form up again, as a team does that cannot raise its dead; one that is still being
            // fought when its run is over turns and fights.
            if (team.Leader == null && team.Team.Squad.Leaving)
            {
                PvpKiteWeights kite = PvpWeights.Of(team.Team.Bots.FirstOrDefault()).Kite;
                if (team.BrokeOffAt == 0)
                {
                    (team.BrokeOffAt, team.LastFollowedTick) = (now, now);
                    Say($"{team.Name} breaks off with {team.Alive} left", new { what = "breakoff", team = team.Name, alive = team.Alive, foes = others.Count });
                    // On a prime-time evening the realm hears of it: from the group that runs, or from the group it runs from.
                    if (Prime && team.Fighting.Count > 0)
                    {
                        eRealm hears = Hears(teams);
                        (double atX, double atY) = team.Centre();
                        string place = InZone(team.Map, team.Map.PlaceOf(atX, atY));
                        if (team.Realm == hears)
                        {
                            IGrouping<eRealm, PvpRoamTeam> most = team.Fighting.GroupBy(other => other.Realm).OrderByDescending(realm => realm.Count()).First();
                            RealmChat(teams, team, RunningText(most.Count(), most.Key, place), (atX, atY));
                        }
                        else if (team.Fighting.FirstOrDefault(other => other.Realm == hears && other.Leader == null && other.Alive > 0) is { } winner)
                            RealmChat(teams, winner, OnTheRunText(team.Alive, team.Realm, place), (atX, atY));
                    }
                }
                if (others.Any(other => Near(team, other, kite.ClearBeyond)))
                    team.LastFollowedTick = now;
                bool runIsOver = PvpKite.RunIsOver((now - team.BrokeOffAt) / 1000.0, kite);
                // (Not taken off the field where a player would see it go: it runs on until it is out of sight.)
                bool inSight = teams.Any(other => other.Leader != null && other.Map == team.Map && Near(team, other, kite.NotGoneWithAPlayerWithin));
                if (PvpKite.FormsUpAgain(PvpKite.GotAway((now - team.LastFollowedTick) / 1000.0, kite) || runIsOver && !inCombat, inSight))
                {
                    team.Reforming = true;
                    Say($"{team.Name} gets away with {team.Alive} and goes back to its start", new { what = "away", team = team.Name, alive = team.Alive });
                }
                else if (runIsOver && inCombat)
                {
                    team.Team.Squad.StopLeaving();
                    Say($"{team.Name} cannot get away and turns to fight", new { what = "caught", team = team.Name, alive = team.Alive });
                }
                else
                    team.Team.Squad.Decide(enemies);
                return;
            }

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
                team.Team.Squad = NewSquad(team);
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
                // Who of its realm fights beside it: a group counts them on its side when it weighs what is on it.
                team.Team.Squad.Beside = teams.Where(own => own != team && own.Realm == team.Realm && own.Alive > 0 && !own.Reforming &&
                        own.Map == team.Map && Near(team, own, PvpSquad.BesideWithin))
                    .SelectMany(own => own.Team.Members).Where(member => member.IsAlive).ToList();
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
            // A guild that walks with its company goes where that goes; one on its own waits a while (GivesUpWaiting), then
            // turns back the way it came or walks on, and does not stand by that fight again.
            bool leftAFightNear = team.Draws.Any(draw => !draw.Value && draw.Key.Alive > 0 && Near(team, draw.Key, team.Waiting ? 12_000 : 9_000));
            if (!leftAFightNear)
                (team.GaveUpWaiting, team.AroundSide) = (false, 0);
            bool waits = WaitsOutAFight(leftAFightNear, enemies.Count, team.Leader != null || team.Entry != null && (team.GaveUpWaiting || LeadOf(team, teams) != null));
            if (waits && team.Waiting && team.Entry != null && GivesUpWaiting((now - team.WaitingSince) / 1000.0))
            {
                (team.GaveUpWaiting, team.GaveUpAt, team.AroundSide, waits) = (true, now, 0, false);
                bool turned = TurnsAway(team);
                Say($"{team.Name} {(turned ? "turns away" : "walks on, round the fight")}", new { what = "turn", team = team.Name, turned });
            }
            if (waits != team.Waiting)
            {
                team.Waiting = waits;
                if (waits)
                {
                    team.WaitingSince = now;
                    Say($"{team.Name} waits where it is", new { what = "wait", team = team.Name });
                }
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
                PvpRoamTeam lead = LeadOf(team, teams);
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
                // (A group that falls back as one is not marched back at the enemy it is falling back from.)
                // (One that has said it joins a fight goes to it while it sees nobody: FightItJoined.)
                else if (!team.Team.Squad.Kiting)
                {
                    // One that has given up waiting by a fight it left alone goes round that fight, not through it (AroundAFight).
                    (int X, int Y)? round = null;
                    if (team.GaveUpWaiting && leftAFightNear && enemies.Count == 0 && team.Team.Route is { Length: > 0 } road &&
                        now - team.GaveUpAt < GoesRoundForSeconds * 1_000L)
                    {
                        List<(double X, double Y)> left = team.Draws.Where(draw => !draw.Value && draw.Key.Alive > 0 && Near(team, draw.Key, 12_000))
                            .SelectMany(draw => draw.Key.Fighting.Where(other => other.Alive > 0).Append(draw.Key)).Distinct().Select(other => other.Centre()).ToList();
                        if (left.Count > 0)
                        {
                            (int X, int Y) next = road[team.Team.Waypoint % road.Length];
                            round = AroundAFight(team.Centre(), (next.X, next.Y), (left.Average(centre => centre.X), left.Average(centre => centre.Y)), ref team.AroundSide);
                        }
                    }
                    team.Team.March(enemies, team.Start.Point, null, null, team.ContactTick,
                        FightItJoined(foes.Select(other => (other.Centre(), team.Draws.TryGetValue(other, out bool joins) && joins)), team.Centre()) ?? round);
                }
            }
            if (enemies.Count > 0)
            {
                team.Team.Squad.ClearPets();
                team.Team.Squad.StandOff();
                team.Team.Squad.KeepApart();
            }
        }

        /// <summary>
        /// What the player's seven tell the player in group chat of the enemy groups about
        /// (bots/pvp/PvpCalls.cs): out of a fight, the groups of a realm they see within
        /// <see cref="SightingWithin"/> ("INC! 2FG MIDS NORTH-WEST, 4,500!"); in a fight, each
        /// further group that comes into it or is seen coming for it ("ADD! 8 MORE MIDS FROM THE
        /// NORTH!"). They see as they do when they march: farther ahead than behind, and not
        /// through a hill. Never throws: a line of chat must not be able to stop an evening.
        /// </summary>
        private static void TellOfIncoming(PvpRoamTeam team, List<PvpRoamTeam> others, long now)
        {
            try
            {
                List<GameBot> watchers = team.Team.Bots.Where(bot => bot.IsAlive).ToList();
                if (watchers.Count == 0 || team.Team.Squad is not { } squad)
                    return;
                // (For the team that stands in for a player's in an evening of bots, its first member is where "you" are.)
                GameLiving player = (GameLiving)team.Leader ?? watchers[0];
                PvpWeights sight = PvpWeights.Of(watchers[0]);
                bool Seen(PvpRoamTeam other) => Near(team, other, SightingWithin + 1_500) && other.Team.Members.Any(member =>
                    member.IsAlive && watchers.Any(bot => PvpSight.Sees(bot, member, SightingWithin, sight)));
                (string Side, double Away) From(IEnumerable<PvpRoamTeam> them)
                {
                    GameLiving nearest = them.SelectMany(other => other.Team.Members).Where(member => member.IsAlive)
                        .OrderBy(member => member.GetDistanceTo(player)).First();
                    return (Compass(nearest.X - player.X, nearest.Y - player.Y), nearest.GetDistanceTo(player));
                }
                if (team.ContactTick != 0 && team.Fighting.Count > 0)
                {
                    // The groups the fight began with are no news.
                    if (team.AddsCalled.Count == 0)
                        team.AddsCalled.UnionWith(team.Fighting);
                    if (now - team.AddCalledAt < PvpCalls.AddEveryMilliseconds)
                        return;
                    // (A group that comes for it, not one the player's group has turned on: in the owner's brawl of 2026-11-24
                    // three groups it had gone for itself were called as adds, twelve lines in fourteen seconds.)
                    bool AimsAtIt(PvpRoamTeam other) => other.Team.Members.Any(member => member.IsAlive && member.TargetObject is GameLiving aimed &&
                        team.Team.Members.Contains(aimed));
                    List<PvpRoamTeam> adds = others.Where(other => other.Alive > 0 && !team.AddsCalled.Contains(other) &&
                        (team.Fighting.Contains(other) && AimsAtIt(other) || other.Draws.TryGetValue(team, out bool joins) && joins && Seen(other))).ToList();
                    if (adds.Count == 0)
                        return;
                    team.AddCalledAt = now;
                    team.AddsCalled.UnionWith(adds);
                    IGrouping<eRealm, PvpRoamTeam> most = adds.GroupBy(other => other.Realm).OrderByDescending(realm => realm.Sum(other => other.Alive)).First();
                    string text = PvpCalls.Add(most.Sum(other => other.Alive), Short(most.Key), From(most).Side);
                    squad.Tell("add", text, PvpCalls.LoudTimes);
                    Say($"{team.Name} is told: {text}", new { what = "addcall", team = team.Name, others = adds.Select(other => other.Name).ToList(), text });
                    return;
                }
                team.AddsCalled.Clear();
                foreach (IGrouping<eRealm, PvpRoamTeam> seen in others.Where(other => other.Alive > 0 && Seen(other)).GroupBy(other => other.Realm))
                {
                    bool known = team.Sighted.TryGetValue(seen.Key, out (long When, int Groups) last);
                    if (!PvpCalls.SaysSighting(known ? (now - last.When) / 1000.0 : -1, last.Groups, seen.Count()))
                        continue;
                    team.Sighted[seen.Key] = (now, seen.Count());
                    (string side, double away) = From(seen);
                    string text = PvpCalls.Sighted(seen.Count(), Short(seen.Key), side, away);
                    squad.Tell("inc-" + seen.Key, text, 2);
                    Say($"{team.Name} is told: {text}", new { what = "inccall", team = team.Name, others = seen.Select(other => other.Name).ToList(), text });
                }
            }
            catch (Exception exception)
            {
                Log.Warn("8v8 roam: telling the player's group of incoming failed", exception);
            }
        }

        // The team of bots that stands in for a player's group in an evening of bots (StandsIn).
        private static PvpRoamTeam _standIn;

        /// <summary>
        /// In an evening of bots with a realm listened to (roam-start Listener=hibernia) one
        /// team of that realm that walks alone stands in for a player's group, so that what a
        /// player's seven would say of the enemy groups they see (TellOfIncoming) is worked out
        /// and written to the recording ("inccall", "addcall"); nothing is said to anybody,
        /// since no player is in it. Without it, only a player's own evening ran that code.
        /// </summary>
        private static bool StandsIn(PvpRoamTeam team, List<PvpRoamTeam> teams)
        {
            if (_listener == eRealm.None || teams.Any(other => other.Leader != null))
                return false;
            if (_standIn == null || !teams.Contains(_standIn))
                _standIn = teams.FirstOrDefault(other => other.Realm == _listener && other.State != "Away" && other.Profile is { Company: PvpCompany.Alone });
            return team == _standIn;
        }

        /// <summary>The groups of a realm that a group which stays near others may pick: the player's, and the guilds that walk alone on the open ground.</summary>
        private static List<PvpRoamTeam> Hosts(IEnumerable<PvpRoamTeam> teams, eRealm realm) =>
            teams.Where(team => team.Realm == realm && (team.Leader != null || team.Profile is { Company: PvpCompany.Alone, KeepsToQuietGround: false, HuntsPacks: false })).ToList();

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
                                                                next.Count(other => other.Company == team) < PvpTeamPool.PackMost - 1);
                else if (profile.Company == PvpCompany.Near && Hosts(next, fresh.Realm).Where(host => host.Map == fresh.Map).ToList() is { Count: > 0 } hosts)
                    fresh.Company = hosts[Random.Shared.Next(hosts.Count)];
                next.Add(fresh);
                Say($"{fresh.Name} will come out", new { what = "join", team = fresh.Name, kind = profile.Kind.ToString(), realm = NameOf(fresh.Realm), beside = fresh.Company?.Name });
            }
            _teams = next;
        }

        /// <summary>On the road: out, alive, and not gone back to its start.</summary>
        private static bool OnTheRoad(PvpRoamTeam team) => team is { Reforming: false, State: "Roaming" or "Fighting" } && team.Alive > 0;

        /// <summary>
        /// The team a team walks behind now; null when it walks by itself. A group that stays near
        /// others: its group, while that is on the road. A pack: whoever of it is in front, which
        /// is its head until the head is down, then the first of the others that is on the road,
        /// and that one stays in front until it is down itself (the head, back from its start,
        /// walks to it like any other). In an evening of 25 minutes (2026-11-22) a pack came
        /// apart whenever its head was wiped: every follower went its own way, and then each
        /// crossed the zone alone to a head that had just set off again. Followers were within
        /// reach of their head 0 to 23% of the time in two packs of three.
        /// </summary>
        private static PvpRoamTeam LeadOf(PvpRoamTeam team, List<PvpRoamTeam> teams)
        {
            if (team.Profile?.Company != PvpCompany.Zerg)
                return team.Company != null && OnTheRoad(team.Company) ? team.Company : null;
            PvpRoamTeam head = team.Company ?? team;
            if (head.PackLead == null || !OnTheRoad(head.PackLead) || !teams.Contains(head.PackLead) || head.PackLead.Map != team.Map)
                head.PackLead = teams.Where(mate => mate == head || mate.Company == head).OrderBy(mate => mate == head ? 0 : 1).FirstOrDefault(OnTheRoad);
            return head.PackLead == team ? null : head.PackLead;
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
            if (team.Entry == null || team.CameFrom is not { } back || team.Team.Route is not { Length: 1 } route)
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
            PvpRoamTeam head = team.Company ?? team;
            if (PackWaits(team.Profile?.Company == PvpCompany.Zerg && teams.Any(mate => mate != team && (mate.Company ?? mate) == head &&
                    mate.Profile?.Company == PvpCompany.Zerg && mate.Alive > 0 && mate.State == "Roaming" && Near(team, mate, 9_000) && !Near(team, mate, 3_000)),
                    (now - team.ArrivedAt) / 1000.0))
                return true;
            // The head of a pack keeps to the road, where the fights are, and so do those who hunt it; an elite group to the quiet ground.
            int leaning = team.Profile is { KeepsToQuietGround: true } ? 1 : team.Profile is { Company: PvpCompany.Zerg } or { HuntsPacks: true } ? -1 : 0;
            (int X, int Y) next = NextPoint(team.Map, at, team.CameFrom, leaning, Random.Shared.Next());
            // A team that hunts packs goes toward the nearest groups of an enemy realm that walk together in its zone
            // (the realm's chat says where they are), and that nobody is fighting yet.
            PvpRoamTeam prey = team.Profile is { HuntsPacks: true }
                ? teams.Where(other => other.Realm != team.Realm && other.Map == team.Map && OnTheRoad(other) && other.Fighting.Count == 0 && Together(other, teams) >= 2)
                    .OrderBy(other => Math.Pow(other.Centre().X - x, 2) + Math.Pow(other.Centre().Y - y, 2)).FirstOrDefault()
                : null;
            if (prey != null)
                next = PointToward(team.Map, at, prey.Centre());
            // A guild with a grudge goes toward the player's group.
            // (In its own zone: it does not port after it.)
            PvpRoamTeam hunted = teams.FirstOrDefault(mine => mine.Leader != null && mine.Alive > 0 && mine.Map == team.Map);
            bool looking = hunted != null && ComesLooking(team.WipedByPlayer, team.AddedOnByPlayer);
            if (looking)
                next = PointToward(team.Map, at, hunted.Centre());
            Say($"{team.Name} goes on{(looking ? ", looking for the player's group" : prey != null ? $", after {prey.Name} and those with it" : string.Empty)}",
                new { what = "leg", team = team.Name, from = new[] { at.X, at.Y }, to = new[] { next.X, next.Y }, looking, prey = prey?.Name });
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
                    // (On an evening at a picked rank the player's group earns nothing: the kill is counted and not paid.)
                    if (!Earns(byTeam.Key.Leader != null, _trialLevel))
                    {
                        byTeam.Key.Kills++;
                        continue;
                    }
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

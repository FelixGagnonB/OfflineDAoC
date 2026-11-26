using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// The nine frontier zones behind the three a realm ported to (M11.8): Breifine, Cruachan
    /// Gorge and Mount Collory behind Emain Macha, the Jamtland Mountains, Uppland and Yggdra
    /// Forest behind Odin's Gate, the Pennine Mountains, Forest Sauvage and Snowdonia behind
    /// Hadrian's Wall. The owner, 2026-11-23: "adding more zones and spreading teams (and type of
    /// teams) across them all". They are the clean zones (<see cref="PvpZoneCrowd.Clean"/>).
    /// <para>
    /// Each was laid out on the installed navmesh on 2026-11-23 by source/tools/pvp_map_pick.py
    /// and looked at on the client's map (source/tools/pvp_map_draw.py): up to nine points at
    /// least 18,000 apart on the one piece of ground that can be walked, none within 5,000 of a
    /// keep (a keep's guards leave the bots alone, not a player of another realm); a leg where
    /// the way on foot is within 13% of the straight line and does not pass within 3,500 of a
    /// keep. The realm of the zone starts 2,800 in front of one of its keeps. The two others had
    /// no keep and no porter there: each comes in at a walkable place off the legs, the three
    /// starts as far from one another as the zone allows. A bot does not walk from one zone into
    /// the next (its ground ends at the border), so each zone is a map of its own, as the three
    /// first are. The names of the points are the keep they are near, or the side of the zone.
    /// </para>
    /// </summary>
    public static class PvpRoamZones
    {
        private static (int X, int Y) At((int X, int Y, string Name)[] points, int index) => (points[index].X, points[index].Y);

        private static PvpRoamMap Zone(string id, string name, string tag, ushort region, ushort zone, (int X, int Y, string Name)[] points,
            (int A, int B)[] ways, int quiet, (eRealm Realm, int X, int Y, bool AtKeep, int First, int Second)[] starts) =>
            new(id, name, region,
                starts.Select(start => new PvpRoamStart(start.Realm, (start.X, start.Y), [At(points, start.First)], [At(points, start.Second)], start.AtKeep)).ToList(),
                ways.Select(way => (At(points, way.A), At(points, way.B))).ToList(),
                [At(points, quiet)],
                points.Select(point => ((point.X, point.Y), point.Name)).ToList(),
                PvpZoneCrowd.Clean, tag, zone);

        // Breifine (region 200, zone 212): 9 points, 13 legs of 15,652 to 25,239 units,
        // Hibernia in front of Dun Bolg, the three starts 49,365 apart at the least.
        public static readonly PvpRoamMap Breifine = Zone("breifine", "Breifine", "Breifine", 200, 212,
            [(389792, 385024, "west field"), (403792, 357024, "Bolg"), (403792, 378024, "Bolg hills"), (410792, 399024, "nGed"), (417792, 364024, "north field"), (417792, 385024, "middle"), (417792, 413024, "south field"), (431792, 378024, "Crimthain"), (438792, 392024, "east field")],
            [(2, 0), (2, 4), (3, 0), (3, 2), (3, 6), (4, 1), (5, 2), (5, 3), (5, 4), (5, 7), (5, 8), (7, 4), (7, 8)],
            6,
            [(eRealm.Hibernia, 402982, 364049, true, 1, 4), (eRealm.Midgard, 445792, 399024, false, 8, 7), (eRealm.Albion, 396792, 413024, false, 3, 6)]);

        // Cruachan Gorge (region 200, zone 211): 9 points, 15 legs of 15,652 to 21,000 units,
        // Hibernia in front of Dun Ailinne, the three starts 45,550 apart at the least.
        public static readonly PvpRoamMap CruachanGorge = Zone("cruachan", "Cruachan Gorge", "Cruachan", 200, 211,
            [(338256, 378024, "west field"), (338256, 399024, "south-west field"), (345256, 364024, "Lamfhota"), (352256, 385024, "middle"), (359256, 371024, "north field"), (359256, 399024, "south field"), (373256, 364024, "north-east field"), (373256, 385024, "east field"), (373256, 406024, "Ailinne")],
            [(0, 1), (0, 2), (3, 0), (3, 1), (3, 4), (3, 5), (3, 7), (4, 2), (4, 6), (4, 7), (5, 1), (5, 7), (5, 8), (7, 6), (7, 8)],
            6,
            [(eRealm.Hibernia, 367084, 397514, true, 5, 8), (eRealm.Midgard, 345256, 357024, false, 2, 4), (eRealm.Albion, 324256, 413024, false, 1, 5)]);

        // Mount Collory (region 200, zone 210): 8 points, 10 legs of 19,799 to 25,239 units,
        // Hibernia in front of Dun Scathaig, the three starts 41,283 apart at the least.
        public static readonly PvpRoamMap MountCollory = Zone("collory", "Mount Collory", "Collory", 200, 210,
            [(389792, 457560, "west field"), (396792, 436560, "north-west field"), (410792, 450560, "Scathaig"), (410792, 471560, "Dagda"), (417792, 429560, "north field"), (431792, 450560, "east field"), (438792, 429560, "north-east field"), (438792, 471560, "south-east field")],
            [(1, 0), (2, 0), (2, 1), (2, 3), (2, 5), (4, 1), (4, 6), (5, 4), (5, 6), (5, 7)],
            3,
            [(eRealm.Hibernia, 414219, 445279, true, 2, 4), (eRealm.Midgard, 389792, 478560, false, 0, 3), (eRealm.Albion, 445792, 478560, false, 7, 5)]);

        // Jamtland Mountains (region 100, zone 113): 9 points, 10 legs of 19,799 to 25,239 units,
        // Midgard in front of Glenlock Faste, the three starts 49,877 apart at the least.
        public static readonly PvpRoamMap Jamtland = Zone("jamtland", "Jamtland Mountains", "Jamtland", 100, 113,
            [(660128, 624976, "north-west field"), (667128, 659976, "south-west field"), (674128, 638976, "west field"), (681128, 617976, "Nottmoor"), (695128, 638976, "middle"), (695128, 659976, "south field"), (702128, 617976, "Blendrake"), (716128, 638976, "east field"), (716128, 659976, "Glenlock")],
            [(2, 0), (2, 1), (3, 0), (3, 6), (4, 2), (4, 3), (4, 5), (4, 7), (6, 7), (7, 8)],
            5,
            [(eRealm.Midgard, 709395, 659202, true, 8, 5), (eRealm.Hibernia, 681128, 610976, false, 3, 6), (eRealm.Albion, 660128, 666976, false, 1, 2)]);

        // Uppland (region 100, zone 111): 8 points, 10 legs of 21,000 to 25,239 units,
        // Midgard in front of Fensalir Faste, the three starts 47,201 apart at the least.
        public static readonly PvpRoamMap Uppland = Zone("uppland", "Uppland", "Uppland", 100, 111,
            [(725664, 659976, "south-west field"), (732664, 638976, "west field"), (739664, 617976, "north-west field"), (753664, 638976, "middle"), (753664, 659976, "south field"), (760664, 617976, "north field"), (774664, 652976, "south-east field"), (781664, 631976, "Mjollner")],
            [(1, 0), (1, 2), (3, 1), (3, 2), (3, 4), (3, 5), (3, 6), (5, 2), (6, 4), (6, 7)],
            7,
            [(eRealm.Midgard, 742628, 657113, true, 4, 0), (eRealm.Hibernia, 732664, 610976, false, 2, 1), (eRealm.Albion, 781664, 610976, false, 7, 5)]);

        // Yggdra Forest (region 100, zone 112): 8 points, 13 legs of 19,799 to 25,239 units,
        // Midgard in front of Arvakr Faste, the three starts 52,281 apart at the least.
        public static readonly PvpRoamMap YggdraForest = Zone("yggdra", "Yggdra Forest", "Yggdra", 100, 112,
            [(660128, 683512, "north-west field"), (667128, 704512, "Grallarhorn"), (681128, 683512, "north field"), (681128, 725512, "south field"), (688128, 704512, "middle"), (702128, 690512, "Arvakr"), (702128, 718512, "south-east field"), (716128, 704512, "east field")],
            [(1, 0), (1, 2), (1, 3), (2, 0), (4, 1), (4, 2), (4, 3), (4, 5), (4, 6), (5, 2), (5, 7), (6, 3), (6, 7)],
            0,
            [(eRealm.Midgard, 700241, 682703, true, 5, 2), (eRealm.Hibernia, 660128, 718512, false, 1, 3), (eRealm.Albion, 716128, 732512, false, 6, 7)]);

        // Pennine Mountains (region 1, zone 14): 8 points, 10 legs of 15,652 to 25,239 units,
        // Albion in front of Caer Erasleigh, the three starts 49,348 apart at the least.
        public static readonly PvpRoamMap Pennine = Zone("pennine", "Pennine Mountains", "Pennine", 1, 14,
            [(570016, 371024, "north-west field"), (584016, 378024, "west field"), (584016, 406024, "south-west field"), (598016, 364024, "north field"), (598016, 385024, "middle"), (605016, 399024, "south field"), (612016, 378024, "Erasleigh"), (619016, 392024, "east field")],
            [(1, 0), (4, 1), (4, 2), (4, 3), (4, 5), (4, 6), (4, 7), (5, 7), (6, 5), (6, 7)],
            3,
            [(eRealm.Albion, 618712, 372019, true, 6, 7), (eRealm.Hibernia, 570016, 364024, false, 0, 1), (eRealm.Midgard, 591016, 413024, false, 2, 5)]);

        // Forest Sauvage (region 1, zone 11): 7 points, 9 legs of 19,799 to 22,136 units,
        // Albion in front of Caer Renaris, the three starts 43,943 apart at the least.
        public static readonly PvpRoamMap ForestSauvage = Zone("sauvage", "Forest Sauvage", "Sauvage", 1, 11,
            [(577016, 443560, "west field"), (584016, 464560, "south-west field"), (591016, 429560, "Renaris"), (598016, 450560, "middle"), (612016, 436560, "north-east field"), (612016, 464560, "south-east field"), (626016, 450560, "east field")],
            [(1, 0), (2, 0), (3, 0), (3, 1), (3, 2), (3, 4), (3, 5), (4, 6), (5, 6)],
            1,
            [(eRealm.Albion, 582677, 429821, true, 2, 0), (eRealm.Hibernia, 626016, 422560, false, 4, 6), (eRealm.Midgard, 626016, 471560, false, 5, 6)]);

        // Snowdonia (region 1, zone 12): 7 points, 10 legs of 19,799 to 25,239 units,
        // Albion in front of Caer Hurbury, the three starts 43,140 apart at the least.
        public static readonly PvpRoamMap Snowdonia = Zone("snowdonia", "Snowdonia", "Snowdonia", 1, 12,
            [(511480, 335872, "west field"), (511480, 356872, "south-west field"), (525480, 321872, "north field"), (532480, 342872, "middle"), (546480, 321872, "north-east field"), (553480, 342872, "Hurbury"), (553480, 363872, "south-east field")],
            [(0, 1), (2, 0), (2, 4), (3, 0), (3, 1), (3, 2), (3, 4), (3, 5), (4, 5), (5, 6)],
            6,
            [(eRealm.Albion, 548926, 338435, true, 5, 4), (eRealm.Hibernia, 504480, 356872, false, 1, 0), (eRealm.Midgard, 518480, 307872, false, 2, 0)]);

        // Agramon (region 163, zone 163), the island in the middle of the New Frontiers that no realm holds: 10 points, 17
        // legs of 9,899 to 15,718 units, seven of them at its middle; Midgard comes in from the north (17,603 to its first
        // point), Hibernia from the south-west (16,337), Albion from the south-east (16,774), the three starts 42,000 apart
        // at the least. No keep, no monster. (Three points and three legs at first: the owner, on it with two other groups
        // of its realm, 2026-11-25, "it looks like we are running the same triangle ... Could we add more routes ?".)
        // It is no zone of an evening by itself: it is ground for the 8v8 zone (PvpRealWeights.EightZone "agramon"). The
        // game as shipped has no navmesh for it: source/development-tools/OpenDAoC-BuildNav makes zone163.nav from the
        // client's files (two and a half minutes), and the map was laid out on that, 2026-11-25 (pvp_map_pick.py).
        public static readonly PvpRoamMap Agramon = Zone("agramon", "Agramon", "Agramon", 163, 163,
            [(510288, 451752, "far north-west"), (510288, 465752, "west field"), (517288, 458752, "north-west field"), (517288, 472752, "south-west field"),
             (524288, 465752, "middle"), (524288, 479752, "south field"), (531288, 458752, "north-east field"), (531288, 472752, "south-east field"),
             (538288, 451752, "far north-east"), (538288, 465752, "east field")],
            [(0, 1), (1, 2), (1, 4), (2, 4), (2, 6), (3, 4), (3, 5), (3, 7), (4, 5), (4, 6), (4, 7), (4, 9), (5, 7), (6, 7), (6, 8), (6, 9), (8, 9)],
            0,
            [(eRealm.Midgard, 531288, 437752, false, 8, 6), (eRealm.Hibernia, 503288, 479752, false, 1, 3), (eRealm.Albion, 545288, 479752, false, 7, 9)]);

        /// <summary>The nine, by frontier: Hibernia's, Midgard's, Albion's.</summary>
        public static readonly IReadOnlyList<PvpRoamMap> All = [Breifine, CruachanGorge, MountCollory, Jamtland, Uppland, YggdraForest, Pennine, ForestSauvage, Snowdonia];
    }
}

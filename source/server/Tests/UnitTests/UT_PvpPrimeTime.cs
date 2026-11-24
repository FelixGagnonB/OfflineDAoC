using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// A prime-time evening (docs/PVP-8V8-ROADMAP.md, M11): the owner, 2026-11-22, on the teams
    /// that were out in the early 2000s, their kinds and a ratio between them.
    /// </summary>
    [TestFixture]
    public class UT_PvpPrimeTime
    {
        [Test]
        public void TheKindsAreInTheOwnersRatio()
        {
            // "like 5% elite, 15% strong, 20% decent, 30% casual and 30% bad, or something like that"
            Assert.That(PvpTeamProfile.All.Sum(profile => profile.SharePercent), Is.EqualTo(100));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Elite).SharePercent, Is.EqualTo(5));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Strong).SharePercent, Is.EqualTo(15));
            // (The hunters, whom the owner named a day later, have their five from the decent.)
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Decent).SharePercent + PvpTeamProfile.Of(PvpTeamKind.Hunter).SharePercent, Is.EqualTo(20));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Hunter).SharePercent, Is.EqualTo(5));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Clingy).SharePercent + PvpTeamProfile.Of(PvpTeamKind.Adder).SharePercent, Is.EqualTo(30), "the two casual kinds");
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Bad).SharePercent, Is.EqualTo(30));

            Dictionary<PvpTeamKind, int> twenty = PvpTeamPool.Quotas(20);
            Assert.That(twenty[PvpTeamKind.Bad], Is.EqualTo(6));
            Assert.That(twenty[PvpTeamKind.Decent], Is.EqualTo(3));
            Assert.That(twenty[PvpTeamKind.Hunter], Is.EqualTo(1));
            Assert.That(twenty[PvpTeamKind.Strong], Is.EqualTo(3));
            Assert.That(twenty[PvpTeamKind.Elite], Is.EqualTo(1));
            foreach (int count in new[] { 1, 3, 4, 7, 8, 12, 16, 26 })
            {
                Assert.That(PvpTeamPool.Quotas(count).Values.Sum(), Is.EqualTo(count), count.ToString());
                Assert.That(PvpTeamPool.Quotas(count, new Random(count)).Values.Sum(), Is.EqualTo(count), "drawn: " + count);
            }
            // Among eight there is an elite team some evenings, and not never.
            int withElite = Enumerable.Range(0, 400).Count(seed => PvpTeamPool.Quotas(8, new Random(seed))[PvpTeamKind.Elite] > 0);
            Assert.That(withElite, Is.InRange(40, 240));
        }

        [Test]
        public void WhatEachKindIsLike()
        {
            // "Bad group (those groups running with more than 1 other group ... sub optimal composition)"
            PvpTeamProfile bad = PvpTeamProfile.Of(PvpTeamKind.Bad);
            Assert.That(bad.Company, Is.EqualTo(PvpCompany.Zerg));
            Assert.That(bad.Lineup, Is.EqualTo(PvpLineup.Random));
            Assert.That(bad.CountsHeads, Is.False);
            // "Casual that were roaming but staying close to other groups"
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Clingy).Company, Is.EqualTo(PvpCompany.Near));
            // "Casual 8v8 that would not hesitate to add and collect rps"
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Adder).AddChancePercent, Is.GreaterThanOrEqualTo(75));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Adder).Company, Is.EqualTo(PvpCompany.Alone));
            // "Decent 8v8 teams that would not add", "Strong 8v8 teams", "Elite 8v8 teams ... to avoid not clean fights"
            foreach (PvpTeamKind kind in new[] { PvpTeamKind.Decent, PvpTeamKind.Strong, PvpTeamKind.Elite })
            {
                Assert.That(PvpTeamProfile.Of(kind).AddChancePercent, Is.Zero, kind.ToString());
                Assert.That(PvpTeamProfile.Of(kind).Lineup, Is.EqualTo(PvpLineup.Meta), kind.ToString());
                Assert.That(PvpTeamProfile.Of(kind).CountsHeads, Is.True, kind.ToString());
            }
            Assert.That(PvpTeamProfile.All.Where(profile => profile.KeepsToQuietGround).Select(profile => profile.Kind), Is.EqualTo(new[] { PvpTeamKind.Elite }));
            // "There was 8v8 elite team that would run to these 'zerg' (many teams running together) to kill them"
            PvpTeamProfile hunter = PvpTeamProfile.Of(PvpTeamKind.Hunter);
            PvpTeamProfile elite = PvpTeamProfile.Of(PvpTeamKind.Elite);
            Assert.That(PvpTeamProfile.All.Where(profile => profile.HuntsPacks).Select(profile => profile.Kind), Is.EqualTo(new[] { PvpTeamKind.Hunter }));
            Assert.That(hunter.CountsHeads, Is.False, "eight walk into twenty-four");
            Assert.That((hunter.Company, hunter.AddChancePercent, hunter.Lineup), Is.EqualTo((PvpCompany.Alone, 0, PvpLineup.Meta)));
            Assert.That((hunter.MinRealmLevel, hunter.MaxRealmLevel, hunter.Skill), Is.EqualTo((elite.MinRealmLevel, elite.MaxRealmLevel, elite.Skill)), "an elite team");
            // "If my group gets to the zerg 1st, the bot elite team won't add if they get there 2nd and 3rd right ?"
            Assert.That(PvpRoam.Joins(hunter.AddChancePercent, 0), Is.False, "it does not add, on a pack no more than on anybody");
            // It is where the crowd is, and not in a clean zone.
            Assert.That(PvpRoam.Leans(PvpTeamKind.Hunter, PvpZoneCrowd.Crowded), Is.GreaterThan(PvpRoam.Leans(PvpTeamKind.Hunter, PvpZoneCrowd.Mixed)));
            Assert.That(PvpRoam.Leans(PvpTeamKind.Hunter, PvpZoneCrowd.Clean), Is.Zero);
            // The better the kind, the higher it begins and the sharper it plays; never above what a rank can be.
            for (int index = 1; index < PvpTeamProfile.All.Count; index++)
            {
                Assert.That(PvpTeamProfile.All[index].MinRealmLevel, Is.GreaterThanOrEqualTo(PvpTeamProfile.All[index - 1].MinRealmLevel));
                Assert.That(PvpTeamProfile.All[index].Skill, Is.GreaterThanOrEqualTo(PvpTeamProfile.All[index - 1].Skill));
            }
            Assert.That(PvpTeamProfile.All.Max(profile => profile.MaxRealmLevel), Is.LessThan(GamePlayer.REALMPOINTS_FOR_LEVEL.Length));
            Assert.That(PvpTeamProfile.Of("no such kind").Kind, Is.EqualTo(PvpTeamKind.Decent));
        }

        [Test]
        public void ARealmLevelAndItsRealmPoints()
        {
            foreach (int level in new[] { 1, 10, 29, 43, 70, 95 })
                Assert.That(PvpRealmPoints.LevelFor(PvpRealmPoints.PointsFor(level)), Is.EqualTo(level), level.ToString());
            Assert.That(PvpRealmPoints.PointsFor(-3), Is.Zero);
        }

        [Test]
        public void ATeamIsEightBuildsOfItsRealm()
        {
            foreach (eRealm realm in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                List<string> main = PvpBuildCatalog.Groups.First(group => group.Realm == realm).BuildIds.ToList();
                Assert.That(PvpTeamPool.Compose(realm, PvpLineup.Meta, new Random(1)), Is.EqualTo(main));
                HashSet<string> ofRealm = PvpTeamPool.BuildsOf(realm).Select(build => build.Id).ToHashSet();
                Assert.That(ofRealm, Has.None.StartsWith("buffbot-"));
                for (int seed = 0; seed < 60; seed++)
                {
                    foreach (PvpLineup lineup in Enum.GetValues<PvpLineup>())
                    {
                        List<string> builds = PvpTeamPool.Compose(realm, lineup, new Random(seed));
                        string what = $"{realm} {lineup} {seed}: {string.Join(",", builds)}";
                        Assert.That(builds, Has.Count.EqualTo(8), what);
                        Assert.That(builds.All(ofRealm.Contains), Is.True, what);
                        List<eCharacterClass> classes = builds.Select(id => PvpBuildCatalog.Find(id).Class).ToList();
                        int healers = classes.Count(c => c is eCharacterClass.Cleric or eCharacterClass.Healer or eCharacterClass.Druid);
                        int speed = classes.Count(c => c is eCharacterClass.Minstrel or eCharacterClass.Skald or eCharacterClass.Bard);
                        Assert.That(healers, Is.InRange(1, 2), what);
                        Assert.That(speed, Is.LessThanOrEqualTo(1), what);
                        Assert.That(builds.GroupBy(id => id).Max(same => same.Count()), Is.LessThanOrEqualTo(2), what);
                        if (lineup == PvpLineup.NearMeta)
                        {
                            Assert.That(builds.Where((id, index) => id != main[index]).Count(), Is.InRange(1, 2), what);
                            Assert.That(healers, Is.EqualTo(2), what);
                            Assert.That(speed, Is.EqualTo(1), what);
                        }
                        if (lineup == PvpLineup.Loose)
                        {
                            Assert.That(healers, Is.EqualTo(2), what);
                            Assert.That(speed, Is.EqualTo(1), what);
                        }
                    }
                }
                // "Most of these groups would run sub optimal composition": some bad groups have no speed, and few are the main group.
                List<List<string>> bad = Enumerable.Range(0, 100).Select(seed => PvpTeamPool.Compose(realm, PvpLineup.Random, new Random(seed))).ToList();
                Assert.That(bad.Count(builds => !builds.Any(id => PvpBuildCatalog.Find(id).Class is eCharacterClass.Minstrel or eCharacterClass.Skald or eCharacterClass.Bard)),
                    Is.InRange(20, 60), realm.ToString());
            }
        }

        [Test]
        public void APoolHasANameAndAKindForEveryTeam()
        {
            List<PvpGuildTeam> pool = PvpTeamPool.Generate(16, new Random(7));
            Assert.That(pool, Has.Count.EqualTo(48));
            Assert.That(pool.Select(team => team.Id).Distinct().Count(), Is.EqualTo(48));
            Assert.That(pool.Select(team => team.Guild).Distinct().Count(), Is.EqualTo(48));
            foreach (eRealm realm in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                List<PvpGuildTeam> ofRealm = pool.Where(team => team.ToRealm() == realm).ToList();
                Assert.That(ofRealm, Has.Count.EqualTo(16), realm.ToString());
                Assert.That(ofRealm.Count(team => team.ToProfile().Kind == PvpTeamKind.Bad), Is.EqualTo(5), realm.ToString());
                Assert.That(ofRealm.Count(team => team.ToProfile().Kind == PvpTeamKind.Elite), Is.EqualTo(1), realm.ToString());
            }
            foreach (PvpGuildTeam team in pool)
            {
                PvpTeamProfile profile = team.ToProfile();
                Assert.That(team.SeedLevel, Is.InRange(profile.MinRealmLevel, profile.MaxRealmLevel), team.Guild);
                Assert.That(team.Builds, Has.Count.EqualTo(8), team.Guild);
                Assert.That(team.ToTemplate().Realm, Is.EqualTo(team.ToRealm()));
                Assert.That(team.Realm, Is.AnyOf("Albion", "Midgard", "Hibernia"), "as a person would write it in the file");
                Assert.That(team.Guild.Length, Is.LessThanOrEqualTo(30), team.Guild);
            }
            // More teams for a pool that has some: none of the names that are taken.
            List<PvpGuildTeam> more = PvpTeamPool.Generate(6, new Random(8), pool);
            Assert.That(more, Has.Count.EqualTo(18));
            Assert.That(more.Select(team => team.Guild).Intersect(pool.Select(team => team.Guild)), Is.Empty);
            Assert.That(PvpTeamPool.MostPerRealm, Is.GreaterThanOrEqualTo(24));
        }

        [Test]
        public void WhoIsOutThisEvening()
        {
            List<PvpGuildTeam> pool = PvpTeamPool.Generate(16, new Random(3));
            List<PvpGuildTeam> tonight = PvpTeamPool.Tonight(pool, 8, new Random(4));
            Assert.That(tonight, Has.Count.EqualTo(24));
            Assert.That(tonight.Distinct().Count(), Is.EqualTo(24));
            foreach (eRealm realm in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                List<PvpGuildTeam> ofRealm = tonight.Where(team => team.ToRealm() == realm).ToList();
                Assert.That(ofRealm, Has.Count.EqualTo(8), realm.ToString());
                Assert.That(ofRealm.Count(team => team.ToProfile().Kind == PvpTeamKind.Bad), Is.InRange(2, 3), realm.ToString());
            }
            // The player's own group is one of its realm's eight.
            List<PvpGuildTeam> withPlayer = PvpTeamPool.Tonight(pool, 8, new Random(4), eRealm.Hibernia);
            Assert.That(withPlayer.Count(team => team.ToRealm() == eRealm.Hibernia), Is.EqualTo(7));
            Assert.That(withPlayer.Count(team => team.ToRealm() == eRealm.Albion), Is.EqualTo(8));
            // Not the same teams every evening.
            Assert.That(PvpTeamPool.Tonight(pool, 8, new Random(5)).Select(team => team.Id), Is.Not.EquivalentTo(tonight.Select(team => team.Id)));
            // A pool too small gives what it has.
            Assert.That(PvpTeamPool.Tonight(PvpTeamPool.Generate(2, new Random(1)), 8, new Random(1)), Has.Count.EqualTo(6));
        }

        [Test]
        public void BadGroupsWalkInPacksOfTwoToFive()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                for (int count = 0; count <= 12; count++)
                {
                    List<string> teams = Enumerable.Range(0, count).Select(index => "team" + index).ToList();
                    Dictionary<string, string> follows = PvpTeamPool.Packs(teams, new Random(seed));
                    List<int> sizes = teams.Where(team => !follows.ContainsKey(team)).Select(head => 1 + follows.Count(pair => pair.Value == head)).ToList();
                    string what = $"{count} teams, seed {seed}: {string.Join(",", sizes)}";
                    Assert.That(sizes.Sum(), Is.EqualTo(count), what);
                    Assert.That(follows.Values.All(head => !follows.ContainsKey(head)), Is.True, what);
                    if (count >= 2)
                        Assert.That(sizes, Is.All.InRange(2, 5), what);
                }
            }
        }

        [Test]
        public void ATeamGoesFromPointToPointAsItPleases()
        {
            PvpRoamMap emain = PvpRoam.Emain;
            (int X, int Y)[] six = [PvpRoam.South, PvpRoam.Middle, PvpRoam.North, PvpRoam.WestNorth, PvpRoam.WestMiddle, PvpRoam.WestSouth];
            // Every point can be left, every way joins two of the six, and a realm's first points are among them.
            foreach ((int X, int Y) point in six)
                Assert.That(emain.From(point), Has.Count.GreaterThanOrEqualTo(2), point.ToString());
            Assert.That(emain.Ways.SelectMany(way => new[] { way.A, way.B }).Distinct(), Is.EquivalentTo(six));
            foreach (PvpRoamStart start in emain.Starts)
            {
                Assert.That(six, Does.Contain(start.RouteOf(1)[0]));
                Assert.That(six, Does.Contain(start.RouteOf(2)[0]));
            }
            foreach ((int X, int Y) point in new[] { PvpRoam.ThidHM, PvpRoam.ThidHA, PvpRoam.ThidMA })
                Assert.That(PvpRoam.Thidranki.From(point), Has.Count.EqualTo(2));

            // Never back the way it came while there is another way, and only along a way.
            for (int roll = 0; roll < 50; roll++)
            {
                (int X, int Y) next = PvpRoam.NextPoint(emain, PvpRoam.Middle, PvpRoam.South, 0, roll);
                Assert.That(next, Is.Not.EqualTo(PvpRoam.South));
                Assert.That(emain.From(PvpRoam.Middle), Does.Contain(next));
            }
            // From the middle of the road: north along it, or across to the west side. An elite group leans west, the head of a pack to the road.
            int Count(int leaning, (int X, int Y) to) =>
                Enumerable.Range(0, 1000).Count(roll => PvpRoam.NextPoint(emain, PvpRoam.Middle, PvpRoam.South, leaning, roll) == to);
            Assert.That(Count(0, PvpRoam.WestMiddle), Is.EqualTo(500));
            Assert.That(Count(1, PvpRoam.WestMiddle), Is.EqualTo(800));
            Assert.That(Count(-1, PvpRoam.North), Is.EqualTo(800));
            // A point with one way only: back it goes. A map without ways: it stays.
            var deadEnd = new PvpRoamMap("x", "x", 1, [], [((0, 0), (10, 10))]);
            Assert.That(PvpRoam.NextPoint(deadEnd, (10, 10), (0, 0), 0, 3), Is.EqualTo((0, 0)));
            Assert.That(PvpRoam.NextPoint(new PvpRoamMap("y", "y", 1, []), (5, 5), null, 0, 3), Is.EqualTo((5, 5)));
        }

        [Test]
        public void ManyGroupsDoNotStartInOneAnother()
        {
            (int X, int Y) start = (100_000, 200_000);
            List<(int X, int Y)> spots = Enumerable.Range(0, 13).Select(index => PvpRoam.SpawnPoint(start, index)).ToList();
            Assert.That(spots[0], Is.EqualTo(start));
            for (int a = 0; a < spots.Count; a++)
            {
                Assert.That(Math.Sqrt(Math.Pow(spots[a].X - start.X, 2) + Math.Pow(spots[a].Y - start.Y, 2)), Is.LessThanOrEqualTo(1_000), a.ToString());
                for (int b = a + 1; b < spots.Count; b++)
                    Assert.That(Math.Sqrt(Math.Pow(spots[a].X - spots[b].X, 2) + Math.Pow(spots[a].Y - spots[b].Y, 2)), Is.GreaterThan(350), $"{a} and {b}");
            }
        }

        [Test]
        public void WhatARealmCallsOut()
        {
            // "which team will we face, how many teams (1 team, a zerg [many teams]), where"
            Assert.That(PvpRoam.SightingText(2, eRealm.Albion, "south road", false), Is.EqualTo("2fg albs at south road"));
            Assert.That(PvpRoam.SightingText(1, eRealm.Midgard, "mid road", true), Is.EqualTo("fighting 1fg mids at mid road"));
            Assert.That(PvpRoam.SightingText(3, eRealm.Hibernia, "north road", false, true), Is.EqualTo("wiped at north road, 3fg hibs"));
            Assert.That(PvpRoam.SightingText(1, eRealm.Albion, null, false), Is.EqualTo("1fg albs"));
            // A spot called by its side of a point takes no "at".
            Assert.That(PvpRoam.SightingText(1, eRealm.Midgard, "east of north road (Odin's)", false), Is.EqualTo("1fg mids east of north road (Odin's)"));
            Assert.That(PvpRoam.SightingText(2, eRealm.Albion, "south-west of Crimthain (Breifine)", true), Is.EqualTo("fighting 2fg albs south-west of Crimthain (Breifine)"));
            Assert.That(PvpRoam.SightingText(3, eRealm.Hibernia, "north of middle", false, true), Is.EqualTo("wiped north of middle, 3fg hibs"));
            Assert.That(PvpRoam.SightingText(1, eRealm.Albion, "north-west hills", false), Is.EqualTo("1fg albs at north-west hills"), "a point whose name begins with a side");
            Assert.That(PvpRoam.OnTheRunText(4, eRealm.Midgard, "west of mid road"), Is.EqualTo("4 mids on the run west of mid road"));
            // A team that has said it joins a fight goes to it: to the nearest enemy team it drew "joins" for.
            Assert.That(PvpRoam.FightItJoined([((1_000.0, 0.0), false), ((9_000.0, 0.0), true), ((0.0, 7_000.0), true)], (0.0, 0.0)), Is.EqualTo((0, 7_000)));
            Assert.That(PvpRoam.FightItJoined([((1_000.0, 0.0), false)], (0.0, 0.0)), Is.Null, "it joined nothing: it walks its road");
            Assert.That(PvpRoam.FightItJoined([], (0.0, 0.0)), Is.Null);
            // An evening at a picked rank (/8v8 prime emain 8 rank 8): rank 8 is realm level 70, and the player's group earns nothing.
            Assert.That(PvpRoam.TrialLevel(8), Is.EqualTo(70));
            Assert.That(PvpRoam.TrialLevel(1), Is.EqualTo(0), "rank 1 is what a new group has");
            Assert.That(PvpRoam.TrialLevel(0), Is.EqualTo(0));
            Assert.That(PvpRoam.TrialLevel(14), Is.EqualTo(0));
            Assert.That(PvpRoam.Earns(true, 70), Is.False);
            Assert.That(PvpRoam.Earns(false, 70), Is.True, "the guilds earn as always");
            Assert.That(PvpRoam.Earns(true, 0), Is.True);
            // A group that breaks off (M12.5): its own realm hears that it runs, the other that it is on the run.
            Assert.That(PvpRoam.RunningText(2, eRealm.Albion, "south road (Emain)"), Is.EqualTo("running from 2fg albs at south road (Emain)"));
            Assert.That(PvpRoam.OnTheRunText(4, eRealm.Midgard, "mid road"), Is.EqualTo("4 mids on the run at mid road"));
            Assert.That(PvpRoam.OnTheRunText(3, eRealm.Hibernia, null), Is.EqualTo("3 hibs on the run"));
            // Not a flood: about one realm at one place one call, until the number there changes or three minutes go by.
            Assert.That(PvpRoam.CallsOut(3, 500, false), Is.False);
            Assert.That(PvpRoam.CallsOut(20, 90, false), Is.False, "said already, and nothing has changed");
            Assert.That(PvpRoam.CallsOut(20, 200, false), Is.True);
            Assert.That(PvpRoam.CallsOut(20, 40, true), Is.True, "a third group has come");
            Assert.That(PvpRoam.CallsOut(20, 10, true), Is.False);
            Assert.That(PvpRoam.NoNewsWithin, Is.LessThan(PvpRoam.SightingWithin));

            // Every point of a map has a name, and a place by a realm's start is "the keep".
            PvpRoamMap emain = PvpRoam.Emain;
            Assert.That(emain.Places.Select(place => place.Point), Is.EquivalentTo(emain.Ways.SelectMany(way => new[] { way.A, way.B }).Distinct()));
            Assert.That(emain.PlaceOf(PvpRoam.South.X + 800, PvpRoam.South.Y - 500), Is.EqualTo("south road"));
            Assert.That(emain.PlaceOf(PvpRoam.WestMiddle.X, PvpRoam.WestMiddle.Y), Is.EqualTo("west hills"));
            // Far from its nearest named point, a spot is called by its side of it (north is less Y, east is more X).
            PvpRoamMap breifine = PvpRoamZones.Breifine;
            Assert.That(breifine.PlaceOf(431792, 378024), Is.EqualTo("Crimthain"));
            Assert.That(breifine.PlaceOf(431792 + 2_000, 378024 - 2_000), Is.EqualTo("Crimthain"), "within 3,000 it is at the point");
            Assert.That(breifine.PlaceOf(431792 + 4_000, 378024 - 4_500), Is.EqualTo("north-east of Crimthain"));
            Assert.That(breifine.PlaceOf(431792, 378024 - 5_000), Is.EqualTo("north of Crimthain"));
            Assert.That(breifine.PlaceOf(431792 - 5_000, 378024 + 500), Is.EqualTo("west of Crimthain"));
            Assert.That(breifine.PlaceOf(431792 - 5_000, 378024 + 500, true), Is.EqualTo("Crimthain"), "the point alone is what counts as news");
            Assert.That(PvpRoam.Compass(1, 0), Is.EqualTo("east"));
            Assert.That(PvpRoam.Compass(-1, 0), Is.EqualTo("west"));
            Assert.That(PvpRoam.Compass(0, 1), Is.EqualTo("south"));
            Assert.That(PvpRoam.Compass(0, -1), Is.EqualTo("north"));
            Assert.That(PvpRoam.Compass(-1, -1), Is.EqualTo("north-west"));
            Assert.That(PvpRoam.Compass(1, 1), Is.EqualTo("south-east"));
            Assert.That(PvpRoam.Compass(-1, 1), Is.EqualTo("south-west"));
            Assert.That(PvpRoam.Compass(100, -30), Is.EqualTo("east"), "a little north of east is east");
            // What the player is told with the line: how far, to the nearest 500, and which way.
            Assert.That(PvpRoam.FromYouText(-11_000, -11_300), Is.EqualTo("16,000 north-west of you"));
            Assert.That(PvpRoam.FromYouText(0, 4_200), Is.EqualTo("4,000 south of you"));
            Assert.That(PvpRoam.FromYouText(300, -200), Is.EqualTo("beside you"));
            (int X, int Y) albion = emain.StartOf(eRealm.Albion).Point;
            Assert.That(emain.PlaceOf(albion.X - 2_000, albion.Y), Is.EqualTo("the alb keep"));
            Assert.That(PvpRoam.Thidranki.Places, Has.Count.EqualTo(3));
            Assert.That(new PvpRoamMap("x", "x", 1, []).PlaceOf(1, 1), Is.Null);

            // What a player is told of the other realms: how many groups, and no more.
            Assert.That(PvpRoam.OutAgainst([(eRealm.Albion, 8), (eRealm.Midgard, 1)]),
                Is.EqualTo("Out against you: 8 groups of Albion and 1 group of Midgard. Where they are, your realm says when it sees them."));
            Assert.That(PvpRoam.OutAgainst([]), Is.EqualTo("Nobody else is out."));
        }

        [Test]
        public void AGuildsEveningEnds()
        {
            // Wiped three times: it has had enough.
            Assert.That(PvpRoam.LogsOff(3, false, true, false), Is.True);
            Assert.That(PvpRoam.LogsOff(2, false, true, false), Is.False);
            Assert.That(PvpRoam.LogsOff(5, false, false, false), Is.False, "not while it is up and fighting");
            // Its time is up: at its next wipe, or on the road with nobody about. Never out of a fight.
            Assert.That(PvpRoam.LogsOff(0, true, false, true), Is.True);
            Assert.That(PvpRoam.LogsOff(1, true, true, false), Is.True);
            Assert.That(PvpRoam.LogsOff(0, true, false, false), Is.False);
            Assert.That(PvpRoam.LogsOff(0, false, false, true), Is.False);
            foreach (int roll in new[] { 0, 1, 1499, 1500, 1501, 99_999, int.MaxValue })
                Assert.That(PvpRoam.EveningSeconds(roll), Is.InRange(25 * 60, 50 * 60), roll.ToString());

            // Another of its realm comes out: of its kind when there is one, never one that is out or has been.
            List<PvpGuildTeam> pool = PvpTeamPool.Generate(16, new Random(11));
            List<PvpGuildTeam> midgard = pool.Where(team => team.ToRealm() == eRealm.Midgard).ToList();
            PvpGuildTeam elite = midgard.Single(team => team.ToProfile().Kind == PvpTeamKind.Elite);
            List<PvpGuildTeam> bad = midgard.Where(team => team.ToProfile().Kind == PvpTeamKind.Bad).ToList();
            var taken = new HashSet<string> { bad[0].Id, elite.Id };
            for (int seed = 0; seed < 30; seed++)
            {
                PvpGuildTeam next = PvpTeamPool.NextOut(pool, eRealm.Midgard, PvpTeamKind.Bad, taken, new Random(seed));
                Assert.That(next.ToRealm(), Is.EqualTo(eRealm.Midgard));
                Assert.That(next.ToProfile().Kind, Is.EqualTo(PvpTeamKind.Bad));
                Assert.That(taken, Does.Not.Contain(next.Id));
                // The realm's one elite team is out: somebody else comes.
                PvpGuildTeam other = PvpTeamPool.NextOut(pool, eRealm.Midgard, PvpTeamKind.Elite, taken, new Random(seed));
                Assert.That(other.ToProfile().Kind, Is.Not.EqualTo(PvpTeamKind.Elite));
                Assert.That(taken, Does.Not.Contain(other.Id));
            }
            Assert.That(PvpTeamPool.NextOut(pool, eRealm.Midgard, PvpTeamKind.Bad, midgard.Select(team => team.Id).ToHashSet(), new Random(1)), Is.Null, "nobody is left");
        }

        [Test]
        public void AGuildRemembers()
        {
            // "a team beaten twice comes looking; a team that was added on remembers"
            Assert.That(PvpRoam.ComesLooking(1, false), Is.False);
            Assert.That(PvpRoam.ComesLooking(2, false), Is.True);
            Assert.That(PvpRoam.ComesLooking(0, true), Is.True);
            Assert.That(PvpRoam.PaysBack(true, true), Is.True);
            Assert.That(PvpRoam.PaysBack(true, false), Is.False, "somebody else's fight: it does as its kind does");
            Assert.That(PvpRoam.PaysBack(false, true), Is.False);
            // Added on: the player's group came into a fight it had with somebody else.
            Assert.That(PvpRoam.WasAddedOn(true, 1, false), Is.True);
            Assert.That(PvpRoam.WasAddedOn(true, 0, false), Is.False, "a fight of the two of them");
            Assert.That(PvpRoam.WasAddedOn(true, 1, true), Is.False, "it was the one that added");
            Assert.That(PvpRoam.WasAddedOn(false, 2, false), Is.False);
            // It goes toward the player's group: from the south end of the road, north along it for a group in the north, west for one in the west.
            Assert.That(PvpRoam.PointToward(PvpRoam.Emain, PvpRoam.South, (PvpRoam.North.X, PvpRoam.North.Y)), Is.EqualTo(PvpRoam.Middle));
            Assert.That(PvpRoam.PointToward(PvpRoam.Emain, PvpRoam.South, (PvpRoam.WestSouth.X - 500, PvpRoam.WestSouth.Y)), Is.EqualTo(PvpRoam.WestSouth));
            Assert.That(PvpRoam.PointToward(new PvpRoamMap("y", "y", 1, []), (5, 5), (0, 0)), Is.EqualTo((5, 5)));

            // Its record against the player is kept from evening to evening.
            List<PvpGuildTeam> pool = PvpTeamPool.Generate(2, new Random(5));
            var evening = new Dictionary<string, PvpEvening> { [pool[0].Id] = new PvpEvening(4, 20, 16, 2, 2, 1) };
            List<PvpGuildTeam> after = PvpTeamPool.Recorded(PvpTeamPool.Recorded(pool, evening), evening);
            Assert.That(after[0].Evenings, Is.EqualTo(2));
            Assert.That(after[0].WipedByPlayer, Is.EqualTo(4));
            Assert.That(after[0].WipedPlayer, Is.EqualTo(2));
            Assert.That(after[0].Wipes, Is.EqualTo(4));
            Assert.That(after[1], Is.EqualTo(pool[1]), "it was not out");
            Assert.That(PvpRoam.RivalLine("The Iron Longship", "Midgard", 3, 2, 1, 0),
                Is.EqualTo("The Iron Longship <Midgard>: you wiped them 3 times (2 tonight), they were in on your wipe 1 time"));
        }

        [Test]
        public void AnEveningOverSeveralZones()
        {
            // "Maps? I would assume more than just emain"
            // A team takes the zone that has the fewest groups of its realm, any of them when several have as few.
            Assert.That(PvpRoam.ZoneWithFewest([3, 1, 2], 7), Is.EqualTo(1));
            Assert.That(Enumerable.Range(0, 30).Select(roll => PvpRoam.ZoneWithFewest([2, 0, 0], roll)).Distinct(), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(Enumerable.Range(0, 30).Select(roll => PvpRoam.ZoneWithFewest([0, 0, 0], roll)).Distinct(), Is.EquivalentTo(new[] { 0, 1, 2 }), "a pack is not always in the first zone");
            Assert.That(PvpRoam.ZoneWithFewest([4], 9), Is.Zero);
            Assert.That(PvpRoam.ZoneWithFewest([], 9), Is.Zero);
            // Eight groups of a realm, a pack of three among them: three, three and two.
            var load = new[] { 0, 0, 0 };
            foreach (int size in new[] { 3, 1, 1, 1, 1, 1 })
                load[PvpRoam.ZoneWithFewest(load, size * 31)] += size;
            Assert.That(load.OrderBy(count => count), Is.EqualTo(new[] { 2, 3, 3 }));
            // A guild that walks alone and has been wiped ports elsewhere one time in three; a pack and a follower stay.
            Assert.That(Enumerable.Range(0, 300).Count(roll => PvpRoam.PortsElsewhere(3, false, false, roll)), Is.EqualTo(100));
            Assert.That(Enumerable.Range(0, 300).Any(roll => PvpRoam.PortsElsewhere(1, false, false, roll)), Is.False, "there is nowhere else");
            Assert.That(Enumerable.Range(0, 300).Any(roll => PvpRoam.PortsElsewhere(3, true, false, roll)), Is.False, "it walks with somebody");
            Assert.That(Enumerable.Range(0, 300).Any(roll => PvpRoam.PortsElsewhere(3, false, true, roll)), Is.False, "somebody walks with it");
            // Every frontier has a start for each realm, points with names, ways between them, and a word that names it.
            Assert.That(PvpRoam.Frontiers, Does.Contain(PvpRoam.Emain));
            Assert.That(PvpRoam.Frontiers, Has.Count.EqualTo(12), "four zones of each realm's frontier");
            Assert.That(PvpRoam.Frontiers.GroupBy(zone => zone.Region).Select(region => region.Count()), Is.EqualTo(new[] { 4, 4, 4 }));
            Assert.That(PvpRoam.Frontiers.Select(zone => zone.Zone).Distinct().Count(), Is.EqualTo(PvpRoam.Frontiers.Count));
            Assert.That(PvpRoam.Frontiers.Select(zone => zone.Zone), Has.None.Zero);
            Assert.That(PvpRoam.Frontiers.Select(zone => zone.Tag).Distinct().Count(), Is.EqualTo(PvpRoam.Frontiers.Count));
            Assert.That(PvpRoam.Frontiers.Select(zone => zone.Id).Distinct().Count(), Is.EqualTo(PvpRoam.Frontiers.Count));
            foreach (PvpRoamMap zone in PvpRoam.Frontiers)
            {
                Assert.That(PvpRoam.MapFor(zone.Id), Is.EqualTo(zone), zone.Name);
                Assert.That(PvpRoam.MapFor(zone.Name.Replace(" ", string.Empty).Replace("'", string.Empty)), Is.EqualTo(zone), zone.Name);
                // The three starts are far from one another: nobody stands up in somebody else's start.
                foreach (PvpRoamStart one in zone.Starts)
                    foreach (PvpRoamStart other in zone.Starts.Where(other => other != one))
                        Assert.That(Math.Sqrt(Math.Pow(one.Point.X - other.Point.X, 2) + Math.Pow(one.Point.Y - other.Point.Y, 2)), Is.GreaterThan(25_000), $"{zone.Name} {one.Realm} {other.Realm}");
                Assert.That(zone.Starts.Select(start => start.Realm), Is.EquivalentTo(new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }), zone.Name);
                List<(int X, int Y)> points = zone.Ways.SelectMany(way => new[] { way.A, way.B }).Distinct().ToList();
                Assert.That(points, Has.Count.GreaterThanOrEqualTo(4), zone.Name);
                Assert.That(zone.Places.Select(place => place.Point), Is.EquivalentTo(points), zone.Name);
                Assert.That(zone.Places.Select(place => place.Name).Distinct().Count(), Is.EqualTo(points.Count), zone.Name);
                foreach ((int X, int Y) point in points)
                    Assert.That(zone.From(point), Is.Not.Empty, $"{zone.Name} {point}");
                foreach (PvpRoamStart start in zone.Starts)
                {
                    Assert.That(points, Does.Contain(start.RouteOf(1)[0]), $"{zone.Name} {start.Realm}");
                    Assert.That(points, Does.Contain(start.RouteOf(2)[0]), $"{zone.Name} {start.Realm}");
                }
                Assert.That(PvpRoam.MapFor(zone.Tag.ToLowerInvariant().Replace("'s", string.Empty)), Is.EqualTo(zone), zone.Name);
            }
            Assert.That(PvpRoam.Emain.Tag, Is.EqualTo("Emain"));
        }

        [Test]
        public void TheZonesBehindTheFirstThree()
        {
            // "adding more zones and spreading teams (and type of teams) across them all"
            Assert.That(PvpRoamZones.All, Has.Count.EqualTo(9));
            Assert.That(PvpRoam.MapFor("sauvage"), Is.SameAs(PvpRoamZones.ForestSauvage));
            Assert.That(PvpRoam.MapFor("ForestSauvage"), Is.SameAs(PvpRoamZones.ForestSauvage));
            Assert.That(PvpRoamZones.MountCollory.Tag, Is.EqualTo("Collory"));
            var home = new Dictionary<ushort, eRealm> { [1] = eRealm.Albion, [100] = eRealm.Midgard, [200] = eRealm.Hibernia };
            foreach (PvpRoamMap zone in PvpRoamZones.All)
            {
                Assert.That(zone.Crowd, Is.EqualTo(PvpZoneCrowd.Clean), zone.Name);
                // The realm of the zone is in front of one of its keeps; the two others have none there.
                Assert.That(zone.Starts.Where(start => start.AtKeep).Select(start => start.Realm), Is.EqualTo(new[] { home[zone.Region] }), zone.Name);
                List<(int X, int Y)> points = zone.Places.Select(place => place.Point).ToList();
                Assert.That(points, Has.Count.InRange(5, 9), zone.Name);
                Assert.That(zone.Quiet, Has.Count.EqualTo(1), zone.Name);
                Assert.That(points, Does.Contain(zone.Quiet[0]), zone.Name);
                foreach (((int X, int Y) a, (int X, int Y) b) in zone.Ways)
                    Assert.That(Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2)), Is.InRange(10_000, 27_000), $"{zone.Name} {a} {b}");
                // Every point can be walked to from every other.
                var reached = new HashSet<(int X, int Y)> { points[0] };
                for (int round = 0; round < points.Count; round++)
                    foreach ((int X, int Y) point in reached.ToList())
                        reached.UnionWith(zone.From(point));
                Assert.That(reached, Has.Count.EqualTo(points.Count), zone.Name);
                // Where a realm comes in without a keep is called by the point it is near, not "the keep".
                PvpRoamStart camp = zone.Starts.First(start => !start.AtKeep);
                Assert.That(zone.PlaceOf(camp.Point.X, camp.Point.Y), Does.Not.Contain("keep"), zone.Name);
                PvpRoamStart keep = zone.Starts.First(start => start.AtKeep);
                Assert.That(zone.PlaceOf(keep.Point.X, keep.Point.Y), Is.EqualTo($"the {PvpRoam.Short(keep.Realm, false)} keep"), zone.Name);
            }
            Assert.That(PvpRoam.Emain.Crowd, Is.EqualTo(PvpZoneCrowd.Crowded));
            Assert.That(PvpRoam.OdinsGate.Crowd, Is.EqualTo(PvpZoneCrowd.Mixed));
            Assert.That(PvpRoam.HadriansWall.Crowd, Is.EqualTo(PvpZoneCrowd.Mixed));
        }

        [Test]
        public void EachKindOfTeamHasItsKindOfZone()
        {
            // "Elite 8v8 teams that would be only 8v8 in specific area to avoid not clean fights"
            Assert.That(PvpRoam.Leans(PvpTeamKind.Elite, PvpZoneCrowd.Crowded), Is.Zero);
            Assert.That(PvpRoam.Leans(PvpTeamKind.Bad, PvpZoneCrowd.Clean), Is.Zero);
            Assert.That(PvpRoam.Leans(PvpTeamKind.Clingy, PvpZoneCrowd.Clean), Is.Zero);
            Assert.That(PvpRoam.Leans(PvpTeamKind.Adder, PvpZoneCrowd.Clean), Is.GreaterThan(0), "now and then one comes where it is not wanted");
            foreach (PvpTeamKind kind in Enum.GetValues<PvpTeamKind>())
                Assert.That(Enum.GetValues<PvpZoneCrowd>().Sum(crowd => PvpRoam.Leans(kind, crowd)), Is.GreaterThan(0), kind.ToString());
            PvpZoneCrowd[] zones = [PvpZoneCrowd.Crowded, PvpZoneCrowd.Mixed, PvpZoneCrowd.Clean];
            int[] nobody = [0, 0, 0];
            List<int> Picks(PvpTeamKind kind, PvpZoneCrowd[] among, int[] load) => Enumerable.Range(0, 1000).Select(roll => PvpRoam.ZoneFor(kind, among, load, roll * 7919)).ToList();
            Assert.That(Picks(PvpTeamKind.Elite, zones, nobody), Has.None.EqualTo(0), "an elite team is not in the crowd");
            Assert.That(Picks(PvpTeamKind.Elite, zones, nobody).Count(pick => pick == 2), Is.GreaterThan(800));
            Assert.That(Picks(PvpTeamKind.Bad, zones, nobody), Has.None.EqualTo(2), "a pack is not in a clean zone");
            Assert.That(Picks(PvpTeamKind.Bad, zones, nobody).Count(pick => pick == 0), Is.GreaterThan(650));
            Assert.That(Picks(PvpTeamKind.Decent, zones, nobody).Distinct(), Is.EquivalentTo(new[] { 0, 1, 2 }), "a decent team is everywhere");
            // Of the zones its kind leans to, rather the one that has few of its realm, and one that has none first.
            Assert.That(Picks(PvpTeamKind.Decent, [PvpZoneCrowd.Mixed, PvpZoneCrowd.Mixed], [4, 0]).Count(pick => pick == 1), Is.GreaterThan(900));
            Assert.That(Picks(PvpTeamKind.Decent, [PvpZoneCrowd.Mixed, PvpZoneCrowd.Mixed], [3, 1]).Count(pick => pick == 1), Is.InRange(580, 760));
            // Where its kind leans to none of the zones there are, it takes the one with the fewest.
            Assert.That(Picks(PvpTeamKind.Bad, [PvpZoneCrowd.Clean, PvpZoneCrowd.Clean], [3, 1]), Has.All.EqualTo(1));
            Assert.That(Picks(PvpTeamKind.Elite, [PvpZoneCrowd.Crowded], [5]), Has.All.EqualTo(0));
            // The choosiest take their zones first.
            Assert.That(new[] { PvpTeamKind.Decent, PvpTeamKind.Bad, PvpTeamKind.Elite, PvpTeamKind.Adder }.OrderBy(PvpRoam.Choosiness).Take(2),
                Is.EqualTo(new[] { PvpTeamKind.Elite, PvpTeamKind.Bad }));
            // The draw among weights.
            Assert.That(PvpRoam.Weighted([0, 0], 5), Is.EqualTo(-1));
            Assert.That(PvpRoam.Weighted([], 5), Is.EqualTo(-1));
            Assert.That(Enumerable.Range(0, 8).Select(roll => PvpRoam.Weighted([1, 0, 3], roll)), Is.EqualTo(new[] { 0, 2, 2, 2, 0, 2, 2, 2 }));
            Assert.That(PvpRoam.Weighted([1, 0, 3], -6), Is.EqualTo(2), "a roll below zero");
        }

        [Test]
        public void TheZonesOfAnEvening()
        {
            // One zone for every three groups of a realm: some nine teams a zone.
            Assert.That(new[] { 1, 2, 4, 5, 8, 12, 16 }.Select(groups => PvpRoam.ZonesFor(groups, 12)), Is.EqualTo(new[] { 1, 1, 1, 2, 3, 4, 5 }));
            Assert.That(PvpRoam.ZonesFor(16, 3), Is.EqualTo(3), "no more than there are");
            Assert.That(PvpRoam.ZonesFor(8, 0), Is.EqualTo(1));
            var seen = new HashSet<PvpRoamMap>();
            for (int seed = 0; seed < 300; seed++)
            {
                List<PvpRoamMap> zones = PvpRoam.ZonesTonight(PvpRoam.Emain, 8, PvpRoam.Frontiers, new Random(seed));
                Assert.That(zones, Has.Count.EqualTo(3));
                Assert.That(zones[0], Is.SameAs(PvpRoam.Emain), "where it begins");
                Assert.That(zones.Distinct().Count(), Is.EqualTo(3));
                seen.UnionWith(zones);
            }
            Assert.That(seen, Has.Count.EqualTo(12), "every zone has its evenings");
            // It begins where the player is, and the crowd is somewhere all the same.
            for (int seed = 0; seed < 50; seed++)
            {
                List<PvpRoamMap> zones = PvpRoam.ZonesTonight(PvpRoamZones.Breifine, 8, PvpRoam.Frontiers, new Random(seed));
                Assert.That(zones.Take(2), Is.EqualTo(new[] { PvpRoamZones.Breifine, PvpRoam.Emain }));
                Assert.That(zones, Has.Count.EqualTo(3));
            }
            Assert.That(PvpRoam.ZonesTonight(PvpRoamZones.Breifine, 4, PvpRoam.Frontiers, new Random(1)), Is.EqualTo(new[] { PvpRoamZones.Breifine }), "few groups: the one zone");
            // A zone the realms ported to comes up more often than one behind it.
            int ported = 0, behind = 0;
            for (int seed = 0; seed < 600; seed++)
            {
                List<PvpRoamMap> zones = PvpRoam.ZonesTonight(PvpRoam.Emain, 8, PvpRoam.Frontiers, new Random(seed));
                ported += zones.Count(zone => zone == PvpRoam.OdinsGate);
                behind += zones.Count(zone => zone == PvpRoamZones.Uppland);
            }
            Assert.That(ported, Is.GreaterThan(behind * 2));
            Assert.That(behind, Is.GreaterThan(0));
            // What the player is told of them.
            Assert.That(PvpRoam.ZonesLine([PvpRoam.Emain, PvpRoam.OdinsGate, PvpRoamZones.Breifine], PvpRoam.Emain),
                Is.EqualTo("Tonight: Emain Macha (the crowd; you are here), Odin's Gate, Breifine (clean). /8v8 go odin, breifine."));
            Assert.That(PvpRoam.ZonesLine([PvpRoam.Emain, PvpRoamZones.Pennine], PvpRoamZones.Pennine),
                Is.EqualTo("Tonight: Emain Macha (the crowd), Pennine Mountains (clean; you are here). /8v8 go emain."));
            // More groups than there are zones for: all twelve.
            Assert.That(PvpRoam.ZonesTonight(PvpRoam.Emain, 40, PvpRoam.Frontiers, new Random(3)), Is.EquivalentTo(PvpRoam.Frontiers));
        }

        [Test]
        public void TeamsComeOutOneByOne()
        {
            var real = new PvpRealWeights();
            // No two in the same moment: a realm's teams half a minute apart, the realms ten seconds apart.
            List<int> times = Enumerable.Range(0, 3).SelectMany(realm => Enumerable.Range(0, 8).Select(before => PvpRoam.ComesOutAfterSeconds(before, realm, real))).ToList();
            Assert.That(times.Distinct().Count(), Is.EqualTo(times.Count));
            Assert.That(PvpRoam.ComesOutAfterSeconds(0, 0, real), Is.Zero, "somebody is out when the evening begins");
            Assert.That(PvpRoam.ComesOutAfterSeconds(1, 0, real) - PvpRoam.ComesOutAfterSeconds(0, 0, real), Is.EqualTo(real.NextTeamFollowsAfterSeconds));
            Assert.That(PvpRoam.Doing("Away", false, false, false, false, [], 0, 0), Is.EqualTo("not out yet"));
        }

        [Test]
        public void APackKeepsTogether()
        {
            Assert.That(PvpRoam.TrailsBehind, Is.LessThan(PvpRoam.KeepsWithin(PvpCompany.Near)), "a group that stays near has room to trail in");
            Assert.That(PvpRoam.KeepsWithin(PvpCompany.Zerg), Is.LessThan(PvpRoam.KeepsWithin(PvpCompany.Near)));
            Assert.That(PvpRoam.KeepsWithin(PvpCompany.Zerg), Is.LessThanOrEqualTo(PvpRoam.BesideWithin), "a pack counts as together");
            // A guild that does not add gives a fight three quarters of a minute to end, then goes.
            Assert.That(PvpRoam.GivesUpWaiting(20), Is.False);
            Assert.That(PvpRoam.GivesUpWaiting(60), Is.True);
            Assert.That(PvpRoam.PackWaits(true, 5), Is.True);
            Assert.That(PvpRoam.PackWaits(true, 30), Is.False, "it does not wait for ever");
            Assert.That(PvpRoam.PackWaits(false, 5), Is.False);
        }
    }
}

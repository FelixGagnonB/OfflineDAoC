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
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Decent).SharePercent, Is.EqualTo(20));
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Clingy).SharePercent + PvpTeamProfile.Of(PvpTeamKind.Adder).SharePercent, Is.EqualTo(30), "the two casual kinds");
            Assert.That(PvpTeamProfile.Of(PvpTeamKind.Bad).SharePercent, Is.EqualTo(30));

            Dictionary<PvpTeamKind, int> twenty = PvpTeamPool.Quotas(20);
            Assert.That(twenty[PvpTeamKind.Bad], Is.EqualTo(6));
            Assert.That(twenty[PvpTeamKind.Decent], Is.EqualTo(4));
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
            Assert.That(PvpRoam.PackWaits(true, 5), Is.True);
            Assert.That(PvpRoam.PackWaits(true, 30), Is.False, "it does not wait for ever");
            Assert.That(PvpRoam.PackWaits(false, 5), Is.False);
        }
    }
}

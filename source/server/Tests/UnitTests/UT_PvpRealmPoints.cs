using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using DOL.GS.Commands;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>A real session: the owner, 2026-11-20, "everything is just for testing. All abilities reset, no rps gain".</summary>
    [TestFixture]
    public class UT_PvpRealmPoints
    {
        [Test]
        public void AKillIsWorthWhatAPlayerOfThatLevelAndRankIs()
        {
            Assert.That(PvpRealmPoints.ValueOf(50, 0), Is.EqualTo(900));
            Assert.That(PvpRealmPoints.ValueOf(50, 90), Is.EqualTo(990));
            Assert.That(PvpRealmPoints.ValueOf(20, 0), Is.EqualTo(1), "never nothing");
        }

        [Test]
        public void TheWorthIsSharedOutEvenlyAtTheRate()
        {
            Assert.That(PvpRealmPoints.Share(900, 8, 1), Is.EqualTo(113));
            Assert.That(PvpRealmPoints.Share(900, 8, 5), Is.EqualTo(563));
            Assert.That(PvpRealmPoints.Share(900, 1, 1), Is.EqualTo(900), "alone, all of it");
            Assert.That(PvpRealmPoints.Share(900, 0, 5), Is.EqualTo(0), "nobody near, nobody paid");
            Assert.That(PvpRealmPoints.Share(1, 8, 1), Is.EqualTo(1), "never under a point");
            Assert.That(new PvpRealWeights().RealmPointRate, Is.EqualTo(5), "as built");
        }

        [Test]
        public void EverybodyStartsAtRankOneAndThePlayersPointsAreKeptWithTheCompanions()
        {
            // The owner, 2026-11-20: "the real will have us start at rr1 and gain rps and see how it goes. We would
            // need to persist bots and companions data (or at least, rps gained)."
            // And: "Start at Realm Rank 1 Level 1."
            Assert.That(PvpRealmPoints.LevelFor(0), Is.EqualTo(1));
            Assert.That(PvpRealmPoints.RankName(PvpRealmPoints.LevelFor(0)), Is.EqualTo("1L1"));
            Assert.That(PvpRealmPoints.RankName(45), Is.EqualTo("5L5"));
            List<PvpRosterEntry> roster = [new(PvpRealmPoints.PlayerBuild, 0, "Talveh", 0), new("bard", 0, "Coneorgus", 0)];
            List<PvpRosterEntry> after = PvpRoster.Credited(roster, new Dictionary<(string, int), long> { [(PvpRealmPoints.PlayerBuild, 0)] = 563, [("bard", 0)] = 563 });
            Assert.That(PvpRoster.Find(after, PvpRealmPoints.PlayerBuild, 0).RealmPoints, Is.EqualTo(563));
            Assert.That(PvpRoster.Find(after, "bard", 0).RealmPoints, Is.EqualTo(563));
        }

        [Test]
        public void AnEnemyGroupIsNeverBelowThePlayersGroup()
        {
            int[] mine = [40, 40, 42, 38, 40, 40, 40, 40];
            Assert.That(PvpRealmPoints.EnemyLevel(0, mine, true), Is.EqualTo(40));
            Assert.That(PvpRealmPoints.EnemyLevel(55, mine, true), Is.EqualTo(55), "what it has earned above that it keeps");
            Assert.That(PvpRealmPoints.EnemyLevel(0, mine, false), Is.EqualTo(0));
            Assert.That(PvpRealmPoints.EnemyRoster("abc", "mid-main"), Is.Not.EqualTo(PvpRealmPoints.EnemyRoster("abc", "alb-main")));
        }

        [Test]
        public void RealmPointsAreAddedToTheMembersThatEarnedThem()
        {
            List<PvpRosterEntry> roster = [new("druid-regrowth", 0, "Eilaea", 1), new("blademaster", 0, "Braninric", 0, 500), new("blademaster", 1, "Fiainaese", 1, 500)];
            List<PvpRosterEntry> after = PvpRoster.Credited(roster, new Dictionary<(string, int), long> { [("blademaster", 1)] = 563, [("druid-regrowth", 0)] = 563 });
            Assert.That(after.Select(entry => entry.RealmPoints), Is.EqualTo(new long[] { 563, 500, 1063 }));
            Assert.That(after.Select(entry => entry.Name), Is.EqualTo(roster.Select(entry => entry.Name)));
        }

        [Test]
        public void ARosterKeptBeforeRealmPointsReadsAsNone()
        {
            var old = System.Text.Json.JsonSerializer.Deserialize<PvpRosterEntry>("{\"Build\":\"bard\",\"Place\":0,\"Name\":\"Coneorgus\",\"Gender\":0}");
            Assert.That(old.RealmPoints, Is.EqualTo(0));
            Assert.That(old.Name, Is.EqualTo("Coneorgus"));
        }

        [Test]
        public void TheSetupWindowOffersPracticeAndReal()
        {
            Pvp8v8Setup setup = Pvp8v8Setup.DefaultFor(eRealm.Hibernia);
            Assert.That(setup.Real, Is.False, "practice unless real is picked");
            Assert.That(setup.Text(eRealm.Hibernia), Does.Contain("<Practice>").And.Contain("[Real]"));
            setup = setup.After("real", eRealm.Hibernia);
            Assert.That(setup.Real, Is.True);
            Assert.That(setup.Text(eRealm.Hibernia), Does.Contain("[Practice]").And.Contain("<Real>"));
            Assert.That(setup.Text(eRealm.Hibernia, "Start puts your character at rank 1L1."), Does.Contain("Start puts your character at rank 1L1."));
            Assert.That(setup.After("practice", eRealm.Hibernia).Text(eRealm.Hibernia, "Start puts your character at rank 1L1."),
                Does.Not.Contain("Start puts"), "said only for a real fight");
            Assert.That(setup.After("practice", eRealm.Hibernia).Real, Is.False);
        }
    }
}

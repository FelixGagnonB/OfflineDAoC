using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The roam: the owner's answers of 2026-11-20 (docs/PVP-8V8-ROADMAP.md, M9.7).</summary>
    [TestFixture]
    public class UT_PvpRoam
    {
        [Test]
        public void EachRealmStartsWhereTheOwnerSaid()
        {
            // "Albion would spawn (and release) at their principal keep. Same for midgard."
            Assert.That(PvpRoam.EmainStarts.Select(start => start.Realm), Is.EquivalentTo(new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }));
            // In front of the outer gate: a bot does not open a keep door, and the keeps' middles are behind two.
            (int X, int Y) albion = PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Albion).Point;
            (int X, int Y) midgard = PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Midgard).Point;
            Assert.That(System.Math.Sqrt(System.Math.Pow(albion.X - 475844, 2) + System.Math.Pow(albion.Y - 343696, 2)), Is.InRange(1_500, 3_500), "the Albion Portal Keep");
            Assert.That(System.Math.Sqrt(System.Math.Pow(midgard.X - 474129, 2) + System.Math.Pow(midgard.Y - 295207, 2)), Is.InRange(1_500, 3_500), "the Midgard Portal Keep");
            // "Hibernia ... could spawn on the hill near the tower in front of Albion Main Gate": the owner's /loc there.
            Assert.That(PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Hibernia).Point, Is.EqualTo((440776, 337761)));
            // Every team first walks to its own end of the open middle, then to the other: never a far goal across the mountains.
            Assert.That(PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Albion).Route.First(), Is.EqualTo(PvpRoam.South));
            Assert.That(PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Midgard).Route.First(), Is.EqualTo(PvpRoam.North));
        }

        [Test]
        public void TheSecondTeamOfALineUpHasItsOwnNameAndSoItsOwnRoster()
        {
            // "I guess if we add teams, we'd need to make sure to persist all the bot / companions and their rps."
            Assert.That(PvpRoam.TeamName("mid-main", 1), Is.EqualTo("mid-main"));
            Assert.That(PvpRoam.TeamName("mid-main", 2), Is.EqualTo("mid-main#2"));
            Assert.That(PvpRealmPoints.EnemyRoster("abc", PvpRoam.TeamName("mid-main", 2)), Is.Not.EqualTo(PvpRealmPoints.EnemyRoster("abc", PvpRoam.TeamName("mid-main", 1))));
        }

        [Test]
        public void AWipedTeamStandsUpAtItsStartOnceTheEnemyHasLeftIt()
        {
            // "We revive at our start point."
            var real = new PvpRealWeights();
            Assert.That(PvpRealWeights.StandsUp(10, false, real), Is.False, "not at once");
            Assert.That(PvpRealWeights.StandsUp(30, false, real), Is.True);
            Assert.That(PvpRealWeights.StandsUp(60, true, real), Is.False, "not under the enemy's feet");
            Assert.That(PvpRealWeights.StandsUp(120, true, real), Is.True, "but it is not kept down for ever");
        }

        [Test]
        public void ATeamAtItsOwnStartIsLeftAloneUnlessTheFightIsAlreadyOn()
        {
            var real = new PvpRealWeights();
            Assert.That(PvpRealWeights.LeftAtItsStart(1_000, false, false, real), Is.True, "nobody camps a start");
            Assert.That(PvpRealWeights.LeftAtItsStart(1_000, true, false, real), Is.False, "a fight that came there goes on");
            Assert.That(PvpRealWeights.LeftAtItsStart(1_000, false, true, real), Is.False, "attacked from there, it answers");
            Assert.That(PvpRealWeights.LeftAtItsStart(6_000, false, false, real), Is.False, "out in the open it is fair game");
            Assert.That(PvpRealWeights.LeftAtItsStart(1_000, false, false, new PvpRealWeights { StartIsSafeWithin = 0 }), Is.False, "off");
        }

        [Test]
        public void ATeamAddsOnItsRealmMatesFightThreeTimesInTen()
        {
            // "It could be random, like 30% would add 70% won't add or so."
            var real = new PvpRealWeights();
            Assert.That(real.AddChancePercent, Is.EqualTo(30));
            Assert.That(Enumerable.Range(0, 100).Count(roll => PvpRoam.Joins(real.AddChancePercent, roll)), Is.EqualTo(30));
            Assert.That(PvpRoam.Joins(0, 0), Is.False, "never");
            Assert.That(PvpRoam.Joins(100, 99), Is.True, "always");
        }

        [Test]
        public void AFightLeftAloneIsLeftAloneByBothSides()
        {
            Assert.That(PvpRoam.LeavesAlone(true, false, false, false), Is.True, "the team that drew it does not attack");
            Assert.That(PvpRoam.LeavesAlone(false, true, false, false), Is.True, "and the team it left to its fight does not turn on it");
            Assert.That(PvpRoam.LeavesAlone(true, false, true, false), Is.False, "attacked, it answers");
            Assert.That(PvpRoam.LeavesAlone(false, true, false, true), Is.False, "the player picks its own fights");
            Assert.That(PvpRoam.LeavesAlone(false, false, false, false), Is.False, "no draw, no truce");
        }

        [Test]
        public void AReleasedPlayersGroupStandsUpWhenItsCompanionsAreDown()
        {
            Assert.That(PvpRoam.IsWiped(0, false, false, false), Is.True, "a team of bots with nobody alive");
            Assert.That(PvpRoam.IsWiped(3, false, false, false), Is.False);
            Assert.That(PvpRoam.IsWiped(0, true, false, false), Is.True, "the player and the seven dead");
            Assert.That(PvpRoam.IsWiped(0, true, true, true), Is.True, "the player released to the start, the seven dead");
            Assert.That(PvpRoam.IsWiped(0, true, true, false), Is.False, "the player is the last one standing in the fight");
            Assert.That(PvpRoam.IsWiped(2, true, true, true), Is.False, "two companions still fight: the player waits for them");
            Assert.That(PvpRoam.ReleasePoint(null), Is.Null, "nobody in a roam releases as the game has it");
        }

        [Test]
        public void ATeamThatLeavesAFightAloneWaitsItOutWhereItIs()
        {
            Assert.That(PvpRoam.WaitsOutAFight(true, 0, false), Is.True, "it does not walk through the fight it left alone");
            Assert.That(PvpRoam.WaitsOutAFight(true, 8, false), Is.False, "another enemy team has come for it");
            Assert.That(PvpRoam.WaitsOutAFight(false, 0, false), Is.False, "nothing near: it walks its route");
            Assert.That(PvpRoam.WaitsOutAFight(true, 0, true), Is.False, "the player goes where the player likes");
        }

        [Test]
        public void ATeamThatStoodAsideJoinsWhenTheOtherSideAdds()
        {
            Assert.That(PvpRoam.EvensTheOdds(1), Is.False, "one against one is theirs to settle");
            Assert.That(PvpRoam.EvensTheOdds(2), Is.True, "two on its realm-mate: it joins after all");
        }

        [Test]
        public void ARealmsSecondTeamOfBotsFollowsItsFirstByHalfAMinute()
        {
            // The owner, 2026-11-21, of a gap of ninety seconds: "The 2nd hib team never left the spawn point".
            var real = new PvpRealWeights();
            Assert.That(PvpRealWeights.SetsOffAfterSeconds(0, real), Is.EqualTo(45), "a realm's first team of bots, the player's realm-mate among them");
            Assert.That(PvpRealWeights.SetsOffAfterSeconds(1, real), Is.EqualTo(75));
            // Albion's first needs some hundred seconds from its gate to the south end of the middle, 6,600 from Hibernia's hill.
            Assert.That(PvpRealWeights.SetsOffAfterSeconds(1, real), Is.LessThan(45 + 100));
        }
    }
}

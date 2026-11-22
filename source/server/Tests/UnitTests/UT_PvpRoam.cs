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
            // First "on the hill near the tower in front of Albion Main Gate" (the owner's /loc, 440776,337761), 6,600 units
            // from the south end of the middle; then, 2026-11-21: "hibernia could start at Dun Crauchon to balance the
            // starting location". In front of the keep, as the two others are in front of theirs.
            PvpRoamStart hibernia = PvpRoam.EmainStarts.Single(start => start.Realm == eRealm.Hibernia);
            Assert.That(System.Math.Sqrt(System.Math.Pow(hibernia.Point.X - 425877, 2) + System.Math.Pow(hibernia.Point.Y - 318720, 2)), Is.InRange(1_500, 3_500), "Dun Crauchon");
            // It reaches both ends of the middle directly: its two teams go opposite ways.
            Assert.That(hibernia.RouteOf(1).First(), Is.EqualTo(PvpRoam.South));
            Assert.That(hibernia.RouteOf(2).First(), Is.EqualTo(PvpRoam.North));
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
            // "I used /release and respawn at correct location... The whole team did not release": it does not wait out the thirty seconds.
            Assert.That(PvpRealWeights.StandsUp(10, false, real, playerIsBack: true), Is.True);
            Assert.That(PvpRealWeights.StandsUp(10, true, real, playerIsBack: true), Is.False, "still not under an enemy's feet");
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
        public void ATeamAddsOnAFightOneTimeInTen()
        {
            // "It could be random, like 30% would add 70% won't add or so."
            var real = new PvpRealWeights();
            // And after a roam of 48 minutes: "I feel like 30% add 70% run by is too high... it always ends up like a huge brawl".
            // And the number the owner then gave: "maybe 15% add 85% run by".
            Assert.That(real.AddChancePercent, Is.EqualTo(15));
            Assert.That(Enumerable.Range(0, 100).Count(roll => PvpRoam.Joins(real.AddChancePercent, roll)), Is.EqualTo(15));
            Assert.That(PvpRoam.Joins(0, 0), Is.False, "never");
            Assert.That(PvpRoam.Joins(100, 99), Is.True, "always");
        }

        [Test]
        public void ThePlayersGroupPassesByWhatThePlayerDoesNotTakeOn()
        {
            // "When /8v8 roam, group should not spread / bard play endurance if we do not engage."
            Assert.That(PvpRoam.PlayerTakesItOn(false, false, false), Is.False, "an enemy team in sight is passed by");
            Assert.That(PvpRoam.PlayerTakesItOn(false, false, true), Is.True, "the player picks one of them as a target");
            Assert.That(PvpRoam.PlayerTakesItOn(false, true, false), Is.True, "they come for the group");
            Assert.That(PvpRoam.PlayerTakesItOn(true, false, false), Is.True, "the fight is on, whatever the player has targeted since");
        }

        [Test]
        public void AnEightVersusEightBardNeverPutsItsDrumAwayToFight()
        {
            // The owner, 2026-11-21: "we had no endurance up during the fight... bard was not dead".
            Assert.That(DOL.AI.Brain.BotBrain.PlaysThroughTheFight(true, eCharacterClass.Bard), Is.True);
            Assert.That(DOL.AI.Brain.BotBrain.PlaysThroughTheFight(false, eCharacterClass.Bard), Is.False, "a gamebot or a companion Bard fights as before");
            Assert.That(DOL.AI.Brain.BotBrain.PlaysThroughTheFight(true, eCharacterClass.Skald), Is.False, "a Skald and a Minstrel are fighters");
            Assert.That(DOL.AI.Brain.BotBrain.PlaysThroughTheFight(true, eCharacterClass.Eldritch), Is.False);
        }

        [Test]
        public void StandingByAFightIsNotBeingInIt()
        {
            // "I think I am also gaining rps for just being close to the fight."
            Assert.That(PvpRoam.IsAt(true, true, true), Is.True, "in combat and aimed at one of theirs");
            Assert.That(PvpRoam.IsAt(true, true, false), Is.False, "in combat with somebody else");
            Assert.That(PvpRoam.IsAt(true, false, true), Is.False, "a target looked at is not a blow");
            Assert.That(PvpRoam.IsAt(false, true, true), Is.False);
        }

        [Test]
        public void ATeamInAFightLetsAnybodyElseWalkBy()
        {
            // "Let say mids are fighting alb and I walk by. Neither mids or albs will try to attack my group."
            Assert.That(PvpRoam.TooBusyFor(1, false, false), Is.True, "in a fight, and the group walking by does nothing");
            Assert.That(PvpRoam.TooBusyFor(1, false, true), Is.False, "\"Only if I decide to add on their fight, they will need to react.\"");
            Assert.That(PvpRoam.TooBusyFor(1, true, false), Is.False, "the one it is fighting");
            Assert.That(PvpRoam.TooBusyFor(0, false, false), Is.False, "a team on its own takes on whom it meets");
        }

        [Test]
        public void ALullIsNotTheEndOfAFight()
        {
            Assert.That(PvpRoam.FightIsOver(3_000), Is.False, "the player between two targets: the Bard keeps its endurance song");
            Assert.That(PvpRoam.FightIsOver(12_000), Is.False);
            Assert.That(PvpRoam.FightIsOver(13_000), Is.True);
        }

        [Test]
        public void TheDrawIsForAnyFightThatIsOn()
        {
            Assert.That(PvpRoam.ComesOnAFight(true, false, false), Is.True, "its realm-mate's fight, or one between the two other realms");
            Assert.That(PvpRoam.ComesOnAFight(false, false, false), Is.False, "a team on its own is simply an enemy");
            Assert.That(PvpRoam.ComesOnAFight(true, true, false), Is.False, "they came into this team's fight");
            Assert.That(PvpRoam.ComesOnAFight(true, false, true), Is.False, "it is in that fight already");
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
        public void ThidrankiIsASecondMapWhoseSixTeamsMeetTwoByTwo()
        {
            Assert.That(PvpRoam.MapFor("thid"), Is.SameAs(PvpRoam.Thidranki));
            Assert.That(PvpRoam.MapFor(" Thidranki "), Is.SameAs(PvpRoam.Thidranki));
            Assert.That(PvpRoam.MapFor("emain"), Is.SameAs(PvpRoam.Emain));
            Assert.That(PvpRoam.MapFor("hib-2bard"), Is.Null, "a group's name is no map");
            Assert.That(PvpRoam.MapFor(null), Is.Null);
            Assert.That(PvpRoam.Thidranki.Region, Is.EqualTo(252));
            Assert.That(PvpRoam.Emain.Region, Is.EqualTo(200));
            Assert.That(PvpRoam.Thidranki.Starts.Select(start => start.Realm), Is.EquivalentTo(new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }));

            // A realm's two teams go round the three points opposite ways...
            foreach (PvpRoamStart start in PvpRoam.Thidranki.Starts)
            {
                Assert.That(start.RouteOf(2), Is.EqualTo(Enumerable.Reverse(start.RouteOf(1)).ToArray()), start.Realm.ToString());
                Assert.That(start.RouteOf(1), Is.EquivalentTo(new[] { PvpRoam.ThidHM, PvpRoam.ThidHA, PvpRoam.ThidMA }));
            }
            // ...so each point is the first of exactly two teams, of two realms.
            var first = PvpRoam.Thidranki.Starts.SelectMany(start => new[] { (start.Realm, Point: start.RouteOf(1)[0]), (start.Realm, Point: start.RouteOf(2)[0]) }).ToList();
            foreach ((int X, int Y) point in new[] { PvpRoam.ThidHM, PvpRoam.ThidHA, PvpRoam.ThidMA })
                Assert.That(first.Where(entry => entry.Point == point).Select(entry => entry.Realm).Distinct().Count(), Is.EqualTo(2), point.ToString());
            // No point within reach of the neutral keep's guards, and none at a start.
            foreach ((int X, int Y) point in new[] { PvpRoam.ThidHM, PvpRoam.ThidHA, PvpRoam.ThidMA })
            {
                Assert.That(System.Math.Sqrt(System.Math.Pow(point.X - 33089, 2) + System.Math.Pow(point.Y - 38271, 2)), Is.GreaterThan(8_000));
                foreach (PvpRoamStart start in PvpRoam.Thidranki.Starts)
                    Assert.That(System.Math.Sqrt(System.Math.Pow(point.X - start.Point.X, 2) + System.Math.Pow(point.Y - start.Point.Y, 2)), Is.GreaterThan(10_000));
            }
            // In Emain Macha both teams of a realm walk the one road.
            Assert.That(PvpRoam.Emain.StartOf(eRealm.Albion).RouteOf(2), Is.EqualTo(PvpRoam.Emain.StartOf(eRealm.Albion).RouteOf(1)));
        }

        [Test]
        public void AfterAFightATeamRaisesItsDeadOrGoesBackToItsStart()
        {
            var real = new PvpRealWeights();
            Assert.That(PvpRealWeights.Reforms(true, 30, real), Is.False, "somebody can raise: it stays with its dead");
            Assert.That(PvpRealWeights.Reforms(true, 90, real), Is.True, "but not for ever");
            Assert.That(PvpRealWeights.Reforms(false, 5, real), Is.False, "nobody can: a moment to be sure the fight is over");
            Assert.That(PvpRealWeights.Reforms(false, 20, real), Is.True, "then it goes back and forms up again");
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
            Assert.That(PvpRoam.EvensTheOdds(2), Is.True, "two of one realm on its realm-mate: it joins after all");
            Assert.That(PvpRoam.EvensTheOdds(0), Is.False);
            Assert.That(PvpRoam.EvensTheOdds(2, true), Is.False, "its realm-mate jumped that fight itself: no help is owed");
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

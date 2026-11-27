using System.Linq;
using DOL.GS;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The owner's session of 2026-11-26 on Agramon (docs/PVP-8V8-ROADMAP.md, M13.37 to M13.39).</summary>
    [TestFixture]
    public class UT_PvpClockAndNews
    {
        private const double Tick = 1000.0 / 30;

        [Test]
        public void TheGamesClockMakesUpForATickThatRanLong()
        {
            // A tick on time: the clock goes on by what it took, and owes nothing.
            double owed = 0;
            Assert.That(GameLoopTickPacer.Advance(Tick, Tick, ref owed), Is.EqualTo(Tick).Within(0.001));
            Assert.That(owed, Is.EqualTo(0).Within(0.001));

            // The turn of an evening took 130 ms: two ticks' time now, the rest in the ticks after it, and then it is level.
            double clock = GameLoopTickPacer.Advance(130, Tick, ref owed);
            Assert.That(clock, Is.EqualTo(2 * Tick).Within(0.001), "no more than two ticks' time in one tick");
            double wall = 130;
            for (int tick = 0; tick < 5; tick++)
            {
                clock += GameLoopTickPacer.Advance(Tick, Tick, ref owed);
                wall += Tick;
            }
            Assert.That(clock, Is.EqualTo(wall).Within(0.001), "nothing of the long tick is lost");
            Assert.That(owed, Is.EqualTo(0).Within(0.001));

            // A server that stood for three seconds does not run at twice its speed for three seconds after it.
            GameLoopTickPacer.Advance(3_000, Tick, ref owed);
            Assert.That(owed, Is.EqualTo(GameLoopTickPacer.MostOwedMilliseconds));

            // A server that cannot keep its rate at all (every tick 50 ms) runs by the wall's clock, at fewer ticks.
            owed = 0;
            Assert.That(GameLoopTickPacer.Advance(50, Tick, ref owed), Is.EqualTo(50).Within(0.001));
            Assert.That(owed, Is.EqualTo(0).Within(0.001));
        }

        [Test]
        public void ABotLosesNoTimeAtAPointOfItsPath()
        {
            Assert.That(NpcMovementComponent.LateAtAPathPoint(18, Tick), Is.EqualTo(18));
            Assert.That(NpcMovementComponent.LateAtAPathPoint(0, Tick), Is.EqualTo(0));
            Assert.That(NpcMovementComponent.LateAtAPathPoint(-12, Tick), Is.EqualTo(0), "early: nothing to give back");
            Assert.That(NpcMovementComponent.LateAtAPathPoint(500, Tick), Is.EqualTo(66), "held for longer: it is not thrown forward");
        }

        [Test]
        public void ADeathIsReadByTheRealmsOfTheDeadAndOfTheKiller()
        {
            Assert.That(PvpDeathNews.Text("Arngenmund", "Jade", "Agramon"), Is.EqualTo("Arngenmund was just killed by Jade in Agramon."));
            Assert.That(PvpDeathNews.Text("Arngenmund", "Jade", null), Is.EqualTo("Arngenmund was just killed by Jade."));
            Assert.That(PvpDeathNews.Text("Arngenmund", null, "Agramon"), Is.EqualTo("Arngenmund was just killed in Agramon."));

            Assert.That(PvpDeathNews.Reads(eRealm.Hibernia, eRealm.Midgard, eRealm.Hibernia, true), Is.True, "one of its realm killed");
            Assert.That(PvpDeathNews.Reads(eRealm.Hibernia, eRealm.Hibernia, eRealm.Albion, true), Is.True, "one of its realm died");
            Assert.That(PvpDeathNews.Reads(eRealm.Hibernia, eRealm.Midgard, eRealm.Albion, true), Is.False, "the two other realms: not its news");
            Assert.That(PvpDeathNews.Reads(eRealm.Hibernia, eRealm.Hibernia, eRealm.Albion, false), Is.False, "another frontier");
            Assert.That(PvpDeathNews.Reads(eRealm.Hibernia, eRealm.Hibernia, null, true), Is.True);

            Assert.That(PvpDeathNews.Colour(eRealm.Midgard), Is.EqualTo(eChatType.CT_KilledByMid));
            Assert.That(PvpDeathNews.Colour(null), Is.EqualTo(eChatType.CT_PlayerDied));
        }

        [Test]
        public void AtARestATimerThatIsHalfRunIsCastAgain()
        {
            // A Druid's resistances: twenty minutes.
            Assert.That(DOL.AI.Brain.BotBrain.CastsTimerAgainAtRest(1_200, 1_100), Is.False, "cast a moment ago");
            Assert.That(DOL.AI.Brain.BotBrain.CastsTimerAgainAtRest(1_200, 601), Is.False);
            Assert.That(DOL.AI.Brain.BotBrain.CastsTimerAgainAtRest(1_200, 599), Is.True, "half run: it would not last the next fights");
            Assert.That(DOL.AI.Brain.BotBrain.CastsTimerAgainAtRest(1_200, 20), Is.True);
            // A buff of half a minute is no timer: it is for the fight it is cast in.
            Assert.That(DOL.AI.Brain.BotBrain.CastsTimerAgainAtRest(30, 5), Is.False);
        }

        [Test]
        public void ATeamDoesNotGoBackToAPointItHasJustBeenAt()
        {
            PvpRoamMap map = PvpRoamZones.Agramon;
            (int X, int Y) farNorthWest = (514288, 451752), westField = (510288, 465752), northWestField = (517288, 458752), middle = (524288, 465752);
            // At the west field, come from the far north-west, having been at the north-west field before that: the middle is the way on.
            for (int roll = 0; roll < 20; roll++)
                Assert.That(PvpRoam.NextPoint(map, westField, farNorthWest, 0, roll, [northWestField, farNorthWest, westField]), Is.EqualTo(middle));
            // Without the memory the north-west field was a way like the other.
            Assert.That(Enumerable.Range(0, 20).Select(roll => PvpRoam.NextPoint(map, westField, farNorthWest, 0, roll)).Distinct().Count(), Is.EqualTo(2));
            // Where every way leads to a point it has just been at, it takes one of them all the same.
            Assert.That(PvpRoam.NextPoint(map, farNorthWest, westField, 0, 0, [northWestField, westField, farNorthWest]), Is.EqualTo(northWestField));
        }

        [Test]
        public void AGroupOfCastersPlaysFromItsBackLine()
        {
            var weights = new PvpOrderWeights();
            Assert.That(weights.PlaysFromItsBackLine(3), Is.True, "Albion's main group: Sorcerer, Theurgist, Cabalist");
            Assert.That(weights.PlaysFromItsBackLine(2), Is.False, "Hibernia's: Eldritch and Enchanter beside three fighters");
            Assert.That(weights.PlaysFromItsBackLine(1), Is.False, "Midgard's: one Runemaster");
            Assert.That(new PvpOrderWeights { CasterGroupFrom = 0 }.PlaysFromItsBackLine(5), Is.False, "the rule is off");
            Assert.That(weights.PlaysFromItsBackLine(3, hasAPlayer: false), Is.False, "a group of bots pushes as before: among bots that is what wins");
            Assert.That(new PvpOrderWeights { CasterGroupOnlyWithAPlayer = false }.PlaysFromItsBackLine(3, hasAPlayer: false), Is.True);
            Assert.That(PvpOrderWeights.WithoutPush(PvpOrder.Push, true), Is.EqualTo(PvpOrder.Engage), "it does not push");
            Assert.That(PvpOrderWeights.WithoutPush(PvpOrder.Push, false), Is.EqualTo(PvpOrder.Push));
            Assert.That(PvpOrderWeights.WithoutPush(PvpOrder.Extend, true), Is.EqualTo(PvpOrder.Extend));
            Assert.That(PvpOrderWeights.WithoutPush(PvpOrder.Regroup, true), Is.EqualTo(PvpOrder.Regroup));
        }

        [Test]
        public void TheMiddleOfTwoFightsIsNoFight()
        {
            // The owner's group near the middle of Agramon, on its way back to the south-west field, with a fight left alone
            // at the north-west field and another at the south-east field.
            (double X, double Y) group = (523168, 467144), southWestField = (517288, 472752);
            (double X, double Y) northWestFight = PvpRoam.MiddleOf([(518702, 458496), (518710, 459396)]);
            (double X, double Y) southEastFight = PvpRoam.MiddleOf([(530809, 472711), (530312, 473191)]);
            int side = 0;
            Assert.That(PvpRoam.AroundAFight(group, southWestField, northWestFight, ref side), Is.Null, "its road does not pass that fight");
            Assert.That(PvpRoam.AroundAFight(group, southWestField, southEastFight, ref side), Is.Null, "nor that one");
            // Their middle is 1,950 units from the group: taken for a fight, the group went round it.
            (double X, double Y) middle = PvpRoam.MiddleOf([northWestFight, southEastFight]);
            Assert.That(PvpRoam.AroundAFight(group, southWestField, middle, ref side), Is.Not.Null);
        }

        [Test]
        public void AHealerOrCasterHitsAGreyPetThatIsOnIt()
        {
            Assert.That(DOL.AI.Brain.BotBrain.StaffsAGreyPet(false, true, true, false), Is.True, "a Druid's lesser spirit on a Cleric: one blow");
            Assert.That(DOL.AI.Brain.BotBrain.StaffsAGreyPet(false, false, true, false), Is.False, "a pet of its own level is not fenced with");
            Assert.That(DOL.AI.Brain.BotBrain.StaffsAGreyPet(false, true, true, true), Is.False, "with an enemy fighter on it the pet is the least of its troubles");
            Assert.That(DOL.AI.Brain.BotBrain.StaffsAGreyPet(true, true, true, false), Is.False, "a fighter has its own targets");
            Assert.That(DOL.AI.Brain.BotBrain.StaffsAGreyPet(false, true, false, false), Is.False);
        }

        [Test]
        public void ATeamDoesNotTakeAWayThatPassesAFightThatIsNotItsOwn()
        {
            PvpRoamMap map = PvpRoamZones.Agramon;
            (int X, int Y) farNorthWest = (514288, 451752), westField = (510288, 465752), northWestField = (517288, 458752),
                middle = (524288, 465752), northEastField = (531288, 458752);
            // The owner's group at the north-west field, turned away from a fight on its way to the north-east field, the
            // west field and the far north-west among its last three points, and the fight at the middle.
            (double X, double Y)[] fights = [(523800, 465500)];
            Assert.That(PvpRoam.LegPassesAFight(northWestField, middle, fights), Is.True);
            Assert.That(PvpRoam.LegPassesAFight(northWestField, westField, fights), Is.False);
            for (int roll = 0; roll < 20; roll++)
                Assert.That(PvpRoam.NextPoint(map, northWestField, northEastField, 0, roll, [westField, farNorthWest, northWestField], fights),
                    Is.Not.EqualTo(middle), "back to a point it has been at before a way through a fight");
            // Without a fight the middle is where it goes: the only way that is neither back nor a point it has just been at.
            Assert.That(PvpRoam.NextPoint(map, northWestField, northEastField, 0, 0, [westField, farNorthWest, northWestField]), Is.EqualTo(middle));
            // Where every way passes the fight, it takes one all the same.
            Assert.That(map.From(middle), Does.Contain(PvpRoam.NextPoint(map, middle, null, 0, 0, null, fights)));
        }

        [Test]
        public void AGroupRestsForItsTimersSoThatNoneRunsOutInAFight()
        {
            // After a fight, hurt or not: a timer that is half run is cast again before the next one.
            Assert.That(DOL.AI.Brain.BotBrain.RestsForItsTimers(0.49, false), Is.True);
            Assert.That(DOL.AI.Brain.BotBrain.RestsForItsTimers(0.51, false), Is.False);
            // Later on the road it stops only for one of which a quarter is left.
            Assert.That(DOL.AI.Brain.BotBrain.RestsForItsTimers(0.49, true), Is.False, "it has rested since its fight: it does not stop again at half");
            Assert.That(DOL.AI.Brain.BotBrain.RestsForItsTimers(0.24, true), Is.True, "five minutes of twenty left: it stops");
            Assert.That(DOL.AI.Brain.BotBrain.RestsForItsTimers(1, false), Is.False, "no timer runs low");
        }

        [Test]
        public void UnderExtendAFighterKeepsToTheReachOfItsBackLine()
        {
            // The kill target, a Druid at the back of the other group, 1,600 units from its own healers: it does not set out for it.
            Assert.That(PvpOrders.ExtendFighterTakes(1_600, false, 1_000), Is.False);
            Assert.That(PvpOrders.ExtendFighterTakes(900, false, 1_000), Is.True, "an enemy near its healers is its to take");
            Assert.That(PvpOrders.ExtendFighterTakes(1_200, true, 1_000), Is.True, "the one it is on is kept a little farther out");
            Assert.That(PvpOrders.ExtendFighterTakes(1_200, false, 1_000), Is.False);
            Assert.That(PvpOrders.ExtendFighterTakes(1_300, true, 1_000), Is.False);
            Assert.That(PvpOrders.ExtendFighterTakes(5_000, false, 0), Is.True, "the rule is off");

            // With nobody to take it goes to its healers and stands there; with somebody, it fights, and is not sent
            // back at the edge of the reach and out again a second later.
            Assert.That(PvpOrders.ExtendFighterComesBack(false, 300, 1_000), Is.True);
            Assert.That(PvpOrders.ExtendFighterComesBack(true, 1_100, 1_000), Is.False, "it was sent back here, and set out again the next second");
            Assert.That(PvpOrders.ExtendFighterComesBack(true, 1_600, 1_000), Is.True, "far beyond the reach: back first");
            Assert.That(PvpOrders.ExtendFighterComesBack(false, 2_000, 0), Is.False, "the rule is off");
            Assert.That(PvpOrders.ExtendFighterStands(400, 1_000), Is.True, "it does not walk on to the middle of its healers");
            Assert.That(PvpOrders.ExtendFighterStands(700, 1_000), Is.False);
        }

        [Test]
        public void AGroupItsCompanionDrivesGoesOnlyForAGroupItSees()
        {
            Assert.That(PvpDriver.KnowsOf(false, false, null), Is.False, "9,000 units off behind a hill: it does not know of it");
            Assert.That(PvpDriver.KnowsOf(false, false, 0), Is.True, "one of its seven sees it");
            Assert.That(PvpDriver.KnowsOf(false, false, PvpDriver.RemembersForSeconds), Is.True, "gone behind a hill a moment ago");
            Assert.That(PvpDriver.KnowsOf(false, false, PvpDriver.RemembersForSeconds + 1), Is.False);
            Assert.That(PvpDriver.KnowsOf(true, false, null), Is.True, "it is fighting it");
            Assert.That(PvpDriver.KnowsOf(false, true, null), Is.True, "it is attacked by it");
        }
    }
}

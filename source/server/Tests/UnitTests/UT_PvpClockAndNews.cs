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

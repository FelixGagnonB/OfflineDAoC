using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpKillTarget
    {
        [Test]
        public void ATargetThatIsNotGoingDownIsGivenUp()
        {
            // A third of the kill calls were given up after 27 seconds with the target at 94% health.
            // Off as the weights are (it did not help in 24 matches); this is the leaning when it is switched on.
            var weights = new PvpKillWeights { PerStaleSecond = -4, RecentlyAbandoned = -80 };
            Assert.That(PvpKillTarget.Choose([new PvpTargetView(0, PvpBuffArchetype.Healer, 95, false, 800, StaleSeconds: 30),
                new PvpTargetView(1, PvpBuffArchetype.Caster, 100, false, 800)], 0), Is.EqualTo(0), "off: the healer is kept");
            var healer = new PvpTargetView(0, PvpBuffArchetype.Healer, 95, false, 800);
            var caster = new PvpTargetView(1, PvpBuffArchetype.Caster, 100, false, 800);
            Assert.Multiple(() =>
            {
                Assert.That(PvpKillTarget.StaleSeconds(8, 95, weights), Is.EqualTo(0), "the first ten seconds are free");
                Assert.That(PvpKillTarget.StaleSeconds(25, 95, weights), Is.EqualTo(15));
                Assert.That(PvpKillTarget.StaleSeconds(25, 60, weights), Is.EqualTo(0), "it is going down: stay on it");
                Assert.That(PvpKillTarget.Choose([healer, caster], 0, weights), Is.EqualTo(0), "fresh: the healer is kept");
                Assert.That(PvpKillTarget.Choose([healer with { StaleSeconds = 30 }, caster], 0, weights), Is.EqualTo(1), "half a minute on it for nothing: the caster");
                // And it is not called again at once.
                Assert.That(PvpKillTarget.Choose([healer with { Abandoned = true }, caster], 1, weights), Is.EqualTo(1));
                Assert.That(PvpKillTarget.Choose([healer, caster], -1, weights), Is.EqualTo(0), "later it may be");
            });
        }

        [Test]
        public void TheOneWhosePetsAreOnTheHealersIsWorthKilling()
        {
            // Albion's Theurgist called 21.6 pets a match against Midgard and was hardly ever attacked.
            var healer = new PvpTargetView(0, PvpBuffArchetype.Healer, 100, false, 1_500);
            var theurgist = new PvpTargetView(1, PvpBuffArchetype.Caster, 100, false, 1_500);
            Assert.That(PvpKillTarget.Choose([healer, theurgist], -1), Is.EqualTo(0), "no pets out: the healer, as before");
            Assert.That(PvpKillTarget.Choose([healer, theurgist with { PetsOnOurBackline = 4 }], -1), Is.EqualTo(1));
            Assert.That(PvpKillTarget.Choose([healer, theurgist with { PetsOnOurBackline = 1 }], -1), Is.EqualTo(0), "one pet is not enough");
            // More than four count as four.
            Assert.That(PvpKillTarget.Score(theurgist with { PetsOnOurBackline = 9 }), Is.EqualTo(PvpKillTarget.Score(theurgist with { PetsOnOurBackline = 4 })));
        }

        [Test]
        public void AnEnemyOutOfPositionIsWorthMore_AndTheWeightsDecide()
        {
            var home = new PvpTargetView(0, PvpBuffArchetype.Caster, 100, false, 1000, 200);
            var forward = new PvpTargetView(1, PvpBuffArchetype.Caster, 100, false, 1000, 1500);
            Assert.That(PvpKillTarget.Score(forward), Is.EqualTo(PvpKillTarget.Score(home) + 65));
            // What 150 matches showed: a Cleric at the back, 2,200 units off, against a Sorcerer that
            // came forward. The Cleric is called first, from afar; the Sorcerer takes the call over.
            var cleric = new PvpTargetView(3, PvpBuffArchetype.Healer, 100, false, 2200, 200);
            var sorcerer = new PvpTargetView(4, PvpBuffArchetype.Caster, 100, false, 300, 1300);
            Assert.That(PvpKillTarget.Choose([cleric, sorcerer], 3), Is.EqualTo(4));
            Assert.That(PvpKillTarget.Choose([home, forward], -1), Is.EqualTo(1));
            // The same two enemies, with weights that favour fighters: none of this is a rule.
            var fighter = new PvpTargetView(2, PvpBuffArchetype.Melee, 100, false, 1000);
            Assert.That(PvpKillTarget.Choose([home, fighter], -1), Is.EqualTo(0));
            Assert.That(PvpKillTarget.Choose([home, fighter], -1, new PvpKillWeights { OnFighter = 250 }), Is.EqualTo(2));
        }

        private static PvpTargetView View(int id, PvpBuffArchetype archetype, int health = 100, bool mezzed = false, int distance = 500) =>
            new(id, archetype, health, mezzed, distance);

        [Test]
        public void HealersBeforeCastersBeforeFighters()
        {
            List<PvpTargetView> enemies = [View(0, PvpBuffArchetype.Melee), View(1, PvpBuffArchetype.Caster), View(2, PvpBuffArchetype.Healer)];
            Assert.That(PvpKillTarget.Choose(enemies, -1), Is.EqualTo(2));
            Assert.That(PvpKillTarget.Choose([enemies[0], enemies[1]], -1), Is.EqualTo(1));
        }

        [Test]
        public void AMezzedEnemyIsLeftAsleepWhileAnotherIsAwake()
        {
            List<PvpTargetView> enemies = [View(0, PvpBuffArchetype.Healer, mezzed: true), View(1, PvpBuffArchetype.Caster)];
            Assert.That(PvpKillTarget.Choose(enemies, -1), Is.EqualTo(1));
            Assert.That(PvpKillTarget.Choose([enemies[0]], -1), Is.EqualTo(0));
        }

        [Test]
        public void TheCalledTargetIsKeptUntilSomethingClearlyBetterAppears()
        {
            List<PvpTargetView> enemies = [View(0, PvpBuffArchetype.Caster, health: 60), View(1, PvpBuffArchetype.Healer)];
            Assert.That(PvpKillTarget.Choose(enemies, 0), Is.EqualTo(0), "a small edge is not worth a switch");
            List<PvpTargetView> swing = [View(0, PvpBuffArchetype.Melee), View(1, PvpBuffArchetype.Healer, health: 30)];
            Assert.That(PvpKillTarget.Choose(swing, 0), Is.EqualTo(1));
        }

        [Test]
        public void EnemiesOutOfReachAreNotCalled()
        {
            Assert.That(PvpKillTarget.Choose([View(0, PvpBuffArchetype.Healer, distance: 5000)], -1), Is.EqualTo(-1));
            Assert.That(PvpKillTarget.Choose([View(0, PvpBuffArchetype.Healer, distance: 5000), View(1, PvpBuffArchetype.Melee, distance: 800)], -1), Is.EqualTo(1));
        }

        [Test]
        public void AWoundedEnemyScoresHigherThanAHealthyOneOfItsKind()
        {
            Assert.That(PvpKillTarget.Score(View(0, PvpBuffArchetype.Caster, health: 40)),
                Is.GreaterThan(PvpKillTarget.Score(View(1, PvpBuffArchetype.Caster))));
        }
    }
}

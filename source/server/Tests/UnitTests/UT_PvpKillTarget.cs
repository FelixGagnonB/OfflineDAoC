using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpKillTarget
    {
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

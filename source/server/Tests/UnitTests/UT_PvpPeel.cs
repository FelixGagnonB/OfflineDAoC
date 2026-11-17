using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpPeel
    {
        private static readonly PvpPeelWeights Weights = new();

        private static PvpWardView Ward(int id, PvpBuffArchetype archetype, int attackers = 0, int health = 100, int distance = 200,
            bool current = false, bool guardedByAnother = false) =>
            new(id, archetype, attackers, health, distance, current, guardedByAnother);

        private static PvpPeelView Enemy(int id, bool onWard = false, bool onBackline = false, bool onSelf = false, bool kill = false,
            bool canStun = true, bool mezzed = false, int fromWard = 150, bool current = false) =>
            new(id, onWard, onBackline, onSelf, kill, canStun, mezzed, fromWard, current);

        [Test]
        public void WithNobodyAttackedAHealerIsGuardedBeforeACaster()
        {
            List<PvpWardView> wards = [Ward(0, PvpBuffArchetype.Caster), Ward(1, PvpBuffArchetype.Healer)];
            Assert.That(PvpPeel.ChooseWard(wards, Weights), Is.EqualTo(1));
        }

        [Test]
        public void TheGroupmateUnderAttackIsGuarded()
        {
            List<PvpWardView> wards = [Ward(0, PvpBuffArchetype.Healer, current: true), Ward(1, PvpBuffArchetype.Caster, attackers: 2)];
            Assert.That(PvpPeel.ChooseWard(wards, Weights), Is.EqualTo(1), "two enemies on the caster outweigh a healer nobody touches");
        }

        [Test]
        public void GuardDoesNotHopBetweenTwoGroupmatesInTheSameTrouble()
        {
            List<PvpWardView> wards = [Ward(0, PvpBuffArchetype.Healer, attackers: 1), Ward(1, PvpBuffArchetype.Healer, attackers: 1, current: true)];
            Assert.That(PvpPeel.ChooseWard(wards, Weights), Is.EqualTo(1));
        }

        [Test]
        public void TwoShieldFightersDoNotGuardTheSameGroupmate()
        {
            List<PvpWardView> wards = [Ward(0, PvpBuffArchetype.Healer, guardedByAnother: true), Ward(1, PvpBuffArchetype.Healer)];
            Assert.That(PvpPeel.ChooseWard(wards, Weights), Is.EqualTo(1));
            Assert.That(PvpPeel.ChooseWard([], Weights), Is.EqualTo(-1));
        }

        [Test]
        public void AnEnemyOnTheWardComesBeforeTheKillTarget()
        {
            List<PvpPeelView> enemies = [Enemy(0, kill: true, fromWard: 200), Enemy(1, onWard: true)];
            Assert.That(PvpPeel.ChooseTarget(enemies, Weights, out double score), Is.EqualTo(1));
            Assert.That(score, Is.GreaterThan(100));
        }

        [Test]
        public void TheKillTargetIsHelpedWithOnlyWhileItIsNearTheWard()
        {
            Assert.That(PvpPeel.ChooseTarget([Enemy(0, kill: true, fromWard: 400)], Weights, out _), Is.EqualTo(0));
            Assert.That(PvpPeel.ChooseTarget([Enemy(0, kill: true, fromWard: 1_200)], Weights, out _), Is.EqualTo(-1),
                "guard stops working that far from the ward, so the shield fighter stays");
        }

        [Test]
        public void AnEnemyOnNobodyIsNoTarget()
        {
            Assert.That(PvpPeel.ChooseTarget([Enemy(0)], Weights, out _), Is.EqualTo(-1));
            Assert.That(PvpPeel.ChooseTarget([], Weights, out _), Is.EqualTo(-1));
        }

        [Test]
        public void AMezzedEnemyIsLeftAsleepUnlessItIsTheKillTarget()
        {
            Assert.That(PvpPeel.ChooseTarget([Enemy(0, onBackline: true, mezzed: true)], Weights, out _), Is.EqualTo(-1));
            Assert.That(PvpPeel.ChooseTarget([Enemy(0, kill: true, mezzed: true)], Weights, out _), Is.EqualTo(0));
        }

        [Test]
        public void AnEnemySlamWouldLandOnIsPreferred()
        {
            List<PvpPeelView> enemies = [Enemy(0, onWard: true, canStun: false), Enemy(1, onWard: true)];
            Assert.That(PvpPeel.ChooseTarget(enemies, Weights, out _), Is.EqualTo(1));
        }

        [Test]
        public void TheShieldFighterDefendsItselfWhenNothingElseCalls()
        {
            Assert.That(PvpPeel.ChooseTarget([Enemy(0, onSelf: true)], Weights, out _), Is.EqualTo(0));
            Assert.That(PvpPeel.Describe(Enemy(0, onSelf: true)), Is.EqualTo("Defend"));
            Assert.That(PvpPeel.Describe(Enemy(0, onWard: true, kill: true)), Is.EqualTo("Peel"));
            Assert.That(PvpPeel.Describe(Enemy(0, kill: true)), Is.EqualTo("Assist"));
        }

        [Test]
        public void WeightsDecideNotRules()
        {
            // An owner who wants the shield fighter on the kill no matter what only changes numbers.
            var aggressive = new PvpPeelWeights { KillTarget = 150, PerHundredUnitsFromWard = 0 };
            List<PvpPeelView> enemies = [Enemy(0, kill: true, fromWard: 1_200), Enemy(1, onWard: true)];
            Assert.That(PvpPeel.ChooseTarget(enemies, aggressive, out _), Is.EqualTo(0));
        }
    }
}

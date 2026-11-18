using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpReading
    {
        private static readonly PvpOrderWeights Weights = new();

        private static PvpMemberState Fighter(bool held = false, bool slowed = false, bool alive = true) => new(true, false, alive, held, slowed, false);
        private static PvpMemberState Healer(bool alive = true, bool held = false, bool interrupted = false) => new(false, true, alive, held, false, interrupted);
        private static PvpMemberState Caster(bool held = false, bool interrupted = false) => new(false, false, true, held, false, interrupted);

        // Two healers, three casters, three fighters, everybody free.
        private static PvpReading Fresh() => PvpReading.Of(
            Enumerable.Repeat(Healer(), 2).Concat(Enumerable.Repeat(Caster(), 3)).Concat(Enumerable.Repeat(Fighter(), 3)));

        [Test]
        public void TheReadingCountsFightersHeldCastersFreeAndHealersFree()
        {
            PvpReading reading = PvpReading.Of([
                Fighter(held: true), Fighter(slowed: true), Fighter(), Fighter(alive: false),
                Healer(alive: false), Healer(interrupted: true), Caster(), Caster(held: true),
            ]);
            Assert.That(reading.Alive, Is.EqualTo(6));
            Assert.That(reading.FightersHeldShare, Is.EqualTo(2 / 3.0).Within(0.001), "of the living fighters");
            Assert.That(reading.CastersFreeShare, Is.EqualTo(1 / 3.0).Within(0.001), "the interrupted healer and the mezzed caster cannot cast");
            Assert.That(reading.HealersFreeShare, Is.EqualTo(0), "one dead, one interrupted");
            Assert.That(reading.HealersBusyShare, Is.EqualTo(0.5), "only the living one is busy");
        }

        [Test]
        public void TwoFreshGroupsEngage()
        {
            Assert.That(PvpOrders.Choose(Fresh(), Fresh(), 0, PvpOrder.Engage, 10, Weights), Is.EqualTo(PvpOrder.Engage));
        }

        [Test]
        public void AGroupPushesWhenTheEnemysHealersAreDown()
        {
            PvpReading theirs = PvpReading.Of(
                Enumerable.Repeat(Healer(alive: false), 2).Concat(Enumerable.Repeat(Caster(), 3)).Concat(Enumerable.Repeat(Fighter(), 3)));
            Assert.That(PvpOrders.Choose(Fresh(), theirs, 0, PvpOrder.Engage, 10, Weights), Is.EqualTo(PvpOrder.Push));
        }

        [Test]
        public void AGroupPushesWhenTheEnemysHealersAreAsleepAndItsFightersHeld()
        {
            PvpReading theirs = PvpReading.Of(
                Enumerable.Repeat(Healer(held: true), 2).Concat(Enumerable.Repeat(Caster(), 3)).Concat(Enumerable.Repeat(Fighter(held: true), 3)));
            Assert.That(PvpOrders.Choose(Fresh(), theirs, 0, PvpOrder.Engage, 10, Weights), Is.EqualTo(PvpOrder.Push));
        }

        [Test]
        public void AGroupExtendsWhenItsHealersCannotHealAndTheEnemysFightersAreFree()
        {
            PvpReading own = PvpReading.Of(
                Enumerable.Repeat(Healer(interrupted: true), 2).Concat(Enumerable.Repeat(Caster(), 3)).Concat(Enumerable.Repeat(Fighter(), 3)));
            Assert.That(PvpOrders.Choose(own, Fresh(), 2, PvpOrder.Engage, 10, Weights), Is.EqualTo(PvpOrder.Extend));
        }

        [Test]
        public void DeadHealersAreNoReasonToExtend()
        {
            // Nobody is left to protect by falling back.
            PvpReading own = PvpReading.Of(
                Enumerable.Repeat(Healer(alive: false), 2).Concat(Enumerable.Repeat(Caster(), 3)).Concat(Enumerable.Repeat(Fighter(), 3)));
            Assert.That(PvpOrders.Choose(own, Fresh(), 0, PvpOrder.Engage, 10, Weights), Is.EqualTo(PvpOrder.Engage));
        }

        [Test]
        public void AnOrderStandsForAFewSeconds()
        {
            PvpReading own = PvpReading.Of(
                Enumerable.Repeat(Healer(interrupted: true), 2).Concat(Enumerable.Repeat(Caster(), 3)).Concat(Enumerable.Repeat(Fighter(), 3)));
            Assert.That(PvpOrders.Choose(own, Fresh(), 2, PvpOrder.Engage, 1, Weights), Is.EqualTo(PvpOrder.Engage), "too soon to change");
            Assert.That(PvpOrders.Choose(Fresh(), Fresh(), 0, PvpOrder.Extend, 1, Weights), Is.EqualTo(PvpOrder.Extend), "and too soon to change back");
            Assert.That(PvpOrders.Choose(Fresh(), Fresh(), 0, PvpOrder.Extend, 10, Weights), Is.EqualTo(PvpOrder.Engage));
        }

        [Test]
        public void AGroupWhoseMezOutrangesTheEnemysHolds()
        {
            Assert.That(PvpOrders.Holds(1875, 1500, 2, 3000, Weights), Is.True, "a Sorcerer against a Healer or a Bard");
            Assert.That(PvpOrders.Holds(1500, 1500, 2, 3000, Weights), Is.False, "no advantage");
            Assert.That(PvpOrders.Holds(1500, 1875, 2, 3000, Weights), Is.False, "outranged: it has to close");
        }

        [Test]
        public void AHoldEndsWhenTheEnemyIsOnTheGroupOrDoesNotCome()
        {
            Assert.That(PvpOrders.Holds(1875, 1500, 2, 1000, Weights), Is.False, "they are on the group");
            Assert.That(PvpOrders.Holds(1875, 1500, 11, 3000, Weights), Is.False, "they are not coming");
            Assert.That(PvpOrders.Holds(1875, 1500, 2, 3000, new PvpOrderWeights { HoldRangeAdvantage = 0 }), Is.False, "holding turned off");
        }

        [Test]
        public void AnOutrangedGroupSometimesWaitsForTheHolderToCome()
        {
            // A Healer's or Bard's group against a Sorcerer's: in half the matches it stands as well.
            Assert.That(PvpOrders.WaitsOutAHold(1500, 1875, 2, 3000, 10, Weights), Is.True);
            Assert.That(PvpOrders.WaitsOutAHold(1500, 1875, 2, 3000, 80, Weights), Is.False, "in the other half it walks in");
            Assert.That(PvpOrders.WaitsOutAHold(1875, 1500, 2, 3000, 10, Weights), Is.False, "the group with the longer mez holds for its own reason");
            Assert.That(PvpOrders.WaitsOutAHold(1500, 1875, 15, 3000, 10, Weights), Is.True, "it outlasts the holder's ten seconds");
            Assert.That(PvpOrders.WaitsOutAHold(1500, 1875, 26, 3000, 10, Weights), Is.False);
            Assert.That(PvpOrders.WaitsOutAHold(1500, 1875, 2, 1000, 10, Weights), Is.False, "the enemy is on the group");
        }

        [Test]
        public void TheEnemysMezzerIsNearsightedFirst()
        {
            var weights = new PvpWeights();
            var caster = new PvpOption(PvpActionKind.Nearsight, 0, PvpBuffArchetype.Caster, false, false, 100, false, false, false, false, 100);
            Assert.That(PvpActionChoice.Score(eCharacterClass.Runemaster, caster with { OnController = true }, weights),
                Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Runemaster, caster, weights)));
        }

        [Test]
        public void OrdersCanBeTurnedOff()
        {
            PvpReading theirs = PvpReading.Of(
                Enumerable.Repeat(Healer(alive: false), 2).Concat(Enumerable.Repeat(Fighter(), 3)));
            Assert.That(PvpOrders.Choose(Fresh(), theirs, 0, PvpOrder.Engage, 10, new PvpOrderWeights { Enabled = false }), Is.EqualTo(PvpOrder.Engage));
        }
    }
}

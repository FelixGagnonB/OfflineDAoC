using System;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpNotice
    {
        [Test]
        public void ABotGoesOnThinkingAnEnemyImmuneForAWhileAfterItIsNot()
        {
            // The owner, 2026-11-20: "It's crazy how stun, nearsight and mez land close to when immunity runs off.
            // Humans are not that precise."
            var belief = new PvpNotice.Belief();
            Assert.That(PvpNotice.ThinksImmune(ref belief, immuneNow: true, now: 1_000, () => 9_000), Is.True);
            Assert.That(PvpNotice.ThinksImmune(ref belief, true, 60_000, () => 9_000), Is.True);
            // It ends at 61 s: the bot notices nine seconds later, by the draw made at that moment and not again.
            Assert.That(PvpNotice.ThinksImmune(ref belief, false, 61_000, () => 9_000), Is.True);
            Assert.That(PvpNotice.ThinksImmune(ref belief, false, 69_900, () => throw new InvalidOperationException("drawn twice")), Is.True);
            Assert.That(PvpNotice.ThinksImmune(ref belief, false, 70_000, () => 9_000), Is.False);
            Assert.That(PvpNotice.ThinksImmune(ref belief, false, 70_100, () => 9_000), Is.False, "and stays noticed");
        }

        [Test]
        public void AnImmunityItNeverSawItDoesNotImagine()
        {
            var belief = new PvpNotice.Belief();
            Assert.That(PvpNotice.ThinksImmune(ref belief, false, 5_000, () => 9_000), Is.False);
        }

        [Test]
        public void ImmuneAgainBeforeItNoticedIsSimplyImmune()
        {
            var belief = new PvpNotice.Belief();
            PvpNotice.ThinksImmune(ref belief, true, 0, () => 9_000);
            PvpNotice.ThinksImmune(ref belief, false, 1_000, () => 9_000);
            Assert.That(PvpNotice.ThinksImmune(ref belief, true, 2_000, () => 9_000), Is.True);
            // The next ending is drawn afresh.
            Assert.That(PvpNotice.ThinksImmune(ref belief, false, 3_000, () => 2_000), Is.True);
            Assert.That(PvpNotice.ThinksImmune(ref belief, false, 5_000, () => 2_000), Is.False);
        }

        [Test]
        public void TheDelayIsDrawnBetweenTheTwoWeightsAndZeroTurnsItOff()
        {
            var weights = new PvpHumanWeights();
            Assert.That(weights.ImmunityEndNoticedAfterMaxSeconds, Is.GreaterThan(0), "on as built");
            var random = new Random(7);
            for (int i = 0; i < 200; i++)
                Assert.That(PvpNotice.Draw(weights, random), Is.InRange(weights.ImmunityEndNoticedAfterMinSeconds * 1000, weights.ImmunityEndNoticedAfterMaxSeconds * 1000));
            Assert.That(PvpNotice.Draw(new PvpHumanWeights { ImmunityEndNoticedAfterMinSeconds = 0, ImmunityEndNoticedAfterMaxSeconds = 0 }, random), Is.EqualTo(0));
        }
    }
}

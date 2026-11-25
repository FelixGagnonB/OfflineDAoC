using System;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpHands
    {
        [Test]
        public void OnlyTheBadLevelHasSlowHands()
        {
            // The owner, 2026-11-24: "In my opinion, the bad group are not that bad". Its member did as much damage a minute as anybody's.
            PvpHumanWeights bad = PvpHumanWeights.For(PvpSkill.Bad);
            Assert.That(bad.HesitatesPercent, Is.GreaterThan(0));
            Assert.That(bad.PlainSwingPercent, Is.GreaterThan(0));
            foreach (PvpSkill level in new[] { PvpSkill.Casual, PvpSkill.Seasoned, PvpSkill.Sharp, PvpSkill.Elite })
            {
                PvpHumanWeights weights = PvpHumanWeights.For(level);
                Assert.That((weights.HesitatesPercent, weights.PlainSwingPercent), Is.EqualTo((0, 0)), level.ToString());
                Assert.That(PvpHands.DrawHesitation(weights, new Random(1)), Is.Zero);
                Assert.That(PvpHands.IsPlainSwing(weights, new Random(1)), Is.False);
            }
        }

        [Test]
        public void TheWaitIsDrawnBetweenTheTwoAndNotEveryTime()
        {
            PvpHumanWeights bad = PvpHumanWeights.For(PvpSkill.Bad);
            var random = new Random(7);
            int waited = 0, plain = 0;
            for (int i = 0; i < 2_000; i++)
            {
                int wait = PvpHands.DrawHesitation(bad, random);
                if (wait > 0)
                {
                    waited++;
                    Assert.That(wait, Is.InRange(bad.HesitatesMinMilliseconds, bad.HesitatesMaxMilliseconds));
                }
                if (PvpHands.IsPlainSwing(bad, random))
                    plain++;
            }
            Assert.That(waited, Is.InRange(2_000 * (bad.HesitatesPercent - 6) / 100, 2_000 * (bad.HesitatesPercent + 6) / 100));
            Assert.That(plain, Is.InRange(2_000 * (bad.PlainSwingPercent - 6) / 100, 2_000 * (bad.PlainSwingPercent + 6) / 100));
        }

        [Test]
        public void OneWaitForOneCast()
        {
            // It waits two seconds, then casts: the wait that is over is not followed by another before the cast has begun.
            (bool waits, long until, bool drawn) = PvpHands.Look(now: 10_000, until: 0, drawn: false, () => 2_000);
            Assert.That((waits, until, drawn), Is.EqualTo((true, 12_000L, true)));
            (waits, until, drawn) = PvpHands.Look(11_900, until, drawn, () => throw new InvalidOperationException("drawn twice"));
            Assert.That(waits, Is.True);
            (waits, until, drawn) = PvpHands.Look(12_000, until, drawn, () => throw new InvalidOperationException("drawn twice"));
            Assert.That((waits, drawn), Is.EqualTo((false, true)), "its hands are free, and stay so until it has cast");
            // The cast has begun (drawn is taken back): the next is drawn for again, and this time there is no wait.
            (waits, until, drawn) = PvpHands.Look(15_000, until, drawn: false, () => 0);
            Assert.That((waits, drawn), Is.EqualTo((false, true)));
        }
    }
}

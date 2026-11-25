using System;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.Tests.Unit.Gameserver
{
    /// <summary>docs/PVP-8V8-ROADMAP.md, M10.1 and M10.2: reaction time, attention, mistakes, and the named levels.</summary>
    [TestFixture]
    public class UT_PvpHuman
    {
        [Test]
        public void ABotAnswersSomethingNewAfterItsReactionTime()
        {
            // The owner, 2026-11-20: "the biggest one is reaction time".
            (long Began, long ReadyAt) memory = default;
            Assert.That(PvpNotice.Reacted(ref memory, 10_000, 10_000, () => 800), Is.False, "the mez has just landed");
            Assert.That(PvpNotice.Reacted(ref memory, 10_000, 10_500, () => 5), Is.False, "the same mez is not drawn for twice");
            Assert.That(PvpNotice.Reacted(ref memory, 10_000, 10_800, () => 5), Is.True);
            Assert.That(PvpNotice.Reacted(ref memory, 20_000, 20_100, () => 300), Is.False, "another mez, another reaction time");
            Assert.That(PvpNotice.Reacted(ref memory, 20_000, 20_300, () => 300), Is.True);
        }

        [Test]
        public void AReactionTimeIsBetweenItsBoundsAndLongerWhenTheBotWasNotLooking()
        {
            var weights = new PvpHumanWeights { ReactionMinMilliseconds = 400, ReactionMaxMilliseconds = 1_200, DistractedPercent = 0 };
            var random = new Random(7);
            int[] drawn = Enumerable.Range(0, 500).Select(_ => PvpNotice.DrawReaction(weights, random)).ToArray();
            Assert.That(drawn.Min(), Is.GreaterThanOrEqualTo(400));
            Assert.That(drawn.Max(), Is.LessThanOrEqualTo(1_200));

            weights.DistractedPercent = 100;
            weights.DistractedTimes = 3;
            Assert.That(Enumerable.Range(0, 100).Select(_ => PvpNotice.DrawReaction(weights, random)).Min(), Is.GreaterThanOrEqualTo(1_200), "always looking elsewhere: three times as long");

            Assert.That(PvpNotice.DrawReaction(new PvpHumanWeights { ReactionMinMilliseconds = 0, ReactionMaxMilliseconds = 0 }, random), Is.EqualTo(0), "off: at once, as it was");
        }

        [Test]
        public void AMistakeIsAScoreALittleOff()
        {
            var random = new Random(3);
            var weights = new PvpHumanWeights { ChoiceNoise = 6 };
            int[] noise = Enumerable.Range(0, 500).Select(_ => PvpNotice.Noise(weights, random)).ToArray();
            Assert.That(noise.Min(), Is.EqualTo(-6));
            Assert.That(noise.Max(), Is.EqualTo(6));
            Assert.That(PvpNotice.Noise(new PvpHumanWeights { ChoiceNoise = 0 }, random), Is.EqualTo(0));
        }

        [Test]
        public void ThreeNamedLevelsFromSlowToSharp()
        {
            PvpHumanWeights casual = PvpHumanWeights.For(PvpSkill.Casual), seasoned = PvpHumanWeights.For(PvpSkill.Seasoned), sharp = PvpHumanWeights.For(PvpSkill.Sharp);
            Assert.That(casual.ReactionMaxMilliseconds, Is.GreaterThan(seasoned.ReactionMaxMilliseconds));
            Assert.That(seasoned.ReactionMaxMilliseconds, Is.GreaterThan(sharp.ReactionMaxMilliseconds));
            Assert.That(casual.ChoiceNoise, Is.GreaterThan(seasoned.ChoiceNoise));
            Assert.That(sharp.ChoiceNoise, Is.EqualTo(0));
            Assert.That(sharp.DistractedPercent, Is.EqualTo(0));
            Assert.That(casual.ImmunityEndNoticedAfterMaxSeconds, Is.GreaterThan(seasoned.ImmunityEndNoticedAfterMaxSeconds));
            // Seasoned is the weights as they are, so a weights file still has its say in a batch.
            var defaults = new PvpHumanWeights();
            Assert.That(seasoned.ReactionMinMilliseconds, Is.EqualTo(defaults.ReactionMinMilliseconds));
            Assert.That(PvpSkills.WeightsFor(PvpSkill.Seasoned), Is.Null);
            Assert.That(PvpSkills.WeightsFor(PvpSkill.Sharp), Is.Not.Null);
        }

        [TestCase("casual", PvpSkill.Casual)]
        [TestCase(" Sharp ", PvpSkill.Sharp)]
        [TestCase("normal", PvpSkill.Seasoned)]
        [TestCase("hard", PvpSkill.Sharp)]
        public void ALevelHasItsWords(string word, PvpSkill expected)
        {
            Assert.That(PvpSkills.Parse(word), Is.EqualTo(expected));
            Assert.That(PvpSkills.Parse("godlike"), Is.Null);
            Assert.That(PvpSkills.Parse(null), Is.Null);
        }
    }
}

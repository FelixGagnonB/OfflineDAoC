using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpKiting
    {
        private static readonly PvpWeights Weights = new();

        private static PvpOption Root(bool pursuer, bool extending = false, bool slowed = false, bool interrupted = false) =>
            new(PvpActionKind.Root, 0, PvpBuffArchetype.Melee, false, false, 100, slowed, false, interrupted, false, 100,
                Pursuer: pursuer, Extending: extending);

        [TestCase(eCharacterClass.Sorcerer)]
        [TestCase(eCharacterClass.Theurgist)]
        [TestCase(eCharacterClass.Healer)]
        public void AFighterBeingHitIsStillWorthRooting(eCharacterClass caster)
        {
            // It is on the group's healers, so somebody is always hitting it: that must not count against the root.
            PvpOption option = Root(pursuer: true, interrupted: true) with { CostPercent = 5, PowerPercent = 60 };
            Assert.That(PvpActionChoice.WorthIt(option, PvpActionChoice.Score(caster, option, Weights), Weights), Is.True);
        }

        [Test]
        public void ARootOnAFighterThatIsOnTheBacklineIsWorthTheTurn()
        {
            PvpOption option = Root(pursuer: true);
            int score = PvpActionChoice.Score(eCharacterClass.Shaman, option, Weights);
            Assert.That(PvpActionChoice.WorthIt(option, score, Weights), Is.True);
        }

        [Test]
        public void ARootOnAFighterThatIsOnNobodyIsNot()
        {
            PvpOption option = Root(pursuer: false);
            int score = PvpActionChoice.Score(eCharacterClass.Shaman, option, Weights);
            Assert.That(PvpActionChoice.WorthIt(option, score, Weights), Is.False);
        }

        [Test]
        public void WhileTheGroupExtendsItIsWorthMore()
        {
            Assert.That(PvpActionChoice.Score(eCharacterClass.Shaman, Root(true, extending: true), Weights),
                Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Shaman, Root(true), Weights)));
        }

        [Test]
        public void APursuerAlreadySlowedIsNotRootedAgain()
        {
            Assert.That(PvpActionChoice.Score(eCharacterClass.Shaman, Root(true, slowed: true), Weights),
                Is.LessThan(PvpActionChoice.Score(eCharacterClass.Shaman, Root(false), Weights)));
        }

        [TestCase(eCharacterClass.Cabalist)]
        [TestCase(eCharacterClass.Eldritch)]
        [TestCase(eCharacterClass.Runemaster)]
        public void ACasterWithARootUsesItOnAPursuer(eCharacterClass caster)
        {
            PvpOption option = Root(pursuer: true);
            Assert.That(PvpActionChoice.WorthIt(option, PvpActionChoice.Score(caster, option, Weights), Weights), Is.True);
        }

        [Test]
        public void AFighterStoppingItsTargetStunsBeforeItSnaresAndSnaresBeforeItHurts()
        {
            Assert.That(PvpStylePolicy.ControlRank(stuns: true, snares: false, targetSlowed: false),
                Is.GreaterThan(PvpStylePolicy.ControlRank(stuns: false, snares: true, targetSlowed: false)));
            Assert.That(PvpStylePolicy.ControlRank(stuns: false, snares: true, targetSlowed: false), Is.GreaterThan(0));
            Assert.That(PvpStylePolicy.ControlRank(stuns: false, snares: false, targetSlowed: false), Is.EqualTo(0));
        }

        [Test]
        public void ATargetAlreadySlowedIsNotSnaredAgain()
        {
            Assert.That(PvpStylePolicy.ControlRank(stuns: false, snares: true, targetSlowed: true), Is.EqualTo(0));
        }
    }
}

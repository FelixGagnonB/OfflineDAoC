using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpShouts
    {
        private static readonly PvpWeights Weights = new();

        private static PvpOption Shout(bool kill = false, bool interrupted = false, bool casting = false, bool instant = true,
            PvpBuffArchetype target = PvpBuffArchetype.Caster) =>
            new(PvpActionKind.Damage, 0, target, kill, false, 100, false, casting, interrupted, false, 100, Instant: instant);

        private static bool Worth(eCharacterClass who, PvpOption option) =>
            PvpActionChoice.WorthIt(option, PvpActionChoice.Score(who, option, Weights), Weights);

        [TestCase(eCharacterClass.Skald)]
        public void AShoutOnAFreeCasterIsAnInterruptWorthTheTurn(eCharacterClass who)
        {
            Assert.That(Worth(who, Shout()), Is.True);
        }

        [Test]
        public void ACasterAlreadyInterruptedIsLeftForTheNextOne()
        {
            Assert.That(Worth(eCharacterClass.Skald, Shout(interrupted: true)), Is.False);
        }

        [Test]
        public void ACasterInTheMiddleOfACastComesFirst()
        {
            Assert.That(PvpActionChoice.Score(eCharacterClass.Skald, Shout(casting: true), Weights),
                Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Skald, Shout(), Weights)));
        }

        [Test]
        public void AShoutIsNotSpentOnAFighterThatIsNotTheKillTarget()
        {
            Assert.That(Worth(eCharacterClass.Skald, Shout(target: PvpBuffArchetype.Melee)), Is.False);
            Assert.That(Worth(eCharacterClass.Skald, Shout(kill: true, target: PvpBuffArchetype.Melee)), Is.True, "the critical kill");
        }

        [Test]
        public void ANukeWithACastTimeStillStaysOnTheKillTarget()
        {
            // Only an instant is an interrupt for free; a cast spent off the kill target kills nobody.
            Assert.That(Worth(eCharacterClass.Enchanter, Shout(instant: false)), Is.False);
            Assert.That(Worth(eCharacterClass.Enchanter, Shout(kill: true, instant: false)), Is.True);
        }

        [Test]
        public void TheBardLooksToControlBeforeItsHealsWhileNobodyIsBadlyHurt()
        {
            Assert.That(Weights.HealsFirstBelowFor("bard", eCharacterClass.Bard), Is.EqualTo(75));
            Assert.That(Weights.HealsFirstBelowFor("druid-regrowth", eCharacterClass.Druid), Is.GreaterThan(100), "a healer heals first always");
        }
    }
}

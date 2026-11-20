using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpCorpse
    {
        [Test]
        public void ALivingBotIsShownAsEver()
        {
            foreach (PvpCorpseShow show in new[] { PvpCorpseShow.Off, PvpCorpseShow.Ghost, PvpCorpseShow.Fall })
            {
                Assert.That(PvpCorpse.DrawsGhost(show, false), Is.False, show.ToString());
                Assert.That(PvpCorpse.BodyFalls(show, false), Is.False, show.ToString());
            }
        }

        [Test]
        public void ADeadBotFallsAndItsGhostStaysToBeTargeted()
        {
            // The owner, 2026-11-20: "When a character dies, its body remains up." And of the first try:
            // "after some seconds the corpse disappear. I could not click on the dead enchanter".
            Assert.That(PvpCorpse.Show, Is.EqualTo(PvpCorpseShow.Fall), "as built");
            Assert.That(PvpCorpse.BodyFalls(PvpCorpseShow.Fall, true), Is.True);
            Assert.That(PvpCorpse.DrawsGhost(PvpCorpseShow.Fall, true), Is.True, "the client keeps something to click");
        }

        [Test]
        public void TheGhostAloneAndNothingAtAllCanBeAskedFor()
        {
            Assert.That(PvpCorpse.DrawsGhost(PvpCorpseShow.Ghost, true), Is.True);
            Assert.That(PvpCorpse.BodyFalls(PvpCorpseShow.Ghost, true), Is.False);
            Assert.That(PvpCorpse.DrawsGhost(PvpCorpseShow.Off, true), Is.False);
            Assert.That(PvpCorpse.BodyFalls(PvpCorpseShow.Off, true), Is.False);
        }

        [Test]
        public void OnlyAnEightVersusEightBotIsABody()
        {
            Assert.That(PvpCorpse.IsDown(null), Is.False);
            Assert.That(PvpCorpse.IsGhost(null), Is.False);
        }
    }
}

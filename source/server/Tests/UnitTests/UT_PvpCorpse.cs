using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpCorpse
    {
        [Test]
        public void ALivingBotIsSentAsEver()
        {
            foreach (PvpCorpseShow show in new[] { PvpCorpseShow.Off, PvpCorpseShow.Died, PvpCorpseShow.Removed })
            {
                Assert.That(PvpCorpse.SendsUpdate(show, false), Is.True, show.ToString());
                Assert.That(PvpCorpse.SendsCreate(show, false), Is.True, show.ToString());
                Assert.That(PvpCorpse.SendsDied(show, false), Is.False, show.ToString());
            }
        }

        [Test]
        public void ABodyLaidDownAndKeptIsMadeAndLaidDownAgainButNotMoved()
        {
            // The owner, 2026-11-20: "When a character dies, its body remains up."
            Assert.That(PvpCorpse.Show, Is.EqualTo(PvpCorpseShow.Died), "as built");
            Assert.That(PvpCorpse.SendsCreate(PvpCorpseShow.Died, true), Is.True, "a client that comes near gets the body");
            Assert.That(PvpCorpse.SendsDied(PvpCorpseShow.Died, true), Is.True, "and gets it lying");
            Assert.That(PvpCorpse.SendsUpdate(PvpCorpseShow.Died, true), Is.False, "a position update would stand it up");
        }

        [Test]
        public void ABodyTheClientForgotIsNotBroughtBack()
        {
            Assert.That(PvpCorpse.SendsCreate(PvpCorpseShow.Removed, true), Is.False);
            Assert.That(PvpCorpse.SendsUpdate(PvpCorpseShow.Removed, true), Is.False, "an update for a body it does not have makes the client ask for it");
            Assert.That(PvpCorpse.SendsDied(PvpCorpseShow.Removed, true), Is.False);
        }

        [Test]
        public void OffIsAsItWas()
        {
            Assert.That(PvpCorpse.SendsCreate(PvpCorpseShow.Off, true), Is.True);
            Assert.That(PvpCorpse.SendsUpdate(PvpCorpseShow.Off, true), Is.True);
            Assert.That(PvpCorpse.SendsDied(PvpCorpseShow.Off, true), Is.False);
        }

        [Test]
        public void OnlyAnEightVersusEightBotIsABody()
        {
            Assert.That(PvpCorpse.IsDown(null), Is.False);
        }
    }
}

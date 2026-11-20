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
            foreach (PvpCorpseShow show in new[] { PvpCorpseShow.Off, PvpCorpseShow.Ghost, PvpCorpseShow.Fall, PvpCorpseShow.Body })
            {
                Assert.That(PvpCorpse.DrawsGhost(show, false), Is.False, show.ToString());
                Assert.That(PvpCorpse.DrawsMarker(show, false), Is.False, show.ToString());
                Assert.That(PvpCorpse.BodyFalls(show, false), Is.False, show.ToString());
            }
        }

        [Test]
        public void ADeadBotIsABodyOnTheGroundAndStaysATarget()
        {
            // The owner, 2026-11-20: "When a character dies, its body remains up." Of the first try: "after some
            // seconds the corpse disappear. I could not click on the dead enchanter". Of the ghost: "doesn't work".
            Assert.That(PvpCorpse.Show, Is.EqualTo(PvpCorpseShow.Body), "as built");
            Assert.That(PvpCorpse.BodyFalls(PvpCorpseShow.Body, true), Is.True);
            Assert.That(PvpCorpse.DrawsMarker(PvpCorpseShow.Body, true), Is.True, "the client keeps the bot, for the group window");
            Assert.That(PvpCorpse.DrawsGhost(PvpCorpseShow.Body, true), Is.False, "and draws no figure standing");
        }

        [Test]
        public void TheGhostOverTheBodyCanStillBeAskedFor()
        {
            Assert.That(PvpCorpse.BodyFalls(PvpCorpseShow.Fall, true), Is.True);
            Assert.That(PvpCorpse.DrawsGhost(PvpCorpseShow.Fall, true), Is.True);
            Assert.That(PvpCorpse.DrawsMarker(PvpCorpseShow.Fall, true), Is.False);
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
            Assert.That(PvpCorpse.IsMarker(null), Is.False);
        }
    }
}

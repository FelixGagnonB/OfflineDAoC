using System;
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
            foreach (PvpCorpseShow show in Enum.GetValues<PvpCorpseShow>())
            {
                Assert.That(PvpCorpse.DrawsPlayer(show, false), Is.False, show.ToString());
                Assert.That(PvpCorpse.DrawsGhost(show, false), Is.False, show.ToString());
                Assert.That(PvpCorpse.DrawsMarker(show, false), Is.False, show.ToString());
                Assert.That(PvpCorpse.BodyFalls(show, false), Is.False, show.ToString());
            }
        }

        [Test]
        public void ADeadBotIsADeadPlayerToTheClient()
        {
            // The owner, 2026-11-20: "When a bot dies, it should lay on the ground, like a human and be clickable.
            // It needs to be targetable to be rez. It cannot stay standup, it's way too confusing and wrong".
            Assert.That(PvpCorpse.Show, Is.EqualTo(PvpCorpseShow.Player), "as built");
            Assert.That(PvpCorpse.DrawsPlayer(PvpCorpseShow.Player, true), Is.True);
            Assert.That(PvpCorpse.BodyFalls(PvpCorpseShow.Player, true), Is.False, "no monster's body beside the player's");
            Assert.That(PvpCorpse.DrawsGhost(PvpCorpseShow.Player, true), Is.False);
            Assert.That(PvpCorpse.DrawsMarker(PvpCorpseShow.Player, true), Is.False);
        }

        [Test]
        public void ThePlayerInABotsPlaceHasAPlayersModelWordAndASessionNoClientHas()
        {
            // A Celt's model 302 with the middle size; a player with size 0 is refused at creation.
            Assert.That(PvpCorpse.PlayerModel(302) & 0x7FF, Is.EqualTo(302));
            Assert.That((PvpCorpse.PlayerModel(302) >> 11) & 3, Is.EqualTo(2));
            Assert.That(PvpCorpse.PlayerModel(0xFFFF) >> 13, Is.EqualTo(0), "nothing above the size");
            foreach (int counter in new[] { 1, 2, 1999, 2000, 2001, int.MaxValue, int.MinValue + 1 })
                Assert.That(PvpCorpse.SessionFor(counter), Is.InRange(1000, 2999), counter.ToString());
            Assert.That(PvpCorpse.SessionFor(1), Is.Not.EqualTo(PvpCorpse.SessionFor(2)));
        }

        [Test]
        public void TheBodyUnderItsNameCanStillBeAskedFor()
        {
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

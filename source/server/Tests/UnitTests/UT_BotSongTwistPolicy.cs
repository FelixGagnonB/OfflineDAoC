using DOL.GS;
using NUnit.Framework;
using Song = DOL.GS.BotSongTwistPolicy.Song;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_BotSongTwistPolicy
    {
        [TestCase(eCharacterClass.Bard, eSpellType.SpeedEnhancement, true)]
        [TestCase(eCharacterClass.Bard, eSpellType.EnduranceRegenBuff, true)]
        [TestCase(eCharacterClass.Bard, eSpellType.PowerRegenBuff, false)]
        [TestCase(eCharacterClass.Bard, eSpellType.HealthRegenBuff, false)]
        [TestCase(eCharacterClass.Skald, eSpellType.SpeedEnhancement, true)]
        [TestCase(eCharacterClass.Skald, eSpellType.HealthRegenBuff, true)]
        [TestCase(eCharacterClass.Skald, eSpellType.DamageAdd, false)]
        [TestCase(eCharacterClass.Minstrel, eSpellType.SpeedEnhancement, true)]
        [TestCase(eCharacterClass.Minstrel, eSpellType.HealthRegenBuff, true)]
        [TestCase(eCharacterClass.Minstrel, eSpellType.PowerRegenBuff, false)]
        [TestCase(eCharacterClass.Paladin, eSpellType.SpeedEnhancement, false)]
        public void CompanionTravelPoolPreservesSpeedAndOnlyUsefulClassSecondary(
            eCharacterClass characterClass, eSpellType type, bool allowed) =>
            Assert.That(BotSongTwistPolicy.IsCompanionTravelSong(characterClass, type), Is.EqualTo(allowed));

        [Test] public void SkaldKeepsSpeedUntilReuseExpiresThenReturnsImmediatelyAfterSecondary()
        {
            Assert.That(BotSongTwistPolicy.Choose(3608, 3608,
                new[] { new Song(3608, 0, 6000, 3000), new Song(3613, 0, 0, 0) }), Is.Zero);
            Assert.That(BotSongTwistPolicy.Choose(3608, 3608,
                new[] { new Song(3608, 0, 0, 2000), new Song(3613, 0, 0, 0) }), Is.EqualTo(3613));
            Assert.That(BotSongTwistPolicy.Choose(3608, 3613,
                new[] { new Song(3608, 0, 0, 1500), new Song(3613, 0, 7800, 4800) }), Is.EqualTo(3608));
        }

        [TestCase(1101, 1111)] // Minstrel
        [TestCase(4101, 4111)] // Bard: IDs immaterial; actual cast/child timings matter.
        public void InstrumentSongsRefreshBeforeExpiryAndRespectBothCastTimes(int speed, int heal)
        {
            Assert.That(BotSongTwistPolicy.Choose(speed, speed,
                new[] { new Song(speed, 3000, 0, 10000), new Song(heal, 3000, 0, 6500) }), Is.EqualTo(heal));
            Assert.That(BotSongTwistPolicy.Choose(speed, heal,
                new[] { new Song(speed, 3000, 0, 6500), new Song(heal, 3000, 0, 10000) }), Is.EqualTo(speed));
            Assert.That(BotSongTwistPolicy.Choose(speed, speed,
                new[] { new Song(speed, 3000, 0, 6000), new Song(heal, 3000, 0, 0) }), Is.Zero,
                "wait for the primary's next pulse; never drop it on a round trip that cannot fit");
        }

        [Test] public void SingleSongAndUnavailableSecondariesDoNotTogglePrimaryOff()
        {
            Assert.That(BotSongTwistPolicy.Choose(1, 1, new[] { new Song(1, 0, 0, 5000) }), Is.Zero);
            Assert.That(BotSongTwistPolicy.Choose(1, 1,
                new[] { new Song(1, 0, 0, 5000), new Song(2, 0, 2000, 0) }), Is.Zero);
            Assert.That(BotSongTwistPolicy.Choose(1, 0, new[] { new Song(1, 0, 0, 0) }), Is.EqualTo(1));
        }

        [Test] public void OnTheRoadAnEightVEightBardTwistsSpeedAndEndurance()
        {
            const int speed = 1, endurance = 2;
            // Speed has just pulsed (six seconds left) and the endurance has run out: endurance now.
            Assert.That(BotSongTwistPolicy.ChooseRoadTwist(speed, speed,
                new[] { new Song(speed, 3000, 0, 5800), new Song(endurance, 3000, 0, 0) }), Is.EqualTo(endurance));
            // The usual rule would never fit it in: the round trip is longer than the speed lasts.
            Assert.That(BotSongTwistPolicy.Choose(speed, speed,
                new[] { new Song(speed, 3000, 0, 5800), new Song(endurance, 3000, 0, 0) }), Is.Zero);
            // Endurance playing: straight back to the speed.
            Assert.That(BotSongTwistPolicy.ChooseRoadTwist(speed, endurance,
                new[] { new Song(speed, 3000, 0, 2800), new Song(endurance, 3000, 0, 5000) }), Is.EqualTo(speed));
            // Endurance still has time: the speed is left to play.
            Assert.That(BotSongTwistPolicy.ChooseRoadTwist(speed, speed,
                new[] { new Song(speed, 3000, 0, 5800), new Song(endurance, 3000, 0, 2500) }), Is.Zero);
            // The speed is itself about to lapse: not now, wait for its pulse.
            Assert.That(BotSongTwistPolicy.ChooseRoadTwist(speed, speed,
                new[] { new Song(speed, 3000, 0, 1500), new Song(endurance, 3000, 0, 0) }), Is.Zero);
            // Nothing playing: the speed first.
            Assert.That(BotSongTwistPolicy.ChooseRoadTwist(speed, 0,
                new[] { new Song(speed, 3000, 0, 0), new Song(endurance, 3000, 0, 0) }), Is.EqualTo(speed));
        }
    }
}

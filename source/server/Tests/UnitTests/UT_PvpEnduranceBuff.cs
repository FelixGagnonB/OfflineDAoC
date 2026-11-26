using System.Linq;
using DOL.Database;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpEnduranceBuff
    {
        private static Spell Buff(int level, double value, int concentration = 1, string type = "EnduranceRegenBuff") =>
            new(new DbSpell { SpellID = 99000 + level, Name = "Test invigoration", Type = type, Target = "Realm", Range = 1000,
                Value = value, Concentration = concentration }, level);

        [Test]
        public void MidgardsEnduranceIsItsShamans()
        {
            // "enemies without endurance ... should not out run ... With midgard, it might be a shaman, which would explain
            // they can keep sprinting": it was the buffbot's, with no range and no death.
            const int reach = 1000;
            // The Shaman is down: what it gave is taken back, from the living and the dead.
            Assert.That(PvpEnduranceBuff.For(shamanIsUp: false, shamanCanCast: false, memberIsUp: true, memberHasItFromThisOne: true,
                memberHasItFromOneThatIsUp: false, apart: 0, reach), Is.EqualTo(PvpEnduranceBuff.Does.Takes));
            Assert.That(PvpEnduranceBuff.For(false, false, true, false, false, 0, reach), Is.EqualTo(PvpEnduranceBuff.Does.Nothing));
            // Up again: it gives it to a living member in range that has none.
            Assert.That(PvpEnduranceBuff.For(true, true, true, false, false, 600, reach), Is.EqualTo(PvpEnduranceBuff.Does.Gives));
            Assert.That(PvpEnduranceBuff.For(true, true, true, true, true, 600, reach), Is.EqualTo(PvpEnduranceBuff.Does.Nothing), "it has it");
            Assert.That(PvpEnduranceBuff.For(true, true, true, false, true, 600, reach), Is.EqualTo(PvpEnduranceBuff.Does.Nothing), "from the other Shaman");
            Assert.That(PvpEnduranceBuff.For(true, true, true, false, false, 1001, reach), Is.EqualTo(PvpEnduranceBuff.Does.Nothing), "too far to cast on");
            Assert.That(PvpEnduranceBuff.For(true, true, memberIsUp: false, false, false, 100, reach), Is.EqualTo(PvpEnduranceBuff.Does.Nothing));
            Assert.That(PvpEnduranceBuff.For(true, shamanCanCast: false, true, false, false, 100, reach), Is.EqualTo(PvpEnduranceBuff.Does.Nothing), "asleep, or casting");
        }

        [Test]
        public void AShamanHasTheBuffOfItsAugmentation()
        {
            Spell[] line = [Buff(2, 1), Buff(12, 2), Buff(22, 3), Buff(32, 4), Buff(42, 5), Buff(40, 9, concentration: 0), Buff(30, 8, type: "StrengthBuff")];
            Assert.That(PvpEnduranceBuff.Best(46, line).Value, Is.EqualTo(5), "the group's Shaman has Augmentation 46");
            Assert.That(PvpEnduranceBuff.Best(41, line).Value, Is.EqualTo(4));
            Assert.That(PvpEnduranceBuff.Best(1, line), Is.Null, "no Augmentation, no buff");
            Assert.That(PvpEnduranceBuff.Best(50, null), Is.Null);
            // Five a second is what a second of sprint takes: with it a bot sprints on, without it twenty seconds.
            Assert.That(new PvpPositionWeights().SprintCostPerSecond, Is.EqualTo(5));
        }

        [Test]
        public void TheBuffbotIsNoGroupsShaman()
        {
            Assert.That(PvpEnduranceBuff.IsItsShaman(null), Is.False);
            Assert.That(PvpEnduranceBuff.Keep(null, 0), Is.Zero);
            Assert.That(PvpEnduranceBuff.Keep([], 0), Is.Zero);
            // The catalog's group has one Shaman, and it has the Augmentation for the best buff.
            PvpBuild shaman = PvpBuildCatalog.Find("shaman-aug");
            Assert.That(shaman, Is.Not.Null);
            Assert.That(shaman.Class, Is.EqualTo(eCharacterClass.Shaman));
        }
    }
}

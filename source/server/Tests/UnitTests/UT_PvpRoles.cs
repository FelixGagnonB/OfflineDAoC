using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpRoles
    {
        [TestCase(eCharacterClass.Sorcerer, PvpRole.Control)]
        [TestCase(eCharacterClass.Bard, PvpRole.Control)]
        [TestCase(eCharacterClass.Theurgist, PvpRole.Interrupt)]
        [TestCase(eCharacterClass.Cleric, PvpRole.Heal)]
        [TestCase(eCharacterClass.Shaman, PvpRole.Heal)]
        [TestCase(eCharacterClass.Armsman, PvpRole.Assist)]
        [TestCase(eCharacterClass.Cabalist, PvpRole.Assist)]
        [TestCase(eCharacterClass.Minstrel, PvpRole.Assist)]
        public void RoleByClass(eCharacterClass characterClass, PvpRole expected)
        {
            Assert.That(PvpRoles.Of(characterClass), Is.EqualTo(expected));
        }

        [TestCase(PvpRole.Assist, 100, true)]
        [TestCase(PvpRole.Control, 100, false)]
        [TestCase(PvpRole.Interrupt, 36, false)]
        [TestCase(PvpRole.Interrupt, 35, true)]
        [TestCase(PvpRole.Control, 10, true)]
        [TestCase(PvpRole.Heal, 5, false)]
        public void ControllersAndInterruptersAssistOnlyWhenItIsCritical(PvpRole role, int health, bool expected)
        {
            Assert.That(PvpRoles.JoinsKill(role, health), Is.EqualTo(expected));
        }

        [Test]
        public void TheInterrupterWorksOnACastingHealerThatIsNotTheKillTarget()
        {
            List<PvpInterruptView> enemies =
            [
                new(0, PvpBuffArchetype.Healer, Casting: true, Mezzed: false, Distance: 900),   // the kill target
                new(1, PvpBuffArchetype.Melee, Casting: false, Mezzed: false, Distance: 300),
                new(2, PvpBuffArchetype.Caster, Casting: true, Mezzed: false, Distance: 1200),
                new(3, PvpBuffArchetype.Healer, Casting: false, Mezzed: false, Distance: 800),
                new(4, PvpBuffArchetype.Healer, Casting: true, Mezzed: true, Distance: 500),
            ];
            Assert.That(PvpRoles.ChooseInterruptTarget(enemies, 0), Is.EqualTo(2), "casting now beats idle");
            Assert.That(PvpRoles.ChooseInterruptTarget([enemies[0], enemies[1], enemies[3], enemies[4]], 0), Is.EqualTo(3));
            Assert.That(PvpRoles.ChooseInterruptTarget([enemies[0], enemies[1], enemies[4]], 0), Is.EqualTo(-1), "a mezzed healer is left asleep");
        }
    }
}

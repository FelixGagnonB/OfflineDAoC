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
        [TestCase(eCharacterClass.Paladin, PvpRole.Peel)]
        [TestCase(eCharacterClass.Warrior, PvpRole.Peel)]
        [TestCase(eCharacterClass.Hero, PvpRole.Peel)]
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
        [TestCase(PvpRole.Peel, 5, false)]
        public void ControllersAndInterruptersAssistOnlyWhenItIsCritical(PvpRole role, int health, bool expected)
        {
            Assert.That(PvpRoles.JoinsKill(role, health), Is.EqualTo(expected));
        }
    }
}

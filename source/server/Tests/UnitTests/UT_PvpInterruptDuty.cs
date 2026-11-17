using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpInterruptDuty
    {
        private static readonly PvpInterruptWeights Weights = new();

        private static PvpDutyPair Pair(int interrupter, int enemy, PvpBuffArchetype archetype = PvpBuffArchetype.Caster,
            bool casting = false, bool free = true, bool current = false, int distance = 1_000) =>
            new(interrupter, enemy, archetype, casting, free, current, distance);

        [TestCase(eCharacterClass.Theurgist, PvpInterruptMeans.Pets)]
        [TestCase(eCharacterClass.Skald, PvpInterruptMeans.Melee)]
        [TestCase(eCharacterClass.Minstrel, PvpInterruptMeans.Melee)]
        [TestCase(eCharacterClass.Shaman, PvpInterruptMeans.Spells)]
        [TestCase(eCharacterClass.Runemaster, PvpInterruptMeans.Spells)]
        [TestCase(eCharacterClass.Sorcerer, PvpInterruptMeans.None)]
        [TestCase(eCharacterClass.Cleric, PvpInterruptMeans.None)]
        [TestCase(eCharacterClass.Berserker, PvpInterruptMeans.None)]
        public void MeansByClass(eCharacterClass characterClass, PvpInterruptMeans expected)
        {
            Assert.That(PvpInterruptDuty.MeansOf(characterClass, Weights), Is.EqualTo(expected));
        }

        [Test]
        public void EachEnemyGetsItsOwnInterrupter()
        {
            // Both interrupters would rather have the healer; the second takes the caster.
            List<PvpDutyPair> pairs =
            [
                Pair(0, 0, PvpBuffArchetype.Healer, distance: 500), Pair(0, 1, distance: 500),
                Pair(1, 0, PvpBuffArchetype.Healer, distance: 900), Pair(1, 1, distance: 900),
            ];
            Dictionary<int, int> duty = PvpInterruptDuty.Assign(pairs, Weights);
            Assert.That(duty[0], Is.EqualTo(0));
            Assert.That(duty[1], Is.EqualTo(1));
        }

        [Test]
        public void AnEnemyCastingNowComesFirst()
        {
            List<PvpDutyPair> pairs = [Pair(0, 0, PvpBuffArchetype.Healer), Pair(0, 1, casting: true)];
            Assert.That(PvpInterruptDuty.Assign(pairs, Weights)[0], Is.EqualTo(1));
        }

        [Test]
        public void AnInterrupterKeepsTheEnemyItHas()
        {
            List<PvpDutyPair> pairs = [Pair(0, 0, PvpBuffArchetype.Healer), Pair(0, 1, current: true)];
            Assert.That(PvpInterruptDuty.Assign(pairs, Weights)[0], Is.EqualTo(1));
        }

        [Test]
        public void AnEnemyOutOfReachIsNotGiven()
        {
            Assert.That(PvpInterruptDuty.Assign([Pair(0, 0, distance: 2_600)], Weights), Is.Empty);
            Assert.That(PvpInterruptDuty.Assign([], Weights), Is.Empty);
        }

        [Test]
        public void TheWeightsFileCanTurnDutyOffOrChangeWhoHasIt()
        {
            var off = new PvpInterruptWeights { MinScore = 10_000 };
            Assert.That(PvpInterruptDuty.Assign([Pair(0, 0, PvpBuffArchetype.Healer, casting: true)], off), Is.Empty);
            var noSkald = new PvpInterruptWeights();
            noSkald.Means["Skald"] = "None";
            Assert.That(PvpInterruptDuty.MeansOf(eCharacterClass.Skald, noSkald), Is.EqualTo(PvpInterruptMeans.None));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpActionChoice
    {
        private static readonly PvpWeights Weights = new();

        private static PvpOption Option(PvpActionKind kind, int id, PvpBuffArchetype archetype = PvpBuffArchetype.Caster, bool kill = false,
            bool assigned = false, int health = 100, bool hasEffect = false, bool casting = false, bool interrupted = false,
            bool mezzed = false, int power = 100, int cost = 0, int walk = 0, int caught = 1, bool catchesKill = false) =>
            new(kind, id, archetype, kill, assigned, health, hasEffect, casting, interrupted, mezzed, power, cost, walk, caught, catchesKill);

        private static (PvpOption Option, int Score) Best(eCharacterClass characterClass, params PvpOption[] options) =>
            PvpActionChoice.Rank(characterClass, options, Weights).First();

        [Test]
        public void AnEldritchNearsightsAFreeCasterThenDiseasesThenNukesTheKillTarget()
        {
            PvpOption[] incoming =
            [
                Option(PvpActionKind.Nearsight, 1),
                Option(PvpActionKind.Disease, 0, PvpBuffArchetype.Healer, kill: true),
                Option(PvpActionKind.Damage, 0, PvpBuffArchetype.Healer, kill: true),
            ];
            Assert.That(Best(eCharacterClass.Eldritch, incoming).Option.Kind, Is.EqualTo(PvpActionKind.Nearsight));

            incoming[0] = incoming[0] with { HasEffect = true };
            Assert.That(Best(eCharacterClass.Eldritch, incoming).Option.Kind, Is.EqualTo(PvpActionKind.Disease));

            incoming[1] = incoming[1] with { HasEffect = true };
            Assert.That(Best(eCharacterClass.Eldritch, incoming).Option.Kind, Is.EqualTo(PvpActionKind.Damage));
        }

        [Test]
        public void TheseAreWeightsNotRules_AKillTargetNearDeathOutweighsANearsight()
        {
            PvpOption[] options =
            [
                Option(PvpActionKind.Nearsight, 1, interrupted: true),
                Option(PvpActionKind.Damage, 0, PvpBuffArchetype.Healer, kill: true, health: 10, hasEffect: true),
            ];
            Assert.That(Best(eCharacterClass.Eldritch, options).Option.Kind, Is.EqualTo(PvpActionKind.Damage));
        }

        [Test]
        public void AShamanDiseasesBeforeItRoots_AndRootsAnImmuneCasterToInterruptIt()
        {
            Assert.That(Best(eCharacterClass.Shaman, Option(PvpActionKind.Root, 1, casting: true), Option(PvpActionKind.Disease, 2, PvpBuffArchetype.Melee)).Option.Kind,
                Is.EqualTo(PvpActionKind.Disease));
            // Everyone is diseased; a healer that already carries the root is casting.
            (PvpOption option, int score) = Best(eCharacterClass.Shaman, Option(PvpActionKind.Root, 1, PvpBuffArchetype.Healer, hasEffect: true, casting: true));
            Assert.That(option.Kind, Is.EqualTo(PvpActionKind.Root));
            Assert.That(score, Is.GreaterThanOrEqualTo(Weights.MinScore));
            // Rooting a fighter is not worth the turn.
            Assert.That(Best(eCharacterClass.Shaman, Option(PvpActionKind.Root, 3, PvpBuffArchetype.Melee)).Score, Is.LessThan(Weights.MinScore));
        }

        [Test]
        public void ASorcererJoinsTheKillOnlyWhenItIsClose()
        {
            int healthy = Best(eCharacterClass.Sorcerer, Option(PvpActionKind.Damage, 0, PvpBuffArchetype.Melee, kill: true)).Score;
            int dying = Best(eCharacterClass.Sorcerer, Option(PvpActionKind.Damage, 0, PvpBuffArchetype.Melee, kill: true, health: 20)).Score;
            Assert.That(healthy, Is.LessThan(Weights.MinScore));
            Assert.That(dying, Is.GreaterThanOrEqualTo(Weights.MinScore));
        }

        [Test]
        public void DamageGoesToTheKillTarget_AndAMezzedEnemyIsLeftAsleepUnlessItIsTheKillTarget()
        {
            Assert.That(Best(eCharacterClass.Enchanter, Option(PvpActionKind.Damage, 1)).Score, Is.LessThan(Weights.MinScore));
            Assert.That(Best(eCharacterClass.Enchanter, Option(PvpActionKind.Damage, 0, kill: true, mezzed: true)).Score, Is.GreaterThanOrEqualTo(Weights.MinScore));
            Assert.That(Best(eCharacterClass.Shaman, Option(PvpActionKind.Disease, 1, mezzed: true)).Score, Is.LessThan(Weights.MinScore));
        }

        [Test]
        public void ATheurgistSendsPetsAtItsAssignedCasterNotAtAFighter()
        {
            (PvpOption option, int score) = Best(eCharacterClass.Theurgist,
                Option(PvpActionKind.Pet, 1, PvpBuffArchetype.Melee), Option(PvpActionKind.Pet, 2, PvpBuffArchetype.Healer, assigned: true));
            Assert.That(option.TargetId, Is.EqualTo(2));
            Assert.That(score, Is.GreaterThanOrEqualTo(Weights.MinScore));
            Assert.That(Best(eCharacterClass.Theurgist, Option(PvpActionKind.Pet, 1, PvpBuffArchetype.Melee)).Score, Is.LessThan(Weights.MinScore));
        }

        [Test]
        public void AWalkCosts_SoANearOptionBeatsAnEqualFarOne_ButAGoodFarOneIsStillWorthWalkingTo()
        {
            PvpOption near = Option(PvpActionKind.Damage, 0, kill: true);
            Assert.That(Best(eCharacterClass.Enchanter, Option(PvpActionKind.Damage, 0, kill: true, walk: 600), near).Option.Short, Is.EqualTo(0));
            Assert.That(Best(eCharacterClass.Enchanter, Option(PvpActionKind.Damage, 0, kill: true, walk: 1500)).Score, Is.GreaterThanOrEqualTo(Weights.MinScore));
        }

        [Test]
        public void AMainMezzerOpensWithTheAreaMezThatCatchesTheMost()
        {
            (PvpOption option, int score) = Best(eCharacterClass.Bard,
                Option(PvpActionKind.Mez, 1, PvpBuffArchetype.Healer),
                Option(PvpActionKind.MezArea, 2, PvpBuffArchetype.Melee, caught: 2),
                Option(PvpActionKind.MezArea, 3, PvpBuffArchetype.Melee, caught: 5),
                Option(PvpActionKind.Damage, 0, kill: true));
            Assert.That(option.TargetId, Is.EqualTo(3));
            Assert.That(score, Is.GreaterThanOrEqualTo(Weights.MinScore));
        }

        [Test]
        public void MezLeavesTheKillTargetAndTheImmuneAlone()
        {
            Assert.That(PvpActionChoice.Rank(eCharacterClass.Sorcerer,
            [
                Option(PvpActionKind.Mez, 0, kill: true),
                Option(PvpActionKind.Mez, 1, hasEffect: true),      // asleep, immune or claimed by another mezzer
                Option(PvpActionKind.MezArea, 2, caught: 0),
            ], Weights), Is.Empty);
            // An area mez over the kill target is worth less than the same mez clear of it.
            Assert.That(Best(eCharacterClass.Sorcerer, Option(PvpActionKind.MezArea, 1, caught: 3, catchesKill: true)).Score,
                Is.LessThan(Best(eCharacterClass.Sorcerer, Option(PvpActionKind.MezArea, 1, caught: 3)).Score));
        }

        [Test]
        public void AHealerMezzesBeforeItStuns_AndStunsTheKillTargetBeforeAnother()
        {
            Assert.That(Best(eCharacterClass.Healer, Option(PvpActionKind.Stun, 0, kill: true), Option(PvpActionKind.Mez, 1)).Option.Kind,
                Is.EqualTo(PvpActionKind.Mez));
            Assert.That(Best(eCharacterClass.Healer, Option(PvpActionKind.Stun, 1, PvpBuffArchetype.Melee), Option(PvpActionKind.Stun, 0, PvpBuffArchetype.Melee, kill: true)).Option.TargetId,
                Is.EqualTo(0));
        }

        [Test]
        public void AMezzedHealerIsWokenBeforeAFighter_AndCureMezComesBeforeANewMez()
        {
            (PvpOption option, _) = Best(eCharacterClass.Healer,
                Option(PvpActionKind.CureMez, 8, PvpBuffArchetype.Melee, mezzed: true),
                Option(PvpActionKind.CureMez, 9, PvpBuffArchetype.Healer, mezzed: true),
                Option(PvpActionKind.Mez, 1, PvpBuffArchetype.Melee));
            Assert.That(option.Kind, Is.EqualTo(PvpActionKind.CureMez));
            Assert.That(option.TargetId, Is.EqualTo(9));
            // A friend that is awake is not an option.
            Assert.That(PvpActionChoice.Rank(eCharacterClass.Healer, [Option(PvpActionKind.CureMez, 9, PvpBuffArchetype.Healer)], Weights), Is.Empty);
        }

        [Test]
        public void PowerGetsDearerAsItRunsOut()
        {
            int full = Best(eCharacterClass.Shaman, Option(PvpActionKind.Disease, 1, PvpBuffArchetype.Melee, cost: 8)).Score;
            int low = Best(eCharacterClass.Shaman, Option(PvpActionKind.Disease, 1, PvpBuffArchetype.Melee, power: 20, cost: 8)).Score;
            Assert.That(full, Is.EqualTo(90));
            Assert.That(low, Is.EqualTo(90 - 19));
        }

        [Test]
        public void UselessOptionsAreNotOffered_AndFightersKeepTheOldLogic()
        {
            List<(PvpOption Option, int Score)> ranked = PvpActionChoice.Rank(eCharacterClass.Eldritch,
            [
                Option(PvpActionKind.Nearsight, 1, PvpBuffArchetype.Melee),
                Option(PvpActionKind.Nearsight, 2, hasEffect: true),
                Option(PvpActionKind.Disease, 3, hasEffect: true),
            ], Weights);
            Assert.That(ranked, Is.Empty);
            Assert.That(PvpActionChoice.Covers(eCharacterClass.Armsman, Weights), Is.False);
            Assert.That(PvpActionChoice.KindsFor(eCharacterClass.Shaman, Weights), Is.EquivalentTo(new[] { PvpActionKind.Disease, PvpActionKind.Root }));
        }
    }
}

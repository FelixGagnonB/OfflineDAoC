using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpActionChoice
    {
        [Test]
        public void AWalkThatEndsAmongEnemyFightersLosesToACastFromWhereItStands()
        {
            var weights = new PvpWeights();
            // An Eldritch: nearsight on a caster in range, or on another it would have to walk 200 units for.
            var here = new PvpOption(PvpActionKind.Nearsight, 0, PvpBuffArchetype.Caster, false, false, 100, false, false, true, false, 100);
            var there = here with { TargetId = 1, Interrupted = false, Casting = true, Short = 200 };
            // The far one is free and casting, which is worth more: with open ground between, it walks.
            Assert.That(PvpActionChoice.Score(eCharacterClass.Eldritch, there, weights), Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Eldritch, here, weights)));
            // With two free enemy fighters where it would have to stand, it casts from where it is.
            Assert.That(PvpActionChoice.Score(eCharacterClass.Eldritch, there with { WalkDanger = 2 }, weights),
                Is.LessThan(PvpActionChoice.Score(eCharacterClass.Eldritch, here, weights)));
            // Danger at a place it does not have to walk to costs nothing here: standing off is the squad's business.
            Assert.That(PvpActionChoice.Score(eCharacterClass.Eldritch, here with { WalkDanger = 3 }, weights),
                Is.EqualTo(PvpActionChoice.Score(eCharacterClass.Eldritch, here, weights)));
        }

        [Test]
        public void TheCabalistDebuffsTheKillTargetBeforeItNukesIt_AndOnlyTheKillTarget()
        {
            var weights = new PvpWeights();
            var fresh = new PvpOption(PvpActionKind.Debuff, 0, PvpBuffArchetype.Melee, true, false, 100, false, false, false, false, 100);
            Assert.That(PvpActionChoice.Score(eCharacterClass.Cabalist, fresh, weights),
                Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Cabalist, fresh with { Kind = PvpActionKind.Damage }, weights)));
            var hurt = fresh with { HealthPercent = 50 };
            Assert.That(PvpActionChoice.Score(eCharacterClass.Cabalist, hurt, weights),
                Is.LessThan(PvpActionChoice.Score(eCharacterClass.Cabalist, hurt with { Kind = PvpActionKind.Damage }, weights)), "a hurt target is finished, not debuffed");
            Assert.That(PvpActionChoice.Useful(fresh with { IsKill = false }), Is.False);
            Assert.That(PvpActionChoice.Useful(fresh with { HasEffect = true }), Is.False);
            Assert.That(PvpActionChoice.Useful(fresh), Is.True);
        }

        [Test]
        public void DiseaseIsWorthMoreOnAFighter_AndMostOnOneThatIsOnTheBackline()
        {
            var weights = new PvpWeights();
            var caster = new PvpOption(PvpActionKind.Disease, 0, PvpBuffArchetype.Caster, false, false, 100, false, false, true, false, 100);
            var fighter = caster with { Archetype = PvpBuffArchetype.Melee };
            int onCaster = PvpActionChoice.Score(eCharacterClass.Cabalist, caster, weights);
            int onFighter = PvpActionChoice.Score(eCharacterClass.Cabalist, fighter, weights);
            int onPursuer = PvpActionChoice.Score(eCharacterClass.Cabalist, fighter with { Pursuer = true }, weights);
            Assert.That(onFighter, Is.GreaterThan(onCaster));
            Assert.That(onPursuer, Is.GreaterThan(onFighter));
        }

        [Test]
        public void AWalkThatEndsAmongTheEnemyCostsScore()
        {
            var weights = new PvpWeights();
            // The same nearsight after the same walk: one ends at the edge of range, the other 500 units inside the enemy's reach.
            var atEdge = new PvpOption(PvpActionKind.Nearsight, 0, PvpBuffArchetype.Caster, false, false, 100, false, false, false, false, 100, Short: 300);
            var among = atEdge with { TargetId = 1, WalkInsideEdge = 500 };
            Assert.That(PvpActionChoice.Score(eCharacterClass.Eldritch, atEdge, weights) - PvpActionChoice.Score(eCharacterClass.Eldritch, among, weights),
                Is.EqualTo((int)(weights.WalkInsideEdgePerHundred * 5)));
            // Standing where it is, how near the enemy is costs nothing here: keeping range is the squad's business.
            Assert.That(PvpActionChoice.Score(eCharacterClass.Eldritch, among with { Short = 0 }, weights),
                Is.EqualTo(PvpActionChoice.Score(eCharacterClass.Eldritch, atEdge with { Short = 0 }, weights)));
        }

        [Test]
        public void ControlGoesToTheEnemiesOnTheTrainedMember()
        {
            var weights = new PvpWeights();
            // Two enemy fighters a Healer could stun; one of them is on the group's trained member.
            var other = new PvpOption(PvpActionKind.Stun, 0, PvpBuffArchetype.Melee, false, false, 100, false, false, false, false, 100);
            var trainer = other with { TargetId = 1, OnTrained = true };
            Assert.That(PvpActionChoice.Rank(eCharacterClass.Healer, [other, trainer], weights)[0].Option.TargetId, Is.EqualTo(1));
            // The same for a mez, and for a nearsight on a caster that is nuking the trained member.
            Assert.That(PvpActionChoice.Score(eCharacterClass.Bard, trainer with { Kind = PvpActionKind.Mez }, weights),
                Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Bard, other with { Kind = PvpActionKind.Mez }, weights)));
            var caster = other with { Kind = PvpActionKind.Nearsight, Archetype = PvpBuffArchetype.Caster };
            Assert.That(PvpActionChoice.Score(eCharacterClass.Eldritch, caster with { OnTrained = true }, weights),
                Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Eldritch, caster, weights)));
            // Damage is not control: it stays on the kill target.
            Assert.That(PvpActionChoice.Score(eCharacterClass.Eldritch, caster with { Kind = PvpActionKind.Damage, OnTrained = true }, weights),
                Is.EqualTo(PvpActionChoice.Score(eCharacterClass.Eldritch, caster with { Kind = PvpActionKind.Damage }, weights)));
            // Switched off by its weight, for a comparison.
            var off = new PvpWeights { ControlOnTrainer = 0 };
            Assert.That(PvpActionChoice.Score(eCharacterClass.Healer, trainer, off), Is.EqualTo(PvpActionChoice.Score(eCharacterClass.Healer, other, off)));
        }

        private static readonly PvpWeights Weights = new();

        [Test]
        public void WithTheKillTargetOutOfReachACasterWorksOnWhatItReaches()
        {
            // The owner, 2026-11-19: the Enchanter "was standing most of the fight, not doing anything".
            PvpOption fighter = Option(PvpActionKind.Damage, 0, PvpBuffArchetype.Melee);
            PvpOption healer = Option(PvpActionKind.Damage, 1, PvpBuffArchetype.Healer);
            Assert.Multiple(() =>
            {
                // The kill target in reach: damage on anybody else is not worth the turn.
                Assert.That(PvpActionChoice.WorthIt(fighter, PvpActionChoice.Score(eCharacterClass.Enchanter, fighter, Weights), Weights), Is.False);
                Assert.That(PvpActionChoice.WorthIt(healer, PvpActionChoice.Score(eCharacterClass.Enchanter, healer, Weights), Weights), Is.False);
                // Out of reach: it is, and a healer or caster that is free to cast comes before a fighter.
                PvpOption fighterNow = fighter with { KillOutOfReach = true }, healerNow = healer with { KillOutOfReach = true };
                Assert.That(PvpActionChoice.WorthIt(fighterNow, PvpActionChoice.Score(eCharacterClass.Enchanter, fighterNow, Weights), Weights), Is.True);
                Assert.That(PvpActionChoice.Score(eCharacterClass.Enchanter, healerNow, Weights),
                    Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Enchanter, fighterNow, Weights)));
                // Never on a rooted enemy: the damage would set it free. Nor on a sleeping one.
                PvpOption rooted = fighterNow with { Rooted = true }, asleep = fighterNow with { Mezzed = true };
                Assert.That(PvpActionChoice.WorthIt(rooted, PvpActionChoice.Score(eCharacterClass.Enchanter, rooted, Weights), Weights), Is.False);
                Assert.That(PvpActionChoice.WorthIt(asleep, PvpActionChoice.Score(eCharacterClass.Enchanter, asleep, Weights), Weights), Is.False);
                // The kill target still comes first when it can be reached by walking a little.
                PvpOption kill = Option(PvpActionKind.Damage, 2, PvpBuffArchetype.Healer, kill: true) with { KillOutOfReach = true };
                Assert.That(PvpActionChoice.Score(eCharacterClass.Enchanter, kill, Weights),
                    Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Enchanter, healerNow, Weights)));
            });
        }

        [Test]
        public void TheRunemasterNukesTheAreaWhenTwoPetsAreInIt()
        {
            // The owner, 2026-11-19: "Runemaster should have an AoE, similar to the Eldritch."
            PvpOption one = Option(PvpActionKind.AreaDamage, 0, PvpBuffArchetype.Melee) with { PetOnBackline = true, Caught = 1 };
            PvpOption two = one with { Caught = 2 }, four = one with { Caught = 4 };
            Assert.Multiple(() =>
            {
                Assert.That(PvpActionChoice.Useful(one), Is.False, "one pet is for a single spell");
                Assert.That(PvpActionChoice.Useful(two), Is.True);
                Assert.That(PvpActionChoice.WorthIt(two, PvpActionChoice.Score(eCharacterClass.Runemaster, two, Weights), Weights), Is.True);
                Assert.That(PvpActionChoice.Score(eCharacterClass.Runemaster, four, Weights), Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Runemaster, two, Weights)));
                Assert.That(PvpActionChoice.KindsFor(eCharacterClass.Runemaster, Weights), Does.Contain(PvpActionKind.AreaDamage));
                Assert.That(PvpActionChoice.KindsFor(eCharacterClass.Sorcerer, Weights), Does.Not.Contain(PvpActionKind.AreaDamage));
            });
        }

        [Test]
        public void APetOnAHealerOrCasterIsWorthANuke()
        {
            // The owner, 2026-11-19: a Druid's pet on two Albion casters, and nobody did anything about it.
            PvpOption pet = Option(PvpActionKind.Damage, 0, PvpBuffArchetype.Melee) with { PetOnBackline = true };
            PvpOption freshKill = Option(PvpActionKind.Damage, 1, PvpBuffArchetype.Melee, kill: true);
            PvpOption hurtKill = Option(PvpActionKind.Damage, 1, PvpBuffArchetype.Melee, kill: true, health: 30);
            Assert.Multiple(() =>
            {
                foreach (eCharacterClass caster in new[] { eCharacterClass.Cabalist, eCharacterClass.Theurgist, eCharacterClass.Enchanter, eCharacterClass.Eldritch, eCharacterClass.Runemaster })
                    Assert.That(PvpActionChoice.WorthIt(pet, PvpActionChoice.Score(caster, pet, Weights), Weights), Is.True, caster.ToString());
                // The Sorcerer keeps to its crowd control.
                Assert.That(PvpActionChoice.WorthIt(pet, PvpActionChoice.Score(eCharacterClass.Sorcerer, pet, Weights), Weights), Is.False);
                // Before a kill target at full health, after one that is nearly dead.
                Assert.That(PvpActionChoice.Score(eCharacterClass.Enchanter, pet, Weights), Is.GreaterThan(PvpActionChoice.Score(eCharacterClass.Enchanter, freshKill, Weights)));
                Assert.That(PvpActionChoice.Score(eCharacterClass.Enchanter, pet, Weights), Is.LessThan(PvpActionChoice.Score(eCharacterClass.Enchanter, hurtKill, Weights)));
                // A sleeping pet is left asleep.
                PvpOption asleep = pet with { Mezzed = true };
                Assert.That(PvpActionChoice.WorthIt(asleep, PvpActionChoice.Score(eCharacterClass.Enchanter, asleep, Weights), Weights), Is.False);
            });
        }

        [Test]
        public void AMezOnATargetThatHasBecomeImmuneIsDropped()
        {
            // 19 of 78 opening mezzes were finished on a target that had become immune during the cast.
            Assert.Multiple(() =>
            {
                Assert.That(PvpWeights.ControlCastIsWasted(true, false, false, true, true, false, false), Is.True, "mezzed by somebody else, or immune");
                Assert.That(PvpWeights.ControlCastIsWasted(true, false, false, true, false, true, true), Is.False, "a mez on a stunned or rooted target still lands");
                Assert.That(PvpWeights.ControlCastIsWasted(false, false, true, true, false, false, true), Is.True, "rooted already, or immune to roots");
                Assert.That(PvpWeights.ControlCastIsWasted(false, true, false, true, false, true, false), Is.True, "stunned already, or immune to stuns");
                Assert.That(PvpWeights.ControlCastIsWasted(false, false, true, false, false, false, false), Is.True, "the target died");
                Assert.That(PvpWeights.ControlCastIsWasted(true, false, false, true, false, false, false), Is.False);
            });
        }

        private static PvpOption Option(PvpActionKind kind, int id, PvpBuffArchetype archetype = PvpBuffArchetype.Caster, bool kill = false,
            bool assigned = false, int health = 100, bool hasEffect = false, bool casting = false, bool interrupted = false,
            bool mezzed = false, int power = 100, int cost = 0, int walk = 0, int caught = 1, bool catchesKill = false, int wakes = 0,
            bool instant = false, bool controller = false) =>
            new(kind, id, archetype, kill, assigned, health, hasEffect, casting, interrupted, mezzed, power, cost, walk, caught, catchesKill, wakes,
                instant, controller);

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
            // Alone, the far one is still worth walking to: the rank penalty for walking does not count against the bar.
            (PvpOption far, int score) = Best(eCharacterClass.Enchanter, Option(PvpActionKind.Damage, 0, kill: true, walk: 1500));
            Assert.That(score, Is.LessThan(Weights.MinScore));
            Assert.That(PvpActionChoice.WorthIt(far, score, Weights), Is.True);
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
            // 90 for the Shaman's disease and 30 because the target is a fighter.
            Assert.That(full, Is.EqualTo(120));
            Assert.That(low, Is.EqualTo(120 - 19));
        }

        [Test]
        public void AmnesiaIsCastOnlyWhileSomebodyInsideItIsCasting()
        {
            Assert.That(PvpActionChoice.Rank(eCharacterClass.Bard, [Option(PvpActionKind.Amnesia, 1, caught: 0)], Weights), Is.Empty);
            // One enemy casting: 60 for the Bard, 45 for the cast it stops, 15 and 30 because that enemy is free and casting.
            Assert.That(Best(eCharacterClass.Bard, Option(PvpActionKind.Amnesia, 1, casting: true)).Score, Is.EqualTo(150));
            Assert.That(Best(eCharacterClass.Bard, Option(PvpActionKind.Amnesia, 1, casting: true, caught: 3)).Score, Is.EqualTo(240));
        }

        [Test]
        public void AmnesiaThatWouldWakeSleepersLosesToOneThatWouldNot()
        {
            (PvpOption option, _) = Best(eCharacterClass.Healer,
                Option(PvpActionKind.Amnesia, 1, casting: true, caught: 2, wakes: 2),
                Option(PvpActionKind.Amnesia, 2, casting: true, caught: 1));
            Assert.That(option.TargetId, Is.EqualTo(2));
        }

        [Test]
        public void TheBardStillMezzesBeforeItUsesAmnesia()
        {
            (PvpOption option, _) = Best(eCharacterClass.Bard,
                Option(PvpActionKind.Amnesia, 1, casting: true),
                Option(PvpActionKind.MezArea, 2, caught: 3));
            Assert.That(option.Kind, Is.EqualTo(PvpActionKind.MezArea));
        }

        [Test]
        public void TheHealerPutsItsInstantMezOnTheEnemyMezzerThatIsCasting()
        {
            // The Sorcerer has begun its area mez. The Healer could cast its own area mez on three
            // enemies (too late), its cast-time mez on the Sorcerer (too late), or the instant.
            (PvpOption option, _) = Best(eCharacterClass.Healer,
                Option(PvpActionKind.MezArea, 1, PvpBuffArchetype.Melee, caught: 3),
                Option(PvpActionKind.Mez, 2, casting: true, controller: true),
                Option(PvpActionKind.Mez, 2, casting: true, controller: true, instant: true));
            Assert.That(option.Instant, Is.True);
            Assert.That(option.TargetId, Is.EqualTo(2));
        }

        [Test]
        public void TheInstantIsKeptWhenNothingPresses()
        {
            (PvpOption option, _) = Best(eCharacterClass.Healer,
                Option(PvpActionKind.Mez, 2, controller: true, instant: true),
                Option(PvpActionKind.Mez, 2, controller: true));
            Assert.That(option.Instant, Is.False, "the target is not casting: the ten-minute instant is saved");
            Assert.That(PvpActionChoice.IsController(eCharacterClass.Sorcerer, Weights), Is.True);
            Assert.That(PvpActionChoice.IsController(eCharacterClass.Cleric, Weights), Is.False);
        }

        [Test]
        public void WithTheWeightSetWhatCanBeCastNowBeatsABetterTargetThatNeedsAWalk()
        {
            // Two fighters in range of the area mez; two enemies around a healer 600 units out of range.
            PvpOption[] options =
            [
                Option(PvpActionKind.MezArea, 1, PvpBuffArchetype.Melee, caught: 2),
                Option(PvpActionKind.MezArea, 2, PvpBuffArchetype.Healer, caught: 2, walk: 600),
            ];
            Assert.That(PvpActionChoice.Rank(eCharacterClass.Sorcerer, options, new PvpWeights { NeedsWalk = 0 }).First().Option.TargetId, Is.EqualTo(2),
                "without the weight it walks to catch the healer");
            Assert.That(PvpActionChoice.Rank(eCharacterClass.Sorcerer, options, Weights).First().Option.TargetId, Is.EqualTo(1));
        }

        [Test]
        public void TheMinstrelWakesTheGroupsMezzerFirst_AndKeepsTheOldBrainForTheRest()
        {
            (PvpOption option, int score) = Best(eCharacterClass.Minstrel,
                Option(PvpActionKind.CureMez, 1, PvpBuffArchetype.Healer, mezzed: true),
                Option(PvpActionKind.CureMez, 2, PvpBuffArchetype.Caster, mezzed: true, controller: true));
            Assert.That(option.TargetId, Is.EqualTo(2), "the Sorcerer before a Cleric");
            Assert.That(score, Is.EqualTo(130 + 20 + 30));
            Assert.That(PvpActionChoice.Covers(eCharacterClass.Minstrel, Weights), Is.True);
            Assert.That(PvpActionChoice.ScoresOffense(eCharacterClass.Minstrel, Weights), Is.False);
            Assert.That(PvpActionChoice.ScoresOffense(eCharacterClass.Sorcerer, Weights), Is.True);
        }

        [Test]
        public void ThePacificationHealerLeansToControlAndTheMendingHealerAwayFromIt()
        {
            PvpOption mez = Option(PvpActionKind.Mez, 1);
            int plain = PvpActionChoice.Score(eCharacterClass.Healer, mez, Weights);
            Assert.That(PvpActionChoice.Score(eCharacterClass.Healer, mez, Weights, "healer-pac"), Is.EqualTo(plain + 15));
            Assert.That(PvpActionChoice.Score(eCharacterClass.Healer, mez, Weights, "healer-mend"), Is.EqualTo(plain - 30));
            Assert.That(PvpActionChoice.Score(eCharacterClass.Healer, mez, Weights, "healer-mend"), Is.GreaterThanOrEqualTo(Weights.MinScore),
                "the Mending Healer still mezzes when nobody needs a heal");
        }

        [Test]
        public void WhoHealsFirstIsSetByBuildThenByClass()
        {
            Assert.That(Weights.HealsFirstBelowFor("healer-pac", eCharacterClass.Healer), Is.EqualTo(75));
            Assert.That(Weights.HealsFirstBelowFor("healer-mend", eCharacterClass.Healer), Is.GreaterThan(100), "heals always first");
            Assert.That(Weights.HealsFirstBelowFor("shaman-aug", eCharacterClass.Shaman), Is.EqualTo(75));
            Assert.That(Weights.HealsFirstBelowFor("cleric-rejuv", eCharacterClass.Cleric), Is.GreaterThan(100));
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

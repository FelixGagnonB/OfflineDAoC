using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpBuffPlan
    {
        // The Albion main group: 0 buff Cleric, 1 heal Cleric, 2 Sorcerer, 3 Theurgist, 4 Cabalist,
        // 5 Minstrel, 6 Paladin, 7 Armsman. Values and costs are the level-50 Enhancement spells.
        private static List<PvpBuffMember> Members(int clericConcentration = 500) =>
        [
            new(0, PvpBuffArchetype.Healer, clericConcentration), new(1, PvpBuffArchetype.Healer, clericConcentration),
            new(2, PvpBuffArchetype.Caster, 0), new(3, PvpBuffArchetype.Caster, 0), new(4, PvpBuffArchetype.Caster, 0),
            new(5, PvpBuffArchetype.Melee, 0), new(6, PvpBuffArchetype.Melee, 0), new(7, PvpBuffArchetype.Melee, 0),
        ];

        private static List<PvpBuffOffer> Offers()
        {
            var offers = new List<PvpBuffOffer>
            {
                // 50 Enhancement: the best of every specialization buff.
                new(0, PvpBuffKind.SpecDexQui, 75, 37), new(0, PvpBuffKind.SpecStrCon, 69, 33),
                new(0, PvpBuffKind.SpecArmor, 67, 32), new(0, PvpBuffKind.Acuity, 52, 15),
                // 28 Enhancement: weaker versions that must never be handed out.
                new(1, PvpBuffKind.SpecDexQui, 39, 13), new(1, PvpBuffKind.SpecStrCon, 45, 18),
                new(1, PvpBuffKind.SpecArmor, 43, 17), new(1, PvpBuffKind.Acuity, 32, 8),
            };
            foreach (int cleric in new[] { 0, 1 })   // baseline buffs: the same for both
            {
                offers.Add(new(cleric, PvpBuffKind.BaseStrength, 50, 19));
                offers.Add(new(cleric, PvpBuffKind.BaseDexterity, 48, 18));
                offers.Add(new(cleric, PvpBuffKind.BaseConstitution, 44, 16));
                offers.Add(new(cleric, PvpBuffKind.BaseArmor, 52, 15));
            }
            return offers;
        }

        [Test]
        public void SpecializationBuffsOnlyComeFromTheStrongestCaster()
        {
            PvpBuffPlanResult plan = PvpBuffPlan.Build(Members(), Offers());
            PvpBuffKind[] spec = [PvpBuffKind.SpecDexQui, PvpBuffKind.SpecStrCon, PvpBuffKind.SpecArmor, PvpBuffKind.Acuity];
            Assert.That(plan.Assignments.Where(a => spec.Contains(a.Kind)).Select(a => a.Caster).Distinct(),
                Is.EqualTo(new[] { 0 }));
            Assert.That(plan.Assignments.Where(a => a.Kind == PvpBuffKind.SpecDexQui).Select(a => a.Value).Distinct(),
                Is.EqualTo(new[] { 75 }));
        }

        [Test]
        public void EveryCasterGetsTopDexQuiAndAcuityBeforeAnythingElseIsSpent()
        {
            PvpBuffPlanResult plan = PvpBuffPlan.Build(Members(700), Offers());
            foreach (int everyone in Enumerable.Range(0, 8))
            {
                Assert.That(plan.Assignments, Has.Some.Matches<PvpBuffAssignment>(a => a.Target == everyone && a.Kind == PvpBuffKind.SpecDexQui));
                Assert.That(plan.Assignments, Has.Some.Matches<PvpBuffAssignment>(a => a.Target == everyone && a.Kind == PvpBuffKind.SpecStrCon));
            }
            Assert.That(plan.Assignments.Select(a => a.Kind), Has.None.EqualTo(PvpBuffKind.BaseArmor));
            foreach (int caster in new[] { 2, 3, 4 })
            {
                Assert.That(plan.Assignments, Has.Some.Matches<PvpBuffAssignment>(a =>
                    a.Target == caster && a.Kind == PvpBuffKind.SpecDexQui && a.Value == 75));
                Assert.That(plan.Assignments, Has.Some.Matches<PvpBuffAssignment>(a =>
                    a.Target == caster && a.Kind == PvpBuffKind.Acuity && a.Value == 52));
            }
            foreach (int melee in new[] { 5, 6, 7 })
            {
                Assert.That(plan.Assignments, Has.Some.Matches<PvpBuffAssignment>(a =>
                    a.Target == melee && a.Kind == PvpBuffKind.SpecStrCon && a.Value == 69));
                Assert.That(plan.Assignments, Has.None.Matches<PvpBuffAssignment>(a =>
                    a.Target == melee && a.Kind == PvpBuffKind.Acuity));
            }
        }

        [Test]
        public void NobodySpendsMoreConcentrationThanTheyHave()
        {
            List<PvpBuffOffer> offers = Offers();
            PvpBuffPlanResult plan = PvpBuffPlan.Build(Members(), offers);
            foreach (IGrouping<int, PvpBuffAssignment> byCaster in plan.Assignments.GroupBy(a => a.Caster))
            {
                int spent = byCaster.Sum(a => offers.First(o => o.Caster == a.Caster && o.Kind == a.Kind && o.Value == a.Value).Concentration);
                Assert.That(spent, Is.LessThanOrEqualTo(500), $"caster {byCaster.Key}");
            }
            Assert.That(plan.Assignments.GroupBy(a => (a.Target, a.Kind)).All(g => g.Count() == 1), Is.True);
        }

        [Test]
        public void AShortageDropsTheLeastNeededBuffsNotTheMostNeeded()
        {
            // 300 concentration each cannot cover the group.
            PvpBuffPlanResult plan = PvpBuffPlan.Build(Members(300), Offers());
            Assert.That(plan.Unfilled, Is.Not.Empty);
            // Everyone still gets their own first need: Str/Con for the melee, Dex/Qui for the rest.
            Assert.That(plan.Assignments.Count(a => a.Kind == PvpBuffKind.SpecStrCon && a.Target >= 5), Is.EqualTo(3));
            Assert.That(plan.Assignments.Count(a => a.Kind == PvpBuffKind.SpecDexQui && a.Target < 5), Is.EqualTo(5));
            // What goes missing is from the back of the list, never a caster's Dex/Qui.
            Assert.That(plan.Unfilled, Has.None.Matches<(int Target, PvpBuffKind Kind)>(u =>
                u.Kind == PvpBuffKind.SpecDexQui && u.Target < 5));
        }

        [Test]
        public void OneBufferAtFortyTwoEnhancementCoversDexQuiAndStrConOnAllEight()
        {
            // The second-best ("yellow") spells of a 42 Enhancement Cleric and their real costs.
            var offers = new List<PvpBuffOffer>
            {
                new(0, PvpBuffKind.SpecDexQui, 63, 28), new(0, PvpBuffKind.SpecStrCon, 57, 24),
                new(0, PvpBuffKind.SpecArmor, 55, 24), new(0, PvpBuffKind.Acuity, 52, 15),
                new(1, PvpBuffKind.SpecDexQui, 39, 13), new(1, PvpBuffKind.SpecStrCon, 45, 18),
            };
            PvpBuffPlanResult plan = PvpBuffPlan.Build(Members(), offers);
            Assert.That(plan.Assignments.Where(a => a.Kind == PvpBuffKind.SpecDexQui).Select(a => a.Target),
                Is.EquivalentTo(Enumerable.Range(0, 8)));
            Assert.That(plan.Assignments.Where(a => a.Kind == PvpBuffKind.SpecStrCon).Select(a => a.Target),
                Is.EquivalentTo(Enumerable.Range(0, 8)));
            Assert.That(plan.Assignments.Count(a => a.Kind == PvpBuffKind.Acuity), Is.EqualTo(5));
            Assert.That(plan.Assignments.Select(a => a.Caster).Distinct(), Is.EqualTo(new[] { 0 }));
            // 8 x 28 + 8 x 24 + 5 x 15 = 491 of 500: the melee's specialization armour does not fit.
            Assert.That(plan.Unfilled.Select(u => u.Kind).Distinct(), Is.EqualTo(new[] { PvpBuffKind.SpecArmor }));
        }

        [Test]
        public void BuffsAlreadyHeldAreLeftAloneSoOnlyTheMissingOnesAreAssigned()
        {
            // Everyone is fully buffed (by the buffbot) except member 5, who died and came back.
            PvpBuffPlanResult plan = PvpBuffPlan.Build(Members(), Offers(), (target, kind) => target != 5);
            Assert.That(plan.Assignments.Select(a => a.Target).Distinct(), Is.EqualTo(new[] { 5 }));
            Assert.That(plan.Assignments.Select(a => a.Kind), Does.Contain(PvpBuffKind.SpecDexQui).And.Contain(PvpBuffKind.SpecStrCon));
            Assert.That(PvpBuffPlan.Build(Members(), Offers(), (_, _) => true).Assignments, Is.Empty);
        }

        [Test]
        public void ABuffNobodyHasIsNotReportedAsAShortage()
        {
            PvpBuffPlanResult plan = PvpBuffPlan.Build(Members(), Offers());
            Assert.That(plan.Assignments.Select(a => a.Kind), Has.None.EqualTo(PvpBuffKind.Haste));
            Assert.That(plan.Unfilled.Select(u => u.Kind), Has.None.EqualTo(PvpBuffKind.Haste));
        }

        [TestCase(eCharacterClass.Sorcerer, PvpBuffArchetype.Caster)]
        [TestCase(eCharacterClass.Enchanter, PvpBuffArchetype.Caster)]
        [TestCase(eCharacterClass.Druid, PvpBuffArchetype.Healer)]
        [TestCase(eCharacterClass.Bard, PvpBuffArchetype.Healer)]
        [TestCase(eCharacterClass.Hero, PvpBuffArchetype.Melee)]
        [TestCase(eCharacterClass.Skald, PvpBuffArchetype.Melee)]
        public void ArchetypeFromClass(eCharacterClass characterClass, PvpBuffArchetype expected)
        {
            Assert.That(PvpBuffPlan.ArchetypeOf(characterClass), Is.EqualTo(expected));
        }

        [Test]
        public void CastersNeverWantStrengthAndMeleeNeverWantAcuity()
        {
            Assert.That(PvpBuffPlan.NeedsOf(PvpBuffArchetype.Caster), Has.None.EqualTo(PvpBuffKind.BaseStrength));
            Assert.That(PvpBuffPlan.NeedsOf(PvpBuffArchetype.Melee), Has.None.EqualTo(PvpBuffKind.Acuity));
            Assert.That(PvpBuffPlan.NeedsOf(PvpBuffArchetype.Caster).Take(2),
                Is.EquivalentTo(new[] { PvpBuffKind.SpecDexQui, PvpBuffKind.SpecStrCon }));
        }

        [Test]
        public void NoDamageShieldIsCastInAnyRealm()
        {
            // The owner, 2026-11-20: "General rule for all 3 realms, do not cast damage shield."
            Assert.That(PvpBuffPlan.NeverCast(eSpellType.DamageShield), Is.True);
            Assert.That(PvpBuffPlan.NeverCast(eSpellType.DamageAdd), Is.False);
            Assert.That(PvpBuffPlan.NeverCast(eSpellType.Bladeturn), Is.False);
        }
    }
}

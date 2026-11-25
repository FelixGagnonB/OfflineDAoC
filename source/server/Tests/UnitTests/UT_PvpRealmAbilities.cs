using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using NUnit.Framework;
using Step = DOL.GS.AutonomousBotRealmAbilities.Step;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpRealmAbilities
    {
        private static readonly PvpRealmAbilityWeights Weights = new();

        private static PvpAbilityView View(bool inFight = true, int health = 100, bool hitInMelee = false, bool interrupted = false, int hurt = 0,
            int badlyHurt = 0, int lowestOther = 100, int missing = 0, int membersHitInMelee = 0, int slowed = 0, int enemyCasters = 0,
            bool retreating = false, PvpBuffArchetype archetype = PvpBuffArchetype.Healer, PvpOrder order = PvpOrder.Engage, int fightersFromEnemy = 0,
            int softHit = 0, int membersAlive = 0, int enemiesAlive = 0) =>
            new(inFight, health, hitInMelee, interrupted, hurt, badlyHurt, lowestOther, missing, membersHitInMelee, slowed, enemyCasters, retreating, archetype,
                order, fightersFromEnemy, softHit, membersAlive, enemiesAlive);

        private static bool Wanted(string key, PvpAbilityView view) => PvpRealmAbilities.Wanted(view, key, Weights);

        // The costs of the game (realmabilities_atlasOF): five levels 1, 3, 6, 10, 14; three levels 3, 6, 10; the rest by name.
        private static int Cost(string key, int level) => key switch
        {
            AutonomousBotRealmAbilities.Purge or PvpRealmAbilities.SpeedOfSound or PvpRealmAbilities.Battery or PvpRealmAbilities.Bunker or
                PvpRealmAbilities.Barricade or AutonomousBotRealmAbilities.RagingPower or AutonomousBotRealmAbilities.SecondWind => 10,
            AutonomousBotRealmAbilities.PurgeReduced => 4,
            AutonomousBotRealmAbilities.IgnorePainTank => 8,
            PvpPurge.GroupPurgeKey or PvpRealmAbilities.Concentration or PvpRealmAbilities.PerfectRecovery or PvpRealmAbilities.Melodies or
                PvpRealmAbilities.Aura or PvpRealmAbilities.FaithHealing or AutonomousBotRealmAbilities.IgnorePain => 14,
            AutonomousBotRealmAbilities.Mcl or AutonomousBotRealmAbilities.FirstAid or PvpRealmAbilities.AvoidPain or
                PvpRealmAbilities.ArmorOfFaith or PvpRealmAbilities.EmptyMind => level < 3 ? new[] { 3, 6, 10 }[level] : 1000,
            PvpRealmAbilities.Determination or PvpRealmAbilities.DeterminationHybrid => level < 5 ? new[] { 1, 2, 3, 6, 10 }[level] : 1000,
            _ => level < 5 ? new[] { 1, 3, 6, 10, 14 }[level] : 1000,
        };

        private static Dictionary<string, int> Plan(eCharacterClass characterClass, int points)
        {
            // Everything its order names, at the levels the game allows.
            Dictionary<string, int> available = PvpRealmAbilities.OrderOf((int)characterClass).GroupBy(step => step.Key)
                .ToDictionary(group => group.Key, group => group.Key is AutonomousBotRealmAbilities.Mcl or AutonomousBotRealmAbilities.FirstAid ? 3
                    : PvpRealmAbilities.Actives.Contains(group.Key) || group.Key is AutonomousBotRealmAbilities.Purge or AutonomousBotRealmAbilities.PurgeReduced or
                        AutonomousBotRealmAbilities.IgnorePain or AutonomousBotRealmAbilities.IgnorePainTank or PvpRealmAbilities.Tireless ? 1 : 5);
            foreach (string actives in new[] { PvpRealmAbilities.AvoidPain, PvpRealmAbilities.ArmorOfFaith, PvpRealmAbilities.EmptyMind })
            {
                if (available.ContainsKey(actives))
                    available[actives] = 3;
            }
            return AutonomousBotRealmAbilities.Plan(available, AutonomousBotRealmAbilities.Archetype.Melee, (int)characterClass, points, Cost,
                PvpRealmAbilities.OrderOf((int)characterClass));
        }

        [Test]
        public void AnAbilityForTheWholeGroupIsUsedByOneMemberAtATime()
        {
            var weights = new PvpRealmAbilityWeights();
            // Both Druids of the owner's group purged it 2.4 seconds apart (2026-11-24): the second keeps its own.
            Assert.That(PvpRealmAbilities.KeptApartSeconds(PvpPurge.GroupPurgeKey, weights), Is.EqualTo(15));
            Assert.That(PvpRealmAbilities.KeptApartSeconds(PvpRealmAbilities.SpeedOfSound, weights), Is.EqualTo(40));
            Assert.That(PvpRealmAbilities.KeptApartSeconds(PvpRealmAbilities.Bunker, weights), Is.EqualTo(30));
            Assert.That(PvpRealmAbilities.KeptApartSeconds(PvpRealmAbilities.Melodies, weights), Is.EqualTo(30));
            Assert.That(PvpRealmAbilities.KeptApartSeconds(PvpRealmAbilities.Concentration, weights), Is.EqualTo(0), "an ability of one's own");
            Assert.That(PvpRealmAbilities.KeptApartSeconds(PvpRealmAbilities.AvoidPain, weights), Is.EqualTo(0));
            Assert.That(PvpRealmAbilities.TakenBySomebodyElse(false, 102_400, 100_000, 15), Is.True, "2.4 seconds after the other Druid");
            Assert.That(PvpRealmAbilities.TakenBySomebodyElse(false, 116_000, 100_000, 15), Is.False, "the next wave of control");
            Assert.That(PvpRealmAbilities.TakenBySomebodyElse(true, 102_400, 100_000, 15), Is.False, "its own use is no reason to wait");
            Assert.That(PvpRealmAbilities.TakenBySomebodyElse(false, 102_400, 0, 15), Is.False, "nobody has used it");
            Assert.That(PvpRealmAbilities.TakenBySomebodyElse(false, 102_400, 100_000, 0), Is.False, "turns switched off");
        }

        [Test]
        public void EveryClassOfTheCatalogueHasItsOwnOrder()
        {
            foreach (eCharacterClass characterClass in PvpBuildCatalog.Builds.Select(build => build.Class).Distinct())
                Assert.That(PvpRealmAbilities.OrderOf((int)characterClass), Is.Not.SameAs(AutonomousBotRealmAbilities.PvpFirst), characterClass.ToString());
        }

        [Test]
        public void NoOrderAsksForSomethingBeforeWhatItNeeds()
        {
            foreach (eCharacterClass characterClass in PvpBuildCatalog.Builds.Select(build => build.Class).Distinct())
            {
                var owned = new Dictionary<string, int>();
                foreach (Step step in PvpRealmAbilities.OrderOf((int)characterClass))
                {
                    if (AutonomousBotRealmAbilities.Requirement(step.Key, (int)characterClass) is { } need)
                        Assert.That(owned.GetValueOrDefault(need.Key), Is.GreaterThanOrEqualTo(need.Level), $"{characterClass}: {step.Key} before {need.Key} {need.Level}");
                    Assert.That(step.Level, Is.GreaterThan(owned.GetValueOrDefault(step.Key)), $"{characterClass}: {step.Key} {step.Level} twice");
                    owned[step.Key] = step.Level;
                }
            }
        }

        [Test]
        public void EveryActiveInAnOrderIsOneABotKnowsHowToUse()
        {
            string[] known = [.. PvpRealmAbilities.Actives, .. AutonomousBotRealmAbilities.UsableActives];
            string[] actives =
            [
                PvpRealmAbilities.SpeedOfSound, PvpRealmAbilities.Concentration, PvpRealmAbilities.Battery, PvpRealmAbilities.Bunker,
                PvpRealmAbilities.Barricade, PvpRealmAbilities.Aura, PvpRealmAbilities.Melodies, PvpRealmAbilities.FaithHealing,
                PvpRealmAbilities.PerfectRecovery, PvpRealmAbilities.AvoidPain, PvpRealmAbilities.ArmorOfFaith, PvpRealmAbilities.EmptyMind,
            ];
            foreach (string key in actives)
            {
                Assert.That(known, Does.Contain(key));
                Assert.That(PvpRealmAbilities.Decides(key), Is.True, key);
            }
        }

        [Test]
        public void ADruidAtRankFiveHasBothPurgesAndItsPowerBack()
        {
            Dictionary<string, int> druid = Plan(eCharacterClass.Druid, 40);
            Assert.That(druid[PvpRealmAbilities.Tireless], Is.EqualTo(1), "the owner: among the first to get");
            Assert.That(druid.ContainsKey(PvpPurge.GroupPurgeKey) && druid.ContainsKey(AutonomousBotRealmAbilities.Purge), Is.True);
            Assert.That(druid[AutonomousBotRealmAbilities.Mcl], Is.EqualTo(2));
            Assert.That(druid.ContainsKey(PvpRealmAbilities.Concentration), Is.False, "24 points more: at rank 7");
            // (The owner's lists of 2026-11-25, docs/DAoC-1.65-8v8-RA-Progressions-v3.md: Group Purge, Purge, Tireless, Mystic Crystal Lore 2,
            // Augmented Acuity 3, Mastery of Concentration: 58 points, held for good from 6L8 once the steps after it are not bought first.)
            Assert.That(Plan(eCharacterClass.Druid, 70).ContainsKey(PvpRealmAbilities.Concentration), Is.True);
        }

        [Test]
        public void AtRankOneAPointIsNotLeftUnspent()
        {
            // One point: Tireless. The Purge it cannot pay for yet is passed over and comes first again later.
            Assert.That(Plan(eCharacterClass.Hero, 1).Keys, Is.EquivalentTo(new[] { PvpRealmAbilities.Tireless }));
            Dictionary<string, int> rank2 = Plan(eCharacterClass.Hero, 10);
            Assert.That(rank2.ContainsKey(AutonomousBotRealmAbilities.PurgeReduced), Is.True, "four points for a fighter");
            Assert.That(rank2[PvpRealmAbilities.Determination], Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void AFighterAtRankFiveHasPurgeDeterminationAndAvoidPain()
        {
            // The owner's list for the Hero that peels (2026-11-25): Determination 4, then Augmented Constitution 3 for Avoid Pain;
            // Ignore Pain comes late in it (rank 11), where the list before had it at rank 4.
            Dictionary<string, int> hero = Plan(eCharacterClass.Hero, 40);
            Assert.That(hero.ContainsKey(AutonomousBotRealmAbilities.PurgeReduced), Is.True);
            Assert.That(hero[PvpRealmAbilities.Determination], Is.GreaterThanOrEqualTo(4));
            Assert.That(hero.ContainsKey(PvpRealmAbilities.AvoidPain), Is.True);
            Assert.That(hero.ContainsKey(AutonomousBotRealmAbilities.IgnorePainTank), Is.False);
            Assert.That(Plan(eCharacterClass.Hero, 113).ContainsKey(AutonomousBotRealmAbilities.IgnorePainTank), Is.True);
        }

        [Test]
        public void TheSpeedClassesBuySpeedOfSoundRightAfterTheirPurge()
        {
            foreach (eCharacterClass characterClass in new[] { eCharacterClass.Minstrel, eCharacterClass.Bard, eCharacterClass.Skald })
                Assert.That(Plan(characterClass, 21).ContainsKey(PvpRealmAbilities.SpeedOfSound), Is.True, characterClass.ToString());
        }

        [Test]
        public void NothingIsPressedOutsideAFight()
        {
            PvpAbilityView everythingWrong = View(inFight: false, health: 10, hitInMelee: true, interrupted: true, hurt: 8, badlyHurt: 8, lowestOther: 5,
                missing: 9_000, membersHitInMelee: 8, slowed: 8, enemyCasters: 8, retreating: true);
            foreach (string key in PvpRealmAbilities.Actives.Where(PvpRealmAbilities.Decides))
                Assert.That(Wanted(key, everythingWrong), Is.False, key);
            Assert.That(PvpRealmAbilities.Recovers(false, new[] { ("a healer", true, 100) }, 1_500), Is.Null);
        }

        [Test]
        public void SpeedOfSoundOnACalledRetreatOrWithTwoMembersHeldByTheLegs()
        {
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(retreating: true)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(slowed: 2)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(slowed: 1)), Is.False, "one snared member is not worth thirty minutes");
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View()), Is.False);
        }

        [Test]
        public void SpeedOfSoundToGetInOnAPushAndToGetAwayWhenJumped()
        {
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Push, fightersFromEnemy: 1_800)), Is.True, "they kite: the group runs in");
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Push, fightersFromEnemy: 600)), Is.False, "its fighters are on them already");
            // Not to get in on fewer than it is itself: the last three of a fight that is won (the owner's, 2026-11-24) are walked down.
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Push, fightersFromEnemy: 1_800, membersAlive: 8, enemiesAlive: 3)), Is.False);
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Push, fightersFromEnemy: 1_800, membersAlive: 8, enemiesAlive: 8)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Push, fightersFromEnemy: 1_800, membersAlive: 5, enemiesAlive: 8)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(retreating: true, membersAlive: 8, enemiesAlive: 3)), Is.True, "a called retreat is a called retreat");
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Engage, fightersFromEnemy: 1_800)), Is.False, "nobody called the push");
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Extend, softHit: 2)), Is.True, "jumped: the group repositions");
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Regroup, softHit: 2)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Extend, softHit: 1)), Is.False);
            Assert.That(Wanted(PvpRealmAbilities.SpeedOfSound, View(order: PvpOrder.Engage, softHit: 3)), Is.False, "it stands and fights");
        }

        [Test]
        public void AHealerPressesConcentrationWhenItCannotCastAndSomebodyIsLow()
        {
            Assert.That(Wanted(PvpRealmAbilities.Concentration, View(interrupted: true, lowestOther: 40)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.Concentration, View(interrupted: true, lowestOther: 90)), Is.False, "nobody needs the heal");
            Assert.That(Wanted(PvpRealmAbilities.Concentration, View(interrupted: false, lowestOther: 20)), Is.False, "it can cast as it is");
        }

        [Test]
        public void ACasterPressesConcentrationToCastThroughAFighter()
        {
            Assert.That(Wanted(PvpRealmAbilities.Concentration, View(interrupted: true, hitInMelee: true, health: 80, archetype: PvpBuffArchetype.Caster)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.Concentration, View(interrupted: true, hitInMelee: true, health: 20, archetype: PvpBuffArchetype.Caster)), Is.False,
                "nearly dead: fifteen seconds of casting it will not live");
            Assert.That(Wanted(PvpRealmAbilities.Concentration, View(interrupted: true, hitInMelee: true, archetype: PvpBuffArchetype.Melee)), Is.False);
        }

        [Test]
        public void TheGroupHealsWaitForTwoHurtMembers()
        {
            Assert.That(Wanted(PvpRealmAbilities.Battery, View(hurt: 2, missing: 1_400)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.Battery, View(hurt: 2, missing: 600)), Is.False, "most of its 1,000 would be lost");
            Assert.That(Wanted(PvpRealmAbilities.Battery, View(hurt: 1, missing: 1_400)), Is.False);
            Assert.That(Wanted(PvpRealmAbilities.Melodies, View(hurt: 2)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.Melodies, View(hurt: 1)), Is.False);
            Assert.That(Wanted(PvpRealmAbilities.FaithHealing, View(hurt: 3, badlyHurt: 2)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.FaithHealing, View(hurt: 3, badlyHurt: 1)), Is.False);
        }

        [Test]
        public void TheGroupShieldsAnswerWhatIsHittingTheGroup()
        {
            Assert.That(Wanted(PvpRealmAbilities.Bunker, View(membersHitInMelee: 2, hurt: 2)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.Barricade, View(membersHitInMelee: 2, hurt: 2)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.Bunker, View(membersHitInMelee: 0, hurt: 4)), Is.False, "absorb does nothing against spells");
            Assert.That(Wanted(PvpRealmAbilities.Aura, View(enemyCasters: 2, hurt: 1)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.Aura, View(enemyCasters: 2, hurt: 0)), Is.False);
            Assert.That(Wanted(PvpRealmAbilities.Aura, View(enemyCasters: 1, hurt: 3)), Is.False);
        }

        [Test]
        public void ItsOwnShieldsNeedAFighterOnIt_AndTheEmptyMindNone()
        {
            Assert.That(Wanted(PvpRealmAbilities.AvoidPain, View(hitInMelee: true, health: 50)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.AvoidPain, View(hitInMelee: false, health: 50)), Is.False);
            Assert.That(Wanted(PvpRealmAbilities.AvoidPain, View(hitInMelee: true, health: 90)), Is.False);
            Assert.That(Wanted(PvpRealmAbilities.ArmorOfFaith, View(hitInMelee: true, health: 70)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.EmptyMind, View(hitInMelee: false, health: 50)), Is.True);
            Assert.That(Wanted(PvpRealmAbilities.EmptyMind, View(hitInMelee: true, health: 50)), Is.False);
        }

        [Test]
        public void PerfectRecoveryRaisesAHealerBeforeAnybodyNearer()
        {
            var dead = new[] { ("a fighter", false, 300), ("a healer", true, 1_200), ("far", true, 2_400) };
            Assert.That(PvpRealmAbilities.Recovers(true, dead, 1_500), Is.EqualTo("a healer"));
            Assert.That(PvpRealmAbilities.Recovers(true, dead.Take(1), 1_500), Is.EqualTo("a fighter"));
            Assert.That(PvpRealmAbilities.Recovers(true, dead.Skip(2), 1_500), Is.Null, "out of its reach");
        }

        [Test]
        public void LongWindMakesABotsSprintCheaperAsItDoesAPlayers()
        {
            Assert.That(PvpPositionWeights.SprintCostWith(5, 0), Is.EqualTo(5));
            Assert.That(PvpPositionWeights.SprintCostWith(5, 20), Is.EqualTo(4));
            Assert.That(PvpPositionWeights.SprintCostWith(5, 60), Is.EqualTo(2));
            Assert.That(PvpPositionWeights.SprintCostWith(5, 100), Is.EqualTo(0));
        }

        [Test]
        public void ASharpGroupIsOnANewKillTargetAtOnce_ACasualOneLateAndNotAllOfIt()
        {
            var random = new System.Random(7);
            PvpHumanWeights sharp = PvpHumanWeights.For(PvpSkill.Sharp), casual = PvpHumanWeights.For(PvpSkill.Casual), seasoned = new();
            List<int> Draws(PvpHumanWeights weights) => Enumerable.Range(0, 400).Select(_ => PvpNotice.DrawAssistSwitch(weights, random)).ToList();
            Assert.That(Draws(sharp).Max(), Is.LessThanOrEqualTo(350));
            List<int> late = Draws(casual);
            Assert.That(late.Min(), Is.GreaterThanOrEqualTo(2_000));
            Assert.That(late.Count(delay => delay == casual.KeepsOldTargetMilliseconds), Is.InRange(60, 140), "about one in four does not hear the call");
            Assert.That(Draws(seasoned).OrderBy(delay => delay).ElementAt(200), Is.InRange(400, 1_200), "the middle one within a second or so");
            Assert.That(PvpNotice.DrawAssistSwitch(new PvpHumanWeights { AssistSwitchMinMilliseconds = 0, AssistSwitchMaxMilliseconds = 0, KeepsOldTargetPercent = 0 }, random), Is.EqualTo(0));
        }

        [Test]
        public void AMemberIsHurtFromTheMomentItGoesBelowTheMarkUntilItIsAboveItAgain()
        {
            Assert.That(PvpNotice.HurtBegan(0, 100, 90, 5_000), Is.EqualTo(0), "not hurt");
            Assert.That(PvpNotice.HurtBegan(0, 80, 90, 5_000), Is.EqualTo(5_000), "it has just gone below");
            Assert.That(PvpNotice.HurtBegan(5_000, 30, 90, 9_000), Is.EqualTo(5_000), "still hurt: the healer that has seen it goes on seeing it");
            Assert.That(PvpNotice.HurtBegan(5_000, 95, 90, 9_000), Is.EqualTo(0), "topped up: the next blow is a new thing to see");
        }

        [Test]
        public void APetDoesNotJoinAPlayersAssistCall()
        {
            Assert.That(PvpCalls.PetAim("its duty", "the kill", "what it has", false), Is.EqualTo("its duty"));
            Assert.That(PvpCalls.PetAim<string>(null, "the kill", "what it has", false), Is.EqualTo("the kill"), "the group's own call: the pet goes too");
            Assert.That(PvpCalls.PetAim<string>(null, "the player's call", "what it has", true), Is.EqualTo("what it has"), "it stays on its enemy");
            Assert.That(PvpCalls.PetAim<string>(null, "the player's call", null, true), Is.Null, "or on none");
            Assert.That(PvpCalls.PetAim("its duty", "the player's call", "what it has", true), Is.EqualTo("its duty"));
        }

        [Test]
        public void SerenityAndEtherealBondHaveTheirPrerequisites()
        {
            Assert.That(AutonomousBotRealmAbilities.Requirement("AtlasOF_Serenity", 6), Is.EqualTo((AutonomousBotRealmAbilities.AugAcuity, 2)));
            Assert.That(AutonomousBotRealmAbilities.Requirement("AtlasOF_EtherealBond", 6), Is.EqualTo(("AtlasOF_Serenity", 2)));
        }
    }
}

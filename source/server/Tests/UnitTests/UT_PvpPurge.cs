using System;
using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_PvpPurge
    {
        private static readonly PvpPurgeWeights Weights = new();

        private static PvpPurgeView Member(bool mezzed = false, bool stunned = false, bool nearsighted = false, int secondsLeft = 40,
            PvpBuffArchetype archetype = PvpBuffArchetype.Melee, bool controller = false) =>
            new(mezzed, stunned, nearsighted, secondsLeft, archetype, controller);

        [Test]
        public void InABadGroupOneMemberInFourHasPurge()
        {
            // The owner, 2026-11-24: "the bad still seems too good to me". Two bad groups under two Bards were asleep 2 to 4% of the time.
            Assert.That(new PvpPurgeWeights().HeldByPercent, Is.EqualTo(100), "every other kind: everybody");
            PvpWeights bad = PvpStyles.For(PvpTeamKind.Bad, new PvpWeights());
            Assert.That(bad.Purge.HeldByPercent, Is.EqualTo(25));
            Assert.That(PvpStyles.For(PvpTeamKind.Clingy, new PvpWeights()).Purge.HeldByPercent, Is.EqualTo(100));
            int holds = 0;
            for (int i = 0; i < 2_000; i++)
            {
                string name = "Member" + (char)('a' + i % 26) + i;
                Assert.That(PvpPurge.Holds(name, 100), Is.True);
                Assert.That(PvpPurge.Holds(name, 0), Is.False);
                Assert.That(PvpPurge.Holds(name, 25), Is.EqualTo(PvpPurge.Holds(name, 25)), "the same member all evening");
                if (PvpPurge.Holds(name, 25))
                    holds++;
            }
            Assert.That(holds, Is.InRange(400, 600));
        }

        [Test]
        public void AMezzedMemberPurges()
        {
            Assert.That(PvpPurge.Score(Member(mezzed: true), false, Weights), Is.GreaterThanOrEqualTo(Weights.MinScore));
        }

        [Test]
        public void AMezAboutToEndIsWaitedOut()
        {
            Assert.That(PvpPurge.Score(Member(mezzed: true, secondsLeft: 3), false, Weights), Is.EqualTo(0));
        }

        [Test]
        public void AStunIsPurgedByAHealerThatAlsoMezzes_NotByAFighter()
        {
            Assert.That(PvpPurge.Score(Member(stunned: true, secondsLeft: 9), false, Weights), Is.LessThan(Weights.MinScore));
            Assert.That(PvpPurge.Score(Member(stunned: true, secondsLeft: 9, archetype: PvpBuffArchetype.Healer, controller: true), false, Weights),
                Is.GreaterThanOrEqualTo(Weights.MinScore));
        }

        [Test]
        public void NearsightAloneIsNotPurged_AndAFreeMemberNeverPurges()
        {
            Assert.That(PvpPurge.Score(Member(nearsighted: true, archetype: PvpBuffArchetype.Caster), false, Weights), Is.LessThan(Weights.MinScore));
            Assert.That(PvpPurge.Score(Member(nearsighted: true), false, Weights), Is.EqualTo(0), "nearsight does not trouble a fighter");
            Assert.That(PvpPurge.Score(Member(), false, Weights), Is.EqualTo(0));
        }

        [Test]
        public void AMemberWaitsForAGroupPurgeThatIsComing()
        {
            Assert.That(PvpPurge.Score(Member(mezzed: true), true, Weights), Is.LessThan(Weights.MinScore));
        }

        [Test]
        public void GroupPurgeIsForSeveralMembers()
        {
            Assert.That(PvpPurge.GroupScore([Member(mezzed: true), Member()], Weights), Is.LessThan(Weights.GroupMinScore));
            Assert.That(PvpPurge.GroupScore([Member(mezzed: true), Member(mezzed: true), Member(mezzed: true)], Weights),
                Is.GreaterThanOrEqualTo(Weights.GroupMinScore));
            Assert.That(PvpPurge.GroupScore([Member(mezzed: true, archetype: PvpBuffArchetype.Healer), Member(mezzed: true)], Weights),
                Is.GreaterThanOrEqualTo(Weights.GroupMinScore), "two asleep, one of them a healer");
        }

        [Test]
        public void An8v8BotBuysItsPurgeFirst()
        {
            var available = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["AtlasOF_AugCon"] = 5, ["AtlasOF_AugDex"] = 5, [AutonomousBotRealmAbilities.Purge] = 1, [PvpPurge.GroupPurgeKey] = 1,
            };
            int Cost(string key, int level) => key == AutonomousBotRealmAbilities.Purge ? 10 : key == PvpPurge.GroupPurgeKey ? 14 : level + 1;
            Dictionary<string, int> druid = AutonomousBotRealmAbilities.Plan(available, AutonomousBotRealmAbilities.Archetype.Healer, 47, 24, Cost,
                AutonomousBotRealmAbilities.PvpFirst);
            Assert.That(druid.ContainsKey(PvpPurge.GroupPurgeKey) && druid.ContainsKey(AutonomousBotRealmAbilities.Purge), Is.True);
            Dictionary<string, int> poor = AutonomousBotRealmAbilities.Plan(available, AutonomousBotRealmAbilities.Archetype.Healer, 47, 9, Cost,
                AutonomousBotRealmAbilities.PvpFirst);
            Assert.That(poor.ContainsKey(PvpPurge.GroupPurgeKey) || poor.ContainsKey(AutonomousBotRealmAbilities.Purge), Is.False);
            Assert.That(poor, Is.Not.Empty, "what it cannot pay for yet does not stop it buying the rest");
        }

        [Test]
        public void ACasterBuysMysticCrystalLoreRightAfterItsPurge()
        {
            // The owner, 2026-11-19: power came back from Mystic Crystal Lore and Raging Power; without them "was a rough time".
            var available = new Dictionary<string, int>
            {
                ["AtlasOF_AugAcuity"] = 5, ["AtlasOF_AugCon"] = 5, [AutonomousBotRealmAbilities.Purge] = 1,
                [AutonomousBotRealmAbilities.Mcl] = 3, [AutonomousBotRealmAbilities.RagingPower] = 1,
            };
            int Cost(string key, int level) => key == AutonomousBotRealmAbilities.Purge || key == AutonomousBotRealmAbilities.RagingPower ? 10
                : key == AutonomousBotRealmAbilities.Mcl ? new[] { 3, 6, 10 }[level] : level + 1;
            Dictionary<string, int> rank3 = AutonomousBotRealmAbilities.Plan(available, AutonomousBotRealmAbilities.Archetype.Caster, 5, 20, Cost,
                AutonomousBotRealmAbilities.PvpFirst);
            Assert.That(rank3[AutonomousBotRealmAbilities.Purge], Is.EqualTo(1));
            Assert.That(rank3[AutonomousBotRealmAbilities.Mcl], Is.EqualTo(2), "nine points, right after the Purge");
            Assert.That(rank3.ContainsKey(AutonomousBotRealmAbilities.RagingPower), Is.False, "no points left for it at this rank");
            // Raging Power is never bought without Mystic Crystal Lore 2.
            Assert.That(AutonomousBotRealmAbilities.Requirement(AutonomousBotRealmAbilities.RagingPower, 5), Is.EqualTo((AutonomousBotRealmAbilities.Mcl, 2)));
            // A fighter's class has neither: nothing changes for it.
            var fighter = new Dictionary<string, int> { ["AtlasOF_AugCon"] = 5, [AutonomousBotRealmAbilities.PurgeReduced] = 1 };
            Assert.That(AutonomousBotRealmAbilities.Plan(fighter, AutonomousBotRealmAbilities.Archetype.Melee, 44, 20, Cost, AutonomousBotRealmAbilities.PvpFirst)
                .ContainsKey(AutonomousBotRealmAbilities.Mcl), Is.False);
        }
    }
}

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
    }
}

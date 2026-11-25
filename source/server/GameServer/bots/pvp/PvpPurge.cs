using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>What Purge would remove from one member of the group, and who that member is.</summary>
    /// <param name="SecondsLeft">Of the mez or stun; an effect about to end is not worth a 30-minute ability.</param>
    public readonly record struct PvpPurgeView(bool Mezzed, bool Stunned, bool Nearsighted, int SecondsLeft,
        PvpBuffArchetype Archetype, bool IsController);

    /// <summary>Part of <see cref="PvpWeights"/>: when Purge and Group Purge are worth their 30 minutes.</summary>
    public sealed class PvpPurgeWeights
    {
        public int Mezzed { get; set; } = 100;
        public int Stunned { get; set; } = 45;
        /// <summary>A nearsighted healer or caster; never alone enough with the default threshold.</summary>
        public int Nearsighted { get; set; } = 35;
        public int IsHealer { get; set; } = 30;
        /// <summary>One of the group's mezzers: awake, it turns the fight.</summary>
        public int IsController { get; set; } = 30;
        /// <summary>A mez or stun with less than this left is waited out.</summary>
        public int MinSecondsLeft { get; set; } = 6;
        /// <summary>A groupmate's Group Purge is about to free this member anyway.</summary>
        public int GroupPurgeComing { get; set; } = -60;
        public int MinScore { get; set; } = 90;

        /// <summary>Group Purge: for each member in reach that is mezzed, stunned, and each healer among them.</summary>
        public int GroupPerMezzed { get; set; } = 40;
        public int GroupPerStunned { get; set; } = 15;
        public int GroupPerHealerFreed { get; set; } = 20;
        public int GroupMinScore { get; set; } = 100;
        /// <summary>Group Purge reaches this far (the ability's own range).</summary>
        public int GroupRange { get; set; } = 1_500;

        /// <summary>
        /// The share of a group's members, in percent, that have Purge on their bar and press
        /// it; the others stay asleep until the mez runs out, they are hit or somebody cures
        /// them. Which members is settled by their names, so it is the same ones all evening.
        /// 100: everybody, which is every kind but the bad one (bots/pvp/PvpStyles.cs). Every
        /// bot buys Purge with its first realm points, and in the owner's fights of 2026-11-24
        /// the members of two bad groups of rank 2 pressed it 8 times in 70 seconds, 15 in two
        /// minutes and 23 in six: they were asleep 2 to 4% of the time they were alive, under
        /// two Bards. The owner, after an evening on the build with their slow hands: "the bad
        /// still seems too good to me".
        /// </summary>
        public int HeldByPercent { get; set; } = 100;
    }

    /// <summary>
    /// Purge by score (docs/PVP-8V8-ROADMAP.md, M6.3). The owner, 2026-11-17: at realm rank 1
    /// nobody has Purge, and in 1.65 a Druid has Group Purge, which is a huge difference. Bots
    /// used Purge only under conditions written for battlegrounds and never had Group Purge.
    /// </summary>
    public static class PvpPurge
    {
        public const string GroupPurgeKey = "AtlasOF_GroupPurge";

        /// <summary>What a member's own Purge is worth now; it is used at <see cref="PvpPurgeWeights.MinScore"/>.</summary>
        public static int Score(PvpPurgeView self, bool groupPurgeComing, PvpPurgeWeights weights)
        {
            bool soft = self.Archetype != PvpBuffArchetype.Melee;
            int score;
            if (self.Mezzed || self.Stunned)
            {
                if (self.SecondsLeft < weights.MinSecondsLeft)
                    return 0;
                score = self.Mezzed ? weights.Mezzed : weights.Stunned;
            }
            else if (self.Nearsighted && soft)
                score = weights.Nearsighted;
            else
                return 0;
            return score
                + (self.Archetype == PvpBuffArchetype.Healer ? weights.IsHealer : 0)
                + (self.IsController ? weights.IsController : 0)
                + (groupPurgeComing ? weights.GroupPurgeComing : 0);
        }

        /// <summary>What Group Purge is worth now, over the members in its reach (its owner among them).</summary>
        public static int GroupScore(IEnumerable<PvpPurgeView> members, PvpPurgeWeights weights) =>
            members.Sum(member =>
            {
                bool held = (member.Mezzed || member.Stunned) && member.SecondsLeft >= weights.MinSecondsLeft;
                if (!held)
                    return 0;
                return (member.Mezzed ? weights.GroupPerMezzed : weights.GroupPerStunned)
                    + (member.Archetype == PvpBuffArchetype.Healer ? weights.GroupPerHealerFreed : 0);
            });

        private static PvpPurgeView View(GameLiving member)
        {
            long now = GameLoop.GameLoopTime;
            long left = member.effectListComponent.GetEffects()
                .Where(effect => effect.EffectType is eEffect.Mez or eEffect.Stun)
                .Select(effect => effect.ExpireTick - now).DefaultIfEmpty(0).Max();
            eCharacterClass characterClass = member is GameBot { PvpBuild: { } build } ? build.Class
                : (eCharacterClass)((member as IGamePlayer)?.CharacterClass?.ID ?? 0);
            return new PvpPurgeView(member.IsMezzed, member.IsStunned,
                member.effectListComponent.ContainsEffectForEffectType(eEffect.Nearsight), (int)(left / 1000),
                PvpBuffPlan.ArchetypeOf(characterClass), PvpActionChoice.IsController(characterClass, PvpWeights.Of(member as GameBot)));
        }

        private static List<GameLiving> InReach(GameBot bot, int range) =>
            bot.Group?.GetMembersInTheGroup().Where(member => member.IsAlive && bot.IsWithinRadius(member, range)).ToList() ?? [bot];

        /// <summary>
        /// True when this 8v8 bot should fire the ability now. Only during a fight: outside one a
        /// mez wears off by itself and a groupmate cures it.
        /// </summary>
        /// <summary>Whether a member of that name is one of the share that has Purge on its bar (PvpPurgeWeights.HeldByPercent).</summary>
        public static bool Holds(string name, int percent)
        {
            if (percent >= 100)
                return true;
            int hash = 17;
            foreach (char letter in name ?? string.Empty)
                hash = unchecked(hash * 31 + letter);
            return (hash & 0x7FFFFFFF) % 100 < percent;
        }

        public static bool Wanted(GameBot bot, string key)
        {
            if (bot.Brain is not AI.Brain.BotBrain { PvpEnemies: { } enemies } ||
                !enemies.Any(enemy => enemy.IsAlive && bot.IsWithinRadius(enemy, PvpScrim.EngageRange)))
                return false;
            if (!Holds(bot.Name, PvpWeights.Of(bot).Purge.HeldByPercent))
                return false;
            // Its own Purge waits for its reaction time: a player sees the mez land before its hand is on the key.
            if (key != GroupPurgeKey && PvpNotice.ControlBegan(bot) is var began && began > 0 && !PvpNotice.Reacted(bot, bot, PvpEvent.OwnControl, began))
                return false;
            PvpPurgeWeights weights = PvpWeights.Of(bot).Purge;
            int score, needed;
            if (key == GroupPurgeKey)
            {
                score = GroupScore(InReach(bot, weights.GroupRange).Select(View), weights);
                needed = weights.GroupMinScore;
            }
            else
            {
                // A groupmate whose Group Purge is ready and worth using frees this member too.
                bool coming = bot.Group?.GetMembersInTheGroup().OfType<GameBot>().Any(mate =>
                    mate != bot && mate.IsAlive && mate.IsWithinRadius(bot, weights.GroupRange) &&
                    mate.GetAbility(GroupPurgeKey) is { } groupPurge && mate.GetSkillDisabledDuration(groupPurge) == 0 &&
                    GroupScore(InReach(mate, weights.GroupRange).Select(View), weights) >= weights.GroupMinScore) == true;
                score = Score(View(bot), coming, weights);
                needed = weights.MinScore;
            }
            if (score < needed)
                return false;
            PvpRecorder.Decision(bot, new { kind = key == GroupPurgeKey ? "GroupPurge" : "Purge", score }, [], null);
            return true;
        }
    }
}

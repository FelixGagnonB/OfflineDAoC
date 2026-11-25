using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>What a group knows about one enemy when it picks whom to kill.</summary>
    /// <param name="FromItsHealers">How far it stands from the middle of its own group's healers; 0 when none of them is known.</param>
    /// <param name="PetsOnOurBackline">How many of its pets are in melee on the group's healers and casters.</param>
    /// <param name="StaleSeconds">For the target being killed: how long it has been the target, beyond the grace time, without its health coming down.</param>
    /// <param name="Abandoned">It was the target a moment ago and was given up alive.</param>
    public readonly record struct PvpTargetView(int Id, PvpBuffArchetype Archetype, int HealthPercent, bool Mezzed, int Distance, int FromItsHealers = 0,
        int PetsOnOurBackline = 0, int StaleSeconds = 0, bool Abandoned = false);

    /// <summary>
    /// What the choice of a kill target weighs. None of it is a rule (the owner, 2026-11-18:
    /// "maybe sometimes a good target would be a melee while other times it might be a caster"):
    /// a batch that draws its weights at random moves these like all the others.
    /// </summary>
    public sealed class PvpKillWeights
    {
        /// <summary>
        /// False: the group has no assist. Its call is made and nobody takes it: each fighter
        /// goes for the nearest enemy it sees, and a caster for whom it likes. That is the bad
        /// group of a prime-time evening (bots/pvp/PvpStyles.cs), and what the recorded fights
        /// of the old brain were: each fighter on seven to nine targets in two and a half
        /// minutes, and nobody died.
        /// </summary>
        public bool Assists { get; set; } = true;
        public int OnHealer { get; set; } = 200;
        public int OnCaster { get; set; } = 150;
        public int OnFighter { get; set; } = 50;
        /// <summary>For each percent of health the enemy has lost.</summary>
        public double PerMissingHealthPercent { get; set; } = 1;
        /// <summary>Hitting a mezzed enemy wakes it.</summary>
        public int Mezzed { get; set; } = -120;
        /// <summary>
        /// For each hundred units from the middle of the group's damage dealers: running time, and
        /// the enemy's whole group to get through. It was -2.5 until 2026-11-18. In 150 matches
        /// Midgard and Hibernia then called an Albion Cleric before contact, from 3,200 units, and
        /// kept it as their target for 99 to 100% of the opening although it stood at the back,
        /// guarded and healed by the other Cleric; they lost 95 fights of 100. With -5 here and 5
        /// for being out of position, in 48 matches with the two scores on opposite teams, their
        /// target was a caster 54 to 75% of the time and the new score won 28 fights to 20.
        /// </summary>
        public double PerHundredUnits { get; set; } = -5;
        /// <summary>
        /// For each hundred units the enemy stands from its own healers, up to 2,000: out of
        /// position, it is harder to heal and easier to reach. The owner, 2026-11-17: the kill
        /// target is whoever is easiest to kill and out of position; a caster or healer that came
        /// too far forward is jumped.
        /// </summary>
        public double PerHundredUnitsFromItsHealers { get; set; } = 5;
        /// <summary>
        /// For each of the enemy's pets that is in melee on the group's healers and casters, up to
        /// four: whoever keeps the healers from healing is worth killing. Counted on 2026-11-19
        /// over eight matches of Albion against Midgard at rank 5: the Theurgist lived 164
        /// seconds a match, was being interrupted for 14% of them, called 21.6 pets, and its pets
        /// attacked Midgard's healers 100 times a match; Midgard's Savage and Berserker never had
        /// it as their target. With four pets out a Theurgist now outscores a healer.
        /// </summary>
        public int PerPetOnOurBackline { get; set; } = 25;
        /// <summary>
        /// A target that is not going down is given up sooner. For each second it has been the
        /// target beyond <see cref="StaleAfterSeconds"/> while its health stayed above
        /// <see cref="StaleIfHealthAbove"/>. Counted on 2026-11-19 over 48 matches at rank 5: of
        /// 467 kill calls 68% ended with the target dead, after a median of 9 seconds, and 31%
        /// were given up with the target alive, after a median of 27 seconds and at a median of
        /// 94% health; most were a Druid, Cleric or Healer that was healed, guarded or ran. The
        /// owner the same day: it is "the right timing to collapse on a target".
        ///
        /// Off (0; tried at -4 with -80 for a target just given up): in 24 matches a call was
        /// given up after 21 seconds against 25, there were 12 calls a match against 9 for the
        /// same number of kills, and a fighter running at the healers was stopped within three
        /// seconds 31% of the time against 45%. Giving up a little sooner and calling more often
        /// is not the cure; the question is whom to call, and when.
        /// </summary>
        public double PerStaleSecond { get; set; } = 0;
        public int StaleAfterSeconds { get; set; } = 10;
        public int StaleIfHealthAbove { get; set; } = 80;
        /// <summary>A target given up alive is not called again for <see cref="AbandonedForSeconds"/>. Off with the above.</summary>
        public int RecentlyAbandoned { get; set; } = 0;
        public int AbandonedForSeconds { get; set; } = 20;

        /// <summary>
        /// For this many seconds after the group calls a new kill target, every member whose job
        /// is damage leaves its interrupt duty and whoever it had turned on, and goes for the
        /// target: the group collapses on it. 0: each keeps its job, and the call is for whoever
        /// is free (which, counted on 2026-11-20, is often one member of eight).
        /// </summary>
        public int CollapseOnCallSeconds { get; set; } = 0;

        public bool Collapses(long now, long calledAt) => CollapseOnCallSeconds > 0 && now - calledAt < CollapseOnCallSeconds * 1_000L;
        /// <summary>The current target is kept unless another scores this much higher.</summary>
        public int SwitchMargin { get; set; } = 60;
        /// <summary>Enemies farther than this from the damage dealers are not called.</summary>
        public int MaxDistance { get; set; } = 3_500;
    }

    /// <summary>
    /// The group's kill target (docs/PVP-8V8-ROADMAP.md, M5.5): one enemy that every damage
    /// dealer attacks until it is dead. The recorded old-brain fights had each fighter on seven to
    /// nine targets in two and a half minutes and nobody died.
    /// </summary>
    public static class PvpKillTarget
    {
        private static readonly PvpKillWeights Defaults = new();
        private const int ExposureCap = 2_000;
        private const int PetCap = 4;

        /// <summary>
        /// Higher is a better kill. Healers first, then casters: they are the softest and matter
        /// most. A wounded enemy is closer to dead. A mezzed one is left alone while there is
        /// anyone else, because hitting it wakes it. Far ones cost running time.
        /// </summary>
        public static int Score(PvpTargetView enemy, PvpKillWeights weights = null)
        {
            weights ??= Defaults;
            return (enemy.Archetype switch { PvpBuffArchetype.Healer => weights.OnHealer, PvpBuffArchetype.Caster => weights.OnCaster, _ => weights.OnFighter })
                + (int)(weights.PerMissingHealthPercent * (100 - enemy.HealthPercent))
                + (enemy.Mezzed ? weights.Mezzed : 0)
                + (int)(weights.PerHundredUnits * enemy.Distance / 100.0)
                + (int)(weights.PerHundredUnitsFromItsHealers * Math.Min(enemy.FromItsHealers, ExposureCap) / 100.0)
                + weights.PerPetOnOurBackline * Math.Min(enemy.PetsOnOurBackline, PetCap)
                + (int)(weights.PerStaleSecond * enemy.StaleSeconds)
                + (enemy.Abandoned ? weights.RecentlyAbandoned : 0);
        }

        /// <summary>
        /// How long a target has gone stale: the seconds it has been the target beyond the grace
        /// time, counted only while its health is above the mark.
        /// </summary>
        public static int StaleSeconds(double secondsOnTarget, int healthPercent, PvpKillWeights weights) =>
            healthPercent <= weights.StaleIfHealthAbove ? 0 : (int)Math.Max(0, secondsOnTarget - weights.StaleAfterSeconds);

        /// <summary>
        /// A damage dealer of a group with no assist (a bad group, PvpKillWeights.Assists) takes
        /// the nearest enemy when it has no target of its own: one that is awake before one that
        /// is asleep. Its target was whoever had made it angry, and nobody else: the owner,
        /// 2026-11-25, "we were 5 out of 5 mezzed. The mid team just stayed idle waiting for mezz
        /// to wear off instead of picking a target and killing it". In that fight a bad Midgard
        /// group, eight up and none asleep, stood ten seconds before six sleeping enemies and
        /// five seconds before three, with no target and no blow.
        /// </summary>
        public static bool TakesTheNearest(bool itsGroupAssists, bool hasATargetOfItsOwn) => !itsGroupAssists && !hasATargetOfItsOwn;

        /// <summary>Which of the enemies it then takes (-1: none): the one it took before while that lives, else the nearest awake, else the nearest.</summary>
        public static int Nearest(IReadOnlyList<(int Distance, bool Asleep)> enemies, int takenBefore, int maxDistance)
        {
            if (takenBefore >= 0 && takenBefore < enemies.Count)
                return takenBefore;
            int best = -1;
            for (int index = 0; index < enemies.Count; index++)
            {
                if (enemies[index].Distance > maxDistance)
                    continue;
                if (best < 0 || (enemies[index].Asleep, enemies[index].Distance).CompareTo((enemies[best].Asleep, enemies[best].Distance)) < 0)
                    best = index;
            }
            return best;
        }

        /// <summary>The enemy to kill now, given the one being killed already (-1 for none).</summary>
        public static int Choose(IReadOnlyList<PvpTargetView> enemies, int currentId, PvpKillWeights weights = null)
        {
            weights ??= Defaults;
            List<PvpTargetView> reachable = enemies.Where(enemy => enemy.Distance <= weights.MaxDistance).ToList();
            if (reachable.Count == 0)
                return -1;
            PvpTargetView best = reachable.OrderByDescending(enemy => Score(enemy, weights)).ThenBy(enemy => enemy.Id).First();
            int current = reachable.FindIndex(enemy => enemy.Id == currentId);
            return current >= 0 && Score(best, weights) < Score(reachable[current], weights) + weights.SwitchMargin ? currentId : best.Id;
        }
    }
}

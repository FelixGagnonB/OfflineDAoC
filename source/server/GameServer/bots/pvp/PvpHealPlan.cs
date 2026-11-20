using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>A member of the group a healer could heal.</summary>
    /// <param name="Incoming">What heals already being cast on it by the group's other healers will restore.</param>
    /// <param name="Trained">The enemy's fighters are on this member right now.</param>
    public readonly record struct PvpPatient(int Id, int Health, int MaxHealth, int Incoming, PvpBuffArchetype Archetype, bool Trained = false);

    /// <summary>Part of <see cref="PvpWeights"/>: how the group's healers share out their patients.</summary>
    public sealed class PvpHealWeights
    {
        /// <summary>
        /// Power has to last the fight (the owner, 2026-11-17). A healer below this share of its
        /// power saves it: it heals a member only below <see cref="SavingHealBelowPercent"/>, and
        /// the member the enemy is training only below <see cref="SavingTrainedHealBelowPercent"/>,
        /// in place of the usual marks. In the recorded matches of 2026-11-18 a Rejuvenation Cleric
        /// spent 29% of the fight under a fifth of its power. 0 = it never saves.
        /// </summary>
        public int SavePowerBelowPercent { get; set; } = 30;

        /// <summary>
        /// During a fight a healer walks to a dead member of its group that lies within this
        /// distance, to raise it; 0 = it raises only a corpse that is in reach already. The owner,
        /// 2026-11-19: "2 teams of equal skill could take 5-10 minutes. I've seen longer fights."
        /// Here a fight lasted two and a half minutes and the dead stayed dead: in 23 matches at
        /// rank 5 there were 8.5 deaths a match and 0.2 resurrections. Of 137 deaths with at
        /// least fifteen seconds of fight left, 41 had a healer alive and awake that was simply
        /// too far from the corpse (2,900 units at the median), and nothing walked a healer to a
        /// corpse while its group was in combat.
        ///
        /// Off (it was tried at 2,500 the same day): in 24 matches a healer set off for a corpse
        /// 3.0 times a match and the resurrections finished went from 0.2 to 0.4, while the two
        /// back lines stood 300 units nearer each other and a group's healers were within 800
        /// units of each other 17% of the time against 12%: both healers walked to the same
        /// corpse, toward the enemy, and seldom got the spell off. It wants one healer sent, not
        /// both, and a look at what stops the cast on arrival, before it is switched on again.
        /// </summary>
        public int ResurrectWalkWithin { get; set; } = 0;
        /// <summary>It does not go while a free enemy fighter is this near the corpse.</summary>
        public int ResurrectNoFighterWithin { get; set; } = 600;
        /// <summary>Nor while a living member in its reach is below this health: the living come first.</summary>
        public int ResurrectIfNobodyBelow { get; set; } = 50;

        /// <summary>
        /// Raising the dead in a fight, from where the bot stands. Until 2026-11-19 an 8v8 bot
        /// asked the rule written for raids, which lets a healer raise in combat only while a
        /// second free healer stands within the spell's reach of it (1,500 units): in 21 recorded
        /// matches, 183 deaths, no Cleric, Druid, Healer, Shaman or Bard began a resurrection once.
        /// Only the Paladin did (33 times, 5 finished: it is no healer, so the rule let it, in the
        /// middle of the melee). In 43 of those deaths a member who can raise stood within reach
        /// of the body, not interrupted and no enemy within 500 units, 34 of them with half their
        /// power or more. Now: within reach, not being hit, with the power, nobody alive in its
        /// reach badly hurt, no free enemy fighter at the body, and one healer at a time.
        /// </summary>
        public bool ResurrectInFight { get; set; } = true;
        /// <summary>The share of its power a member must have to raise (the spell takes a quarter).</summary>
        public int ResurrectPowerPercent { get; set; } = 50;

        /// <summary>
        /// A member raised in a fight has none of its buffs, and once a fight has begun nobody
        /// begins a buff (PvpWeights.BuffsInFight). The owner, 2026-11-20, raised in a fight they
        /// then played on in: "I just never received buffs after rez but I assume druids were not
        /// finding downtime to buff me". There was no downtime to find: the rebuff was held back
        /// with every other buff. On: the group plan's buffs that a member lacks are cast in a
        /// fight too, by the member whose buff it is, when it is not being hit, has the power, and
        /// nobody alive in its reach is below <see cref="RebuffIfNobodyBelow"/>.
        /// </summary>
        public bool RebuffInFight { get; set; } = true;
        /// <summary>Higher than for a raise: a buff is worth less than a member standing.</summary>
        public int RebuffIfNobodyBelow { get; set; } = 70;
        public int RebuffPowerPercent { get; set; } = 30;

        /// <summary>True for a member that may begin a rebuff in a fight now.</summary>
        public bool RebuffsInFight(int powerPercent, bool beingHit, int lowestHealthInReach) =>
            RebuffInFight && !beingHit && powerPercent >= RebuffPowerPercent && lowestHealthInReach >= RebuffIfNobodyBelow;

        /// <summary>True for a member that should raise this body now, from where it stands.</summary>
        public bool Raises(int distance, int range, int powerPercent, bool beingHit, int lowestHealthInReach, int freeFightersAtCorpse,
            bool anotherHealerIsRaising) =>
            ResurrectInFight && distance <= range && powerPercent >= ResurrectPowerPercent && !beingHit && !anotherHealerIsRaising &&
            lowestHealthInReach >= ResurrectIfNobodyBelow && freeFightersAtCorpse == 0;

        /// <summary>True for a healer that should walk to a corpse now, to raise it.</summary>
        public bool WalksToRaise(int distance, int range, bool hasPower, bool beingHit, int lowestHealthInReach, int freeFightersAtCorpse) =>
            ResurrectWalkWithin > 0 && distance > range && distance <= ResurrectWalkWithin && hasPower && !beingHit &&
            lowestHealthInReach >= ResurrectIfNobodyBelow && freeFightersAtCorpse == 0;
        public int SavingHealBelowPercent { get; set; } = 60;
        public int SavingTrainedHealBelowPercent { get; set; } = 85;
        /// <summary>
        /// A heal with a cast time is dropped when its target is at or above this health before it
        /// lands: somebody else healed it meanwhile. It is seldom needed (6 of 157 big heals in a check
        /// run of 2026-11-18; most heals that restore nothing are the instant group heal reaching
        /// members who were not hurt). Not for the member the enemy is training: damage is on its
        /// way to it. 0 = never dropped.
        /// </summary>
        public int DropHealAtPercent { get; set; } = 98;
        /// <summary>
        /// A backup healer (Pacification Healer, Shaman, Bard) heals first only while a member is
        /// below its own mark (<see cref="PvpWeights.HealsFirstBelow"/>). While none of the group's
        /// healers that only heal can heal (dead, asleep, stunned, silenced or being interrupted),
        /// its mark rises to this: it takes over. Playbook, section 5: "When the Mending Healer is
        /// asleep, interrupted or dead, it turns to healing".
        /// </summary>
        public int BackupHealsBelowWhenHealersDown { get; set; } = 95;
        /// <summary>A spread or group heal is cast when at least this many members in reach are hurt. 0 = never.</summary>
        public int SpreadHealMinHurt { get; set; } = 3;
        /// <summary>A member counts as hurt, for the spread heal, below this health.</summary>
        public int SpreadHealBelowPercent { get; set; } = 80;
        /// <summary>A member is a patient while its health, with the heals already on their way, is below this percent.</summary>
        public int HealBelowPercent { get; set; } = 80;
        /// <summary>A healer or caster is healed as if it were this many percent lower: it dies faster and matters more.</summary>
        public int PatientIsHealer { get; set; } = 8;
        public int PatientIsCaster { get; set; } = 4;
        /// <summary>The patient the healer had picked by itself keeps the heal unless another is worse off by more than this.</summary>
        public int KeepOwnChoice { get; set; } = 5;

        /// <summary>
        /// The owner, who played healers, 2026-11-17: the hard part was knowing whether someone was
        /// being trained by the enemy's assist train; once that was called, all focus went to
        /// healing that one until a peel, mez or stun let it get away. A member counts as trained
        /// with this many enemies on it.
        /// </summary>
        public int TrainAttackers { get; set; } = 2;
        /// <summary>
        /// Who counts as "on it": with this on, every enemy that hit the member in the last few
        /// seconds, a caster from range as much as a fighter, and an enemy whose pet hit it. Off,
        /// only enemy fighters in melee on it, as before. In the 150 matches of 2026-11-18 the
        /// Bard was Albion's kill target for 63% of its life and its own group saw it trained for
        /// 9%: the train on it was one fighter, the casters and the pets (1,090 damage a match
        /// from fighters, 669 from casters, 679 from pets).
        /// </summary>
        public bool TrainCountsEveryAttacker { get; set; } = true;
        /// <summary>
        /// The "anytime" heal restores at least this percent of what the big heal does. The owner
        /// healed with two spells, the spec line's Major heal and its Greater heal; the bots'
        /// second heal was the base line's Minor heal (119 health where the Major gives 387),
        /// picked for costing the least power per point healed.
        /// </summary>
        public int AnytimeHealPercentOfBig { get; set; } = 50;
        /// <summary>
        /// Cure nearsight (owner, 2026-11-17: if the healers have it, it belongs on their list). It is
        /// cast on a nearsighted caster or healer of the group once nobody is below 65% health and
        /// nobody is asleep, and only a version that casts within this many milliseconds: the first one
        /// takes six seconds, the second three and a half, and neither is shortened by dexterity.
        /// 0 turns it off, and it is off: measured overnight over 192 matches, with it at 4,000
        /// Albion won 45% of its matches against 65% without and Hibernia 48% against 62% (Midgard
        /// 39% either way). The main Cleric cast it four times a match, 14 seconds not spent healing.
        /// </summary>
        public int CureNearsightMaxCastTime { get; set; } = 0;
        /// <summary>
        /// A healer built to heal leaves waking a mezzed member to the group's control healer, as
        /// long as that one is able to act, and wakes the control healer itself. The owner,
        /// 2026-11-17: in Midgard the Pacification Healer keeps interrupt, mez, demez and stun
        /// ahead of heals while knowing when to heal, and for the Mending Healer it is the other
        /// way round. In the recordings the Mending Healer cast 8 to 13 cure mezzes a match and 2
        /// to 3 heals.
        /// </summary>
        public bool HealerLeavesCureMezToController { get; set; } = true;
        /// <summary>A trained member is healed as soon as it is below this percent, not only once it is hurt.</summary>
        public int TrainedHealBelowPercent { get; set; } = 97;
        /// <summary>And it is healed as if it were this many percent lower than it is.</summary>
        public int PatientIsTrained { get; set; } = 25;
    }

    /// <summary>
    /// Heal assignment across a group's healers (docs/PVP-8V8-ROADMAP.md, M5.6), on top of each
    /// healer's own triage: a heal already on its way to a member counts as landed, so the second
    /// healer goes to whoever is then worst off, and nobody gets two heals while another dies.
    /// </summary>
    public static class PvpHealPlan
    {
        /// <summary>True when a heal being cast is no longer worth finishing.</summary>
        public static bool DropsHeal(int targetHealthPercent, bool targetTrained, PvpHealWeights weights) =>
            weights.DropHealAtPercent > 0 && !targetTrained && targetHealthPercent >= weights.DropHealAtPercent;

        /// <summary>The health mark below which a backup healer heals before anything else, given whether a healer of its group can heal.</summary>
        public static int BackupHealsBelow(int ownMark, bool aHealerCanHeal, PvpHealWeights weights) =>
            aHealerCanHeal ? ownMark : Math.Max(ownMark, weights.BackupHealsBelowWhenHealersDown);

        /// <summary>
        /// True when a heal for several is the heal to cast: enough members in reach are hurt at
        /// once, and nobody has the enemy's assist train on it (that member gets the single heals).
        /// </summary>
        public static bool SeveralHurt(IEnumerable<int> healthPercents, bool someoneTrained, PvpHealWeights weights) =>
            weights.SpreadHealMinHurt > 0 && !someoneTrained &&
            healthPercents.Count(percent => percent < weights.SpreadHealBelowPercent) >= weights.SpreadHealMinHurt;

        /// <summary>
        /// Which heal for several to cast: the one that restores the most health for each point
        /// of power, given how much each member in reach is missing (the owner, 2026-11-18: "I
        /// would use the one with the best power to heal ratio"). A group heal gives each member
        /// up to its value; a spread heal shares a pool of its value times the group's size among
        /// the most hurt, up to twice its value each. Returns an index into
        /// <paramref name="heals"/>, or -1 when none would restore anything.
        /// </summary>
        public static int BestAreaHeal(IReadOnlyList<int> missing, IReadOnlyList<(bool Spread, int Value, int Power)> heals, int groupSize)
        {
            int best = -1;
            double bestRatio = 0;
            for (int i = 0; i < heals.Count; i++)
            {
                (bool spread, int value, int power) = heals[i];
                double restored = spread
                    ? Math.Min(missing.Sum(health => Math.Min(health, 2 * value)), (double)value * groupSize)
                    : missing.Sum(health => Math.Min(health, value));
                double ratio = restored / Math.Max(1, power);
                if (ratio > bestRatio)
                    (best, bestRatio) = (i, ratio);
            }
            return best;
        }

        /// <summary>Health percent with the heals already on their way, as low as zero and no higher than 100.</summary>
        public static double Projected(PvpPatient patient) =>
            Math.Clamp(100.0 * (patient.Health + Math.Max(0, patient.Incoming)) / Math.Max(1, patient.MaxHealth), 0, 100);

        /// <summary>How much a patient needs this heal: higher is needier.</summary>
        public static double Need(PvpPatient patient, bool own, PvpHealWeights weights) =>
            100 - Projected(patient)
            + (patient.Archetype switch { PvpBuffArchetype.Healer => weights.PatientIsHealer, PvpBuffArchetype.Caster => weights.PatientIsCaster, _ => 0 })
            + (patient.Trained ? weights.PatientIsTrained : 0)
            + (own ? weights.KeepOwnChoice : 0);

        /// <summary>
        /// A healer's two single heals with a cast time, from what it knows (amount healed and power
        /// cost of each): the big one is the one that heals most; the anytime one is the cheapest
        /// per point healed among those of real size. With one heal both are that heal; -1 with none.
        /// </summary>
        public static (int Anytime, int Big) PickHeals(IReadOnlyList<(double Amount, int Power)> heals, PvpHealWeights weights)
        {
            if (heals.Count == 0)
                return (-1, -1);
            int big = 0;
            for (int id = 1; id < heals.Count; id++)
            {
                if (heals[id].Amount > heals[big].Amount)
                    big = id;
            }
            int anytime = big;
            for (int id = 0; id < heals.Count; id++)
            {
                if (heals[id].Amount * 100 < heals[big].Amount * weights.AnytimeHealPercentOfBig)
                    continue;
                if (heals[id].Amount / Math.Max(1, heals[id].Power) > heals[anytime].Amount / Math.Max(1, heals[anytime].Power))
                    anytime = id;
            }
            return (anytime, big);
        }

        /// <summary>
        /// The member the enemy's fighters are on: the one with the most in melee on it, when that
        /// is at least <see cref="PvpHealWeights.TrainAttackers"/>; the one already called keeps
        /// the call on a tie. -1 for nobody.
        /// </summary>
        public static int Trained(IReadOnlyList<int> attackersByMember, int current, PvpHealWeights weights)
        {
            int best = -1;
            for (int id = 0; id < attackersByMember.Count; id++)
            {
                if (attackersByMember[id] < weights.TrainAttackers)
                    continue;
                if (best < 0 || attackersByMember[id] > attackersByMember[best] ||
                    attackersByMember[id] == attackersByMember[best] && id == current)
                    best = id;
            }
            return best;
        }

        /// <summary>
        /// Whom this heal goes to: <paramref name="own"/> (the healer's own pick) or a needier
        /// member. -1 when the others' heals already cover everyone, so the turn is free for
        /// something else.
        /// </summary>
        /// <param name="healerPowerPercent">The healer's own power: one that is running low heals only members who are really hurt.</param>
        public static int Choose(IReadOnlyList<PvpPatient> patients, int own, PvpHealWeights weights, int healerPowerPercent = 100)
        {
            bool saving = healerPowerPercent < weights.SavePowerBelowPercent;
            int mark = saving ? Math.Min(weights.HealBelowPercent, weights.SavingHealBelowPercent) : weights.HealBelowPercent;
            int trainedMark = saving ? Math.Min(weights.TrainedHealBelowPercent, weights.SavingTrainedHealBelowPercent) : weights.TrainedHealBelowPercent;
            List<PvpPatient> needy = patients.Where(patient => Projected(patient) < (patient.Trained ? trainedMark : mark)).ToList();
            return needy.Count == 0 ? -1
                : needy.OrderByDescending(patient => Need(patient, patient.Id == own, weights)).ThenBy(patient => patient.Id).First().Id;
        }
    }
}

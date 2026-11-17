using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>A member of the group a healer could heal.</summary>
    /// <param name="Incoming">What heals already being cast on it by the group's other healers will restore.</param>
    public readonly record struct PvpPatient(int Id, int Health, int MaxHealth, int Incoming, PvpBuffArchetype Archetype);

    /// <summary>Part of <see cref="PvpWeights"/>: how the group's healers share out their patients.</summary>
    public sealed class PvpHealWeights
    {
        /// <summary>A member is a patient while its health, with the heals already on their way, is below this percent.</summary>
        public int HealBelowPercent { get; set; } = 80;
        /// <summary>A healer or caster is healed as if it were this many percent lower: it dies faster and matters more.</summary>
        public int PatientIsHealer { get; set; } = 8;
        public int PatientIsCaster { get; set; } = 4;
        /// <summary>The patient the healer had picked by itself keeps the heal unless another is worse off by more than this.</summary>
        public int KeepOwnChoice { get; set; } = 5;
    }

    /// <summary>
    /// Heal assignment across a group's healers (docs/PVP-8V8-ROADMAP.md, M5.6), on top of each
    /// healer's own triage: a heal already on its way to a member counts as landed, so the second
    /// healer goes to whoever is then worst off, and nobody gets two heals while another dies.
    /// </summary>
    public static class PvpHealPlan
    {
        /// <summary>Health percent with the heals already on their way, as low as zero and no higher than 100.</summary>
        public static double Projected(PvpPatient patient) =>
            Math.Clamp(100.0 * (patient.Health + Math.Max(0, patient.Incoming)) / Math.Max(1, patient.MaxHealth), 0, 100);

        /// <summary>How much a patient needs this heal: higher is needier.</summary>
        public static double Need(PvpPatient patient, bool own, PvpHealWeights weights) =>
            100 - Projected(patient)
            + (patient.Archetype switch { PvpBuffArchetype.Healer => weights.PatientIsHealer, PvpBuffArchetype.Caster => weights.PatientIsCaster, _ => 0 })
            + (own ? weights.KeepOwnChoice : 0);

        /// <summary>
        /// Whom this heal goes to: <paramref name="own"/> (the healer's own pick) or a needier
        /// member. -1 when the others' heals already cover everyone, so the turn is free for
        /// something else.
        /// </summary>
        public static int Choose(IReadOnlyList<PvpPatient> patients, int own, PvpHealWeights weights)
        {
            List<PvpPatient> needy = patients.Where(patient => Projected(patient) < weights.HealBelowPercent).ToList();
            return needy.Count == 0 ? -1
                : needy.OrderByDescending(patient => Need(patient, patient.Id == own, weights)).ThenBy(patient => patient.Id).First().Id;
        }
    }
}

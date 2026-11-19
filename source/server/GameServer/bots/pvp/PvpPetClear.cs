namespace DOL.GS
{
    /// <summary>
    /// What a group does about enemy pets (docs/PVP-8V8-ROADMAP.md). Part of <see cref="PvpWeights"/>.
    /// </summary>
    public sealed class PvpPetWeights
    {
        /// <summary>Enemy pets on the group's healers and casters before they gather; 0 = they never do.</summary>
        public int GatherAtPets { get; set; } = 3;
        /// <summary>Pets inside the spell before it is cast; 0 = it never is.</summary>
        public int BlastAtPets { get; set; } = 2;
        /// <summary>How close to the caster a member comes to stand. The spells reach 300 units.</summary>
        public int GatherWithin { get; set; } = 150;
        /// <summary>A member farther from the caster than this does not come.</summary>
        public int GatherFromWithin { get; set; } = 1_800;
        /// <summary>A caster with a free enemy fighter this near is not gathered on.</summary>
        public int NoGatherWithFighterWithin { get; set; } = 400;
        /// <summary>Pets farther than this from the caster are not counted.</summary>
        public int ConsiderRange { get; set; } = 2_000;
    }

    /// <summary>
    /// The owner, 2026-11-19, watching Hibernia against Albion: "Hibernia doesn't seem to know how
    /// to handle the Theurgist and clean pets. One of the things we were doing, as Hibernia against
    /// pets, was to stack on the Eldritch and it would PBAoE the pets (using quick cast if
    /// needed)". In the 16 recorded matches before, the Theurgist's pets attacked Hibernia 63 times
    /// a match, 28 of them the Druids and 12 the Bard, each one an interruption, and the only
    /// answers were a Druid's staff and its root.
    ///
    /// So: the member with a point-blank area damage spell is where the others come. A healer or
    /// caster with a pet on it walks to stand beside that member, the pet follows, and the spell
    /// is cast once enough pets are inside it. Quick cast is not part of this yet: bots do not
    /// have it, so a caster that pets are hitting cannot get the spell off.
    /// </summary>
    public static class PvpPetClear
    {
        /// <summary>True when the group starts to gather: this many pets are on its healers and casters.</summary>
        public static bool Gathers(int petsOnBackline, PvpPetWeights weights) =>
            weights.GatherAtPets > 0 && petsOnBackline >= weights.GatherAtPets;

        /// <summary>Once gathered the group stays so until no pet is left on its healers and casters.</summary>
        public static bool StaysGathered(int petsOnBackline) => petsOnBackline > 0;

        /// <summary>True for a healer or caster that walks to the caster: a pet is on it, and it is neither there already nor too far.</summary>
        public static bool Comes(int petsOnIt, int fromCaster, PvpPetWeights weights) =>
            petsOnIt > 0 && fromCaster > weights.GatherWithin && fromCaster <= weights.GatherFromWithin;

        /// <summary>
        /// What an area mez aimed at a pet counts as catching: every enemy in it, and one more
        /// for every two pets. Counted on 2026-11-19 over 24 matches at rank 5: Albion's pets
        /// attacked Midgard's three healers 144 times a match (the Theurgist's 100, the
        /// Cabalist's 44), pets were the last thing to hit a healer in 41 of the 68 seconds its
        /// healers spent interrupted and not casting before a group's first death, and Midgard,
        /// which has no point-blank spell, did nothing about them. A pet has no Purge.
        /// </summary>
        public static int MezCount(int enemies, int pets) => enemies + pets / 2;

        /// <summary>True when the spell is worth casting: enough pets inside it, and no sleeping enemy that it would wake.</summary>
        public static bool Blasts(int petsInside, bool wouldWakeAnEnemy, PvpPetWeights weights) =>
            weights.BlastAtPets > 0 && petsInside >= weights.BlastAtPets && !wouldWakeAnEnemy;
    }
}

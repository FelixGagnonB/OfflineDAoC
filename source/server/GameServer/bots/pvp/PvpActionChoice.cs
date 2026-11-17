using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DOL.GS
{
    /// <summary>What a caster can spend its turn on in an 8v8 fight.</summary>
    public enum PvpActionKind
    {
        Nearsight,
        Disease,
        /// <summary>Direct damage or a lifedrain.</summary>
        Damage,
        /// <summary>A root or snare. It is cast even on a target that already has it or is immune: the cast still interrupts.</summary>
        Root,
        /// <summary>A Theurgist pet sent at the target.</summary>
        Pet,
        /// <summary>A mez on one enemy.</summary>
        Mez,
        /// <summary>A mez on everyone around the target, or around the caster.</summary>
        MezArea,
        Stun,
        /// <summary>Waking a mezzed member of the caster's own group. The target is a friend, not an enemy.</summary>
        CureMez,
        /// <summary>Makes every enemy around the target lose the spell it is casting (Bard's Lullaby, Healer, Sorcerer).</summary>
        Amnesia,
    }

    /// <summary>One thing the caster could do now: this kind of spell on this enemy, and what is known about the enemy.</summary>
    public readonly record struct PvpOption(
        PvpActionKind Kind,
        int TargetId,
        PvpBuffArchetype Archetype,
        bool IsKill,          // the group's called kill target
        bool IsAssigned,      // the enemy this bot was told to keep interrupted
        int HealthPercent,
        bool HasEffect,       // already nearsighted, diseased or rooted
        bool Casting,
        bool Interrupted,     // cannot cast right now: it was hit or had a spell cast on it moments ago
        bool Mezzed,
        int PowerPercent,     // the caster's own power
        int CostPercent = 0,  // what this cast takes, as a share of the caster's whole pool
        int Short = 0,        // how far the caster must still walk before it can cast this; 0 = castable now
        int Caught = 1,       // area mez: how many awake enemies it puts to sleep, the kill target not counted
        bool CatchesKill = false, // area mez: the kill target is inside it
        int Wakes = 0,        // amnesia: mezzed enemies inside it, the kill target not counted; it wakes them
        bool Instant = false, // an instant spell: it lands now and cannot be interrupted, but mezzes and stuns of this sort come back after ten minutes
        bool OnController = false); // the target is one of the enemy's mezzers

    /// <summary>
    /// The numbers behind the choice. Every one is a weight added to an option's score, so none is
    /// a rule by itself; the recorder writes each decision with its scores, and these are what a
    /// batch of recorded matches is used to tune (docs/PVP-8V8-ROADMAP.md, M5.7 and M6.4).
    /// A file <c>pvp-weights.json</c> beside the server replaces any of them without a rebuild.
    /// </summary>
    public sealed class PvpWeights
    {
        public const string FileName = "pvp-weights.json";

        /// <summary>Nothing scoring lower than this is cast; the bot does something else with its turn.</summary>
        public int MinScore { get; set; } = 50;

        /// <summary>How much a class leans to each kind of action, before the situation is looked at.</summary>
        public Dictionary<string, Dictionary<string, int>> ClassBase { get; set; } = new()
        {
            // The owner's tendencies of 2026-11-16: a Shaman spams disease and roots to interrupt;
            // an Eldritch nearsights casters, then diseases, then nukes the called target; a
            // Theurgist keeps pets on enemy healers and casters; a Sorcerer controls and interrupts
            // and joins the kill only when it is close.
            ["Shaman"] = new() { ["Disease"] = 90, ["Root"] = 40 },
            ["Eldritch"] = new() { ["Nearsight"] = 80, ["Disease"] = 70, ["Damage"] = 55 },
            ["Cabalist"] = new() { ["Nearsight"] = 60, ["Disease"] = 70, ["Damage"] = 55 },
            ["Runemaster"] = new() { ["Nearsight"] = 75, ["Damage"] = 60 },
            ["Theurgist"] = new() { ["Pet"] = 70, ["Damage"] = 30, ["Root"] = 30 },
            // Main mezzers (owner, 2026-11-16): Sorcerer, Bard, Pacification Healer. The Healer's entry
            // serves both its specs: the Mending Healer simply has fewer of the spells.
            // Cure mez (owner, 2026-11-16): Midgard's two Healers, Albion's Sorcerer and Minstrel. A class
            // without the spell simply never has the option.
            // Amnesia (owner, 2026-11-17): the Bard's is instant with a ten-second recast, the other two take a two-second cast.
            ["Sorcerer"] = new() { ["Mez"] = 120, ["MezArea"] = 120, ["CureMez"] = 130, ["Damage"] = -10, ["Root"] = 35, ["Amnesia"] = 45 },
            ["Bard"] = new() { ["Mez"] = 120, ["MezArea"] = 120, ["CureMez"] = 130, ["Damage"] = 40, ["Amnesia"] = 60 },
            ["Healer"] = new() { ["Mez"] = 110, ["MezArea"] = 110, ["CureMez"] = 140, ["Stun"] = 70, ["Amnesia"] = 45 },
            ["Enchanter"] = new() { ["Damage"] = 60 },
            ["Wizard"] = new() { ["Damage"] = 60 },
            ["Mentalist"] = new() { ["Damage"] = 60 },
            ["Spiritmaster"] = new() { ["Damage"] = 60 },
        };

        public int NearsightOnCaster { get; set; } = 30;
        public int NearsightOnHealer { get; set; } = 15;
        public int DiseaseOnKillTarget { get; set; } = 40;     // disease halves the healing it receives
        public int DamageOnKillTarget { get; set; } = 30;
        public int DamageOffKillTarget { get; set; } = -60;    // spread damage kills nobody
        public double DamagePerMissingHealth { get; set; } = 0.5;
        public int PetOnCasterOrHealer { get; set; } = 30;
        public int PetOnMelee { get; set; } = -50;
        public int OnAssignedTarget { get; set; } = 25;
        /// <summary>The target is a healer or caster in the middle of a cast that nothing is stopping.</summary>
        public int InterruptsACast { get; set; } = 30;
        /// <summary>For each enemy a mez puts to sleep (one for a single mez).</summary>
        public int MezPerEnemyCaught { get; set; } = 30;
        public int MezOnHealer { get; set; } = 15;
        /// <summary>An area mez that also lands on the kill target: the group's own damage wakes it and it comes out immune.</summary>
        public int MezCatchesKillTarget { get; set; } = -60;
        public int StunOnKillTarget { get; set; } = 40;
        /// <summary>
        /// A mez or stun on one of the enemy's own mezzers: asleep, it mezzes nobody. The owner, 2026-11-17:
        /// a Healer has a hard time against a Sorcerer's mez range, so it goes in and puts its instant mez on the Sorcerer.
        /// </summary>
        public int ControlOnEnemyController { get; set; } = 40;
        /// <summary>An instant mez or stun on a target that is casting: it lands before that cast does, a spell with a cast time would not.</summary>
        public int InstantOnCastingTarget { get; set; } = 40;
        /// <summary>An instant mez or stun is gone for ten minutes once used, so the cast-time spell is taken when nothing presses.</summary>
        public int InstantSpent { get; set; } = -10;
        /// <summary>For each enemy inside an amnesia that is casting right now: it loses that spell.</summary>
        public int AmnesiaPerCastStopped { get; set; } = 45;
        /// <summary>For each mezzed enemy inside it: amnesia breaks mez.</summary>
        public int AmnesiaPerSleeperWoken { get; set; } = -80;
        /// <summary>Waking a healer is worth the most: it then heals and, in Midgard, cures the others.</summary>
        public int CureMezOnHealer { get; set; } = 40;
        public int CureMezOnCaster { get; set; } = 20;
        /// <summary>The target is a healer or caster that nobody is keeping interrupted.</summary>
        public int TargetIsFree { get; set; } = 15;
        public int RootAlreadyOnTarget { get; set; } = -25;
        public int RootTargetAlreadyInterrupted { get; set; } = -15;
        /// <summary>Waking a mezzed enemy that is not the kill target.</summary>
        public int TargetMezzed { get; set; } = -120;
        public int LowPowerPercent { get; set; } = 25;
        public int DamageOnLowPower { get; set; } = -30;       // keep what is left for control
        /// <summary>
        /// Power gets dearer as it runs out: an option loses this times its cost (percent of the
        /// pool) times the share of the pool already spent. A cast of 8% at 30% power loses 8 x 0.7 x this.
        /// </summary>
        public double PowerScarcity { get; set; } = 3;
        /// <summary>Lost per 100 units the caster would first have to walk; picking such an option means walking.</summary>
        public double PerHundredUnitsShort { get; set; } = 2;
        /// <summary>Enemies farther than this are not considered at all.</summary>
        public int ConsiderRange { get; set; } = 3_500;

        /// <summary>What a shield fighter weighs when it picks whom to guard and whom to go for.</summary>
        public PvpPeelWeights Peel { get; set; } = new();

        /// <summary>Who has interrupt duty, by what means, and how interrupters are paired with enemy healers and casters.</summary>
        public PvpInterruptWeights Interrupt { get; set; } = new();

        /// <summary>How far apart the group's healers and casters keep.</summary>
        public PvpSpreadWeights Spread { get; set; } = new();

        /// <summary>When Purge and Group Purge are worth using.</summary>
        public PvpPurgeWeights Purge { get; set; } = new();

        /// <summary>How the group's healers share out their patients.</summary>
        public PvpHealWeights Heal { get; set; } = new();

        public const string ReferenceFileName = "pvp-weights-old.json";

        public static PvpWeights Current { get; private set; } = new();

        /// <summary>
        /// The set to measure <see cref="Current"/> against, from <c>pvp-weights-old.json</c> beside
        /// the server; null without that file. In a comparison one team of a match fights with
        /// each set, so every match is a direct result of new against old.
        /// </summary>
        public static PvpWeights Reference { get; private set; }

        /// <summary>The weights a bot fights with: its team's set in a comparison, otherwise <see cref="Current"/>.</summary>
        public static PvpWeights Of(GameBot bot) => bot?.PvpWeightsSet ?? Current;

        /// <summary>Reads the weights files beside the server, if there are any. Returns what was loaded, or why not.</summary>
        public static string Reload(string folder)
        {
            (PvpWeights current, string first) = Read(Path.Combine(folder, FileName));
            Current = current ?? new PvpWeights();
            string old = Path.Combine(folder, ReferenceFileName);
            if (!File.Exists(old))
            {
                Reference = null;
                return first;
            }
            (PvpWeights reference, string second) = Read(old);
            Reference = reference;
            return $"{first}; compared against {second}";
        }

        private static (PvpWeights Weights, string Source) Read(string path)
        {
            if (!File.Exists(path))
                return (null, "defaults");
            try
            {
                PvpWeights loaded = JsonSerializer.Deserialize<PvpWeights>(File.ReadAllText(path)) ?? new PvpWeights();
                // A class the file does not mention keeps its built-in leaning.
                foreach ((string name, Dictionary<string, int> kinds) in new PvpWeights().ClassBase)
                    loaded.ClassBase.TryAdd(name, kinds);
                return (loaded, path);
            }
            catch (Exception exception)
            {
                return (null, $"defaults ({Path.GetFileName(path)} could not be read: {exception.Message})");
            }
        }
    }

    public static class PvpActionChoice
    {
        /// <summary>True when the class picks its casts here; other classes keep the old brain's logic.</summary>
        public static bool Covers(eCharacterClass characterClass, PvpWeights weights) =>
            weights.ClassBase.ContainsKey(characterClass.ToString());

        /// <summary>The kinds this class would ever cast, so the brain does not look up spells for the rest.</summary>
        public static IEnumerable<PvpActionKind> KindsFor(eCharacterClass characterClass, PvpWeights weights) =>
            weights.ClassBase.TryGetValue(characterClass.ToString(), out Dictionary<string, int> kinds)
                ? Enum.GetValues<PvpActionKind>().Where(kind => kinds.ContainsKey(kind.ToString()))
                : [];

        /// <summary>True for a class that mezzes for its group: one with an area mez among its leanings.</summary>
        public static bool IsController(eCharacterClass characterClass, PvpWeights weights) =>
            weights.ClassBase.TryGetValue(characterClass.ToString(), out Dictionary<string, int> kinds) && kinds.ContainsKey(nameof(PvpActionKind.MezArea));

        /// <summary>False for an option that does nothing: a second nearsight or disease, or nearsight on a fighter.</summary>
        public static bool Useful(PvpOption option) => option.Kind switch
        {
            PvpActionKind.Nearsight => !option.HasEffect && option.Archetype != PvpBuffArchetype.Melee,
            PvpActionKind.Disease => !option.HasEffect,
            // Not on an enemy that is asleep, immune or the one being killed; an area mez must catch somebody.
            PvpActionKind.Mez => !option.HasEffect && !option.IsKill,
            PvpActionKind.MezArea => option.Caught > 0,
            PvpActionKind.Stun => !option.HasEffect,
            PvpActionKind.CureMez => option.Mezzed,
            // Worth a turn only while somebody inside it is casting.
            PvpActionKind.Amnesia => option.Caught > 0,
            _ => true,
        };

        public static int Score(eCharacterClass characterClass, PvpOption option, PvpWeights weights)
        {
            bool soft = option.Archetype != PvpBuffArchetype.Melee;
            double score = weights.ClassBase.TryGetValue(characterClass.ToString(), out Dictionary<string, int> kinds) &&
                           kinds.TryGetValue(option.Kind.ToString(), out int lean) ? lean : 0;
            if (option.Kind == PvpActionKind.CureMez)
            {
                // The target is a friend: none of the terms about enemies below apply.
                score += option.Archetype switch { PvpBuffArchetype.Healer => weights.CureMezOnHealer, PvpBuffArchetype.Caster => weights.CureMezOnCaster, _ => 0 };
                score -= weights.PowerScarcity * option.CostPercent * (100 - option.PowerPercent) / 100.0;
                score -= weights.PerHundredUnitsShort * option.Short / 100.0;
                return (int)Math.Round(score);
            }
            switch (option.Kind)
            {
                case PvpActionKind.Nearsight:
                    score += option.Archetype == PvpBuffArchetype.Caster ? weights.NearsightOnCaster : weights.NearsightOnHealer;
                    break;
                case PvpActionKind.Disease:
                    score += option.IsKill ? weights.DiseaseOnKillTarget : 0;
                    break;
                case PvpActionKind.Damage:
                    score += option.IsKill
                        ? weights.DamageOnKillTarget + weights.DamagePerMissingHealth * (100 - option.HealthPercent)
                        : weights.DamageOffKillTarget;
                    score += option.PowerPercent < weights.LowPowerPercent ? weights.DamageOnLowPower : 0;
                    break;
                case PvpActionKind.Root:
                    score += option.HasEffect ? weights.RootAlreadyOnTarget : 0;
                    score += option.Interrupted ? weights.RootTargetAlreadyInterrupted : 0;
                    break;
                case PvpActionKind.Mez:
                case PvpActionKind.MezArea:
                    score += weights.MezPerEnemyCaught * option.Caught;
                    score += option.Archetype == PvpBuffArchetype.Healer ? weights.MezOnHealer : 0;
                    score += option.CatchesKill ? weights.MezCatchesKillTarget : 0;
                    break;
                case PvpActionKind.Stun:
                    score += option.IsKill ? weights.StunOnKillTarget : 0;
                    break;
                case PvpActionKind.Amnesia:
                    score += weights.AmnesiaPerCastStopped * option.Caught + weights.AmnesiaPerSleeperWoken * option.Wakes;
                    break;
                case PvpActionKind.Pet:
                    score += soft ? weights.PetOnCasterOrHealer : weights.PetOnMelee;
                    break;
            }
            if (option.Kind is PvpActionKind.Mez or PvpActionKind.MezArea or PvpActionKind.Stun)
            {
                score += option.OnController ? weights.ControlOnEnemyController : 0;
                if (option.Instant)
                    score += weights.InstantSpent + (option.Casting ? weights.InstantOnCastingTarget : 0);
            }
            score -= weights.PowerScarcity * option.CostPercent * (100 - option.PowerPercent) / 100.0;
            score -= weights.PerHundredUnitsShort * option.Short / 100.0;
            if (option.IsAssigned)
                score += weights.OnAssignedTarget;
            if (soft && !option.Interrupted)
                score += weights.TargetIsFree + (option.Casting ? weights.InterruptsACast : 0);
            if (option.Mezzed && !option.IsKill)
                score += weights.TargetMezzed;
            return (int)Math.Round(score);
        }

        /// <summary>The useful options, best first. The caller casts the first one that reaches <see cref="PvpWeights.MinScore"/>.</summary>
        public static List<(PvpOption Option, int Score)> Rank(eCharacterClass characterClass, IEnumerable<PvpOption> options, PvpWeights weights) =>
            options.Where(Useful)
                .Select(option => (Option: option, Score: Score(characterClass, option, weights)))
                .OrderByDescending(scored => scored.Score)
                .ThenBy(scored => scored.Option.Kind)
                .ThenBy(scored => scored.Option.TargetId)
                .ToList();
    }
}

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
        /// <summary>Lowers the target's resistance to the kind of damage the caster's own spells do.</summary>
        Debuff,
        /// <summary>A ranged area nuke, cast on enemy pets that are on the group's healers and casters.</summary>
        AreaDamage,
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
        bool OnController = false, // the target is one of the enemy's mezzers
        bool Pursuer = false,     // an enemy fighter in melee on one of the group's healers or casters
        bool Extending = false,   // the group's order is Extend
        int WalkDanger = 0,       // free enemy fighters near the place the caster would have to walk to
        bool OnTrained = false,   // one of the enemies on the member of the group that is being trained
        int WalkInsideEdge = 0,   // how far inside the edge distance of the nearest awake enemy the walk would end
        bool KillOutOfReach = false, // no kill target is called, or it is beyond this spell's range from where the caster stands
        bool Rooted = false,      // held by a root or slowed by a spell, which damage would break
        bool PetOnBackline = false); // the target is an enemy pet in melee on one of the group's healers or casters

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
            // Root, for the casters that have one: low, so that it is cast on a fighter that is on
            // their healers and casters (RootOnPursuer) and seldom otherwise.
            // The Eldritch has no root; its snare nuke stands in for one.
            ["Eldritch"] = new() { ["Nearsight"] = 80, ["Disease"] = 70, ["Damage"] = 55, ["Root"] = 30 },
            // Debuff (owner, 2026-11-18): the tri-spec Cabalist has a body resistance debuff, for
            // when the casters kill a target together.
            ["Cabalist"] = new() { ["Nearsight"] = 60, ["Disease"] = 70, ["Damage"] = 55, ["Root"] = 30, ["Debuff"] = 45 },
            ["Runemaster"] = new() { ["Nearsight"] = 75, ["Damage"] = 60, ["Root"] = 30, ["AreaDamage"] = 40 },
            ["Theurgist"] = new() { ["Pet"] = 70, ["Damage"] = 30, ["Root"] = 30 },
            // Main mezzers (owner, 2026-11-16): Sorcerer, Bard, Pacification Healer. The Healer's entry
            // serves both its specs: the Mending Healer simply has fewer of the spells.
            // Cure mez (owner, 2026-11-16): Midgard's two Healers, Albion's Sorcerer and Minstrel. A class
            // without the spell simply never has the option.
            // Amnesia (owner, 2026-11-17): the Bard's is instant with a ten-second recast, the other two take a two-second cast.
            ["Sorcerer"] = new() { ["Mez"] = 120, ["MezArea"] = 120, ["CureMez"] = 130, ["Damage"] = -10, ["Root"] = 35, ["Amnesia"] = 45 },
            ["Bard"] = new() { ["Mez"] = 120, ["MezArea"] = 120, ["CureMez"] = 130, ["Damage"] = 40, ["Amnesia"] = 60 },
            ["Healer"] = new() { ["Mez"] = 110, ["MezArea"] = 110, ["CureMez"] = 140, ["Stun"] = 70, ["Amnesia"] = 45, ["Root"] = 30 },
            // Albion's second cure mez (owner, 2026-11-16). The Minstrel had the spell and never cast it:
            // the old brain reaches cure mez only through its healing routine, and a Minstrel has no heals.
            // Only this is scored for it; its mezzes, shouts and melee stay with the old brain.
            // Scoring its shouts and mez as well was tried on 2026-11-18 and taken back: its single mez
            // is a song that began ninety times a match and never finished, where the old brain had
            // been landing six mezzes a match with its area lullaby.
            ["Minstrel"] = new() { ["CureMez"] = 130 },
            // A Druid's base line has a 70-second root with the range of a nuke. The owner, watching on
            // 2026-11-18: "the Druid has a Skald on himself and never tried to cast a root while the
            // Skald was running toward him." Heals still come first; with nobody to heal, a root on a
            // fighter that is on, or running at, the group's healers and casters reaches the bar
            // (30 here and RootOnPursuer), and one on a fighter that is on nobody does not.
            ["Druid"] = new() { ["Root"] = 30 },
            // The owner, 2026-11-18: the Skald is an interrupt class. Target a caster, instant damage
            // shout, switch to another caster, healer or mezzer, second shout; a snare on a fighter in
            // passing; its instant mez on a free enemy that is not a fighter. Changing target often
            // is right for it. All of these are instants, so they cost it no swing.
            ["Skald"] = new() { ["Damage"] = 60, ["Mez"] = 100, ["Root"] = 35 },
            ["Enchanter"] = new() { ["Damage"] = 60 },
            ["Wizard"] = new() { ["Damage"] = 60 },
            ["Mentalist"] = new() { ["Damage"] = 60 },
            ["Spiritmaster"] = new() { ["Damage"] = 60 },
        };

        /// <summary>
        /// Added to the class's leaning for one build. The owner, 2026-11-17: the Pacification
        /// Healer weighs toward crowd control and the Mending or Augmentation Healer toward healing;
        /// the second still has its mez, stun and cure mez, for when nobody needs a heal.
        /// </summary>
        public Dictionary<string, Dictionary<string, int>> BuildLean { get; set; } = new()
        {
            ["healer-pac"] = new() { ["Mez"] = 15, ["MezArea"] = 15, ["Stun"] = 10, ["CureMez"] = 30 },
            ["healer-mend"] = new() { ["Mez"] = -30, ["MezArea"] = -30, ["Stun"] = -20 },
        };

        /// <summary>
        /// By build or class: the healing routine takes a healer's turn before its scored choice
        /// only while a member of the group in reach is below this health percent. Anyone not
        /// listed heals first always, as before. The owner, 2026-11-17: the Pacification Healer is
        /// for control and the Shaman for interrupts, with heals as a backup when the others are
        /// busy; both Healers have an instant heal and an instant group heal for emergencies. 40 and 50
        /// were tried first and cost a quarter of the group's healing; at 75 nothing is lost (2026-11-17).
        /// </summary>
        public Dictionary<string, int> HealsFirstBelow { get; set; } = new()
        {
            ["healer-pac"] = 75,
            ["Shaman"] = 75,
            // A mezzer first; it heals when somebody is hurt. (The assistant's reading of the class,
            // not the owner's word: the question is open in the playbook.)
            ["Bard"] = 75,
        };

        /// <summary>The threshold for a build, by its id first and its class second; above 100 means heals always come first.</summary>
        public int HealsFirstBelowFor(string build, eCharacterClass characterClass) =>
            build != null && HealsFirstBelow.TryGetValue(build, out int byBuild) ? byBuild
            : HealsFirstBelow.TryGetValue(characterClass.ToString(), out int byClass) ? byClass : 101;

        public int NearsightOnCaster { get; set; } = 30;
        public int NearsightOnHealer { get; set; } = 15;
        /// <summary>More for the enemy's mezzer: nearsighted, its mez no longer outranges anybody's.</summary>
        public int NearsightOnEnemyController { get; set; } = 25;
        public int DiseaseOnKillTarget { get; set; } = 40;     // disease halves the healing it receives
        /// <summary>
        /// Disease also slows and weakens: on a fighter it is worth more than on a caster. The
        /// owner's Cabalist, 2026-11-18: "disease enemy tanks and other relevant targets". Without
        /// this three in four went on healers and casters, which are free more often.
        /// </summary>
        public int DiseaseOnFighter { get; set; } = 30;
        /// <summary>More on a fighter that is on the group's healers and casters.</summary>
        public int DiseaseOnPursuer { get; set; } = 20;
        /// <summary>
        /// A resistance debuff is for the kill target only, and goes on before the damage: with
        /// this it outscores a nuke on a target near full health and loses to one once the target is hurt.
        /// </summary>
        public int DebuffOnKillTarget { get; set; } = 50;
        public int DamageOnKillTarget { get; set; } = 30;
        public int DamageOffKillTarget { get; set; } = -60;    // spread damage kills nobody
        /// <summary>
        /// In place of the above while the kill target is beyond the spell's range, or none is
        /// called: the caster works on what it can reach. The owner, watching on 2026-11-19: the
        /// "Enchanter was standing most of the fight, not doing anything". Once the two back
        /// lines stood apart the fighters' target was beyond the casters' range for 34 to 62% of
        /// the turns a caster found nothing to cast, and damage on anybody else never reached the
        /// bar: an enemy fighter in range scored -4, a healer or caster 24, against 50. The
        /// Enchanter stood free to act for 24% of its life and cast for 17%.
        /// </summary>
        public int DamageOffKillTargetOutOfReach { get; set; } = -5;
        /// <summary>
        /// Damage on an enemy pet that is in melee on one of the group's healers or casters, in
        /// place of the kill-target terms. The owner, watching Albion against Hibernia on
        /// 2026-11-19: two Albion casters had a Druid's pet on them, "and they just ran away and
        /// stood still. They could always quickcast or staff or someone dd or attack the pet.
        /// They are really weak". In that match a lynx was on the Cabalist for 71 seconds and
        /// attacked it 24 times; the Cabalist cast nothing in that time and nobody attacked the
        /// lynx. With this a nuke on such a pet outscores one on a kill target at full health.
        /// The Theurgist's pets are left to the point-blank spells (bots/pvp/PvpPetClear.cs).
        /// </summary>
        public int DamageOnPetOnBackline { get; set; } = 35;
        /// <summary>
        /// A ranged area nuke on enemy pets that are on the group's healers and casters, for each
        /// pet inside it (two at least, and nobody asleep in it). The owner, 2026-11-19, on what
        /// Midgard did about a Theurgist: "Runemaster should have an AoE, similar to the
        /// Eldritch." On this server the Runemaster's is not point-blank: Odin's Hatred (level 44
        /// of the Runecarving line) does 158 damage 350 units around a target up to 1,500 away.
        /// Albion's pets attacked Midgard's healers about 135 times a match with no answer.
        /// </summary>
        public int AreaDamagePerPet { get; set; } = 20;
        /// <summary>
        /// A healer or caster that cannot cast because an enemy pet is hitting it hits the pet
        /// with its staff (the owner, the same day).
        /// </summary>
        public bool StaffThePetOnMe { get; set; } = true;
        /// <summary>
        /// A caster drops the mez, stun or root it is casting when its target has meanwhile been
        /// put to sleep, stunned or rooted by somebody else, or has become immune, as a player
        /// who sees the immunity go up stops casting. In 24 matches at rank 5, of 78 mezzes begun
        /// on an enemy fighter in the first fifteen seconds 19 were finished on a target that
        /// had become immune during the cast (another mez had landed and been purged): about
        /// two seconds each, for nothing.
        /// </summary>
        public bool DropsAWastedControlCast { get; set; } = true;

        /// <summary>True for a single-target mez, stun or root whose target no longer takes it.</summary>
        public static bool ControlCastIsWasted(bool mez, bool stun, bool root, bool targetAlive, bool mezzedOrImmune, bool stunnedOrImmune, bool rootedOrImmune) =>
            !targetAlive || mez && mezzedOrImmune || stun && stunnedOrImmune || root && rootedOrImmune;

        /// <summary>Damage on an enemy that a spell holds or slows, and that is not the kill target, sets it free.</summary>
        public int DamageBreaksRoot { get; set; } = -60;
        /// <summary>
        /// In place of the above for an instant on a healer or caster that is free to cast: a shout
        /// there is an interrupt, not spread damage. On one already interrupted it stays spread
        /// damage, which is what makes a Skald move on to the next caster.
        /// </summary>
        public int InstantDamageOnFreeCaster { get; set; } = -20;
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
        /// <summary>
        /// A mez or stun on an enemy that is on the group's trained member, and a nearsight on a
        /// caster or healer that is: the owner, 2026-11-17, on being trained: all heals went to
        /// that member "until a peel, mez or stun" let it get away. Without this nothing told a
        /// mezzer which enemies those were; its control went wherever it caught the most.
        /// </summary>
        public int ControlOnTrainer { get; set; } = 40;
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
        /// <summary>
        /// Waking the group's own mezzer: it comes out immune to mez for a minute and can then do its work.
        /// In 32 recorded Albion matches the Sorcerer, mezzed in the first seconds, was never woken.
        /// </summary>
        public int CureMezOnController { get; set; } = 30;
        /// <summary>The target is a healer or caster that nobody is keeping interrupted.</summary>
        public int TargetIsFree { get; set; } = 15;
        public int RootAlreadyOnTarget { get; set; } = -25;
        public int RootTargetAlreadyInterrupted { get; set; } = -15;
        /// <summary>
        /// A root on an enemy fighter that is on the group's healers and casters and is not slowed
        /// yet: the member it is on can then walk away from it. The owner, 2026-11-18: while
        /// kiting, a caster that is already far away casts its snare, area snare or root on the
        /// pursuers. Without this a root on a fighter never reached the bar.
        /// </summary>
        public int RootOnPursuer { get; set; } = 30;
        /// <summary>More of the same while the group's order is Extend.</summary>
        public int RootOnPursuerWhileExtending { get; set; } = 30;
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
        /// <summary>
        /// Lost by any option the caster would first have to walk for, on top of the distance: what
        /// can be cast from where it stands comes first. The base of 2026-11-17 showed why: a
        /// Sorcerer whose area mez reaches 1,875 units walked on toward a healer in the enemy's
        /// back row while the enemy's front row was already in range, and was mezzed by an instant
        /// before it had cast anything. Measured overnight, 192 matches with one team at -40 and
        /// the other at 0: the team with it won 123 to 69, and every realm did better with it
        /// (Albion 78% against 54%, Midgard 54% against 18%, Hibernia 59% against 34%).
        /// </summary>
        public int NeedsWalk { get; set; } = -40;
        /// <summary>
        /// Lost by an option the caster would have to walk for, for each enemy fighter that is
        /// free to act within <see cref="WalkDangerWithin"/> of the place it would stop to cast
        /// (up to three). The owner, 2026-11-18: casters and healers stand near the edge of their
        /// range and away from enemy fighters. Recordings of that day: the Sorcerer, the Bard, the
        /// Enchanter and the Pacification Healer walked 1,000 to 1,900 units to cast on the
        /// enemy's back row, ended up halfway into the enemy and were the first to die; the Bard
        /// was dead 58% of the time one of its Druids was asleep and needed waking.
        /// </summary>
        public int WalkEndsNearFighter { get; set; } = -30;
        public int WalkDangerWithin { get; set; } = 600;
        /// <summary>
        /// Lost by an option the caster would have to walk for, for every 100 units by which the
        /// place it would stop lies inside <see cref="PvpPositionWeights.EdgeDistance"/> of the
        /// nearest enemy healer or caster that is awake (a fighter there is the walk's danger,
        /// above). A target deep in the enemy's back row is in range only
        /// from among their front casters: in the twelve check matches of the night of 2026-11-18
        /// a caster walked toward a target 23 times in the first minute, and the two back lines
        /// still closed from 1,180 units at twenty seconds to 875 at forty-five. 0 = as before.
        /// At 8 the lines still closed to 1,010; 16 is part of what kept them apart
        /// (<see cref="PvpPositionWeights.EdgeDistance"/>).
        /// </summary>
        public double WalkInsideEdgePerHundred { get; set; } = 16;
        /// <summary>Enemies farther than this are not considered at all.</summary>
        public int ConsiderRange { get; set; } = 3_500;
        /// <summary>
        /// Off: once the group's fight has begun no buff with a cast time is begun, on a member
        /// or on the caster itself, and a healer does not summon its pet
        /// (BotBrain.IsBuffHeldBackInFight). On: as it was until 2026-11-18.
        /// </summary>
        public bool BuffsInFight { get; set; } = false;

        /// <summary>
        /// A rise of the ground between two characters hides one from the other
        /// (bots/pvp/PvpSight.cs): for what a group spots on the road and knows in a fight. Off:
        /// only where nobody can walk hides anything, as it was until 2026-11-19.
        /// </summary>
        public bool SightOverGround { get; set; } = true;

        /// <summary>
        /// A bot on the move sees its whole range inside this arc in front of it (degrees), and
        /// <see cref="SightBehindPercent"/> of it outside: a group can be come upon from behind.
        /// Standing still it sees all round. 360 or 100: it sees all round always, as until 2026-11-20.
        /// </summary>
        public int SightFrontArc { get; set; } = 200;
        public int SightBehindPercent { get; set; } = 40;
        /// <summary>
        /// An 8v8 Bard in a fight starts its endurance song at most once in this many seconds,
        /// only that song, and not while it is being hit or a groupmate is asleep. 0: no song is
        /// started during a fight. -1: as it was from the morning of 2026-11-18, when the Bard
        /// was told to keep its songs up in combat: every song is a three-second cast, and in the
        /// 150 matches of that evening it began one 20 times a match (endurance and the three
        /// resistance songs in turn), finished 7, and was refused 13 times while being hit: about
        /// 22 of the 46 seconds it lived, from the first second of the fight. It cast cure mez
        /// 0.0 times a match against Albion.
        /// </summary>
        public int SongInFightEverySeconds { get; set; } = 20;


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

        /// <summary>Where healers and casters stand in a fight (bots/pvp/PvpPositioning.cs).</summary>
        public PvpPositionWeights Position { get; set; } = new();

        /// <summary>Whom the group calls as its kill target (bots/pvp/PvpKillTarget.cs).</summary>
        public PvpKillWeights Kill { get; set; } = new();

        /// <summary>What the group does about enemy pets (bots/pvp/PvpPetClear.cs).</summary>
        public PvpPetWeights Pets { get; set; } = new();

        /// <summary>How the group reads the fight into an order (bots/pvp/PvpReading.cs).</summary>
        public PvpOrderWeights Order { get; set; } = new();

        /// <summary>
        /// For a set drawn at random for one team of one match (bots/pvp/PvpWeightDraw.cs): which
        /// weights were moved, -1 down and +1 up. Null for any other set.
        /// </summary>
        public Dictionary<string, int> Drawn { get; set; }

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

        /// <summary>
        /// True when the class's attacking casts are picked here too, so the old brain's random
        /// pick is not used for it. A class with only cure mez scored (the Minstrel) keeps the
        /// old brain for everything else.
        /// </summary>
        public static bool ScoresOffense(eCharacterClass characterClass, PvpWeights weights) =>
            KindsFor(characterClass, weights).Any(kind => kind != PvpActionKind.CureMez);

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
            PvpActionKind.Debuff => option.IsKill && !option.HasEffect,
            PvpActionKind.AreaDamage => option.Caught >= 2,
            _ => true,
        };

        public static int Score(eCharacterClass characterClass, PvpOption option, PvpWeights weights, string build = null)
        {
            bool soft = option.Archetype != PvpBuffArchetype.Melee;
            double score = weights.ClassBase.TryGetValue(characterClass.ToString(), out Dictionary<string, int> kinds) &&
                           kinds.TryGetValue(option.Kind.ToString(), out int lean) ? lean : 0;
            if (build != null && weights.BuildLean.TryGetValue(build, out Dictionary<string, int> leans) &&
                leans.TryGetValue(option.Kind.ToString(), out int more))
                score += more;
            if (option.Kind == PvpActionKind.CureMez)
            {
                // The target is a friend: none of the terms about enemies below apply.
                score += option.Archetype switch { PvpBuffArchetype.Healer => weights.CureMezOnHealer, PvpBuffArchetype.Caster => weights.CureMezOnCaster, _ => 0 };
                score += option.OnController ? weights.CureMezOnController : 0;
                score -= weights.PowerScarcity * option.CostPercent * (100 - option.PowerPercent) / 100.0;
                score -= weights.PerHundredUnitsShort * option.Short / 100.0 - (option.Short > 0 ? weights.NeedsWalk : 0);
                return (int)Math.Round(score);
            }
            switch (option.Kind)
            {
                case PvpActionKind.Nearsight:
                    score += option.Archetype == PvpBuffArchetype.Caster ? weights.NearsightOnCaster : weights.NearsightOnHealer;
                    score += option.OnController ? weights.NearsightOnEnemyController : 0;
                    score += option.OnTrained ? weights.ControlOnTrainer : 0;
                    break;
                case PvpActionKind.Disease:
                    score += option.IsKill ? weights.DiseaseOnKillTarget : 0;
                    score += soft ? 0 : weights.DiseaseOnFighter + (option.Pursuer ? weights.DiseaseOnPursuer : 0);
                    break;
                case PvpActionKind.Debuff:
                    score += option.IsKill ? weights.DebuffOnKillTarget : 0;
                    break;
                case PvpActionKind.Damage:
                    score += option.PetOnBackline ? weights.DamageOnPetOnBackline
                        : option.IsKill
                        ? weights.DamageOnKillTarget + weights.DamagePerMissingHealth * (100 - option.HealthPercent)
                        : option.Instant && soft && !option.Interrupted ? weights.InstantDamageOnFreeCaster
                        : option.KillOutOfReach ? weights.DamageOffKillTargetOutOfReach
                        : weights.DamageOffKillTarget;
                    score += option.Rooted && !option.IsKill ? weights.DamageBreaksRoot : 0;
                    score += option.PowerPercent < weights.LowPowerPercent ? weights.DamageOnLowPower : 0;
                    break;
                case PvpActionKind.Root:
                    score += option.HasEffect ? weights.RootAlreadyOnTarget : 0;
                    // "Already interrupted" is about a root cast to stop a caster. A fighter on the group's
                    // healers is always being hit, and that took a root on it to 46 against a bar of 50.
                    score += option.Interrupted && !option.Pursuer ? weights.RootTargetAlreadyInterrupted : 0;
                    if (option.Pursuer && !option.HasEffect)
                        score += weights.RootOnPursuer + (option.Extending ? weights.RootOnPursuerWhileExtending : 0);
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
                case PvpActionKind.AreaDamage:
                    score += weights.AreaDamagePerPet * option.Caught;
                    break;
            }
            if (option.Kind is PvpActionKind.Mez or PvpActionKind.MezArea or PvpActionKind.Stun)
            {
                score += option.OnController ? weights.ControlOnEnemyController : 0;
                score += option.OnTrained ? weights.ControlOnTrainer : 0;
                if (option.Instant)
                    score += weights.InstantSpent + (option.Casting ? weights.InstantOnCastingTarget : 0);
            }
            score -= weights.PowerScarcity * option.CostPercent * (100 - option.PowerPercent) / 100.0;
            score -= weights.PerHundredUnitsShort * option.Short / 100.0 - (option.Short > 0 ? weights.NeedsWalk : 0);
            if (option.Short > 0)
                score += weights.WalkEndsNearFighter * Math.Min(3, option.WalkDanger) - weights.WalkInsideEdgePerHundred * option.WalkInsideEdge / 100.0;
            if (option.IsAssigned)
                score += weights.OnAssignedTarget;
            if (soft && !option.Interrupted)
                score += weights.TargetIsFree + (option.Casting ? weights.InterruptsACast : 0);
            if (option.Mezzed && !option.IsKill)
                score += weights.TargetMezzed;
            return (int)Math.Round(score);
        }

        /// <summary>
        /// True when an option is worth the turn. What needs a walk is ranked below what does
        /// not (<see cref="PvpWeights.NeedsWalk"/>), but that must not stop a caster walking when
        /// walking is all there is: for the bar, the walk's rank penalty is given back.
        /// </summary>
        public static bool WorthIt(PvpOption option, int score, PvpWeights weights) =>
            score - (option.Short > 0 ? weights.NeedsWalk : 0) >= weights.MinScore;

        /// <summary>The useful options, best first. The caller casts the first one that is <see cref="WorthIt"/>.</summary>
        public static List<(PvpOption Option, int Score)> Rank(eCharacterClass characterClass, IEnumerable<PvpOption> options, PvpWeights weights,
            string build = null) =>
            options.Where(Useful)
                .Select(option => (Option: option, Score: Score(characterClass, option, weights, build)))
                .OrderByDescending(scored => scored.Score)
                .ThenBy(scored => scored.Option.Kind)
                .ThenBy(scored => scored.Option.TargetId)
                .ToList();
    }
}

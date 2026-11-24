using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.RealmAbilities;
using Step = DOL.GS.AutonomousBotRealmAbilities.Step;

namespace DOL.GS
{
    /// <summary>
    /// What an 8v8 bot sees when it asks itself whether to press a realm ability. Counted over the
    /// living members of its group within 1,500 units, itself among them.
    /// </summary>
    /// <param name="InFight">An enemy stands within engage range.</param>
    /// <param name="HitInMelee">An enemy fighter that is awake has it as its target, within 500 units.</param>
    /// <param name="Interrupted">It cannot begin a cast because somebody hit it.</param>
    /// <param name="Hurt">Members below <see cref="PvpRealmAbilityWeights.HurtBelow"/>.</param>
    /// <param name="BadlyHurt">Members below <see cref="PvpRealmAbilityWeights.BadlyHurtBelow"/>.</param>
    /// <param name="LowestOther">The health of the lowest member that is not itself; 100 alone.</param>
    /// <param name="MissingHealthOfOthers">Hit points the other members are short of.</param>
    /// <param name="MembersHitInMelee">Members with an enemy fighter on them.</param>
    /// <param name="MembersSlowed">Members rooted or snared.</param>
    /// <param name="EnemyCastersOnUs">Enemy casters, awake, whose target is a member.</param>
    /// <param name="Retreating">The group follows a called retreat.</param>
    /// <param name="Order">What the group is doing (push, extend, regroup).</param>
    /// <param name="FightersFromEnemy">How far the group's fighters are from the nearest enemy, the middle one of them; 0 without fighters.</param>
    /// <param name="SoftMembersHitInMelee">Healers and casters of the group with an enemy fighter on them.</param>
    /// <param name="MembersAlive">The living members of the group; 0 when it is not told.</param>
    /// <param name="EnemiesAlive">The living enemies the group reckons with.</param>
    public readonly record struct PvpAbilityView(bool InFight, int Health, bool HitInMelee, bool Interrupted, int Hurt, int BadlyHurt,
        int LowestOther, int MissingHealthOfOthers, int MembersHitInMelee, int MembersSlowed, int EnemyCastersOnUs, bool Retreating,
        PvpBuffArchetype Archetype, PvpOrder Order = PvpOrder.Engage, int FightersFromEnemy = 0, int SoftMembersHitInMelee = 0,
        int MembersAlive = 0, int EnemiesAlive = 0);

    /// <summary>Part of <see cref="PvpWeights"/>: when an active realm ability is worth its fifteen or thirty minutes.</summary>
    public sealed class PvpRealmAbilityWeights
    {
        /// <summary>A member below this is "hurt"; below the second, "badly hurt".</summary>
        public int HurtBelow { get; set; } = 60;
        public int BadlyHurtBelow { get; set; } = 40;

        /// <summary>
        /// An ability for the whole group is used by one member at a time: for this long after
        /// one has used it, no other member of the group uses the same one, and keeps it for
        /// the next time. The owner, 2026-11-24, after a fight in which both Druids of the group
        /// had purged it 2.4 seconds apart: "Only 1 bard should use it, and 2nd should keep it
        /// for another itme. In the same vein, druid should use group purge one at a time".
        /// Group Purge: fifteen seconds (whoever was freed cannot be put to sleep again for a
        /// minute). Speed of Sound: forty (it lasts thirty). The others that are for the whole
        /// group (Battery of Life, Bunker of Faith, Soldier's Barricade, Brilliant Aura,
        /// Ameliorating Melodies): thirty. 0: no turns are taken.
        /// </summary>
        public int GroupPurgeApartSeconds { get; set; } = 15;
        public int SpeedOfSoundApartSeconds { get; set; } = 40;
        public int GroupAbilityApartSeconds { get; set; } = 30;

        /// <summary>Speed of Sound: on a called retreat with the enemy near, or with this many members rooted or snared in a fight.</summary>
        public bool SpeedOfSoundOnRetreat { get; set; } = true;
        public int SpeedOfSoundAtSlowed { get; set; } = 2;

        /// <summary>
        /// Speed of Sound to get in: the group pushes and its fighters are still farther than this
        /// from any enemy. And to get away: the group falls back (extend, regroup) with this many of
        /// its healers and casters under an enemy fighter. The owner, 2026-11-19: a group that is
        /// jumped uses it "to reposition or to run through", and a group that is kited uses it to
        /// get in. Until 2026-11-22 a bot used it on a called retreat or with two members held by the
        /// legs, and the owner had "not seen Speed of Sound" from anybody. 0: not for that.
        /// </summary>
        public int SpeedOfSoundToCloseBeyond { get; set; } = 1_200;
        /// <summary>
        /// Not to get in on fewer enemies than the group has members alive: the last of a fight
        /// that is won are walked down. The owner, 2026-11-24, of a fight against one group won
        /// with nobody lost: "There's a bard that used SoS at the end of the that fight. Is there
        /// a reason?" The reason was this rule: the group pushed, its kill target had just died
        /// and its fighters stood 1,200 units from the next of the three enemies left; the
        /// fight before, both Bards had used it one second apart on the last five of forty.
        /// </summary>
        public bool SpeedOfSoundNotToCloseOnFewer { get; set; } = true;
        public int SpeedOfSoundAtSoftMembersJumped { get; set; } = 2;

        /// <summary>Mastery of Concentration, a healer: interrupted, with a member below this.</summary>
        public int ConcentrationHealerAtMemberBelow { get; set; } = 50;
        /// <summary>Mastery of Concentration, a caster: interrupted by a fighter on it, with at least this health left to cast through.</summary>
        public int ConcentrationCasterAtHealthAbove { get; set; } = 40;

        /// <summary>Battery of Life (1,000 hit points for the others): this many hurt, and this much missing.</summary>
        public int BatteryAtHurt { get; set; } = 2;
        public int BatteryAtMissingHealth { get; set; } = 1_000;

        /// <summary>Bunker of Faith and Soldier's Barricade: this many members with a fighter on them, and this many hurt.</summary>
        public int BunkerAtMembersHitInMelee { get; set; } = 2;
        public int BunkerAtHurt { get; set; } = 2;

        /// <summary>Brilliant Aura of Deflection: this many enemy casters on the group, and somebody hurt.</summary>
        public int AuraAtEnemyCasters { get; set; } = 2;

        /// <summary>Ameliorating Melodies (100 a member every 1.5 seconds for 30): this many hurt.</summary>
        public int MelodiesAtHurt { get; set; } = 2;

        /// <summary>Faith Healing (everybody to full): this many badly hurt.</summary>
        public int FaithHealingAtBadlyHurt { get; set; } = 2;

        /// <summary>Avoid Pain and Armor of Faith: a fighter on it and its health below this.</summary>
        public int AvoidPainBelow { get; set; } = 60;
        public int ArmorOfFaithBelow { get; set; } = 75;

        /// <summary>The Empty Mind (resistances for 30 seconds): below this with nobody on it in melee, so the damage is magic.</summary>
        public int EmptyMindBelow { get; set; } = 60;

        /// <summary>Perfect Recovery: a dead member within this, a healer before anybody else.</summary>
        public int PerfectRecoveryRange { get; set; } = 1_500;
    }

    /// <summary>
    /// The realm abilities of an 8v8 bot (docs/PVP-8V8-ROADMAP.md, M1.5): what each class buys with its
    /// points, in which order, and when it uses what it has bought.
    ///
    /// Until 2026-11-22 an 8v8 bot bought by the gamebots' rule (one build for each of four kinds of
    /// class) with Purge and Mystic Crystal Lore put first, and of the active abilities it knew Purge,
    /// Group Purge, Ignore Pain, Second Wind, First Aid, Mystic Crystal Lore and Raging Power. The
    /// owner, the same day: "let's finish realm abilities (passive and active)".
    ///
    /// The orders are the assistant's reading of how the classes were built in 1.65, with what the
    /// owner has said (Tireless among the first, Group Purge a Druid's first, a caster's power back
    /// through Mystic Crystal Lore and Raging Power). They are the owner's to correct, class by class.
    /// A step that cannot be paid for yet is passed over and comes first again when the points are
    /// there: the plan is worked out afresh from all the points each time, as if a respec were free.
    /// After its own order a class goes on with the gamebots' build for its kind.
    ///
    /// Bought by no bot, because no bot knows how to use it: the realm abilities that are an area of
    /// damage or control at a place or around the user (Volcanic Pillar, Negative Maelstrom, Rune of
    /// Decimation, Thornweed Field, Ichor of the Deep, Static Tempest, Wrath of the Champion,
    /// Bladedance, Whirling Dervish, Corporeal Disintegration: each breaks the group's own mez), the
    /// melee ones (Trip, Grapple, Battle Yell, the six styles, Rain of Fire, Ice and Annihilation,
    /// Fury of the Gods, Hail of Blows), the pet ones (Juggernaut, Excited Frenzy, Whip of
    /// Encouragement, Severing the Tether), Majestic Will, and Concentration, which resets the quick
    /// cast a bot does not have.
    /// </summary>
    public static class PvpRealmAbilities
    {
        public const string Tireless = "AtlasOF_Tireless", Determination = "AtlasOF_Determination", DeterminationHybrid = "AtlasOF_DeterminationHybrid",
            SpeedOfSound = "AtlasOF_SpeedOfSound", Concentration = "AtlasOF_MasteryOfConcentration", Battery = "AtlasOF_BatteryOfLife",
            Bunker = "AtlasOF_BunkerOfFaith", Barricade = "AtlasOF_SoldiersBarricade", Aura = "AtlasOF_BrilliantAura",
            Melodies = "AtlasOF_AmelioratingMelodies", FaithHealing = "AtlasOF_FaithHealing", PerfectRecovery = "AtlasOF_PerfectRecovery",
            AvoidPain = "AtlasOF_AvoidPain", ArmorOfFaith = "AtlasOF_ArmorOfFaith", EmptyMind = "AtlasOF_EmptyMind";

        private const string Purge = AutonomousBotRealmAbilities.Purge, PurgeReduced = AutonomousBotRealmAbilities.PurgeReduced,
            Mcl = AutonomousBotRealmAbilities.Mcl, RagingPower = AutonomousBotRealmAbilities.RagingPower,
            FirstAid = AutonomousBotRealmAbilities.FirstAid, IgnorePain = AutonomousBotRealmAbilities.IgnorePain,
            IgnorePainTank = AutonomousBotRealmAbilities.IgnorePainTank, AugStr = AutonomousBotRealmAbilities.AugStr,
            AugCon = AutonomousBotRealmAbilities.AugCon, AugDex = AutonomousBotRealmAbilities.AugDex,
            AugAcuity = AutonomousBotRealmAbilities.AugAcuity;

        /// <summary>
        /// The active abilities only an 8v8 bot buys, in the order it looks at them each second: what
        /// frees or saves the group before what helps itself.
        /// </summary>
        public static readonly string[] Actives =
        [
            PvpPurge.GroupPurgeKey, SpeedOfSound, PerfectRecovery, FaithHealing, Battery, Bunker, Barricade, Aura, Melodies,
            Concentration, AvoidPain, ArmorOfFaith, EmptyMind, Mcl, RagingPower,
        ];

        private static Step[] Order(params (string Key, int Level)[] steps) =>
            steps.Select(step => new Step(step.Key.StartsWith("AtlasOF_") ? step.Key : "AtlasOF_" + step.Key, step.Level)).ToArray();

        // A caster that kills: its Purge, its power back, then Mastery of Concentration to cast with a fighter on it.
        private static readonly Step[] Caster = Order((Tireless, 1), (Purge, 1), (Mcl, 2), (AugAcuity, 3), (Concentration, 1), (RagingPower, 1),
            ("MasteryOfMagery", 2), ("WildPower", 2), ("Serenity", 2), (EmptyMind, 1), ("Toughness", 2), ("MasteryOfMagery", 3));

        // A fighter in plate or chain: the cheap Purge, Determination against the mez and root that keep it off its target, Ignore Pain.
        private static readonly Step[] Tank = Order((Tireless, 1), (PurgeReduced, 1), (Determination, 3), (FirstAid, 2), (IgnorePainTank, 1),
            (Determination, 4), (AugStr, 2), (AugDex, 2), ("MasteryOfPain", 2), (Determination, 5), (AugCon, 3), (AvoidPain, 1), ("Toughness", 2));

        // A fighter with two weapons: the same, with its critical hits before its strength.
        private static readonly Step[] LightTank = Order((Tireless, 1), (PurgeReduced, 1), (Determination, 3), (FirstAid, 2), (IgnorePainTank, 1),
            (AugDex, 2), ("MasteryOfPain", 2), (Determination, 4), ("DualistsReflexes", 2), (AugStr, 2), (Determination, 5), ("MasteryOfPain", 3));

        private static readonly Dictionary<eCharacterClass, Step[]> Orders = new()
        {
            // ---- the healers
            // Group Purge first (owner, 2026-11-17: "a Druid has Group Purge, which is a huge difference").
            [eCharacterClass.Druid] = Order((Tireless, 1), (PvpPurge.GroupPurgeKey, 1), (Purge, 1), (Mcl, 2), (AugAcuity, 3), (Concentration, 1),
                (Battery, 1), (RagingPower, 1), ("Serenity", 2), ("WildHealing", 2), ("MasteryOfHealing", 2), ("Toughness", 2)),
            [eCharacterClass.Cleric] = Order((Tireless, 1), (Purge, 1), (Mcl, 2), (AugAcuity, 3), (Concentration, 1), (Battery, 1), (Bunker, 1),
                (RagingPower, 1), ("Serenity", 2), ("WildHealing", 2), ("MasteryOfHealing", 2), ("Toughness", 2)),
            [eCharacterClass.Healer] = Order((Tireless, 1), (Purge, 1), (Mcl, 2), (AugAcuity, 3), (Concentration, 1), (PerfectRecovery, 1),
                (Battery, 1), (RagingPower, 1), ("Serenity", 2), ("WildHealing", 2), ("MasteryOfHealing", 2), ("Toughness", 2)),
            [eCharacterClass.Shaman] = Order((Tireless, 1), (Purge, 1), (Mcl, 2), (AugAcuity, 3), (Concentration, 1), (RagingPower, 1),
                ("Serenity", 2), ("WildHealing", 2), ("MasteryOfHealing", 2), ("Toughness", 2), (FirstAid, 2), (IgnorePain, 1)),

            // ---- the casters
            [eCharacterClass.Sorcerer] = Caster, [eCharacterClass.Theurgist] = Caster, [eCharacterClass.Cabalist] = Caster,
            [eCharacterClass.Wizard] = Caster, [eCharacterClass.Runemaster] = Caster, [eCharacterClass.Spiritmaster] = Caster,
            [eCharacterClass.Eldritch] = Caster, [eCharacterClass.Mentalist] = Caster,
            // The group's resistances for thirty seconds, after its own Mastery of Concentration.
            [eCharacterClass.Enchanter] = Order((Tireless, 1), (Purge, 1), (Mcl, 2), (AugAcuity, 3), (Concentration, 1), (Aura, 1), (RagingPower, 1),
                ("MasteryOfMagery", 2), ("WildPower", 2), ("Serenity", 2), (EmptyMind, 1), ("Toughness", 2)),

            // ---- the speed classes
            [eCharacterClass.Minstrel] = Order((Tireless, 1), (Purge, 1), (SpeedOfSound, 1), (Mcl, 1), (FirstAid, 2), (IgnorePain, 1),
                (AugCon, 2), (AugDex, 2), ("MasteryOfPain", 2), ("Toughness", 2)),
            [eCharacterClass.Bard] = Order((Tireless, 1), (Purge, 1), (SpeedOfSound, 1), (Mcl, 2), (Melodies, 1), (RagingPower, 1),
                (FirstAid, 2), (IgnorePain, 1), (AugCon, 2), ("Toughness", 2)),
            [eCharacterClass.Skald] = Order((Tireless, 1), (Purge, 1), (SpeedOfSound, 1), (FirstAid, 2), (IgnorePain, 1), (AugStr, 3),
                (AugDex, 2), ("MasteryOfPain", 2), (AugCon, 2), ("Toughness", 2)),

            // ---- the fighters
            [eCharacterClass.Armsman] = [.. Tank.Take(5), new Step(Barricade, 1), .. Tank.Skip(5)],
            [eCharacterClass.Warrior] = Tank, [eCharacterClass.Hero] = Tank,
            [eCharacterClass.Mercenary] = LightTank, [eCharacterClass.Blademaster] = LightTank,
            [eCharacterClass.Berserker] = LightTank, [eCharacterClass.Savage] = LightTank,

            // ---- the hybrids
            [eCharacterClass.Paladin] = Order((Tireless, 1), (Purge, 1), (DeterminationHybrid, 3), (FirstAid, 2), (IgnorePain, 1), (FaithHealing, 1),
                (AugCon, 3), (AvoidPain, 1), (AugStr, 2), ("Toughness", 2)),
            [eCharacterClass.Champion] = Order((Tireless, 1), (Purge, 1), (DeterminationHybrid, 3), (FirstAid, 2), (IgnorePain, 1), (AugStr, 3),
                (AugDex, 2), ("MasteryOfPain", 2), (AugCon, 2), ("Toughness", 2)),
            [eCharacterClass.Warden] = Order((Tireless, 1), (Purge, 1), (Mcl, 2), (FirstAid, 2), (IgnorePain, 1), (AugCon, 3), (ArmorOfFaith, 1),
                (AugDex, 2), ("MasteryOfParrying", 2), ("Toughness", 2)),
        };

        /// <summary>What a class buys first; a class without an order keeps the one every 8v8 bot had.</summary>
        public static IReadOnlyList<Step> OrderOf(int classId) =>
            Orders.TryGetValue((eCharacterClass)classId, out Step[] order) ? order : AutonomousBotRealmAbilities.PvpFirst;

        /// <summary>How long after one member has used an ability for the whole group the others keep theirs (PvpRealmAbilityWeights.GroupPurgeApartSeconds); 0 for an ability of one's own.</summary>
        public static int KeptApartSeconds(string key, PvpRealmAbilityWeights weights) => key switch
        {
            PvpPurge.GroupPurgeKey => weights.GroupPurgeApartSeconds,
            SpeedOfSound => weights.SpeedOfSoundApartSeconds,
            Battery or Bunker or Barricade or Aura or Melodies => weights.GroupAbilityApartSeconds,
            _ => 0,
        };

        /// <summary>Whether somebody else of the group has just used that ability, so that this member keeps its own.</summary>
        public static bool TakenBySomebodyElse(bool usedByThisMember, long now, long lastUsed, int apartSeconds) =>
            apartSeconds > 0 && !usedByThisMember && lastUsed > 0 && now - lastUsed < apartSeconds * 1_000L;

        /// <summary>True for the abilities <see cref="Wanted(PvpAbilityView, string, PvpRealmAbilityWeights)"/> decides.</summary>
        public static bool Decides(string key) => key is SpeedOfSound or Concentration or Battery or Bunker or Barricade or Aura or Melodies or
            FaithHealing or PerfectRecovery or AvoidPain or ArmorOfFaith or EmptyMind;

        /// <summary>
        /// True when the ability is worth pressing now. Nothing is pressed outside a fight: each of
        /// these is gone for fifteen or thirty minutes, and outside a fight heals, songs and time do
        /// the same. Perfect Recovery is not decided here: it needs somebody dead (<see cref="Recovers"/>).
        /// </summary>
        public static bool Wanted(PvpAbilityView view, string key, PvpRealmAbilityWeights weights)
        {
            if (!view.InFight)
                return false;
            return key switch
            {
                SpeedOfSound => weights.SpeedOfSoundOnRetreat && view.Retreating ||
                                weights.SpeedOfSoundAtSlowed > 0 && view.MembersSlowed >= weights.SpeedOfSoundAtSlowed ||
                                weights.SpeedOfSoundToCloseBeyond > 0 && view.Order == PvpOrder.Push &&
                                view.FightersFromEnemy > weights.SpeedOfSoundToCloseBeyond &&
                                !(weights.SpeedOfSoundNotToCloseOnFewer && view.MembersAlive > 0 && view.EnemiesAlive < view.MembersAlive) ||
                                weights.SpeedOfSoundAtSoftMembersJumped > 0 && view.Order is PvpOrder.Extend or PvpOrder.Regroup &&
                                view.SoftMembersHitInMelee >= weights.SpeedOfSoundAtSoftMembersJumped,
                Concentration => view.Interrupted && (view.Archetype == PvpBuffArchetype.Healer
                    ? view.LowestOther < weights.ConcentrationHealerAtMemberBelow
                    : view.Archetype == PvpBuffArchetype.Caster && view.HitInMelee && view.Health > weights.ConcentrationCasterAtHealthAbove),
                Battery => view.Hurt >= weights.BatteryAtHurt && view.MissingHealthOfOthers >= weights.BatteryAtMissingHealth,
                Bunker or Barricade => view.MembersHitInMelee >= weights.BunkerAtMembersHitInMelee && view.Hurt >= weights.BunkerAtHurt,
                Aura => view.EnemyCastersOnUs >= weights.AuraAtEnemyCasters && view.Hurt >= 1,
                Melodies => view.Hurt >= weights.MelodiesAtHurt,
                FaithHealing => view.BadlyHurt >= weights.FaithHealingAtBadlyHurt,
                AvoidPain => view.HitInMelee && view.Health < weights.AvoidPainBelow,
                ArmorOfFaith => view.HitInMelee && view.Health < weights.ArmorOfFaithBelow,
                EmptyMind => !view.HitInMelee && view.Health < weights.EmptyMindBelow,
                _ => false,
            };
        }

        /// <summary>Whom Perfect Recovery raises: the nearest dead healer in reach, or else the nearest dead member; nobody outside a fight.</summary>
        public static T Recovers<T>(bool inFight, IEnumerable<(T Member, bool IsHealer, int Distance)> dead, int range) where T : class =>
            !inFight ? null : dead.Where(one => one.Distance <= range).OrderBy(one => one.IsHealer ? 0 : 1).ThenBy(one => one.Distance)
                .Select(one => one.Member).FirstOrDefault();

        private const int Reach = 1_500;

        private static bool Awake(GameLiving living) => living.IsAlive && !living.IsMezzed && !living.IsStunned;

        private static bool FighterOn(IReadOnlyList<GameLiving> enemies, GameLiving member) => enemies.Any(enemy =>
            Awake(enemy) && enemy.TargetObject == member && PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee && enemy.IsWithinRadius(member, 500));

        private static IReadOnlyList<GameLiving> EnemiesOf(GameBot bot) => (bot.Brain as AI.Brain.BotBrain)?.PvpEnemies;

        private static bool InFight(GameBot bot, IReadOnlyList<GameLiving> enemies) =>
            enemies != null && enemies.Any(enemy => enemy.IsAlive && bot.IsWithinRadius(enemy, PvpScrim.EngageRange));

        public static PvpAbilityView View(GameBot bot)
        {
            IReadOnlyList<GameLiving> enemies = EnemiesOf(bot);
            PvpRealmAbilityWeights weights = PvpWeights.Of(bot).Abilities;
            List<GameLiving> members = AtlasRAHelpers.GroupInReach(bot, Reach);
            List<GameLiving> others = members.Where(member => member != bot).ToList();
            bool inFight = InFight(bot, enemies);
            enemies ??= [];
            return new PvpAbilityView(inFight, bot.HealthPercent, FighterOn(enemies, bot), bot.IsBeingInterruptedByOther,
                members.Count(member => member.HealthPercent < weights.HurtBelow),
                members.Count(member => member.HealthPercent < weights.BadlyHurtBelow),
                others.Select(member => (int)member.HealthPercent).DefaultIfEmpty(100).Min(),
                others.Sum(member => member.MaxHealth - member.Health),
                members.Count(member => FighterOn(enemies, member)),
                members.Count(member => member.effectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedDebuff) ||
                                        member.effectListComponent.ContainsEffectForEffectType(eEffect.Snare)),
                enemies.Count(enemy => Awake(enemy) && PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Caster &&
                                       enemy.TargetObject is GameLiving aim && members.Contains(aim)),
                (bot.Brain as AI.Brain.BotBrain)?.PvpSquad is { } runs && (runs.RetreatsTo != null || runs.Leaving),
                PvpMember.ArchetypeOf(bot),
                (bot.Brain as AI.Brain.BotBrain)?.PvpSquad?.Order ?? PvpOrder.Engage,
                Middle(members.Where(PvpMember.IsFighter).Select(fighter => enemies.Where(enemy => enemy.IsAlive)
                    .Select(enemy => (int)fighter.GetDistanceTo(enemy)).DefaultIfEmpty(0).Min()).ToList()),
                members.Count(member => !PvpMember.IsFighter(member) && FighterOn(enemies, member)),
                bot.Group?.GetMembersInTheGroup().Count(member => member.IsAlive) ?? 1,
                enemies.Count(enemy => enemy.IsAlive));
        }

        private static int Middle(List<int> values)
        {
            if (values.Count == 0)
                return 0;
            values.Sort();
            return values[values.Count / 2];
        }

        /// <summary>The dead member this bot's Perfect Recovery would raise now; null when there is none.</summary>
        public static GameLiving Recovers(GameBot bot)
        {
            if (bot.Group == null || bot.IsCasting)
                return null;
            return Recovers(InFight(bot, EnemiesOf(bot)),
                bot.Group.GetMembersInTheGroup()
                    .Where(member => member != bot && !member.IsAlive && member.ObjectState == GameObject.eObjectState.Active &&
                                     member.CurrentRegionID == bot.CurrentRegionID &&
                                     member.TempProperties.GetProperty<GameLiving>("RESURRECT_CASTER") == null)
                    .Select(member => (member, PvpMember.RoleOf(member) == PvpRole.Heal, (int)bot.GetDistanceTo(member))),
                // Its reach as it is now: in six matches of 2026-11-22 a nearsighted Healer tried a body at 1,058 units
                // every second for twelve seconds, and the game refused each one.
                Math.Min(PvpWeights.Of(bot).Abilities.PerfectRecoveryRange, (int)(1_500 * bot.GetModified(eProperty.SpellRange) * 0.01)));
        }

        /// <summary>True when this 8v8 bot should press the ability now.</summary>
        public static bool Wanted(GameBot bot, string key)
        {
            // Asleep or stunned it can press none of them, and its Purge must have the turn.
            if (bot.IsMezzed || bot.IsStunned)
                return false;
            bool wanted = key == PerfectRecovery ? Recovers(bot) != null : Wanted(View(bot), key, PvpWeights.Of(bot).Abilities);
            if (wanted)
                PvpRecorder.Decision(bot, new { kind = "RealmAbility", spell = key.Replace("AtlasOF_", string.Empty) }, [], null);
            return wanted;
        }

        /// <summary>
        /// Presses the ability. Perfect Recovery is a spell on a target: the bot takes the dead member
        /// as its target for the cast and has its own back afterwards.
        /// </summary>
        public static void Use(GameBot bot, TimedRealmAbility ability)
        {
            if (ability.KeyName != PerfectRecovery)
            {
                ability.Execute(bot);
                return;
            }
            if (Recovers(bot) is not { } dead)
                return;
            GameObject before = bot.TargetObject;
            bot.TargetObject = dead;
            ability.Execute(bot);
            bot.TargetObject = before;
        }
    }
}

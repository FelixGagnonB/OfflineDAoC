using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>A named 8v8 group: eight builds of one realm.</summary>
    public sealed record PvpGroupTemplate(string Id, eRealm Realm, IReadOnlyList<string> BuildIds);

    /// <summary>
    /// The level-50 builds and meta groups of the 8v8 mode (docs/PVP-8V8-ROADMAP.md, M1.1).
    /// One line per build so the owner can retune a spec in place; the unit tests check that
    /// every build fits its class's specialization points.
    /// </summary>
    public static class PvpBuildCatalog
    {
        private static PvpSpecLine L(string spec, int level) => new(spec, level);

        private static PvpBuild Caster(string id, eCharacterClass c, eSpecType type, params PvpSpecLine[] lines) =>
            new(id, c, type, eObjectType.Staff, eObjectType.GenericItem, false, lines);

        private static PvpBuild Melee(string id, eCharacterClass c, eSpecType type, eObjectType one, eObjectType two,
            bool twoHanded, params PvpSpecLine[] lines) => new(id, c, type, one, two, twoHanded, lines);

        // There are no buffbots, so one member of each group is its buffer. The owner's standard
        // (2026-11-16) is the second-best ("yellow") Str/Con and Dex/Qui on every member, which
        // keeps the buffer a useful healer and costs less concentration than the best ("red").
        // Spell levels, best / second best (spell table read 2026-11-16):
        //   Cleric Enhancement  Dex/Qui 50 / 40, Str/Con 46 / 35, armour 44 / 34, acuity 42 / 31
        //   Druid Nurture       Dex/Qui 49 / 39, Str/Con 44 / 34, haste 50 / 40, acuity 42 / 31
        //   Shaman Augmentation Dex/Qui 47 / 37, Str/Con 46 / 36, acuity 42 / 31, endurance 42
        // So 42 in the line gives yellow Dex/Qui and Str/Con and the best acuity. The Shaman is a
        // buffer first and takes 47 for the best of all three.
        // The single-stat and base armour buffs are baseline: any level-50 Cleric, Druid, Healer
        // or Shaman casts the best one whatever its spec.
        public static readonly IReadOnlyList<PvpBuild> Builds =
        [
            // ---- Albion
            Melee("cleric-rejuv", eCharacterClass.Cleric, eSpecType.RejuvCleric, eObjectType.CrushingWeapon, eObjectType.GenericItem, false,
                L(Specs.Rejuvenation, 46), L(Specs.Enhancement, 28), L(Specs.Smite, 4)),
            Melee("cleric-enhance", eCharacterClass.Cleric, eSpecType.EnhanceCleric, eObjectType.CrushingWeapon, eObjectType.GenericItem, false,
                L(Specs.Enhancement, 42), L(Specs.Rejuvenation, 33), L(Specs.Smite, 7)),
            Caster("sorcerer-mind", eCharacterClass.Sorcerer, eSpecType.MindSorc,
                L(Specs.Mind_Magic, 40), L(Specs.Body_Magic, 35), L(Specs.Matter_Magic, 8)),
            Melee("minstrel", eCharacterClass.Minstrel, eSpecType.Instrument, eObjectType.SlashingWeapon, eObjectType.GenericItem, false,
                L(Specs.Instruments, 47), L(Specs.Slash, 44), L(Specs.Stealth, 13)),
            Caster("theurgist-air", eCharacterClass.Theurgist, eSpecType.AirTheur,
                L(Specs.Wind_Magic, 45), L(Specs.Earth_Magic, 28), L(Specs.Cold_Magic, 9)),
            Melee("paladin", eCharacterClass.Paladin, eSpecType.OneHandAndShield, eObjectType.SlashingWeapon, eObjectType.TwoHandedWeapon, false,
                L(Specs.Chants, 46), L(Specs.Shields, 42), L(Specs.Slash, 39), L(Specs.Parry, 20)),
            Melee("armsman-polearm", eCharacterClass.Armsman, eSpecType.TwoHandHybrid, eObjectType.SlashingWeapon, eObjectType.PolearmWeapon, true,
                L(Specs.Polearms, 50), L(Specs.Shields, 42), L(Specs.Slash, 39), L(Specs.Parry, 6)),
            // The three-line utility Cabalist (owner, 2026-11-18). Each level is where a spell is
            // gained (spell table read that day): Body 34 the disease, Spirit 33 the 30% body
            // resistance debuff (and the best simulacrum, at 32), Matter 25 the 45% nearsight,
            // which Albion's group had none of. The lifedrain it kills with is baseline.
            Caster("cabalist-tri", eCharacterClass.Cabalist, eSpecType.BodyCab,
                L(Specs.Body_Magic, 34), L(Specs.Spirit_Magic, 33), L(Specs.Matter_Magic, 25)),
            Melee("mercenary", eCharacterClass.Mercenary, eSpecType.DualWield, eObjectType.SlashingWeapon, eObjectType.SlashingWeapon, false,
                L(Specs.Dual_Wield, 50), L(Specs.Slash, 50), L(Specs.Parry, 28)),
            Caster("wizard-fire", eCharacterClass.Wizard, eSpecType.FireWiz,
                L(Specs.Fire_Magic, 48), L(Specs.Earth_Magic, 24), L(Specs.Cold_Magic, 6)),

            Melee("buffbot-cleric", eCharacterClass.Cleric, eSpecType.EnhanceCleric, eObjectType.CrushingWeapon, eObjectType.GenericItem, false,
                L(Specs.Enhancement, 50), L(Specs.Rejuvenation, 20), L(Specs.Smite, 4)),

            // ---- Midgard
            Melee("buffbot-shaman", eCharacterClass.Shaman, eSpecType.AugShaman, eObjectType.Hammer, eObjectType.GenericItem, false,
                L(Specs.Augmentation, 48), L(Specs.Subterranean, 24), L(Specs.Mending, 6)),
            Melee("healer-pac", eCharacterClass.Healer, eSpecType.PacHealer, eObjectType.Hammer, eObjectType.GenericItem, false,
                L(Specs.Pacification, 44), L(Specs.Mending, 31), L(Specs.Augmentation, 4)),
            Melee("healer-mend", eCharacterClass.Healer, eSpecType.MendHealer, eObjectType.Hammer, eObjectType.GenericItem, false,
                L(Specs.Mending, 40), L(Specs.Augmentation, 36), L(Specs.Pacification, 4)),
            Melee("shaman-aug", eCharacterClass.Shaman, eSpecType.AugShaman, eObjectType.Hammer, eObjectType.GenericItem, false,
                L(Specs.Augmentation, 47), L(Specs.Subterranean, 26), L(Specs.Mending, 5)),
            Melee("skald", eCharacterClass.Skald, eSpecType.Mid, eObjectType.Sword, eObjectType.GenericItem, false,
                L(Specs.Battlesongs, 46), L(Specs.Sword, 44), L(Specs.Parry, 17)),
            Caster("runemaster-rune", eCharacterClass.Runemaster, eSpecType.RuneRune,
                L(Specs.Runecarving, 47), L(Specs.Suppression, 26), L(Specs.Darkness, 5)),
            Melee("savage", eCharacterClass.Savage, eSpecType.DualWield, eObjectType.HandToHand, eObjectType.HandToHand, false,
                L(Specs.Savagery, 49), L(Specs.HandToHand, 44), L(Specs.Parry, 4)),
            Melee("berserker", eCharacterClass.Berserker, eSpecType.LeftAxe, eObjectType.Axe, eObjectType.Axe, false,
                L(Specs.Left_Axe, 50), L(Specs.Axe, 44), L(Specs.Parry, 37)),
            Melee("warrior", eCharacterClass.Warrior, eSpecType.Mid, eObjectType.Hammer, eObjectType.GenericItem, false,
                L(Specs.Hammer, 50), L(Specs.Shields, 42), L(Specs.Parry, 39)),
            Caster("spiritmaster-dark", eCharacterClass.Spiritmaster, eSpecType.DarkSpirit,
                L(Specs.Darkness, 47), L(Specs.Suppression, 26), L(Specs.Summoning, 5)),

            // ---- Hibernia
            Melee("buffbot-druid", eCharacterClass.Druid, eSpecType.NurtureDruid, eObjectType.Blunt, eObjectType.GenericItem, false,
                L(Specs.Nurture, 50), L(Specs.Regrowth, 20), L(Specs.Nature, 4)),
            Melee("druid-regrowth", eCharacterClass.Druid, eSpecType.RegrowthDruid, eObjectType.Blunt, eObjectType.GenericItem, false,
                L(Specs.Regrowth, 44), L(Specs.Nurture, 30), L(Specs.Nature, 8)),
            Melee("druid-nurture", eCharacterClass.Druid, eSpecType.NurtureDruid, eObjectType.Blunt, eObjectType.GenericItem, false,
                L(Specs.Nurture, 42), L(Specs.Regrowth, 33), L(Specs.Nature, 7)),
            Melee("bard", eCharacterClass.Bard, eSpecType.Instrument, eObjectType.Blades, eObjectType.GenericItem, false,
                L(Specs.Music, 46), L(Specs.Nurture, 43), L(Specs.Regrowth, 19)),
            Melee("hero", eCharacterClass.Hero, eSpecType.OneHandAndShield, eObjectType.Blades, eObjectType.LargeWeapons, false,
                L(Specs.Blades, 50), L(Specs.Shields, 42), L(Specs.Parry, 39)),
            Melee("blademaster", eCharacterClass.Blademaster, eSpecType.DualWield, eObjectType.Blades, eObjectType.Blades, false,
                L(Specs.Celtic_Dual, 50), L(Specs.Blades, 44), L(Specs.Parry, 37)),
            Caster("eldritch-light", eCharacterClass.Eldritch, eSpecType.LightEld,
                L(Specs.Light, 45), L(Specs.Mana, 29), L(Specs.Void, 6)),
            Caster("enchanter-light", eCharacterClass.Enchanter, eSpecType.LightEnchanter,
                L(Specs.Light, 45), L(Specs.Enchantments, 29), L(Specs.Mana, 6)),
            Melee("warden", eCharacterClass.Warden, eSpecType.NurtureWarden, eObjectType.Blades, eObjectType.GenericItem, false,
                L(Specs.Nurture, 34), L(Specs.Blades, 39), L(Specs.Regrowth, 28), L(Specs.Parry, 29)),
            Melee("champion", eCharacterClass.Champion, eSpecType.TwoHanded, eObjectType.Blades, eObjectType.LargeWeapons, true,
                L(Specs.Valor, 50), L(Specs.Large_Weapons, 50), L(Specs.Parry, 28)),
            Caster("mentalist-light", eCharacterClass.Mentalist, eSpecType.LightMenta,
                L(Specs.Light, 45), L(Specs.Mana, 29), L(Specs.Mentalism, 6)),
        ];

        /// <summary>
        /// The buffbot each group gets at its spawn point (owner's rule, 2026-11-16): it gives
        /// every base and specialization buff, so the group's own buffers start with all their
        /// concentration free and spend it only on members who died and came back.
        /// </summary>
        public static PvpBuild BuffbotFor(eRealm realm) => Find(realm switch
        {
            eRealm.Albion => "buffbot-cleric",
            eRealm.Midgard => "buffbot-shaman",
            _ => "buffbot-druid",
        });

        /// <summary>The owner's main groups (set 2026-11-16).</summary>
        public static readonly IReadOnlyList<PvpGroupTemplate> Groups =
        [
            new("alb-main", eRealm.Albion,
                ["cleric-rejuv", "cleric-enhance", "sorcerer-mind", "minstrel", "theurgist-air", "paladin", "armsman-polearm", "cabalist-tri"]),
            new("mid-main", eRealm.Midgard,
                ["healer-pac", "healer-mend", "shaman-aug", "skald", "runemaster-rune", "savage", "berserker", "warrior"]),
            new("hib-main", eRealm.Hibernia,
                ["druid-regrowth", "druid-nurture", "bard", "hero", "blademaster", "blademaster", "eldritch-light", "enchanter-light"]),
        ];

        private static readonly Dictionary<string, PvpBuild> _byId =
            Builds.ToDictionary(build => build.Id, StringComparer.OrdinalIgnoreCase);

        public static PvpBuild Find(string id) =>
            id != null && _byId.TryGetValue(id, out PvpBuild build) ? build : null;

        public static IEnumerable<PvpBuild> ForClass(eCharacterClass characterClass) =>
            Builds.Where(build => build.Class == characterClass);

        public static PvpGroupTemplate FindGroup(string id) =>
            Groups.FirstOrDefault(group => string.Equals(group.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>One race per class, so two bots of a build have the same base stats.</summary>
        public static eRace RaceOf(eCharacterClass characterClass) => characterClass switch
        {
            eCharacterClass.Armsman or eCharacterClass.Paladin or eCharacterClass.Mercenary => eRace.Highlander,
            eCharacterClass.Cleric or eCharacterClass.Minstrel => eRace.Briton,
            eCharacterClass.Sorcerer or eCharacterClass.Theurgist or eCharacterClass.Cabalist or eCharacterClass.Wizard => eRace.Avalonian,
            eCharacterClass.Healer or eCharacterClass.Runemaster or eCharacterClass.Spiritmaster => eRace.Norseman,
            eCharacterClass.Shaman => eRace.Kobold,
            eCharacterClass.Skald or eCharacterClass.Savage or eCharacterClass.Berserker or eCharacterClass.Warrior => eRace.Troll,
            eCharacterClass.Druid or eCharacterClass.Bard or eCharacterClass.Hero or eCharacterClass.Blademaster or
                eCharacterClass.Champion or eCharacterClass.Warden => eRace.Celt,
            eCharacterClass.Eldritch or eCharacterClass.Enchanter or eCharacterClass.Mentalist => eRace.Elf,
            _ => eRace.Unknown,
        };

        public static eRealm RealmOf(eCharacterClass characterClass) => characterClass switch
        {
            eCharacterClass.Armsman or eCharacterClass.Cabalist or eCharacterClass.Cleric or eCharacterClass.Friar or
            eCharacterClass.Infiltrator or eCharacterClass.Mercenary or eCharacterClass.Minstrel or
            eCharacterClass.Necromancer or eCharacterClass.Paladin or eCharacterClass.Reaver or eCharacterClass.Scout or
            eCharacterClass.Sorcerer or eCharacterClass.Theurgist or eCharacterClass.Wizard => eRealm.Albion,
            eCharacterClass.Berserker or eCharacterClass.Bonedancer or eCharacterClass.Healer or eCharacterClass.Hunter or
            eCharacterClass.Runemaster or eCharacterClass.Savage or eCharacterClass.Shadowblade or
            eCharacterClass.Shaman or eCharacterClass.Skald or eCharacterClass.Spiritmaster or eCharacterClass.Thane or
            eCharacterClass.Warrior => eRealm.Midgard,
            eCharacterClass.Animist or eCharacterClass.Bard or eCharacterClass.Blademaster or eCharacterClass.Champion or
            eCharacterClass.Druid or eCharacterClass.Eldritch or eCharacterClass.Enchanter or eCharacterClass.Hero or
            eCharacterClass.Mentalist or eCharacterClass.Nightshade or eCharacterClass.Ranger or
            eCharacterClass.Valewalker or eCharacterClass.Warden => eRealm.Hibernia,
            _ => eRealm.None,
        };
    }
}

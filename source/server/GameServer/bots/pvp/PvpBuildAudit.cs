using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.GS.Styles;

namespace DOL.GS
{
    /// <summary>
    /// Describes what a bot created from an 8v8 build really ends up with on a running server:
    /// trained lines, spells by the category the AI files them under, styles, abilities, pools
    /// and gear. Unit tests cannot see this because the class data lives in the database.
    /// </summary>
    public static class PvpBuildAudit
    {
        public sealed record SpecLine(string Name, int Level, int Planned);

        public sealed record Report(
            string BuildId, string Class, string Realm, int Level, bool SpecMatchesBuild, int UnspentSpecPoints,
            IReadOnlyList<SpecLine> Specs, IReadOnlyDictionary<string, int> Pools,
            IReadOnlyDictionary<string, string> HealSlots, IReadOnlyDictionary<string, IReadOnlyList<string>> Spells,
            IReadOnlyDictionary<string, IReadOnlyList<string>> Styles, IReadOnlyList<string> Abilities,
            IReadOnlyList<string> Equipment, IReadOnlyDictionary<string, int> Resists,
            IReadOnlyDictionary<string, int> TemplateTotals, IReadOnlyList<string> TemplateShortfalls,
            IReadOnlyList<string> TemplateItems);

        public static Report Describe(GameBot bot)
        {
            PvpBuild build = bot.PvpBuild;
            Dictionary<string, int> trained = bot.GetSpecList()
                .Where(spec => spec.Trainable)
                .ToDictionary(spec => spec.KeyName, spec => spec.Level, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> planned = build.Lines
                .ToDictionary(line => line.Spec, line => line.Level, StringComparer.OrdinalIgnoreCase);
            List<SpecLine> specs = trained.Keys.Union(planned.Keys, StringComparer.OrdinalIgnoreCase)
                .Select(name => new SpecLine(name, trained.GetValueOrDefault(name, -1), planned.GetValueOrDefault(name, 1)))
                .OrderByDescending(line => line.Level).ThenBy(line => line.Name).ToList();
            // A line the class does not have shows as -1; an untrained class line stays at 1.
            bool matches = specs.All(line => line.Level == line.Planned);

            return new Report(
                build.Id, ((eCharacterClass)bot.ClassId).ToString(), RealmName(bot.Realm), bot.Level, matches,
                bot.UnspentSpecPoints, specs,
                new Dictionary<string, int>
                {
                    ["Health"] = bot.MaxHealth, ["Power"] = bot.MaxMana, ["Endurance"] = bot.MaxEndurance,
                    ["Concentration"] = bot.MaxConcentration,
                    // Which weapons are in hand decides which weapon bonuses count.
                    ["ActiveWeaponSlot"] = (int)bot.ActiveWeaponSlot, ["VisibleWeaponSlots"] = bot.VisibleActiveWeaponSlots,
                    ["ItemStrength"] = bot.ItemBonus[eProperty.Strength], ["ItemConstitution"] = bot.ItemBonus[eProperty.Constitution],
                    ["ItemDexterity"] = bot.ItemBonus[eProperty.Dexterity], ["ItemHits"] = bot.ItemBonus[eProperty.MaxHealth],
                    ["Strength"] = bot.GetModified(eProperty.Strength), ["Constitution"] = bot.GetModified(eProperty.Constitution),
                    ["Dexterity"] = bot.GetModified(eProperty.Dexterity), ["Quickness"] = bot.GetModified(eProperty.Quickness),
                    ["Intelligence"] = bot.GetModified(eProperty.Intelligence), ["Piety"] = bot.GetModified(eProperty.Piety),
                    ["Empathy"] = bot.GetModified(eProperty.Empathy), ["Charisma"] = bot.GetModified(eProperty.Charisma),
                },
                new Dictionary<string, string>
                {
                    ["Big"] = Spell(bot.HealBig), ["Efficient"] = Spell(bot.HealEfficient), ["Group"] = Spell(bot.HealGroup),
                    ["Instant"] = Spell(bot.HealInstant), ["InstantGroup"] = Spell(bot.HealInstantGroup),
                    ["OverTime"] = Spell(bot.HealOverTime), ["CureMezz"] = Spell(bot.CureMezz),
                    ["CureDisease"] = Spell(bot.CureDisease), ["CurePoison"] = Spell(bot.CurePoison),
                    ["Resurrection"] = Spell(bot.ResurrectionSpell),
                }.Where(pair => pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value),
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["CrowdControl"] = Spells(bot.CrowdControlSpells), ["Bolts"] = Spells(bot.BoltSpells),
                    ["Harmful"] = Spells(bot.HarmfulSpells), ["InstantHarmful"] = Spells(bot.InstantHarmfulSpells),
                    ["Misc"] = Spells(bot.MiscSpells), ["InstantMisc"] = Spells(bot.InstantMiscSpells),
                    ["Heal"] = Spells(bot.HealSpells),
                },
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["Anytime"] = Styles(bot.StylesAnytime), ["Chain"] = Styles(bot.StylesChain),
                    ["Defensive"] = Styles(bot.StylesDefensive), ["Back"] = Styles(bot.StylesBack),
                    ["Side"] = Styles(bot.StylesSide), ["Front"] = Styles(bot.StylesFront),
                    ["Taunt"] = Styles(bot.StylesTaunt), ["Detaunt"] = Styles(bot.StylesDetaunt),
                    ["Shield"] = Styles(bot.StylesShield),
                },
                bot.GetAllAbilities().Cast<Ability>()
                    .Select(ability => ability.Level > 0 ? $"{ability.KeyName} {ability.Level}" : ability.KeyName)
                    .OrderBy(name => name).ToList(),
                (bot.Inventory?.EquippedItems ?? Enumerable.Empty<DbInventoryItem>())
                    .OrderBy(item => item.SlotPosition)
                    .Select(item => $"{(eInventorySlot)item.SlotPosition}: {item.Name} ({(eObjectType)item.Object_Type}, level {item.Level}, quality {item.Quality}, dps/af {item.DPS_AF}, spd/abs {item.SPD_ABS})")
                    .ToList(),
                ResistProperties.ToDictionary(property => Name(property).Replace("Resist_", ""), property => bot.GetModified(property)),
                (bot.PvpTemplate?.Totals ?? new Dictionary<eProperty, int>())
                    .ToDictionary(pair => Name(pair.Key), pair => pair.Value),
                (bot.PvpTemplate?.Shortfalls ?? [])
                    .Select(gap => $"{Name(gap.Property)}: {gap.Reached} of {gap.Target}").ToList(),
                (bot.PvpTemplate?.Items ?? [])
                    .Select(item => $"{item.Slot} ({item.ImbuePoints}/{item.Capacity}): " +
                        string.Join(", ", item.Bonuses.Select(bonus => $"{Name(bonus.Property)} {bonus.Value}"))).ToList());
        }

        private static readonly eProperty[] ResistProperties =
        [
            eProperty.Resist_Crush, eProperty.Resist_Slash, eProperty.Resist_Thrust, eProperty.Resist_Body,
            eProperty.Resist_Cold, eProperty.Resist_Heat, eProperty.Resist_Energy, eProperty.Resist_Matter, eProperty.Resist_Spirit,
        ];

        // Three properties share their value with a range marker, which ToString() would print.
        private static string Name(eProperty property) => property switch
        {
            eProperty.Strength => "Strength",
            eProperty.Resist_Body => "Resist_Body",
            eProperty.Resist_Thrust => "Resist_Thrust",
            _ => property.ToString(),
        };

        // eRealm.Albion shares its value with a range marker, so ToString() prints the marker.
        private static string RealmName(eRealm realm) => realm switch
        {
            eRealm.Albion => "Albion",
            eRealm.Midgard => "Midgard",
            eRealm.Hibernia => "Hibernia",
            _ => "None",
        };

        private static string Spell(Spell spell) =>
            spell == null ? null : $"{spell.Name} (level {spell.Level}, {spell.SpellType}, value {spell.Value:0.#}, damage {spell.Damage:0.#})";

        private static IReadOnlyList<string> Spells(IEnumerable<Spell> spells) =>
            (spells ?? []).Where(spell => spell != null).OrderByDescending(spell => spell.Level)
                .Select(Spell).ToList();

        private static IReadOnlyList<string> Styles(IEnumerable<Style> styles) =>
            (styles ?? []).Where(style => style != null).OrderByDescending(style => style.Level)
                .Select(style => $"{style.Name} (level {style.Level}, {style.Spec})").ToList();
    }
}

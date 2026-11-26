using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// Midgard's endurance buff in an 8v8 group is its own Shaman's.
    /// <para>The owner, 2026-11-25: "can we make sure, enemies without endurance (for example, killing the albion Paladin)
    /// should not out run or keep the same distance with a chanser than sprint or has speed? I feel like sometime, 2-3
    /// enemies are running away and we can't keep up. With midgard, it might be a shaman, which would explain they can keep
    /// sprinting". A sprint takes five endurance a second from a bot as from a player
    /// (<see cref="PvpPositionWeights.SprintCostPerSecond"/>). Albion gets five back from its Paladin's chant and Hibernia
    /// from its Bard's song, both gone five seconds after the singer is. Midgard's is the Shaman's concentration buff
    /// (Superior Earth Invigoration, five a second, within 1,500 units of the Shaman), and until now every Midgard group had
    /// it from the buffbot at its keep, which neither dies nor has a range: in the owner's evening of 2026-11-25 every
    /// living Midgard bot had it in 98% of its seconds, a Runemaster sprinting 27 seconds with its endurance at 95 and no
    /// Shaman alive in its group.</para>
    /// <para>Now the group's Shaman gives it, one member a second (a cast each, for a player), to whoever of its group is
    /// within the spell's range and has none; the game ends it beyond 1,500 units of the Shaman and gives it back within;
    /// and when the Shaman dies it is taken from the group (the game does that for a player who dies, not for a bot). A
    /// group with no Shaman has none.</para>
    /// </summary>
    public static class PvpEnduranceBuff
    {
        /// <summary>The spell line the buff is in; a Shaman has the spells of it up to its Augmentation.</summary>
        public const string Line = "Shaman Augmentation Spec";

        /// <summary>A Shaman gives it to one member in this time.</summary>
        public const int EveryMilliseconds = 1_000;

        private const string LastGiven = "PvpEnduranceBuffAt";

        public enum Does { Nothing, Gives, Takes }

        /// <summary>
        /// What a Shaman does about one member of its group. Down, it takes back what it gave. Up, it gives the buff to a
        /// living member within the spell's range that has none from a living Shaman of the group, unless it cannot cast
        /// (it is asleep, stunned, or in the middle of a spell).
        /// </summary>
        public static Does For(bool shamanIsUp, bool shamanCanCast, bool memberIsUp, bool memberHasItFromThisOne, bool memberHasItFromOneThatIsUp,
            int apart, int reach) =>
            !shamanIsUp ? memberHasItFromThisOne ? Does.Takes : Does.Nothing
            : memberIsUp && !memberHasItFromOneThatIsUp && shamanCanCast && apart <= reach ? Does.Gives
            : Does.Nothing;

        /// <summary>The best endurance buff held by concentration that a Shaman with this much Augmentation has.</summary>
        public static Spell Best(int augmentation, IEnumerable<Spell> line) =>
            (line ?? []).Where(spell => spell != null && spell.SpellType == eSpellType.EnduranceRegenBuff && spell.Concentration > 0 && spell.Level <= augmentation)
                .OrderByDescending(spell => spell.Value).FirstOrDefault();

        /// <summary>Whether this bot is an 8v8 group's Shaman (not the buffbot at the keep).</summary>
        public static bool IsItsShaman(GameBot bot) =>
            bot is { PvpBuild: not null, IsPvpBuffbot: false } && bot.CharacterClass?.ID == (int)eCharacterClass.Shaman;

        /// <summary>
        /// Once a second for a group: each of its Shamans gives the buff to one member that lacks it, or, dead, takes back
        /// what it gave. Returns how many buffs were given or taken.
        /// </summary>
        public static int Keep(IReadOnlyList<GameLiving> group, long now)
        {
            if (group == null)
                return 0;
            List<GameBot> shamans = group.OfType<GameBot>().Where(IsItsShaman).ToList();
            if (shamans.Count == 0)
                return 0;
            int done = 0;
            foreach (GameBot shaman in shamans)
            {
                if (now - shaman.TempProperties.GetProperty<long>(LastGiven) < EveryMilliseconds)
                    continue;
                bool up = shaman.IsAlive && shaman.ObjectState == GameObject.eObjectState.Active;
                Spell spell = up ? Best(shaman.GetBaseSpecLevel(Specs.Augmentation), SkillBase.GetSpellList(Line)) : null;
                if (up && spell == null)
                    continue;
                bool canCast = up && !shaman.IsCrowdControlled && !shaman.IsCasting;
                foreach (GameLiving member in group)
                {
                    if (member == null || member.ObjectState != GameObject.eObjectState.Active)
                        continue;
                    List<ECSGameSpellEffect> held = member.effectListComponent.GetSpellEffects(eEffect.EnduranceRegenBuff)
                        .Where(effect => effect.SpellHandler?.Spell is { Concentration: > 0 } && effect.SpellHandler.Caster is GameBot giver && shamans.Contains(giver))
                        .ToList();
                    List<ECSGameSpellEffect> own = held.Where(effect => effect.SpellHandler.Caster == shaman).ToList();
                    bool fromOneThatIsUp = held.Any(effect => effect.SpellHandler.Caster is { IsAlive: true });
                    Does does = For(up, canCast, member.IsAlive, own.Count > 0, fromOneThatIsUp,
                        up ? shaman.GetDistanceTo(member) : 0, spell?.Range ?? 0);
                    if (does == Does.Takes)
                    {
                        foreach (ECSGameSpellEffect effect in own)
                            effect.End();
                        done += own.Count;
                    }
                    else if (does == Does.Gives)
                    {
                        SpellLine line = SkillBase.GetSpellLine(Line) ?? SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);
                        ScriptMgr.CreateSpellHandler(shaman, spell, line)?.StartSpell(member);
                        shaman.TempProperties.SetProperty(LastGiven, now);
                        done++;
                        break;   // one member a second
                    }
                }
            }
            return done;
        }
    }
}

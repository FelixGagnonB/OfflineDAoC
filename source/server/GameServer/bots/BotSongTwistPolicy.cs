using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;

namespace DOL.GS
{
    /// <summary>One native song source at a time; never extend its child effects or skip recast timers.</summary>
    public static class BotSongTwistPolicy
    {
        public readonly record struct Song(int Id, int CastMilliseconds, int CooldownMilliseconds, long RemainingMilliseconds);

        public static bool IsManagedSong(GameLiving living, Spell spell)
        {
            if (living is not GameBot bot || bot.CharacterClass == null ||
                spell == null || !spell.IsPulsing || spell.IsHarmful || spell.IsFocus)
                return false;
            eCharacterClass characterClass = (eCharacterClass)bot.CharacterClass.ID;
            if (BotBrain.IsClassicSongClass(characterClass))
                return BotBrain.IsMaintainableClassBuff(spell);
            // Tank chants use a separate selector: never send Paladin/Warden
            // through the performer code that drops songs for solo melee.
            return spell.IsInstantCast && !spell.NeedInstrument && (characterClass switch
            {
                eCharacterClass.Paladin => spell.SpellType is eSpellType.EnduranceRegenBuff or
                    eSpellType.SpecArmorFactorBuff or eSpellType.DamageAdd or eSpellType.CombatHeal,
                eCharacterClass.Warden => spell.SpellType is eSpellType.SpeedEnhancement or
                    eSpellType.Bladeturn or eSpellType.DamageAdd,
                _ => false
            });
        }

        /// <summary>The one chant a solo Paladin holds: damage in a fight, the combat heal when hurt, endurance otherwise.</summary>
        public static eSpellType SoloPaladinAnchor(bool fighting, int healthPercent) =>
            !fighting ? eSpellType.EnduranceRegenBuff : healthPercent < 70 ? eSpellType.CombatHeal : eSpellType.DamageAdd;

        public static bool IsMobileSong(GameLiving living, Spell spell)
        {
            return IsManagedSong(living, spell) &&
                (spell.IsInstantCast || spell.NeedInstrument || spell.MoveCast);
        }

        // Only temporary player companions use this travel pool. Bards have
        // a real endurance song; Skalds and Minstrels do not, so their useful
        // second travel song is health regeneration. Power and combat chants
        // must not take the speed anchor while the owner is moving.
        /// <summary>What an 8v8 Bard holds when its group has two of them; AsOne for the Bard of a group with one, which plays as it did.</summary>
        public enum PvpBardSong
        {
            AsOne,
            Speed,
            Endurance,
            Nothing,
        }

        /// <summary>
        /// Two Bards share the songs. The owner, 2026-11-24, with the group of two Bards: "we
        /// should be roaming at speed 6: 1 bard has speed up and the other has endurance. During
        /// a retreat one could try to play speed has one should already have endurance up. If the
        /// endurance bard dies, then the other has to play it if possilbe, high priority". Until
        /// then each of the two played as the Bard of a group with one: both speed on the road
        /// and on a retreat, both endurance in a fight.
        ///
        /// With both alive, the first (by name) holds speed, on the road, on the way in and on
        /// the run; the second holds endurance all the time. The one left of two holds
        /// endurance whenever an enemy is known, in a fight, on the run and at rest, and speed
        /// only on an empty road.
        ///
        /// In a fight the first holds endurance too, begun as the Bard of a group with one
        /// begins it (between its mezzes, PvpWeights.SongInFightEverySeconds). Until 2026-11-26
        /// it played nothing there and its speed song ran on, which does nothing for anybody
        /// who gives or takes blows. The owner, of a fight of four minutes that was lost: "We
        /// had no endurance song up, we were out of endurance most of the fight". The song
        /// reaches about 2,000 units and the fight was 3,000 wide: the player was without it
        /// for 115 of 250 seconds, 53 of them within 1,500 of the Bard that held the speed, and
        /// 16 more went by between the death of the one and the song of the other.
        /// </summary>
        /// <param name="placeAmongTheAlive">0 for the first of the group's living Bards by name.</param>
        /// <param name="fightingAndNotOnTheRun">It is in combat and its group neither chases, retreats nor has broken off.</param>
        /// <param name="onTheRun">Its group chases, retreats behind its player or has broken off.</param>
        public static PvpBardSong TwoBards(int bardsOfTheGroup, int bardsAlive, int placeAmongTheAlive, bool fightingAndNotOnTheRun, bool onTheRun, bool enemyKnownOrResting) =>
            bardsOfTheGroup < 2 ? PvpBardSong.AsOne
            : bardsAlive >= 2 ? placeAmongTheAlive > 0 || fightingAndNotOnTheRun ? PvpBardSong.Endurance : PvpBardSong.Speed
            : fightingAndNotOnTheRun || onTheRun || enemyKnownOrResting ? PvpBardSong.Endurance : PvpBardSong.Speed;

        public static bool IsCompanionTravelSong(eCharacterClass characterClass, eSpellType type) =>
            characterClass switch
            {
                eCharacterClass.Bard => type is eSpellType.SpeedEnhancement or eSpellType.EnduranceRegenBuff,
                eCharacterClass.Skald or eCharacterClass.Minstrel =>
                    type is eSpellType.SpeedEnhancement or eSpellType.HealthRegenBuff,
                _ => false
            };

        // Only one helpful pulse source is legal. Generic buff selection must
        // not independently replace a tank's anchor with a resist/endurance
        // pulse; those are deliberately not part of the chosen core rotation.
        public static bool IsReservedPulse(GameLiving living, Spell spell) =>
            IsManagedSong(living, spell) ||
            living is GameBot bot && bot.CharacterClass != null &&
            (eCharacterClass)bot.CharacterClass.ID is eCharacterClass.Paladin or eCharacterClass.Warden &&
            spell != null && spell.IsPulsing && !spell.IsHarmful && !spell.IsFocus &&
            BotBrain.IsMaintainableClassBuff(spell);

        public static bool HasMobileSongCast(GameLiving living)
        {
            if (living is not GameBot bot || bot.CharacterClass == null)
                return false;

            CastingComponent casting = living.castingComponent;
            if (casting == null) return false;
            Spell active = casting.SpellHandler?.Spell;
            Spell queued = casting.QueuedSpellHandler?.Spell;
            bool pending = casting.TryPeekPendingSpell(out Spell requested);
            // Never use a song to authorize movement during an ordinary heal,
            // hostile spell, or unknown ability request queued alongside it.
            return (active != null || queued != null || pending) &&
                (active == null || IsMobileSong(living, active)) &&
                (queued == null || IsMobileSong(living, queued)) &&
                (!pending || IsMobileSong(living, requested));
        }

        public static eSpellType WardenAnchor(bool traveling, bool combat, bool grouped, bool hasBladeturn)
        {
            if (traveling && !combat) return eSpellType.SpeedEnhancement;
            if (hasBladeturn && (combat || grouped)) return eSpellType.Bladeturn;
            return eSpellType.DamageAdd;
        }

        public static int Choose(int primaryId, int activeId, IReadOnlyList<Song> songs)
        {
            Song primary = songs.FirstOrDefault(song => song.Id == primaryId);
            if (primary.Id == 0) return 0;
            // Return to the anchor immediately after the secondary takes. In
            // particular Skald must not leave speed while its 8s reuse is up.
            if (activeId != primaryId)
                return primary.CooldownMilliseconds <= 0 ? primaryId : 0;
            if (primary.CooldownMilliseconds > 0) return 0;

            foreach (Song secondary in songs.Where(song => song.Id != primaryId && song.CooldownMilliseconds <= 0)
                         .OrderBy(song => song.RemainingMilliseconds))
            {
                int roundTrip = Math.Max(0, secondary.CastMilliseconds) + Math.Max(0, primary.CastMilliseconds) + 1000;
                if (primary.RemainingMilliseconds > roundTrip && secondary.RemainingMilliseconds <= roundTrip)
                    return secondary.Id;
            }
            return 0;
        }
    }
}

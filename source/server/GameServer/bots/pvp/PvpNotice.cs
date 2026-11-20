using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DOL.GS
{
    /// <summary>What a bot does not know as exactly as the server does (docs/PVP-8V8-ROADMAP.md, M10.1).</summary>
    public sealed class PvpHumanWeights
    {
        /// <summary>
        /// A bot goes on thinking an enemy immune to mez or to stun for a while after the immunity
        /// has ended: between these two numbers of seconds, drawn each time, for each bot and each
        /// enemy. The owner, 2026-11-20: "I think the bots are too optimized with crowd control.
        /// It's crazy how stun, nearsight and mez land close to when immunity runs off. Humans are
        /// not that precise." Counted the same day over 209 bots-only matches: an enemy whose mez
        /// immunity had run out was mezzed again 55% of the time, a third of those within five
        /// seconds of the immunity ending and a tenth within two; after a stun immunity 64%, 15%
        /// within five seconds. (Nearsight was not: it came again a median 70 seconds after the
        /// last one ended, never within ten.) A bot read the immunity off the enemy, to the tick; a
        /// player keeps a rough count from when the mez broke and looks at something else meanwhile.
        /// Both 0: a bot knows at once, as it did.
        /// </summary>
        public int ImmunityEndNoticedAfterMinSeconds { get; set; } = 4;
        public int ImmunityEndNoticedAfterMaxSeconds { get; set; } = 20;
    }

    /// <summary>What each bot believes about each enemy's immunities, which lags what is true.</summary>
    public static class PvpNotice
    {
        /// <summary>What one bot last knew of one immunity of one enemy.</summary>
        public readonly record struct Belief(bool SawImmune, long NoticesAt);

        /// <summary>
        /// One look by a bot at an enemy: whether it takes the enemy for immune, and what it
        /// remembers. While the immunity is there it sees it. Once it is gone the bot still thinks
        /// it there until a moment drawn when it ended (<paramref name="delay"/> gives the
        /// milliseconds); an immunity it never saw it does not imagine.
        /// </summary>
        public static bool ThinksImmune(ref Belief belief, bool immuneNow, long now, Func<int> delay)
        {
            if (immuneNow)
            {
                belief = new Belief(true, 0);
                return true;
            }
            if (!belief.SawImmune)
                return false;
            if (belief.NoticesAt == 0)
                belief = belief with { NoticesAt = now + Math.Max(0, delay()) };
            if (now < belief.NoticesAt)
                return true;
            belief = default;
            return false;
        }

        /// <summary>The delay drawn for one ending, in milliseconds.</summary>
        public static int Draw(PvpHumanWeights weights, Random random)
        {
            int min = Math.Max(0, weights.ImmunityEndNoticedAfterMinSeconds), max = Math.Max(min, weights.ImmunityEndNoticedAfterMaxSeconds);
            return max == 0 ? 0 : random.Next(min * 1000, max * 1000 + 1);
        }

        private static readonly ConditionalWeakTable<GameLiving, Dictionary<(GameLiving, eEffect), Belief>> Memory = new();

        /// <summary>
        /// True while <paramref name="observer"/> takes <paramref name="target"/> for immune (eEffect.MezImmunity
        /// or eEffect.StunImmunity). Only an 8v8 bot has a memory; anybody else is told what is true.
        /// </summary>
        public static bool ThinksImmune(GameLiving observer, GameLiving target, eEffect immunity)
        {
            bool immuneNow = target.effectListComponent.ContainsEffectForEffectType(immunity);
            if (observer is not GameBot { IsPvpBot: true } bot)
                return immuneNow;
            PvpHumanWeights weights = PvpWeights.Of(bot).Human;
            if (weights.ImmunityEndNoticedAfterMaxSeconds <= 0)
                return immuneNow;
            Dictionary<(GameLiving, eEffect), Belief> mine = Memory.GetOrCreateValue(observer);
            lock (mine)
            {
                mine.TryGetValue((target, immunity), out Belief belief);
                bool thinks = ThinksImmune(ref belief, immuneNow, GameLoop.GameLoopTime, () => Draw(weights, Random.Shared));
                mine[(target, immunity)] = belief;
                return thinks;
            }
        }
    }
}

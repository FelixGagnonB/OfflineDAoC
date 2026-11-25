using System;
using System.Runtime.CompilerServices;
using DOL.AI.Brain;

namespace DOL.GS
{
    /// <summary>
    /// Slow hands (docs/PVP-8V8-ROADMAP.md, M12.9). A level of play set how late a bot answered
    /// something new, how often it was looking elsewhere and how often it took the lesser choice
    /// (PvpHumanWeights); once it was doing something, its hands were as quick as an elite
    /// team's. Over six evenings of bots on 2026-11-24 a bad group's member did 1.07 times the
    /// damage, a minute of fighting, of the same class in every other kind of team. The owner,
    /// that evening: "In my opinion, the bad group are not that bad, especially considering they
    /// always run with at least an other group".
    ///
    /// Two things a slow player does, and nothing else: it waits a moment before a cast that
    /// takes time (it was looking for the key, or at the fight), and some of its swings are
    /// without a style (it did not press one). Both in a fight only, and both off (0) for every
    /// level but the bad one.
    /// </summary>
    public static class PvpHands
    {
        private sealed class State
        {
            public long Until;
            public bool Drawn;
        }

        private static readonly ConditionalWeakTable<GameBot, State> States = new();

        /// <summary>How long it waits before its next cast, in milliseconds; 0 this time, or for a level that does not.</summary>
        public static int DrawHesitation(PvpHumanWeights weights, Random random)
        {
            if (weights.HesitatesPercent <= 0 || random.Next(100) >= weights.HesitatesPercent)
                return 0;
            int min = Math.Max(0, weights.HesitatesMinMilliseconds), max = Math.Max(min, weights.HesitatesMaxMilliseconds);
            return random.Next(min, max + 1);
        }

        /// <summary>
        /// What one look says, and what to keep: it waits while a wait is running; it is drawn
        /// for once for each cast (<paramref name="drawn"/>), so that a wait that is over is not
        /// followed by another before the cast it was for has begun.
        /// </summary>
        public static (bool Waits, long Until, bool Drawn) Look(long now, long until, bool drawn, Func<int> draw)
        {
            if (now < until)
                return (true, until, drawn);
            if (drawn)
                return (false, until, true);
            int wait = draw();
            return wait > 0 ? (true, now + wait, true) : (false, until, true);
        }

        /// <summary>True while an 8v8 bot in a fight is not yet casting what it means to cast.</summary>
        public static bool Hesitates(GameBot bot)
        {
            if (bot is not { IsPvpBot: true })
                return false;
            PvpHumanWeights weights = PvpNotice.HumanOf(bot);
            if (weights.HesitatesPercent <= 0 || (bot.Brain as BotBrain)?.PvpSquad is not { FightStarted: true })
                return false;
            State state = States.GetOrCreateValue(bot);
            (bool waits, state.Until, state.Drawn) = Look(GameLoop.GameLoopTime, state.Until, state.Drawn, () => DrawHesitation(weights, Random.Shared));
            return waits;
        }

        /// <summary>A cast has begun: the next one is drawn for again.</summary>
        public static void CastBegan(GameBot bot)
        {
            if (bot != null && States.TryGetValue(bot, out State state))
                state.Drawn = false;
        }

        /// <summary>Whether this swing is without a style.</summary>
        public static bool IsPlainSwing(PvpHumanWeights weights, Random random) =>
            weights.PlainSwingPercent > 0 && random.Next(100) < weights.PlainSwingPercent;

        /// <summary>True when an 8v8 fighter in a fight did not press a style for the swing it is making.</summary>
        public static bool SwingsPlain(GameBot bot) =>
            bot is { IsPvpBot: true } && (bot.Brain as BotBrain)?.PvpSquad is { FightStarted: true } &&
            IsPlainSwing(PvpNotice.HumanOf(bot), Random.Shared);
    }
}

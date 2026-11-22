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

        /// <summary>
        /// Reaction time: a bot answers something new only after a moment, drawn between these
        /// two for each bot and each thing. "Something new" is a groupmate put to sleep (the
        /// cure), the bot itself mezzed or stunned (its Purge), an enemy that has begun a cast
        /// (the interrupt), and an enemy fighter arrived on the group's healers and casters (the
        /// peel, the root, the stun). A bot read all of these off the server in the tick they
        /// happened. The owner, 2026-11-20: "the biggest one is reaction time and how the team
        /// cohesion behave in each situation". Both 0: at once, as it was.
        /// </summary>
        public int ReactionMinMilliseconds { get; set; } = 400;
        public int ReactionMaxMilliseconds { get; set; } = 1_200;

        /// <summary>
        /// Attention: this often, in percent, the bot was looking at something else and its
        /// reaction takes <see cref="DistractedTimes"/> times as long.
        /// </summary>
        public int DistractedPercent { get; set; } = 15;
        public int DistractedTimes { get; set; } = 3;

        /// <summary>
        /// Mistakes: every option a bot weighs is off by up to this many points either way, so
        /// that it does not always take the one the numbers say is best. The bar for casting at
        /// all is 50, and the leanings between kinds are 30 to 120 apart. 0: none.
        /// </summary>
        public int ChoiceNoise { get; set; } = 6;

        /// <summary>The settings of a named level (docs/PVP-8V8-ROADMAP.md, M10.2).</summary>
        public static PvpHumanWeights For(PvpSkill skill) => skill switch
        {
            // As near to what the bots were as a level may be: they still do not see an immunity end to the tick.
            PvpSkill.Sharp => new PvpHumanWeights
            {
                ImmunityEndNoticedAfterMinSeconds = 1, ImmunityEndNoticedAfterMaxSeconds = 6,
                ReactionMinMilliseconds = 150, ReactionMaxMilliseconds = 450, DistractedPercent = 0, ChoiceNoise = 0,
            },
            PvpSkill.Casual => new PvpHumanWeights
            {
                ImmunityEndNoticedAfterMinSeconds = 8, ImmunityEndNoticedAfterMaxSeconds = 35,
                ReactionMinMilliseconds = 900, ReactionMaxMilliseconds = 2_500, DistractedPercent = 30, ChoiceNoise = 14,
            },
            _ => new PvpHumanWeights(),
        };
    }

    /// <summary>How sharp a group of bots plays (M10.2). Seasoned is what the weights say; the two others are fixed.</summary>
    public enum PvpSkill
    {
        Casual,
        Seasoned,
        Sharp,
    }

    /// <summary>
    /// The level of the player's own group and of every other group, chosen separately
    /// (<c>/8v8 skill</c>, the setup window). It is given to a bot when it is made and lasts
    /// until the server restarts. A batch of scrims has no player and leaves both at Seasoned,
    /// which is the weights as they are (and as a weights file may have them).
    /// </summary>
    public static class PvpSkills
    {
        public static PvpSkill Group { get; set; } = PvpSkill.Seasoned;
        public static PvpSkill Others { get; set; } = PvpSkill.Seasoned;

        public static PvpSkill? Parse(string word) => (word ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "casual" or "easy" => PvpSkill.Casual,
            "seasoned" or "normal" => PvpSkill.Seasoned,
            "sharp" or "hard" => PvpSkill.Sharp,
            _ => null,
        };

        /// <summary>
        /// The level a bot was made with, for a recording's header. On 2026-11-22 the owner could not
        /// tell Casual from Sharp in three matches, and the recordings did not say which was which.
        /// </summary>
        public static string NameOf(GameBot bot) =>
            bot?.PvpHuman is not { } human ? nameof(PvpSkill.Seasoned)
            : human.ReactionMaxMilliseconds <= PvpHumanWeights.For(PvpSkill.Sharp).ReactionMaxMilliseconds ? nameof(PvpSkill.Sharp)
            : human.ReactionMinMilliseconds >= PvpHumanWeights.For(PvpSkill.Casual).ReactionMinMilliseconds ? nameof(PvpSkill.Casual)
            : nameof(PvpSkill.Seasoned);

        /// <summary>What a bot of that level is given; nothing for Seasoned, which reads the weights.</summary>
        public static PvpHumanWeights WeightsFor(PvpSkill skill) => skill == PvpSkill.Seasoned ? null : PvpHumanWeights.For(skill);

        public static string Describe(PvpSkill skill) => skill switch
        {
            PvpSkill.Sharp => "Sharp: answers in a quarter to half a second, never distracted, always takes its best choice",
            PvpSkill.Casual => "Casual: answers in one to two and a half seconds, distracted three times in ten, often takes a lesser choice",
            _ => "Seasoned: answers in half a second to a second, distracted now and then, mostly takes its best choice",
        };
    }

    /// <summary>The new things a bot answers after its reaction time (PvpHumanWeights.ReactionMinMilliseconds).</summary>
    public enum PvpEvent
    {
        AllyMezzed,
        OwnControl,
        EnemyCasting,
        OnTheBackLine,
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
        // When each bot is ready to answer each new thing: by whom it is about and what, with when that began.
        private static readonly ConditionalWeakTable<GameLiving, Dictionary<(object, PvpEvent), (long Began, long ReadyAt)>> Reactions = new();

        /// <summary>The human limits of a bot: its own when it was given a level, else those of its weights.</summary>
        public static PvpHumanWeights HumanOf(GameBot bot) => bot?.PvpHuman ?? PvpWeights.Of(bot).Human;

        /// <summary>One reaction time, in milliseconds: drawn between the two bounds, and longer when the bot was not looking.</summary>
        public static int DrawReaction(PvpHumanWeights weights, Random random)
        {
            int min = Math.Max(0, weights.ReactionMinMilliseconds), max = Math.Max(min, weights.ReactionMaxMilliseconds);
            if (max == 0)
                return 0;
            int drawn = random.Next(min, max + 1);
            return random.Next(100) < weights.DistractedPercent ? drawn * Math.Max(1, weights.DistractedTimes) : drawn;
        }

        /// <summary>How far off one option's score is: up to the noise either way.</summary>
        public static int Noise(PvpHumanWeights weights, Random random) =>
            weights.ChoiceNoise <= 0 ? 0 : random.Next(-weights.ChoiceNoise, weights.ChoiceNoise + 1);

        /// <summary>
        /// One look at something that began at <paramref name="began"/>: true once the reaction
        /// time drawn for it has passed. The same thing is not drawn for twice; a new one (it
        /// began at another moment) is.
        /// </summary>
        public static bool Reacted(ref (long Began, long ReadyAt) memory, long began, long now, Func<int> delay)
        {
            if (memory.Began != began || memory.ReadyAt == 0)
                memory = (began, began + Math.Max(0, delay()));
            return now >= memory.ReadyAt;
        }

        /// <summary>
        /// True once <paramref name="observer"/> has had its reaction time since something about
        /// <paramref name="subject"/> began. Only an 8v8 bot waits; anybody else knows at once.
        /// </summary>
        public static bool Reacted(GameLiving observer, object subject, PvpEvent what, long began)
        {
            if (observer is not GameBot { IsPvpBot: true } bot || subject == null)
                return true;
            PvpHumanWeights weights = HumanOf(bot);
            if (weights.ReactionMaxMilliseconds <= 0)
                return true;
            Dictionary<(object, PvpEvent), (long Began, long ReadyAt)> mine = Reactions.GetOrCreateValue(observer);
            lock (mine)
            {
                mine.TryGetValue((subject, what), out (long Began, long ReadyAt) memory);
                bool ready = Reacted(ref memory, began, GameLoop.GameLoopTime, () => DrawReaction(weights, Random.Shared));
                mine[(subject, what)] = memory;
                return ready;
            }
        }

        /// <summary>When the mez or stun now on a living began; 0 when it has none.</summary>
        public static long ControlBegan(GameLiving living)
        {
            long began = 0;
            foreach (ECSGameEffect effect in living.effectListComponent.GetEffects())
            {
                if (effect.EffectType is eEffect.Mez or eEffect.Stun && effect.StartTick > began)
                    began = effect.StartTick;
            }
            return began;
        }

        /// <summary>True when a bot has seen that a groupmate is asleep: it is, and the bot has had its reaction time.</summary>
        public static bool SeesAsleep(GameLiving observer, GameLiving friend) =>
            friend.IsMezzed && Reacted(observer, friend, PvpEvent.AllyMezzed, ControlBegan(friend));

        /// <summary>True when a bot has seen that an enemy is casting.</summary>
        public static bool SeesCasting(GameLiving observer, GameLiving enemy) =>
            enemy.IsCasting && Reacted(observer, enemy, PvpEvent.EnemyCasting, (enemy.CurrentSpellHandler as Spells.SpellHandler)?.CastStartTick ?? 0);

        /// <summary>
        /// True while <paramref name="observer"/> takes <paramref name="target"/> for immune (eEffect.MezImmunity
        /// or eEffect.StunImmunity). Only an 8v8 bot has a memory; anybody else is told what is true.
        /// </summary>
        public static bool ThinksImmune(GameLiving observer, GameLiving target, eEffect immunity)
        {
            bool immuneNow = target.effectListComponent.ContainsEffectForEffectType(immunity);
            if (observer is not GameBot { IsPvpBot: true } bot)
                return immuneNow;
            PvpHumanWeights weights = HumanOf(bot);
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

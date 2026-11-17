using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DOL.AI.Brain;
using DOL.Events;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>
    /// Records a scrim match as one JSON-lines file (docs/PVP-8V8-ROADMAP.md, M3): a header, then
    /// what happened (attacks, finished casts, deaths) and a once-a-second snapshot of every bot,
    /// then the result. <c>source/tools/pvp_analyze.py</c> turns a folder of these into a report.
    ///
    /// Line types ("k"): match, snap, hit, cast, death, end. Times ("t") are milliseconds since
    /// the match started. Format version 1.
    ///
    /// Events arrive from game threads that run in parallel, so they are queued and written by
    /// the scrim's own tick; nothing here blocks a brain.
    /// </summary>
    public static class PvpRecorder
    {
        public const int FormatVersion = 1;

        private static readonly Logger Log = LoggerManager.Create(typeof(PvpRecorder));
        private static readonly ConcurrentQueue<string> Pending = new();
        private static readonly object WriteLock = new();
        private static StreamWriter _writer;
        private static volatile bool _recording;
        private static long _startTick;
        private static Dictionary<GameLiving, string> _teams = [];
        private static bool _hooked;

        public static string Folder => Path.Combine(AppContext.BaseDirectory, "logs", "scrims");
        public static string CurrentFile { get; private set; }

        public static void Begin(int number, PvpGroupTemplate groupA, PvpGroupTemplate groupB,
            IReadOnlyList<GameBot> teamA, IReadOnlyList<GameBot> teamB, int realmLevel, string batch)
        {
            End(null);
            if (!_hooked)
            {
                GameEventMgr.AddHandler(GameLivingEvent.AttackedByEnemy, OnAttacked);
                GameEventMgr.AddHandler(GameLivingEvent.CastFinished, OnCastFinished);
                GameEventMgr.AddHandler(GameLivingEvent.Dying, OnDying);
                _hooked = true;
            }
            Directory.CreateDirectory(Folder);
            CurrentFile = Path.Combine(Folder, $"{batch}-match-{number:000}.jsonl");
            lock (WriteLock)
                _writer = new StreamWriter(CurrentFile, false) { AutoFlush = false };
            _teams = teamA.ToDictionary(bot => (GameLiving)bot, _ => "A")
                .Concat(teamB.ToDictionary(bot => (GameLiving)bot, _ => "B"))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            _startTick = GameLoop.GameLoopTime;
            while (Pending.TryDequeue(out _)) { }
            _recording = true;
            Line(new
            {
                k = "match", v = FormatVersion, batch, number, utc = DateTime.UtcNow, realmLevel,
                a = groupA.Id, b = groupB.Id,
                bots = teamA.Concat(teamB).Select(bot => new
                {
                    n = bot.Name, team = _teams[bot], build = bot.PvpBuild.Id, cls = ((eCharacterClass)bot.ClassId).ToString(),
                    hp = bot.MaxHealth, pow = bot.MaxMana,
                }),
            });
        }

        /// <summary>Once a second from the scrim tick: every bot's state, then everything queued since.</summary>
        public static void Snapshot()
        {
            if (!_recording)
                return;
            foreach ((GameLiving living, string team) in _teams)
            {
                var bot = (GameBot)living;
                Line(new
                {
                    k = "snap", t = Now, n = bot.Name, x = bot.X, y = bot.Y,
                    hp = bot.IsAlive ? bot.Health : 0, pow = bot.Mana, end = bot.Endurance,
                    alive = bot.IsAlive, mez = bot.IsMezzed, stun = bot.IsStunned,
                    root = bot.effectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedDebuff) ||
                           bot.effectListComponent.ContainsEffectForEffectType(eEffect.Snare),
                    mezImm = bot.effectListComponent.ContainsEffectForEffectType(eEffect.MezImmunity),
                    stunImm = bot.effectListComponent.ContainsEffectForEffectType(eEffect.StunImmunity),
                    combat = bot.InCombat, moving = bot.IsMoving,
                    casting = bot.IsCasting ? bot.castingComponent?.SpellHandler?.Spell?.Name : null,
                    target = (bot.TargetObject as GameLiving)?.Name,
                    state = (bot.Brain as BotBrain)?.FSM?.GetCurrentState()?.StateType.ToString(),
                });
            }
            Drain();
        }

        public static void End(PvpMatchResult result)
        {
            if (!_recording)
                return;
            if (result != null)
                Line(new { k = "end", t = Now, result.Winner, result.End, result.Seconds, result.SecondsToContact, result.AliveA, result.AliveB });
            _recording = false;
            Drain();
            lock (WriteLock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        private static long Now => GameLoop.GameLoopTime - _startTick;

        // The owner of a pet counts as the actor, so a Theurgist's pets are the Theurgist's damage.
        private static (string Name, string Team, string Pet) Who(GameLiving living)
        {
            if (living == null)
                return (null, null, null);
            if (_teams.TryGetValue(living, out string team))
                return (living.Name, team, null);
            GameLiving owner = living is GameNPC { Brain: IControlledBrain brain } ? brain.GetLivingOwner() : null;
            return owner != null && _teams.TryGetValue(owner, out team) ? (owner.Name, team, living.Name) : (living.Name, null, null);
        }

        private static void OnAttacked(DOLEvent e, object sender, EventArgs args)
        {
            if (!_recording || args is not AttackedByEnemyEventArgs { AttackData: { } ad })
                return;
            var (attacker, attackerTeam, pet) = Who(ad.Attacker);
            var (target, targetTeam, targetPet) = Who(ad.Target);
            if (attackerTeam == null && targetTeam == null)
                return;
            Line(new
            {
                k = "hit", t = Now, a = attacker, at = attackerTeam, pet, d = target, dt = targetTeam, dpet = targetPet,
                res = ad.AttackResult.ToString(), type = ad.AttackType.ToString(), dmg = ad.Damage, crit = ad.CriticalDamage,
                resisted = ad.Modifier, spell = ad.SpellHandler?.Spell?.Name, st = ad.SpellHandler?.Spell?.SpellType.ToString(),
                style = ad.Style?.Name,
            });
        }

        private static void OnCastFinished(DOLEvent e, object sender, EventArgs args)
        {
            if (!_recording || sender is not GameLiving caster || args is not CastingEventArgs { SpellHandler: { Spell: { } spell } } cast)
                return;
            var (name, team, pet) = Who(caster);
            if (team == null)
                return;
            var (target, targetTeam, _) = Who(cast.Target);
            Line(new
            {
                k = "cast", t = Now, n = name, team, pet, spell = spell.Name, st = spell.SpellType.ToString(),
                lvl = spell.Level, val = spell.Value, dmg = spell.Damage, pow = spell.Power, castTime = spell.CastTime,
                radius = spell.Radius, d = target, dt = targetTeam,
            });
        }

        private static void OnDying(DOLEvent e, object sender, EventArgs args)
        {
            if (!_recording || sender is not GameLiving victim || !_teams.TryGetValue(victim, out string team))
                return;
            var (killer, killerTeam, pet) = Who((args as DyingEventArgs)?.Killer as GameLiving);
            Line(new { k = "death", t = Now, n = victim.Name, team, by = killer, byTeam = killerTeam, pet });
        }

        private static void Line(object value) => Pending.Enqueue(JsonSerializer.Serialize(value));

        private static void Drain()
        {
            lock (WriteLock)
            {
                if (_writer == null)
                    return;
                try
                {
                    while (Pending.TryDequeue(out string line))
                        _writer.WriteLine(line);
                    _writer.Flush();
                }
                catch (Exception exception)
                {
                    Log.Error("8v8 recorder could not write", exception);
                }
            }
        }
    }
}

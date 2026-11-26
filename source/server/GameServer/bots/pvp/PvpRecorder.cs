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
    /// Records scrim matches, each as one JSON-lines file (docs/PVP-8V8-ROADMAP.md, M3): a header,
    /// then what happened (attacks, finished casts, deaths) and a once-a-second snapshot of every
    /// bot, then the result. <c>source/tools/pvp_analyze.py</c> turns a folder of these into a report.
    ///
    /// Line types ("k"): match, snap, squad, ready, hit, begin, stop, cast, heal, wake, death, dec, call, end.
    /// Times ("t") are milliseconds since the match started. Format version 2 added "dec" (a
    /// squad-brain decision: what was picked, the best-scoring options, and what stood in the way)
    /// and the weights in the header. Version 3 (2026-11-18) added what a review of a fight was
    /// missing: "begin" and "stop" (a cast started, and one that ended without finishing, with why
    /// and by whom), "heal" (health actually restored, and how much was asked for), "wake" (who
    /// broke a mez), "squad" (each group's kill target and trained member, every second), and in
    /// "snap" the nearsight, disease, silence and interrupted states, the height, and whom the bot
    /// is on, guards and has interrupt duty for. "squad" also carries the group's order and the
    /// two readings it came from (bots/pvp/PvpReading.cs).
    ///
    /// Version 4 (2026-11-21) is a roam (M9.7): one file for the session, in logs/roams, whose
    /// first line is "roam" instead of "match", with any number of teams, each under its own
    /// name ("hib-main#2") where a match has "A" and "B". Every other line is as in a match,
    /// and a roam adds "team" (each team's state, place and whom it is fighting, every second),
    /// "ev" (a team sets off, a fight begins, a draw for adding on a fight, a wipe, a team
    /// standing up, realm points paid) and "spawn" (the members of a team that stood up: they
    /// are new bots under the old names). A team with no enemy near is written every fifth
    /// second only. <c>source/tools/pvp_roam.py</c> counts a roam and cuts its two-team fights
    /// out as matches, which every tool for a match then reads.
    ///
    /// Several matches can be recorded at once (parallel arenas, M2.5): each has its own
    /// <see cref="PvpRecording"/>, and an event goes to the recording its bots belong to.
    /// Events arrive from game threads that run in parallel, so they are queued and written by
    /// the match's own tick; nothing here blocks a brain.
    /// </summary>
    public static class PvpRecorder
    {
        public const int FormatVersion = 3;
        public const int RoamFormatVersion = 4;

        internal static readonly Logger Log = LoggerManager.Create(typeof(PvpRecorder));
        // Every bot of every match being recorded, and its recording.
        private static readonly ConcurrentDictionary<GameLiving, PvpRecording> ByBot = new();
        private static bool _hooked;

        public static string Folder => Path.Combine(AppContext.BaseDirectory, "logs", "scrims");
        public static string RoamFolder => Path.Combine(AppContext.BaseDirectory, "logs", "roams");

        private static void Hook()
        {
            if (_hooked)
                return;
            GameEventMgr.AddHandler(GameLivingEvent.AttackedByEnemy, OnAttacked);
            GameEventMgr.AddHandler(GameLivingEvent.CastFinished, OnCastFinished);
            GameEventMgr.AddHandler(GameLivingEvent.Dying, OnDying);
            _hooked = true;
        }

        // What a member is at the start, to check a bot against a player of the same build.
        private static object Describe(GameLiving member, string team) => member is GameBot bot
            ? (object)new
            {
                n = bot.Name, team, build = bot.PvpBuild.Id, cls = ((eCharacterClass)bot.ClassId).ToString(),
                hp = bot.MaxHealth, pow = bot.MaxMana, rl = PvpRealmPoints.RealmLevelOf(bot),
                af = (int)bot.GetArmorAF(eArmorSlot.TORSO), abs = (int)Math.Round(bot.GetArmorAbsorb(eArmorSlot.TORSO) * 100),
                qui = bot.GetModified(eProperty.Quickness), swing = bot.AttackSpeed(bot.ActiveWeapon),
                crit = bot.GetModified(eProperty.CriticalMeleeHitChance), spellCrit = bot.GetModified(eProperty.CriticalSpellHitChance),
            }
            : new
            {
                n = member.Name, team, build = PvpMember.BuildOf(member), cls = PvpMember.ClassOf(member).ToString(),
                hp = member.MaxHealth, pow = member.MaxMana, rl = PvpRealmPoints.RealmLevelOf(member), player = true, level = (int)member.Level,
                af = (int)member.GetArmorAF(eArmorSlot.TORSO), abs = (int)Math.Round(member.GetArmorAbsorb(eArmorSlot.TORSO) * 100),
                qui = member.GetModified(eProperty.Quickness),
            };

        /// <summary>
        /// A roam: one recording for the whole session and all of its teams, each under its own
        /// name. <paramref name="teams"/>: name, line-up, realm and members (a player among them).
        /// </summary>
        /// <param name="kinds">On a prime-time evening: each team's kind by its name (PvpTeamKind).</param>
        public static PvpRecording BeginRoam(string label, IReadOnlyList<(string Name, string Group, eRealm Realm, IReadOnlyList<GameLiving> Members)> teams,
            IReadOnlyDictionary<string, string> kinds = null)
        {
            Hook();
            Directory.CreateDirectory(RoamFolder);
            var recording = new PvpRecording(Path.Combine(RoamFolder, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{label}.jsonl"),
                teams.SelectMany(team => team.Members.Select(member => (member, team.Name))));
            foreach (GameLiving member in recording.Teams.Keys)
                ByBot[member] = recording;
            recording.Line(new
            {
                k = "roam", v = RoamFormatVersion, label, utc = DateTime.UtcNow,
                weights = PvpWeights.Current,
                teams = teams.Select(team => new
                {
                    name = team.Name, group = team.Group, realm = team.Realm.ToString(),
                    player = team.Members.OfType<GamePlayer>().FirstOrDefault()?.Name,
                    // (A guild of a prime-time evening has no bots yet when this is written: its level is its kind's.)
                    skill = kinds != null && kinds.TryGetValue(team.Name, out string itsKind) && !team.Members.OfType<GameBot>().Any()
                        ? PvpTeamProfile.Of(itsKind).Skill.ToString() : PvpSkills.NameOf(team.Members.OfType<GameBot>().FirstOrDefault()),
                    kind = kinds != null && kinds.TryGetValue(team.Name, out string kind) ? kind : null,
                }),
                bots = teams.SelectMany(team => team.Members.Select(member => Describe(member, team.Name))),
            });
            return recording;
        }

        /// <summary>A team of a roam stood up again as new bots: the old ones leave the recording, the new ones come into it.</summary>
        public static void Regroup(PvpRecording recording, string team, IReadOnlyList<GameLiving> members)
        {
            if (recording == null)
                return;
            foreach (GameLiving gone in recording.Teams.Where(pair => pair.Value == team && !members.Contains(pair.Key)).Select(pair => pair.Key).ToList())
                ByBot.TryRemove(new KeyValuePair<GameLiving, PvpRecording>(gone, recording));
            recording.Regroup(team, members);
            foreach (GameLiving member in members)
                ByBot[member] = recording;
            recording.Line(new { k = "spawn", t = recording.Now, team, bots = members.Select(member => Describe(member, team)) });
        }

        public static PvpRecording Begin(int number, PvpGroupTemplate groupA, PvpGroupTemplate groupB,
            IReadOnlyList<GameBot> teamA, IReadOnlyList<GameBot> teamB, int realmLevel, string batch, GamePlayer leader = null)
        {
            Hook();
            Directory.CreateDirectory(Folder);
            var recording = new PvpRecording(Path.Combine(Folder, $"{batch}-match-{number:000}.jsonl"), teamA, teamB, leader);
            foreach (GameLiving member in recording.Teams.Keys)
                ByBot[member] = recording;
            // A play session: the player is a member of side A like the bots, with "player" set and no weights of its own.
            object[] players = leader == null ? [] :
            [
                new
                {
                    n = leader.Name, team = "A", build = PvpMember.BuildOf(leader), cls = PvpMember.ClassOf(leader).ToString(),
                    hp = leader.MaxHealth, pow = leader.MaxMana, player = true, level = (int)leader.Level,
                    af = (int)leader.GetArmorAF(eArmorSlot.TORSO), abs = (int)Math.Round(leader.GetArmorAbsorb(eArmorSlot.TORSO) * 100),
                    qui = leader.GetModified(eProperty.Quickness),
                },
            ];
            recording.Line(new
            {
                k = "match", v = FormatVersion, batch, number, utc = DateTime.UtcNow, realmLevel,
                a = groupA.Id, b = groupB.Id,
                // In a comparison of two sets of weights the teams fight with different ones.
                weights = PvpWeights.Of(teamA[0]), weightsB = PvpWeights.Of(teamB[0]) == PvpWeights.Of(teamA[0]) ? null : PvpWeights.Of(teamB[0]),
                setA = SetName(teamA[0]), setB = SetName(teamB[0]),
                skillA = PvpSkills.NameOf(teamA[0]), skillB = PvpSkills.NameOf(teamB[0]),
                bots = teamA.Concat(teamB).Select(bot => (object)new
                {
                    n = bot.Name, team = recording.Teams[bot], build = bot.PvpBuild.Id, cls = ((eCharacterClass)bot.ClassId).ToString(),
                    hp = bot.MaxHealth, pow = bot.MaxMana,
                    // What its gear and buffs come to, to check a bot against a player of the same build.
                    af = (int)bot.GetArmorAF(eArmorSlot.TORSO), abs = (int)Math.Round(bot.GetArmorAbsorb(eArmorSlot.TORSO) * 100),
                    qui = bot.GetModified(eProperty.Quickness), swing = bot.AttackSpeed(bot.ActiveWeapon),
                    crit = bot.GetModified(eProperty.CriticalMeleeHitChance), spellCrit = bot.GetModified(eProperty.CriticalSpellHitChance),
                }).Concat(players),
            });
            return recording;
        }

        /// <summary>"new" or "old" for a team in a comparison of two sets of weights, null otherwise.</summary>
        internal static string SetName(GameBot bot) =>
            PvpWeights.Reference == null || bot == null ? null : bot.PvpWeightsSet == PvpWeights.Reference ? "old" : "new";

        internal static void Forget(PvpRecording recording)
        {
            foreach (GameLiving bot in recording.Teams.Keys)
                ByBot.TryRemove(new KeyValuePair<GameLiving, PvpRecording>(bot, recording));
        }

        /// <summary>The recording a living belongs to: a bot's own, or its owner's for a pet.</summary>
        private static PvpRecording Of(GameLiving living)
        {
            if (living == null)
                return null;
            if (ByBot.TryGetValue(living, out PvpRecording recording))
                return recording;
            GameLiving owner = living is GameNPC { Brain: IControlledBrain brain } ? brain.GetLivingOwner() : null;
            return owner != null && ByBot.TryGetValue(owner, out recording) ? recording : null;
        }

        private static void OnAttacked(DOLEvent e, object sender, EventArgs args)
        {
            if (args is not AttackedByEnemyEventArgs { AttackData: { } ad })
                return;
            // Either end of the attack may be the one in a match: a guard or a monster is in none.
            PvpRecording recording = Of(ad.Attacker) ?? Of(ad.Target);
            if (recording == null)
                return;
            var (attacker, attackerTeam, pet) = recording.Who(ad.Attacker);
            var (target, targetTeam, targetPet) = recording.Who(ad.Target);
            if (attackerTeam == null && targetTeam == null)
                return;
            recording.Line(new
            {
                k = "hit", t = recording.Now, a = attacker, at = attackerTeam, pet, d = target, dt = targetTeam, dpet = targetPet,
                res = ad.AttackResult.ToString(), type = ad.AttackType.ToString(), dmg = ad.Damage, crit = ad.CriticalDamage,
                resisted = ad.Modifier, spell = ad.SpellHandler?.Spell?.Name, st = ad.SpellHandler?.Spell?.SpellType.ToString(),
                style = ad.Style?.Name,
                // A guarded attack lands on the guard's shield: who it was meant for.
                guardFor = ad.OriginalTarget != null && ad.OriginalTarget != ad.Target ? recording.Who(ad.OriginalTarget).Name : null,
            });
        }

        private static void OnCastFinished(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GameLiving caster || args is not CastingEventArgs { SpellHandler: { Spell: { } spell } } cast ||
                Of(caster) is not { } recording)
                return;
            var (name, team, pet) = recording.Who(caster);
            if (team == null)
                return;
            var (target, targetTeam, _) = recording.Who(cast.Target);
            recording.Line(new
            {
                k = "cast", t = recording.Now, n = name, team, pet, spell = spell.Name, st = spell.SpellType.ToString(),
                lvl = spell.Level, val = spell.Value, dmg = spell.Damage, pow = spell.Power, castTime = spell.CastTime,
                radius = spell.Radius, d = target, dt = targetTeam,
            });
        }

        private static void OnDying(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GameLiving victim || !ByBot.TryGetValue(victim, out PvpRecording recording) ||
                !recording.Teams.TryGetValue(victim, out string team))
                return;
            var (killer, killerTeam, pet) = recording.Who((args as DyingEventArgs)?.Killer as GameLiving);
            recording.Line(new { k = "death", t = recording.Now, n = victim.Name, team, by = killer, byTeam = killerTeam, pet });
            // The players of the evening read of it as a frontier read of a death (PvpDeathNews).
            PvpDeathNews.Tell(victim, (args as DyingEventArgs)?.Killer as GameLiving, killer,
                ByBot.Where(pair => pair.Value == recording).Select(pair => pair.Key).OfType<GamePlayer>().ToList());
            if (victim is GameBot dead)
                PvpObserver.Say(dead, $"{PvpObserver.Name(dead)} died" + (killer == null ? "" : $", killed by {killer}" + (pet == null ? "" : "'s pet")));
        }

        /// <summary>True while a match is being recorded. The engine's hooks below cost one read otherwise.</summary>
        public static bool Active => !ByBot.IsEmpty;

        /// <summary>A spell with a cast time has begun (called by the casting component).</summary>
        public static void CastStarted(GameLiving caster, Spells.SpellHandler handler)
        {
            if (!Active || Of(caster) is not { } recording || handler?.Spell is not { } spell)
                return;
            var (name, team, pet) = recording.Who(caster);
            if (team == null)
                return;
            var (target, targetTeam, _) = recording.Who(handler.Target);
            recording.Line(new
            {
                k = "begin", t = recording.Now, n = name, team, pet, spell = spell.Name, st = spell.SpellType.ToString(),
                d = target, dt = targetTeam, castTime = handler.CalculateCastingTime(),
            });
        }

        /// <summary>
        /// A cast ended without finishing: interrupted by an attack (<paramref name="by"/>), by
        /// moving, by something the game refused, or dropped by the caster itself.
        /// </summary>
        public static void CastStopped(GameLiving caster, Spells.SpellHandler handler, string why, GameLiving by = null)
        {
            if (!Active || Of(caster) is not { } recording || handler?.Spell is not { } spell)
                return;
            var (name, team, pet) = recording.Who(caster);
            if (team == null || !recording.FirstStop(caster, spell.ID))
                return;
            var (attacker, attackerTeam, attackerPet) = recording.Who(by);
            recording.Line(new { k = "stop", t = recording.Now, n = name, team, pet, spell = spell.Name, why, by = attacker, byTeam = attackerTeam, byPet = attackerPet });
        }

        /// <summary>Health restored by a heal: what was asked for and what the target could take.</summary>
        public static void Healed(GameLiving target, GameObject source, int asked, int restored)
        {
            if (!Active || Of(target) is not { } recording)
                return;
            var (name, team, pet) = recording.Who(target);
            if (team == null)
                return;
            var (healer, healerTeam, healerPet) = recording.Who(source as GameLiving);
            recording.Line(new { k = "heal", t = recording.Now, n = healer, team = healerTeam, pet = healerPet, d = name, dt = team, dpet = pet, asked, got = restored });
        }

        /// <summary>A mez was broken by this attack.</summary>
        public static void Woken(GameLiving target, AttackData ad)
        {
            if (!Active || ad == null || Of(target) is not { } recording)
                return;
            var (name, team, _) = recording.Who(target);
            if (team == null)
                return;
            var (attacker, attackerTeam, pet) = recording.Who(ad.Attacker);
            recording.Line(new
            {
                k = "wake", t = recording.Now, n = name, team, by = attacker, byTeam = attackerTeam, pet,
                spell = ad.SpellHandler?.Spell?.Name, style = ad.Style?.Name, type = ad.AttackType.ToString(),
            });
        }

        /// <summary>
        /// A group's kill-target call (line type "call"): whom it now kills, whom it was killing,
        /// and the best-scoring enemies it chose from.
        /// </summary>
        public static void Call(GameBot member, GameLiving target, int score, GameLiving previous, IEnumerable<object> options)
        {
            if (!ByBot.TryGetValue(member, out PvpRecording recording) || !recording.Teams.TryGetValue(member, out string team))
                return;
            recording.Line(new { k = "call", t = recording.Now, team, d = target.Name, score, was = previous?.Name, opts = options.ToList() });
        }

        /// <summary>
        /// A squad-brain decision, called from brain threads. <paramref name="pick"/> is null when
        /// the bot found nothing worth casting; <paramref name="blocked"/> then says why.
        /// </summary>
        public static void Decision(GameBot bot, object pick, IEnumerable<object> options, object blocked)
        {
            if (!ByBot.TryGetValue(bot, out PvpRecording recording) || !recording.Teams.TryGetValue(bot, out string team))
                return;
            recording.Line(new { k = "dec", t = recording.Now, n = bot.Name, team, pick, opts = options.ToList(), blocked });
        }
    }

    /// <summary>One match being recorded: its file, its two teams and the lines waiting to be written.</summary>
    public sealed class PvpRecording
    {
        private readonly ConcurrentQueue<string> _pending = new();
        private readonly object _writeLock = new();
        private readonly long _startTick = GameLoop.GameLoopTime;
        // The last cast each bot was recorded as stopping: one cast can be reported as stopped by
        // more than one part of the engine.
        private readonly ConcurrentDictionary<GameLiving, (int Spell, long Tick)> _stops = new();

        // A reading for the record: fighters held, casters free, healers free (shares), and how many are alive.
        private static object Shares(PvpReading reading) => new
        {
            fh = Math.Round(reading.FightersHeldShare, 2), cf = Math.Round(reading.CastersFreeShare, 2),
            hf = Math.Round(reading.HealersFreeShare, 2), alive = reading.Alive,
        };

        internal bool FirstStop(GameLiving caster, int spell)
        {
            long now = GameLoop.GameLoopTime;
            if (_stops.TryGetValue(caster, out var last) && last.Spell == spell && now - last.Tick < 300)
                return false;
            _stops[caster] = (spell, now);
            return true;
        }
        private StreamWriter _writer;
        private volatile bool _open = true;
        // Replaced as a whole when a team of a roam stands up again: brains read it while the session writes it.
        private volatile Dictionary<GameLiving, string> _teams;
        private int _snapshots;

        public string File { get; }
        internal Dictionary<GameLiving, string> Teams => _teams;

        internal PvpRecording(string file, IReadOnlyList<GameBot> teamA, IReadOnlyList<GameBot> teamB, GamePlayer leader = null)
            : this(file, teamA.Select(bot => ((GameLiving)bot, "A")).Concat(teamB.Select(bot => ((GameLiving)bot, "B")))
                .Concat(leader == null ? [] : new[] { ((GameLiving)leader, "A") }))
        {
        }

        internal PvpRecording(string file, IEnumerable<(GameLiving Member, string Team)> members)
        {
            File = file;
            _writer = new StreamWriter(file, false) { AutoFlush = false };
            var teams = new Dictionary<GameLiving, string>();
            foreach ((GameLiving member, string team) in members)
                teams[member] = team;
            _teams = teams;
        }

        internal void Regroup(string team, IReadOnlyList<GameLiving> members)
        {
            var teams = _teams.Where(pair => pair.Value != team).ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (GameLiving member in members)
                teams[member] = team;
            _teams = teams;
        }

        internal long Now => GameLoop.GameLoopTime - _startTick;

        // The owner of a pet counts as the actor, so a Theurgist's pets are the Theurgist's damage.
        internal (string Name, string Team, string Pet) Who(GameLiving living)
        {
            if (living == null)
                return (null, null, null);
            if (Teams.TryGetValue(living, out string team))
                return (living.Name, team, null);
            GameLiving owner = living is GameNPC { Brain: IControlledBrain brain } ? brain.GetLivingOwner() : null;
            // Anything else (a keep guard, a monster, a bot of another match) keeps its own name and
            // no team; the analyzer reports it as outside damage.
            return owner != null && Teams.TryGetValue(owner, out team) ? (owner.Name, team, living.Name) : (living.Name, null, null);
        }

        /// <summary>
        /// Once a second from the match's tick: every bot's state, then everything queued since.
        /// In a roam a team that is <paramref name="quiet"/> (no enemy near) is written every
        /// fifth second only: six teams walk for minutes between fights.
        /// </summary>
        public void Snapshot(Func<string, bool> quiet = null)
        {
            if (!_open)
                return;
            Dictionary<GameLiving, string> teams = Teams;
            bool sparse = quiet != null && _snapshots++ % 5 != 0;
            foreach ((GameLiving bot, string team) in teams)
            {
                if (sparse && quiet(team))
                    continue;
                // A player in the match has every row a bot has; what a brain decides (aim, ward, duty, state) stays empty.
                var brain = (bot as GameBot)?.Brain as BotBrain;
                Line(new
                {
                    k = "snap", t = Now, n = bot.Name, team, x = bot.X, y = bot.Y, z = bot.Z,
                    ns = bot.effectListComponent.ContainsEffectForEffectType(eEffect.Nearsight),
                    dis = bot.IsDiseased, sil = bot.IsSilenced, intr = bot.IsBeingInterrupted,
                    // Whom it is on, whom it guards, whom it has interrupt duty for.
                    aim = brain?.PvpAssignedTarget?.Name, ward = bot is GameBot own ? brain?.PvpSquad?.WardOf(own)?.Name : null, duty = brain?.PvpDutyTarget?.Name,
                    hp = bot.IsAlive ? bot.Health : 0, pow = bot.Mana, end = bot.Endurance,
                    // What it regains a tick (er), and what its songs and chants add to that (erb): a song up that adds nothing, or less, is a fault.
                    er = bot.GetModified(eProperty.EnduranceRegenerationAmount), erb = bot.BaseBuffBonusCategory[eProperty.EnduranceRegenerationAmount],
                    // The song a singer has running (its pulse, not what it last cast): a song can be begun and then lost.
                    song = bot.effectListComponent.GetPulseEffects().FirstOrDefault(pulse => pulse?.SpellHandler?.Spell is { IsHarmful: false } && !pulse.IsEnding && !pulse.IsEnded)?.SpellHandler.Spell.Name,
                    alive = bot.IsAlive, mez = bot.IsMezzed, stun = bot.IsStunned,
                    root = bot.effectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedDebuff) ||
                           bot.effectListComponent.ContainsEffectForEffectType(eEffect.Snare),
                    mezImm = bot.effectListComponent.ContainsEffectForEffectType(eEffect.MezImmunity),
                    stunImm = bot.effectListComponent.ContainsEffectForEffectType(eEffect.StunImmunity),
                    // A root or snare is refused on a bot that holds this.
                    snareImm = bot.effectListComponent.ContainsEffectForEffectType(eEffect.SnareImmunity),
                    combat = bot.InCombat, moving = bot.IsMoving,
                    casting = bot.IsCasting ? bot.castingComponent?.SpellHandler?.Spell?.Name : null,
                    target = (bot.TargetObject as GameLiving)?.Name,
                    state = brain?.FSM?.GetCurrentState()?.StateType.ToString(),
                });
            }
            // What each group has decided, so any second of the fight can be read on its own.
            foreach (IGrouping<string, GameLiving> side in teams.Keys.GroupBy(living => teams[living]))
            {
                if (sparse && quiet(side.Key))
                    continue;
                PvpSquad squad = side.Select(living => (living as GameBot)?.Brain as BotBrain).FirstOrDefault(brain => brain?.PvpSquad != null)?.PvpSquad;
                if (squad != null)
                    Line(new
                    {
                        k = "squad", t = Now, team = side.Key, kill = squad.KillTarget?.Name, trained = squad.Trained?.Name, known = squad.Known.Count,
                        // The order in force and the readings it came from: the group's own, and the enemy's as it sees it.
                        order = squad.Order.ToString(), own = Shares(squad.Own), theirs = Shares(squad.Theirs), incoming = squad.Incoming,
                        // The kite (bots/pvp/PvpKite.cs): its side, the enemies that aim at it, and the legs it has gone back.
                        side = squad.OwnSide, on = squad.OnIt, kite = squad.Kiting, legs = squad.KiteLegs, leaving = squad.Leaving,
                        stands = squad.KiteStands, prey = squad.Prey?.Name,
                    });
            }
            Drain();
        }

        /// <summary>
        /// The ready check ended and the match proper starts: how long buffing took, who was still
        /// at it, and the buffs each bot starts the match with (to check that a change to the
        /// ready check leaves every bot as it was).
        /// </summary>
        public void Ready(int seconds, IReadOnlyList<GameBot> notReady) =>
            Line(new
            {
                k = "ready", t = Now, seconds, timedOut = notReady.Count > 0, notReady = notReady.Select(bot => bot.Name).ToList(),
                buffs = Teams.Keys.Select(bot => new
                {
                    n = bot.Name,
                    b = bot.effectListComponent.GetSpellEffects().Where(effect => effect?.SpellHandler?.Spell != null && !effect.IsDisabled)
                        .Select(effect => $"{effect.SpellHandler.Spell.SpellType} {effect.SpellHandler.Spell.Value:0.#}").OrderBy(text => text).ToList(),
                    pet = (bot as GameLiving)?.ControlledBrain?.Body?.Name,
                }).ToList(),
            });

        /// <summary>Closes the file; with a result, the result is its last line.</summary>
        public void End(PvpMatchResult result)
        {
            if (!_open)
                return;
            if (result != null)
                Line(new { k = "end", t = Now, result.Winner, result.End, result.Seconds, result.SecondsToContact, result.AliveA, result.AliveB });
            _open = false;
            PvpRecorder.Forget(this);
            Drain();
            lock (_writeLock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        /// <summary>Closes the file of a roam; <paramref name="last"/> is its last line.</summary>
        public void Finish(object last)
        {
            if (!_open)
                return;
            Line(last);
            End(null);
        }

        internal void Line(object value)
        {
            if (_open)
                _pending.Enqueue(JsonSerializer.Serialize(value));
        }

        private void Drain()
        {
            lock (_writeLock)
            {
                if (_writer == null)
                    return;
                try
                {
                    while (_pending.TryDequeue(out string line))
                        _writer.WriteLine(line);
                    _writer.Flush();
                }
                catch (Exception exception)
                {
                    PvpRecorder.Log.Error("8v8 recorder could not write", exception);
                }
            }
        }
    }
}

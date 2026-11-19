using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.AI.Brain;
using DOL.GS.RealmAbilities;

namespace DOL.GS
{
    /// <summary>
    /// The 8v8 bots currently in the world, and the calls that put a meta group there and take
    /// it out again (docs/PVP-8V8-ROADMAP.md M1.6; the match loop of M2 builds on it). The bots
    /// are temporary: nothing here is saved, and <see cref="Clear"/> removes every one of them.
    /// All calls must run on the game loop.
    /// </summary>
    public static class PvpArena
    {
        private static readonly List<GameBot> Bots = [];

        public static IReadOnlyList<GameBot> Members
        {
            get { lock (Bots) return Bots.ToList(); }
        }

        /// <summary>
        /// Creates the eight bots of a group around a point and groups them. <paramref name="all"/>,
        /// when given, receives every bot put in the world, the buffbot too, so that one match can
        /// later be removed without touching the others.
        /// </summary>
        public static IReadOnlyList<GameBot> SpawnGroup(PvpGroupTemplate template, ushort regionId, int x, int y, int realmLevel = 0,
            ICollection<GameBot> all = null)
        {
            Region region = WorldMgr.GetRegion(regionId) ?? throw new ArgumentException($"No region {regionId}.");
            Zone zone = region.GetZone(x, y) ?? throw new ArgumentException($"No zone at {x},{y} in region {regionId}.");
            var nav = PathfindingProvider.Instance;
            // The caller gives no height: take the ground under the point from the navmesh.
            Vector3 centre = nav.GetFloorBeneath(zone, new Vector3(x, y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999
                ? floor
                : nav.GetClosestPoint(zone, new Vector3(x, y, 5_000), 256, 256, 20_000, nav.DefaultFilters)
                  ?? throw new InvalidOperationException($"No walkable ground at {x},{y} in {zone.Description}.");

            var spawned = new List<GameBot>();
            Group group = null;
            for (int i = 0; i < template.BuildIds.Count; i++)
            {
                PvpBuild build = PvpBuildCatalog.Find(template.BuildIds[i]);
                GameBot bot = CreateBot(build, template.Realm, i, template.BuildIds.Count, zone, regionId, centre, 140, realmLevel);
                if (!bot.AddToWorld())
                {
                    bot.Delete();
                    throw new InvalidOperationException($"{build.Id} could not enter region {regionId}.");
                }

                if (group == null)
                {
                    group = new Group(bot);
                    GroupMgr.AddGroup(group);
                }
                group.AddMember(bot);
                spawned.Add(bot);
                all?.Add(bot);
                lock (Bots) Bots.Add(bot);
            }
            all?.Add(SpawnBuffbot(template.Realm, spawned.Cast<GameLiving>().ToList(), regionId, centre));
            return spawned;
        }

        /// <summary>One 8v8 bot of a build, placed on a ring around a point but not yet in the world.</summary>
        private static GameBot CreateBot(PvpBuild build, eRealm realm, int index, int count, Zone zone, ushort regionId,
            Vector3 centre, int radius, int realmLevel)
        {
            var nav = PathfindingProvider.Instance;
            eGender gender = index % 2 == 0 ? eGender.Male : eGender.Female;
            string name = AutonomousBotIdentityGenerator.GenerateForClass(realm, gender, build.Class).Name;
            // Two 8v8 bots never share a name: the recorder and the analyzer tell bots apart by it,
            // and one dead Theurgist and one living namesake read as a bot standing up far from
            // its corpse (seen once in 816 matches, 2026-11-18).
            for (int attempt = 0; attempt < 20 && Members.Any(other => other.Name == name); attempt++)
                name = AutonomousBotIdentityGenerator.GenerateForClass(realm, gender, build.Class).Name;
            var bot = new GameBot(build, name, (byte)PvpBuildCatalog.RaceOf(build.Class), (byte)gender);
            bot.SetPvpRealmLevel(realmLevel);
            double angle = Math.PI * 2 * index / count;
            var desired = new Vector3(centre.X + (float)Math.Cos(angle) * radius, centre.Y + (float)Math.Sin(angle) * radius, centre.Z);
            Vector3 spot = nav.GetMoveAlongSurface(zone, centre, desired, nav.DefaultFilters) ?? centre;
            bot.X = (int)Math.Round(spot.X);
            bot.Y = (int)Math.Round(spot.Y);
            bot.Z = (int)Math.Round(spot.Z);
            bot.CurrentRegionID = regionId;
            return bot;
        }

        /// <summary>
        /// The owner's group: the realm's main group without the build of the player's own class,
        /// seven bots that join the player's group and follow, plus the buffbot, which buffs the
        /// player too. Not yet tried in a game client.
        /// </summary>
        /// <param name="all">When given, receives every bot put in the world, the buffbot too.</param>
        public static IReadOnlyList<GameBot> SpawnCompanions(GamePlayer player, int realmLevel, ICollection<GameBot> all = null)
        {
            PvpGroupTemplate template = PvpBuildCatalog.Groups.First(group => group.Realm == player.Realm);
            List<string> builds = template.BuildIds.ToList();
            int own = builds.FindIndex(id => PvpBuildCatalog.Find(id).Class == (eCharacterClass)player.CharacterClass.ID);
            builds.RemoveAt(own >= 0 ? own : builds.Count - 1);

            var nav = PathfindingProvider.Instance;
            var centre = new Vector3(player.X, player.Y, player.Z);
            if (player.Group == null)
            {
                var group = new Group(player);
                GroupMgr.AddGroup(group);
                group.AddMember(player);
            }
            var spawned = new List<GameBot>();
            for (int i = 0; i < builds.Count; i++)
            {
                PvpBuild build = PvpBuildCatalog.Find(builds[i]);
                GameBot bot = CreateBot(build, player.Realm, i, builds.Count, player.CurrentZone, player.CurrentRegionID, centre, 160, realmLevel);
                bot.Heading = player.Heading;
                if (!bot.AddToWorld() || !player.Group.AddMember(bot))
                {
                    bot.Delete();
                    continue;
                }
                bot.EnterPlayerLedGroup(player);
                bot.Follow(player, BotManager.FOLLOW_DISTANCE, BotManager.MAX_FOLLOW_DISTANCE);
                (bot.Brain as BotBrain)?.FSM.SetCurrentState(eFSMStateType.FOLLOW);
                spawned.Add(bot);
                all?.Add(bot);
                lock (Bots) Bots.Add(bot);
            }
            GameBot buffbot = SpawnBuffbot(player.Realm, spawned.Cast<GameLiving>().Prepend(player).ToList(), player.CurrentRegionID, centre);
            all?.Add(buffbot);
            return spawned;
        }

        /// <summary>
        /// Puts the group's buffbot at its spawn point and has it buff everyone at once. It is
        /// not in the group, never moves or fights, and its buffs hold at any distance.
        /// </summary>
        private static GameBot SpawnBuffbot(eRealm realm, IReadOnlyList<GameLiving> group, ushort regionId, Vector3 centre)
        {
            var buffbot = new GameBot(PvpBuildCatalog.BuffbotFor(realm), "Buffbot") { IsPvpBuffbot = true };
            buffbot.X = (int)Math.Round(centre.X);
            buffbot.Y = (int)Math.Round(centre.Y);
            buffbot.Z = (int)Math.Round(centre.Z);
            buffbot.CurrentRegionID = regionId;
            buffbot.Flags |= GameNPC.eFlags.PEACE | GameNPC.eFlags.CANTTARGET;
            if (!buffbot.AddToWorld())
            {
                buffbot.Delete();
                throw new InvalidOperationException("The buffbot could not enter the region.");
            }
            lock (Bots) Bots.Add(buffbot);
            buffbot.Brain?.Stop();   // it only ever buffs, and that happens right here

            SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);
            List<Spell> buffs = BotBrain.PvpBuffSpells(buffbot).Select(pair => pair.Value)
                .Concat((buffbot.MiscSpells ?? []).Where(spell => spell.SpellType == eSpellType.EnduranceRegenBuff &&
                    spell.Target == eSpellTarget.REALM && spell.Concentration > 0).OrderByDescending(spell => spell.Value).Take(1))
                .ToList();
            foreach (GameLiving member in group)
            {
                // Owner's rule: every member gets every buff, acuity on fighters included.
                foreach (Spell spell in buffs)
                    ScriptMgr.CreateSpellHandler(buffbot, spell, line)?.StartSpell(member);
            }
            return buffbot;
        }

        /// <summary>Moves every 8v8 bot except the buffbots by an offset, onto the ground there. For range tests.</summary>
        public static int MoveGroups(int dx, int dy)
        {
            var nav = PathfindingProvider.Instance;
            int moved = 0;
            foreach (GameBot bot in Members.Where(bot => !bot.IsPvpBuffbot))
            {
                int x = bot.X + dx, y = bot.Y + dy;
                Zone zone = bot.CurrentRegion.GetZone(x, y);
                if (zone == null)
                    continue;
                Vector3 ground = nav.GetFloorBeneath(zone, new Vector3(x, y, 20_000), 20_000, nav.DefaultFilters) ?? new Vector3(x, y, bot.Z);
                if (bot.MoveTo(bot.CurrentRegionID, x, y, (int)Math.Round(ground.Z < 19_999 ? ground.Z : bot.Z), bot.Heading))
                    moved++;
            }
            return moved;
        }

        /// <summary>Strips every buff from one bot, as dying does. For testing the field rebuff.</summary>
        public static bool StripBuffs(string name)
        {
            GameBot bot = Members.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            bot?.effectListComponent.CancelAll();
            return bot != null;
        }

        /// <summary>Removes every 8v8 bot from the world.</summary>
        public static int Clear()
        {
            List<GameBot> all;
            lock (Bots)
                all = Bots.ToList();
            return Remove(all);
        }

        /// <summary>Removes these 8v8 bots from the world: one match's, while others go on.</summary>
        public static int Remove(IEnumerable<GameBot> bots)
        {
            List<GameBot> all = bots.ToList();
            lock (Bots)
                Bots.RemoveAll(all.Contains);
            foreach (GameBot bot in all)
            {
                try
                {
                    bot.Group?.RemoveMember(bot);
                    if (bot.ObjectState != GameObject.eObjectState.Deleted)
                    {
                        bot.RemoveFromWorld();
                        bot.Delete();
                    }
                }
                catch (Exception)
                {
                    // One bot failing to leave must not strand the others.
                }
            }
            return all.Count;
        }

        /// <summary>Where each 8v8 bot is, how it is doing and which buffs it holds.</summary>
        public static object Status()
        {
            IReadOnlyList<GameBot> all = Members;
            all = all.Where(bot => !bot.IsPvpBuffbot).ToList();
            var groups = all.Where(bot => bot.Group != null).GroupBy(bot => bot.Group).Select(members =>
            {
                var plan = (members.First().Brain as BotBrain)?.DescribePvpBuffPlan();
                return new
                {
                    Leader = members.Key.LivingLeader?.Name,
                    Size = members.Key.MemberCount,
                    BuffAssignments = plan?.Assignments,
                    BuffsUnfilled = plan?.Unfilled,
                };
            }).ToList();
            return new
            {
                Count = all.Count,
                Groups = groups,
                Bots = all.Select(bot => new
                {
                    bot.Name,
                    Build = bot.PvpBuild.Id,
                    Race = ((eRace)bot.Race).ToString(),
                    bot.RealmLevel,
                    RealmAbilities = bot.GetAllAbilities().OfType<RealmAbility>().Select(ability => $"{ability.KeyName} {ability.Level}").ToList(),
                    Alive = bot.IsAlive,
                    State = bot.ObjectState.ToString(),
                    Brain = (bot.Brain as BotBrain)?.FSM?.GetCurrentState()?.StateType.ToString(),
                    Health = $"{bot.Health}/{bot.MaxHealth}",
                    Power = $"{bot.Mana}/{bot.MaxMana}",
                    Concentration = bot.MaxConcentration > 100_000 ? "none" : $"{bot.effectListComponent.UsedConcentration} used of {bot.MaxConcentration}",
                    Position = $"{bot.X},{bot.Y},{bot.Z} in {bot.CurrentZone?.Description}",
                    Casting = bot.IsCasting ? bot.castingComponent?.SpellHandler?.Spell?.Name : null,
                    DistanceToBuffbot = Members.Where(other => other.IsPvpBuffbot && other.Realm == bot.Realm)
                        .Select(other => (int)bot.GetDistanceTo(other)).DefaultIfEmpty(-1).Min(),
                    DisabledBuffs = bot.effectListComponent.GetSpellEffects().Count(effect => effect.IsDisabled),
                    Buffs = bot.effectListComponent.GetSpellEffects()
                        .Where(effect => effect?.SpellHandler?.Spell != null)
                        .Select(effect => $"{effect.SpellHandler.Spell.SpellType} {effect.SpellHandler.Spell.Value:0.#} ({effect.SpellHandler.Spell.Name}, from {effect.SpellHandler.Caster?.Name}){(effect.IsDisabled ? " DISABLED" : "")}")
                        .OrderBy(text => text).ToList(),
                }).ToList(),
            };
        }
    }
}

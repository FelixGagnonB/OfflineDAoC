using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.AI.Brain;
using static DOL.GS.PvpScrim;

namespace DOL.GS
{
    /// <summary>
    /// One group in a session: its bots, the player who leads it when there is one, its squad,
    /// where it started, and how it moves when it is not yet fighting (docs/PVP-8V8-ROADMAP.md,
    /// M9.7). These were the two sides of a scrim, written into the session as "A" and "B"; a
    /// session of six teams needs each to carry its own.
    /// </summary>
    public sealed class PvpTeam
    {
        public List<GameBot> Bots = [];
        /// <summary>The player at the head of the team; null for a team of bots.</summary>
        public GamePlayer Leader;
        /// <summary>Null for a team on the old brain.</summary>
        public PvpSquad Squad;
        /// <summary>Where the team came into the world.</summary>
        public (int X, int Y) Home;
        /// <summary>Which point of its route it is heading for.</summary>
        public int Waypoint;
        /// <summary>
        /// A roam (bots/pvp/PvpRoam.cs): the points the team walks between, in order and round again.
        /// Each must be a walk the navmesh finds whole from the one before: asked for a far goal
        /// across the mountains of Emain Macha, a path ends in a dead end. Null: a lane's patrol,
        /// or the basin's points.
        /// </summary>
        public (int X, int Y)[] Route;
        /// <summary>Whether it had an enemy in sight at its last march, to catch the moment one comes into sight.</summary>
        public bool SawEnemy;
        /// <summary>Healers that stayed back on incoming, and members that stopped at range, each recorded once.</summary>
        public readonly HashSet<GameBot> HungBack = [];
        public readonly HashSet<GameBot> StoodAtRange = [];
        /// <summary>Drivers standing to let their group catch up on the road.</summary>
        public readonly HashSet<GameBot> DriversWaiting = [];

        /// <summary>Everybody in it, the player too.</summary>
        public IReadOnlyList<GameLiving> Members => Leader == null ? Bots : Bots.Cast<GameLiving>().Append(Leader).ToList();

        /// <summary>A new match: what was kept from the last one is forgotten.</summary>
        public void NewMatch(GamePlayer leader)
        {
            Leader = leader;
            SawEnemy = false;
            HungBack.Clear();
            StoodAtRange.Clear();
            DriversWaiting.Clear();
        }

        /// <summary>
        /// A fresh group stands in its travelling formation from the start, facing where the
        /// enemy starts: in a lane the two groups are too close to form up on the way.
        /// </summary>
        public void FormUp((int X, int Y) enemySpawn)
        {
            if (Bots.Count == 0)
                return;
            var centre = new Vector2((float)Bots.Average(bot => bot.X), (float)Bots.Average(bot => bot.Y));
            Vector2 toward = new Vector2(enemySpawn.X, enemySpawn.Y) - centre;
            if (toward.LengthSquared() < 1)
                return;
            // The front row a row's depth ahead of where the group spawned, the back row on the spot.
            Vector2 front = centre + Vector2.Normalize(toward) * BackRowDepth;
            Dictionary<GameBot, Vector3> places = SpreadPlaces(Bots, new Vector3(front.X, front.Y, (float)Bots.Average(bot => bot.Z)));
            if (places == null)
                return;
            foreach ((GameBot bot, Vector3 place) in places)
                bot.MoveTo(bot.CurrentRegionID, (int)Math.Round(place.X), (int)Math.Round(place.Y), (int)Math.Round(place.Z), bot.GetHeading(new Point2D(enemySpawn.X, enemySpawn.Y)));
        }

        /// <summary>
        /// A play session: a companion with nothing to fight travels with the player, as it did
        /// before the session, and does not march on the enemy by itself. Where the group goes
        /// is the player's choice.
        /// </summary>
        public void TravelWithLeader(IReadOnlyList<GameLiving> enemies)
        {
            // The enemy is in sight: the companions leave the clump they travel in and take their
            // places in rows around the player, the player's own place left free. In the six
            // sessions of 2026-11-19 all eight stood inside one area mez eight seconds before the
            // first blow, and two to seven of them still did at the first blow.
            // A group that rests stays where it is (PvpSquad.Resting): no order to follow or to take its rows.
            if (Squad?.Resting == true)
                return;
            bool overGround = PvpWeights.Of(Bots.FirstOrDefault()).SightOverGround;
            // Called to retreat, the group takes no rows: each of its bots runs behind the player (BotBrain.TryPvpRetreat).
            if (Squad?.RetreatsTo != null)
                enemies = [];
            List<GameLiving> seen = enemies.Where(enemy => enemy.IsAlive &&
                (Leader.IsWithinRadius(enemy, PvpFormation.FormRange) && (!overGround || PvpSight.Clear(Leader, enemy)) ||
                 Bots.Any(bot => bot.IsAlive && PvpSight.Sees(bot, enemy, PvpFormation.FormRange, PvpWeights.Of(bot))))).ToList();
            Dictionary<GameBot, Vector3> places = seen.Count == 0 ? null : LeaderPlaces(Bots.Where(bot => bot.IsAlive).ToList(),
                new Vector3((float)seen.Average(enemy => enemy.X), (float)seen.Average(enemy => enemy.Y), (float)seen.Average(enemy => enemy.Z)));
            foreach (GameBot bot in Bots)
            {
                if (bot.Brain is BotBrain own)
                    own.PvpSpreadOut = places != null;
                if (!bot.IsAlive || bot.Brain is not BotBrain brain || bot.IsCasting || bot.IsIncapacitated || bot.InCombat || brain.HasAggro ||
                    enemies.Any(enemy => enemy.IsAlive && bot.IsWithinRadius(enemy, EngageRange)))
                    continue;
                if (places != null)
                {
                    if (!places.TryGetValue(bot, out Vector3 place))
                        continue;
                    if (bot.FollowTarget != null)
                        bot.StopFollowing();
                    if (brain.FSM.GetCurrentState()?.StateType == eFSMStateType.FOLLOW)
                        brain.FSM.SetCurrentState(eFSMStateType.IDLE);
                    // The player moves, and its places with it: a new order every other second.
                    if (!bot.IsWithinRadius(new Point3D((int)place.X, (int)place.Y, (int)place.Z), 120) &&
                        !(bot.IsMoving && GameLoop.GameLoopTime % 2_000 >= 1_000))
                        bot.PathTo(place, bot.MaxSpeed);
                    continue;
                }
                if (!bot.IsWithinRadius(Leader, BotManager.MAX_FOLLOW_DISTANCE))
                    bot.PathTo(new Vector3(Leader.X, Leader.Y, Leader.Z), bot.MaxSpeed);
                else if (bot.FollowTarget != Leader)
                {
                    bot.Follow(Leader, BotManager.FOLLOW_DISTANCE, BotManager.MAX_FOLLOW_DISTANCE);
                    brain.FSM.SetCurrentState(eFSMStateType.FOLLOW);
                }
            }
        }

        /// <summary>A bot that is not fighting takes up the nearest living enemy it can see.</summary>
        public void Engage(IReadOnlyList<GameLiving> enemies)
        {
            // Called to retreat, nobody takes anybody on; nor in a group that has broken off.
            if (Squad?.RetreatsTo != null || Squad?.Leaving == true)
                return;
            var nav = PathfindingProvider.Instance;
            // While a group holds, its fighters go for nobody (bots/pvp/PvpReading.cs).
            bool holds = Squad?.Order == PvpOrder.Hold;
            foreach (GameBot bot in Bots)
            {
                // A shield fighter staying beside its ward has chosen not to attack.
                if (!bot.IsAlive || bot.Brain is not BotBrain brain || brain.HasAggro || Squad?.IsHolding(bot) == true ||
                    holds && PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee)
                    continue;
                GameLiving target = enemies
                    .Where(enemy => enemy.IsAlive && bot.IsWithinRadius(enemy, SightRange))
                    .OrderBy(enemy => bot.GetDistanceTo(enemy))
                    .FirstOrDefault(enemy => nav.HasLineOfSight(bot.CurrentZone, new Vector3(bot.X, bot.Y, bot.Z),
                        new Vector3(enemy.X, enemy.Y, enemy.Z), nav.DefaultFilters) &&
                        PvpSight.Sees(bot, enemy, SightRange, PvpWeights.Of(bot)));
                if (target == null)
                    continue;
                brain.AddToAggroList(target, 1);
                brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
            }
        }

        /// <summary>
        /// Bots with nothing to fight move as a group. While no member can see an enemy the group
        /// does not know where the other side is: it patrols (the middle of the arena, the far
        /// end, back to its own end, and round again). Once any member spots enemies it goes for
        /// them spread out, each member to its own place in two rows. The order is repeated every
        /// few seconds because the brain's own idle logic stops a walking bot.
        /// </summary>
        /// <param name="far">Where the other side started: the far end of a patrol in a lane.</param>
        /// <param name="roam">Set in the open basin: the group goes from point to point by this draw.</param>
        /// <param name="contactTick">When the first blow of the fight fell; 0 before it.</param>
        /// <param name="toward">Where it goes while it sees no enemy, instead of on along its route: the fight it has said it joins (PvpRoam.FightItJoined).</param>
        public void March(IReadOnlyList<GameLiving> enemies, (int X, int Y) far, PvpArenaBox arena, Random roam, long contactTick, (int X, int Y)? toward = null)
        {
            List<GameBot> members = Bots.Where(bot => bot.IsAlive).ToList();
            if (members.Count == 0)
                return;
            // A group that holds stands where it is and lets the enemy come.
            if (Squad?.Order == PvpOrder.Hold)
            {
                foreach (GameBot bot in members.Where(bot => bot.IsMoving && !bot.IsCasting))
                {
                    bot.StopMovingOnPath();
                    bot.StopMoving();
                }
                return;
            }
            // In the open basin a group sees the other from farther than in a lane, where the two start in sight.
            bool basin = Route != null || arena == PvpArenaBox.EmainSouthEast;
            int spot = basin ? PvpFormation.FormRange : SpotRange;
            PvpWeights sight = PvpWeights.Of(members[0]);
            List<GameLiving> seen = enemies.Where(enemy => enemy.IsAlive && members.Any(bot => PvpSight.Sees(bot, enemy, spot, sight))).ToList();
            Vector3 goal;
            Dictionary<GameBot, Vector3> places = null;
            if (seen.Count > 0)
            {
                goal = new Vector3((float)seen.Average(enemy => enemy.X), (float)seen.Average(enemy => enemy.Y), (float)seen.Average(enemy => enemy.Z));
            }
            else
            {
                (int X, int Y)[] route = [((Home.X + far.X) / 2, (Home.Y + far.Y) / 2), far, ((Home.X + far.X) / 2, (Home.Y + far.Y) / 2), Home];
                // A play session in the basin: each group goes from one point of the basin to another as it
                // pleases, so the two do not meet at the same place and from the same side every time.
                if (Route != null)
                    route = Route;
                else if (roam != null && basin)
                    route = PvpFormation.RoamPoints(arena.MinX, arena.MinY, arena.MaxX, arena.MaxY, 3_000);
                (int X, int Y) next = route[Waypoint % route.Length];
                double x = members.Average(bot => bot.X), y = members.Average(bot => bot.Y);
                // (On its way to a fight it leaves its route as it is, and takes it up again afterwards.)
                if (toward is { } fight)
                    next = fight;
                else if (Math.Sqrt(Math.Pow(next.X - x, 2) + Math.Pow(next.Y - y, 2)) < 900)
                    next = route[Waypoint = Route == null && roam != null && basin
                        ? PvpFormation.NextRoamPoint(Waypoint % route.Length, route.Length, roam.Next()) : (Waypoint + 1) % route.Length];
                // The route has no heights: take the ground under the waypoint from the navmesh,
                // as the spawn does. A goal above or below the mesh gives no path and nobody moves.
                var nav = PathfindingProvider.Instance;
                Zone zone = members[0].CurrentZone;
                goal = nav.GetFloorBeneath(zone, new Vector3(next.X, next.Y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999
                    ? floor
                    : nav.GetClosestPoint(zone, new Vector3(next.X, next.Y, 5_000), 256, 256, 20_000, nav.DefaultFilters)
                      ?? new Vector3(next.X, next.Y, (float)members.Average(bot => bot.Z));
            }
            // On the road in the basin a group follows its driver, the member with the speed song, close
            // behind it as players on /stick do; it takes its rows when the enemy is in sight. (A clump
            // 300 apart set around a point ahead, tried for a night, was to the owner's eye still "a
            // rectangle", and with the driver stopping for its songs the driver was the one left behind.)
            bool onTheRoad = basin && seen.Count == 0;
            GameBot driver = onTheRoad ? PvpFormation.DriverOf(members) : null;
            places = onTheRoad ? null : SpreadPlaces(members, goal);
            bool sawBefore = SawEnemy;
            bool sighted = seen.Count > 0 && !sawBefore;
            SawEnemy = seen.Count > 0;
            foreach (GameBot bot in Bots)
            {
                if (!bot.IsAlive || bot.IsCasting || bot.IsIncapacitated)
                    continue;
                // On incoming a healer that only heals stays out of the enemy's reach, and comes
                // in once the fight has settled (bots/pvp/PvpPositioning.cs).
                if (seen.Count > 0 && Squad != null)
                {
                    PvpWeights weights = PvpWeights.Of(bot);
                    bool onlyHeals = PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Healer &&
                        weights.HealsFirstBelowFor(bot.PvpBuild.Id, PvpMember.ClassOf(bot)) > 100;
                    int nearestSeen = (int)seen.Min(enemy => bot.GetDistanceTo(enemy));
                    // Before the first blow the whole back row stops at range and its fighters go on (PvpPositionWeights.BackRowStopsAt).
                    if (weights.Position.BackRowStops(PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee, nearestSeen, contactTick != 0))
                    {
                        if (StoodAtRange.Add(bot))
                            PvpRecorder.Decision(bot, new { kind = "StopAtRange", score = nearestSeen }, [], null);
                        if (bot.IsMoving && !bot.IsCasting)
                        {
                            bot.StopMovingOnPath();
                            bot.StopMoving();
                        }
                        continue;
                    }
                    if (weights.Position.HealerHangsBack(onlyHeals, nearestSeen,
                            contactTick == 0 ? -1 : (GameLoop.GameLoopTime - contactTick) / 1000.0))
                    {
                        if (HungBack.Add(bot))
                            PvpRecorder.Decision(bot, new { kind = "HangBack", score = 0 }, [], null);
                        if (bot.IsMoving)
                        {
                            bot.StopMovingOnPath();
                            bot.StopMoving();
                        }
                        continue;
                    }
                }
                // Fighting, or about to: an enemy is within reach. Otherwise it marches, even if
                // the brain still holds an old target from before it fell behind.
                bool straggler = !enemies.Any(enemy => enemy.IsAlive && bot.IsWithinRadius(enemy, EngageRange));
                // Once the fight has started, a member within reach of the enemy is the brain's
                // alone. A caster whose targets are all asleep has no attack target, and a march
                // order here made it walk in the middle of its casts, which then never started
                // (the Sorcerer made 92 mez decisions for 19 casts).
                if (!straggler && (contactTick != 0 || bot.InCombat || bot.Brain is BotBrain { HasAggro: true }))
                    continue;
                if (straggler && bot.Brain is BotBrain { HasAggro: true } behind)
                {
                    behind.ClearAggroList();
                    behind.FSM.SetCurrentState(eFSMStateType.IDLE);
                }
                if (driver != null && bot != driver)
                {
                    // Each a little farther back than the one before, so that they string out behind it.
                    if (bot.FollowTarget != driver)
                    {
                        int back = PvpFormation.FollowDistance(members.IndexOf(bot));
                        bot.Follow(driver, back, PvpFormation.FollowGivesUpBeyond);
                    }
                    continue;
                }
                if (bot.FollowTarget != null)
                    bot.StopFollowing();
                // The driver does not leave its group behind: it waits for whoever has dropped back.
                if (driver == bot)
                {
                    int farthest = members.Where(member => member != bot && member.IsAlive)
                        .Select(member => bot.GetDistanceTo(member)).DefaultIfEmpty(0).Max();
                    bool waits = PvpFormation.DriverWaits(farthest, DriversWaiting.Contains(bot));
                    if (waits ? DriversWaiting.Add(bot) : DriversWaiting.Remove(bot))
                        PvpRecorder.Decision(bot, new { kind = waits ? "DriverWaits" : "DriverGoesOn", score = farthest }, [], null);
                    if (waits)
                    {
                        if (bot.IsMoving)
                        {
                            bot.StopMovingOnPath();
                            bot.StopMoving();
                        }
                        continue;
                    }
                }
                // The moment the enemy comes into sight the order to take the rows goes out at once.
                if (!sighted && bot.IsMoving && GameLoop.GameLoopTime % 4_000 >= 1_000)
                    continue;
                bot.PathTo(places != null && places.TryGetValue(bot, out Vector3 place) ? place : goal, bot.MaxSpeed);
            }
        }

        /// <summary>Out of a fight, everybody goes to a point of the ground and stops there (a place off the road, to rest).</summary>
        public void GatherAt(Vector3 at, int within)
        {
            foreach (GameBot bot in Bots)
            {
                if (!bot.IsAlive || bot.IsCasting || bot.IsIncapacitated)
                    continue;
                if (bot.FollowTarget != null)
                    bot.StopFollowing();
                if (bot.IsWithinRadius(new Point3D((int)at.X, (int)at.Y, (int)at.Z), within))
                {
                    if (bot.IsMoving)
                    {
                        bot.StopMovingOnPath();
                        bot.StopMoving();
                    }
                    continue;
                }
                if (bot.IsMoving && GameLoop.GameLoopTime % 4_000 >= 1_000)
                    continue;
                bot.PathTo(at, bot.MaxSpeed);
            }
        }

        /// <summary>Out of a fight, everybody goes to a place and stops there: a dead groupmate, so that it is in reach to be raised.</summary>
        public void GatherAt(GameObject at, int within)
        {
            foreach (GameBot bot in Bots)
            {
                if (!bot.IsAlive || bot.IsCasting || bot.IsIncapacitated)
                    continue;
                if (bot.FollowTarget != null)
                    bot.StopFollowing();
                if (bot.IsWithinRadius(at, within))
                {
                    if (bot.IsMoving)
                    {
                        bot.StopMovingOnPath();
                        bot.StopMoving();
                    }
                    continue;
                }
                // Repeated every few seconds: the brain's own idle logic stops a walking bot.
                if (bot.IsMoving && GameLoop.GameLoopTime % 4_000 >= 1_000)
                    continue;
                bot.PathTo(new Vector3(at.X, at.Y, at.Z), bot.MaxSpeed);
            }
        }

        /// <summary>Everybody stops where it is (a team of a roam waiting out a fight it has left alone).</summary>
        public void Halt()
        {
            foreach (GameBot bot in Bots)
            {
                if (!bot.IsAlive || bot.IsCasting)
                    continue;
                if (bot.FollowTarget != null)
                    bot.StopFollowing();
                if (bot.IsMoving)
                {
                    bot.StopMovingOnPath();
                    bot.StopMoving();
                }
            }
        }

        /// <summary>
        /// The companions' places around the player once the enemy is in sight: the same two rows
        /// as <see cref="SpreadPlaces"/>, but set on the player, who goes where it likes. The
        /// player's row has the player in its middle; the fighters' row is a row's depth nearer
        /// the enemy than the others'.
        /// </summary>
        public Dictionary<GameBot, Vector3> LeaderPlaces(List<GameBot> members, Vector3 enemyCentre)
        {
            var centre = new Vector2(Leader.X, Leader.Y);
            Vector2 toward = new Vector2(enemyCentre.X, enemyCentre.Y) - centre;
            if (toward.LengthSquared() < 1 || members.Count == 0)
                return null;
            toward = Vector2.Normalize(toward);
            var across = new Vector2(-toward.Y, toward.X);
            bool leaderFights = PvpMember.IsFighter(Leader);
            var nav = PathfindingProvider.Instance;
            var places = new Dictionary<GameBot, Vector3>();
            Vector3 Ground(GameBot bot, Vector2 point) =>
                nav.GetFloorBeneath(bot.CurrentZone, new Vector3(point.X, point.Y, 20_000), 20_000, nav.DefaultFilters) is { } floor && floor.Z < 19_999
                    ? floor : new Vector3(point.X, point.Y, Leader.Z);
            foreach (IGrouping<bool, GameBot> row in members.GroupBy(bot => PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee))
            {
                List<GameBot> inRow = row.ToList();
                if (!row.Key)
                {
                    // The back row: a healer at each end, the casters between them.
                    List<GameBot> healers = inRow.Where(bot => PvpMember.RoleOf(bot) == PvpRole.Heal).ToList();
                    if (healers.Count >= 2)
                        inRow = [healers[0], .. inRow.Where(bot => !healers.Contains(bot)), .. healers.Skip(1)];
                }
                float[] offsets = PvpFormation.RowOffsets(inRow.Count, row.Key == leaderFights, SpreadSpacing);
                // Measured from the player: the fighters' row ahead of a player in the back row, the back row behind a player who fights.
                float ahead = row.Key == leaderFights ? 0 : row.Key ? BackRowDepth : -BackRowDepth;
                for (int slot = 0; slot < inRow.Count; slot++)
                    places[inRow[slot]] = Ground(inRow[slot], centre + across * offsets[slot] + toward * ahead);
            }
            // A shield fighter stands beside the member it guards, on the enemy's side of it: guard reaches 256 units.
            foreach (GameBot bot in members)
            {
                GameLiving ward = Squad?.WardOf(bot);
                if (ward == Leader && ward != null)
                    places[bot] = Ground(bot, centre + toward * 100);
                else if (ward is GameBot wardBot && places.TryGetValue(wardBot, out Vector3 beside))
                    places[bot] = new Vector3(beside.X + toward.X * 100, beside.Y + toward.Y * 100, beside.Z);
            }
            return places;
        }

        /// <summary>
        /// Where each member heads when the group has spotted the enemy: two rows across the line
        /// of approach, fighters aimed at the enemy and the rest a little short of it, neighbours
        /// <see cref="SpreadSpacing"/> apart.
        /// </summary>
        public Dictionary<GameBot, Vector3> SpreadPlaces(List<GameBot> members, Vector3 enemyCentre)
        {
            var centre = new Vector2((float)members.Average(bot => bot.X), (float)members.Average(bot => bot.Y));
            Vector2 toward = new Vector2(enemyCentre.X, enemyCentre.Y) - centre;
            if (toward.LengthSquared() < 1)
                return null;
            toward = Vector2.Normalize(toward);
            var across = new Vector2(-toward.Y, toward.X);
            var nav = PathfindingProvider.Instance;
            var places = new Dictionary<GameBot, Vector3>();
            foreach (IGrouping<bool, GameBot> row in members.GroupBy(bot => PvpMember.ArchetypeOf(bot) == PvpBuffArchetype.Melee))
            {
                List<GameBot> inRow = row.ToList();
                // The back row: one healer at each end, the casters between them (PvpSpreadWeights.HealerSpacing).
                if (!row.Key)
                {
                    List<GameBot> healers = inRow.Where(bot => PvpMember.RoleOf(bot) == PvpRole.Heal).ToList();
                    if (healers.Count >= 2)
                        inRow = [healers[0], .. inRow.Where(bot => !healers.Contains(bot)), .. healers.Skip(1)];
                }
                for (int slot = 0; slot < inRow.Count; slot++)
                {
                    Vector2 point = new Vector2(enemyCentre.X, enemyCentre.Y)
                                    + across * ((slot - (inRow.Count - 1) / 2f) * SpreadSpacing)
                                    - toward * (row.Key ? 0 : BackRowDepth);
                    // The place needs a height the navmesh knows, or no path is found.
                    places[inRow[slot]] = nav.GetFloorBeneath(inRow[slot].CurrentZone, new Vector3(point.X, point.Y, 20_000), 20_000, nav.DefaultFilters)
                        is { } floor && floor.Z < 19_999 ? floor : new Vector3(point.X, point.Y, enemyCentre.Z);
                }
            }
            // A shield fighter travels beside the groupmate it guards, on the enemy's side of it:
            // guard reaches 256 units, and its own place in the front row is 700 from the back row.
            foreach (GameBot bot in members)
            {
                if (Squad?.WardOf(bot) is GameBot ward && places.TryGetValue(ward, out Vector3 beside))
                    places[bot] = new Vector3(beside.X + toward.X * 100, beside.Y + toward.Y * 100, beside.Z);
            }
            return places;
        }
    }
}

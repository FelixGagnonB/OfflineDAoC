using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.GS;

namespace DOL.AI.Brain
{
    /// <summary>
    /// An 8v8 caster's turn when its group runs the squad brain: every spell kind it has, on every
    /// enemy it can reach, is scored (<see cref="PvpActionChoice"/>) and the best is cast. Each
    /// decision goes to the recorder with the runners-up, and so does a turn where nothing could
    /// be cast, with what stood in the way.
    /// </summary>
    public partial class BotBrain
    {
        /// <summary>The enemy group as the squad brain sees it; null without a squad brain.</summary>
        public IReadOnlyList<GameLiving> PvpEnemies { get; set; }

        /// <summary>
        /// Set while a companion of a play session stands in its place around the player with the
        /// enemy in sight (bots/pvp/PvpScrim.cs): the companion rules that walk it back to the
        /// player's side are off for as long.
        /// </summary>
        public bool PvpSpreadOut { get; set; }

        /// <summary>The group's called kill target, for every member (an interrupter's own aim is PvpAssignedTarget).</summary>
        public GameLiving PvpKillTarget { get; set; }

        /// <summary>The squad this bot belongs to (bots/pvp/PvpSquad.cs); null without a squad brain.</summary>
        public PvpSquad PvpSquad { get; set; }

        /// <summary>The enemy healer or caster this bot is to keep from casting (bots/pvp/PvpInterruptDuty.cs); null without one.</summary>
        public GameLiving PvpDutyTarget { get; set; }

        /// <summary>
        /// Set by the squad when it sends this bot a step away from enemy fighters
        /// (bots/pvp/PvpPositioning.cs): the step began at From and is the bot's turn until Until.
        /// </summary>
        public long PvpMoveFrom { get; set; }
        public long PvpMoveUntil { get; set; }
        /// <summary>When this bot last set off, or kept walking, toward a target to cast on it.</summary>
        public long PvpApproachTick => _pvpApproachTick;

        /// <summary>
        /// True while this bot is on such a step. Its brain starts no cast meanwhile: a cast would
        /// stop it where it stands, which is where it did not want to be.
        /// </summary>
        private bool PvpOnTheMove()
        {
            long now = GameLoop.GameLoopTime;
            if (BotBody?.IsPvpBot != true || now >= PvpMoveUntil || Body.IsIncapacitated)
                return false;
            // A healer breaks the step off for a member that is badly hurt (PvpPositionWeights.HealerBreaksOffAStepToHeal).
            // Not on a leg of its group's kite: the group goes back as one, and it heals from where the leg ends.
            if (PvpSquad?.Kiting != true && PvpBreaksOffToHeal())
            {
                PvpMoveUntil = 0;
                Body.StopMovingOnPath();
                Body.StopMoving();
                return false;
            }
            // The path may take a moment to be found.
            if (Body.IsMoving || now - PvpMoveFrom < 400)
                return true;
            PvpMoveUntil = 0;
            return false;
        }

        private long _pvpRaiseTick;
        private bool _pvpRestLocked;

        /// <summary>
        /// The group rests (PvpSquad.Resting): this bot first casts what it keeps up, one buff a
        /// turn, then rests where it stands; the Bard's endurance song plays through it. True
        /// when the turn is taken. A bot that was resting and no longer is gets up.
        /// </summary>
        private bool TryPvpRest()
        {
            if (BotBody?.IsPvpBot != true || !Body.IsAlive || PvpSquad?.Resting != true)
            {
                if (_pvpRestLocked)
                {
                    _pvpRestLocked = false;
                    BotBody?.WakeRecoveryRest();
                }
                return false;
            }
            if (Body.IsIncapacitated)
                return true;
            // A buff on its way is finished before anything else.
            if (Body.IsCasting || Body.castingComponent?.HasPendingSkillRequests == true)
                return true;
            if (HasAggro)
                ClearAggroList();
            if (Body.attackComponent.AttackState)
                Body.StopAttack();
            // At rest it also casts again the timers that are half run (PvpTimerRunsLow).
            _pvpRefreshesTimers = true;
            bool buffed;
            try
            {
                buffed = TryMaintainTravelAndClassBuffs();
            }
            finally
            {
                _pvpRefreshesTimers = false;
            }
            if (buffed)
            {
                _pvpRestLocked = false;
                return true;
            }
            TryMaintainClassicSongTwist();
            if (Body.IsCasting || Body.castingComponent?.HasPendingSkillRequests == true)
                return true;
            if (TryPvpRestCure())
            {
                _pvpRestLocked = false;
                return true;
            }
            // It sits down with the others: while it is still on its way to the group's place it does not sit (the group
            // is walked in by PvpTeam.GatherAt).
            if (PvpSquad.WalksInToRest(BotBody))
            {
                if (BotBody.IsRecoveryResting)
                    BotBody.WakeRecoveryRest();
                _pvpRestLocked = false;
                return true;
            }
            if (!BotBody.IsRecoveryResting)
                BotBody.BeginRecoveryRest();
            _pvpRestLocked = true;
            return true;
        }

        private bool _pvpRefreshesTimers;

        /// <summary>
        /// A timer is a buff that runs out by itself after two minutes or more; at a rest it is cast again once half
        /// of it has run. The owner, 2026-11-26, having rested six times in a session and seen it at two of them (at
        /// the others the timers had 15 to 18 minutes left), and offered a quarter: "that's fine to keep the 50% then".
        /// </summary>
        public const int TimerAtLeastSeconds = 120;
        public const double TimerCastAgainBelowShare = 0.5;

        /// <summary>
        /// Whether a buff with a timer is cast again at a rest: half of its time has run. The
        /// owner, 2026-11-26: "when we use the /8v8 call rest or the bot call for a rest, could we
        /// make sure druid and caster cast their timers? Like druid cast all 3 resists, caster
        /// their self bubble and buffs". A bot cast a buff only where it was missing, so a timer
        /// (twenty minutes for a Druid's resistances and an Eldritch's two barriers) was cast
        /// again only after it had run out, and it runs out when it pleases: in the owner's
        /// session of that day the Druid's three resistances were cast as the group stood up, at
        /// 480 seconds, and again at the rest at 1,785; by their timer they had run out some 85
        /// seconds before that, in the middle of the fight that had begun at 1,582.
        /// </summary>
        public static bool CastsTimerAgainAtRest(double timerSeconds, double leftSeconds) =>
            timerSeconds >= TimerAtLeastSeconds && leftSeconds < timerSeconds * TimerCastAgainBelowShare;

        /// <summary>
        /// At its group's rest: this very spell is on the target and half run
        /// (<see cref="CastsTimerAgainAtRest"/>). Only the same spell: that one is renewed by a
        /// cast (EffectListComponent, "the same spell ID"), where a cast under another buff of
        /// its kind may be for nothing. And not twice within a minute, whatever became of the
        /// first cast: a resting bot must not cast one buff over and over and never sit down.
        /// </summary>
        private bool PvpTimerRunsLow(GameLiving target, Spell spell)
        {
            if (!_pvpRefreshesTimers || target == null || spell == null || spell.IsConcentration || spell.IsPulsing)
                return false;
            long now = GameLoop.GameLoopTime;
            if (_pvpTimerCastAgainAt.TryGetValue(spell.ID, out long castAt) && now - castAt < 60_000)
                return false;
            ECSGameSpellEffect running = target.effectListComponent.GetSpellEffects()
                .FirstOrDefault(effect => effect?.SpellHandler?.Spell?.ID == spell.ID && !effect.IsDisabled);
            if (running == null || !CastsTimerAgainAtRest(running.Duration / 1000.0, (running.ExpireTick - now) / 1000.0))
                return false;
            _pvpTimerCastAgainAt[spell.ID] = now;
            PvpRecorder.Decision(BotBody, new { kind = "TimerAgain", spell = spell.Name, d = target.Name, left = (int)((running.ExpireTick - now) / 1000), rest = true }, [], null);
            return true;
        }

        private readonly Dictionary<int, long> _pvpTimerCastAgainAt = [];

        /// <summary>On the road a group stops to cast a timer again once only this share of it is left: five minutes of a Druid's twenty.</summary>
        public const double TimerStopsTheGroupBelowShare = 0.25;
        /// <summary>A rest is held for timers that are still to be cast again for this long from its beginning, full or not.</summary>
        public const int TimersHoldARestMilliseconds = 40_000;
        /// <summary>A group stops on the road for its timers no more often than this.</summary>
        public const int TimerStopEveryMilliseconds = 300_000;

        /// <summary>
        /// Whether a group rests for its timers, hurt or not: after a fight, and until it has
        /// rested since, when one of them is half run; later on the road, when only a quarter of
        /// one is left. At the rest they are cast again (<see cref="CastsTimerAgainAtRest"/>).
        /// The owner, 2026-11-26: "I just want to avoid that we get in a fight and the resist
        /// wear off or the caster shield / buble wear off". Cast again only at a rest that was
        /// taken for somebody low, a timer with a little over half left was passed over, and the
        /// group could be on the road and in two fights before the next rest.
        /// </summary>
        public static bool RestsForItsTimers(double leastShareLeft, bool hasRestedSinceItsFight) =>
            leastShareLeft < (hasRestedSinceItsFight ? TimerStopsTheGroupBelowShare : TimerCastAgainBelowShare);

        /// <summary>
        /// What is left of the timer of this bot's own that has the least left, as a share of its
        /// time: of the buffs with a timer it has cast on itself or its group and would cast
        /// again. 1 with none. Never throws: it is asked from the evening's turn.
        /// </summary>
        public double PvpLeastTimerShare()
        {
            try
            {
                if (BotBody is not { IsPvpBot: true, IsAlive: true } bot)
                    return 1;
                HashSet<int> own = (bot.MiscSpells ?? []).Concat(bot.InstantMiscSpells ?? [])
                    .Where(spell => spell != null && !spell.IsHarmful && spell.Level <= bot.Level && !spell.IsConcentration && !spell.IsPulsing &&
                                    IsMaintainableClassBuff(spell) && !IsShortCombatShield(spell) && !HoldsBackPvpBuff(spell))
                    .Select(spell => spell.ID).ToHashSet();
                if (own.Count == 0)
                    return 1;
                long now = GameLoop.GameLoopTime;
                double least = 1;
                foreach (ECSGameSpellEffect running in bot.effectListComponent.GetSpellEffects())
                {
                    if (running?.SpellHandler?.Spell is not { } spell || running.IsDisabled || running.SpellHandler.Caster != bot ||
                        !own.Contains(spell.ID) || running.Duration < TimerAtLeastSeconds * 1_000L)
                        continue;
                    least = Math.Min(least, (running.ExpireTick - now) / (double)running.Duration);
                }
                return least;
            }
            catch (Exception)
            {
                return 1;
            }
        }

        private long _pvpRestCureAt;

        /// <summary>
        /// At rest a member that can takes off its group what the fight left on it: disease,
        /// poison and nearsight, from every member in its reach and from the player. The owner,
        /// 2026-11-24: "When resting, druid do not cure desease, nearsight, etc." In a fight a
        /// nearsight is lifted from a healer or caster of bots only and a disease between two
        /// heals; at rest nothing was, and a group set off again diseased. One cure at a time for
        /// a member: what another is already casting at it is left to that one.
        /// </summary>
        private bool TryPvpRestCure()
        {
            if (Body.Group == null || GameLoop.GameLoopTime < _pvpRestCureAt)
                return false;
            Spell Ready(params Spell[] cures) => cures.FirstOrDefault(CheckHealSpell);
            foreach (GameLiving member in Body.Group.GetMembersInTheGroup().Where(member => member.IsAlive && InSupportReach(member))
                         .OrderBy(member => member is GamePlayer ? 0 : 1))
            {
                Spell cure = PvpHealPlan.RestCure(member.IsDiseased, member.IsPoisoned, member.effectListComponent.ContainsEffectForEffectType(eEffect.Nearsight)) switch
                {
                    eSpellType.CureDisease => Ready(BotBody.CureDisease, BotBody.CureDiseaseGroup),
                    eSpellType.CurePoison => Ready(BotBody.CurePoison, BotBody.CurePoisonGroup),
                    eSpellType.CureNearsightCustom => Ready(BotBody.CureNearsight),
                    _ => null,
                };
                if (cure == null || member != Body && !Body.IsWithinRadius(member, cure.CalculateEffectiveRange(Body)) ||
                    Body.Group.GetMembersInTheGroup().OfType<GameBot>().Any(other => other != Body && other.IsCasting && other.TargetObject == member &&
                        other.castingComponent?.SpellHandler?.Spell?.SpellType == cure.SpellType))
                    continue;
                GameObject before = Body.TargetObject;
                Body.TargetObject = member;
                if (BotBody.CastSpell(cure, m_mobSpellLine, false))
                {
                    _pvpRestCureAt = GameLoop.GameLoopTime + 1_000;
                    PvpRecorder.Decision(BotBody, new { kind = cure.SpellType == eSpellType.CureNearsightCustom ? "CureNearsight" : "Cure", spell = cure.Name, d = member.Name, rest = true }, [], null);
                    if (cure.IsInstantCast)
                        Body.TargetObject = before;
                    return true;
                }
                Body.TargetObject = before;
            }
            return false;
        }

        /// <summary>
        /// "/8v8 call retreat" (PvpSquad.CallRetreat): while the call stands this bot drops its
        /// cast, its attack and its targets, answers nobody, and runs behind the player that
        /// called, as it does on the road; its Bard, Skald or Minstrel plays the speed song.
        /// True when the turn is taken.
        /// </summary>
        private bool TryPvpRetreat()
        {
            if (BotBody?.IsPvpBot != true || !Body.IsAlive)
                return false;
            // A group of bots that has broken off (PvpSquad.Leaving) runs the same way, for a point and not behind a player.
            if (PvpSquad?.RetreatsTo is not { } leader)
                return PvpSquad?.LeavesTo is { } away && PvpLeaves(away);
            // A song may be played on the run; anything else is let go.
            if (Body.IsCasting && Body.castingComponent?.SpellHandler?.Spell is not { InstrumentRequirement: not 0, IsHarmful: false })
                Body.StopCurrentSpellcast();
            if (Body.attackComponent.AttackState)
                Body.StopAttack();
            if (HasAggro)
                ClearAggroList();
            PvpAssignedTarget = null;
            if (Body.TargetObject != null && Body.TargetObject != leader)
                Body.TargetObject = null;
            if (Body.IsIncapacitated)
                return true;
            if (!Body.IsWithinRadius(leader, BotManager.MAX_FOLLOW_DISTANCE))
            {
                if (Body.FollowTarget != null)
                    Body.StopFollowing();
                if (!Body.IsMoving || GameLoop.GameLoopTime % 2_000 < 1_000)
                    Body.PathTo(new Vector3(leader.X, leader.Y, leader.Z), Body.MaxSpeed);
            }
            else if (Body.FollowTarget != leader)
            {
                Body.Follow(leader, BotManager.FOLLOW_DISTANCE, BotManager.MAX_FOLLOW_DISTANCE);
                FSM.SetCurrentState(eFSMStateType.FOLLOW);
            }
            TryMaintainClassicSongTwist();
            return true;
        }

        private bool PvpLeaves(Vector3 to)
        {
            if (Body.IsCasting && Body.castingComponent?.SpellHandler?.Spell is not { InstrumentRequirement: not 0, IsHarmful: false })
                Body.StopCurrentSpellcast();
            if (Body.attackComponent.AttackState)
                Body.StopAttack();
            if (HasAggro)
                ClearAggroList();
            PvpAssignedTarget = null;
            if (Body.TargetObject != null)
                Body.TargetObject = null;
            if (Body.IsIncapacitated)
                return true;
            if (Body.FollowTarget != null)
                Body.StopFollowing();
            if (!Body.IsWithinRadius(new Point3D((int)to.X, (int)to.Y, (int)to.Z), 200) && (!Body.IsMoving || GameLoop.GameLoopTime % 2_000 < 1_000))
                Body.PathTo(to, Body.MaxSpeed);
            TryMaintainClassicSongTwist();
            return true;
        }

        /// <summary>
        /// The dead groupmate an 8v8 bot raises now, from where it stands, or null
        /// (<see cref="PvpHealWeights.Raises"/>): a healer first, then the nearest.
        /// </summary>
        private GameLiving PvpCorpseToRaise(Spell resurrection)
        {
            // (A group that falls back as one leaves its dead where they are, and so does one with too many on it:
            // the raised come back hurt and without their buffs, and the Cleric and the Cabalist of the eight that
            // lost to sixteen on 2026-11-23 were raised and died again.)
            if (BotBody?.IsPvpBot != true || PvpSquad == null || PvpEnemies == null || Body.Group == null || resurrection == null ||
                Body.IsCasting || Body.IsIncapacitated || Body.IsSilenced || PvpSquad.Kiting || PvpSquad.TooManyOnIt)
                return null;
            PvpWeights all = PvpWeights.Of(BotBody);
            int range = BotBody.castingComponent.CalculateSpellRange(resurrection);
            // Its own last swing counts too: a Paladin that has just struck cannot cast, and in 24 matches
            // six of its seven tries were refused at once, each one a step out of its fight for nothing.
            bool beingHit = Body.IsBeingInterrupted || PvpEnemies.Any(enemy => enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned &&
                enemy.TargetObject == Body && PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee && enemy.IsWithinRadius(Body, 500));
            // The living come first for a member that heals; one that does not (a Paladin) has nobody to weigh.
            int lowest = 100;
            if (PvpMember.RoleOf(BotBody) == PvpRole.Heal)
            {
                foreach (GameLiving member in Body.Group.GetMembersInTheGroup())
                {
                    if (member != Body && member.IsAlive && member.HealthPercent < lowest && Body.IsWithinRadius(member, all.Position.HealRange + 300))
                        lowest = member.HealthPercent;
                }
            }
            bool anotherRaises = PvpSquad.AnotherHealerIsRaising(BotBody);
            // After the fight: with the power the spell takes, the player first (PvpHealWeights.RaisesAfterTheFight).
            bool fightIsOver = !PvpEnemies.Any(enemy => enemy.IsAlive);
            bool hasThePower = BotBody.Mana >= BotBody.PowerCost(resurrection);
            GameLiving corpse = Body.Group.GetMembersInTheGroup()
                .Where(member => member != Body && !member.IsAlive && member.ObjectState == GameObject.eObjectState.Active &&
                                 member.CurrentRegionID == Body.CurrentRegionID &&
                                 member.TempProperties.GetProperty<GameLiving>("RESURRECT_CASTER") == null)
                .OrderBy(member => PvpHealWeights.RaisedInTurn(fightIsOver, member is GamePlayer, PvpMember.RoleOf(member) == PvpRole.Heal))
                .ThenBy(member => Body.GetDistanceTo(member))
                .FirstOrDefault(member => (fightIsOver
                        ? all.Heal.RaisesAfterTheFight((int)Body.GetDistanceTo(member), range, hasThePower)
                        : all.Heal.Raises((int)Body.GetDistanceTo(member), range, Body.ManaPercent, beingHit, lowest,
                            PvpEnemies.Count(enemy => enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned &&
                                PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee &&
                                enemy.IsWithinRadius(member, all.Heal.ResurrectNoFighterWithin)), anotherRaises)) &&
                    BotGroupSupport.HasCorpseLineOfSight(BotBody, member));
            if (corpse == null || !PvpSquad.ClaimRaise(BotBody, corpse, resurrection.CastTime))
                return null;
            PvpRecorder.Decision(BotBody, new { kind = "Raise", d = corpse.Name, hp = lowest, score = (int)Body.GetDistanceTo(corpse) }, [], null);
            return corpse;
        }

        /// <summary>
        /// A healer walks to within reach of a dead groupmate to raise it, when it has the power, is
        /// not being hit, nobody alive in its reach is badly hurt and no free enemy fighter stands
        /// at the corpse (<see cref="PvpHealWeights.WalksToRaise"/>). True when it is on its way.
        /// </summary>
        private bool PvpWalkToRaise(Spell resurrection)
        {
            if (BotBody?.IsPvpBot != true || PvpEnemies == null || Body.Group == null || resurrection == null ||
                PvpMember.RoleOf(BotBody) != PvpRole.Heal || Body.IsCasting || Body.IsIncapacitated || PvpSquad?.Kiting == true || PvpSquad?.TooManyOnIt == true)
                return false;
            PvpWeights all = PvpWeights.Of(BotBody);
            int range = BotBody.castingComponent.CalculateSpellRange(resurrection);
            bool beingHit = Body.IsBeingInterruptedByOther || PvpEnemies.Any(enemy => enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned &&
                enemy.TargetObject == Body && PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee && enemy.IsWithinRadius(Body, 500));
            int lowest = 100;
            foreach (GameLiving member in Body.Group.GetMembersInTheGroup())
            {
                if (member != Body && member.IsAlive && member.HealthPercent < lowest && Body.IsWithinRadius(member, all.Position.HealRange + 300))
                    lowest = member.HealthPercent;
            }
            GameLiving corpse = Body.Group.GetMembersInTheGroup()
                .Where(member => member != Body && !member.IsAlive && member.ObjectState == GameObject.eObjectState.Active &&
                                 member.CurrentRegionID == Body.CurrentRegionID &&
                                 member.TempProperties.GetProperty<GameLiving>("RESURRECT_CASTER") == null)
                .OrderBy(member => Body.GetDistanceTo(member))
                .FirstOrDefault(member => all.Heal.WalksToRaise((int)Body.GetDistanceTo(member), range,
                    Body.Mana >= (int)(Body.MaxMana * Math.Max(.1f, .5f + (member.Level - Body.Level) / (float)Math.Max(1, (int)Body.Level))),
                    beingHit, lowest,
                    PvpEnemies.Count(enemy => enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned &&
                        PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee &&
                        enemy.IsWithinRadius(member, all.Heal.ResurrectNoFighterWithin))));
            // One healer goes: the body is this one's for the next few seconds, and nobody sets off while another raises.
            if (corpse == null || PvpSquad == null || PvpSquad.AnotherHealerIsRaising(BotBody) || !PvpSquad.ClaimRaise(BotBody, corpse, 2_000))
                return false;
            Body.PathTo(PvpSight.OnGround(Body.CurrentZone, PvpPositioning.StopShortOf(
                new Vector3(Body.X, Body.Y, Body.Z), new Vector3(corpse.X, corpse.Y, corpse.Z), range)), Body.MaxSpeed);
            long now = GameLoop.GameLoopTime;
            if (now - _pvpRaiseTick >= 3_000)
            {
                _pvpRaiseTick = now;
                PvpRecorder.Decision(BotBody, new { kind = "GoRaise", d = corpse.Name, score = (int)Body.GetDistanceTo(corpse) }, [], null);
            }
            return true;
        }

        /// <summary>True for a healer with a badly hurt groupmate in reach and no enemy fighter on itself.</summary>
        public bool PvpBreaksOffToHeal()
        {
            if (BotBody?.PvpBuild == null || PvpEnemies == null || Body.Group == null || PvpMember.RoleOf(BotBody) != PvpRole.Heal)
                return false;
            PvpPositionWeights weights = PvpWeights.Of(BotBody).Position;
            int lowest = 100;
            foreach (GameLiving member in Body.Group.GetMembersInTheGroup())
            {
                if (member != Body && member.IsAlive && member.HealthPercent < lowest && Body.IsWithinRadius(member, weights.HealRange + 300))
                    lowest = member.HealthPercent;
            }
            bool hunted = PvpEnemies.Any(enemy => enemy.IsAlive && !enemy.IsMezzed && !enemy.IsStunned && enemy.TargetObject == Body &&
                PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee && enemy.IsWithinRadius(Body, 500));
            return weights.BreaksOffToHeal(true, hunted, lowest);
        }

        /// <summary>The enemy of this bot's last scored choice, the range of the spell chosen, and when: what it must stay in range of.</summary>
        public GameLiving PvpWorkTarget { get; private set; }
        public int PvpWorkRange { get; private set; }
        public long PvpWorkTick { get; private set; }

        /// <summary>
        /// Set by the squad for a fighter whose target should be stopped, not killed: an enemy that
        /// is on the group's healers and casters. Its next style is a stun or a snare when it has
        /// one that would land (bots/pvp/PvpStylePolicy.cs).
        /// </summary>
        public bool PvpWantsControlStyle { get; set; }

        /// <summary>
        /// A fighter that carries a two-hander and a shield takes out the one this moment calls
        /// for (bots/pvp/PvpWeaponSwap.cs): the shield to Slam a target that can be stunned, and
        /// to guard while it attacks nobody; the two-hander for the rest. False for every other bot.
        /// </summary>
        public bool PvpChooseHands()
        {
            if (BotBody?.IsPvpBot != true || !PvpWeaponSwap.Applies(BotBody, out _))
                return false;
            if (Body.IsIncapacitated || Body.IsCasting)
                return true;
            // The enemy it is swinging at, which is not always the one the squad named: with the
            // shield out for a kill target it was not on, the Armsman threw 14 one-handed styles
            // in the first eight matches.
            GameLiving target = Body.attackComponent.AttackState && Body.TargetObject is GameLiving { IsAlive: true } own ? own
                : PvpAssignedTarget is { IsAlive: true } aim ? aim : null;
            if (PvpWeaponSwap.Slot(BotBody, target, PvpSquad?.WardOf(BotBody) != null) is not { } slot || slot == Body.ActiveWeaponSlot)
                return true;
            Body.SwitchWeapon(slot);
            if (PvpSquad?.FightStarted == true)
                PvpRecorder.Decision(BotBody, new { kind = slot == eActiveWeaponSlot.Standard ? "ShieldOut" : "TwoHander", d = target?.Name, score = 0 }, [], null);
            return true;
        }

        /// <summary>
        /// This bot's best point-blank area damage spell (no range, a radius around the caster);
        /// null when it has none. The scored choice never casts it: among the damage spells the
        /// highest level is taken, and that is the caster's own line. It is cast on enemy pets
        /// (bots/pvp/PvpPetClear.cs).
        /// </summary>
        public Spell PvpPointBlank() => BotBody?.IsPvpBot != true ? null : Body.Spells?
            .Where(spell => spell != null && spell.SpellType == eSpellType.DirectDamage && spell.Range <= 0 && spell.Radius > 0 &&
                            spell.CastTime > 0 && spell.Level <= Body.Level)
            .OrderByDescending(spell => spell.Level).FirstOrDefault();

        /// <summary>Casts the point-blank area spell on the enemy pets around this bot. True when the cast began.</summary>
        public bool PvpBlast(int pets)
        {
            if (Body.IsCasting || Body.IsIncapacitated || Body.IsBeingInterrupted || Body.castingComponent?.HasPendingSkillRequests == true ||
                PvpPointBlank() is not { } spell || Body.GetSkillDisabledDuration(spell) != 0 || Body.Mana < BotBody.PowerCost(spell))
                return false;
            Body.StopMovingOnPath();
            Body.StopMoving();
            Body.StopAttack();
            if (!Body.CastSpell(spell, m_mobSpellLine))
                return false;
            long now = GameLoop.GameLoopTime;
            (_pvpLastCast, _pvpLastCastTarget, _pvpLastCastTick) = (spell, null, now);
            _pvpControlSpellId = spell.ID;
            _pvpControlCastUntil = now + spell.CastTime + 2_000;
            PvpRecorder.Decision(BotBody, new { kind = "Blast", spell = spell.Name, score = pets, caught = pets }, [], null);
            return true;
        }

        private long _pvpBehindTick;

        /// <summary>
        /// A fighter in reach of a stunned target walks behind it when it has a style that needs
        /// the target's back (bots/pvp/PvpWeaponSwap.cs). True when the walk was started: it is
        /// the bot's turn until it arrives, and the attack goes on.
        /// </summary>
        private bool PvpStepBehind(GameLiving target)
        {
            if (BotBody?.IsPvpBot != true || PvpSquad == null || target?.IsAlive != true || Body.IsIncapacitated)
                return false;
            PvpPositionWeights weights = PvpWeights.Of(BotBody).Position;
            long now = GameLoop.GameLoopTime;
            if (!weights.FightersStepBehindStunned || now - _pvpBehindTick < weights.StepBehindEveryMilliseconds)
                return false;
            DOL.Database.DbInventoryItem left = BotBody.Inventory?.GetItem(eInventorySlot.LeftHandWeapon);
            bool backStyle = BotBody.StylesBack?.Any(style => style.Level <= Body.Level && PvpStylePolicy.Allows(BotBody, style) &&
                BotMeleeStylePolicy.MatchesWeapon(style, Body.ActiveWeapon, left, Body.ActiveWeaponSlot)) == true;
            int distance = (int)Body.GetDistanceTo(target);
            if (!PvpWeaponSwap.StepsBehind(target.IsStunned && !target.IsMoving, backStyle, target.GetAngle(Body), distance, Body.MeleeAttackRange))
                return false;
            Point2D behind = target.GetPointFromHeading((ushort)((target.Heading + 2048) % 4096), weights.StepBehindDistance);
            _pvpBehindTick = now;
            // The follow would stop the walk at once: the fighter is already as near as it follows.
            Body.StopFollowing();
            PvpMoveFrom = now;
            PvpMoveUntil = now + Math.Clamp((long)((distance + weights.StepBehindDistance) / (double)Math.Max(100, (int)Body.MaxSpeed) * 1000) + 300, 600, 2_500);
            Body.PathTo(new Vector3(behind.X, behind.Y, target.Z), Body.MaxSpeed);
            PvpRecorder.Decision(BotBody, new { kind = "StepBehind", d = target.Name, score = 0 }, [], null);
            return true;
        }

        /// <summary>
        /// Set by the squad for a shield fighter it keeps beside its ward and for a fighter it has
        /// called back: it attacks nobody on its own. Without this its own brain went for whoever
        /// had hit it, the squad walked it back a second later, and so on: the owner, watching on
        /// 2026-11-18, saw the Warrior "walk back and forth between the enemy and the healer
        /// without doing anything". In the recordings a shield fighter told to stay was moving
        /// 54 to 64% of that time and had an attack target for 60%.
        /// </summary>
        public bool PvpStaysPut { get; set; }

        /// <summary>
        /// The heal for several to cast now, or null: several members in reach are hurt, nobody is
        /// trained, and of the spread and group heals that are ready this one restores the most
        /// health for its power (bots/pvp/PvpHealPlan.cs).
        /// </summary>
        private Spell PvpAreaHeal()
        {
            if (BotBody?.IsPvpBot != true || BotBody.PvpAreaHeals is not { Count: > 0 } heals || Body.Group == null)
                return null;
            List<GameLiving> members = Body.Group.GetMembersInTheGroup().Where(member => member.IsAlive && InSupportReach(member)).ToList();
            // (The owner's rule: four or more damaged, or the one in need is out of sight. PvpHealWeights.SpreadHealMinHurt.)
            PvpWeights its = PvpWeights.Of(BotBody);
            var nav = PathfindingProvider.Instance;
            var here = new Vector3(Body.X, Body.Y, Body.Z);
            bool hidden = its.Heal.AreaHealWhenOutOfSight && members.Any(member => member != Body && member.HealthPercent < its.Heal.HealBelowPercent &&
                !(nav.HasLineOfSight(Body.CurrentZone, here, new Vector3(member.X, member.Y, member.Z), nav.DefaultFilters) && PvpSight.ClearForACast(Body, member, its)));
            if (!PvpHealPlan.AreaHealIsRight(PvpHealPlan.SeveralHurt(members.Select(member => (int)member.HealthPercent), PvpSquad?.Trained != null, its.Heal), hidden, its.Heal))
                return null;
            List<Spell> ready = heals.Where(CheckHealSpell).ToList();
            int best = PvpHealPlan.BestAreaHeal(members.Select(member => member.MaxHealth - member.Health).ToList(),
                ready.Select(spell => (spell.SpellType == eSpellType.SpreadHeal, (int)GameBot.HealAmount(spell, Body), BotBody.PowerCost(spell))).ToList(),
                Body.Group.MemberCount);
            return best < 0 ? null : ready[best];
        }

        /// <summary>
        /// True for an 8v8 caster or control healer under a squad brain: with nothing worth casting it
        /// stands, and does not hand the turn to the old brain's walk toward its attack target
        /// (<see cref="PvpPositionWeights.CastersStandWithNothingToCast"/>).
        /// </summary>
        public bool PvpStandsWithNothingToCast()
        {
            if (BotBody?.IsPvpBot != true || PvpSquad == null || BotBody.PvpBuild == null || PvpEnemies == null)
                return false;
            PvpWeights weights = PvpWeights.Of(BotBody);
            var characterClass = (eCharacterClass)BotBody.CharacterClass.ID;
            return weights.Position.CastersStandWithNothingToCast && PvpBuffPlan.ArchetypeOf(characterClass) != PvpBuffArchetype.Melee &&
                PvpActionChoice.ScoresOffense(characterClass, weights);
        }

        /// <summary>True while this bot's group holds: it stands and casts what reaches (bots/pvp/PvpReading.cs).</summary>
        private bool PvpHolding() => BotBody?.IsPvpBot == true && PvpSquad?.Order == PvpOrder.Hold;

        private long _pvpNoActionRecordedTick, _pvpApproachTick;
        // The last cast this routine started, to notice one that was accepted and then never began.
        private Spell _pvpLastCast;
        private GameLiving _pvpLastCastTarget;
        private long _pvpLastCastTick;

        private static readonly eSpellType[] PvpDamageTypes = [eSpellType.DirectDamage, eSpellType.Lifedrain, eSpellType.DirectDamageWithDebuff];

        private static bool IsPvpSpellOfKind(Spell spell, PvpActionKind kind) => kind switch
        {
            PvpActionKind.Nearsight => spell.SpellType == eSpellType.Nearsight,
            PvpActionKind.Disease => spell.SpellType == eSpellType.Disease,
            // Not an area nuke aimed at an enemy: that one is kept for pets (AreaDamage, below).
            PvpActionKind.Damage => PvpDamageTypes.Contains(spell.SpellType) && !IsPvpSpellOfKind(spell, PvpActionKind.AreaDamage),
            // A root, an area root, or a snare nuke: whichever the class has (the highest level is taken).
            PvpActionKind.Root => spell.SpellType is eSpellType.SpeedDecrease or eSpellType.DamageSpeedDecrease,
            PvpActionKind.Pet => spell.SpellType == eSpellType.SummonTheurgistPet,
            PvpActionKind.Mez => spell.SpellType == eSpellType.Mesmerize && spell.Radius == 0,
            PvpActionKind.MezArea => spell.SpellType == eSpellType.Mesmerize && spell.Radius > 0,
            PvpActionKind.Stun => spell.SpellType == eSpellType.Stun,
            PvpActionKind.CureMez => spell.SpellType == eSpellType.CureMezz,
            PvpActionKind.Amnesia => spell.SpellType == eSpellType.Amnesia,
            PvpActionKind.Debuff => IsPvpResistDebuff(spell),
            // Aimed at an enemy: a ground-targeted one (Mark of Undoing) was begun 21 times in two matches and never finished.
            PvpActionKind.AreaDamage => spell.SpellType == eSpellType.DirectDamage && spell.Radius > 0 && spell.Range > 0 && spell.Target == eSpellTarget.ENEMY,
            _ => false,
        };

        private static bool IsPvpResistDebuff(Spell spell) => spell.SpellType is eSpellType.BodyResistDebuff or eSpellType.SpiritResistDebuff or
            eSpellType.EnergyResistDebuff or eSpellType.HeatResistDebuff or eSpellType.ColdResistDebuff or eSpellType.MatterResistDebuff;

        // As this bot knows it: an immunity that has just ended it still takes to be there for a while (PvpNotice).
        private bool PvpMezzedOrImmune(GameLiving living) =>
            living.IsMezzed || PvpNotice.ThinksImmune(Body, living, eEffect.MezImmunity);

        private bool PvpStunnedOrImmune(GameLiving living) =>
            living.IsStunned || PvpNotice.ThinksImmune(Body, living, eEffect.StunImmunity);

        // As it is: for a cast under way, which is dropped when it can no longer land.
        private static bool PvpMezzedOrImmuneNow(GameLiving living) =>
            living.IsMezzed || living.effectListComponent.ContainsEffectForEffectType(eEffect.MezImmunity);

        private static bool PvpStunnedOrImmuneNow(GameLiving living) =>
            living.IsStunned || living.effectListComponent.ContainsEffectForEffectType(eEffect.StunImmunity);

        /// <summary>True when this class's mez and stun are chosen by score here, not by the old crowd-control routine.</summary>
        private bool PvpCrowdControlIsScored() => PvpEnemies != null && BotBody?.IsPvpBot == true &&
            PvpActionChoice.KindsFor((eCharacterClass)BotBody.CharacterClass.ID, PvpWeights.Of(BotBody))
                .Any(kind => kind is PvpActionKind.Mez or PvpActionKind.MezArea or PvpActionKind.Stun);

        /// <summary>Whom to wake first, for the healing routine's cure mez; 0 for everyone outside an 8v8 group.</summary>
        private int PvpCureMezPriority(GameLiving member)
        {
            // A bot of the group, or a player in it.
            eCharacterClass own = PvpMember.ClassOf(member);
            if (BotBody?.IsPvpBot != true || own == eCharacterClass.Unknown)
                return 0;
            PvpWeights weights = PvpWeights.Of(BotBody);
            return PvpBuffPlan.ArchetypeOf(own) switch
            {
                PvpBuffArchetype.Healer => weights.CureMezOnHealer,
                PvpBuffArchetype.Caster => weights.CureMezOnCaster,
                _ => 0,
            } + (PvpActionChoice.IsController(own, weights) ? weights.CureMezOnController : 0);
        }

        /// <summary>A fight is on: a living enemy is within reach of this bot.</summary>
        private bool PvpFightIsOn() => PvpEnemies != null &&
            PvpEnemies.Any(enemy => enemy.IsAlive && Body.IsWithinRadius(enemy, PvpScrim.EngageRange));

        /// <summary>
        /// Walks into casting range of <paramref name="aim"/>, to a place apart from the group's
        /// other healers and casters (bots/pvp/PvpSpacing.cs): everybody heading straight for the
        /// same enemy is what put eight bots inside one area mez.
        /// </summary>
        private void PvpWalkIntoRange(GameLiving aim, Spell spell)
        {
            PvpSpreadWeights spread = PvpWeights.Of(BotBody).Spread;
            int range = (spell.Target == eSpellTarget.SELF || spell.Range <= 0 ? spell.Radius : spell.CalculateEffectiveRange(Body)) - spread.RangeMargin;
            // In range already, so the trouble is something in the way; or spreading is off: straight at it.
            if (spread.MinSpacing <= 0 || range <= 0 || Body.IsWithinRadius(aim, range))
            {
                Body.PathTo(new Vector3(aim.X, aim.Y, aim.Z), Body.MaxSpeed);
                return;
            }
            List<Vector2> mates = Body.Group?.GetMembersInTheGroup()
                .Where(member => member != Body && member.IsAlive && member is GameBot { PvpBuild: { } build } &&
                                 PvpBuffPlan.ArchetypeOf(build.Class) != PvpBuffArchetype.Melee)
                .Select(member => new Vector2(member.X, member.Y)).ToList() ?? [];
            Vector2 point = PvpSpacing.ApproachPoint(new Vector2(Body.X, Body.Y), new Vector2(aim.X, aim.Y), range, mates, spread.MinSpacing);
            var nav = PathfindingProvider.Instance;
            // The place needs a height the navmesh knows, or no path is found.
            Body.PathTo(nav.GetFloorBeneath(Body.CurrentZone, new Vector3(point.X, point.Y, 20_000), 20_000, nav.DefaultFilters)
                is { } floor && floor.Z < 19_999 ? floor : new Vector3(point.X, point.Y, Body.Z), Body.MaxSpeed);
        }

        /// <summary>
        /// True when this healer's scored choice goes before its healing routine this turn: its
        /// build leans to control or interrupts (<see cref="PvpWeights.HealsFirstBelow"/>) and
        /// nobody of the group in reach is hurt badly enough to claim the turn.
        /// </summary>
        private bool PvpScoredChoiceBeforeHeals()
        {
            if (BotBody?.IsPvpBot != true || PvpEnemies == null || BotBody.PvpBuild == null)
                return false;
            PvpWeights weights = PvpWeights.Of(BotBody);
            int below = weights.HealsFirstBelowFor(BotBody.PvpBuild.Id, PvpMember.ClassOf(BotBody));
            if (below > 100)
                return false;
            // With the group's healers dead, asleep or kept from casting, this one takes over.
            bool aHealerCanHeal = Body.Group?.GetMembersInTheGroup().OfType<GameBot>().Any(member =>
                member != Body && member.PvpBuild != null && PvpMember.ArchetypeOf(member) == PvpBuffArchetype.Healer &&
                weights.HealsFirstBelowFor(member.PvpBuild.Id, PvpMember.ClassOf(member)) > 100 &&
                member.IsAlive && !member.IsMezzed && !member.IsStunned && !member.IsSilenced && !member.IsBeingInterrupted) == true;
            below = PvpHealPlan.BackupHealsBelow(below, aHealerCanHeal, weights.Heal);
            // The trained member too only claims this healer's turn below that mark: healing it
            // from the first blow is the heal-first healers' work.
            return Body.Group?.GetMembersInTheGroup().Any(member => member.IsAlive && member.HealthPercent < below && InSupportReach(member)) != true;
        }

        /// <summary>
        /// True when this healer, built to heal, leaves waking <paramref name="sleeper"/> to the
        /// group's control healer: one that has cure mez, is itself able to act and is near enough
        /// (<see cref="PvpHealWeights.HealerLeavesCureMezToController"/>).
        /// </summary>
        private bool PvpLeavesCureMezToController(GameLiving sleeper)
        {
            if (BotBody?.IsPvpBot != true || BotBody.PvpBuild == null)
                return false;
            PvpWeights weights = PvpWeights.Of(BotBody);
            if (!weights.Heal.HealerLeavesCureMezToController ||
                weights.HealsFirstBelowFor(BotBody.PvpBuild.Id, PvpMember.ClassOf(BotBody)) <= 100)
                return false;
            return Body.Group?.GetMembersInTheGroup().OfType<GameBot>().Any(mate =>
                mate != BotBody && mate != sleeper && mate.IsAlive && !mate.IsMezzed && !mate.IsStunned && mate.CureMezz != null &&
                mate.PvpBuild != null && weights.HealsFirstBelowFor(mate.PvpBuild.Id, PvpMember.ClassOf(mate)) <= 100 &&
                mate.IsWithinRadius(sleeper, 2_000)) == true;
        }

        private long _pvpIdleRecordedTick;

        /// <summary>
        /// For the recording: a caster whose casts are scored spent this turn on something else
        /// (its healing routine, a cast still waiting to start), so the scored choice did not run.
        /// Written at most every two seconds, and only while a fight is on.
        /// </summary>
        private void RecordPvpTurnTaken(string by)
        {
            if (BotBody?.IsPvpBot != true || PvpEnemies == null || GameLoop.GameLoopTime - _pvpIdleRecordedTick < 2_000 ||
                Body.IsCasting || Body.IsIncapacitated || !PvpFightIsOn() ||
                !PvpActionChoice.Covers((eCharacterClass)BotBody.CharacterClass.ID, PvpWeights.Of(BotBody)))
                return;
            _pvpIdleRecordedTick = GameLoop.GameLoopTime;
            PvpRecorder.Decision(BotBody, null, [], new Dictionary<string, string> { ["turn taken by"] = by });
        }

        private bool TryPvpRotation()
        {
            if (PvpEnemies == null || BotBody?.IsPvpBot != true || Body.IsIncapacitated)
                return false;
            if (Body.castingComponent?.HasPendingSkillRequests == true)
            {
                RecordPvpTurnTaken("a cast waiting to start");
                return false;
            }
            // In the middle of a cast only an instant can be added, as a player does (owner, 2026-11-17:
            // a Bard fires its amnesia while it casts its mez).
            bool midCast = Body.IsCasting;
            // A bot of a slow level that is waiting before its next cast chooses nothing meanwhile (PvpHands).
            if (!midCast && PvpHands.Hesitates(BotBody))
            {
                RecordPvpTurnTaken("slow hands");
                return false;
            }
            var characterClass = (eCharacterClass)BotBody.CharacterClass.ID;
            PvpWeights weights = PvpWeights.Of(BotBody);
            // The target of the mez, stun or root it is casting has been dealt with or is immune now: stop, and choose again.
            if (midCast && weights.DropsAWastedControlCast && _pvpLastCast is { Radius: 0, CastTime: > 0 } running &&
                _pvpLastCastTarget is { } aimedAt && Body.castingComponent?.SpellHandler?.Spell?.ID == running.ID &&
                running.SpellType is eSpellType.Mesmerize or eSpellType.Stun or eSpellType.SpeedDecrease &&
                PvpWeights.ControlCastIsWasted(running.SpellType == eSpellType.Mesmerize, running.SpellType == eSpellType.Stun,
                    running.SpellType == eSpellType.SpeedDecrease, aimedAt.IsAlive, PvpMezzedOrImmuneNow(aimedAt), PvpStunnedOrImmuneNow(aimedAt),
                    aimedAt.effectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedDebuff) ||
                    aimedAt.effectListComponent.ContainsEffectForEffectType(eEffect.SnareImmunity)))
            {
                Body.StopCurrentSpellcast();
                PvpRecorder.Decision(BotBody, new { kind = "DropCast", spell = running.Name, d = aimedAt.Name, score = 0 }, [], null);
                _pvpLastCast = null;
                midCast = false;
            }
            if (!PvpActionChoice.Covers(characterClass, weights))
                return false;

            // What stood in the way, for the recorder: kind or count by reason.
            var blocked = new Dictionary<string, string>();

            // Choosing again while the last cast should still be running means it never began or was cut.
            // Half the listed cast time: dexterity shortens a cast to about 60% of it, and the two-second
            // nearsights that finished in 1.2 to 1.4 seconds were being counted as lost (2026-11-17).
            if (!midCast && _pvpLastCast is { CastTime: > 0 } lost && GameLoop.GameLoopTime - _pvpLastCastTick < Math.Min(lost.CastTime / 2, 1_500))
            {
                GameLiving aimed = _pvpLastCastTarget;
                int reach = lost.Target == eSpellTarget.SELF || lost.Range <= 0 ? lost.Radius : lost.CalculateEffectiveRange(Body);
                // Numbers before the semicolon, yes/no facts after it, so the analyzer can add equal causes up.
                blocked["previous cast lost"] = $"{lost.Name} after {GameLoop.GameLoopTime - _pvpLastCastTick} ms, {(aimed == null ? -1 : (int)Body.GetDistanceTo(aimed))} of {reach}; " +
                    $"moving {Body.IsMoving}, interrupted {Body.IsBeingInterrupted}, " +
                    $"target {(aimed == null ? "none" : !aimed.IsAlive ? "dead" : aimed.IsMezzed ? "mezzed" : "awake")}, " +
                    $"in range {aimed != null && Body.IsWithinRadius(aimed, reach)}, " +
                    $"still targeted {Body.TargetObject == aimed}, " +
                    $"may attack {aimed != null && GameServer.ServerRules.IsAllowedToAttack(Body, aimed, true)}, " +
                    $"in front {aimed != null && Body.IsObjectInFront(aimed, 180)}";
            }
            if (!midCast)
                _pvpLastCast = null;

            // The best castable spell of each kind this class uses, and the best instant: an instant
            // is an option of its own, because it can do what a spell with a cast time cannot.
            List<Spell> known = (Body.HarmfulSpells ?? []).Concat(Body.InstantHarmfulSpells ?? [])
                .Concat(BotBody.CrowdControlSpells ?? []).Concat(BotBody.InstantCrowdControlSpells ?? [])
                .Append(BotBody.CureMezz)
                // The old brain leaves amnesia out of its lists on purpose (it does nothing to a monster's cast).
                .Concat(Body.Spells?.Where(spell => spell?.SpellType == eSpellType.Amnesia) ?? [])
                // Nor does it list resistance debuffs.
                .Concat(Body.Spells?.Where(spell => spell != null && IsPvpResistDebuff(spell)) ?? [])
                // Nor every area nuke: the Runemaster's level 44 one was not in them.
                .Concat(Body.Spells?.Where(spell => spell != null && IsPvpSpellOfKind(spell, PvpActionKind.AreaDamage)) ?? [])
                .Where(spell => spell != null).DistinctBy(spell => spell.ID).ToList();
            // A resistance debuff is of use only against the kind of damage this caster's own spells do.
            HashSet<eDamageType> ownDamage = known.Where(spell => IsPvpSpellOfKind(spell, PvpActionKind.Damage)).Select(spell => spell.DamageType).ToHashSet();
            var spells = new Dictionary<PvpActionKind, Spell>();
            var instants = new Dictionary<PvpActionKind, Spell>();
            foreach (PvpActionKind kind in PvpActionChoice.KindsFor(characterClass, weights))
            {
                // The fight is won: nobody puts the last of them to sleep (PvpPositionWeights.MopsUp).
                if (PvpSquad?.MopsUp == true && PvpPositionWeights.NotInAMopUp(kind))
                    continue;
                List<Spell> ofKind = known.Where(spell => IsPvpSpellOfKind(spell, kind) && spell.Level <= Body.Level &&
                        (kind != PvpActionKind.Debuff || ownDamage.Contains(spell.DamageType)))
                    .OrderByDescending(spell => spell.Level).ToList();
                bool Ready(Spell spell) => Body.GetSkillDisabledDuration(spell) == 0 && Body.Mana >= BotBody.PowerCost(spell);
                Spell cast = midCast ? null : ofKind.FirstOrDefault(spell => spell.CastTime > 0 && Ready(spell) &&
                    (spell.Uninterruptible || !Body.IsBeingInterrupted));
                Spell instant = ofKind.FirstOrDefault(spell => spell.CastTime == 0 && Ready(spell));
                if (cast != null)
                    spells[kind] = cast;
                if (instant != null)
                    instants[kind] = instant;
                if (cast == null && instant == null)
                    blocked[kind.ToString()] = ofKind.Count == 0 ? "no such spell"
                        : ofKind.All(spell => Body.Mana < BotBody.PowerCost(spell)) ? "no power"
                        : Body.IsBeingInterrupted ? "interrupted" : "recast timer";
            }

            List<GameLiving> enemies = PvpEnemies.Where(enemy => enemy.IsAlive && enemy.ObjectState == GameObject.eObjectState.Active).ToList();
            GameLiving kill = PvpKillTarget is { IsAlive: true } called ? called : null;
            var nav = PathfindingProvider.Instance;
            var here = new Vector3(Body.X, Body.Y, Body.Z);
            int tooFar = 0, offLimitsCount = 0;
            var options = new List<PvpOption>();
            var sight = new Dictionary<GameLiving, bool>();
            // Whom each area mez would put to sleep, by option (kind and primary target).
            var caughtBy = new Dictionary<(PvpActionKind, int, bool), List<GameLiving>>();
            // Enemies first, then the mezzed members of this bot's own group (for cure mez); an option's target is an index here.
            List<GameLiving> friends = Body.Group?.GetMembersInTheGroup()
                .Where(member => member != Body && member.IsAlive && PvpNotice.SeesAsleep(Body, member)).ToList() ?? [];
            List<GameLiving> targets = enemies.Cast<GameLiving>().Concat(friends).ToList();
            foreach ((PvpActionKind kind, Spell spell, bool instant) in spells.Select(pair => (pair.Key, pair.Value, false))
                         .Concat(instants.Select(pair => (pair.Key, pair.Value, true))))
            {
                bool mez = kind is PvpActionKind.Mez or PvpActionKind.MezArea;
                // A spell centred on the caster reaches as far as its radius.
                bool aroundCaster = spell.Target == eSpellTarget.SELF || spell.Range <= 0;
                int range = aroundCaster ? spell.Radius : spell.CalculateEffectiveRange(Body);
                int cost = Body.MaxMana > 0 ? 100 * BotBody.PowerCost(spell) / Body.MaxMana : 0;
                // With the kill target beyond this spell, or none called, the caster works on what it reaches.
                bool killOutOfReach = kill == null || Body.GetDistanceTo(kill) > range;
                if (kind == PvpActionKind.CureMez)
                {
                    for (int friend = 0; friend < friends.Count; friend++)
                    {
                        GameLiving member = friends[friend];
                        int away = (int)Body.GetDistanceTo(member);
                        // (The navmesh, and the ground between the two: a hill hides. PvpSight.ClearForACast.)
                        bool visible = nav.HasLineOfSight(Body.CurrentZone, here, new Vector3(member.X, member.Y, member.Z), nav.DefaultFilters) &&
                                       PvpSight.ClearForACast(Body, member, weights);
                        int classId = member switch
                        {
                            GamePlayer { CharacterClass: { } playerClass } => playerClass.ID,
                            IGamePlayer { CharacterClass: { } botClass } => botClass.ID,
                            _ => 0,
                        };
                        options.Add(new PvpOption(kind, enemies.Count + friend, PvpBuffPlan.ArchetypeOf((eCharacterClass)classId), false, false,
                            member.HealthPercent, false, false, false, true, Body.ManaPercent, cost,
                            Math.Max(away > range ? away - range + 50 : 0, visible ? 0 : 300))
                        {
                            Instant = instant, OnController = PvpActionChoice.IsController((eCharacterClass)classId, weights),
                        });
                    }
                    continue;
                }
                // An area nuke is for enemy pets only, below: around an enemy it would wake whoever sleeps beside it.
                if (kind == PvpActionKind.AreaDamage)
                    continue;
                for (int id = 0; id < enemies.Count; id++)
                {
                    GameLiving enemy = enemies[id];
                    int distance = (int)Body.GetDistanceTo(enemy);
                    if (distance > weights.ConsiderRange)
                    {
                        tooFar++;
                        continue;
                    }
                    if (!sight.TryGetValue(enemy, out bool seen))
                        sight[enemy] = seen = nav.HasLineOfSight(Body.CurrentZone, here, new Vector3(enemy.X, enemy.Y, enemy.Z), nav.DefaultFilters) &&
                                              PvpSight.ClearForACast(Body, enemy, weights);
                    // Out of range or behind something: still an option, at the price of the walk.
                    int walk = Math.Max(distance > range ? distance - range + 50 : 0, seen ? 0 : 300);
                    // Where that walk would end, and how many enemy fighters that are free to act would be beside it there.
                    int walkDanger = 0, walkInsideEdge = 0;
                    if (walk > 0 && distance > 1)
                    {
                        float stop = Math.Max(0, distance - walk) / (float)distance;
                        var end = new Vector2(enemy.X + (Body.X - enemy.X) * stop, enemy.Y + (Body.Y - enemy.Y) * stop);
                        // The edge as the group's order has scaled it: wider when it falls back, tighter when it pushes.
                        double edge = weights.Position.EdgeDistance * (PvpSquad?.Order == PvpOrder.Extend ? weights.Order.ExtendSafeFactor
                            : PvpSquad?.Order == PvpOrder.Push ? weights.Order.PushSafeFactor : 1);
                        // Counted from the enemy's healers and casters only. A fighter beside where the walk
                        // ends is the walk's danger, below; counted here too, the enemy fighters on this
                        // group's own front made every walk forward one that "ended among the enemy".
                        float nearest = float.MaxValue;
                        foreach (GameLiving other in enemies)
                        {
                            if (other.IsAlive && !other.IsMezzed && PvpMember.ArchetypeOf(other) != PvpBuffArchetype.Melee)
                                nearest = Math.Min(nearest, Vector2.Distance(end, new Vector2(other.X, other.Y)));
                        }
                        walkInsideEdge = (int)Math.Max(0, edge - nearest);
                        walkDanger = enemies.Count(other => PvpMember.ArchetypeOf(other) == PvpBuffArchetype.Melee &&
                            !other.IsMezzed && !other.IsStunned && !other.effectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedDebuff) &&
                            Vector2.Distance(end, new Vector2(other.X, other.Y)) <= weights.WalkDangerWithin);
                    }
                    // Asleep, or claimed by a mezzer of this group for the length of its cast, and not
                    // the kill target: the server refuses a bot any attack on it (the casts that were
                    // accepted and never began), so it is no option. The exception is an area mez,
                    // which may be aimed at a sleeping enemy to catch the ones around it.
                    bool offLimits = BotPvpCrowdControl.Protected(Body, enemy);
                    if (offLimits && kind != PvpActionKind.MezArea)
                    {
                        offLimitsCount++;
                        continue;
                    }
                    bool hasEffect = kind switch
                    {
                        PvpActionKind.Mez => PvpMezzedOrImmune(enemy),
                        // The enemy an area mez is aimed at may itself be immune; the ones around it count.
                        PvpActionKind.MezArea => false,
                        PvpActionKind.Stun => PvpStunnedOrImmune(enemy),
                        PvpActionKind.Pet => false,
                        _ => LivingHasEffect(enemy, spell),
                    };
                    int caught = 1;
                    bool catchesKill = false;
                    if (kind == PvpActionKind.MezArea)
                    {
                        GameObject centre = aroundCaster ? Body : enemy;
                        List<GameLiving> inside = enemies.Where(other => other.IsWithinRadius(centre, spell.Radius)).ToList();
                        List<GameLiving> asleep = inside.Where(other => other != kill && !PvpMezzedOrImmune(other) && !BotPvpCrowdControl.Protected(Body, other)).ToList();
                        caughtBy[(kind, id, instant)] = asleep;
                        caught = asleep.Count;
                        catchesKill = kill != null && inside.Contains(kill) && !PvpMezzedOrImmune(kill);
                    }
                    int wakes = 0;
                    if (kind == PvpActionKind.Amnesia)
                    {
                        // Counted: the enemies in it that would lose a cast, and the sleepers it would wake.
                        List<GameLiving> inside = spell.Radius > 0 ? enemies.Where(other => other.IsWithinRadius(enemy, spell.Radius)).ToList() : [enemy];
                        caught = inside.Count(other => !other.IsMezzed && other.IsCasting && other.CurrentSpellHandler?.Spell.InstrumentRequirement == 0);
                        wakes = inside.Count(other => other.IsMezzed && other != kill);
                    }
                    // A pet that is cast (the Theurgist's) does not count a player's called assist as the kill target either.
                    options.Add(new PvpOption(kind, id, PvpMember.ArchetypeOf(enemy),
                        enemy == kill && !(kind == PvpActionKind.Pet && PvpSquad?.IsCalledAssist(enemy) == true),
                        enemy == PvpAssignedTarget || enemy == PvpDutyTarget, enemy.HealthPercent, hasEffect,
                        PvpNotice.SeesCasting(Body, enemy), enemy.IsBeingInterrupted,
                        PvpActionChoice.SleepCounts(enemy.IsMezzed, mez, !PvpEnemies.Any(other => other.IsAlive && !other.IsMezzed)), Body.ManaPercent, cost, walk, caught, catchesKill, wakes,
                        instant, PvpActionChoice.IsController(PvpMember.ClassOf(enemy), weights),
                        PvpSquad?.IsPursuer(enemy) == true, PvpSquad?.Order == PvpOrder.Extend, walkDanger,
                        PvpSquad?.IsOnTrained(enemy) == true, walkInsideEdge, killOutOfReach,
                        enemy.effectListComponent.ContainsEffectForEffectType(eEffect.MovementSpeedDebuff),
                        Called: PvpSquad?.IsCalledControl(enemy) == true));
                }
            }
            // Enemy pets in melee on this group's healers and casters, after the enemies and the friends: a target for
            // damage (not the Theurgist's swarm: those are for a point-blank spell) and for a mez. Pets have no Purge.
            List<GameNPC> pets = PvpPetsOnBackline();
            if (pets.Count > 0)
            {
                foreach ((PvpActionKind kind, Spell spell, bool instant) in spells.Select(pair => (pair.Key, pair.Value, false))
                             .Concat(instants.Select(pair => (pair.Key, pair.Value, true)))
                             .Where(entry => entry.Item1 is PvpActionKind.Damage or PvpActionKind.Mez or PvpActionKind.MezArea or PvpActionKind.AreaDamage))
                {
                    if (spell.Target == eSpellTarget.SELF || spell.Range <= 0)
                        continue;
                    int range = spell.CalculateEffectiveRange(Body);
                    int cost = Body.MaxMana > 0 ? 100 * BotBody.PowerCost(spell) / Body.MaxMana : 0;
                    for (int index = 0; index < pets.Count; index++)
                    {
                        GameNPC pet = pets[index];
                        bool swarm = pet is TheurgistPet;
                        // Not the pet that is on this bot itself while it is being hit: it could not finish the cast.
                        if (!instant && pet.TargetObject == Body && Body.IsBeingInterrupted)
                            continue;
                        bool area = kind is PvpActionKind.MezArea or PvpActionKind.AreaDamage;
                        if (!area && swarm || kind is PvpActionKind.Mez or PvpActionKind.MezArea && PvpPetMezzedOrImmune(pet))
                            continue;
                        int caught = 1;
                        bool catchesKill = false;
                        if (kind == PvpActionKind.AreaDamage)
                        {
                            // Every pet inside it, unless it would wake an enemy or a pet that sleeps there.
                            if (enemies.Any(enemy => enemy.IsMezzed && enemy.IsWithinRadius(pet, spell.Radius)) ||
                                pets.Any(other => other.IsMezzed && other.IsWithinRadius(pet, spell.Radius)))
                                continue;
                            caught = pets.Count(other => other.IsWithinRadius(pet, spell.Radius));
                            if (caught < 2)
                                continue;
                        }
                        if (kind == PvpActionKind.MezArea)
                        {
                            int players = enemies.Count(enemy => enemy != kill && enemy.IsWithinRadius(pet, spell.Radius) && !PvpMezzedOrImmune(enemy));
                            caught = PvpPetClear.MezCount(players, pets.Count(other => other.IsWithinRadius(pet, spell.Radius) && !PvpPetMezzedOrImmune(other)));
                            catchesKill = kill != null && kill.IsWithinRadius(pet, spell.Radius) && !PvpMezzedOrImmune(kill);
                            if (caught == 0)
                                continue;
                        }
                        int distance = (int)Body.GetDistanceTo(pet);
                        bool seen = nav.HasLineOfSight(Body.CurrentZone, here, new Vector3(pet.X, pet.Y, pet.Z), nav.DefaultFilters) &&
                                    PvpSight.ClearForACast(Body, pet, weights);
                        options.Add(new PvpOption(kind, targets.Count + index, PvpBuffArchetype.Melee, false, false,
                            pet.HealthPercent, false, false, false, kind == PvpActionKind.Damage && pet.IsMezzed, Body.ManaPercent, cost,
                            Math.Max(distance > range ? distance - range + 50 : 0, seen ? 0 : 300))
                        {
                            Instant = instant, PetOnBackline = true, Caught = caught, CatchesKill = catchesKill,
                        });
                    }
                }
                targets.AddRange(pets);
            }
            if (tooFar > 0)
                blocked["too far"] = tooFar.ToString();
            if (offLimitsCount > 0)
                blocked["asleep or claimed"] = offLimitsCount.ToString();

            List<(PvpOption Option, int Score)> ranked = PvpActionChoice.Rank(characterClass, options, weights, BotBody.PvpBuild?.Id);
            // Mistakes (PvpHumanWeights.ChoiceNoise): each score is a little off, so the best by the numbers is not always taken.
            PvpHumanWeights human = PvpNotice.HumanOf(BotBody);
            if (human.ChoiceNoise > 0 && ranked.Count > 1)
                ranked = ranked.Select(scored => (scored.Option, Score: scored.Score + PvpNotice.Noise(human, Random.Shared)))
                    .OrderByDescending(scored => scored.Score).ToList();
            object Describe((PvpOption Option, int Score) scored) => new
            {
                kind = scored.Option.Kind.ToString(), d = targets[scored.Option.TargetId].Name, score = scored.Score, walk = scored.Option.Short,
                caught = scored.Option.Kind == PvpActionKind.MezArea ? scored.Option.Caught : (int?)null,
                // For a root: the target is on this group's healers and casters; it is rooted, slowed or immune already.
                pursuer = scored.Option.Pursuer ? true : (bool?)null,
                danger = scored.Option.WalkDanger > 0 ? scored.Option.WalkDanger : (int?)null,
                has = scored.Option.Kind == PvpActionKind.Root && scored.Option.HasEffect ? true : (bool?)null,
            };

            foreach ((PvpOption Option, int Score) scored in ranked.Where(scored => PvpActionChoice.WorthIt(scored.Option, scored.Score, weights)))
            {
                Spell spell = (scored.Option.Instant ? instants : spells)[scored.Option.Kind];
                GameLiving aim = targets[scored.Option.TargetId];
                // No walking in the middle of a cast: it would end the cast. And none while the group holds.
                // A fighter does not walk for a shout either: it goes where its melee target is.
                // (It still walks to wake a groupmate.)
                bool fighterShout = PvpBuffPlan.ArchetypeOf(characterClass) == PvpBuffArchetype.Melee && scored.Option.Kind != PvpActionKind.CureMez;
                if ((midCast || PvpHolding() || fighterShout) && scored.Option.Short > 0)
                    continue;
                // Before the first blow a healer or caster does not walk in to cast: it stands at range and
                // the enemy comes to it (PvpPositionWeights.BackRowStopsAt). It still walks to wake a groupmate.
                if (scored.Option.Short > 0 && scored.Option.Kind != PvpActionKind.CureMez && PvpSquad is { FightStarted: false } &&
                    weights.Position.BackRowStopsAt > 0 && PvpBuffPlan.ArchetypeOf(characterClass) != PvpBuffArchetype.Melee)
                    continue;
                if (enemies.Contains(aim))
                    (PvpWorkTarget, PvpWorkRange, PvpWorkTick) = (aim,
                        spell.Target == eSpellTarget.SELF || spell.Range <= 0 ? spell.Radius : spell.CalculateEffectiveRange(Body), GameLoop.GameLoopTime);
                if (scored.Option.Short > 0)
                {
                    // The best thing to do is out of reach: walk toward it. The order is renewed
                    // once a second; the choice is made again on every think tick on the way.
                    if (GameLoop.GameLoopTime - _pvpApproachTick >= 1_000 || !Body.IsMoving)
                    {
                        _pvpApproachTick = GameLoop.GameLoopTime;
                        PvpWalkIntoRange(aim, spell);
                        PvpRecorder.Decision(BotBody, new { kind = "Approach", spell = spell.Name, d = aim.Name, score = scored.Score },
                            ranked.Take(5).Select(Describe), blocked.Count > 0 ? blocked : null);
                    }
                    return true;
                }
                bool mez = scored.Option.Kind is PvpActionKind.Mez or PvpActionKind.MezArea;
                // One mezzer per enemy: claim it for the length of the cast, or leave it to whoever has.
                bool overSleeper = scored.Option.Kind == PvpActionKind.MezArea && BotPvpCrowdControl.Protected(Body, aim);
                if (mez && !overSleeper && !BotPvpCrowdControl.Reserve(BotBody, aim, spell.CastTime + 2_000))
                    continue;
                if (overSleeper)
                {
                    // Lets this one cast through the server's rule against attacking a sleeping enemy.
                    _pvpAreaMezAim = aim;
                    _pvpControlCastUntil = GameLoop.GameLoopTime + spell.CastTime + 2_000;
                }
                GameObject previous = Body.TargetObject;
                Body.TargetObject = aim;
                if (spell.CastTime > 0)
                {
                    Body.StopMovingOnPath();
                    Body.StopMoving();
                    Body.StopAttack();
                }
                if (Body.CastSpell(spell, m_mobSpellLine))
                {
                    if (midCast)
                    {
                        // The running cast keeps its own target and its own record.
                        Body.TargetObject = previous;
                    }
                    else
                    {
                        (_pvpLastCast, _pvpLastCastTarget, _pvpLastCastTick) = (spell, aim, GameLoop.GameLoopTime);
                        // A healer's attack spell is otherwise stopped by its support routine.
                        _pvpControlSpellId = spell.ID;
                        _pvpControlCastUntil = GameLoop.GameLoopTime + spell.CastTime + 2_000;
                    }
                    if (scored.Option.Kind == PvpActionKind.MezArea)
                        PvpObserver.Say(Body, $"{PvpObserver.Name(BotBody)} casts an area mez at {aim.Name}: {scored.Option.Caught} awake in it");
                    if (caughtBy.TryGetValue((scored.Option.Kind, scored.Option.TargetId, scored.Option.Instant), out List<GameLiving> asleep))
                    {
                        foreach (GameLiving other in asleep)
                            BotPvpCrowdControl.Reserve(BotBody, other, spell.CastTime + 2_000);
                    }
                    PvpRecorder.Decision(BotBody, new
                        {
                            kind = scored.Option.Kind.ToString(), spell = spell.Name, d = aim.Name, score = scored.Score,
                            caught = scored.Option.Kind == PvpActionKind.MezArea ? scored.Option.Caught : (int?)null,
                            instant = scored.Option.Instant ? true : (bool?)null, midCast = midCast ? true : (bool?)null,
                        },
                        ranked.Take(5).Select(Describe), blocked.Count > 0 ? blocked : null);
                    return true;
                }
                if (mez && !overSleeper)
                    BotPvpCrowdControl.Release(BotBody, aim);
                if (overSleeper)
                    _pvpAreaMezAim = null;
                Body.TargetObject = previous;
                blocked["cast refused"] = spell.Name;
            }

            // Nothing cast. Say why, but not on every think tick, and not for a look taken in the middle of a cast.
            // A class with only its cure mez scored has nothing to say while nobody is asleep.
            if (!midCast && GameLoop.GameLoopTime - _pvpNoActionRecordedTick >= 2_000 &&
                (ranked.Count > 0 || PvpActionChoice.ScoresOffense(characterClass, weights)))
            {
                _pvpNoActionRecordedTick = GameLoop.GameLoopTime;
                if (ranked.Count > 0 && !ranked.Any(scored => PvpActionChoice.WorthIt(scored.Option, scored.Score, weights)))
                    blocked["best score too low"] = ranked[0].Score.ToString();
                PvpRecorder.Decision(BotBody, null, ranked.Take(5).Select(Describe), blocked);
            }
            return !midCast && PvpStaffThePet(weights);
        }

        private static bool PvpPetMezzedOrImmune(GameNPC pet) => pet.IsMezzed ||
            pet.effectListComponent.ContainsEffectForEffectType(eEffect.MezImmunity) ||
            pet.effectListComponent.ContainsEffectForEffectType(eEffect.NPCMezImmunity);

        /// <summary>
        /// Enemy pets in melee on a healer or caster of this bot's group, the Theurgist's swarm
        /// included: single spells leave those alone (there are many and they do not last), an
        /// area mez or a point-blank spell (bots/pvp/PvpPetClear.cs) takes them together.
        /// </summary>
        private List<GameNPC> PvpPetsOnBackline() => PvpEnemies == null ? [] : Body.GetNPCsInRadius(2_500)
            .Where(npc => npc.IsAlive && npc.Brain is IControlledBrain owned &&
                          owned.GetLivingOwner() is { } owner && PvpEnemies.Contains(owner) &&
                          npc.TargetObject is GameLiving { IsAlive: true } victim && victim.Realm == Body.Realm &&
                          PvpMember.ClassOf(victim) != eCharacterClass.Unknown && !PvpMember.IsFighter(victim) && npc.IsWithinRadius(victim, 350))
            .ToList();

        private GameNPC _pvpStaffedPet;

        /// <summary>Until when this bot is at an enemy pet with its staff: its group's positioning leaves it where it is (PvpSquad.StandOff).</summary>
        public long PvpStaffsUntil { get; private set; }

        /// <summary>
        /// Whether the blow a bot is about to strike is struck before its brain is asked for a spell: it is at an enemy
        /// pet with its staff, the pet is alive and still on it, and it is not casting. Every swing of a bot first asks
        /// its brain for a spell and is called off when the brain says its turn is taken
        /// (NpcAttackAction.PrepareMeleeAttack), and a bot that staffs a pet says exactly that
        /// (<see cref="PvpStaffThePet"/>): its swing was called off, each time, and begun again at its next thought. The
        /// owner, 2026-11-27, on its Cleric: "I am not sure the rule to attack pet when caster are unable to cast
        /// worked. One of our caster had a druid grey pet and I just ran to it and auto-attack it". In its recording
        /// that Theurgist had the lynx as its target for 16 seconds, stood still and struck no blow. In 33 minutes of
        /// that evening, all groups: of 231 decisions to hit a grey pet 49 were followed by a blow (taken before the
        /// pet's first hit had landed: the bot was not kept from casting yet, so its brain did not say it staffed), and
        /// of the 261 to hit a pet that is not grey, none.
        /// </summary>
        public static bool StrikesBeforeItChoosesASpell(bool atAPetWithItsStaff, bool petIsAliveAndOnIt, bool casting) =>
            atAPetWithItsStaff && petIsAliveAndOnIt && !casting;

        /// <summary>True while the next blow of this bot at the pet it staffs goes ahead (<see cref="StrikesBeforeItChoosesASpell"/>).</summary>
        private bool PvpStrikesThePet() => BotBody?.IsPvpBot == true && _pvpStaffedPet is { } pet &&
            StrikesBeforeItChoosesASpell(Body.TargetObject == pet && Body.attackComponent.AttackState, pet.IsAlive && pet.TargetObject == Body,
                Body.IsCasting || Body.castingComponent?.HasPendingSkillRequests == true);

        /// <summary>
        /// Whether a healer or caster turns on an enemy pet that is on it, before anything else:
        /// the pet is grey to it (it falls to a blow or two), it is in reach, and no enemy
        /// fighter is on the bot (then the pet is the least of its troubles). The owner, on its
        /// Cleric, 2026-11-26: "Caster are allowed to staff druid grey pet. I one shot them with
        /// my auto-attack, instead of running around doing nothing". A bot hit a pet only when it
        /// had found nothing to cast, was being kept from casting at that moment and stood still
        /// (<see cref="PvpStaffThePet"/>), and one that heals never came to that rule at all: a
        /// Druid's lesser nature spirit kept a caster from casting for as long as it lived.
        /// </summary>
        public static bool StaffsAGreyPet(bool isFighter, bool petIsGrey, bool petInReach, bool anEnemyFighterIsOnIt) =>
            !isFighter && petIsGrey && petInReach && !anEnemyFighterIsOnIt;

        /// <summary>
        /// Grey by its own level, not by the level it is taken to have: a pet counts as its owner's level where the game
        /// asks how dangerous it is (GameNPC.EffectiveLevel), so to that question a Druid's spirit of level 12 is no more
        /// grey to a bot of level 50 than its Druid is. In the first evening of bots with the rule no pet was hit by it.
        /// </summary>
        private bool PvpPetIsGrey(GameNPC pet) => (ConColor)GameObject.GetConLevel(Body.Level, pet.Level) <= ConColor.GREY;

        /// <summary>The grey pet of an enemy that is on this bot is hit until it is down (<see cref="StaffsAGreyPet"/>). True when the turn is taken.</summary>
        private bool TryPvpStaffAGreyPet()
        {
            if (BotBody?.IsPvpBot != true || PvpEnemies == null || BotBody.PvpBuild == null || !Body.IsAlive)
                return false;
            // (The one it was at is down, or has turned to somebody else: back to its work.)
            if (_pvpStaffedPet != null && (!_pvpStaffedPet.IsAlive || _pvpStaffedPet.TargetObject != Body))
            {
                if (Body.TargetObject == _pvpStaffedPet && Body.attackComponent.AttackState)
                    Body.StopAttack();
                _pvpStaffedPet = null;
                PvpStaffsUntil = 0;
            }
            if (!PvpWeights.Of(BotBody).StaffThePetOnMe || Body.IsIncapacitated || Body.IsCasting || Body.castingComponent?.HasPendingSkillRequests == true)
                return false;
            bool fighter = PvpMember.ArchetypeOf(BotBody) == PvpBuffArchetype.Melee;
            if (fighter)
                return false;
            GameNPC pet = Body.GetNPCsInRadius((ushort)(Body.MeleeAttackRange + 100))
                .Where(npc => npc.IsAlive && !npc.IsMezzed && npc.TargetObject == Body && npc.Brain is IControlledBrain owned &&
                              owned.GetLivingOwner() is { } owner && PvpEnemies.Contains(owner) && PvpPetIsGrey(npc))
                .OrderBy(npc => npc.HealthPercent).FirstOrDefault();
            bool fighterOnIt = PvpEnemies.Any(enemy => enemy.IsAlive && !enemy.IsMezzed && enemy.TargetObject == Body &&
                PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee && enemy.IsWithinRadius(Body, 400));
            if (!StaffsAGreyPet(fighter, pet != null, pet != null, fighterOnIt))
                return false;
            if (Body.IsMoving)
            {
                Body.StopFollowing();
                Body.StopMovingOnPath();
                Body.StopMoving();
            }
            if (!Body.attackComponent.AttackState || Body.TargetObject != pet)
            {
                Body.TargetObject = pet;
                SwitchToUsableMeleeWeapon();
                Body.StartAttack(pet);
            }
            PvpStaffsUntil = GameLoop.GameLoopTime + 2_500;
            if (_pvpStaffedPet != pet)
            {
                _pvpStaffedPet = pet;
                PvpRecorder.Decision(BotBody, new { kind = "Staff", d = pet.Name, score = 0, grey = true }, [], null);
            }
            return true;
        }

        /// <summary>
        /// A healer or caster with nothing it can cast, because an enemy pet is hitting it, hits
        /// the pet with its staff (<see cref="PvpWeights.StaffThePetOnMe"/>). True while it does.
        /// </summary>
        private bool PvpStaffThePet(PvpWeights weights)
        {
            if (!weights.StaffThePetOnMe || BotBody.PvpBuild == null || PvpMember.ArchetypeOf(BotBody) == PvpBuffArchetype.Melee ||
                !Body.IsBeingInterrupted || Body.IsMoving)
                return false;
            // Not with an enemy fighter on it: then the pet is the least of its troubles. (A Druid with an
            // Armsman and a Paladin on it was seen fencing with an air spirit, 2026-11-19.)
            if (PvpEnemies.Any(enemy => enemy.IsAlive && !enemy.IsMezzed && enemy.TargetObject == Body &&
                    PvpMember.ArchetypeOf(enemy) == PvpBuffArchetype.Melee && enemy.IsWithinRadius(Body, 400)))
                return false;
            GameNPC pet = Body.GetNPCsInRadius((ushort)(Body.MeleeAttackRange + 100))
                .Where(npc => npc.IsAlive && !npc.IsMezzed && npc.TargetObject == Body && npc.Brain is IControlledBrain owned &&
                              owned.GetLivingOwner() is { } owner && PvpEnemies.Contains(owner))
                .OrderBy(npc => npc.HealthPercent).FirstOrDefault();
            if (pet == null)
                return false;
            if (!Body.attackComponent.AttackState || Body.TargetObject != pet)
            {
                Body.TargetObject = pet;
                SwitchToUsableMeleeWeapon();
                Body.StartAttack(pet);
            }
            if (_pvpStaffedPet != pet)
            {
                _pvpStaffedPet = pet;
                PvpRecorder.Decision(BotBody, new { kind = "Staff", d = pet.Name, score = 0 }, [], null);
            }
            return true;
        }
    }
}

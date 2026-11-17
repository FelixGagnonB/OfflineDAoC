using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.GS;

namespace DOL.AI.Brain
{
    /// <summary>Casts an 8v8 caster's rotation (<see cref="PvpRotation"/>) when its group runs the squad brain.</summary>
    public partial class BotBrain
    {
        /// <summary>The enemy group as the squad brain sees it; null without a squad brain.</summary>
        public IReadOnlyList<GameBot> PvpEnemies { get; set; }
        /// <summary>The group's called kill target, for every member (an interrupter's own aim is PvpAssignedTarget).</summary>
        public GameLiving PvpKillTarget { get; set; }

        private bool TryPvpRotation()
        {
            if (PvpEnemies == null || BotBody?.IsPvpBot != true || Body.IsCasting || Body.IsIncapacitated ||
                Body.castingComponent?.HasPendingSkillRequests == true)
                return false;
            IReadOnlyList<PvpRotationStep> steps = PvpRotation.For((eCharacterClass)BotBody.CharacterClass.ID);
            if (steps.Count == 0)
                return false;

            List<GameBot> awake = PvpEnemies.Where(enemy => enemy.IsAlive && !enemy.IsMezzed &&
                enemy.ObjectState == GameObject.eObjectState.Active).OrderBy(Body.GetDistanceTo).ToList();
            GameLiving kill = PvpKillTarget is { IsAlive: true } called ? called : null;
            IEnumerable<Spell> known = (Body.HarmfulSpells ?? []).Concat(BotBody.CrowdControlSpells ?? []);

            foreach (PvpRotationStep step in steps)
            {
                Spell spell = known.Where(candidate => step.Types.Contains(candidate.SpellType) && candidate.Level <= Body.Level &&
                        Body.GetSkillDisabledDuration(candidate) == 0 && Body.Mana >= BotBody.PowerCost(candidate) &&
                        (candidate.CastTime == 0 || candidate.Uninterruptible || !Body.IsBeingInterrupted))
                    .OrderByDescending(candidate => candidate.Level).FirstOrDefault();
                if (spell == null)
                    continue;
                int range = spell.CalculateEffectiveRange(Body);
                IEnumerable<GameLiving> aims = step.Aim switch
                {
                    PvpAim.Kill => kill == null ? [] : [kill],
                    PvpAim.Assigned => PvpAssignedTarget is { IsAlive: true } mine ? [mine] : [],
                    PvpAim.FreeCaster => awake.Where(enemy => PvpBuffPlan.ArchetypeOf(enemy.PvpBuild.Class) != PvpBuffArchetype.Melee && enemy != kill),
                    _ => (kill == null ? [] : new[] { kill }).Concat(awake.Where(enemy => enemy != kill)),
                };
                foreach (GameLiving aim in aims)
                {
                    if (!Body.IsWithinRadius(aim, range) || !step.Spam && LivingHasEffect(aim, spell) ||
                        !PathfindingProvider.Instance.HasLineOfSight(Body.CurrentZone, new Vector3(Body.X, Body.Y, Body.Z),
                            new Vector3(aim.X, aim.Y, aim.Z), PathfindingProvider.Instance.DefaultFilters))
                        continue;
                    GameObject previous = Body.TargetObject;
                    Body.TargetObject = aim;
                    if (spell.CastTime > 0)
                    {
                        Body.StopMovingOnPath();
                        Body.StopMoving();
                        Body.StopAttack();
                    }
                    if (Body.CastSpell(spell, m_mobSpellLine))
                        return true;
                    Body.TargetObject = previous;
                }
            }
            return false;
        }
    }
}

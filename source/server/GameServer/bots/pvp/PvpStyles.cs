using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DOL.GS
{
    /// <summary>
    /// How each kind of team plays (docs/PVP-8V8-ROADMAP.md, M12.6). Until 2026-11-24 a kind
    /// differed in whom it walked with, whether it added, its rank, its eight and how fast it
    /// answered; in whom it called, where it stood, how it healed, controlled, peeled and
    /// interrupted, a bad group played as an elite one did. The owner, that day, shown what each
    /// kind had: "all kinds should behave differently regarding Targeting, positioning, healing,
    /// crowd control, peeling and interrupting instead of sharing the same value", and of the
    /// habits proposed for each kind: "These suggestions looks fine to me. We can start with it."
    ///
    /// A style is a copy of the weights in force with that kind's habits written over it. The
    /// decent team has none: it plays by the weights as they are, which is how every team of bots
    /// played before. Every number here is a first guess, to be looked at in a fight.
    /// </summary>
    public static class PvpStyles
    {
        private static PvpWeights _from;
        private static readonly Dictionary<PvpTeamKind, PvpWeights> Made = new();

        /// <summary>The weights a team of this kind fights with; null for the kind that plays by the weights as they are.</summary>
        public static PvpWeights For(PvpTeamKind kind, PvpWeights from)
        {
            if (kind == PvpTeamKind.Decent || from == null)
                return null;
            lock (Made)
            {
                if (!ReferenceEquals(_from, from))
                {
                    Made.Clear();
                    _from = from;
                }
                if (!Made.TryGetValue(kind, out PvpWeights style))
                    Made[kind] = style = Make(kind, from);
                return style;
            }
        }

        /// <summary>Scales what a class leans to for some kinds of action, and adds to others.</summary>
        private static void Lean(PvpWeights weights, double mez, double cureMez, int damage)
        {
            foreach (Dictionary<string, int> leans in weights.ClassBase.Values)
            {
                foreach (string kind in leans.Keys.ToList())
                {
                    if (kind is "Mez" or "MezArea")
                        leans[kind] = (int)Math.Round(leans[kind] * mez);
                    else if (kind == "CureMez")
                        leans[kind] = (int)Math.Round(leans[kind] * cureMez);
                    else if (kind is "Damage" or "AreaDamage")
                        leans[kind] += damage;
                }
            }
        }

        private static PvpWeights Make(PvpTeamKind kind, PvpWeights from)
        {
            PvpWeights w = JsonSerializer.Deserialize<PvpWeights>(JsonSerializer.Serialize(from));
            switch (kind)
            {
                case PvpTeamKind.Bad:
                    // Targeting: no assist. Each fighter takes the nearest enemy, a caster hits whom it likes, and a sleeping enemy is woken.
                    w.Kill.Assists = false;
                    (w.Kill.Mezzed, w.TargetMezzed, w.MezCatchesKillTarget, w.DamageOffKillTarget) = (-20, -20, 0, 0);
                    // Positioning: healers and casters in the melee and in one clump; no order but "engage", so nobody is called back.
                    (w.Position.SafeDistance, w.Position.EdgeDistance, w.Position.KitesWhenPinned) = (250, 0, false);
                    (w.Spread.MinSpacing, w.Spread.HealerSpacing) = (0, 0);
                    w.Order.Enabled = false;
                    // Healing: late, two healers on one patient, and the dead raised in the middle of the fight.
                    (w.Heal.HealBelowPercent, w.Heal.SavingHealBelowPercent, w.Heal.SpreadHealBelowPercent) = (60, 40, 60);
                    (w.Heal.KeepOwnChoice, w.Heal.ResurrectIfNobodyBelow, w.Heal.ResurrectNoFighterWithin) = (100, 20, 0);
                    // Control: the mezzer mostly does damage; nobody roots a fighter that is on its healers.
                    Lean(w, 0.5, 0.6, 40);
                    (w.RootOnPursuer, w.RootOnPursuerWhileExtending) = (0, 0);
                    // Peel and interrupt: none. A shield fighter is one more fighter.
                    w.Peel.Enabled = false;
                    (w.Interrupt.MinScore, w.InterruptsACast, w.InstantOnCastingTarget) = (10_000, 0, 0);
                    break;

                case PvpTeamKind.Clingy:
                    // Targeting: the nearest enemy, whatever its class; slow to switch.
                    (w.Kill.OnHealer, w.Kill.OnCaster, w.Kill.OnFighter) = (100, 100, 100);
                    (w.Kill.PerHundredUnits, w.Kill.PerHundredUnitsFromItsHealers, w.Kill.SwitchMargin) = (-10, 0, 120);
                    // Positioning: the back line in one clump, out of the melee but nearer than it should be; the fighters chase far.
                    (w.Spread.MinSpacing, w.Spread.HealerSpacing, w.Position.EdgeDistance) = (0, 0, 900);
                    (w.Order.RegroupFighterIsFarAt, w.Order.ExtendFightersStayWithin) = (4_000, 2_500);
                    // Healing: a little late, and the dead raised in the fight.
                    (w.Heal.HealBelowPercent, w.Heal.SavingHealBelowPercent, w.Heal.SpreadHealBelowPercent) = (70, 50, 70);
                    (w.Heal.ResurrectIfNobodyBelow, w.Heal.ResurrectNoFighterWithin) = (30, 300);
                    // Control: it mezzes at the start and then often breaks it.
                    (w.Kill.Mezzed, w.TargetMezzed, w.MezCatchesKillTarget) = (-40, -40, 0);
                    // Peel and interrupt: it guards, and stays on its target; whoever interrupts takes the nearest.
                    (w.Peel.KillTarget, w.Peel.OnBackline, w.Peel.Coming, w.Peel.InterceptWithin) = (120, 20, 0, 0);
                    (w.Interrupt.PerHundredUnits, w.Interrupt.OnHealer) = (-6, 20);
                    // Group: it breaks off in six fights of ten that it loses.
                    w.Kite.BreaksOffChancePercent = 60;
                    break;

                case PvpTeamKind.Adder:
                    // Targeting: the most hurt enemy (it is there for the realm points), and quick to change for one.
                    (w.Kill.PerMissingHealthPercent, w.Kill.SwitchMargin) = (4, 30);
                    // Positioning: as a decent team, but its fighters go too far for a kill.
                    w.Order.RegroupFighterIsFarAt = 3_200;
                    // Peel and interrupt: it peels only once a healer is being hit, and interrupts healers only.
                    (w.Peel.Coming, w.Peel.InterceptWithin, w.Peel.OnBackline) = (0, 0, 30);
                    (w.Interrupt.OnCaster, w.Interrupt.OnController) = (-50, -50);
                    // Group: it breaks off in half the fights it loses.
                    w.Kite.BreaksOffChancePercent = 50;
                    break;

                case PvpTeamKind.Strong:
                case PvpTeamKind.Elite:
                case PvpTeamKind.Hunter:
                    // Targeting: everybody is on a new call for a few seconds.
                    w.Kill.CollapseOnCallSeconds = 4;
                    // Positioning: it regroups sooner, and its fighters seldom leave their healers' reach.
                    (w.Order.RegroupFighterIsFarAt, w.Order.RegroupWithin) = (2_000, 1_500);
                    // Healing: the member the enemy is on is healed earlier.
                    (w.Heal.PatientIsTrained, w.Heal.SavingTrainedHealBelowPercent) = (40, 92);
                    // Control: the enemy's mezzer first.
                    (w.ControlOnEnemyController, w.NearsightOnEnemyController, w.Interrupt.OnController) = (70, 45, 30);
                    // Peel: a fighter on its way to the healers is met before it arrives.
                    (w.Peel.InterceptWithin, w.Peel.Coming) = (1_600, 80);
                    if (kind == PvpTeamKind.Strong)
                        break;
                    // Elite: whoever stands out of position is called sooner; nobody is raised with somebody hurt.
                    w.Kill.PerHundredUnitsFromItsHealers = 8;
                    w.Heal.ResurrectIfNobodyBelow = 70;
                    if (kind == PvpTeamKind.Elite)
                        break;
                    // The hunters: whoever is near, not the healer at the back of a pack; they do not go in deep.
                    (w.Kill.OnHealer, w.Kill.PerHundredUnits, w.Kill.MaxDistance) = (120, -9, 2_000);
                    break;
            }
            return w;
        }
    }
}

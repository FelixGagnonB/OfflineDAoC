# DAoC 8v8 AI --- Class Playbook (Review Draft)

**Status:** Draft for review; not an implementation specification\
**Intended ruleset:** OfflineDAoC / DAoC 1.65-era baseline, subject to
verification against the server's actual spell, style, ability, and
realm-ability data\
**Purpose:** Define group roles and context-sensitive combat priorities
for the three 8-player templates supplied by the project owner.

------------------------------------------------------------------------

## 1. How to use this playbook

This document describes the *intended behaviour* of each class in the
supplied group templates. It is not a claim that every listed action,
spell, style effect, or specialization breakpoint has already been
verified in the OfflineDAoC codebase.

Before implementation, Claude Code should: 1. Inspect the actual server
data for each class and specialization. 2. Verify spell levels, spell
types, range, cast time, recast time, duration, immunity interactions,
power cost, and target restrictions. 3. Verify style opening conditions
and effects (anytime, positional, reactionary, follow-up, snare, stun,
bleed, etc.). 4. Verify which abilities are available in the target
ruleset and what the server actually implements. 5. Replace generic
action labels in this playbook with concrete in-game ability
identifiers. 6. Keep uncertain mechanics configurable rather than
assuming a modern Live or freeshard implementation is identical to the
target server.

The templates below are reproduced from the supplied class/specification
image. The role descriptions are retained as the starting intent, with
additional behavioural guidance proposed for review.

## 2. Core AI philosophy: utility and priorities, not rotations

DAoC combat is reactive. A fixed rotation is inappropriate. The AI
should evaluate the current situation, score available actions, execute
the best valid action, then reassess after relevant events.

A class should not ask, "What is my next spell in the rotation?" It
should ask: - What is happening right now? - What is the most important
problem I can solve? - Which actions are legal and useful from my
current position? - What will this action prevent, enable, or risk? -
Does this action conflict with a group command or another teammate's
assignment?

### 2.1 Suggested decision loop

Run a lightweight decision evaluation at a configurable interval and
immediately after important events (new enemy detected, cast started,
target changed, ally crowd-controlled, ally health threshold crossed,
command received, line of sight lost, etc.).

For each candidate action:

1.  **Validate:** Is the ability ready, in range, in line of sight,
    affordable, and legal for this target?
2.  **Check hard constraints:** Is the target immune? Is the action
    blocked by an existing effect? Would it break valuable crowd
    control? Is the character moving or interrupted?
3.  **Calculate urgency and value:** How much does this action improve
    the group's current situation?
4.  **Apply coordination rules:** Is another group member already
    assigned to do this? Is this the designated assist target? Is there
    a higher-priority leader command?
5.  **Choose:** Execute the highest-scoring valid action, or do nothing
    if no action is sufficiently valuable.
6.  **Reassess:** Update the state after the action and after its result
    is known.

### 2.2 Utility score (conceptual)

Use a transparent scoring model rather than hard-coded action order:

`score(action) = urgency + tactical_value + role_value + opportunity - resource_cost - exposure_risk - coordination_penalty - conflict_penalty`

Possible factors: - **Urgency:** Will an ally die or an enemy complete a
dangerous cast if we wait? - **Tactical value:** Does the action create
a kill opportunity, stop enemy pressure, or preserve group control? -
**Role value:** Is this character specifically responsible for the
task? - **Opportunity:** Is the target exposed, out of position, already
controlled, or within a useful positional-style window? - **Resource
cost:** Power, endurance, health, cooldown, or limited-use ability. -
**Exposure risk:** Does acting require moving into dangerous range or
breaking formation? - **Coordination penalty:** Is another teammate
already handling this target or task? - **Conflict penalty:** Would the
action break mez, overwrite useful CC, abandon a critical role, or
contradict a current command?

Weights should be configurable by class, role, matchup, and tactical
state. Record the component scores and reason for the selected action so
that decisions can be debugged.

### 2.3 Hard rules versus soft preferences

Some rules should be absolute: - Do not cast a spell if the target is
invalid, out of range, out of line of sight, or the spell is
unavailable. - Do not intentionally break valuable mez without an
explicit high-priority reason. - Do not chase so far that the character
abandons a critical group responsibility. - Do not assume an enemy's
position or target without legitimate information. - Do not waste a
single-target interrupt on an enemy who is not casting unless there is a
separately justified interrupt action. - Do not use a long cast when
movement or an immediate defensive action is required.

Other decisions should be weighted, not absolute: - Heal now versus
continue interrupting. - Cure nearsight versus heal. - Use single-target
mez versus AE mez. - Root a pursuer versus interrupt a caster. -
Continue assisting versus peel. - Use a positional style versus an
anytime style. - Spend a major defensive ability now versus preserve it.

### 2.4 Shared combat state

The group AI should maintain a shared, timestamped tactical picture.
Each bot must still distinguish between: - **Observed facts:** what this
character personally sees or receives through legitimate group
communication. - **Shared facts:** observations explicitly communicated
to the group. - **Inferences:** likely but uncertain conclusions. -
**Unknowns:** information the group does not possess.

Suggested state: - Current group order: engage, push, extend/kite, peel,
regroup, hold, disengage. - Current assist target and target
confidence. - Enemy roster: observed class/role estimates, location,
health estimate if legitimately available, CC state, cast state, last
seen time. - Friendly roster: health, CC state, position, role,
threat/pressure estimate, current action. - Active crowd control and
expected expiry/immunity state, where the server exposes it. - Interrupt
assignments and recent interrupt history. - Current kill target and
secondary control targets. - Group cohesion, formation, and retreat
direction. - Ability/cooldown availability. - Recent tactical calls and
their expiry conditions.

### 2.5 Coordination and task ownership

Avoid seven bots independently deciding to perform the same task.

Examples: - Only one designated character should normally attempt the
primary mez on a given target. - A target already under long-duration
mez should be marked as protected from damage unless a deliberate break
is called. - One healer should normally own the urgent heal while the
other remains free for backup, unless incoming damage requires both. -
Interrupt targets should be distributed across available
interrupt-capable characters. - A peeler should be assigned to a
pressured friendly rather than having every tank abandon the assist
train. - A target should not receive redundant disease, root, or debuff
casts when the effect is already active, unless refreshing is useful and
safe.

Use short-lived task leases with expiry. If the assigned bot cannot act
(CC, range, LOS, interrupted, dead), another eligible bot can take over.

------------------------------------------------------------------------

## 3. Group-level tactical states

These states are shared intent, not rigid scripts. Each class interprets
them according to its role.

### 3.1 Roam / pre-contact

-   Maintain group cohesion and speed.
-   Keep support and casters in safe relative positions.
-   Avoid unnecessary combat actions that reveal or slow the group.
-   Identify enemy sightings and communicate them.
-   Do not let a tank or pet wander far from the group.

### 3.2 Initial contact

-   Identify enemy group direction and approximate composition.
-   Establish whether the group has initiative or is reacting.
-   Designate initial CC and interrupt targets.
-   Avoid dumping every CC ability at once.
-   Establish the first assist target only when there is a credible kill
    opportunity.
-   Preserve an escape route and avoid accidental overextension.

### 3.3 Engage / pressure

-   Apply coordinated pressure to the assigned target.
-   Maintain support positioning and healing coverage.
-   Use CC to isolate or disable high-value enemies.
-   Interrupt dangerous casts and enemy support.
-   Switch to peel when a friendly is in immediate danger.
-   Avoid damage into protected mez unless deliberately breaking it.

### 3.4 Kite / extend

-   Move away from enemy pressure while maintaining group cohesion.
-   Do not turn the retreat into eight unrelated paths.
-   Tanks prioritize snaring, stunning, or interrupting pursuers rather
    than chasing damage.
-   Support maintains enough distance to avoid being caught but remains
    able to heal/control.
-   Casters use safe instant/ranged interrupts where available and avoid
    stationary casts that expose them.
-   Re-engage only when the group has created a meaningful positional or
    numerical advantage.

### 3.5 Push / collapse

-   Move toward the selected engagement point or target.
-   Tanks apply pressure without outrunning support.
-   Support advances only to a role-appropriate safe position.
-   CC and interrupts should enable the push, not be spammed without
    regard to immunity or diminishing value.
-   Abort or modify the push if the group loses healing coverage or a
    critical member is controlled.

### 3.6 Peel / protect

-   Identify the friendly under pressure and the enemies creating that
    pressure.
-   Use the least costly effective control action: snare, stun, root,
    interrupt, body position, guard, or damage pressure as appropriate.
-   Do not automatically chase the enemy tank train away from the group.
-   Resume the prior group task when the immediate threat is resolved.

### 3.7 Regroup / disengage

-   Stop unnecessary pursuit.
-   Return toward a shared rally point or leader.
-   Recover speed, formation, and support coverage.
-   Reassess deaths, CC, cooldowns, and enemy position before
    re-engaging.
-   Do not re-enter merely because one enemy is visible.

------------------------------------------------------------------------

# 4. Albion playbook

## 4.1 Group template

  -----------------------------------------------------------------------
  Class                   Spec in template        Primary role
  ----------------------- ----------------------- -----------------------
  Cleric                  Rejuvenation 46,        Main healer
                          Enhancement 28          

  Cleric                  Enhancement 42,         Second healer
                          Rejuvenation 33         

  Sorcerer                Mind 40, Body 35        Main mezzer

  Minstrel                Instruments 47, Slash   Interrupt, backup mez,
                          44                      cure mez, speed

  Theurgist               Wind 45, Earth 28       Interrupt (pets)

  Paladin                 Chants 46, Shields 42,  Shield fighter (guard
                          Slash 39                and peel)

  Armsman                 Polearms 50, Shields    Assist train
                          42, Slash 39            

  Cabalist                Body 47, Spirit 26      Caster damage
  -----------------------------------------------------------------------

### 4.2 Cleric --- Rejuvenation 46 / Enhancement 28 --- Main healer

**Mission:** Keep the group alive through focused healing, spread/group
healing when appropriate, emergency recovery, and essential cures.
Preserve enough positioning and awareness to avoid being locked down
with the rest of the backline.

**Primary priorities** 1. Prevent an imminent friendly death. 2. Respond
to concentrated damage on the assist train or a support member. 3. Cure
high-impact control or debuffs when the cure changes the outcome. 4.
Maintain safe positioning and line of sight to likely heal targets. 5.
Use efficient healing rather than automatically using the largest heal.
6. Rebuff or resurrect only when the fight state permits.

**Healing decision logic** - Use a fast/large single-target heal when
one friendly is in immediate danger and the heal can land in time. - Use
spread heal when multiple group members are damaged and the spell's
smart-target behaviour makes it more efficient than repeatedly selecting
individuals. - Use group heal when several members are simultaneously
damaged and the group heal is more efficient for the situation. - Avoid
overhealing a target that is stable if another friendly is under lethal
pressure. - Predict near-term damage only from observed events (enemy
cast, melee pressure, target focus), not hidden information. - If the
other Cleric has already committed a sufficient heal to the same target,
redirect unless the target remains in danger.

**Cures** - Cure mez/stun/root or other control only if the class has
the relevant cure in the verified ruleset and the target's state makes
it useful. - Cure disease/poison when it materially affects survival,
movement, healing, or damage. - Cure nearsight when available to this
template's actual spell list; do not assume it is present at this spec
without verifying the spell data. - Coordinate cures: do not have both
healers waste a cure on the same target. - A cure should not be cast if
it will not remove the relevant effect or if the effect has already
expired.

**Positioning** - Prefer a safe rear/central position with line of sight
to the group. - Do not follow a tank deep into the enemy group to
preserve a heal. - Reposition early if enemy tanks are closing, rather
than waiting until interrupted. - During kite, move with the support
line and preserve range to the group.

**Offensive actions** - Damage and offensive control are secondary to
healing and survival. - Use an interrupt or defensive action only when
it does not create unacceptable healing downtime.

**Do not** - Spam the highest heal regardless of urgency or power
cost. - Cure an effect without checking whether the cure is available
and relevant. - Chase a target or move into melee simply to maintain a
heal. - Begin a long cast when an ally is likely to die before it
completes.

### 4.3 Cleric --- Enhancement 42 / Rejuvenation 33 --- Second healer

**Mission:** Provide backup healing while contributing buffs,
resist/utility support, and emergency healing coverage. This Cleric
should be more willing than the main healer to perform secondary tasks,
but must not abandon healing when pressure rises.

**Primary priorities** 1. Back up the main healer when a target is
taking dangerous damage. 2. Heal a different threatened friendly rather
than duplicating the main healer's target. 3. Maintain the verified
group buffs/resists appropriate to the template. 4. Handle assigned
cures when the main healer is occupied. 5. Provide secondary
control/interrupts only when healing coverage is stable. 6. Recover the
group after deaths when safe.

**Decision logic** - Track the main healer's current cast/target if the
server exposes it. - If the main healer is casting on a target, estimate
whether the incoming heal is sufficient; do not assume it will land. -
Take over if the main healer is interrupted, crowd-controlled, out of
range, or unable to respond. - Prioritize a different endangered ally if
multiple members are pressured. - Do not refresh buffs during active
lethal pressure unless the buff is urgently required and no healing
action is needed. - Do not spend time rebuffing a dead or unavailable
member while the living group is collapsing.

**Positioning** - Maintain a position that gives overlapping coverage
with the main healer without standing in exactly the same vulnerable
location. - Avoid both healers being caught by the same enemy push or
area control. - During kite, preserve line of sight and enough
separation to reduce shared exposure.

### 4.4 Sorcerer --- Mind 40 / Body 35 --- Main mezzer

**Mission:** Control the opening and flow of the fight. Identify
high-value targets, establish crowd control, prevent enemy
support/casters from acting, and maintain pressure through interrupts or
damage when CC is not the best action.

**Primary priorities** 1. Land high-value initial mez when the group has
a legitimate opportunity. 2. Control enemy support and dangerous
casters. 3. Prevent enemy actions through interrupt, amnesia, root, or
other verified control. 4. Maintain awareness of CC already applied and
avoid redundant casts. 5. Use damage only when it does not sacrifice a
more valuable control opportunity. 6. Stay alive; the main mezzer's
death can remove the group's primary control.

**Mez selection** - Prefer single-target mez when one specific target
must be controlled without catching nearby enemies or creating
unnecessary breaks. - Use AE mez when multiple enemies are valid targets
and the risk of catching low-value targets or breaking existing CC is
acceptable. - Consider target immunity, prior CC, enemy proximity, line
of sight, and whether the group is ready to capitalize. - Do not mez a
target already under useful mez unless refreshing is valid and
beneficial. - Avoid casting mez into damage that is already committed to
the target. - If an enemy is immune or likely immune, switch to another
control tool or target rather than repeatedly attempting mez.

**Amnesia and interrupts** - Use amnesia or another verified interrupt
tool against dangerous enemy casts, especially support or high-impact
casters. - Interrupt urgent casts before low-value damage. - Coordinate
with Minstrel and Theurgist so they do not all spend their interrupt on
the same target. - Maintain a list of observed enemy casters and recent
interrupt attempts.

**Root and secondary control** - Root can isolate or delay a target,
especially when mez is unsafe or the target is already immune to mez. -
Avoid rooting a target that the assist train is trying to kill if root
prevents melee contact and no tactical reason exists. - Use
debuffs/damage when the group is stable and control coverage is
adequate.

**Positioning** - Start from a position that allows initial CC without
exposing the Sorcerer to immediate melee. - Reposition after the
opening; do not remain stationary in a predictable location. - Keep
enough distance to cast, but not so far that the group cannot protect or
support you.

### 4.5 Minstrel --- Instruments 47 / Slash 44 --- Interrupt, backup mez, cure mez, speed

**Mission:** Provide group mobility, flexible interruption,
emergency/backup crowd control, and support-line protection. This is a
high-awareness role that must constantly choose between speed, control,
interrupt, and peel.

**Primary priorities** 1. Maintain the appropriate group speed/song
state outside combat and during repositioning, subject to verified
mechanics. 2. Interrupt enemy casters/support when doing so prevents a
high-value action. 3. Back up the Sorcerer's CC when the primary mezzer
is controlled, interrupted, dead, or occupied. 4. Cure mez when the
actual spell/ability is available and the target benefits. 5. Protect
the support line from enemy melee pressure. 6. Use melee styles only
when they support interruption/peel or when safe and tactically useful.

**CC and cure logic** - Do not automatically overwrite the Sorcerer's CC
assignment. - Use backup mez on targets not already controlled and not
protected for an assist kill. - Check immunity and existing CC before
casting. - Cure mez promptly on a critical friendly when the cure is
valid; avoid wasting it on a target who is immediately re-controlled or
whose release does not change the situation. - Coordinate cure ownership
with both Clerics.

**Interrupt logic** - Prioritize enemy healers/support actively casting,
then dangerous casters, then other relevant casts. - Use the cheapest
reliable interrupt that can land in time. - If a target is already
interrupted by a teammate, switch to another enemy. - Do not chase an
enemy caster so far that the Minstrel abandons speed, peel, or group
cohesion.

**Melee and styles** - Use snare/stun/interrupt styles only when their
actual opening conditions are met and the target is tactically
relevant. - Prefer a peel action against a tank attacking a healer over
damage on an unrelated target. - Do not assume a style's snare or stun
effect; read the server's style data.

**Speed of Sound** - Treat SoS as a group-level tactical resource, not a
personal speed buff. - On `/sos` or an equivalent call, coordinate
activation with the group retreat/push plan. - Do not activate
automatically whenever combat begins. - Consider whether the group needs
to escape CC/pressure, cross dangerous ground, secure an engage, or
create distance. - Respect the ability's actual duration, reuse timer,
and target/group rules.

### 4.6 Theurgist --- Wind 45 / Earth 28 --- Interrupt (pets)

**Mission:** Disrupt enemy casting and support actions through pets and
other verified control tools, while contributing damage when
interruption needs are covered.

**Primary priorities** 1. Identify enemy support/casters who are
free-casting or preparing high-impact actions. 2. Send appropriate pets
to interrupt targets with high tactical value. 3. Distribute pets across
multiple relevant casters rather than overcommitting all pets to one
target. 4. Maintain pet pressure when it is producing meaningful
interruption. 5. Use direct damage or control when pet pressure is
already sufficient or ineffective. 6. Preserve positioning and avoid
being caught while managing pets.

**Pet assignment** - Maintain an interrupt assignment table: target,
pet(s) assigned, expected arrival, last known result. - Avoid sending
additional pets to a target already reliably interrupted if another
enemy caster is free. - Reassign pets if the target dies, becomes
unreachable, or is no longer a priority. - Do not send pets blindly into
targets that are already controlled if the pet will break that CC. -
Consider travel time: a pet that cannot reach a cast before completion
is not a valid interrupt response.

**Other actions** - Use verified root, debuff, or damage spells when
they have greater value than another pet. - Use damage on the assist
target only when it does not sacrifice critical interrupt coverage. -
Track pet life, pathing, and target validity.

**Failure handling** - If pets get stuck or cannot path, record the
failure and choose a direct spell or new target. - Do not repeatedly
issue an identical failed pet command without state change.

### 4.7 Paladin --- Chants 46 / Shields 42 / Slash 39 --- Shield fighter (guard and peel)

**Mission:** Protect vulnerable group members, especially the support
line, through guard/defensive tools, positional awareness, and
control/peel. The Paladin is not simply a second assist tank.

**Primary priorities** 1. Keep the assigned guard/protection
responsibility on the most valuable exposed friendly, unless the group
leader changes assignment. 2. Identify enemy melee threatening healers,
casters, or the Minstrel. 3. Peel with shield control, snare/stun
styles, body positioning, or interrupts as actually available. 4.
Maintain relevant chants according to verified group mechanics and
situational value. 5. Contribute assist damage only when defensive
duties are stable. 6. Stay close enough to protect the group without
standing on top of the healer in a way that worsens area exposure.

**Guard and peel** - Use guard against the assigned friendly when the
mechanic is available and relevant to the incoming attack. - Do not
constantly switch guard target based on small health fluctuations;
switch when threat or group command justifies it. - Prioritize an enemy
tank actively attacking a healer over a distant enemy caster unless the
caster's action is more urgent. - Use shield styles according to their
verified opening conditions and effects. - If the target is already
controlled, do not waste a stun or break mez with damage. - If no
immediate peel is required, return to the assigned group task.

**Chants** - Maintain the most valuable chant(s) for the current
situation, subject to range, pulse/toggle mechanics, and server
implementation. - Avoid unnecessary chant switching that disrupts more
important actions. - Verify whether chants stack, pulse, or require
specific activation behaviour on this server.

### 4.8 Armsman --- Polearms 50 / Shields 42 / Slash 39 --- Assist train

**Mission:** Deliver coordinated melee pressure on the group's kill
target while remaining responsive to peel and tactical disengagement
calls.

**Primary priorities** 1. Follow the designated assist target when the
group is in kill mode. 2. Use the highest-value available damage/CC
style given the current opening. 3. Maintain contact without outrunning
support. 4. Apply snare or stun styles when they help secure the kill or
prevent escape. 5. Switch to peel when a critical friendly is under
immediate threat or a leader command assigns it. 6. Stop chasing when
the group kites, disengages, or the target becomes strategically
irrelevant.

**Assist behaviour** - Do not independently choose a new target simply
because it is closer or lower health. - Re-evaluate if the target is
unreachable, immune to relevant control, protected, or no longer a
viable kill. - Coordinate target switches through the Group Commander. -
Avoid overextending beyond healing/CC coverage.

**Style selection** - Use anytime styles when no positional/reactionary
opening is available. - Use positional styles only when the required
relative position is verified. - Use reactionary styles only after the
correct trigger (for example, evade/parry/block) is observed. - Use
snare styles when the target is escaping or when the group needs to
maintain pressure. - Use stun styles when the stun creates a kill window
or protects a friendly; avoid wasting it into immunity. - Do not blindly
repeat a style chain if its opener failed or the target moved out of
position.

### 4.9 Cabalist --- Body 47 / Spirit 26 --- Caster damage

**Mission:** Provide reliable ranged damage and utility while
maintaining safe positioning. Damage should support the group's kill
plan, not override control and survival needs.

**Primary priorities** 1. Assist on the designated kill target when a
kill window exists. 2. Apply relevant debuffs/disease or other verified
utility when they improve group pressure. 3. Interrupt dangerous enemy
casts when the Cabalist has a suitable tool and no higher-value damage
action is available. 4. Maintain range and line of sight without
exposing the caster. 5. Use pet/utility tools only when supported by the
actual spec and server data. 6. Reposition or kite when threatened.

**Damage logic** - Prefer coordinated damage on the assist target over
spreading damage across several enemies. - Do not nuke a mez-protected
target unless a deliberate break is called. - Consider target
resistances, debuffs, vulnerability, range, and cast time. - Avoid
beginning a long cast if a high-priority interrupt or defensive
reposition is needed. - Do not continue casting at a target that has
moved out of line of sight.

**Utility** - Use disease, debuffs, nearsight, or other tools only if
present in the actual spell list and valuable in the current matchup. -
Avoid redundant debuffs when the effect is already active. - Record
resist/immune outcomes to improve target selection.

------------------------------------------------------------------------

# 5. Hibernia playbook

## 5.1 Group template

  -----------------------------------------------------------------------
  Class                   Spec in template        Primary role
  ----------------------- ----------------------- -----------------------
  Druid                   Regrowth 44 / Nurture   Main healer
                          30                      

  Druid                   Nurture 42 / Regrowth   Second healer
                          33                      

  Bard                    Music 46 / Nurture 43 / Main mezzer, speed
                          Regrowth 19             

  Hero                    Blades 50 / Shields 42  Shield fighter (guard
                          / Parry 39              and peel)

  Blademaster             Celtic Dual 50 / Blades Assist train
                          44 / Parry 37           

  Blademaster             Celtic Dual 50 / Blades Assist train
                          44 / Parry 37           

  Eldritch                Light 45 / Mana 29      Caster damage,
                                                  nearsight

  Enchanter               Light (remaining spec   Caster damage
                          points to be confirmed) 
  -----------------------------------------------------------------------

### 5.2 Druid --- Regrowth 44 / Nurture 30 --- Main healer

**Mission:** Primary healing and group survival, with attention to
cure/utility responsibilities and safe support positioning.

**Primary priorities** 1. Prevent imminent friendly deaths. 2. Heal
focused targets and stabilize the group. 3. Use spread/group healing
when multiple allies are damaged and the spell is appropriate. 4. Cure
high-impact conditions when the cure changes the fight. 5. Maintain
required buffs/resists according to the actual template and spell data.
6. Preserve position and line of sight to the group.

**Healing** - Use fast single-target healing for urgent focused
damage. - Use spread healing when damage is distributed and its
smart-target behaviour is useful. - Use group healing when multiple
members are simultaneously damaged and it is more efficient than
individual heals. - Avoid redundant heals if the second Druid has a
sufficient heal committed. - If the other Druid is controlled or
interrupted, take over urgent healing immediately.

**Cures and utility** - Cure disease/poison/control effects only when
the Druid has the relevant spell and it is useful. - Coordinate cure
assignments with the second Druid and Bard. - If the template does not
provide a particular cure (for example, nearsight cure), assign it to
another verified class/ability or mark it unavailable. - Use pet or
other utility only if available and tactically justified.

**Positioning** - Remain in the support line with safe distance from
enemy tanks. - Avoid both Druids sharing the same exposed position. -
Maintain line of sight and healing range during kiting. - Do not push
forward just to maintain a marginal heal.

### 5.3 Druid --- Nurture 42 / Regrowth 33 --- Second healer

**Mission:** Backup healing, buff/resist maintenance, and flexible
support. This Druid should keep the main healer's coverage stable while
being ready to take over.

**Primary priorities** 1. Back up urgent healing. 2. Heal a separate
threatened friendly when several are under pressure. 3. Maintain
verified buffs/resists and restore them after death when safe. 4. Handle
assigned cures. 5. Use available pet/utility/interrupt tools only when
healing coverage permits. 6. Stay alive and preserve a second healing
source.

**Decision logic** - Avoid duplicating the main Druid's heal when
another friendly is in greater danger. - Take over when the main Druid
is controlled, interrupted, out of range, or unable to act. - Do not
rebuff during a lethal damage spike unless the buff is immediately
necessary. - Keep enough separation from the main Druid to avoid one
enemy action controlling both.

### 5.4 Bard --- Music 46 / Nurture 43 / Regrowth 19 --- Main mezzer, speed

**Mission:** Drive group tempo, provide speed, establish initial crowd
control, coordinate demezz/backup healing, and manage the group's
movement. This is a high-responsibility role.

**Primary priorities** 1. Maintain appropriate speed outside combat and
during movement. 2. Identify the best initial mez opportunity. 3.
Control dangerous enemy support/casters and isolate targets. 4.
Coordinate with the group before breaking or replacing CC. 5.
Demezz/cure mez when the verified ability is available and the target's
release is valuable. 6. Use interrupt, root, or other utility when mez
is unsafe or not useful. 7. Maintain safe positioning; do not become the
first easy target.

**Mez logic** - Use single-target mez for a high-value isolated target
when AE risk is unacceptable. - Use AE mez when multiple targets can be
controlled without catching allies' intended kill target or causing
unnecessary breaks. - Avoid mez on targets already controlled or
immune. - Do not mez targets receiving committed damage unless the group
has chosen to break/kill. - Prioritize enemy support or a dangerous
caster when doing so creates a meaningful advantage. - Track CC duration
and immunity where server data permits.

**Speed and SoS** - Maintain the correct speed state for travel and
combat transitions. - Treat Speed of Sound as a coordinated group
resource. - On `/sos`, coordinate with the group order: retreat,
reposition, or engage. - Do not activate merely because an enemy
appears. - Ensure companions understand whether SoS means kite away,
cross ground, or push through an engagement.

**Demezz and healing** - Demezz a critical friendly when the ability is
available and the release can affect the fight. - Do not waste a demezz
on a target likely to be immediately re-controlled if a better target
needs it more. - Use backup healing only when the Bard can safely do so
and the group needs it; do not let a low-priority heal replace essential
CC or speed management.

**Positioning** - Stay close enough to control and support the group,
but avoid being caught by enemy tanks. - During kite, maintain distance
and line of sight while preserving the ability to control pursuers. - Do
not chase a target beyond the group's ability to protect the Bard.

### 5.5 Hero --- Blades 50 / Shields 42 / Parry 39 --- Shield fighter (guard and peel)

**Mission:** Protect the support line and control enemy melee pressure.
The Hero is the primary defensive tank in this template.

**Primary priorities** 1. Guard the assigned friendly according to
current threat and group assignment. 2. Peel enemy tanks attacking
Druids, Bard, or Eldritch. 3. Use shield control and weapon styles to
interrupt, snare, or stun when verified and useful. 4. Maintain a
position that protects the backline without abandoning the group. 5.
Join assist pressure only when defensive responsibilities are stable. 6.
Respond to `/peel`, `/extend`, and `/regroup` commands.

**Peel logic** - Identify the enemy currently pressuring the
highest-value friendly. - Prefer a reliable interrupt or control action
that stops pressure. - Use stun when it creates a meaningful protection
window; account for immunity. - Use snare when the objective is to
create distance rather than lock the enemy in place. - Avoid damaging a
mez-protected enemy unless a break is deliberate. - Do not chase the
enemy tank far from the protected friendly.

**Guard** - Maintain guard on the assigned friendly. - Change assignment
only when the current target is safe, another friendly is under more
immediate threat, or the leader changes the plan. - Do not switch
constantly due to minor health differences.

### 5.6 Blademaster --- Celtic Dual 50 / Blades 44 / Parry 37 --- Assist train (two characters)

**Mission:** Deliver coordinated melee damage on the assist target. The
two Blademasters should behave as a coordinated train, not as
independent attackers.

**Primary priorities** 1. Follow the shared assist target. 2. Maintain
melee contact while staying within support coverage. 3. Select the best
valid style based on current opening and desired effect. 4. Apply
snare/stun or other control styles when they support the kill or stop an
escape. 5. Switch to peel only when assigned or when a critical friendly
is in immediate danger. 6. Stop chasing and return on kite/regroup
commands.

**Two-BM coordination** - Avoid both BMs spending the same control
effect on the same target if one application is sufficient. - Coordinate
style choices when one BM can set up a follow-up or positional
opportunity for the other. - If one BM is assigned to peel, the other
may remain on assist unless the group order changes. - Avoid both
switching targets independently. - Use shared target and target-state
information.

**Style selection** - Anytime style when no better opening exists. -
Positional style only when the required position is confirmed. -
Reactionary style only after the appropriate evade/parry/block or other
trigger. - Snare style when maintaining contact or preventing escape is
more valuable than immediate damage. - Stun style when it creates a kill
window or stops pressure; avoid into immunity or redundant stun. -
Follow-up styles only when their opener succeeded and the chain remains
valid. - Verify the actual style effects from the server's style data;
do not infer effects from style names.

**Assist discipline** - Do not continue hitting an old target after the
Group Commander switches target. - Do not switch merely because a nearby
enemy is low health. - If target is unreachable or protected,
request/trigger a coordinated retarget rather than independently
choosing.

### 5.7 Eldritch --- Light 45 / Mana 29 --- Caster damage, nearsight

**Mission:** Provide primary ranged caster pressure, use
nearsight/utility to reduce enemy caster effectiveness, and maintain
safe positioning.

**Primary priorities** 1. Assist on the designated kill target when a
kill window exists. 2. Apply nearsight to a high-value enemy
caster/support when it materially reduces their effectiveness. 3.
Coordinate damage with the Enchanter and melee train. 4. Interrupt
dangerous casts when a suitable tool is available and the opportunity is
urgent. 5. Maintain range, line of sight, and safe positioning. 6. Avoid
breaking mez or wasting casts on invalid targets.

**Nearsight** - Prioritize enemy casters/support whose range or ability
to influence the fight matters most. - Avoid redundant nearsight if the
effect is already active and cannot be meaningfully improved. - Do not
nearsight a target that is about to die if another high-value caster is
free. - Track immunity/resist/failure outcomes where possible. -
Consider cast time and exposure: do not stand in danger for a low-value
nearsight.

**Damage** - Focus damage on the assist target. - Coordinate with the
Enchanter to avoid unnecessary split damage. - Do not nuke mez-protected
targets unless a deliberate break is called. - Avoid long casts when
immediate movement, interrupt, or defense is more important.

### 5.8 Enchanter --- Light (remaining spec points to be confirmed) --- Caster damage

**Mission:** Provide coordinated caster damage and class-specific
utility supported by the actual specialization. The exact spell package
cannot be finalized until the remaining specialization points and server
spell data are confirmed.

**Primary priorities** 1. Assist on the shared kill target. 2. Use
available Light-line damage/utility according to verified spell data. 3.
Coordinate damage timing with the Eldritch and melee train. 4. Use
pet/utility abilities only if present in the actual template and
ruleset. 5. Maintain safe range and line of sight. 6. Avoid breaking
valuable CC.

**Required clarification** - Confirm the Enchanter's full specialization
allocation. - Verify which spells are available at that spec in
OfflineDAoC. - Determine whether the intended role includes a specific
pet, debuff, stun, or other utility action. - Until confirmed, implement
only the generic caster/assist behaviour and do not invent missing spell
access.

------------------------------------------------------------------------

# 6. Midgard playbook

## 6.1 Group template

  -----------------------------------------------------------------------
  Class                   Spec in template        Primary role
  ----------------------- ----------------------- -----------------------
  Healer                  Pacification 44 /       Main mezzer, control
                          Mending 31              before heals

  Healer                  Mending 40 /            Main healer, backup mez
                          Augmentation 36         

  Shaman                  Augmentation 47 /       Interrupt (disease,
                          Subterranean 26         root), backup heals

  Skald                   Battlesongs 46 / Sword  Interrupt, speed
                          44                      

  Runemaster              Runecarving 47 /        Caster damage,
                          Suppression 26          nearsight

  Savage                  Savagery 49 / Hand to   Assist train
                          Hand 44                 

  Berserker               Left Axe 50 / Axe 44    Assist train

  Warrior                 Hammer 50 / Shields 42  Shield fighter (guard
                          / Parry 39              and peel)
  -----------------------------------------------------------------------

### 6.2 Healer --- Pacification 44 / Mending 31 --- Main mezzer, control before heals

**Mission:** Lead crowd control and interrupt activity, control enemy
support/casters, and provide backup healing when control duties allow.
The role is explicitly control-first, but not at the expense of
preventable deaths.

**Primary priorities** 1. Land high-value initial mez when there is a
safe opportunity. 2. Control enemy support and dangerous casters. 3.
Interrupt high-impact casts with available tools. 4. Use root/stun/other
control when mez is unsafe, resisted, or immunity makes it ineffective.
5. Heal when an ally is in danger and no more valuable control action
can prevent the damage. 6. Coordinate with the Mending Healer to avoid
duplicated control and healing.

**Mez logic** - Use single-target mez for precise control of a key
target. - Use AE mez when multiple targets can be caught safely and the
group can capitalize. - Avoid redundant mez on already controlled or
immune targets. - Track CC and immunity where possible. - Do not mez an
enemy that is receiving committed damage unless a deliberate break is
intended.

**Control before heals --- interpretation** - This means establish and
maintain valuable control when the group is stable. - It does not mean
ignore an ally who is about to die. - An imminent lethal threat
overrides routine CC. - If the Mending Healer has healing coverage, the
Pacification Healer can remain forward and focus more heavily on
control. - If the Mending Healer is controlled, interrupted, or out of
range, the Pacification Healer must shift toward emergency healing.

**Interrupts** - Use amnesia or other verified interrupt tools on
dangerous enemy casts. - Coordinate with Shaman and Skald. - Prioritize
interrupting a heal, major CC, or high-impact damage cast over low-value
targets.

**Positioning** - May play somewhat forward to control, but must not
overextend beyond support. - Preserve a path back to the group. -
Reposition after initial CC and avoid becoming an easy tank target.

### 6.3 Healer --- Mending 40 / Augmentation 36 --- Main healer, backup mez

**Mission:** Primary healing, essential support buffs, and backup crowd
control when the Pacification Healer is occupied.

**Primary priorities** 1. Prevent imminent friendly deaths. 2. Heal
focused targets and stabilize multiple damaged allies. 3. Maintain
relevant celerity/buffs/resists from the verified spell list. 4. Take
over control when the Pacification Healer is controlled, interrupted,
dead, or otherwise unable to act. 5. Provide backup interrupts/amnesia
when healing is stable. 6. Resurrect and rebuff after a death only when
the immediate fight state permits.

**Healing logic** - Use fast single-target healing for urgent damage. -
Use spread/group healing based on how damage is distributed and the
actual spell behaviour. - Avoid redundant heals when the Pacification
Healer or another source is already covering the target. - If several
allies are threatened, prioritize by time-to-death and available
incoming healing, not simply lowest health percentage. - Do not let
routine buff maintenance delay an emergency heal.

**Buff and celerity logic** - Maintain the correct group buff/celerity
state where appropriate. - Reapply after deaths or expiry when safe. -
Do not waste time refreshing effects that remain active. - Verify
stacking and pulse behaviour in the target server.

**Backup mez** - Use only when the Pacification Healer cannot cover the
required target or a second CC assignment is explicitly needed. - Avoid
overlapping mez on the same target. - Coordinate with Skald and Shaman
control.

### 6.4 Shaman --- Augmentation 47 / Subterranean 26 --- Interrupt (disease, root), backup heals

**Mission:** Disrupt enemy movement and effectiveness with disease,
root, debuffs, and interrupts; provide backup healing when necessary.
The Shaman is a flexible utility class whose action priorities change
quickly with the fight.

**Primary priorities** 1. Disease high-value enemy melee/support when it
reduces their effectiveness or enables the kite. 2. Root targets whose
movement must be controlled, especially pursuers or exposed support. 3.
Interrupt dangerous enemy casts with available tools. 4. Apply relevant
stat/utility debuffs when they have tactical value. 5. Back up healing
when the main Healer cannot cover the damage. 6. Maintain position and
avoid being caught while casting.

**Disease** - Prioritize enemy tanks pursuing the support line or
targets whose healing/movement effectiveness is important. - Avoid
repeatedly applying disease to a target already affected unless
refreshing is justified. - Prefer targets that can be reached safely and
whose debuff matters now. - Do not spend a long cast on a low-value
target while an urgent cast needs interrupting.

**Root** - Root pursuers to create distance and protect support. - Root
enemy support/casters when isolating them creates a useful advantage. -
Do not root the assist target if it prevents the melee train from
reaching it, unless the group is intentionally controlling rather than
killing that target. - Check immunity and existing CC. - Re-root after
purge/expiry only when the target remains a relevant threat and the
group can capitalize.

**Backup healing** - Heal urgent targets when the primary Healer is
occupied, controlled, or unable to respond. - Do not automatically
become a full-time healer if the primary healer is functioning and
utility is more valuable. - Coordinate to avoid redundant heals.

**Positioning** - Stay close enough to support the group but far enough
to avoid enemy tank pressure. - During kite, root/disease pursuers and
move with the support line. - Avoid pushing so far forward that backup
healing becomes impossible.

### 6.5 Skald --- Battlesongs 46 / Sword 44 --- Interrupt, speed

**Mission:** Provide speed and mobile interruption, support the group's
movement, and help control enemy casters or melee threats. The Skald is
a flexible roaming/interrupt role, not a second assist tank by default.

**Primary priorities** 1. Maintain appropriate group speed. 2. Interrupt
dangerous enemy casters/support. 3. Help peel or snare/stun pursuers
when the support line is threatened. 4. Use damage on the assist target
only when interrupt/peel responsibilities are covered. 5. Support push,
kite, and regroup commands. 6. Avoid overextending to chase a low-value
target.

**Speed** - Maintain the correct speed state outside combat and during
movement. - Coordinate speed/song decisions with group state. - Do not
let song maintenance override an immediate interrupt or survival action
when the mechanics allow a choice.

**Interrupt** - Prioritize enemy support actively casting, then
dangerous casters. - Use available instant/ranged or melee interrupt
tools according to verified mechanics. - Avoid duplicating the
Pacification Healer's or Shaman's interrupt on the same cast if another
target is free. - Stay within a useful radius of the group.

**Melee** - Use snare/stun styles when the actual opening and tactical
situation justify them. - Peel enemies attacking support rather than
pursuing damage into the enemy backline. - Follow assist only when
explicitly assigned or when the group is in a coordinated collapse.

### 6.6 Runemaster --- Runecarving 47 / Suppression 26 --- Caster damage, nearsight

**Mission:** Provide ranged damage and caster disruption, particularly
nearsight and other verified utility.

**Primary priorities** 1. Assist on the designated kill target. 2. Apply
nearsight to high-value enemy casters/support when useful. 3. Use
available control/utility from the actual spec to disrupt enemy actions.
4. Maintain safe range and line of sight. 5. Avoid breaking mez and
avoid split damage without a tactical reason. 6. Reposition when
pressured.

**Nearsight** - Select targets by tactical importance: enemy support or
caster whose range/uptime matters. - Avoid redundant casts on an already
affected target. - Do not nearsight a target that is already controlled
and safely irrelevant if another caster is free. - Track resistance,
immunity, and effect state where possible.

**Damage** - Focus the shared assist target. - Coordinate with other
casters to create a damage window. - Avoid long casts if movement or
interruption is urgent. - Reassess when the target leaves line of sight
or becomes protected by CC.

### 6.7 Savage --- Savagery 49 / Hand to Hand 44 --- Assist train

**Mission:** Apply coordinated melee pressure and capitalize on the
group's kill windows. The Savage should follow the assist target while
managing its own self-enhancement/resource/health costs according to
verified class mechanics.

**Primary priorities** 1. Follow the designated assist target. 2.
Maintain melee contact without outrunning support. 3. Use the most
valuable valid style based on opening, position, and target state. 4.
Apply snare/stun or other control when it secures contact or creates a
kill window. 5. Manage self-buffs/abilities and their costs; do not
blindly activate everything. 6. Peel only when assigned or when an
immediate emergency requires it. 7. Respect kite, push, and regroup
commands.

**Style and ability logic** - Select anytime, positional, reactionary,
and follow-up styles based on actual conditions. - Verify style effects
from server data. - Avoid repeating a failed chain opener. - Consider
whether a control style is more valuable than a damage style. - Monitor
health/resource costs of Savage abilities and avoid self-inflicted death
or excessive healer burden. - Coordinate with Berserker to avoid
redundant control and maintain assist discipline.

### 6.8 Berserker --- Left Axe 50 / Axe 44 --- Assist train

**Mission:** Deliver coordinated melee burst/pressure on the assist
target while respecting group positioning and control.

**Primary priorities** 1. Follow the shared assist target. 2. Use
high-value valid styles and class abilities when the target is in a kill
window. 3. Maintain melee contact while remaining within support
coverage. 4. Use snare/stun or other control styles when they help
secure the kill or protect the group. 5. Avoid uncontrolled target
switching. 6. Return promptly on kite/regroup commands.

**Style logic** - Use the best valid style chain for the current
opening. - Use positional/reactionary styles only when their conditions
are actually satisfied. - Prefer snare when preventing escape is more
valuable than raw damage. - Avoid wasting stun into immunity or on a
target already controlled. - Do not use a long or costly action if the
target is about to leave range and a faster option is available.

**Coordination** - Coordinate burst windows with Savage and caster
damage. - Avoid both tanks abandoning the assist target to chase
separate enemies. - If assigned to peel, communicate/record the
temporary role change.

### 6.9 Warrior --- Hammer 50 / Shields 42 / Parry 39 --- Shield fighter (guard and peel)

**Mission:** Protect the support line, control enemy melee pressure, and
provide a defensive anchor for the group.

**Primary priorities** 1. Guard the assigned friendly. 2. Identify and
peel enemy tanks attacking Healers, Shaman, Skald, or Runemaster. 3. Use
shield control and weapon styles to interrupt, stun, or snare where
appropriate. 4. Maintain a defensive position and avoid being drawn too
far from the group. 5. Join assist pressure only when defensive
responsibilities are stable. 6. Follow `/peel`, `/extend`, `/regroup`,
and `/hold` commands.

**Peel** - Prioritize the enemy currently creating the greatest threat
to a critical friendly. - Use the least costly effective control
action. - Do not break useful mez without a deliberate reason. - Avoid
chasing a tank away from the friendly it is supposed to protect. -
Return to guard/defensive assignment when the immediate threat ends.

**Guard** - Keep guard on the assigned target while it remains the best
defensive assignment. - Change only when threat or leader instruction
justifies it. - Record guard target changes and the reason.

------------------------------------------------------------------------

# 7. Cross-class action policy

This section defines common behaviour that should be shared by all
classes.

## 7.1 Healing policy

Estimate friendly urgency using more than health percentage.

Useful inputs: - Current health percentage and absolute health. - Recent
damage rate. - Observed enemies attacking the target. - Enemy cast
currently directed at the target, if known. - Current CC state. -
Incoming heals already committed. - Distance and line of sight. -
Whether the target is a critical role (healer, mezzer, group driver). -
Whether the target is in range of enemy melee.

A conceptual urgency model:

`heal_urgency = death_risk + incoming_pressure + role_criticality + lack_of_other_healing - distance_penalty - redundant_heal_penalty`

Do not assume exact health prediction is possible unless the server
exposes the required information. Start with observable health/damage
and improve later.

## 7.2 Crowd-control policy

Track: - CC type. - Target. - Caster/source. - Start time and expected
duration. - Break event/source if known. - Immunity state/expiry if the
server exposes it. - Whether the group intends to preserve or break it.

General rules: - Do not overwrite valuable CC without reason. - Do not
damage a mez-protected target unless a deliberate break/kill call
exists. - Prefer the narrowest CC that solves the problem when AE risk
is high. - Use root to control movement when mez is unsafe or
inappropriate. - Use stun for a short, high-value window, not as an
automatic opener. - Avoid redundant attempts into known immunity. -
Reassess after purge, resist, expiry, or accidental break.

## 7.3 Interrupt policy

An interrupt is valuable only if it prevents or delays an action that
matters.

Prioritize: 1. Enemy healer/support completing a high-impact heal or
control spell. 2. Enemy mezzer or caster attempting a decisive
control/damage cast. 3. Enemy caster pressuring a critical friendly. 4.
Other relevant casts based on current group strategy.

Do not waste all interrupt tools on the same target. Assign targets and
track recent attempts. If an enemy cast completes before an interrupt
can arrive, choose a different response.

## 7.4 Nearsight and debuff policy

-   Prioritize targets whose range or effectiveness matters to the
    current fight.
-   Avoid redundant applications.
-   Consider cast time, target value, resistance, immunity, and whether
    the target is already controlled.
-   Do not spend a long cast on a low-value target while an urgent
    interrupt or heal is needed.
-   Verify exact availability and effect in the target server's spell
    data.

## 7.5 Disease policy

-   Disease can be valuable against enemy melee pressure, healing, or
    movement depending on the actual ruleset effect.
-   Prioritize high-impact targets rather than blindly spreading it to
    every visible enemy.
-   Avoid redundant casts where the effect is already active.
-   Verify duration, recast, resistance, and exact effect.

## 7.6 Melee style selection

A melee AI needs a style evaluator, not a static chain.

Candidate style features: - Opening condition: anytime, behind, side,
front, after evade, after parry, after block, follow-up. - Current
relative position and facing. - Whether the required defensive event
occurred. - Effect: damage, snare, stun, bleed, debuff, threat/interrupt
effect, or other server-defined outcome. - Endurance cost. - Style
growth/damage value. - Whether the target is immune or already
affected. - Whether the target is fleeing, pursuing, casting, or
attacking a friendly. - Group state: assist, peel, kite, push,
disengage.

Selection principles: - Prefer a control style over damage when control
is the immediate objective. - Prefer damage when the group has a kill
window and control is unnecessary. - Do not attempt positional styles
based on guessed position. - Do not attempt reactionary styles without
the required event. - Do not repeat a follow-up style if its chain
opener did not land. - Stop attacking when a retreat command requires
disengagement, unless a short control action is explicitly part of the
retreat plan. - The exact style effect must come from the server's style
data, not from assumptions based on style names.

## 7.7 Pet policy

For classes with pets: - Maintain pet ownership, target, current order,
path, and state. - Assign pets to meaningful targets. - Avoid sending
pets to break valuable mez. - Reassign on target death, loss of path, or
changed tactical priority. - Record pet travel time and failed
pathing. - Do not assume pet commands succeed immediately.

## 7.8 Positioning policy

Positioning should be evaluated continuously.

Relevant inputs: - Distance to group center and assigned formation
point. - Distance to support and assist target. - Distance to enemy
melee and casters. - Line of sight to friendly heal targets and enemy
targets. - Current group state and retreat direction. - Role-specific
safe range. - Terrain and pathfinding cost. - Whether moving will
interrupt a cast or cause loss of a critical action.

Role tendencies: - Healers: protected support line, overlapping but not
identical positions. - Main CC: enough forward range to control, with a
safe retreat path. - Interrupt support: mobile, near enough to disrupt,
not isolated. - Assist tanks: close to assist target but within support
coverage. - Guard/peel tanks: positioned to intercept threats to
assigned friendlies. - Damage casters: safe range and line of sight,
avoid unnecessary exposure.

Do not force every character into a fixed formation coordinate. Use
role-dependent preferred regions and dynamic threat avoidance.

------------------------------------------------------------------------

# 8. Human tactical commands

The player should be able to issue short commands through in-game macros
or another verified command mechanism.

Initial command set:

  -----------------------------------------------------------------------
  Command                 Shared intent           Expected class-specific
                                                  interpretation
  ----------------------- ----------------------- -----------------------
  `/extend`               Kite/pull back          Tanks snare/peel
                                                  pursuers; support
                                                  retreats with healing
                                                  coverage; casters
                                                  reposition and
                                                  interrupt

  `/push`                 Advance/collapse        Assist tanks advance;
                                                  casters follow at safe
                                                  range; support moves
                                                  only as far as safe

  `/assist`               Focus designated target Assist train and
                                                  casters align damage;
                                                  CC avoids protecting
                                                  the kill target

  `/peel`                 Protect pressured       Guard/peel tanks
                          friendly                prioritize threats;
                                                  other classes help with
                                                  CC/interrupt

  `/regroup`              Reform group            Stop chase, return to
                                                  rally point, restore
                                                  cohesion

  `/hold`                 Hold position           Avoid unnecessary
                                                  chase; maintain
                                                  defensive spacing

  `/sos`                  Execute coordinated SoS Bard/Minstrel/Skald
                          plan                    uses verified SoS if
                                                  available and
                                                  appropriate; all
                                                  members follow the
                                                  declared push/retreat
                                                  direction
  -----------------------------------------------------------------------

A command should contain: - Command type. - Issuing player/leader. -
Timestamp. - Optional target or direction. - Priority. -
Expiry/termination condition. - Whether the command is still active.

The system must distinguish `/sos` from the tactical intent associated
with it. For example, SoS may be used to retreat, cross a dangerous
area, or push through a controlled opening. The command should specify
the intended movement, not assume that every SoS activation means "run
away."

------------------------------------------------------------------------

# 9. Matchup and adaptation

The AI should adapt its action weights to observed enemy composition and
current behaviour.

Examples: - Against a caster-heavy group, increase the value of
interrupting free-casting casters and preserving line of sight. -
Against a tank-heavy group, increase peel, disease/root, support
spacing, and coordinated kiting value. - If the enemy support line is
exposed, increase the value of CC or pressure on those targets. - If the
enemy group is retreating, avoid uncontrolled chase; maintain formation
and choose whether to pursue or reset. - If the player's group has lost
a healer or main CC, shift to a survival/disengage strategy where
appropriate.

Do not hard-code a single response to an enemy composition. Use
composition as a prior; actual observed behaviour should determine
decisions.

------------------------------------------------------------------------

# 10. Logging and evaluation requirements

Every significant action should have an explainable decision record.

Suggested record fields: - Match ID and timestamp. - Character ID,
class, spec, and role. - Current group tactical state. - Observations
available to the character. - Candidate actions considered. - Invalid
actions and why they were rejected. - Utility components for shortlisted
actions. - Selected action and reason. - Action result: success, resist,
miss, interrupted, out of range, LOS failure, target invalid, etc. -
Relevant state change after the action.

### Initial performance metrics

**Healers** - Time to respond to urgent damage. - Preventable deaths. -
Overheal rate. - Redundant healing rate. - Cure success and wasted cure
attempts. - Time spent out of healing position.

**CC / support** - Initial mez success. - Useful CC duration. - CC
applied to high-value targets. - Accidental CC breaks. - Redundant CC
attempts. - Interrupts that prevented meaningful casts. - Time spent
exposed or out of position.

**Tanks** - Assist-target compliance. - Time on assigned target. -
Useful snare/stun applications. - Peel response time. - Time spent out
of support range. - Unnecessary chase/overextension. - Guard uptime and
useful guard events.

**Casters** - Damage contribution to kill targets. - Useful
nearsight/debuff applications. - Interrupt contribution. - Wasted casts
due to LOS/range/invalid target. - Time spent out of safe position. -
Damage into protected CC.

**Group** - Time to find an opponent. - Group cohesion during roam and
kite. - Time to first kill. - Win rate across repeated matchups. -
Deaths by role. - Command response time. - Percentage of commands
executed correctly. - Re-engagement quality after kite/regroup.

**Human player** - Target selection and switching. - Interrupt
opportunities taken/missed. - CC timing and coordination. - Positioning
relative to group. - Defensive ability usage. - Command timing and
effectiveness. - Time attacking ineffective or unreachable targets.

------------------------------------------------------------------------

# 11. Open questions and verification checklist

Resolve these before treating the playbook as an exact ability
specification.

1.  Confirm the exact OfflineDAoC build/ruleset and whether its
    mechanics align with the intended 1.65-era baseline.
2.  Confirm the full Enchanter specialization.
3.  Verify all class spell lists and spell effects from server data.
4.  Verify whether Cleric/Druid/Healer cure abilities in the templates
    include cure nearsight, cure mez, cure root, disease, and poison as
    applicable.
5.  Verify exact Bard/Minstrel/Skald Speed of Sound and speed/song
    behaviour.
6.  Verify which classes have amnesia, nearsight, disease, root, instant
    CC, and interrupt tools at the supplied specs.
7.  Verify style opening conditions and effects for each melee spec.
8.  Verify stun/root/mez immunity and duration rules.
9.  Verify pet control and pet pathfinding for Theurgist and any other
    pet-enabled template class.
10. Verify group buffs, resists, celerity, chants, and stacking rules.
11. Verify what combat, cast, target, health, CC, and position
    information the server exposes to bots.
12. Verify whether the server can detect the player's normal client
    macros/custom commands safely.

When uncertain, mark the action as `UNVERIFIED` and keep it out of
executable logic until checked.

------------------------------------------------------------------------

# 12. Implementation guidance for Claude Code

Treat this playbook as desired behaviour, not as proof of existing
functionality.

Before implementation: 1. Map each class to the actual server class
definition and spell/style data. 2. Build an ability catalogue from
server data rather than maintaining a manually duplicated list. 3.
Represent actions with metadata: type, target rules, range, cast time,
recast, duration, resource cost, interrupt behaviour, CC category,
effect, and style opening conditions. 4. Implement shared decision
interfaces so each class can score actions using common state. 5. Keep
class-specific priorities and weights in configuration/data where
practical. 6. Log the reason for every meaningful decision. 7. Add
scenario tests for urgent heal, mez opportunity, CC protection,
interrupt assignment, peel, kite, push, and regroup. 8. Compare AI
versions using repeatable encounters.

Do not implement a rigid action rotation. Do not make every class use
the same priority list. Do not let an LLM choose spells or styles in the
real-time combat loop.

The first implementation should support a small, testable subset of
actions for one group composition. Expand only after recording shows
that the basic decisions are reliable.

------------------------------------------------------------------------

## 13. Review notes

This draft deliberately defines role intent and decision policy more
completely than exact spell/style selection. Exact ability selection
must be grounded in the actual OfflineDAoC data and target ruleset.

Items especially requiring owner review: - Whether the role split
matches the intended historical group playstyle. - Whether the
main/second healer responsibilities are correct for each realm. -
Whether the Bard/Minstrel/Skald should have more explicit offensive or
defensive interrupt assignments. - Whether the shield tank should always
guard one fixed support or dynamically select the threatened friendly. -
Whether the two assist tanks should have identical behaviour or distinct
assignments. - Whether the AI should preserve CC by default and require
an explicit break/kill decision. - Whether the player should be the
tactical leader by default, or whether the AI leader can make autonomous
calls.

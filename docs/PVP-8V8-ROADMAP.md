# 8v8 roadmap

The plan for taking the bots from their current state to a challenging 8v8: one human with seven
companion bots roaming until they meet and fight an enemy group of eight bots. Work happens as 8 bots
vs 8 bots first, with every fight recorded and analyzed, and the human joins once the bots are worth
fighting.

Written 2026-11-16 from a code assessment. Nothing in it has been built yet.

## Order of work (owner, 2026-11-16)

Bots first. Everything that needs a human at the keyboard waits until the bots fight well: `/ready`
(M1.4), trying `/8v8 group` in a client (M1.6), realm points from kills (M1.5) and all of M9. The
path now is M2 (arena and match loop), M3 (recorder, analyzer, baseline), then M4 to M8, each
measured by scrims. M1 counts as finished for that purpose.

## How to use this file

- Every step has an ID (`M3.2`) and a checkbox. Tick it when its "done when" line is true.
- A milestone is finished when its exit check passes, not when its steps are ticked.
- Sizes are rough: **S** about a session, **M** a few sessions, **L** a week or more of sessions.
- The scorecard near the end holds the numbers. Fill the baseline column at M3 and update the
  "current" column after each milestone.
- Command names (`/ready`, `/scrim`, `/8v8`, `/call`) are placeholders until built.

## Definition of done

8v8 is "challenging" when all of these hold:

1. **The new AI clearly beats today's AI.** In mirrored scrims (same groups, sides swapped) it wins
   at least 90% of matches against the baseline recorded at M3.
2. **Bots play their roles.** The scorecard targets are met, and the analyzer's failure list has no
   entries of the "obviously wrong" kind (healer in melee, mez broken by own group, idle bot, Purge
   unused while mezzed to death).
3. **The fights are fair.** A bot and a player of the same class, spec and gear produce the same
   combat numbers, and bots are bound by the same sight, range and interrupt rules.
4. **The owner agrees.** At the default difficulty, over 20 fights with the owner leading a group,
   the result is close to even, and losses come from being outplayed, not from companion mistakes.

## Decisions already taken

| Topic | Decision |
|---|---|
| Scope | A dedicated 8v8 mode. No PvE; gamebot economy rules do not constrain this work. |
| Ruleset | DAoC 1.65: Classic and Shrouded Isles, Old Frontiers realm abilities, no RR5 class abilities. |
| Arena | A bounded box in Emain Macha (zone 214, region 200), placed inside one connected area of the zone (see M2.1). It needs the navmesh rebuilt in 0.35, where the invisible walls block bots (see M0.3). |
| Characters | Level 50 and fully templated on demand, for the owner's characters and for bots. |
| Realm rank | Everyone starts at RR1 and earns realm points from kills. Scrims may override rank so ability AI can be tested early. |
| Groups | Meta groups per realm (below, set by the owner on 2026-11-16). |
| Companions | Act on their own by role and adapt to the owner's class. Calls can come from a bot shot-caller or from the owner's macros. |

**Meta groups**

| Realm | Main group | Alternates |
|---|---|---|
| Albion | 2 Clerics, Sorcerer, Minstrel, Theurgist, Paladin, Armsman, Cabalist | Mercenary for the Armsman; Wizard for the Cabalist |
| Midgard | 2 Healers (Pacification, Mending), Shaman, Skald, Runemaster, Savages, Berserker, Warrior | A second Savage for the Berserker; Spiritmaster for the Runemaster |
| Hibernia | 2 Druids, Bard, Hero, 2 Blademasters, Eldritch, Enchanter | Warden or Champion for a Blademaster; Mentalist for the Enchanter |

The main groups use 20 classes. Those come first everywhere below; alternates follow.

## Where we start

| Area | Today |
|---|---|
| Decisions | Each bot decides alone from a monster-style threat list (`BotBrain.cs`, 5,300 lines). No group plan. |
| Targeting | Gamebot warbands spread their targets on purpose; companions copy the leader's target. No focus fire. |
| Roles | From class only (Tank, Support, Damage). Spec is ignored. |
| Spells and styles | Spell picked at random among valid ones. Positional styles excluded. Guard, Intercept, Protect, slam never used. |
| Realm abilities | Only Purge, Ignore Pain, Second Wind, First Aid, and only for gamebots. No Speed of Sound, sprint or Quickcast. |
| Positioning | Formation ends when combat starts. No backline, kiting or retreat. |
| Sight | Warbands know where enemy bots are across the frontier. Line of sight is a navmesh raycast that hills and trees never block. Bot-vs-bot casts skip the engine's sight check. |
| Setup | Spec is random and cannot be chosen. No way to set rank. Level-50 companions get random generated gear, not capped templates. |
| Measurement | No record of decisions or actions. No test runs a real fight. Game time is wall-clock only. |
| Already good | Heal triage, cure mez, PvP mez with shared reservations, combat resurrection budget, 2,790 unit tests, small testable policy classes. |

## Working rules

- Ask before starting a server or copying a build into an install.
- Scrims run on a copy of an install with its own database and the gamebot population off, never
  on a played save.
- Scrim bots are temporary: never written to the owner's roster, never left in the world after a match.
- New decision logic goes in small pure classes with unit tests, as the existing policies do.
- Each milestone ends with a scrim batch and an analyzer comparison against the previous build.

---

## M0. Workspace

- [x] **M0.1** (S) Two fresh installs of the current release (0.37) from the downloader
  (`Get-OfflineDAoC.ps1`): the play folder on `D:\`, and a scrim folder copied from it before first
  play, with its own database and the population off. The repository itself is source only and is
  not playable. Then a written deploy loop (build, test, back up, copy, start). *Done when* a fresh
  build runs in the scrim folder and the server tests pass.
  *Progress (2026-11-16):* the play folder `D:\OfflineDAoC` is at 0.37b and unplayed (launcher and
  server files match the release fingerprints; the patch log shows 0.37). The deploy loop is
  `source/server/tools/deploy_scrim.ps1`: a dry run by default, `-Apply` runs the tests, backs up
  and installs `GameServer.dll` in its three places, `-Rollback` undoes it. The scrim folder
  `D:\OfflineDAoC-scrim` is a copy of the unplayed game (37,550 files, 8.1 GB), and our build was
  installed there with all 2,915 tests passing. *Done 2026-11-16:* the owner started the scrim
  server; our build (0.38 development source on the 0.37 database) started in 18.7 seconds,
  loaded 326 commands and listens on port 10300, with no database schema errors. The scrim world
  has no gamebots (hard cap 0). The log's startup errors are data gaps (spells missing from the
  potion and item-effect lines, NPC templates, 35 doors not matched to the navmesh, a duplicate
  `/tc` alias); they were not compared with the released build's log.
- [x] **M0.2** (S) Home for the new code (`GameServer/bots/pvp/`) and its tests
  (`Tests/UnitTests/UT_Pvp*.cs`); `AGENTS.md` gains a short section on the 8v8 focus and a pointer
  to this file. Done 2026-11-16 on branch `pvp-8v8`. Baseline before any change: build clean,
  2,805 server tests pass.

- [x] **M0.3** (S) Navmesh wall check: a tool that samples every invisible wall of a zone (the
  client's `bound.csv`) and reports how much of it the installed navmesh lets bots walk across.
  Run it on Emain Macha in the scrim install. Measured 2026-11-16 on an old install's mesh from
  before the 0.35 wall fix (since removed): 61% of the wall length open. *Done when* the scrim install's mesh shows
  about 0% open, or the zone has been rebuilt with `OpenDAoC-BuildNav` until it does.
  *Done 2026-11-16:* `source/server/tools/navmesh_splice/wallcheck.py` reports 0.0% open in the
  new install for Emain Macha (0 of 11,560 samples), Breifine, Cruachan Gorge and Mount Collory,
  and reproduces the 61% on the old mesh. The installed Emain mesh matches the released file.

**Exit check:** a code change reaches the scrim install in one documented pass, and the arena zone's
walls stop bots. **Passed 2026-11-16.**

## M1. Level 50 and templated, on demand

- [x] **M1.1** (M) Build catalogue: a data file with, per class, one or more named builds (spec
  lines, realm ability purchase order, weapon and armour types). Main-group classes first.
  *Progress:* `bots/pvp/PvpBuildCatalog.cs` has 29 builds (the 20 main-group classes and the
  alternates) and the three main groups; tests check each build against its class's spec points.
  *Closed 2026-11-16.* Realm abilities follow the existing plan per archetype (fighter, caster,
  healer, archer) as the bot's rank allows; armour type comes from the class. The owner reviewed
  the buffer specs; the other specs stay open to correction in the one-line-per-build table.
  A per-build realm ability order is deferred to M5, where active abilities get used.
- [x] **M1.2** (S) Bots created with a chosen build instead of a random spec.
  *Done 2026-11-16.* An ownerless level-50 8v8 bot is built from a catalogue entry
  (`new GameBot(PvpBuild)`), and the running scrim server audits all 29 builds on request
  (`audit-builds`): every one trains exactly its planned lines. The audit found that every bot got
  its specialization-line spells as if it had 38 in each line, whatever its spec (a rule of thumb
  of 75% of level). 8v8 bots now get the spells of the level they trained; 23 of the 29 builds
  changed spell ranks. Example: the heal Cleric's group heal went from the level-36 to the level-46
  spell and its resurrection from level 24 to level 40.
- **Tools added for this and for scrims:** `source/tools/scrim_server.py` (start, stop, status of
  the scrim server without the launcher) and `source/tools/pvp_request.py` (send a request to the
  server's 8v8 control channel, `bots/pvp/PvpControlChannel.cs`: `ping`, `audit-builds`,
  `stop-server`). The owner has agreed that the scrim server may be stopped and started as needed.
- [x] **M1.3** (L) Template generator: a full set of level-50 gear that reaches the stat, resist,
  hit-point and skill caps for a build, each item within 1.65 spellcrafting limits. *Done when* a
  unit test per build asserts the caps.
  *Done 2026-11-16* (`bots/pvp/PvpTemplateGenerator.cs`, `PvpTemplateOutfit.cs`). Every item follows
  this server's spellcrafting rules: four bonuses of real gem sizes, 32 imbue points plus 5 of
  overcharge on level-51, 100% quality pieces. Tests assert, for all 29 builds, that the first
  needs (three main stats, 200 hit points, +11 in the two main lines) are capped and that every
  resist is at least 15%. Checked on the scrim server: the bonuses worn equal the bonuses the
  server counts, for every build. Typical result: the Hero has 190 Strength, 168 Constitution,
  1,619 hit points and 26% in every resist before racial bonuses; casters, who carry one crafted
  weapon instead of two, end at 18 to 26%.
  Three faults found and fixed for 8v8 bots on the way: bots took no resists from their gear at
  all; a fighter handed a bow kept it as its active weapon, which switched off the bonuses on its
  real weapons (8v8 fighters no longer get a bow); and bonuses were landing on weapons not in hand.
  Assumptions to revisit: jewelry is allowed 24 imbue points per piece (crafted pieces hold 37);
  items carry no procs or charges; race is random per bot, so base stats vary from run to run.
- [ ] **M1.4** (M) `/ready` for the owner's own character: level 50, respec to a chosen build,
  equip the template. Realm rank untouched.
  *Not started.* It changes a real player character (level, specs, inventory), so it has to be
  built and then tried in a game client with the owner; nothing on the scrim server can check it.
- [ ] **M1.5** (M) Realm rank for 8v8 bots and companions: stored realm points, earned from kills,
  realm abilities bought in the build's order. A scrim setting overrides rank.
  *Progress 2026-11-16:* a rank can be set when a group spawns (`RealmRank=5` on `spawn-group`,
  `/8v8 group 5` in game), and the bot gets the realm abilities that rank pays for; the default is
  rank 1. Checked on the scrim server at rank 5 (ten or so abilities each) and rank 1 (one).
  Still to do: realm points earned from kills and kept between sessions. That needs the lasting
  roster of M9.1 and real fights, so it moves there.
- [x] **M1.6** (S) Group builder: one call produces a named meta group of eight, or seven built
  around the owner's class.
  *Progress:* `spawn-group` on the control channel puts a named group of eight in the world,
  grouped, at a point (`bots/pvp/PvpArena.cs`); `arena-status` reports each bot and its buffs;
  `arena-clear` removes them. 8v8 bots now think with no human nearby (other bots sleep then).
  *Done 2026-11-16:* each class has one fixed race; `/8v8 group [rank]` in game spawns the realm's
  main group without the player's own class, joins it to the player and has the buffbot buff all
  eight; `/8v8 clear` removes them. The control-channel path is checked on the scrim server. The
  in-game command builds and is registered but has not been tried in a game client.
- [x] **M1.7** (M) Group buffing without a buffbot. The owner's standard is the second-best
  ("yellow") Str/Con and Dex/Qui on every member. Each group has one buffer specced for that
  (Cleric 42 Enhancement, Druid 42 Nurture; the Shaman at 47 Augmentation has the best), and a
  plan decides who buffs whom: a buff is only cast by the member with the group's strongest
  version, and concentration goes to the most needed buffs first (casters: Dex/Qui and acuity;
  melee: Str/Con and Dex/Qui; then the other of the two for everyone). Today each bot buffs on its own, a random member at a time, and a
  weaker buff that lands first blocks the strong one. *Progress:* the planner and its tests exist
  (`bots/pvp/PvpBuffPlan.cs`). Still to do: feed it the bots' real spells and make 8v8 bots cast
  from the plan; check in the scrim install that nobody holds a weak specialization buff.
  Measured on the scrim server with untemplated gear: concentration is 388 on the buff Cleric,
  420 on the buff Druid and 375 on the Shaman, so Dex/Qui and Str/Con on all eight (416 for
  Albion) does not quite fit yet. Concentration grows with the casting stat, so re-measure once
  templates (M1.3) are in.
  *Done when* a freshly buffed group shows only top-value specialization buffs, on the right
  members, with the shortfall (if any) reported.
  *Done 2026-11-16* (`bots/pvp/BotBrain.PvpBuffs.cs`). Checked on the scrim server with all three
  groups, two minutes after spawning. Albion: all eight hold Dex/Qui 63 from the buff Cleric, the
  melee and Clerics Str/Con 57, the casters acuity 52; the heal Cleric gave everyone the baseline
  buffs and never cast its weaker Dex/Qui. Hibernia: Dex/Qui 61 and Str/Con 55 on all but the two
  casters' Str/Con. Midgard: Dex/Qui 70 on seven, Str/Con 69 on the melee. The buffers end at 404
  of 416, 428 of 430 and 415 of 416 concentration, and the plan lists what did not fit.
  *Owner's rules, 2026-11-16:* every character must hold Str/Con and Dex/Qui; nobody is given
  base armour; if the group's own concentration cannot cover that, a buffbot spawns beside the
  group and tops everyone up, and after a death in the field the group rebuffs the member itself.
  The first two rules are in the planner.
  *Buffbot built 2026-11-16* (`PvpArena.SpawnBuffbot`, builds `buffbot-cleric`, `buffbot-shaman`,
  `buffbot-druid`): one spawns with each group, cannot be targeted, has no concentration limit and
  no range limit on its buffs, and buffs all eight at once. Checked on the scrim server: every
  member of all three groups holds every base and specialization buff its realm's buffer has
  (Albion Dex/Qui 75 and Str/Con 69, Midgard 70 and 69, Hibernia 73 and 67), and the group's own
  buffers keep nearly all their concentration. Not yet checked: that the buffs hold beyond 5,000
  units, and the rebuff of a member who died. Gaps by realm, since a buffbot only has its class's
  buffs: Albion gets no haste or endurance regeneration from it, Midgard no haste or
  specialization armour, Hibernia no endurance regeneration or specialization armour.
  *Closed 2026-11-16.* The plan now keeps what is already cast and assigns only what is missing,
  from each buffer's free concentration. Checked on the scrim server: a Hero stripped of all its
  buffs (as a death does) was rebuffed by the two Druids within a minute with Str/Con, Dex/Qui,
  haste and the three base stats, and the plan reported nothing unfilled. The buffbot's buffs were
  also confirmed active on all 24 characters 9,000 units from their buffbots. Open refinements:
  buffs outside the plan (group resists, power regeneration) still land first-come, so a weaker
  one can be cast and sit disabled; dropping a tier when the best does not fit is not built.
  *Reopened earlier on 2026-11-16* after reading all three groups once buffing had settled (about three to
  four minutes after spawning). The specialization buffs are right, but:
  - **The plan forgets what is already cast.** It is rebuilt every five seconds from empty
    concentration pools, so the baseline buffs move between the two buffers from one plan to the
    next and some are never cast. Hibernia ended with each member holding only one or two of
    Constitution, Dexterity and base armour, with 175 concentration unused on the heal Druid. The
    plan must keep the buffs already in place, count the concentration they hold, and only assign
    what is missing. This also stops it reserving concentration for the casters' own armour buff.
  - **Midgard has no endurance regeneration.** The Shaman spends all 416 concentration on stat
    buffs before its endurance buff. Endurance for the melee has to rank above the last stat buffs.
  - **The best tier can cost too much:** the Shaman's left the Warrior without Dex/Qui. When the
    best version cannot cover everyone, drop a tier for all (the owner's "yellow is fine").
  - **Albion's melee get no armour buff from the plan** (base or specialization); the Paladin's
    chant covers it only while that chant is the one playing.
  - **Buffs outside the plan still follow the old first-come rule:** both Druids' group resists
    land (8% and 16%), and two Midgard members hold the Mending Healer's weaker power regeneration.

**Exit check:** one command yields a capped, specced, correctly buffed level-50 group at a chosen
rank, and the cap tests pass for all 20 main-group classes.

## M2. Arena and scrim loop

- [ ] **M2.1** (S) Arena file: box bounds in Emain Macha, two spawn areas, patrol waypoints. Size
  is a setting. With its walls in place the Emain mesh splits into two large areas that do not
  connect (about 64% and 33% of the zone). The zone centre is in the smaller one; the two frontier
  arrival points first proposed as lane ends are in the other. The box, both spawns and every
  waypoint must sit in one area: pick it from the map and prove it with a path check.
- [ ] **M2.2** (M) Match lifecycle: spawn both groups, buff, roam, contact, end on wipe or timeout,
  clean up, repeat.
- [ ] **M2.3** (S) `/scrim` command: start N matches between two named groups, show status, stop.
- [ ] **M2.4** (S) Simple roaming along the waypoints with no knowledge of the enemy's position.
  Good enough to produce contact; M8 replaces it.
- [ ] **M2.5** (M) Several arenas at once in different boxes, since game time cannot be sped up.

**Exit check:** 20 matches run unattended with no stuck bot, no leftover bot and no server error.

## M3. Recorder, analyzer and baseline

- [ ] **M3.1** (S) Trace format, versioned: match header (groups, builds, ranks, code version),
  events, decisions, snapshots.
- [ ] **M3.2** (M) Event capture: attacks from both sides (a new attacker-side hook is needed),
  casts started, finished and interrupted, crowd control applied, broken and expired, heals,
  realm abilities, deaths.
- [ ] **M3.3** (S) Snapshots twice a second: position, health, power, endurance, control state, target.
- [ ] **M3.4** (M) Thread-safe writer, one JSONL file per match. Brains tick in parallel, so it
  must not block them.
- [ ] **M3.5** (L) Analyzer: a per-match report, an aggregate over a batch, and an A/B comparison
  of two builds with the scorecard metrics.
- [ ] **M3.6** (S) Baseline: at least 30 matches per realm pairing on today's AI. Fill the
  scorecard's baseline column and write the ranked failure list.

**Exit check:** the baseline report exists, and one command compares two builds.

## M4. Fair fights

- [ ] **M4.1** (L) Parity audit. `GameBot` is an NPC type, so compare a bot with a player of the
  same class, spec and gear: to-hit, defence, damage, resists, criticals, spell variance, control
  durations and immunity timers. Fix every difference. *Done when* parity unit tests pass.
- [ ] **M4.2** (M) Sight that respects terrain: a height check so hills block sight, used for both
  casting and spotting.
- [ ] **M4.3** (S) Bot-vs-bot casts go through a sight check in the engine.
- [ ] **M4.4** (L) Missing mechanics made usable by bots, each with a test that triggers it: Guard,
  Intercept, Protect, positional and reactive styles, slam, sprint, Quickcast, and the Old
  Frontiers active realm abilities, Speed of Sound included.
- [ ] **M4.5** (M) Interrupt parity: bots interrupt and are interrupted exactly as players are.
- [ ] **M4.6** (S) Run a new baseline, since these fixes change the numbers.

**Exit check:** parity tests pass, and no cast in a recorded scrim goes through terrain.

## M5. Squad brain: sight, roles, focus fire

- [ ] **M5.1** (M) A dedicated brain class for 8v8 bots and a squad object with a shared
  blackboard, ticked once per group before its members think.
- [ ] **M5.2** (M) Perception: enemies are known only when in view range and in sight; stealth
  respected; last known positions remembered and aged; enemy class and current cast visible.
- [ ] **M5.3** (M) Roles from class and spec: main tank, peeler, assist damage, caster damage,
  primary control, healer, speed and support.
- [ ] **M5.4** (L) Class kits: a data table tagging each spell, style and ability with its
  function (area mez, stun, root, snare, disease, interrupt, guard and so on). Main-group classes
  first. Replaces the scattered class switches for 8v8 bots.
- [ ] **M5.5** (M) Kill-target calling: one target per group, chosen by how killable it is, with
  rules for when to swap.
- [ ] **M5.6** (M) Heal assignment across the group's healers, on top of the existing triage, so
  two healers do not land on the same patient while another dies.
- [ ] **M5.7** (S) Every decision written to the recorder with its options, scores and reason.

**Exit check:** wins at least 70% of mirrored matches against the M4 baseline; focus-fire
coherence meets its target.

## M6. Control, interrupts and protection

- [ ] **M6.1** (L) Control plan: an area mez opener, assignments per controller, immunity timers
  tracked, re-mez, stuns, roots and snares used on purpose, never breaking the group's own mez.
- [ ] **M6.2** (M) Interrupt duty: each enemy healer and caster gets an assigned interrupter, using
  instants, pets or melee.
- [ ] **M6.3** (M) Protection: guard pairs set at fight start, slam and snare on enemies attacking
  the backline, cure mez and Purge used with judgement.
- [ ] **M6.4** (L) Scored action choice for every role in place of the random pick: debuffs before
  damage, openers, burst on the called target, power and endurance budgeting.
- [ ] **M6.5** (M) Upkeep during fights: bladeturn chants, endurance and speed songs, resists,
  and full buffing before a fight.

**Exit check:** wins at least 70% of mirrored matches against the M5 build; first-mez time and
enemy-healer free-cast share meet their targets.

## M7. Movement and positioning

- [ ] **M7.1** (M) Range bands per role and a backline anchor: healers and casters hold distance
  from enemy melee while staying in range of their group.
- [ ] **M7.2** (M) Reactions to pressure: kite when attacked in melee, sprint, step out of area
  effects, move to regain sight or range.
- [ ] **M7.3** (M) Melee movement: stick to the target, reach the position a positional style
  needs, stop chasing past the group's support range.
- [ ] **M7.4** (M) Cohesion: formation while roaming, a tight group on approach, regrouping during
  a fight.

**Exit check:** healer-distance and overextension targets met; wins at least 65% against the M6 build.

## M8. Group tactics and adaptation

- [ ] **M8.1** (M) Fight-state estimate: winning, even or losing, from numbers alive, health,
  power, control and cooldowns on both sides.
- [ ] **M8.2** (L) Group calls: push, hold, retreat with Speed of Sound, regroup, resurrect and
  rebuff, turn on enemies who overextend.
- [ ] **M8.3** (M) Cooldown timing: Purge held for the mez that matters, defensive abilities under
  focus, offensive abilities with the burst.
- [ ] **M8.4** (M) Roaming: scouting, choosing to engage or avoid, choosing the approach (charge,
  open from range, wait at a chokepoint).
- [ ] **M8.5** (M) Plans per opponent: against a caster group, against a melee group, against each
  realm's control.

**Exit check:** meets definition-of-done point 1 (90% against the original baseline); alternate
groups play correctly as well.

## M9. The owner's group

- [ ] **M9.1** (M) A lasting companion roster built around the owner's class, earning realm points
  with the owner.
- [ ] **M9.2** (M) Bot shot-caller: calls announced in group chat (assist target, mez landed,
  retreat), with an assist key for the owner.
- [ ] **M9.3** (M) Owner's calls: `/call` macros for target, push, retreat, control that one,
  regroup. The squad brain treats them as its own decisions.
- [ ] **M9.4** (M) `/8v8` play session: pick the enemy realm and group, enemy roams the arena,
  rematch after each fight. The enemy roster keeps its own realm points.
- [ ] **M9.5** (S) Play sessions are recorded and analyzed like scrims.
- [ ] **M9.6** (S) Check both clients: the original client and the new client's target and assist
  buttons.

**Exit check:** a full session from the launcher: ready a character, form the group, roam, fight,
rematch, with no GM commands.

## M10. Difficulty and tuning

- [ ] **M10.1** (M) Human-like limits as settings: reaction delay, attention, mistake rate.
- [ ] **M10.2** (S) Named difficulty levels for the enemy group and, separately, for companions.
- [ ] **M10.3** (M) Regression gate: each new AI build plays a fixed scrim set against the
  previous one, and a drop blocks it.
- [ ] **M10.4** (M) Tuning rounds with the owner: 20 fights, review the analyzer's failure list,
  fix, repeat.
- [ ] **M10.5** (S) Player guide for the 8v8 mode and an update of the developer docs.

**Exit check:** all four points of the definition of done.

---

## Scorecard

Targets are first guesses and will be reset once the M3 baseline exists.

| Metric | What it shows | Baseline (M3) | Target | Current |
|---|---|---|---|---|
| Win rate against the baseline AI, mirrored | Overall strength | 50% by definition | 90% or more | |
| Focus-fire coherence: share of group damage on the called target | Assist discipline | | 70% or more | |
| Time from contact to first kill | Burst and coordination | | Lower than baseline | |
| Time from contact to first area mez landing | Control opener | | Under 3 s | |
| Mezzes broken by the caster's own group | Control discipline | | Under 5% | |
| Share of the fight enemy healers cast freely | Interrupt duty | | Under 40% | |
| Healer time within melee range of an enemy | Positioning | | Under 15% | |
| Deaths while controlled with Purge available | Cooldown use | | 0 | |
| Idle time per bot while able to act | Wasted turns | | Under 5% | |
| Deaths beyond the group's heal range | Overextension | | Under 10% of deaths | |
| Stuck or pathing failures per match | Navigation | | 0 | |

## Open questions

- **Match throughput:** time cannot be accelerated. If parallel arenas (M2.5) are not enough,
  adding a time scale to the game loop is the fallback, and it touches a lot of code.
- **Template generator:** how much of the existing item generator can be reused is unchecked.
- **Battlegrounds and other zones:** only the Old Frontier zones have navmeshes. A second arena
  outside them would need a mesh built.
- **Sluaghbinder:** left out of the meta groups unless the owner wants it in.

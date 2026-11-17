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

## Measuring progress (owner, 2026-11-16)

No baseline against the old brain. The squad brain replaces it, so the old brain's numbers are not
a target worth chasing. Progress is measured by squad brain versions against each other: each new
version fights the previous one (same groups, ends swapped), and the recorder and analyzer show
which behaviours changed. The two matches recorded on 2026-11-16 stay as the only record of the
old brain. This replaces M3.6 and the "90% against the baseline" test in the definition of done;
the equivalent test is that each version beats the one before it, until the owner finds the fights
challenging.

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

*Progress 2026-11-16 (branch `pvp-8v8-m2`, `bots/pvp/PvpScrim.cs`):* the arena is the south-east
basin of Emain Macha, open ground about 26,000 by 18,000 units inside the playable part of the
mesh, spawns 21,500 apart (M2.1). `scrim-start A=alb-main B=mid-main Matches=N` spawns both groups
with their buffbots, marches each on the other, has every idle bot attack the nearest enemy it
sees within 3,000 units, ends a match on a wipe or after 600 seconds, clears and starts the next,
swapping ends each match (M2.2 to M2.4, baseline version). First run, Albion against Midgard:
contact after 37 seconds, a real fight (damage, heals, stuns, snares), and no death in five
minutes: 11 to 15 of the 16 bots are mesmerized at any moment, nobody attacks a mesmerized
target, and the healers undo the rest. That stalemate is the baseline the recorder has to show.
Still to do: a complete batch to the time limit, parallel arenas (M2.5), the 20-match exit check.

*Progress 2026-11-17:* groups no longer walk at each other's position. Each patrols a route (arena
middle, far end, middle, own end) and goes for enemies only once a member sees one within 3,000
units (M2.4). The first 20-match run met nobody: the waypoints had no ground height, so no path
was found; they now take their height from the navmesh floor. Exit check, Albion against Midgard,
squad brain on both, 180-second limit, run as four batches of five with a server restart between
batches: 20 of 20 matches completed, contact at 32 to 35 seconds in every one, no server error, no
bot left in the world. Results: Albion 7, Midgard 2, 11 draws, every match on the time limit with
at most three deaths (a brain problem, M5 and M6). The earlier no-contact run also completed 20
matches in one server without error. Server memory: 5.1 to 5.4 GB after the first match of a
batch, then about 0.1 GB more per match; worth a look before long runs or parallel arenas.
Scrims are started through the control channel (`scrim-start`, `scrim-status`, `scrim-stop`); the
in-game `/scrim` command waits with the rest of the human-side work. M2.5 is not built.

*Correction, 2026-11-17: dead bots were releasing.* Every result recorded before this date is
wrong in its winner and its survivor count. A dead 8v8 bot ran the ordinary bot release: after 20
seconds (90 with a living healer in the group) it stood up alive at a bind point far from the
arena, the same one for both teams, where released bots of the two teams fought again. So no team
could be wiped, every match ran to the time limit, and "alive at the limit" counted released bots.
One match recorded as an 8 to 8 draw was a wipe of Midgard 103 seconds in. Damage, heal and mez
figures of those runs include the second fight at the bind point. Fixed: an 8v8 bot stays down
where it fell until a groupmate raises it or the match clears the arena
(`GameBot.HandleDeathRecovery`). The analyzer now counts bots raised per match and prints a
warning when a dead bot stands up away from its corpse.

*Ready check, 2026-11-17 (owner's request):* the fixed five seconds of buffing let groups walk
into the fight still casting their own buffs, with casters at half power (the Shaman at 196 of
396 at contact). A match now starts when both groups are ready: a member is ready once it has
gone 8 seconds without casting a buff it had not cast yet (the same spell on the same target
counts once, so a song or chant recast in a loop keeps nobody waiting); the wait is at least 5
seconds and at most 120, and a timeout names who was still buffing. At the start everyone is set
to full health, power and endurance, as a group that rested after buffing, and the match clock
starts there, so buffing is not part of the match. The recorder writes a `ready` line and the
analyzer shows buffing time and power at contact per match. First run, Midgard against Hibernia,
two matches: buffing took 106 and 98 seconds, power at contact 100% (was about 75%), and deaths
rose from 2 to 5 a match to 10 and 9, with one win each. Buffing took about 100 seconds because
members buffed each other: group resist buffs (each Druid three times, overwriting the other's),
damage add and damage shield on every member, haste, power regeneration.

*Own buffs only before the fight, 2026-11-17 (owner's rule):* from spawn until first contact a
member casts only what is its own: buffs on itself, its pet, its songs and chants. The buffbot
does the rest; buffing others (the field rebuff included) starts with the fight
(`BotBrain.PvpSelfBuffsOnly`). Buffing fell from about 100 seconds to 41, and a two-match check
from about 13 minutes to 8. Not given before the fight any more, because the buffbot does not
have them: group resist buffs, the Healer's haste and power regeneration, damage add and damage
shield on others. Still open: the old brain casts these during the fight (damage shield 20 times
by the two Druids in two matches).

- [x] **M2.1** (S) Arena file: box bounds in Emain Macha, two spawn areas, patrol waypoints. Size
  is a setting. With its walls in place the Emain mesh splits into two large areas that do not
  connect (about 64% and 33% of the zone). The zone centre is in the smaller one; the two frontier
  arrival points first proposed as lane ends are in the other. The box, both spawns and every
  waypoint must sit in one area: pick it from the map and prove it with a path check.
- [x] **M2.2** (M) Match lifecycle: spawn both groups, buff, roam, contact, end on wipe or timeout,
  clean up, repeat.
- [x] **M2.3** (S) `/scrim` command: start N matches between two named groups, show status, stop.
  Done as control-channel commands; no in-game command yet.
- [x] **M2.4** (S) Simple roaming along the waypoints with no knowledge of the enemy's position.
  Good enough to produce contact; M8 replaces it.
- [ ] **M2.5** (M) Several arenas at once in different boxes, since game time cannot be sped up.

**Exit check:** 20 matches run unattended with no stuck bot, no leftover bot and no server error.
Passed 2026-11-17 (four batches of five, see the progress note).

## M3. Recorder, analyzer and baseline

*Progress 2026-11-16:* the recorder (`bots/pvp/PvpRecorder.cs`) writes one JSON-lines file per
match under `runtime/server/logs/scrims`: a header, every attack (result, damage, spell or
style), every finished cast (spell, type, power, target), deaths, and a once-a-second snapshot of
each bot (position, health, power, mezzed, stunned, immunities, casting, target, brain state).
The analyzer (`source/tools/pvp_analyze.py`) reports per batch how matches ended and, per build,
damage dealt and taken, share of the fight spent mezzed, stunned, idle, casting or out of power,
power left, and the spell types cast. Match limit is now a setting (default 300 seconds).
First recorded batch, Albion against Midgard at rank 1, two matches of 180 seconds: both draws,
no death, each side dealt about 14,800 damage in total over the two fights (about 50 a second
for a whole group). Midgard spent 34% of the fight mezzed, Albion 11%; nobody was mezzed while
holding mez immunity. Fighters dealt 12 a second or less; the Clerics were idle 35 to 38% of the
fight and cast resist and regeneration buffs during it; the Paladin recast its endurance chant
146 times; the Cabalist and Theurgist had no power for half the fight.
Not yet recorded: the reason behind each decision (the legacy brain does not expose it; the squad
brain of M5 will), heals as amounts, mez breaks. Still to do: M3.5's build-to-build comparison,
and the baseline batch of 30 matches per pairing (M3.6).

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

*Found early, 2026-11-17 (part of M4.1):* 8v8 bots were mezzed and stunned by the NPC rules,
because a bot is an NPC type: no minute of immunity afterwards (repeat mezzes that shrink
instead), and every mez resisted below 75% health. They now follow the player rules
(`GameBot.UsesPlayerCrowdControlRules`): immunity after a mez or stun, no second one while the
first runs, no health-based resist. Albion against Midgard, two matches: Midgard mezzed 41% of
the fight, from 62 to 73% before; the Sorcerer's mez casts that were accepted and never began
(83 in one match) stopped. The recorder's "mezzed while immune" figure meant nothing before this.

*Audit, 2026-11-17 (M4.1):* the engine has about 1,100 "is this a player?" tests in its combat
code, and a bot, being an NPC type, falls on the monster side of each unless the code says
otherwise. Three read-only audits (melee; spells; pools, effects and movement) went through them.
One test now answers the question for 8v8 bots, `GameBot.UsesPlayerRules`, and these places use it
or an equivalent. All were found by reading code; what a fight shows is noted under each run.
Fixed, 8v8 bots only:
- **Armor.** The piece that was hit decides armor factor and absorb, with the player caps and the
  extra 20 armor factor; before, every bot had the same level-based armor and 27% absorb raised by
  its buffs, a cloth caster included. Template armor now carries its class's absorb.
- **Swing speed** reads Quickness with items, buffs and debuffs (it read the bare base stat).
- **Interrupts** last as long as on a player (they lasted 2.5 seconds longer); after its own swing
  a bot cannot cast for 3 seconds, as a player.
- **Item +skill** counts, in weapon and magic lines (it counted for nothing).
- **Dual wield** is recognised: the target's evade and block are halved.
- **No limping** below a third of health.
- **Miss chance** by the player formula; template gear carries the to-hit bonus on both sides.
- **Critical hits:** 10% base chance in melee and for list casters; a critical on a bot adds at
  most 50%.
- **A missed, blocked, parried or evaded style** costs half its endurance.
- **Control durations** (mez, stun, root, snare, nearsight) are shortened by the target's resists.
- **Spell damage variance** comes from the bot's trained level in the spell's own line (it was the
  monster 60 to 100%); the same for buff and debuff strength and for heal variance (bot heals
  always landed at the maximum).
- **A damage-over-time spell** interrupts once, not on every tick.
- **Focus:** the caster staff focuses every line at 50 and the bot pays 80% of a spell's power
  (it paid 120%).
- **Resist debuffs** below zero are halved; **to-hit** uses the spell's level; **a non-damaging
  debuff** no longer breaks mez; **bolts** take the in-combat penalty and hit a piece of armor;
  **power regeneration** is halved under half power for list casters.
Found and left for later:
- Melee reach is 200 for a bot and 128 to 192 for a player (the follow distances depend on it).
- Resurrection: a raised bot has no sickness, no five seconds of immunity, and comes back at full
  health in most cases.
- Sprint does nothing for a bot; Quickcast is off; a caster bot cannot fire an instant during a
  cast; shared recast timers are not applied.
- Player-led bots have a fast rest that an ownerless enemy group does not.
- Savage multi-target styles hit one target; shield styles swing the main-hand weapon.
- Guard, Intercept and Protect are not used by bots at all (M4.4, M6.3).
- No line-of-sight check during a bot's cast (M4.2, M4.3); no facing check on a bot's swing.
- Server properties in the live database were not read; the code defaults were assumed.

*Keep guards in the fight, found 2026-11-17:* the arena is in a frontier zone with Albion keeps,
and their guards shot at Midgard and Hibernia bots: a Master Ranger for about 7,600 a shot, up to
53,000 damage in one two-match batch, and some guard damage in nearly every batch recorded that
day. Results before this date between Albion and the other realms are not to be trusted. Guards
and monsters now leave 8v8 bots alone and the reverse (`GameBot.IsWorldAgainstPvpBot`, in the
attack-permission rule), and the analyzer prints a warning naming any damage from outside the two
groups.
*First clean run with the parity fixes, Albion against Midgard, two matches:* no outside damage;
damage dealt and taken now add up between the teams. Albion won both on the time limit, 7 to 4 and
8 to 5, with 9 and 11 deaths. Fighters now do the damage: Armsman 11,000, Berserker 8,600, Warrior
8,100 over two matches (the Armsman did about 5,000 before). Midgard is mezzed 25% of the fight
and Albion 10%. The match header records each bot's armor factor, absorb, Quickness, swing time
and critical chances: cloth 50 and 0%, studded 19%, chain 27%, plate 34%, 10% melee critical for
all and 10% spell critical for list casters. Still open: casts accepted and never begun (24 for
the Sorcerer in this run), cause not found.

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

*Started 2026-11-16 with melee styles* (`bots/pvp/PvpStylePolicy.cs`): 8v8 bots never use taunt or
detaunt styles, use a stun style only on a target that is neither stunned nor immune, may use
positional styles, and tanks no longer spend turns on protective taunts. One recorded match
confirms the styles changed (Armsman from Distract to Crippling Blow, Savage from Wild Call to
Kelgor's Bane, back styles on the Paladin and Minstrel). It did not raise damage: fighters still
swing 12 to 25 times in 145 seconds because they are mezzed, stunned or chasing a new target.
Damage check: the attack code already uses the player damage formula for bots
(`AttackComponent.CalculatePlayerDamage`), so per-hit damage was not changed.

*Kill target, first version, 2026-11-16* (`bots/pvp/PvpKillTarget.cs`, M5.5): one enemy per group
for every non-healer to attack, chosen by score (healers, then casters; wounded first; mezzed ones
left asleep; near before far) and kept until something scores clearly higher. A called target may
be attacked while mezzed, and the group's own mezzers leave it alone. `scrim-start` takes
`BrainA=squad` / `BrainB=squad`, so one side can run it against the other without.
First run, Albion with it against Midgard without, two matches of 240 seconds: the first deaths
(one, then two, all Midgard), Albion ahead 8 to 7 at the time limit both times. Albion's damage
landed on its called targets (the Mending Healer took 9,352, the Pacification Healer 6,165);
Midgard's was spread. Problems seen: Albion's Theurgist dealt no damage and its Sorcerer cast no
mez at all with a target assigned, and both stood idle about 30% of the fight, so the called
target is stalling the casters. Fix that next, then heal-through: nobody dies inside the limit.

*Roles, first version, 2026-11-16* (`bots/pvp/PvpRoles.cs`, M5.3). Owner's rule: the Sorcerer is
the main crowd controller and interrupter, the Theurgist keeps its pets on enemy healers and
casters, and both assist only when it is critical. Four roles by class: Assist (kills the called
target), Control (Sorcerer, Bard: no target forced, own crowd-control logic), Interrupt
(Theurgist: pointed at an unmezzed enemy healer or caster that is not the kill target, one casting
right now first), Heal. Control and Interrupt join the kill once the target is at 35% health or
less. Run after the change (Albion with squad brain, two matches): the stall is gone (Sorcerer
idle 4%, was 28%; Theurgist idle 5% and dealing damage again), results a draw and 8 to 7.
Still wrong: what they cast. The Sorcerer cast 27 stat debuffs and 5 mezzes and was out of power
67% of the fight; the Theurgist cast 18 snares. Spell choice is still the old random pick, so the
next piece is a priority list per role (M6.4): mez and stun for Control, pets for Interrupt.

*Owner's guidance, 2026-11-16, for the next version:*
- **No fixed thresholds.** When a controller joins the kill, or whether to wake a mezzed enemy,
  depends on the whole situation (who is interrupted, who is free, positions, immunities), not on
  one number such as 35% health. Decisions are to be weighed, not switched.
- **Mezzers.** Main: Sorcerer, Bard, Pacification Healer. Backup: Minstrel, the Mending Healer,
  and any other class with a mez (Spiritmaster arguable).
- **Interrupters.** Any caster can fill it, the Shaman included. Spamming a root at an enemy that
  is root-immune is still right when the point is to interrupt and lock it down.
- **"Do not break mez" is too strict.** Usually leave a mezzed enemy alone, but a mezzed healer
  that is out of position and can be killed by the assist train should be killed.
- **Interrupters, added.** The Skald is Midgard's prime interrupter, the Theurgist's equivalent:
  three instant spells made for it, runs between casters and swings as needed. The Minstrel is
  the same for Albion: two instant direct-damage shouts and a stun, a pet to park on a caster,
  and a mez.
- **Shaman: disease first.** It should be spamming disease, not only root: disease also halves
  the healing its target receives, on top of interrupting.
- **"Interrupted" means** the caster is busy running or kiting, or was hit, nuked or had a spell
  cast on it; after an interrupt it cannot cast for a few seconds (this server's setting is
  `SPELL_INTERRUPT_DURATION`, and the bot can read its own remaining time).

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
  Done for caster spell choice (2026-11-17, recorder format 2, line type `dec`); the kill-target
  call, crowd control, heals and melee are not recorded as decisions yet.

**Exit check:** wins at least 70% of mirrored matches against the M4 baseline; focus-fire
coherence meets its target.

## M6. Control, interrupts and protection

*Rotations, first version, 2026-11-16* (`bots/pvp/PvpRotation.cs`, M6.4): an ordered list per
caster class replaces the random spell pick when the squad brain is on. Owner's orders: Shaman
disease on everything then root as an interrupt (cast even on an immune target); Eldritch
nearsight on casters, disease, direct damage, and round again; Cabalist disease then damage on the
kill target (pet still to do); Theurgist pets on the enemy it keeps interrupted; Runemaster
nearsight then damage; other casters damage on the kill target.
First run, Midgard against Hibernia, squad brain on both, two matches of 240 seconds: seven
deaths in each (the earlier fights had none to three), Midgard ahead 8 to 3 and 8 to 2 at the
limit. The Runemaster followed its order (66 direct-damage casts, 10 nearsights). The Shaman cast
disease only 6 times against 46 heals. Hibernia's casters did not work: the Eldritch cast three
nearsights and no damage or disease, the Enchanter no damage at all and was in combat for 22
seconds of a 240-second match. Find out why before reading anything into the result.

*Owner, 2026-11-16: these orders are weighted tendencies, not hard rules.* On the incoming an
Eldritch will most probably nearsight, then maybe disease; after that it is mostly direct damage
on the called target, then back to nearsight, and so on. The first version above is a fixed
first-match list and is to become scored choices: each option gets a weight from the situation
(is a caster free and not nearsighted, is the kill target close to dead, how much power is left),
and the weights are what the recorder and analyzer help tune.

*Scored choice, 2026-11-17 (branch `pvp-8v8-m5`, `bots/pvp/PvpActionChoice.cs`, M6.4 and M5.7):*
the fixed lists are gone. On its turn a covered caster (Shaman, Eldritch, Cabalist, Runemaster,
Theurgist, Sorcerer, Enchanter, Wizard, Mentalist, Spiritmaster) scores every spell kind it has
(nearsight, disease, damage, root, Theurgist pet) on every enemy within 3,500 units and takes the
best. A score is a sum of weights: the class's leaning to that kind, then the situation (kill
target, its missing health, target casting and not interrupted, nobody keeping it interrupted,
already has the effect, mezzed, assigned target, the caster's power and the cast's cost, the walk
needed to get in range). An option out of range can still win, and then the bot walks. Below a
minimum score it casts nothing and keeps its power; covered casters no longer fall back to the
old random pick. The weights are data (`PvpWeights`): a `pvp-weights.json` beside the server
overrides any of them without a rebuild, and each match header records the weights it ran with.
Each decision is recorded with its five best options and scores; a turn with no cast records
what stood in the way (no such spell, no power, interrupted, recast timer, too far, score too
low). The analyzer prints both per build.
What the decision log found in its first three runs (Midgard against Hibernia, two matches each):
- The Enchanter dealt nothing because it stopped to cast self-buffs while the group walked on,
  was then given the kill target 8,000 units behind and stood there. A member farther than 3,500
  from every enemy now keeps marching. Enchanter damage 0 to about 3,700 over two matches.
- Casters sat out of range of a good option. With walking as part of the choice the Runemaster
  went from 6 damage casts to 49 and the Eldritch now does nearsight, disease and damage (7, 24
  and 20 casts; it had cast no damage before).
- Power is now the limit. The Enchanter had no power for 69% of the fight, the Eldritch 57%, the
  Shaman 55%; "no power" is the main reason for a turn without a cast. Causes to check next:
  pools are about 400 at level 50 (compare with a player, M4.1), power is spent before contact on
  self-buffs, and the old brain still recasts buffs in combat (damage add, resists, damage shield).
- The Shaman still casts few diseases (10) against 40 heals, and is interrupted or out of power
  for most of its free turns.
Results did not move: Midgard won all six matches on the time limit, 2 to 5 deaths a match.

*Mez and stun by score, 2026-11-17 (M6.1, first version):* for the main mezzers (Sorcerer, Bard,
Healer of either spec) single mez, area mez and stun are now options in the same scored choice,
recorded like the rest; other classes with a mez keep the old routine for now. An area mez
scores by how many awake, non-immune enemies it catches; the kill target and enemies already
asleep, immune or claimed by another mezzer are left out, and an area mez that also lands on the
kill target loses points. A stun gains on the kill target. A Healer's attack spell is no longer
cancelled by its support routine. Also: during a fight a member holds back buffs on others
(damage add, damage shield, resists); the group plan's rebuff of a member who died is still cast.
First run, Albion against Midgard, two matches: the Sorcerer, which used to cast stat debuffs,
opened with an area mez that caught 7 of 8 and cast 19 mezzes; Midgard was mezzed 64% of the
fight and Albion won both 8 to 5 (it had been 7 wins, 2 losses and 11 draws at one death or so).
Open, from the decision log:
- Midgard's Healers are the kill target and are interrupted on nearly every free turn (37 of 37
  for the Pacification Healer), so they neither mez nor cure mez: this is what guard, peel and
  interrupt duty (M6.2, M6.3) are for. Cure mez is still the old first-in-group-order routine.
- A mez was picked several times within a second before a cast completed (the Sorcerer made 92
  area-mez decisions for 19 finished mezzes over two matches). Two causes, both fixed: the match
  loop ordered a member with no attack target to walk in the middle of its casts, and the engine
  refused repeat mezzes under the NPC immunity rules (see M4). A decision now records when the
  previous cast was lost; the Runemaster still loses about ten a match, cause not found.
- Bots walk in as a clump, which is why one area mez catches seven (formation, M7.4).
- The Paladin recast its endurance chant 141 times (old brain).

*Starting point on the clean setup, 2026-11-17* (parity fixes in, guards out, two matches per
pairing, 180-second limit, squad brain on both sides; every match ended on the time limit):
| Pairing | Result | Deaths per match | Mezzed |
|---|---|---|---|
| Albion against Midgard | Albion 2 to 0 (7/4, 8/5) | 9, 11 | Albion 10%, Midgard 25% |
| Midgard against Hibernia | 1 each (8/5, 6/8) | 9, 9 | Midgard 16%, Hibernia 18% |
| Albion against Hibernia | 1 each (8/5, 3/8) | 9, 13 | Albion 11%, Hibernia 23% |
Two matches are a small sample: three later runs of Albion against Midgard on nearly the same
code gave 2 to 0, 1 each and 2 to 0.
**These results are not valid** (dead bots were releasing, see the correction under M2). Counted
from the death records instead, with a death as final: Albion against Midgard 1 each, then 2 to 0;
Midgard against Hibernia 1 each; Albion against Hibernia 1 each; all eight matches were wipes, 92
to 161 seconds after the ready check.

*Ten matches per pairing, 2026-11-17* (same code plus area mez over a sleeper, 180-second limit,
run before the release fix, so read from the death records with a death as final):
| Pairing | Result | How |
|---|---|---|
| Albion against Midgard | Albion 10 to 0 | ten wipes, 103 to 133 seconds; Albion lost 0 to 3 a match |
| Albion against Hibernia | Albion 10 to 0 | nine wipes, 102 to 151 seconds; one on losses at the limit |
| Midgard against Hibernia | Midgard 4, Hibernia 5, 1 draw | nine wipes, 74 to 126 seconds |
Albion's opponents were mezzed 23 to 31% of the fight and stunned 11%, Albion 4 to 9% and 1%.
Nothing touches Albion's Sorcerer (damage taken 0 in most batches) while the Theurgist's pets and
the Minstrel keep the other side's healers and casters interrupted on most of their free turns.
Albion's fighters take almost no damage; nearly all of it goes to one Cleric, which the other
Cleric keeps up. No damage from outside the groups, no server error, no bot left behind.
The area mez over a sleeper produced no lost casts (Sorcerer, Bard and Pac Healer each cast about
two area mezzes a match).

*Cure mez by score, 2026-11-17:* for the Healer, Sorcerer and Bard, waking a mezzed member of the
group is an option in the same choice (healer first, then caster, then fighter); the healing
routine's cure mez uses the same order. The Sorcerer, which never cured mez, did it four to five
times a run.

*Casts accepted and never begun, cause found 2026-11-17:* the server refuses a bot any attack on
an enemy that is asleep, or claimed by a groupmate for the mez it is casting, unless it is the
bot's kill target (`BotPvpCrowdControl.Protected`, in the attack-permission rule). Casters kept
choosing such targets: the Sorcerer aimed its area mez at a sleeping enemy 59 times in one run.
These are no longer options, and the exception now covers the group's kill target for every
member, not only for those assigned to it. Lost casts went from about 90 a run to 1. Note: this
makes "leave a mezzed enemy asleep unless it is the kill target" a hard rule for casters for now;
the owner's guidance is that it should be weighed, which at present happens only in the
kill-target call.
*Owner, 2026-11-17:* casting an area mez over an enemy that is already mezzed is no problem if it
catches more enemies. An area mez may therefore be aimed at a sleeping or claimed enemy; it still
scores only by the awake enemies it catches (`BotBrain.IsPvpAreaMezAim` lets that cast through the
server rule). Measured in the ten-match runs below the starting-point table: no lost casts.

*After the release fix, 2026-11-17:* three matches of Albion against Midgard ended on a wipe, 104
to 134 seconds in, with 8, 6 and 8 of Albion standing; Midgard was mezzed 45% of the fight and
took 94 attacks a match on its healers and casters, Albion 73. Nobody was raised: healers never
get a resurrection off during a fight.

*What the openings show (same recordings):* Albion gets the mezzes off. Its Sorcerer's area mez
(350 units, three-second cast) goes out up to five times in the first eight seconds because
nothing ever lands on the Sorcerer; the Minstrel's mez is an area spell in this database (250
units); six of eight opponents are asleep ten seconds in. Midgard's Pacification Healer got its
cast area mez off once in fifteen matches: it is interrupted from the first second by the
Theurgist's pets, which are a third of everything that lands on the losing side's backline.

*Guard and peel (M6.3), built 2026-11-17, `bots/pvp/PvpPeel.cs`:* the shield fighters (Paladin,
Warrior, Hero; role `Peel`) no longer simply join the kill. Each second, by score: which healer or
caster to guard (healer before caster, the one with enemies in melee on it first, guard does not
hop between two in the same trouble) and which enemy to go for (one on the ward, then one on
another healer or caster, then the kill target while it is near the ward, then one on itself).
With nothing scoring enough it stays beside its ward, where guard works (256 units), and it
travels beside the ward in the formation. Guard is the game's own ability; the engine already let
a bot be the guard. All numbers are in `PvpWeights.Peel`. The recorder writes guard changes and
peel choices as decisions and marks attacks taken on the shield (`guardFor`); the analyzer prints
attacks landing on each team's healers and casters per match and what each guard took.

*Interrupt duty (M6.2), built 2026-11-17, `bots/pvp/PvpInterruptDuty.cs`:* every enemy healer and
caster that is neither the kill target nor asleep gets one interrupter of its own, best pair
first, so interrupts are spread over the enemy backline. Who interrupts and how is a table in the
weights (`PvpWeights.Interrupt.Means`, the owner's list of 2026-11-16): the Theurgist by pets,
Skald and Minstrel by running to the enemy (they leave the kill for it and rejoin when the kill
target is nearly dead), Shaman and the casters by spells: for them the duty is a leaning in the
scored choice (nearsight, disease or root on the duty target, damage still on the kill target).
This replaces the Theurgist-only rule (`PvpRoles.ChooseInterruptTarget`).

*Guard and peel, then interrupt duty, measured 2026-11-17* (four matches per pairing, rank 1,
300-second limit; all 24 matches ended on a wipe, no server error, no bot left behind):
| Pairing | Before (ten matches) | Guard and peel | Plus interrupt duty |
|---|---|---|---|
| Albion against Midgard | Albion 10 to 0 | Albion 4 to 0 | Albion 4 to 0 |
| Albion against Hibernia | Albion 10 to 0 | 2 each | Albion 3 to 1 |
| Midgard against Hibernia | Midgard 4, Hibernia 5, 1 draw | Midgard 3 to 1 | Hibernia 3 to 1 |
What the recordings say:
- Guard works where the guard stands by the enemy's kill target: Albion's Paladin took 17 to 21
  attacks a match on its shield for the Cleric against Midgard, and attacks landing on Albion's
  healers and casters fell from 73 to 54 a match. The Warrior and the Hero took 5 to 9.
- Interrupt duty put Midgard's Skald, Shaman and Runemaster on Albion's backline (52 duty
  changes for the Skald in four matches, which is too many) and did not change that pairing.
- The winner follows the opening, not these two features: in every batch the side with more
  members asleep ten seconds after contact lost. Midgard against Hibernia swung from Hibernia
  7.0 asleep (Midgard won 3 to 1) to Midgard 4.2 against 1.2 (Hibernia won 3 to 1) between two
  runs of four. Four matches a pairing is too few to rank two versions that differ less than
  the opening does.

*Amnesia, built 2026-11-17 on the owner's question:* the Pacification Healer and the Bard do use
their instant mezzes and stuns, about once a match each (ten-minute recast); the 300-unit
instants need 47 or 48 in the line and the templates have 44 Pacification and 46 Music. Amnesia
was never cast, in 30 matches: the old brain leaves it out on purpose and the scored choice had no
such option. It is now a kind of its own (`PvpActionKind.Amnesia`) for the Bard (Superior
Lullaby: instant, 2,300 range, 325 radius, ten-second recast), the Healer (Wake Oblivious) and
the Sorcerer (Inflict Amnesia), the last two with a two-second cast. It scores by the enemies
inside it that are casting right now and loses for each sleeper inside it, because in this
engine amnesia breaks mez; with nobody casting it is not cast. On an 8v8 bot amnesia now only
cancels the cast, as on a player; it used to be able to clear the bot's target list as well (the
monster rule). Not yet measured in a match.

*Owner, 2026-11-17, on the openings:* a Bard usually rushes in and fires its amnesia while it
is casting its mez; a Healer has a hard time against a Sorcerer's mez range, so the idea is to
rush in and put the single-target instant mez on the Sorcerer. Built the same day, as weights:
- An instant is now an option of its own beside the cast-time spell of the same kind (before,
  only the highest-level ready spell of a kind was offered, so the instant mez was used only when
  the caster was being interrupted). An instant mez or stun loses a little for the ten minutes
  it is then gone (`InstantSpent`) and gains when its target is casting, because it lands first
  (`InstantOnCastingTarget`).
- A mez or stun aimed at one of the enemy's own mezzers gains `ControlOnEnemyController`.
- In the middle of a cast the scored choice is run again for instants only, so the Bard's
  amnesia, an instant mez or a shout can go out during a cast, as a player's can. The engine
  already handled an instant during a cast; two bot-only gates refused it.
- "Rushing in" is the existing walk toward the best option that is out of reach. Mezzers still
  travel in the back row of the formation; putting them in front is not done (M7).
Not yet measured in a match.

*"Cast lost" was over-counted, found 2026-11-17:* nearly all of the Runemaster's and Eldritch's
lost casts were two-second nearsights that had finished in 1.2 to 1.4 seconds (dexterity
shortens a cast to about 60%); the check now uses half the listed cast time.

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

*Owner, 2026-11-17:* eight humans who see another group spread out at once, and a healer may run
off on the incoming and come back into range afterwards, so as not to be targeted or mezzed. An
area mez that catches seven of eight on first contact is very rare between groups of equal
skill (it takes a lucky angle, a hill, broken line of sight).
*First version, 2026-11-17 (`PvpScrim.SpreadPlaces`):* groups travel in two rows across the line
of march, fighters in front, casters and healers 700 behind, neighbours 450 apart (an area mez
reaches about 350). Spreading only once the enemy was spotted did not work: two groups close the
1,500 units between spotting and engaging in a second or two. Opening area mezzes went from
catching 7 to catching 2 to 4. Still to do: travel tight and spread on sight as humans do, keep
the spread once the fight is on, and healers stepping back out of range on the incoming.

*Owner, 2026-11-17, on first contact:* the three realms behave differently on incoming because
their crowd control differs. A Sorcerer's cast mez outranges what a Pacification Healer or a Bard
has, and those two make up for it with other tools: instant mez, instant amnesia. A bot has to
see the situation and react to it. And with everyone at realm rank 1 nobody has Purge; in 1.65 a
Druid has Group Purge, which is a huge difference.

*Checked the same day:* this server's 1.65 tables give Purge to every class (10 points, 4 on a
cheaper variant some classes have; 30-minute reuse) and Group Purge to the Druid alone (14
points, 30 minutes). At rank 1 level 0 a bot has one point and buys neither; Purge needs rank 2
level 0, Group Purge rank 2 level 4. Bots buy Purge late, after several passives, use it only
under conditions written for battlegrounds (or below 60% health), and never buy or use Group
Purge. So every result so far is from a world without Purge, where the first area mez decides
the fight. To do: 8v8 bots buy Purge first (the Druid Group Purge) and use it by score; every
pairing is then run at two ranks, rank 1 and one where Purge exists.

*Instants, amnesia and keeping apart, measured 2026-11-17* (four matches per pairing, rank 1; all
24 ended on a wipe, no server error, no bot left behind):
| Pairing | Guard, peel, duty | Plus instants and amnesia | Plus keeping apart |
|---|---|---|---|
| Albion against Midgard | Albion 4 to 0 (18 to 0 over all earlier matches) | 2 each | Albion 3 to 1 |
| Albion against Hibernia | Albion 3 to 1 | Hibernia 3 to 1 | 2 each |
| Midgard against Hibernia | Hibernia 3 to 1 | Hibernia 3 to 1 | Hibernia 4 to 0 |
- The Pacification Healer now opens with its instant mez about a second before contact. Against
  Albion, members asleep ten seconds after contact went from Albion 0.8 and Midgard 6.2 to
  Albion 3.8 and Midgard 1.5, and Albion's Sorcerer got no area mez off in the first ten seconds
  of any of those four matches.
- The Bard casts amnesia 7 to 9 times a match and fires instants in the middle of its casts 3
  to 5 times a match; Hibernia won 12 of its 16 matches in these two runs.
- Keeping apart: 7 to 31 steps a match per team. Openings became even: with it, each side had
  1 to 4 asleep at ten seconds in 23 of 24 cases (one 5); without it, four openings of 24 caught
  5 to 7. Heals cast did not fall (Hibernia's rose).
- Over the two runs together, eight matches a pairing: Albion 5 to 3 over Midgard, Hibernia 5 to
  3 over Albion, Hibernia 7 to 1 over Midgard. Midgard is now the weakest of the three.
The analyzer's "asleep ten seconds after contact" could count a bot twice; fixed, and the figures
above are from the corrected count.

*Purge by score, built 2026-11-17, `bots/pvp/PvpPurge.cs`:* an 8v8 bot now buys its answer to
mez before anything else, as soon as its points allow: the Druid Group Purge, then everyone
Purge (the cheaper variant where the class has it). What it cannot pay for yet does not stop it
buying the rest, so rank 1 is unchanged. Both are used by score, only during a fight: Purge when
the bot is mezzed (or a healer-mezzer stunned) with enough of the effect left, less when a
groupmate's Group Purge is about to free it; Group Purge by how many members in its 1,500 units
are mezzed or stunned, healers counting more (three asleep, or two with a healer among them).
The Group Purge handler was written for players only and now serves an 8v8 bot. Weights in
`PvpWeights.Purge`; both uses are recorded as decisions. Not yet deployed or measured; it needs
a run at a rank where Purge exists (`RealmRank=5`).

*Why the bots bunched up at contact, found 2026-11-17:* the two-row formation holds only while
the group travels. A bot gets its target at 3,000 to 3,500 units, the formation orders stop for
it, and it walks straight at that target; eight bots walking at the same few enemies converge,
and gaps of 450 units are about 200 by the time they are in casting range. Ten seconds after
contact five to seven of the losing side were asleep, against none or one of Albion.

*Keeping apart, first version built 2026-11-17, `bots/pvp/PvpSpacing.cs`:* a healer or caster
walking into range heads for the nearest place at that range that is not within mez radius of
another healer or caster of its group (it slides along the circle of its range); and once the
enemy is in sight, one standing within that radius of another takes a step away, never toward
the enemy, on a turn it is not using. Fighters are left out: they stack on the kill target, and
a shield fighter belongs beside its ward. `PvpWeights.Spread` holds the distance (400) and the
step; 0 turns it off. The analyzer prints how many of each team are asleep ten seconds after
contact. Still to do: mezzers of the instant-mez realms going in ahead of their group, the
Sorcerer's group holding at range, healers stepping back on incoming.

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

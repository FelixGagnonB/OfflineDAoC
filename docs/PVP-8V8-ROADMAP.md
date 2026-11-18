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
- [x] **M2.5** (M) Several arenas at once in different boxes, since game time cannot be sped up.
  Done 2026-11-17, see the note below.

*Parallel arenas, 2026-11-17 (owner: four hours for a comparison base is far too long).* Measured
first: of 203 seconds a match, 55 were buffing, 29 walking to contact and 118 fighting, and
matches ran one at a time on a machine with 12 cores. Now:
- `PvpScrim` is a manager and `PvpScrimRun` is one series; several run at once, each in its own
  lane with its own recording (`PvpRecording`), and a match is removed without touching the
  others. `scrim-start` takes `Pairs=a:b,c:d`, `Copies` and `Lanes=1`; `scrim-status` answers
  with one entry per series.
- Six lanes in the Emain box, each 6,000 units long, no two closer than 6,000 (nothing a bot
  does reaches 4,500). They were chosen from a scan of the box (`scrim-scan`): three of the six
  first drawn on paper crossed ground that is not open. Four run north to south on nearly level
  ground (it varies by 48 to 123 units along them); two run west to east over hills (425 and
  451) and are used only for a fifth and sixth series. `scrim-lanes` checks them before a run.
- Groups start 6,000 apart, already in their travelling formation; contact comes after 6 to 9
  seconds instead of 29.
- `source/tools/pvp_batch.py` does a whole run: weights made explicit, server up, lanes
  checked, all series, server down, analyzer on every batch. It replaces the runner scripts
  that lived in a temporary folder.
Measured: 12 matches (three pairings, four each) in 13.7 minutes where they took 40; 24 matches
in six lanes in 13.3 minutes. No lane saw another (no damage from outside a match), no bot was
left behind, no server error, server memory 5.7 to 5.8 GB. Results over those 36 matches agree
with the single-arena runs before them: Albion 7 to 5 over Midgard, Hibernia 8 to 4 over
Albion, Hibernia 10 to 2 over Midgard. Teams are no closer together at contact than in the big
arena (average distance between members 876 against 684), but openings that put six or more to
sleep were seen 7 times in 72 against 0 in 24; too few to say whether level ground helps the
long-range mezzer or it is chance.
*Owner, 2026-11-17:* an area mez catching five or more in about one opening in ten can be
valid; it depends on the angle of contact and on whether the enemy group was aware of the
contact. Accepted as it is. In a lane the two groups always meet head-on and both know: contact
from the side or behind, and a group caught unaware, are not played yet (M7 and M8).
*Owner, later the same day:* then, head-on and aware, one in ten is high: there is no reason for
a team to get five to seven mezzed on contact.
*Looked at mez by mez, 2026-11-17:* it never was one spell. "Asleep ten seconds after contact"
adds up every mez of those ten seconds. In the 36 lane matches no area mez in the first ten
seconds put more than two to sleep (87 casts: 65 caught one, 22 caught two); the same holds for
the 12 big-arena matches with keeping apart (27 casts). Before keeping apart, 9 of 100 opening
area mezzes caught three to five; the largest was five, so the earlier note that one mez caught
all eight was wrong (that figure, too, was everybody asleep a moment later). The openings with
five to eight asleep are five to eight separate mezzes landing one after another: a Bard got six
off in ten seconds, the two Healers and the Skald seven, the Sorcerer and Minstrel six or
seven, while nothing interrupted them and nobody was woken. So keeping apart does its job, and
what is missing is the answer to a mezzer that is left to cast: its interrupter going to it
first, and waking those it puts to sleep. The analyzer now prints, per team, the mezzes that
landed in the first ten seconds and the most any one area mez caught.
Quick buffing, tried and taken out again the same day: with a bot's own buffs taking a quarter
of a second each during the ready check, every bot started with the same buffs (after a first
version that ended the check too early was caught by the comparison), but the check took 57
seconds instead of 60 to 67 for Albion and 37 instead of 40 for the others. The wait is the pace
at which the old brain puts up buffs, chants and songs, one every few seconds, not their cast
times. The "ready" line of a recording now lists every bot's buffs and pet; that was kept.
Shortening the ready check (55 of about 175 seconds a match) is still open.

*New against old in the same match, built 2026-11-17:* a second set of weights
(`pvp-weights-old.json`, `PvpWeights.Reference`) can be given to one team of each match
(`scrim-start Versus=1`, `GameBot.PvpWeightsSet`). With two lanes per pairing, the second with
the sets the other way round, every realm fights with both sets against each opponent, and the
count that is left is new against old. `pvp_batch.py --weights new.json --versus old.json` (or
`builtin`) runs it and prints how many matches the new set won, so a change switched by a weight
needs no base run of its own. First use: `Interrupt.OnController` (an interrupter goes to the
enemy's mezzer first; 0 in the built-in weights until measured).
Results, 24 matches each: the weight at 40 won 14 to 10; with a guard added (no interrupter for
an enemy the team's own mezzer is putting to sleep, which wakes it and leaves it immune) it lost
8 to 16; and a control run with the same weights on both sides came out 10 to 14. So a
24-match comparison moves by four either way on chance alone, the weight shows no effect that
size, and it stays at 0. A comparison needs about a hundred matches to show a difference of
ten points of win rate, or a change large enough to see in 24.

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

*Recorder format 3 and the fight review, 2026-11-18* (owner: first make sure the recorder and
the analyzer have everything needed to review a fight, then finish the brain, then test). Added
to every recording: a cast beginning (`begin`) and a cast that ended without finishing (`stop`,
with why: attacked and by whom, moved, refused by the game, dropped by the caster); health
actually restored by a heal and how much was asked for (`heal`); who broke a mez (`wake`); each
group's kill target and trained member every second (`squad`); and in the once-a-second state of
each bot its height, nearsight, disease, silence, being interrupted, and whom it is on, guards
and has interrupt duty for. The engine calls the recorder at five places for this (cast start,
the two ways a cast is cut, a health change, a mez removed); each call returns at once when no
match is being recorded.
`pvp_analyze.py` now also reports, per team: how close its healers and casters stand to the
nearest enemy fighter and to each other, how far apart the two groups are, how much of the time
its healers and casters are nearsighted, how long a member had the enemy's fighters on it and
how often it died of it, mezzes broken on the kill target and on somebody else; and per build:
casts begun against finished with what stopped the rest, health restored, wasted, and the share
that went to the trained member. `pvp_review.py` prints one match second by second, or one bot's
fight with its state and decisions.
First things they showed, on recordings already there: the middles of the two groups are 258
units apart for most of a fight (under 500 units 84% of the time), and a healer or caster has
an enemy fighter within 500 units about half the time. One Mending Healer's opening, read with
the review tool: amnesia on the Bard, nearsighted a second later, then it walks toward the enemy
to get its mez in range and is mezzed 117 units from an enemy fighter.

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

- [x] **M5.1** (M) A dedicated brain class for 8v8 bots and a squad object with a shared
  blackboard, ticked once per group before its members think.
  Squad object done 2026-11-17 (`bots/pvp/PvpSquad.cs`): what the group has decided (kill
  target, interrupt duty, guard and peel, keeping apart, heal assignment, what it sees) lives
  there and no longer in the scrim loop, so the owner's own group can have one. A separate
  brain class was not made: the 8v8 behaviour stays in partial files of `BotBrain` under
  `bots/pvp`, switched on `IsPvpBot`. Splitting the class would mean opening most of its
  private members for no change in behaviour; revisit if the two kinds of bot start to get in
  each other's way.
- [x] **M5.2** (M) Perception: enemies are known only when in view range and in sight; stealth
  respected; last known positions remembered and aged; enemy class and current cast visible.
  First version 2026-11-17 (`PvpSquad.Known`): an enemy is known while a living member has it
  within 4,500 units with open ground between, and for ten seconds after. The group decides,
  and its members score, only about known enemies. Not done: stealth (the main groups have no
  stealther) and acting on a last known position.
- [x] **M5.3** (M) Roles from class and spec: main tank, peeler, assist damage, caster damage,
  primary control, healer, speed and support. `PvpRoles` (assist, control, interrupt, heal,
  peel) and the interrupt means per class in the weights.
- [ ] **M5.4** (L) Class kits: a data table tagging each spell, style and ability with its
  function (area mez, stun, root, snare, disease, interrupt, guard and so on). Main-group classes
  first. Replaces the scattered class switches for 8v8 bots.
  Partly there: spells are tagged by kind for the scored choice (`PvpActionKind`,
  `IsPvpSpellOfKind`) and each class's leanings are data (`PvpWeights.ClassBase`). Styles and
  abilities are not tagged, and the old brain's class switches still decide for fighters. The
  rest is done with M6.4, when fighters get a scored choice and need it.
- [x] **M5.5** (M) Kill-target calling: one target per group, chosen by how killable it is, with
  rules for when to swap. `PvpKillTarget`; the call is recorded (line type `call`).
- [x] **M5.6** (M) Heal assignment across the group's healers, on top of the existing triage, so
  two healers do not land on the same patient while another dies.
  Done 2026-11-17 (`PvpHealPlan`, `PvpSquad.AssignHeal`): a heal already being cast on a member
  counts as landed, so the next healer goes to whoever is then worst off, or has its turn free
  when everyone is covered. Weights in `PvpWeights.Heal`.
- [x] **M5.7** (S) Every decision written to the recorder with its options, scores and reason.
  Recorded: caster spell choice, guard, peel, interrupt duty, steps apart, Purge, heals and
  cures (with the patient's health and whether the heal was sent elsewhere), the kill-target
  call, and the target of each fighter on the kill. Not recorded as decisions: the mezzes of the
  classes still on the old crowd-control routine (Minstrel, Skald; their casts are recorded)
  and the choice of melee style (the style used is in every hit).

**Exit check** (reworded 2026-11-17 with the owner: no base against the old brain is kept, so a
win rate against it means nothing): every role decides through the squad or the scored choice
and what it decides is recorded; a reference base with this build is on record (ten matches a
pairing at rank 1 and at rank 5, no server error, no bot left behind, no damage from outside a
match). Behaviour is then judged and tuned in M6, one question at a time, against that base.

**Passed 2026-11-17.** *Reference base, M5 build* (`pvp_batch.py Matches=5 Copies=2`, six lanes,
ten matches a pairing, built-in weights; 17.1 and 16.7 minutes; all 60 matches ended on a wipe
after 71 to 178 seconds; no server error, no bot left behind, no damage from outside a match;
3,193 automated tests pass on the build):
| Pairing | Rank 1 | Rank 5 (Purge and Group Purge) |
|---|---|---|
| Albion against Midgard | Midgard 7 to 3 | Albion 6 to 4 |
| Albion against Hibernia | Albion 6 to 4 | Albion 8 to 2 |
| Midgard against Hibernia | Hibernia 8 to 2 | Hibernia 7 to 3 |
Per team and match at rank 1: 1.7 to 4.7 members asleep ten seconds after contact, from 1.8 to
4.9 mezzes; no area mez put more than two to sleep; teams mezzed 12 to 28% of the fight; 57 to
90 attacks landing on healers and casters; 3.7 to 8.1 kill-target calls; 1.4 to 13.1 heals sent
to another patient because one was already on its way (Midgard's Healers and Shaman most).
At rank 5 nobody is asleep ten seconds after contact in 55 of 60 team openings although 2.5 to
3.7 mezzes land: they are purged (every match starts with Purge ready, see M6). Teams are mezzed
0 to 8% of the fight and 87 to 153 attacks a match land on healers and casters.
Read with care: ten matches a pairing still moves by two or three on chance (a control run of
24 matches with the same weights on both sides came out 10 to 14).

*Owner, 2026-11-17, for M6:* it is still surprising that with the Pacification Healer's instant
mez and the Bard's instant amnesia the Sorcerer wins the mez fight on first contact so often.
To be looked at closely in M6.

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

*M6 deep dive, first question, 2026-11-17: who wins the mez fight on first contact, and why.*
From the rank 1 base (M5 build), mezzer by mezzer:
- Against Hibernia the Bard landed the first mez of the match in 10 of 10; Albion's Sorcerer was
  first hit 1.2 seconds after contact, asleep 8.2 of the first 10 seconds, and landed its first
  mez 22 seconds in (never in 3 of 10). Against Midgard the Skald landed the first mez in 8 of
  10 and the Sorcerer was asleep 5.5 of the first 10 seconds. So since instants became options
  of their own the Sorcerer loses the opening; before, it won it.
- The owner remembered right: in this server's data the Sorcerer's area mez reaches 1,875 units
  (the Senility line), every Healer and Bard mez 1,500.
- The Sorcerer did not use that range. With the enemy's front row inside 1,875 it kept walking
  toward a healer in their back row, which scored a little higher, and cast with the nearest
  enemy at 770 to 1,000 units. A weight for it, `NeedsWalk` (what can be cast from where the
  caster stands comes first; 0 in the built-in weights), tried at -40 in 12 Albion matches: the
  Sorcerer now begins its area mez about 1.6 seconds before contact with the nearest enemy at
  about 1,700, and still loses the race. The Bard runs ahead of its group for the Sorcerer
  (the owner: "a Bard would usually rush in") and its instant lands half a second before
  contact; the Sorcerer's three-second cast, about 1.9 with its dexterity, would have ended
  0.3 after. Both groups charging close 600 units a second, so 375 units of range are worth
  0.6 seconds, and the half second between a bot's decisions eats most of that.
- What a head-on charge leaves out: the Sorcerer's group holding at range instead of running
  in (halves the closing speed), and a human Bard needing a moment to find the Sorcerer among
  eight, where a bot knows at once. Both still to do (opening positions; reaction time, M10).
- Albion's own answer was missing: in 32 Albion matches the Sorcerer was never woken. The
  Minstrel has cure mez (Focusing Chant) and never cast it: the old brain reaches cure mez only
  through its healing routine, and a Minstrel has no heals. Fixed: the Minstrel's cure mez is a
  scored option (nothing else of the Minstrel's is), and waking the group's own mezzer scores
  more (`CureMezOnController`), since it comes out immune for a minute.
- New in the recording: when a scored caster's turn goes to its healing and curing routine or
  to a cast waiting to start, a line says so. Midgard's Healers lose 3 to 4 turns a match that
  way, the Shaman 2.

  Measured in 12 Albion matches with the built-in weights: the Minstrel cures mez about twice a
  match and the Sorcerer was woken in 5 of 12 (2, 3, 7, 8 and 34 seconds after it was first
  mezzed), against never before. It is still asleep 16 of the first 30 seconds.

*Owner, 2026-11-17, on Midgard's healers:* the Pacification Healer should weigh more toward crowd
control and the Augmentation or Mending Healer more toward healing; the second also has its cast
mez, stun and cure mez. The Healer is a tricky class because of its role and tight specs. The
Shaman should weigh more toward interrupts, with backup heals when the Pacification Healer is
too busy to help. Both Healers have an instant single heal and an instant group heal.
Built the same day:
- A leaning per build on top of the class's (`PvpWeights.BuildLean`): `healer-pac` gets more on
  mez, area mez and stun; `healer-mend` less, but its mez still clears the bar when nobody needs
  a heal.
- Who heals first (`PvpWeights.HealsFirstBelow`): a healer's healing routine used to take every
  turn it wanted before the scored choice was even looked at (3 to 4 turns a match for each
  Healer, 2 for the Shaman, recorded since this morning). For `healer-pac` it now comes first
  only while a member in reach is below 40% health, for the Shaman below 50%; above that the
  scored choice (control, cure mez, disease, root) goes first and heals follow. Everyone else
  heals first always, as before.
Measured the same day, 12 Midgard matches each way on one build: with the thresholds at 40
and 50% the three cast 5.2 heals a match against 7.2 without, all three were dead more of the
fight (59, 51 and 57% against 54, 42 and 45%), the Pacification Healer cast one more mez or stun
a match and three fewer cure mezzes, and Midgard's enemies were mezzed 8% of the fight against
13%. Waiting until a member is below 40 or 50% is too late (the owner: "probably too low").

*Owner, 2026-11-17, on healing, first-hand (they played Druid, Cleric and Mending or
Augmentation Healer and nothing else):* the hard part was knowing whether someone was being
trained by the enemy's assist train. When someone called that the train was on them, all focus
went to spamming heals on them until a peel, mez or stun let the teammate run and get distance.
Mostly two heals, the "anytime" one and the "needs big heals" one: the spec line's Major heal
and its Greater heal, both hungry for power. Spread heal when needed. Never group heals. How
that compares with 1.65 they do not know.
Checked against this server's data and the base recordings:
- The spec lines have exactly that pair (at 41 and 42 in the line: Greater 549 health for 109
  power, Major 387 for 57), and the base line a plain heal (231 for 42) and a Minor one (119 for
  17, in 2.2 seconds).
- The bots' two heals were picked for power efficiency, which made the second one the Minor
  heal. The Regrowth Druid and Rejuvenation Cleric happened to end up with Major and Greater;
  the Nurture Druid cast the Minor heal 76 times in 20 matches and never its Major, the
  Mending Healer the Minor 28 times and never its Major, the Enhancement Cleric the Minor 44.
Built: an 8v8 healer's big heal is the one that heals most and its "anytime" heal the cheapest
per point among those healing at least half as much (`PvpHealPlan.PickHeals`), which is the
spec Major heal for whoever has one. And the trained member (`PvpSquad.Trained`: two or more
enemy fighters in melee on it, which a human group hears on voice) is healed first and as soon
as it is below 97%, not only once it is hurt; it also claims the turn of a healer built for
control. Running away to get distance once peeled is not built (M7.2).
The heal-first threshold for the Pacification Healer and the Shaman was then set to 75%: at
that value the three Midgard healers cast 9.1 heals a match against 7.2 with the leaning off,
died no more, and the Shaman cast 6.0 diseases and roots a match against 4.4.
Measured, 24 matches against the 30 of the base: "trained" is called 12 times a match and a
third of the heals go to that member; the Rejuvenation Cleric heals 1,941 health a match
against 986, the Regrowth Druid 1,574 against 1,347, the Nurture Druid 899 against 733 with its
Major heal in place of the Minor; Albion and Hibernia are dead 16% of the fight against 21 and
20%, in fights of the same length. Midgard is dead 30% against 22% and won 4 of 16.
- The Mending Healer heals about 300 health a match: it casts 2 to 3 heals and 8 to 13 cure
  mezzes, because Midgard is mezzed a lot and the old routine puts cure mez before any heal that
  is not an emergency. Putting heals first for every 8v8 healer was tried and made it worse:
  Midgard's cure mezzes fell from 7.7 to 2.0 a match, its healing did not rise (its healers are
  dead half the fight and interrupted for much of the rest), and it lost all eight to Hibernia.
  Taken out again. What keeps Midgard's healers from casting is the question, not the order.
- The Enhancement Cleric heals less than before (250 health a match against 435): it is
  usually the enemy's kill target and a 3.2-second Major heal is interrupted where a 2.2-second
  Minor one sometimes got through.

*Owner, 2026-11-17:* "As healer, I was always just healing; maybe 10% of the fight I used roots,
stun, mez or demez to help or peel if I could." And on learning that this server's healers have
cure nearsight (the owner does not think 1.65 had it; the server marks the spell as custom, so
it was probably added by the emulator): then it belongs on the priority list.
Built: a healer with the 3.5-second cure nearsight (the main healer of each realm; the second
healers have only the 6-second one, the Shaman none) lifts nearsight from a caster or healer of
its group once nobody is below 65% health and nobody is asleep (`PvpHealWeights.
CureNearsightMaxCastTime`). Not yet measured.

*Owner, 2026-11-17, on Midgard's two Healers:* in Albion and Hibernia the roles are split and
everybody knows who does what; in Midgard the two Healers are in a tight spot. The Pacification
Healer must keep interrupt, mez, demez and stun ahead of heals while knowing when to heal; for
the Mending Healer it is the other way round. It has to heal most of the time and position
itself so that it is interrupted as little as possible, since it is the main healer for the
whole fight. Midgard can turn that to its advantage when the enemy pushes deep to put its
assist train on the Mending or Augmentation Healer.
Built from it:
- The Pacification Healer's cure mez scores more (`BuildLean`), with its mez and stun.
- A healer built to heal leaves waking a member to the group's control healer while that one
  is alive, awake and within 2,000 units, and wakes the control healer itself
  (`PvpHealWeights.HealerLeavesCureMezToController`). The earlier attempt stopped both Healers
  curing; this one steps only the Mending Healer back, and only while the other can cover.
- The trained member claims a control healer's turn only below its heal-first mark (75%); from
  the first blow it is the heal-first healers' patient.
Still to build from the same guidance: the main healer's position (standing
back at heal range, stepping away when enemies come for it; M7.1 and M7.2), and turning on
enemies who push deep for the healer (the kill-target call weighs class and health and not who
has overextended).

*Overnight, 2026-11-17 to 18: 816 matches unattended* (34 batches of 24 in six lanes, about
eight hours; every batch finished, every match ended on a wipe, no server error, no bot left
behind). Build: everything above, cure nearsight on, Midgard's Healer split on, casting from
range off.
| Pairing, 40 matches each | Rank 1 | Rank 5 (Purge and Group Purge) |
|---|---|---|
| Albion against Midgard | Albion 25 to 15 | Albion 25 to 15 |
| Albion against Hibernia | Albion 28 to 12 | Hibernia 21 to 19 |
| Midgard against Hibernia | Hibernia 24 to 16 | Hibernia 29 to 11 |
Midgard is the weakest at both ranks (31 of 80 at rank 1, 26 of 80 at rank 5).
Three comparisons of 192 matches each, one team with the new set and one with the old, every
pairing fought both ways round:
| What the new set had | New won | Old won | By realm, new against old |
|---|---|---|---|
| Casting from range before walking (`NeedsWalk` -40) | 123 | 69 | Albion 78% against 54%, Midgard 54% against 18%, Hibernia 59% against 34% |
| Cure nearsight | 85 | 107 | Albion 45% against 65%, Hibernia 48% against 62%, Midgard 39% against 39% |
| Midgard's Healer split | 102 | 90 | Midgard 34% against 32% (the setting does nothing for the other two) |
- Casting from range is a clear gain for every realm and is now the default. One flaw found
  while adopting it: a caster whose only option needed a walk scored under the bar and stood
  still. For the bar, the walk's rank penalty is now given back (`PvpActionChoice.WorthIt`).
- Cure nearsight cost Albion and Hibernia about fifteen to twenty points of win rate: the main
  Cleric cast it four times a match, 14 seconds not spent healing. It is now off by default
  (`CureNearsightMaxCastTime` 0); the code stays for a narrower use (the group's mezzer only,
  or a shorter list of moments).
- Midgard's Healer split shows no effect on Midgard's results either way. It stays, as the
  owner's description of the two roles.
- A yardstick from the same night: in the Healer-split comparison Albion won 67% with one set
  and 53% with the other, and the setting does nothing for Albion. So with 64 matches an arm, a
  gap of fifteen points can be chance alone; the gaps above for casting from range (24 to 36
  points, every realm the same way) are not, and cure nearsight's (14 and 20, two realms the
  same way, the third unaffected) is likely real.
- The analyzer's warning about bots standing up away from their corpse fired once in the 816
  matches: two bots of one match had been given the same name, and a dead one and its living
  namesake read as one bot. 8v8 bots now get names no other 8v8 bot in the world has.

*Correction, 2026-11-18: the cure nearsight result above is not valid.* Asked by the owner how
many members get nearsighted, the rank 1 base (120 matches) gave: nearsight is cast by the
Runemaster (2.5 landed a match) and the Eldritch (2.1); 4.6 of the 16 in a match are nearsighted
at least once (1 to 9); by role, a main healer in 47% of its matches, a second healer 48%, a
mezzer 51%, a caster 55%; Albion gets most of it (Theurgist 91%, main Cleric 81%, second Cleric
and Cabalist 78%, Sorcerer 73%, against 23 to 42% for Midgard's and Hibernia's), usually within
ten seconds of contact; it lasts two minutes.
The same count showed the cure being started far more often than nearsight landed: the main
Cleric started cure nearsight 11.8 times a match and finished 1.7, with 8.4 restarts inside a
second and a half; the Regrowth Druid 5.2 and 0.7; the Mending Healer 2.8 and 0.4. While a
healer's instant heal is ready, the old healing routine runs on through a cast in progress,
picks the same cure again and begins it anew on every think. So the overnight comparison
measured a healer stuck restarting a cast, not cure nearsight. Cure mez had the same fault to a
lesser degree (Pacification Healer 2.7 started, 0.7 finished). Fixed: an 8v8 healer does not
restart or drop the heal or cure it is casting; only an emergency heal takes the place of a
cure in progress. Cure nearsight is being measured again; it stays off until then.

*Owner, 2026-11-18: "we jump ahead too fast with all those tests, instead of finishing the brain
and making sure the bots know how to play correctly."* Agreed. From here the order is: write
down how each role plays ([PVP-8V8-PLAYBOOK.md](PVP-8V8-PLAYBOOK.md), each line marked done,
partly or missing, with its source), have the owner correct it, build the missing lines class by
class, check each one only for whether it happens in a match, and leave comparison runs and
weight tuning for when the playbook is built.
*Owner, same day, on the Skald:* changing target often is right for it. Most of the time the
Skald is not part of the assist train, unless there is a critical kill. It is an interrupt
class: target a caster, instant damage shout, switch to another caster, healer or mezzer, second
shout; running past a fighter it may swing with its snare attack, then back to interrupting;
instant mez on a free enemy that is not a fighter, and on with the interrupts.

*Owner, 2026-11-18, on the playbook:* "The name of the game is how to react to what is going on.
There are no hard rules, only soft rules, and any soft rule could overrule any other based on
the current situation." A Bard should not always run in to stop the Sorcerer; in some fights the
better answer is to kite and work on the enemy as it pushes in. "It really comes down to how the
enemies' melee are controlled (stun, snare, mez), how casters are interrupted and how busy the
opposite healers are." The owner's own review draft and the first checklist were merged into one
[PVP-8V8-PLAYBOOK.md](PVP-8V8-PLAYBOOK.md) on that footing: it is not a rulebook. It holds the
three readings of a fight (fighters controlled, casters interrupted, healers busy, each for both
groups), the orders a group can be under (hold, engage, extend, push, peel, regroup), what each
role leans toward and what shifts it, and what is built. The server's data settled the draft's
open points (who has cure mez, cure nearsight, spread heal, amnesia, nearsight; Speed of Sound
is Minstrel only here). It also showed that Albion's group had no nearsight: the Cabalist's
nearsight is in its Matter spec line and it had 5 there. The owner changed the Cabalist to the
three-line utility build, Body 34 / Spirit 33 / Matter 25 (`cabalist-tri`, replacing
`cabalist-body` at Body 47 / Spirit 26 / Matter 5): disease at Body 34, the body resistance
debuff at Spirit 33, the 45% nearsight at Matter 25. Recordings before this change have the old
build under the old name.
The owner's role for it: nearsight enemy casters (healers and high-impact casters first),
disease enemy fighters and other targets that matter, keep the pet on an enemy and take
interrupt assignments, damage on the kill target when there is a kill, the body resistance
debuff when the casters kill together, no needless damage into a mez.
First run of the new build (four matches, Albion against Midgard and Hibernia): it finishes 4.8
nearsights, 5.2 diseases and 15 lifedrains a match; enemy healers and casters were nearsighted
55% to 98% of the fight (the Bard 14%). Not yet as the owner describes: 15 of 19 diseases went on
healers and casters and only 4 on fighters; the pet never attacked; the body debuff was never
cast.

*Readings, group orders and standing off, 2026-11-18* (first step of "finish the brain").
- **Readings** (`PvpReading`): every second the squad takes the three readings for itself and
  for the enemy as it sees it (an enemy out of sight counts as free): share of living fighters
  mezzed, stunned, rooted or slowed; share of living healers and casters able to cast; share of
  all healers, dead ones included, able to heal. They are written to the recording's `squad`
  line.
- **Orders** (`PvpOrders`): Engage scores 0; Push and Extend must beat it. Push grows with the
  enemy's healers down, its fighters held and the numbers ahead; Extend with the group's own
  living healers unable to heal, the enemy's fighters free and the group's healers and casters
  under attack. An order stands three seconds. Hold and Regroup are not built.
  What an order does so far: under Extend healers and casters want 1.4 times the usual distance
  and the fighters leave the kill target for an enemy fighter on their backline; under Push
  healers and casters accept 0.6 times the distance.
- **Standing off** (`PvpPositioning`): a healer or caster with an enemy fighter nearer than 900
  units scores sixteen steps of 350 units (distance gained from the nearest fighter, less the
  cost of leaving the range of its work or its healers, less crowding a groupmate) and takes the
  best if it is at least 150 better than staying. The step is its turn: the brain starts no cast
  until it has arrived. A healer does not step while a member in reach is below 60% health,
  unless it is the one attacked.
  The measure behind it (ten recorded matches): a healer or caster had an enemy fighter within
  500 units 57% of the time; for 24% of the time that fighter was free and on somebody else, for
  13% every such fighter was mezzed or stunned, for 20% a free fighter was on the bot itself.
- First run (six matches): steps away from fighters that were not on the bot worked (farther two
  seconds later after 71%), steps away from a free fighter that was on it did not (43%, 7 units
  nearer at the median: both run at the same speed). So a free fighter on the bot within 600
  units now pins it, and it runs only once the fighters on it are mezzed, stunned, rooted or
  slowed, which is what the owner described. Second run: 73% of those runs and 65% of the other
  steps gained distance.
- **Not there yet.** Over the whole fight the median distance from a healer or caster to the
  nearest enemy fighter did not clearly move (500 to 720 by match-up in the first run, 492
  overall in the second), and they now walk for a quarter of the fight (the Sorcerer and
  Cabalist a third). The steps are taken and then undone: the fighters follow, and the caster
  walks back into range of its next target. Orders: about 40% of the time engage, 35% push, 25%
  extend, the winning side mostly pushing and the losing side mostly extending, with frequent
  changes. Both need the next pieces: the group moving as one under Extend, fighters held back
  for their healers, Hold on incoming, and a caster choosing targets it can reach from where it
  stands. 3,220 automated tests pass (20 new for readings, orders and positioning). No
  comparison run was made: nothing here says the bots win more or less for it.
- **Second round, same day.** Why the steps were undone: 35% of them were followed by another
  step (the caster walking away 350 units at a time and casting nothing between), and 15% by
  the caster walking back, mostly a nearsighted one whose range is cut. Changed: a step may be
  700 units when that is the better one; after a step the bot works from its new place for four
  seconds (one running from held fighters does not wait); and a caster's work range is the
  range of its last chosen spell on its last chosen enemy, so a nearsighted caster no longer
  steps out of the range it has. Result over six matches each, same measure: time with no enemy
  fighter within 500 units of a healer or caster 44% before, 59% now; a free fighter on somebody
  else within 500 units 24% to 18%; only mezzed or stunned fighters within 500 units 13% to 7%.
  The median distance is about 550 units and walking is still a quarter of the fight.
- **Hold** (`PvpOrders.Holds`): before the fight a group whose longest cast mez outranges the
  enemy realm's by 200 units or more stands where it is; its fighters are given no target, its
  casters cast what reaches without walking, nobody marches. It ends when an enemy is within
  1,200 units of a member, after ten seconds, or at the first blow, and is not taken up again.
  The enemy's reach is its realm's (a group that sees Midgard's fighters knows a Healer is
  behind them); comparing with only the enemies in sight made Midgard and Hibernia hold against
  each other, which was corrected. In the runs only Albion holds, about nine seconds a match;
  in the match read with `pvp_review.py` its Sorcerer's area mez caught two and its Cabalist's
  nearsight was cast while Midgard was still closing, before Midgard's first mez.
  3,223 automated tests pass. Still no comparison run.
- **Kiting** (owner, 2026-11-18: "when kiting, melee could snare or slam while moving back;
  similarly, an already far caster could cast snare, area snare or even roots").
  Built: a fighter whose target is an enemy fighter on the group's healers and casters asks for
  a stopping style (`PvpStylePolicy.ControlStyle`): a stun that would land, else a snare on a
  target not slowed yet, else its best damage style. Under Extend a fighter with nobody to turn
  on and more than 1,000 units from the middle of its healers and casters comes back to them
  (`FallBack`). A root on such a pursuer scores 30 more, and 30 more again under Extend; Cabalist
  and Runemaster got a low root leaning (the Eldritch has no root).
  First run (six matches): four or five fall-backs a match; stun and snare styles landing 19 a
  match against 15 before. Roots on pursuers: five in six matches, so that part does not work
  yet. Fighters are on an enemy healer or caster in melee for about 190 fighter-seconds a
  match, so the chance is there; root options on fighters mostly score 30 to 34, without the
  pursuer bonus. Suspected and not yet checked: a diseased fighter already carries a movement
  slow and so counts as "already rooted". Area snares and snare nukes are not scored at all.
  3,231 automated tests pass. No comparison run.
- **Why casters did not root pursuers** (same day). The suspicion was wrong. The recorder now
  writes snare immunity for every bot and, for a root option, whether its target is a pursuer and
  whether it is rooted, slowed or immune already. A run showed a root would have landed on a
  pursuer 75% of the time (19% immune, 5% slowed), and that the option was listed and not taken:
  it scored 46 against a bar of 50. The 15 points taken off a root whose target is "already
  interrupted" are about a root cast to stop a caster; a fighter on the group's healers is
  always being hit, so every pursuer carried the penalty. It no longer applies to a pursuer.
  Also: a snare nuke counts as a root for a class without one (the Eldritch), an area root is
  taken when it is the class's highest, and both Healers have their Pacification root scored.
  Next run (six matches): a root picked on a pursuer 41 times against 3; about seven roots on
  fighters a match (Theurgist 4.3, Sorcerer 1.7, Shaman 0.7). Pursuers slowed or rooted 8% of
  the time against 5%. Stun and snare styles landing were 14 a match in this run and 19 in the
  one before: the style change shows no effect beyond noise. 3,235 automated tests pass.

*Owner, 2026-11-18, on how to work:* do not spend much effort optimizing the fights for now;
testing one piece instead of the whole picture may not give meaningful insight, and every rule
is a soft one (sometimes a good target is a melee, other times a caster). Finish M6 so that all
behaviours are in place, then run large batches, record, analyze, and improve after each batch.
Agreed way to learn from the batches, since fights can only be read, not watched: (1) the story
of each fight from the three readings, with its turning point and the ten seconds before it,
and a summary over the batch; (2) detectors of missed chances (an enemy healer free to cast
with an interrupter idle, a trained member unhealed with a healer free, a pursuer that could
have been rooted); (3) every match drawing its weights at random, so that several hundred
matches show which weights move the result. Until M6 is finished a new behaviour is built,
checked once for whether it happens in a match, and left.

*Three more behaviours, 2026-11-18, each checked once.*
- **Skald:** its instant shouts, instant mez and instant snare are scored. An instant on a
  healer or caster that is free to cast counts as an interrupt and not as damage off the kill
  target; on one already interrupted it does not, which sends the Skald on to the next. Seen:
  ten shouts a match, eight on healers and casters, half on another enemy than the one before.
  A fighter does not walk for a shout.
- **Bard:** control before heals unless somebody in reach is below 75% health, as a guess (the
  owner's answer is open). Seen: amnesia 8.5, mez 6 a match, and no heal cast at all.
- **Caster pets:** the squad orders a Cabalist's or Enchanter's pet onto its owner's duty or
  kill target. Seen: fifteen orders a match and still no attack by a pet. Not working.
- **Minstrel, tried and taken back:** scoring its shouts, stun shout and single mez turned the
  old brain off for it, and its single mez is a song that began ninety times a match and never
  finished; before, the old brain landed six mezzes a match with the area lullaby. Back to cure
  mez only, and checked: 4.5 mezzes cast and 4 landed a match again.
  3,241 automated tests pass.
- **Caster pets, looked at again:** the order is accepted (the pet's ordered target is set, its
  state is AGGRO, its stance Defensive), yet it stays 50 to 230 units from its owner with no
  target of its own and never attacks, in every one of 48 orders recorded. The Theurgist's pets
  (another brain) attack as before. Cause not found; left for later.
- **Heals for several** (owner, 2026-11-17: "I was using spread heals when needed. I never used
  group heals"; 2026-11-18: "I do not remember the 1.65 era. I would use the one with the best
  power to heal ratio"). With three or more members in reach below 80% health and nobody
  trained, the healer takes its spread heals and its group heals with a cast time that are
  ready, works out what each would restore (a group heal gives each member up to its value; a
  spread heal shares its value times the group's size among the most hurt, up to twice its
  value each) and casts the one that restores the most for each point of power
  (`PvpHealPlan.BestAreaHeal`). On this server at level 50: spread 295 for 110 power, spec group
  heal 283 for 97, baseline group heal 93 for 25, so many lightly hurt get the cheap group heal
  and a few badly hurt get the spread heal. The cast group heal is no longer the last resort of
  the single-heal routine. Seen once in six matches (0.2 a match): somebody is nearly always
  being trained, and that member gets the single heals. 3,245 automated tests pass.
- **Two lines that were already there.** Chant upkeep: the Paladin's hundred recasts are it
  twisting three chants (endurance about 45 a match, armor 10, damage 8); they are instants and
  cost it no swing, so nothing was changed. Raising the dead: the shared healing routine already
  weighs it (a healer not under attack, with the half of its power a resurrection costs, a
  corpse in reach and sight, and at least half the healers left healing); it is rare in fights
  for those reasons (0.3 begun a match against 8 deaths), and nothing was changed.
- **Answering a hold.** A group that is outranged stands as well in half its matches
  (`HoldWhenOutrangedChance`, a chance and not a rule, so that batches hold both openings), for
  up to 25 seconds, longer than the holder's ten, so the holder moves first; and a nearsight is
  worth more on the enemy's mezzer (`NearsightOnEnemyController`): nearsight reaches 2,300,
  farther than any mez, so a Sorcerer walking in can be nearsighted before it casts.
  Checked on four matches with the facts behind the choice recorded (`incoming` on the squad
  line): Midgard drew under 50 in both of its matches and stood 18 seconds in each; Hibernia drew
  66 and 50 and walked in, as it should. A first check of six matches showed no wait at all,
  which was the luck of six draws.
- **The learning tool, first two layers** (`source/tools/pvp_insight.py`, no server needed).
  The story of each fight: the turning point (the second from which the winner stayed ahead for
  good, by numbers alive and the three readings), the loser's state in the ten seconds before
  it, and labels for what went wrong, with a summary over the batch. Missed chances, in seconds a
  match for each group: an enemy healer free with a member idle in range, a pursuer that could be
  rooted with a rooter free in range, an enemy that could be mezzed with a mezzer free in range,
  a trained member unhealed with a healer free in range.
  Tried on six recorded matches (too few to conclude from): the turning point came at 14
  seconds in fights of about two minutes, the side that lost the first member lost all six, and
  the commonest label was three or more of the loser asleep.
Order of work agreed with the owner: finish the brain, with the readings, the group orders and
positioning (hold, kite, extend, push) first; then play matches and record everything; then
analyze and iterate on behavior as deep as wanted. No measure has to be fixed in advance: the
recordings keep every event, so new questions can be asked of old fights. `pvp_analyze.py`
reports the three readings per group from now on.

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

*Purge measured at rank 5, 2026-11-17* (four matches per pairing, all on a wipe, no server
error, no bot left behind): Albion 3 to 1 over Hibernia, Albion 3 to 1 over Midgard, Midgard
and Hibernia 2 each. Purge was used 3 to 7 times a match per team, Group Purge 0.2 to 0.5 times
(members purge themselves before three are asleep, the Group Purge threshold). The first mez no
longer decides anything: nobody was asleep ten seconds after contact in 11 of 12 matches, and
teams were mezzed 1 to 4% of the fight (Midgard 14% against Albion) where rank 1 had 10 to 30%.
Fights last longer (115 to 227 seconds) and twice as many attacks land on healers and casters
(90 to 166 a match).
Open point for the owner: every match starts with fresh bots, so Purge is always ready. In
play it has a 30-minute timer and most fights are fought without it. A scrim should be able to
start with Purge ready, spent, or ready for some members only.

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

*Owner, 2026-11-17:* "I am by no means an 8v8 expert; I am going from memory." Once the owner can
play, every one of their fights is to be recorded, and the recordings and the analyzer used both
to improve the bots and to give the owner insight into their own play. Needed for that: the
recorder taking a human member (today a team is bots only), and a report written for the player
(time spent mezzed or interrupted, what the group's scored choice would have done in the same
spot, what the enemy did to them). The owner also remembers a captioned video explaining the
basics of an 8v8 fight and will look for it: an outside reference to check the bots against.

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

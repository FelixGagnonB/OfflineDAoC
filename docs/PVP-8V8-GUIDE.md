# 8v8: how to play it

You and seven companion bots against groups of eight. Everything is typed as `/8v8 ...` or picked
in the window that `/8v8 setup` opens. This is the player's guide; the plan and what is still
unfinished are in [PVP-8V8-ROADMAP.md](PVP-8V8-ROADMAP.md).

## In short

1. `/8v8 ready` once: your character becomes level 50 with a build's spec and gear
   (`/8v8 ready` alone lists the builds for your class).
2. `/8v8 setup`: pick, then click **Start**.
3. `/8v8 stop` ends whatever is running and removes every 8v8 bot.

## The three ways to play

| | What it is | Groups | Rank and realm points |
|---|---|---|---|
| **Practice** (Lane or Basin) | One fight against one enemy group, for trying things. | 2 | Everybody at the rank you pick. Your timers are reset. Nothing is earned. |
| **Real** (Lane or Basin) | The same one fight, for keeps. | 2 | Everybody at the rank it has earned, from rank 1. Kills earn realm points that are kept. No timer is reset. |
| **Roam** (Emain Macha or Thidranki) | A session that goes on from fight to fight until you stop it. | 6: two of each realm, yours one of them | Always the Real rules. |
| **Prime time** (the frontiers) | An evening: guilds of every kind out in three of the twelve frontier zones (more zones with more groups). | 24 by default: eight of each realm, yours one of them | Always the Real rules. Each guild has the rank of its kind and of what it has earned. |

Picking **Real** does not start the roam: Real and Practice are the rules of a single fight. The
six groups are the **Roam** and **Thidranki** starts. Prime time is typed (`/8v8 prime`); it is
not in the setup window yet.

### A single fight: Lane or Basin

- **Lane**: face to face, 6,000 units apart. The quickest way to a fight.
- **Basin**: opposite ends of a basin in Emain Macha, out of sight. The enemy patrols and you find
  each other.
- The same command or **Start** again is the rematch.

Typed: `/8v8 play mid` (practice, lane), `/8v8 play mid 5 basin` (rank 5, basin),
`/8v8 play mid real` (real rules). Add `follow` to let your Bard, Skald or Minstrel lead on the
road while you stick to it.

### The roam

`/8v8 roam` (Emain Macha) or `/8v8 roam thid` (Thidranki), or the **Roam** and **Thidranki**
starts of the setup window.

- **Six groups**: yours, a second one of your realm, and two of each other realm. Each is its
  realm's main line-up. A group fights any group of another realm.
- **Where they start.** Emain Macha: Albion and Midgard in front of their portal keeps, Hibernia
  in front of Dun Crauchon, each a similar walk from the middle of the zone. Thidranki: each realm in front of its
  portal keep, round a middle; the groups there meet sooner and more often two by two.
- **You lead, and you pick the fights.** Your seven travel behind you, the Bard on its speed
  song, and pass by any enemy group you do not take on. Target an enemy within sight (6,500
  units), or get attacked, and they take their rows, the Bard goes to its endurance song, and
  the group fights.
- **Adding on a fight.** A group of bots that comes on a fight already going on joins it about
  one time in seven (15%) and otherwise stands off until it is over. If the other side adds on its
  realm-mate in a fair fight, it joins after all. Your own group does what you do.
- **Walking by a fight.** Groups that are fighting each other leave you alone as you pass. They
  turn on you only if you attack one of them.
- **A wipe.** About thirty seconds after your whole group is down you all stand up at your start,
  without resurrection sickness. `/release` takes you there at once; your group stands up there
  when the rest of it is down too.
- **Nobody is fought at its own start.** A group of bots leaves a group alone that stands within
  3,500 units of its own start point, unless that group attacks it.
- **What the others are doing.** `/8v8 status` lists every group of the roam: at its start buffing (and when it
  leaves), roaming, fighting whom, resting, waiting by a fight, or wiped (and when it stands up), with how
  many of it are alive and how far it is from you.
- **Resting.** After a fight a group of bots with a member under 60% walks off its road to a place out of
  the way, rests there for up to forty seconds, and comes back. It gets up if an enemy comes within 5,000
  units.
- **Realm points.** A kill is worth what a player of that level and rank is worth, five times
  over, shared among the members of the groups that were fighting the dead one's group and are
  near. Standing by a fight earns nothing. Yours go on your character.
  Every group keeps what it earns from one session to the next: `/8v8 ranks` lists them.

### Prime time

`/8v8 prime` starts an evening in the old frontiers. **It has only been run with bots: no player
has played it yet.**

- `/8v8 prime [zone] [number] [one|all]`. *zone* is where you begin: `emain` if you give none;
  `thid` is Thidranki alone. *number* is the groups of each realm: 8 if you give none, 16 at
  most. `one` keeps the evening in that one zone; `all` puts it in all twelve.
- **The zones.** Twelve, four in each realm's frontier:

  | Frontier | The zone the others ported to | The zones behind it |
  |---|---|---|
  | Hibernia | `emain` (Emain Macha) | `breifine`, `cruachan` (Cruachan Gorge), `collory` (Mount Collory) |
  | Midgard | `odin` (Odin's Gate) | `jamtland` (Jamtland Mountains), `uppland`, `yggdra` (Yggdra Forest) |
  | Albion | `hadrian` (Hadrian's Wall) | `pennine` (Pennine Mountains), `sauvage` (Forest Sauvage), `snowdonia` |

  An evening is not in all of them: it has one zone for every three groups of a realm, so three
  with eight groups, four with twelve, five with sixteen. They are the zone you begin in, Emain
  Macha, and others drawn anew each evening, Odin's Gate and Hadrian's Wall four times as
  readily as a zone behind them. `/8v8 who` begins with the zones of the evening.
- **What a zone is like.** Emain Macha is where **the crowd** is. Odin's Gate and Hadrian's Wall
  are **mixed**. The nine behind them are **clean**: no pack and no group that stays near others
  goes there, a group that adds seldom. In a zone behind, the realm whose frontier it is starts
  in front of one of its keeps; the two others had no keep and no porter there, so each comes in
  at a place of its own at the edge. A keep's guards leave the bots alone. They do not leave you
  alone when the keep is not your realm's, and the points keep 9,000 away from every keep.
- **Who is out.** Guilds, each with a name its members wear, a kind, its own eight and a rank.
  They are kept from evening to evening (`pvp-teams.json` beside the server), with their records.
  The names are invented; you can put guilds you remember in the file.

  | Kind | Share | Walks | Adds on a fight | Its eight | Rank it began at | Where it is |
  |---|---|---|---|---|---|---|
  | Bad | 30% | in a pack of two to five | always, and without counting the enemy | one or two healers, speed six times in ten, anybody | 1 to 3 | the crowd; sometimes a mixed zone; never a clean one |
  | Casual, stays near others | 15% | 1,800 to 3,500 behind a group of its realm (maybe yours) | nine times in ten | two healers, speed, anybody | 2 to 4 | where its group is; it picks none in a clean zone |
  | Casual 8v8 | 15% | alone | eight times in ten | the main group with one or two changed | 3 to 5 | the crowd and the mixed zones; a clean one now and then |
  | Decent 8v8 | 15% | alone | never | the main group | 4 to 6 | everywhere |
  | Strong 8v8 | 15% | alone | never | the main group | 6 to 8 | the clean zones first, then the mixed |
  | Elite 8v8 | 5% | alone, leaning to the quiet ground | never | the main group | 8 to 10 | the clean zones; never the crowd |
  | Elite, hunts the zerg | 5% | alone, toward the nearest enemy groups that walk together and that nobody is fighting yet | never: a pack somebody is on already is theirs | the main group | 8 to 10 | the crowd; sometimes a mixed zone; never a clean one |

  A guild's rank does not follow yours: an elite team outranks a new character by a lot.
- **Where they go.** Nobody walks a fixed round. At each point a guild picks where to go next,
  not back the way it came. One that does not add stands by a fight it comes on for 45 seconds
  for it to end, then turns back or walks on past. One that does not count heads (the bad
  groups, and the hunters) walks into anything; the others do not start on more groups than
  they have together.
- **A guild with too many on it falls back as one.** When ten or more aim at its eight and they
  are on its healers and casters, its caller moves the whole group back 1,800 units at a time,
  away from the enemy and toward its start where the ground allows: healers and casters run to
  their spots without casting, the fighters stand between them and the enemy and take whoever
  follows, nobody is raised, and then it stands and fights before the next leg. A sharp guild
  is on its way within a second and a half, a casual one after six to twelve; a bad group does
  not do it at all. Run with bots only; it carries a group thousands of units and has not yet
  made eight beat sixteen. With too many on it a guild raises nobody. Your own group still moves
  back only on your call, unless you turn `/8v8 lead on` (see "Talking to your group").
- **A guild that has lost breaks off.** With half of its eight dead, or both its healers, and the
  enemy half again as many as its side, the ones left stop fighting and run: away from the enemy,
  or toward their start when the enemy is all round them. A team of bots follows them 3,000
  units and no farther; you may follow as far as you like. Once no enemy team has been near them
  for a few seconds they are gone, and the guild stands up whole at its start half a minute
  later, as after a wipe; it does not count as one. A bad group does not break off, and the last
  two of any group against three times their number stand and fight. Run with bots only.
- **The evening moves.** Guilds come out one by one over the first minutes. A guild logs off
  when it has been wiped three times, or after 25 to 50 minutes, and another of its realm comes
  out. A guild that walks alone and is wiped ports one time in three to another zone of the
  evening that its kind goes to.
- **What you know.** Of your own realm: `/8v8 who` (the zones of the evening and what each is
  like, then the guilds that are out, rank, classes, zone, whom they walk with) and
  `/8v8 status` (what each is doing). Of the others: how many groups are out, and what your
  realm calls out in realm chat when it sees them, such as "2fg albs at south road (Emain)",
  "fighting 1fg mids at north road (Odin's)", "wiped at Crimthain (Breifine), 3fg mids", and when a group breaks off, "running from 2fg albs at
  south road (Emain)" or "4 mids on the run at south road (Emain)". The
  names of the places are made up: in the zones behind, a point is called by the keep it is
  near or by its side of the zone.
- **Going elsewhere.** `/8v8 go <zone>` with one of the zones of the evening: your group ports
  to your realm's start there. Not out of a fight. Walking from one zone into the next one of
  the evening also makes your group of that zone; the bots do not cross a zone's border on foot.
- **Grudges.** A guild your group has wiped twice in an evening comes looking for you. One you
  added on (you came into a fight it had with somebody else) comes looking too, and joins the
  next fight of yours it comes on, whatever its kind. `/8v8 rivals` lists the enemy guilds you
  have wiped or been wiped by, over all evenings.
- **How the groups held together.** `source/tools/pvp_group.py` reads a recording of an evening:
  each kind's spread, how far its fights carried, every time a group fell back as one, and every
  time one broke off and whether it got away.
- **One fight, close up.** `source/tools/pvp_fight_draw.py <recording> "<team>" <from s> <to s> <out.png>`
  draws a team's fight on a grid of 1,000 units: its middle with the time along it, its members
  every five seconds, the teams near it, where its place went back a leg, where it broke off and
  who died where.
- **Pictures.** `source/tools/pvp_map_draw.py` draws a zone's points and legs on the client's map
  of it, and, from a recording of an evening, where every team went, fought and was wiped.

## Your group

- The setup window's **Your group** line picks another line-up of your realm where there is one
  (Hibernia: `hib-main`, or `hib-2bard` with two Bards). Your own class takes its place in it.
- Your companions keep their names from one session to the next.

## Realm abilities

A bot has the realm ability points of its rank (one a realm level: 10 at rank 2, 40 at rank 5) and
spends them in a fixed order for its class. A step it cannot pay for yet is passed over and comes
first again when the points are there, as if a respec were free.

| Class | What it buys first, in order |
|---|---|
| Druid | Tireless, Group Purge, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Battery of Life, Raging Power |
| Cleric | Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Battery of Life, Bunker of Faith, Raging Power |
| Healer | Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Perfect Recovery, Battery of Life, Raging Power |
| Shaman | Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power |
| The casters | Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 2, Wild Power 2 (the Enchanter: Brilliant Aura of Deflection after Mastery of Concentration) |
| Minstrel | Tireless, Purge, Speed of Sound, Mystic Crystal Lore 1, First Aid 2, Ignore Pain |
| Bard | Tireless, Purge, Speed of Sound, Mystic Crystal Lore 2, Ameliorating Melodies, Raging Power, First Aid 2, Ignore Pain |
| Skald | Tireless, Purge, Speed of Sound, First Aid 2, Ignore Pain, Augmented Strength 3 |
| Armsman, Warrior, Hero | Tireless, Purge (4 points), Determination 3, First Aid 2, Ignore Pain, Determination 4, Augmented Strength 2, Mastery of Pain 2 (the Armsman: Soldier's Barricade after Ignore Pain) |
| Mercenary, Blademaster, Berserker, Savage | Tireless, Purge (4 points), Determination 3, First Aid 2, Ignore Pain, Augmented Dexterity 2, Mastery of Pain 2, Determination 4, Dualist's Reflexes 2 |
| Paladin, Champion | Tireless, Purge, Determination 3, First Aid 2, Ignore Pain (the Paladin: then Faith Healing) |
| Warden | Tireless, Purge, Mystic Crystal Lore 2, First Aid 2, Ignore Pain |

Every class's full order, with what each step costs and at which rank it is reached, is in
[PVP-8V8-REALM-ABILITIES.md](PVP-8V8-REALM-ABILITIES.md). The orders themselves are in
`bots/pvp/PvpRealmAbilities.cs`, one line a class, to be corrected there.
Speed of Sound is the Bard's and the Skald's as well as the Minstrel's (10 points, from rank 3 level 1);
a player of those classes can train it too.

When a bot presses what it has bought (all of it only in a fight; the numbers are in
`pvp-weights.json` under `Abilities`):

| Ability | When |
|---|---|
| Purge, Group Purge | A mez or stun worth it (a healer or a mezzer first; two or more members for the group one). |
| Speed of Sound | On your `/8v8 call retreat` with the enemy near; with two members rooted or snared; on a push while the group's fighters are still more than 1,200 units from any enemy; when the group falls back (extend, regroup) with two of its healers and casters under an enemy fighter. |
| Mastery of Concentration | A healer that is interrupted while a member is below half; a caster interrupted by a fighter on it. |
| Battery of Life | Two members hurt and 1,000 hit points missing. |
| Bunker of Faith, Soldier's Barricade | Two members with a fighter on them and two hurt. |
| Brilliant Aura of Deflection | Two enemy casters on the group and somebody hurt. |
| Ameliorating Melodies | Two members hurt. |
| Faith Healing | Two members below 40%. |
| Perfect Recovery | A dead member within 1,500, a healer first. |
| Ignore Pain | Below 30%. |
| Avoid Pain, Armor of Faith | A fighter on it and its health below 60% (75%). |
| The Empty Mind | Below 60% with no fighter on it. |
| Mystic Crystal Lore, Raging Power, Second Wind, First Aid | Out of power; out of endurance; hurt after the fight. |

No bot buys the realm abilities it does not know how to use: the areas of damage or control
(Volcanic Pillar, Negative Maelstrom, Rune of Decimation, Thornweed Field, Ichor of the Deep,
Static Tempest, Wrath of the Champion, Bladedance, Whirling Dervish), the melee ones (Trip,
Grapple, Battle Yell, the style abilities, the Rains, Fury of the Gods, Hail of Blows), the pet
ones, Majestic Will and Concentration.

**Your own group abilities reach your companions.** Group Purge, Speed of Sound, Bunker of Faith,
Soldier's Barricade, Brilliant Aura of Deflection, Ameliorating Melodies and Faith Healing used to
look for the players of the group only, so they did nothing for seven bots.

## Talking to your group

Your companions say in group chat what the group is doing: the assist target, an order (push,
regroup, hold, extend), "3 on you!", a member out of power, a mez that put two or more of the enemy
to sleep, a Purge used. `/8v8 calls off` silences them.

`/8v8 lead on` lets your companions' own caller move the group in a roam or a prime-time evening:
when ten or more aim at your eight and they are on your healers and casters, your seven fall back
1,800 units as one, as the guilds of bots do, and say so four times in capitals in group chat
("KITE BACK! 12 ON OUR 8! KITE BACK!"), then "BACK AGAIN! KEEP MOVING!" for each further leg and
"STAND AND FIGHT HERE!" when they stop. It is off unless you turn it on, because a group that
moves by itself leaves you behind if you miss the call; a call of your own for anything else
(push, regroup, hold, engage, retreat) stands over it. They never break off by themselves: the
retreat is yours to call. **Built 2026-11-24, automated tests only: nobody has played with it.**

You can call too (make macros of these):

| Command | What the group does |
|---|---|
| `/8v8 call assist` | The damage dealers go for your target while it lives (45 seconds at most). |
| `/8v8 call control` | A mez, stun, root or nearsight goes to your target first (20 seconds). Also `mez`, `cc`. |
| `/8v8 call push` | Everybody presses forward. An order stands 12 seconds. |
| `/8v8 call rest` | After a fight: everybody recasts the buffs it keeps up (its own, the group's resistances, its pet), then rests where you stand and regains health, power and endurance, about twenty seconds from empty to full; the Bard plays its endurance song. It ends after two minutes, when you walk on, call something else, or somebody is attacked. Also `sit`, `buff`. |
| `/8v8 call regroup` | Everybody comes back to you and goes on fighting from there. Also `back`. |
| `/8v8 call retreat` | Everybody drops what it is doing and runs with you: no attack, no cast, no answer to an attack, the speed song on. It stands 30 seconds; call it again to keep running, or any other call to end it. Also `run`, `follow`. |
| `/8v8 call hold` | The group stands where it is and lets them come. |
| `/8v8 call engage` | Back to a normal fight: close, mez, kill the called target. |
| `/8v8 call extend` | The kite: your healers and casters step back, straight away from the enemy, again and again while an enemy is within 1,800 units, casting between steps; your fighters turn on whoever follows and stay with the group. It stands 12 seconds; call it again to keep kiting. Also `kite`. |

## The dead

A dead companion or enemy is meant to lie where it fell, to be clicked and raised like a dead
player. That display has not been confirmed in a client yet: if a dead bot stands, vanishes or
cannot be clicked, `/8v8 corpse` switches how it is shown (`player`, `body`, `fall`, `ghost`,
`off`).

## Recordings

Every fight and every roam is recorded beside the server (`runtime/server/logs/scrims` and
`logs/roams`). They are your own play data and are not part of any release.

- `python source/tools/pvp_roam.py <logs/roams>` counts the newest roam: its events, every fight,
  and a line for each group. `--html` writes the roam as a page for the browser: the zone from
  above with every member as a dot, to play and scrub, and a view that follows one group.
  `--cut` writes each fight of exactly two groups as a match.
- `python source/tools/pvp_replay.py`, `pvp_referee.py` and `pvp_analyze.py` read a match.

## What has and has not been tried in a client

Played by the owner: Practice and Real fights in the lane and the basin, and the roam in Emain
Macha. Built and run with bots only, not yet seen with a player: the dead lying down and clickable, the
roam in Thidranki, standing
up after a wipe and `/release` in a roam, the Roam starts of the setup window, `/8v8 call control`,
the calls for a landed mez and a used Purge, and a group of bots waiting out a fight or raising
its dead before it walks on. The realm abilities of 2026-11-22 (the orders, the new active ones,
your group abilities on your companions) have passed their automated tests and nothing else.

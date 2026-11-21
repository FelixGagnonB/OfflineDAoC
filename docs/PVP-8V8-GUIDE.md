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

Picking **Real** does not start the roam: Real and Practice are the rules of a single fight. The
six groups are the **Roam** and **Thidranki** starts.

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
  on the hill by the tower in front of the Albion gate. Thidranki: each realm in front of its
  portal keep, round a middle; the groups there meet sooner and more often two by two.
- **You lead, and you pick the fights.** Your seven travel behind you, the Bard on its speed
  song, and pass by any enemy group you do not take on. Target an enemy within sight (6,500
  units), or get attacked, and they take their rows, the Bard goes to its endurance song, and
  the group fights.
- **Adding on a fight.** A group of bots that comes on a fight already going on joins it three
  times in ten and otherwise stands off until it is over. If the other side adds on its
  realm-mate, it joins after all. Your own group does what you do.
- **A wipe.** About thirty seconds after your whole group is down you all stand up at your start,
  without resurrection sickness. `/release` takes you there at once; your group stands up there
  when the rest of it is down too.
- **Nobody is fought at its own start.** A group of bots leaves a group alone that stands within
  3,500 units of its own start point, unless that group attacks it.
- **Realm points.** A kill is worth what a player of that level and rank is worth, five times
  over, shared among the members of the groups that were fighting the dead one's group and are
  near. Standing by a fight earns nothing. Yours go on your character.
  Every group keeps what it earns from one session to the next: `/8v8 ranks` lists them.

## Your group

- The setup window's **Your group** line picks another line-up of your realm where there is one
  (Hibernia: `hib-main`, or `hib-2bard` with two Bards). Your own class takes its place in it.
- Your companions keep their names from one session to the next.

## Talking to your group

Your companions say in group chat what the group is doing: the assist target, an order (push,
regroup, hold, extend), "3 on you!", a member out of power, a mez that put two or more of the enemy
to sleep, a Purge used. `/8v8 calls off` silences them.

You can call too (make macros of these):

| Command | What the group does |
|---|---|
| `/8v8 call assist` | The damage dealers go for your target while it lives (45 seconds at most). |
| `/8v8 call control` | A mez, stun, root or nearsight goes to your target first (20 seconds). Also `mez`, `cc`. |
| `/8v8 call push` | Everybody presses forward. An order stands 12 seconds. |
| `/8v8 call regroup` | Everybody comes back to you. Also `back`, `retreat`. |
| `/8v8 call hold` | The group stands where it is and lets them come. |
| `/8v8 call engage`, `extend` | Back to a normal fight; chase them when they back off. |

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
its dead before it walks on.

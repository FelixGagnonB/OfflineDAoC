# 8v8 v0.1: commands and how-to

For the game you play (`D:\OfflineDAoC`). The long version of everything here is in
[PVP-8V8-GUIDE.md](PVP-8V8-GUIDE.md); building, installing and undoing are in
[PVP-8V8-HANDOVER.md](PVP-8V8-HANDOVER.md).

## The first time

1. Start the game with its launcher (`START OFFLINE DAOC.cmd`). Leave the gamebot population at 0.
   The scrim server must not be running: only one server can run at a time.
2. Make a character of the realm you want. Its class does not matter: the next step sets it.
3. Type `/8v8 ready`. It lists your realm's builds (for Hibernia: `druid-regrowth`,
   `druid-nurture`, `bard`, `hero`, `blademaster`, `eldritch-light`, `enchanter-light`, ...).
4. Type `/8v8 ready <build> confirm`, for example `/8v8 ready hero confirm`. Your character
   becomes level 50 with that build's class, spec and gear. This is for good.
5. Your evening begins by itself: you are taken to your realm's start in Emain Macha, your seven
   companions join you, and guilds of all three realms come out over the first minutes.
6. Once, when you like: `/8v8 guild <name>` gives your group a guild (your companions wear its
   name). `/8v8 guild lineup hib-2bard` picks the two-Bard group from your next evening on.

## Every evening after that

You log in, and a few seconds later the evening begins: same zone, same line-up and same number
of groups as last time. It ends when you leave the game. Everything earned is kept: your realm
points, your companions', the guilds', and who has wiped whom.

Everybody started at realm rank 1 level 1. Ranks only move while you are in the game.

## Your commands

| Command | What it does |
|---|---|
| `/8v8 ready` | Lists your realm's builds. `/8v8 ready <build> confirm` makes this character level 50 with that build. |
| `/8v8 guild` | Says what guild you have. `/8v8 guild <name>` forms one, free, wherever you stand (letters and spaces, 30 at most). |
| `/8v8 guild lineup` | Says your line-up and lists your realm's. `/8v8 guild lineup <name>` sets it from your next evening on. |
| `/8v8 who` | Each zone of the evening: how many groups every realm has in it, and of what kinds. |
| `/8v8 go <zone>` | Your group ports to your realm's start in another zone of the evening. Not in a fight. |
| `/8v8 rivals` | The enemy guilds you have wiped or been wiped by, and how often. |
| `/8v8 ranks` | Your realm rank and your companions'. |
| `/8v8 lead on` / `off` | On (as built): your companions fall back as one by themselves when too many are on the group, and say so in capitals. Kept between logins. |
| `/8v8 calls on` / `off` | Your companions' calls in group chat. |
| `/8v8 corpse player`, `body`, `fall`, `ghost` or `off` | How a dead bot is shown. |
| `/8v8 call <word>` | Your call to the group (below). |

Zones for `/8v8 go`: `emain`, `odin`, `hadrian`, `breifine`, `cruachan`, `collory`, `pennine`,
`sauvage`, `snowdonia`, `jamtland`, `uppland`, `yggdra`. Only the zones of tonight's evening
answer; `/8v8 who` names them.

## Your calls

Put the ones you use on quickbar buttons with `/macro <name> /8v8 call <word>`.

| Call | What the group does | Stands for |
|---|---|---|
| `/8v8 call assist` | The damage dealers go for your target. | while it lives, 45 s at most |
| `/8v8 call control` | A mez, stun, root or nearsight goes to your target first. Also `mez`, `cc`. | 20 s |
| `/8v8 call push` | Everybody presses forward. | 12 s |
| `/8v8 call engage` | Back to a normal fight: close, mez, kill the called target. Also `go`. | 12 s |
| `/8v8 call extend` | The kite: healers and casters step back again and again, fighters take whoever follows. Also `kite`. | 12 s; call again to keep kiting |
| `/8v8 call regroup` | Everybody comes back to you and fights from there. Also `back`. | 12 s |
| `/8v8 call hold` | The group stands where it is and lets them come. Also `wait`. | 12 s |
| `/8v8 call retreat` | Everybody drops everything and runs with you, sprinting. Also `run`, `follow`. | 30 s; call again to keep running |
| `/8v8 call rest` | After a fight: cures, buffs, then sitting until full. Also `sit`, `buff`. | 2 minutes, or until you move or are attacked |
| `/8v8 call die` | You are dead and the fight is lost: the group gives it up and stands up at its start. Also `wipe`, `giveup`. | at once |

Repeating a call only renews it. A retreat is ended by any other call.

## How to

- **Find a fight.** `/8v8 who` says where the groups are; realm chat tells you what your realm
  sees ("2 groups of Midgard at the north road"). Walk there, or `/8v8 go <zone>`.
- **Start a fight.** Out of a fight, target an enemy: your group forms up on that enemy's
  group. Hit it, and they go in.
- **Pick the kill target.** Target an enemy and `/8v8 call assist`. Otherwise the group calls
  its own.
- **Keep a second group out.** A group that stands by your fight has left you alone. Clicking
  or tabbing to one of its members does nothing while you fight. It comes in if you hit one of
  them or call assist or control on one.
- **Get out.** `/8v8 call retreat` and run; your Bard uses Speed of Sound when the enemy is
  near. With lead on, your group already falls back by itself when ten or more are on it.
- **After a fight.** `/8v8 call rest`: disease, poison and nearsight are taken off everybody,
  buffs are recast, the group sits.
- **When you die.** A healer raises you if the fight allows. If the whole group goes down, it
  stands up at your realm's start about half a minute later; `/release` takes you there at
  once. If you are dead and the fight is lost, `/8v8 call die` ends it without waiting.
- **Spend your realm points.** At your class trainer, as in the game. Your companions spend
  theirs by themselves, in the orders of
  [PVP-8V8-REALM-ABILITIES.md](PVP-8V8-REALM-ABILITIES.md).
- **Change your group.** `/8v8 guild lineup <name>`, from the next evening. Your own class
  takes its place in the line-up.
- **Look at a fight afterwards.** See the handover page: `pvp_myfights.py` on the newest file
  in `D:\OfflineDAoC\runtime\server\logs\roams`.

## What your group says in capitals

| It says | It means |
|---|---|
| `INC! 2FG MIDS NORTH-WEST, 4,500!` | Enemies sighted before a fight: how many groups, which realm, which side, how far. |
| `ADD! 8 MORE ALBS FROM THE NORTH!` | Another group is coming for your fight. |
| `TOO MANY! 12 ON OUR 8! CALL IT: KITE OR RETREAT!` | Only with lead off: the group waits for your call. |
| `KITE BACK! 12 ON OUR 8! KITE BACK!` | With lead on: the group falls back as one, 1,800 units. |
| `BACK AGAIN! KEEP MOVING!` | A further leg of the same. |
| `PUSH! PUSH! ALL ON THEM!`, `EXTEND! FALL BACK AND KITE!`, `REGROUP! BACK TO THE HEALERS!` | The group's order changed. |
| `THIS ONE IS LOST! 3 OF US LEFT, 14 OF THEM! RETREAT?` | Half the group or both healers are dead and you are outnumbered. |

## The kinds of guild you meet

| Kind | How you know it |
|---|---|
| Bad | In a pack of two or three groups; mixed line-ups; adds on everything; slow hands; few have Purge. |
| Casual | Walks near another group of its realm, or alone and adds; a little slow. |
| Decent | The realm's main group, alone; does not add. |
| Strong | The same, sharper; regroups sooner. |
| Elite | Alone on quiet ground; leaves a fight a second group comes into. |
| Zerg hunters | Elite; goes where the packs are. |

## If something is wrong

- **The evening does not begin.** Your character must be level 50 from `/8v8 ready`. If you are
  told a fight or an evening is already running, leave the game and come in again.
- **You want the test commands** (`/8v8 prime`, `/8v8 stop`, `/8v8 status`, a trial at a picked
  rank): put an empty file `pvp-test-commands.txt` in `D:\OfflineDAoC\runtime\server` and
  restart the server.
- **You want a fresh world.** With the server closed, delete `pvp-teams.json` in
  `D:\OfflineDAoC\runtime\server`: new guilds at rank 1 level 1 next evening, rivals forgotten.
- **You want the game as it was before v0.1.** The undo line is in the handover page; the backup
  of 2026-11-25 is `D:\OfflineDAoC\deployment-backups\scrim-20261125-082823`.

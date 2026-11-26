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
5. Your evening begins by itself, in the frontier: guilds of all three realms come out over the
   first minutes. You are not moved. `/8v8 go emain` takes you to your realm's start in Emain
   Macha, and your seven companions join you there.
6. Once, when you like: `/8v8 guild <name>` gives your group a guild (your companions wear its
   name; a new character is in its realm's starter guild, so `/gc quit` first), and
   `/8v8 guild emblem` picks its emblem. `/8v8 guild lineup hib-2bard` picks the two-Bard group from your next evening on.

## Every evening after that

You log in, and a few seconds later the evening begins: same line-up and same number of groups
as last time. You stay where you logged out. In a frontier zone your seven are beside you at
once; anywhere else they wait off the field until you are in the frontier (`/8v8 go <zone>`, or
walk in). The evening goes on wherever you go, the capital too, and ends when you leave the
game. Everything earned is kept: your realm
points, your companions', the guilds', and who has wiped whom.

Everybody started at realm rank 1 level 1. Ranks only move while you are in the game.

## Your commands

| Command | What it does |
|---|---|
| `/8v8 ready` | Lists your realm's builds. `/8v8 ready <build> confirm` makes this character level 50 with that build. |
| `/8v8 guild` | Says what guild you have. `/8v8 guild <name>` forms one, free, wherever you stand (letters and spaces, 30 at most). |
| `/8v8 guild emblem` | Opens the game's emblem window, wherever you stand and at no cost: your guild's emblem goes on your cloak and shields and on your companions'. Pick again whenever you like. |
| `/8v8 guild lineup` | Says your line-up and lists your realm's. `/8v8 guild lineup <name>` sets it from your next evening on. |
| `/8v8 evening` | Begins your evening when none is running. It begins by itself when you log in and runs until you log out, so you seldom need this. |
| `/8v8 who` | Each zone of the evening: how many groups every realm has in it, and of what kinds. |
| `/8v8 go <zone>` | You and your group port to your realm's start in a zone of the evening, from anywhere (the capital too). Not in a fight. |
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
| `/8v8 drive [on\|off]` | Who drives your group: one of your companions (see below), or you. Alone it says which. Kept from one login to the next. | until you change it |
| `/8v8 call abilities` | Each companion says in group chat which of its active realm abilities are up, which are down and for how long ("up: Purge | down: Group Purge 12:40"). Also `ra`, `timers`, `cd`. | at once |

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
  buffs are recast, the group sits. A buff with a timer (a Druid's three resistances, an
  Eldritch's two barriers: twenty minutes) is cast again once half of it has run, so that it
  does not run out in the next fight. At a rest with more than ten minutes of it left
  nothing is cast again. So that none runs out in a fight, a group with a companion at the
  wheel also rests after a fight for a timer that is half run, hurt or not, and on the road
  it stops where it is when only five minutes of one are left. When you drive, the companion
  says "My timers run out soon: a rest, and I cast them again.": the rest is yours to call.
- **Who died.** When somebody of your realm dies or kills in your frontier, the system window
  says who, by whom and where: "Arngenmund was just killed by Jade in Agramon."
- **After you stand up at your start.** Your group is whole (health, power, endurance) while
  its buffs go on, for its first minute and as long as it has met nobody: no waiting at the
  keep.
- **When you die.** A healer raises you if the fight allows. If the whole group goes down, it
  stands up at your realm's start about half a minute later; `/release` takes you there at
  once. If you are dead and the fight is lost, `/8v8 call die` ends it without waiting.
- **Spend your realm points.** At your class trainer, as in the game. Out of the frontier your
  companions wait off the field and the evening goes on without your group; `/8v8 go <zone>`
  takes you back with them. Your companions spend
  theirs by themselves, in the orders of
  [PVP-8V8-REALM-ABILITIES.md](PVP-8V8-REALM-ABILITIES.md).
- **Change your own build or class.** `/8v8 ready <build> confirm`, out of combat: your evening
  is ended for the change and a new one begins when it is done.
- **Change your group.** `/8v8 guild lineup <name>`, from the next evening. Your own class
  takes its place in the line-up.
- **Look at a fight afterwards.** Double-click `source\tools\review_my_fights.cmd`: see
  *Reviewing your fights* below.

## Reviewing your fights

A page in your browser for each evening: pick a fight, watch it from above, read what happened.

**To make the pages and open them:** double-click `source\tools\review_my_fights.cmd` in
`C:\Workspace\OfflineDAoC`. Play an evening first: the game writes the recording the pages are
made from; the server may be running or closed. It reads your newest evenings, writes the pages
to `D:\OfflineDAoC\runtime\logs\fights` and opens the list. From a command line:

    python source\tools\pvp_fights_page.py --open            (the game you play)
    python source\tools\pvp_fights_page.py --open --scrim    (the copy for trying things)
    python source\tools\pvp_fights_page.py --open --last 20  (more evenings than the newest eight)

An evening already written is not read again, so only the first run takes a few seconds an
evening. The pages are plain files with their data in them: nothing is sent anywhere.

**On the list:** the evenings, newest first, each with its fights in a line (when, how long,
against how many groups, killed and lost, how it ended). Click an evening.

**On an evening's page:**

1. **Pick the fight** in the box at the top. The page opens on the last one.
2. **Watch it.** `Play` (or the space bar) runs it; the speed is beside it (2x to start with);
   drag the bar or use the arrow keys to go second by second. North is up, the grid is 1,000
   units.
   - A square is a fighter, a circle a caster, a triangle a healer; red is Albion, blue Midgard,
     green Hibernia. Your group has a white edge, and you a double ring.
   - A purple ring is asleep, yellow stunned, brown rooted; a cross is dead; the bar under each
     mark is its health.
   - The red cross-hair is your group's kill target; the thin yellow lines show whom each of
     your members is on; the grey tails are the last eight seconds of your group's way.
   - The box beside the speed picks the view: close, on your group (the names of the others
     show there), wider, or the whole fight at once.
   - Under the picture: your group's order at that second, how many of each side are alive, and
     how many enemies aim at your group.
3. **The strip under it** is the fight at a glance: damage your group dealt each second (up,
   green) and took (down, red), a line for how many of your eight were alive, a red tick where
   one of yours died and a green one where an enemy did. Click anywhere on it to go there.
4. **On the right:** every member at that second, yours first, then each enemy group with its
   kind and rank: health, power, and whether it is asleep, stunned, rooted, nearsighted (NS) or
   diseased.
5. **"What the recording says"** is the fight in a dozen sentences: the numbers against you,
   the damage both ways and how much your healers gave back, when your first member fell and to
   whom, how long each side was held by control, how long your healers could not act, how much
   of the fight an enemy fighter stood on your healers and casters, your healers' power at the
   end, which abilities each side pressed, how often your group's order changed, and what you
   called.
6. **"The groups"** lists who was in it (kind, rank, what each dealt to you and took from you)
   and your group member by member (dealt, took, healed).
7. **"What happened, in order"** is every death, call, order, ability and group that came in.
   Click a line and the picture goes to that second.

**What to look for:**

- *Were you simply outnumbered?* The first sentence, and "aiming at you" under the picture.
- *Did your mez do anything?* "The enemy's members were asleep N% of the time".
- *Whom was the enemy on?* Watch their fighters' squares arrive: the member with squares on it
  and a falling bar is the one being trained.
- *Were your healers free?* "Your healers could not act" and "an enemy fighter stood on one of
  your healers or casters".
- *Did you run dry?* The healers' power at the end, and the blue bars on the right.
- *Did a second group come in?* The lines "... is on you" and "ADD!" in the list of what
  happened.
- *Did the retreat work?* Go to your "You call: retreat" line and play from there in the wide
  view.

A link can name a fight and a second: add `#f=2&t=45` to the page's address for the second
fight, 45 seconds in.

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

Every guild of bots wears an emblem of its own on its cloaks and shields, made from its name and
the same every evening, so a group can be told from afar by its colours.

## Who drives your group

Unless your own character is a Bard, Sorcerer, Minstrel, Skald or Pacification Healer, one of
your companions drives: the Bard; else the Sorcerer, then the Minstrel; else the Pacification
Healer, then the Skald. `/stick` to it.

- It takes the group from point to point of the zone and at the enemy groups it meets, as a
  decent group does: not into a fight that is on, not into more groups than you are, and by
  the rules of the zone. It goes only for a group one of your seven sees (or has seen in the
  last twenty seconds): it does not know where a group is that is behind a hill. They see
  4,000 units ahead, less behind (`"PlayerGroupSeesWithin"` under `"Real"` in
  `runtime\server\pvp-weights.json`), and that is where they call "INC!". It does not go
  back to one of the last three points it was at while another way leads on, and to its
  resting place the group walks as one, behind it.
- In group chat it says "Stick, moving out: mid road." when it sets off, "Waiting for you,
  <name>." when you are more than 1,500 units behind (it goes on when you are within 800), and
  "Resting here." after a fight. It waits thirty seconds after the group has stood up.
- It keeps your pace: the group runs no faster than you can at that moment (after a fight,
  until the speed song is on you again), and a little slower while you are more than 300 units
  behind the driver, so that you come up.
- Your calls hold as ever, and an enemy you strike or call is the group's. It stays by you
  when you are down. It never takes the group off the field: that is yours (`/8v8 call die`,
  `/release`).
- `/8v8 drive off` gives you the wheel (kept); `/8v8 drive on` gives it back; `/8v8 drive`
  says who drives.

## Where to find which fight

An evening has twelve groups of each realm (yours one of them), in three frontier zones and the
8v8 zone. A guild plays for 25 to 50 minutes, or until it has been wiped three times, and logs
off; another comes out in its place, and when all have been out, one that left ten minutes ago
or more comes out again: the evening lasts as long as you play. `/8v8 who` lists the zones and what is in each; `/8v8 go <zone>` takes you there.

| Zone | What you find |
|---|---|
| Emain Macha (the crowd) | The packs of bad groups, the groups that add, the hunters. Rarely one against one. |
| Odin's Gate, Hadrian's Wall (mixed) | Some of everything. |
| The nine behind them (clean, nobody adds) | Decent, strong and elite groups, each alone. Nobody joins a fight that is on. |
| Thidranki (8v8 only) | Guilds of every kind, each alone, and nobody adds. Small: a fight is never far. |

Of ten groups about two are bad, two casual, and six of the kinds that fight one group against
one (decent, strong, elite, hunters).

## The 8v8 zone

One zone of every evening is for one group against one: **Agramon** in the game you play
(Thidranki where no other is set). `/8v8 go agramon` takes you and your group to your realm's
start there; `/8v8 who` lists it as "8v8 only".

- Two guilds of each realm are in it, of every kind, each by itself: no packs.
- Nobody joins a fight that is on. A group that comes on one waits, or walks round it.
- A group picks one enemy group, and that one is then nobody else's: two do not start on one.
- After a fight, the decent, strong and elite groups leave the winner alone until it is whole
  again (two minutes at the most), and start nothing hurt themselves. A bad or a casual group
  attacks as it finds you.
- You are held to none of it by the game: your companions go for what you attack or call.

Which zone it is, how many guilds are in it and how long a group is left to rest are settings
(`EightZone`, `EightZoneGroups`, `EightZoneRestSeconds`; see the handover page). Thidranki has
water in it. The game's data has no diving there (you swim on top, as in a frontier river, and
need no potion), but whether the water gets in the way of a fight is not known until it is
played: if it does, `"EightZone": "collory"` (or any zone's word) moves the 8v8 zone.

## The kinds of guild you meet

| Kind | How you know it |
|---|---|
| Bad | In a pack of two or three groups; mixed line-ups; adds on everything; slow hands; few have Purge. |
| Casual | Walks near another group of its realm, or alone and adds; a little slow. |
| Decent | The realm's main group, alone; does not add. |
| Strong | The same, sharper; regroups sooner. |
| Elite | Alone on quiet ground; leaves a fight a second group comes into. |
| Zerg hunters | Elite; goes where the packs are. |

## Your macros on another character

The game keeps a character's macros on your computer, one file a character
(`%APPDATA%\Electronic Arts\Dark Age of Camelot\OfflineDAoC03\<Name>-5.ini`), so a new character
has none. To give it another character's:

    python source\tools\copy_macros.py                 lists your characters and their macros
    python source\tools\copy_macros.py Jade Mycleric   Jade's macros and macro buttons go to Mycleric

Do it at the character selection or with the game closed, not while the character you copy to
is in the game. Every macro is copied, and every quickbar button that is a macro goes to the
same place on the same bar; a place where the other character has a spell or a style keeps it.
`--layout` copies the windows, chat colours, key binds and camera too. The file that is changed
is copied first (`...ini.bak-<date>-<time>`); a character that has never left the game gets its
file made from the first one's.

## If something is wrong

- **The evening does not begin.** Your character must be level 50 from `/8v8 ready`. Type
  `/8v8 evening`. If you are told a fight or an evening is already running, leave the game and
  come in again.
- **You want the test commands** (`/8v8 prime`, `/8v8 stop`, `/8v8 status`, a trial at a picked
  rank): put an empty file `pvp-test-commands.txt` in `D:\OfflineDAoC\runtime\server` and
  restart the server.
- **You want a fresh world.** With the server closed, delete `pvp-teams.json` in
  `D:\OfflineDAoC\runtime\server`: new guilds at rank 1 level 1 next evening, rivals forgotten.
- **You want the game as it was before v0.1.** The undo line is in the handover page; the backup
  that puts the 0.37b server back is `D:\OfflineDAoC\deployment-backups\scrim-20261125-082823`.

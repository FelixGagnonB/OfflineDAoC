# 8v8: running it yourself

Written 2026-11-25 for the owner, to keep the 8v8 going without an assistant. The commands and a
how-to for playing are in [PVP-8V8-QUICKSTART.md](PVP-8V8-QUICKSTART.md). Everything here is
a command to type or a file to look at; the why of each thing is in
[PVP-8V8-GUIDE.md](PVP-8V8-GUIDE.md) (how it plays) and [PVP-8V8-ROADMAP.md](PVP-8V8-ROADMAP.md)
(what was built, when and from which of your remarks).

## What is where

| Thing | Where |
|---|---|
| The source, and these pages | `C:\Workspace\OfflineDAoC` (GitHub: your fork; `main` is what is released: tag `v0.1.2` is what the game you play runs; later work is on branch `pvp-8v8-m11` until it is merged) |
| The game you play | `D:\OfflineDAoC` (release 0.37b with the 8v8 build installed over it on 2026-11-25: `v0.1`, then the fix of M13.9 an hour later, which is tag `v0.1.1`, then the work of the rest of that day, which is tag `v0.1.2`. The backups are in `D:\OfflineDAoC\deployment-backups`: `scrim-20261125-082823` puts the 0.37b server back, `scrim-20261125-085442` puts `v0.1` as tagged back, `scrim-20261125-101034` puts `v0.1.1` back, `scrim-20261125-121142` puts back the build between the two tags (the evening as your session, emblems; not the 8v8 zone, not the twelve groups)) |
| The copy for trying things | `D:\OfflineDAoC-scrim` (has `pvp-test-commands.txt`, so every `/8v8` command answers there) |
| The 8v8 code | `source\server\GameServer\bots\pvp`, the command in `source\server\GameServer\commands\playercommands\Pvp8v8Command.cs` |
| The tools | `source\tools\pvp_*.py`, `source\tools\scrim_server.py`, `source\server\tools\deploy_scrim.ps1` |

Only one server can run at a time (port 10300): close the scrim server before starting the game
you play, and the other way round.

## Build, test, install

In PowerShell, from `C:\Workspace\OfflineDAoC\source\server`:

    dotnet build "Dawn of Light.sln" -c Release --nologo -v q
    dotnet test Tests/Tests.csproj -c Release --no-build --nologo

Install the build (the server and the game client must be closed; the launcher may stay open).
The script runs the tests again, keeps a backup of the six files it replaces and prints the line
that undoes it:

    # the copy for trying things
    .\tools\deploy_scrim.ps1 -Apply
    # the game you play
    .\tools\deploy_scrim.ps1 -Runtime "D:\OfflineDAoC\runtime" -AllowPlayed -Apply

    # undo: the folder is the one the install printed
    .\tools\deploy_scrim.ps1 -Runtime "D:\OfflineDAoC\runtime" -Rollback "D:\OfflineDAoC\deployment-backups\<folder>"

Without `-Apply` it only says what would change. `-AllowPlayed` is needed once the folder has an
`account.txt`, which it has as soon as its launcher was opened. Only `GameServer.dll` and its
`.pdb` are replaced (in `server`, `server\lib` and `server\win-x64`): your characters, the world
and the files below are not touched.

## An evening on the game you play

1. Start the game with its launcher as always. Leave the gamebot population at 0.
2. A new character: `/8v8 ready` lists your realm's builds, `/8v8 ready <build> confirm` makes
   the character level 50 with that build. The evening begins at once, and from then on each
   time you come in: guilds of every kind in tonight's zones, everybody from realm rank 1
   level 1. You are not moved: `/8v8 go emain` takes you to the frontier, where your seven join
   you; out of the frontier they wait off the field and the evening goes on.
3. Once: `/8v8 guild <name>` (your seven wear it), `/8v8 guild lineup hib-2bard` if you want the
   two Bards (from the next evening).
4. In the evening: `/8v8 call ...` (push, regroup, hold, engage, extend, assist, control,
   retreat, rest, die), `/8v8 who`, `/8v8 go <zone>`, `/8v8 rivals`, `/8v8 ranks`,
   `/8v8 lead off|on`, `/8v8 calls off|on`.
5. It ends when you leave the game. Everything earned is kept.

To have every `/8v8` command on the game you play too (`prime`, `stop`, `status`, a trial at
rank 8 and so on), put an empty file `pvp-test-commands.txt` in `D:\OfflineDAoC\runtime\server`.

## The files beside the server

In `runtime\server` of each install. They are your game: none of them belongs in Git.

| File | What it holds |
|---|---|
| `pvp-rosters.json` | Your seven companions: names, what they have earned |
| `pvp-teams.json` | The guilds: kind, line-up, rank they began at, their record against you (the pool `world` is the one your evenings use) |
| `pvp-players.json` | What you chose: lead on or off, line-up, zone, number of groups |
| `pvp-test-commands.txt` | Its presence opens the test commands to a player |
| `pvp-weights.json` | Optional. Any number of the game's weights, to change one without a build (below) |
| `logs\roams\*.jsonl` | A recording of each evening |

Deleting `pvp-teams.json` gives a fresh world of guilds at rank 1 level 1 on the next evening
(and forgets your rivals). Deleting `pvp-rosters.json` gives seven new companions at rank 1.

## Changing a number without building

A file `pvp-weights.json` beside the server replaces the weights it names; the rest stay as
built. The ones you are most likely to want, with what is built in:

    {
      "Real": {
        "EveningAtLogin": true,
        "EveningGroups": 12,
        "GuildsBeginAtTheBottom": true,
        "RealmPointRate": 5,
        "EnemyKeepsUp": true,
        "EightZone": "thid",
        "EightZoneGroups": 2,
        "EightZoneRestSeconds": 120,
        "CleanZonesHaveNoAdds": true
      },
      "Abilities": {
        "GroupPurgeApartSeconds": 15,
        "SpeedOfSoundApartSeconds": 40,
        "GroupAbilityApartSeconds": 30
      }
    }

`EveningGroups`: groups of each realm (16 at most; eight was the number until 2026-11-25).
`CleanZonesHaveNoAdds`: the nine clean frontier zones are played as the 8v8 zone is. `RealmPointRate`: how many times the game's
realm points a kill is worth. `EnemyKeepsUp`: no enemy fights below your group's average rank.
`EightZone`: the zone where nobody adds, by the word `/8v8 go` takes (`""`: none); `EightZoneGroups`:
guilds of each realm in it at the most; `EightZoneRestSeconds`: how long a group that has just
fought is left alone there. Restart the server after a change. Not in that file (they are in the code): the size of a pack
(`PvpTeamPool.PackMost`, 3), what each kind is like (`PvpPrimeTime.cs`, `PvpStyles.cs`), the five
levels of play (`PvpNotice.cs`).

## Changing the realm ability orders

They are your lists in [DAoC-1.65-8v8-RA-Progressions-v3.md](DAoC-1.65-8v8-RA-Progressions-v3.md).
Edit a list there, then from `source\tools`:

    python pvp_ra_check.py ..\..\docs\DAoC-1.65-8v8-RA-Progressions-v3.md     (checks it, changes nothing)
    python pvp_ra_orders.py ..\..\docs\DAoC-1.65-8v8-RA-Progressions-v3.md    (writes the game's lists and the page)

then build, test and install. What a class may train at all is in
[PVP-8V8-REALM-ABILITY-MENU.md](PVP-8V8-REALM-ABILITY-MENU.md). A bot only presses the abilities
it has a rule for; the check says so if a list has another.

## Looking at a fight afterwards

To watch a fight in your browser, double-click `source\tools\review_my_fights.cmd`; how to read
the page is in [PVP-8V8-QUICKSTART.md](PVP-8V8-QUICKSTART.md), *Reviewing your fights*. In text:

From `source\tools`, with a recording of the install you played on:

    python pvp_myfights.py "D:\OfflineDAoC\runtime\server\logs\roams\<file>.jsonl"          (your fights, one line each)
    python pvp_myfights.py "D:\OfflineDAoC\runtime\server\logs\roams\<file>.jsonl" last     (the last one in full)
    python pvp_kinds.py "D:\OfflineDAoC\runtime\server\logs\roams\<file>.jsonl"             (what each kind of guild did)

The newest file in `logs\roams` is the evening you just played.

## What nobody has seen in a client yet

Built and tested by the automated tests, and where it could be by evenings of bots, but not yet
looked at by you (2026-11-25): the evening beginning at login; `/8v8 guild` and
`/8v8 guild lineup`; `/8v8 call die`; a click in a fight no longer bringing a group that stands
by; your companions sprinting on a called retreat; cures at rest; the two Bards sharing speed and
endurance; the new `/8v8 who`; the test commands kept from a player; your realm ability orders.

## Known and left as they are

- Your own character runs about 305 to 320 on the road where your companions run 390 with the
  same song; not explained.
- Speed of Sound is 189% of a walk, slower than the Bard's best song (204%); it replaces the song
  for its thirty seconds. The server's stock number.
- Only the Hero peels in the Hibernian line-ups; in your group that is you.
- A group's order (push, extend, regroup) changes two to four times a minute in an even fight.
- The kinds' habits, the levels of play and the slow hands of a bad group are first guesses.
- Thirteen of your realm ability lists cost more than the cap gives (their last steps are never
  bought) and eight end short of it; the page of the orders says which.

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

## Current focus (owner, 2026-11-19, evening): positioning and playing as one team

After an evening of about ten sessions played against the bots, the owner: "I feel the bots are
pretty optimized, target and crowd control wise. I would not try to spend more time optimizing
that part for now, I think we did enough at this point for where we stand. Now it's more about
positioning and behaving as a whole team during fight." Asked what was left of M6, and told it
was two decisions (below, under M6), the owner: "I'd say yes to both decisions. Let's close and
accept current behavior/status."

So **M6 is closed**, and the work is M7 (movement and positioning) and then M8 (group tactics),
led by what the owner sees in play sessions and counted over the recordings before anything is
changed. Nothing new is opened on target choice, crowd control, rotations or power pacing; what
was left of those is parked under M10.

### The night of 2026-11-19 to 20: what the owner asked for on leaving, and what was counted

The owner played until late (as a Cleric, an Eldritch and a Hero), asked for four things and
left for twelve hours: an assist call ("I have no way to call target and make a focus on
enemy"), the Druid's resistance buffs ("the druid never cast the 3 resists"), sight for first
contact ("LoS for initial contact would be really cool") and a bot driving the roam ("the
Sorc, Pac healer or Skald and Bard driving. That mean I would stick to the bard and it would
roam around until contact"). All four are built. What a recorded match could show of them:

| Built | Seen in recorded matches | Not seen yet |
|---|---|---|
| **The owner's calls** (M9.3): `/8v8 call assist` puts the group's damage dealers on the player's target while it lives (45 s at most); `push`, `regroup` (everybody comes back to the player), `hold`, `engage`, `extend` stand for 12 s; a bot answers in group chat | three lane matches in which a bot Cleric made the calls (`scrim-call`, `pvp_test_calls.py`): a called regroup had every member that could move within 1,700 of the caller in three seconds; a called assist on a Warrior, after two fixes, put two or three members on it and took it from 2,200 to 1,124 health in sixteen seconds, when its healers brought it back | typed by a player, and the chat lines |
| **A player-healer is told** when two or more of the group are beyond heal range | nothing | all of it |
| **The group's own resistance buffs** are cast before the fight by the member with the strongest of each | Twelve lane matches after three rounds of fixes: every member of every realm starts with all of its realm's, at the strongest its group has (Albion body, spirit, energy 16; Hibernia heat, cold, matter 16; Midgard heat and cold 24, matter, body and spirit 16, energy 8). Buffing takes 60 s at the median (it was about 40), never timed out | with a player in the group |
| **Raising the dead in a fight** by an 8v8 rule (`PvpHealWeights.Raises`) | 24 matches, one team of each with the rule and one without: 14 tries and 3 raised against none; wins 13 to 11, deaths 3.9 against 4.5 a team (neither means anything at this size) | the Paladin no longer trying right after its own swing (6 of its 7 tries were refused) |
| **Sight over the ground** (M4.2, `PvpSight.cs`): a rise between two characters hides one from the other, for spotting and for what a squad knows. Not for casts | a scan of the basin: of 4,000 pairs of points 1,500 to 6,500 apart, the ground hides 35% (18% at 1,000 to 2,000, 47% at 5,000 to 6,000; the ground there runs from 2,900 to 8,100 high). Three roaming matches: the groups first knew of each other 3,000 to 3,400 apart | what it does to a fight on a slope |
| **Roaming in the basin** (M8.4): in a play session each group goes between nine points of the basin by its own draw | the same three matches: first blow 23 to 34 s after the ready, from different sides (one drawn and looked at), one or two members in one area mez at the blow | with a player |
| **A bot drives** (`/8v8 setup`: Lead or Follow): the Bard, Skald or Minstrel leads and the player sticks to it | nothing: it needs a player | all of it |

What it took to get the resistances onto everybody, since each round looked done: (1) letting
them through the hold on buffs for others; (2) only the strongest of each type cast, since the
second Druid's weaker one ended up on half the group; (3) the groups buff standing together
and take their rows at the ready, since a buff on the group reaches 1,000 units and the far end
of a row stood 1,140 from the caster (a third of Hibernia's members lacked one or other, though
the Druid cast each two or three times); and a buffer that runs dry rests and goes on. With
the resistances fights are longer: the first death comes at a median of 65 s against 46, and
2 of 12 lane matches ran to the 300-second limit, which batches should now be given more of.

Also that night: an order is said in group chat once it has stood three seconds, not as it
changes; a bot does not take a target it cannot see over a rise, and a group knows an enemy
that is fighting one of its members from within 2,000 units, seen or not;
`source/tools/pvp_sessions.py` lists the owner's sessions on one page with their replays.

What the test of the calls showed about a group and its kill target. Called onto a Warrior,
the group changed its call and nobody went: the called target was set after the targets had
been handed out. With that fixed, still nobody went, and this is how a group of eight is in
fact spread at such a moment: two healers; the Sorcerer on control; the Theurgist and the
Minstrel on interrupt duty for the enemy's healers; the Paladin peeling a Skald; the Armsman on
the kill target until an enemy came onto the back line and it was turned on that; and the
Cabalist, 1,400 units off, the only one left on the target. Albion's "assist train" is then one
caster. A call by the player now takes every member whose job is damage off its duty and onto
the target while the call stands.
*Tried for the squad's own calls and left off* (`PvpKillWeights.CollapseOnCallSeconds`, 0): 24
matches at rank 5, one team of each collapsing on every new kill target for eight seconds and
the other not. The collapsing teams won 9 and the others 15, lost 5.2 members a fight against
3.8, had 60% of their calls end with the target dead against 64%, and the share of their damage
on the kill target hardly moved (57% against 54%). Each member keeping its job is worth more
than everybody going for the target every time. What is left of the owner's "our assist train
and casters did not kill anyone" is when to collapse, not whether: M8.

*Facing* (step 6 of the way to a first session, built the same night): a bot on the move sees
its whole range inside an arc of 200 degrees in front of it and 40% of it outside; standing
still it looks about and sees all round (`PvpSight.RangeAt`, weights `SightFrontArc` and
`SightBehindPercent`). Four roaming matches of bots in the basin after it: first blow 24, 51,
133 and 33 seconds after the ready, and in all four the two groups knew of each other in the
same second, because they met head on (the other 2 to 21 degrees off the way each was
heading): a group that spots the other turns to it, and two groups of the same speed do not
catch each other up from behind. So among bots it changes nothing that was seen; what it is
for is a player coming up on a patrol from its flank or its rear, which only a player can try.

Things counted that night and what came of them:

- *Why nobody but the Paladin ever raised anybody.* The raid rule lets a healer raise in combat
  only while a second free healer stands within 1,500 units. In 21 matches, 183 deaths: 39 tries,
  33 of them the Paladin's, 5 raised, and not one try by a Cleric, Druid, Healer, Shaman or Bard.
  The new rule lets a member raise a body in its reach when it is not being hit, has half its
  power, nobody alive near it is badly hurt and no free enemy fighter is at the body, one healer
  at a time. It fires when it is safe to, and that is seldom: of 79 deaths under it, 30% had
  nobody alive who could raise, 15% nobody within reach, 25% a raiser interrupted, held, short of
  power or with a fighter at the body, and 23% a clear second by the snapshots.
- *Whether a group's damage follows its own call.* It does, more or less: 54 to 63% of a group's
  damage lands on its kill target (fighters 48 to 62%, casters 67 to 77%), 65% of calls end
  with the target dead, a call lasts 11 to 16 s. Left alone.
- *The Skald the owner could not get killed* (session 012306, as a Hero): the owner did 2,783
  damage to it in 40 s and the group 300; it got 2,959 of healing. The group's call was the
  Shaman. But that session was at realm rank 1, where nobody has Purge: the owner's side spent
  38% of those 40 s mezzed or stunned (9 to 11% in that evening's rank-5 sessions). The setup
  window now starts at rank 5.
- *The Bard's endurance song and sight.* Asking for sight before the change of song made it
  late again (up as the first blow fell in one roaming match of three), so that one thing goes
  by distance alone: a bot knows an enemy within 9,000 units that it cannot see. The owner's
  other way, twisting speed and endurance on the road, would not need it: with three-second
  casts and six seconds of speed the speed would lapse for a moment every round. For the owner
  to choose.

### 2026-11-20, from the owner's sessions as a Hero against Midgard

Built and installed on the scrim server. Automated tests (3,414 pass) and one bots-only basin
match of 11:44; what the owner has seen of it in play is noted per row.

| The owner said | What was found | What was built |
|---|---|---|
| "Follow is not really working. Team is moving as a rectangle" | the group walked 300 apart around a point ahead of it | on the road every member follows the driver, close behind (`PvpFormation.FollowDistance`) |
| "I'd rather keep speed 100% while roaming and endurance during fight" | a Bard cannot hold two songs here | speed on the road, endurance from the moment an enemy is seen |
| "Team 'knowing' where the enemies are without any LoS drops speed" | the song switched on distance to enemies nobody had seen | it switches on sight only |
| "I was on top of them. Client was not updating their position." and, an hour later, "I'm unable to attack healer and shaman. Always says out of range, even if I'm on top of them." | first guess, wrong: lossy position updates (they are sent reliably now, which did not help). The cause: a healer walking up to a patient out of its range went in a straight line at its own height, and kept that height on sloping ground. In the session of 11:24 the Shaman's height did not change for two and a half minutes and 3,300 units; it and a Healer stood 150 to 230 units above the owner's Hero, 18 to 96 away on the map. Fighters were within 20 | the walk into range (and to a corpse) goes along the ground (`PvpPositioning.StopShortOf`, `PvpSight.OnGround`), and an 8v8 bot that stops is put on the ground under it. In a bots-only basin match afterwards: 51 readings of a healer or caster beside a fighter, median difference 1, largest 54. **Not yet confirmed by the owner's client** |
| "Warrior would be alone. We kill him, then 5-10 seconds later we see the 7 other Mids" | the road follow of that morning was broken twice. A follow ends when its leader is farther than the distance it is given: given 210 to 455, the seven stood still while the driver (the Skald) went on alone, 8,500 to 10,300 units ahead in two sessions of four. Given 60,000, the mover's int square overflowed, every follow ended at once, and with the driver now waiting the whole group stood at its start (session of 11:36) | 40,000, with a test that its square fits; the driver waits when a living member is more than 700 behind and goes on at 400. In the bots-only basin match: every member within 500 of its driver four and eight seconds before the first blow, both groups |
| "Bard should be aware when we are chasing someone and sing speed" | a Healer was chased for a minute across the basin, nobody with speed | a group out of contact for three seconds in a fight is chasing (`PvpSquad.Chasing`): speed song until it is in contact again |
| "If they have no endurance, they can't sprint." | a bot's sprint was free; the fleeing Healer stayed at 100 | sprinting on the move takes five endurance a second, as it takes a player (`SprintCostPerSecond`) |
| "Mids have an endurance buff which differs from Hib and Alb" | Midgard's buffbot gives every member the Shaman's endurance buff, five a second: exactly what sprint takes, so Midgard can sprint without end; Hibernia needs the Bard's song (five) and Albion the Paladin's chant (five) | nothing changed; recorded here |
| "No sprint when roaming, just speed... Sprint during fight when needed." | with sprint paid for on the road the companions reached a fight at 2 to 20% endurance (one session, 11:10) | no sprint before the first blow; after it, when pressed (a fighter on it, falling back, a chase) or with more than 400 units to cover (`SprintNeeded`) |
| "General rule for all 3 realms, do not cast damage shield." | 52 casts by Healers, 46 by Eldritches, 11 by Druids, 63 by Cabalists' simulacra in the last sixty recordings | no 8v8 bot casts one, and no pet of one (`PvpBuffPlan.NeverCast`) |

Still open from these sessions: Midgard's fighters dive and die 1,240 to 2,165 units from their
healers (a leash, to be built switched off and compared); in two fights the Bard finished no
endurance song at all; the Shaman casts almost no roots or diseases.
(The leash was counted and dropped the same day: Midgard's fighters die out of heal range no
more often than the others'. Those distances may have been read from where the server had the
bodies, which was not where they fell: see the next section.)

### 2026-11-20, evening: the last comparisons, M7 closed, and what comes next

The owner, after a day of play and of comparisons: "I think the bots are too optimized with
crowd control. It's crazy how stun, nearsight and mez land close to when immunity runs off.
Humans are not that precise... For now, I am not sure if it's worth to keep trying to
optimize. There are many variables and the biggest one is reaction time and how the team
cohesion behave in each situation". And then: "let's move on finishing this Milestone and
let's move with the remaining milestone work. We still have a lot to work on. At the moment,
we have a nice way to playtest 8v8 in line and basin, but everything is just for testing. All
abilities reset, no rps gain, only 2 groups, no real roaming".

So **M7 is closed** (below, under M7), no more weight comparisons are queued after the round
that was already prepared, and the work goes to making the mode a game: what is left of M9 and
M1.5 (realm points earned and kept, timers that are not reset, more than two groups, a roam
that goes on). What is left of M8 beyond that (plans per opponent, cooldown timing) joins M10.

What the day's comparisons said, each 24 matches at rank 5 with one team of each on the new
setting (two dozen team-fights a side are a check, not a result):

| Tried | New against old | Kept? |
|---|---|---|
| The group's order changes the back line's distance only after 5 s (`Order.SafeFactorAfterSeconds`) | wins 13/11, back line walking 42% against 43%, casting 24% against 23% | off: nothing moved |
| A fighter keeps 40% endurance for styles (`Position.FighterKeepsEndurancePercent`) | under 20% endurance 5% of the fight against 21%, styled swings 79% against 72%, wins 13/11, deaths 4.4 against 5.1; a dive stopped in 3 s 33% against 49% | on, the shield fighter exempt |
| A root on a fighter that is on the back line weighs 60, not 30 (`RootOnPursuer`) | a dive stopped in 3 s 45% against 38%, in 6 s 66% against 57%, wins 13/11, deaths 4.5 against 5.3 | on, as a lean |
| A buff a member lacks is cast in a lull of a fight (`Heal.RebuffInFight`, first version) | wins 10/14, deaths 5.1 against 4.0; 15 of 36 such casts were a haste buff on a member nobody had raised | kept only for what a raise took; to be run again |
| Hibernia with two Bards (`hib-2bard`) against the main group, 12 matches each | 9 wins against 6; deaths 1.8 and 3.8 a match against 4.3 and 4.0; amnesia 36 to 42 casts a match against 15 to 17; asleep 4% and 11% of its time against 2% and 8% | the main group stays the default; the owner played it twice and liked it |

*The first death decides.* In 93 matches of the morning the winner lost nobody in 85% and the
group that lost the first member lost the match in 96%; in 55 of the afternoon, with bodies
staying where they fall and raises working, 65% and 85%. A group's first death comes 75 s after
the first blow at the median and its second 17 s later; the first was raised 24 times in 74.
In the thirty seconds after one of a group's first two deaths the raiser nearest to raising was
out of range of the body 26% of the time (438 units short at the median), kept off by a free
enemy fighter at the body 24%, asleep or stunned 14%, being hit 12%. Two switches built for
it, both off until run: `Heal.RaisesWithFighterAtBody` (the raised member stands up at the
raiser's place, not where it fell) and `Heal.ResurrectWalkWithin` with one healer going (the
two trials that had switched the walk off walked to where the body was not).

*How exact the bots are (M10.1, first piece).* Over 209 matches an enemy whose mez immunity had
run out was mezzed again 55% of the time, a third of those within five seconds and a tenth
within two; after a stun immunity 64%, 15% within five seconds. A bot read the immunity off
the enemy. Now each bot goes on taking an enemy for immune for 4 to 20 seconds after it ended,
drawn for each bot, enemy and ending (`PvpNotice`, `Human.ImmunityEndNoticedAfter*Seconds`).
Nearsight is not re-cast on the dot (a median 70 s after the last one ended, never within
ten); it is the amount: healers and casters are nearsighted 22 to 42% of the time they are
alive, a nearsight lasts its whole minute, and nobody cures it (tried 2026-11-18 and off).
Casters stand without casting a third of their living time: 25% of that interrupted, 23%
nearsighted with nobody near enough, 17% out of power, 32% free with an enemy in reach.

### 2026-11-20, later: comparisons, the dead that moved, and M7 as it measures

The owner was away for a while ("All yours... it would be nice to move into the Milestones").
Each comparison below is a batch at rank 5 in which one team of every match has the setting
and the other does not, read team by team with `source/tools/pvp_versus.py` (the referee counts
a match as a whole and mixes the two). Two dozen team-fights a side are a check, not a result.

| Compared | With | Without | Decision |
|---|---|---|---|
| **Kiting when pinned** (`KitesWhenPinned`, 24 matches) | wins 58%, deaths 3.6 a fight, healer deaths 1.3, first healer death at 66 s | 42%, 4.9, 1.6, 84 s | stays on |
| **The Shaman healing first only below 40%** (not 75; 24 matches, Midgard on one side of each) | Midgard won 4 of its 12, lost 5.3 a fight; about twice the diseases and a fifth fewer heals | 2 of 12, 6.7 | not switched: too few to tell |
| **Sprint paid for with endurance** (`SprintCostPerSecond` 5 against 0, 24 matches) | fighters under 20% endurance 17% of the fight, 75% of swings with a style, wins 46%, deaths 4.4 | 1%, 82%, 54%, 3.7 | the cost stays (the owner's rule); `FighterKeepsEndurancePercent` built, off, to be compared |
| **Control on a fighter that is on the healers** (`RootOnPursuer` 100 and `StunOnPursuer` 70 against 30 and 0; 21 matches) | such a fighter stunned, mezzed or rooted within 3 s: 63% of 43; within 6 s: 74%. Wins 43%, deaths 4.7 | 33% of 48; 56%. Wins 57%, deaths 3.8 | off: it stops the dives and costs more than it saves; a milder one to be tried |
| **An order moving the back line only after 5 s** (`Order.SafeFactorAfterSeconds`) | not run yet | | off |

*The dead that moved.* The owner, back and playing a Cleric: "I was not able to rez anyone
because I could not target them even if I was standing beside them." A brain that stops puts
its body back at its spawn point, as for a mob (`ABrain.Stop`, called from `NpcService` once the
bot is dead). A dead 8v8 bot keeps its body for a raise, so the body went to where the bot had
stood at the start of the match: 700 to 5,000 units from where it died in a lane and about
9,000 in the basin, in every recording since 2026-11-18. The owner's client showed it where it
fell; the server refuses a target beyond 6,000 units and deletes it from the client. Bots chose
whom to raise by the wrong place too (52 raise decisions that day, the raiser a median 1,136
from where the server had the body and 1,221 from where it fell). Found with a temporary log
of who moved a body, in the owner's own game; an 8v8 bot's body now stays where it fell.
**Anything counted before this from a dead bot's position was wrong**: a first count that day
of deaths beyond heal range gave 35%, and from the last living position it is 8%.

*The Shaman's instant disease.* The owner: "Doesn't shaman have an instant aoe disease?" and
then "I'd change the shaman spec to have that instant pbaoe disease. It's somewhat common to
see a shaman run and pbaoe disease then kite". It is the Cave line's Mold Cloud at 27 (Mold
Burst 37, Mold Explosion 47): no cast time, 350 units around the Shaman, every 8 seconds. The
group's Shaman had Cave 26 and only the three-second Plague Spores (3.7 casts a match in 72
matches). It is now Augmentation 46, Cave 27, Mending 8: the best Str/Con still, yellow
Dex/Qui. The scored choice already takes an instant around the caster as an option within its
radius and as a walk beyond it; whether the Shaman uses it well is to be read from matches.
First read, two games the owner played as a Hero against Midgard that evening: in one the bot
Shaman never had an enemy within 350 and cast Plague Spores five times; in the other it cast
Mold Cloud three times as an instant, with five enemies diseased by the three. In the four
games with deaths that the owner played after the body fix, a body lay at most 203 units from
where the bot was last recorded alive (that snapshot is up to a second old), and bots chose to
raise a groupmate 17 times.

*The dead that stood.* With the body staying, the owner saw what the move had hidden: "When a
character dies, its body remains up. Unless you look at his hp, you don't know he's dead." A
bot is a monster to the client, and a monster that dies is removed; the removal (type 0) is
what lays it down. Nothing says "dead" for a monster that stays. *First try, seen by the owner
not to work:* the packet a dead player gets, with the bot's id, and no position updates for
the body. "The corpses still stand up and after some seconds the corpse disappear. I could
not click on the dead enchanter from the mini group window": the client ignores that packet
for a monster, forgets a monster it gets no updates for, and cannot target a group member it
does not have. *Second try, installed, **not yet seen in a client**:* the client keeps the bot
and its updates and is sent what it does take, the removal of a dead monster (the body falls
and lies) and then the bot again as a ghost (the see-through flag) with "Dead" under its
name, to click or to target from the group window (`PvpCorpse`). `/8v8 corpse fall|ghost|off`
switches in game between that, the ghost alone, and nothing. Raising moves the bot, which
makes it anew for the client, alive. Only 8v8 bots. *Seen by the owner and taken for broken*
("Dead body fix doesn't work"), and then in so many words: "When a bot dies, it should lay on
the ground, like a human and be clickable. It needs to be targetable to be rez. It cannot stay
standup, it's way too confusing and wrong". *Third* (the body, and the bot as a marker nobody
sees under its name, for the group window only) was installed for one fight and is a switch.
*Fourth, installed, **not yet seen in a client**:* the client is given a human. While the bot
is dead the monster is taken from the client and a dead player is made in its place under the
same object id and a session id no client has, with a player's position packet in the dead
state where the monster got its updates; a removal while it is shown so is a player's.
`/8v8 corpse player|body|fall|ghost|off`.

*Hibernia with two Bards.* The owner, on the Bard being Hibernia's only cure for mez: "we could
always try this setup, that we ran for many years. I just don't know how it would behave in
1.65. We use to run: bard, bard, druid, druid, hero, bm, bm, eld". It is `hib-2bard`, a group
beside the main ones (`PvpBuildCatalog.Variants`): a batch takes it by its name
(`Pairs=hib-2bard:alb-main`), `/8v8 play hib-2bard` fights it and `/8v8 play mid 5 hib-2bard`
plays in it. Both Bards have the one Bard spec until the owner says what the second one had.
Not run yet; to compare with `hib-main` on time asleep and how fast a sleeping healer is woken.

*M7 as it measures* (72 bots-only matches of that day, 144 team-fights, the positions of the
dead read from their last living snapshot):

| Scorecard line | Target | Albion | Hibernia | Midgard |
|---|---|---|---|---|
| Healer time with an enemy fighter within 250 | under 15% | 13% | 15% | 17% |
| Deaths beyond 2,000 of every living healer (of deaths with a healer alive) | under 10% | 5% of 57 | 12% of 56 | 8% of 179 |
| Fighter time beyond 2,000 of every own healer | | 5% | 4% | 5% |
| Healer to the nearest enemy fighter, first 30 s (median) | | 865 | 805 | 959 |
| Wins of its team-fights | | 66% of 44 | 70% of 44 | 21% of 56 |

Half of all deaths come with no healer of the group left alive. Midgard is the weak one of the
three by far. What the referee still fails in nearly every session is not on this card: a
fighter that reaches the healers is stopped within three seconds a third of the time, and the
back line walks 40% of its free time and casts 24%.

## The afternoon before (owner, 2026-11-19): to where the owner can play

After a day of watching, and a loop of seven recorded rounds (below), the owner: "Maybe the best
course of action, at this point, would be to leave the optimization as it is, and continue with
milestone until the point where I can play and resume the optimization." And what that play
should be: "7 bots vs 8 bots in a 'let's roam around an area until we contact the opponent team'
and record how it goes."

So the tuning stops where it stands, and the work is M9 (the owner's group: a play session, the
recorder taking a human member, calls) with the roaming start of M8.4. The owner's own reading of
the bots that day, to keep for M10: "maybe the bots are already better than human. We do not
have a set watch with a countdown for each immune. We have a feeling when it runs out, but
that's pretty much it. Similarly, we sometimes focus on something and forget to lock down the
Theurgist until there are pets... But I think we are still much better than what I can observe."
A bot knows every immunity timer to the tick; a setting that blurs that belongs to M10.1.

## Focus of 2026-11-18: movement and positioning

After an evening watching scrims as a GM, the owner: "the biggest takeaway at the moment is the
movement and positioning." What they saw, each checked against the recordings the same evening:

- The outranged group stood still until the Sorcerer was in range (it was built to wait until an
  enemy was within 1,200 units; the Sorcerer mezzes from 1,875).
- "The fight is way too tight. There are casters in the middle of melee and melee do not react."
  The middles of the two groups' healers and casters were 1,870 units apart five seconds after
  contact and 850 at forty-five; casters cast from a median of 740 to 925 units with spells that
  reach 1,500; a caster had a free enemy fighter within 350 units 21% of the time, and in 40% of
  those moments no enemy fighter had turned on it.
- A Skald "ran past everything" to the two Druids (nothing looked at an enemy fighter before it
  was within 350 units of its victim), and the two Druids stood close together (healers kept 400
  apart, like any two casters).
- A Warrior sent to peel walked back and forth between the enemy and its healer (told to stay,
  its own brain still went for whoever had hit it), often without a Slam.
- A Cabalist cast its bubble in a fight (bots far from the enemy did not count as fighting; the
  Clerics began eight buffs a match after contact).

The owner also doubts how much the lane scrims say: two groups walking head-on across open
ground is one opening among many, and the Sorcerer's best. Agreed the same evening:

- **M7 (movement and positioning) comes first**, ahead of what is left of M6 and of weight tuning.
- **Who beats whom in the lanes is not a target.** "Albion won 95 of 100" measured that one
  opening plus bugs (the Bard sang for half its life). The recordings stay the tool for finding
  what is broken inside a fight; realm win rates are not reported as progress.
- **The owner watching is the fastest check there is**: in one evening it found things 150
  recorded matches had not shown as wrong. Fixes go to the scrim server for the owner to look at
  before any large batch.
- **No night batch of weight draws** until the positioning looks right and fights start in more
  than one way (different distances and angles, one group moving, one side seeing the other
  first, a start inside mez range).

### Built the same evening and night

All of it is on the scrim server. The owner watched the build of that night on the morning of
2026-11-19 ("the spacing and positioning looks better") and what was seen then is the next
section but one. Each line names the weight that switches it, where there is one.

- **The Bard's songs** (`SongInFightEverySeconds`): in a fight it starts only its endurance song,
  only when that is not playing, at most once in 20 seconds, not while it is hit or a groupmate
  sleeps; once the enemy is in sight the endurance song is the one it starts. The version of
  that morning had it cycle four songs of three seconds each from the first second of a fight.
- **Seeing the train** (`Heal.TrainCountsEveryAttacker`): a member counts as trained by every
  enemy that hit it in the last seconds, casters and the owners of pets included.
- **Control on the train** (`ControlOnTrainer`): a mez or stun, and a nearsight on a caster, is
  worth more on an enemy that is on the trained member.
- **The outranged group's wait** (`Order.HoldWhenOutrangedBreakMargin`): ends while the enemy is
  still 500 units outside its mez reach.
- **No buffing in a fight** (`BuffsInFight`): once the group's fight has begun nobody begins a
  buff with a cast time, on a member, on itself or on a pet; a healer calls no pet and does no
  pet upkeep. It took three passes: the buffs go through four separate paths (upkeep, the group
  plan, the self bladeturn restored "as soon as it is safe", the combat routine's defensive
  pick) and pet upkeep through a fifth. The gate is now in `CastCoordinatedBuff`, where the
  first four end. Lost with it until 2026-11-20: a member raised in a fight was not rebuffed (the
  owner, raised and playing on: "I just never received buffs after rez but I assume druids were
  not finding downtime to buff me"). Now the group plan's buffs that a member lacks are cast in
  a fight too, by the member whose buff it is, when it is not being hit, has 30% power and
  nobody alive in its reach is below 70% (`Heal.RebuffInFight`). Not seen in play yet.
- **A shield fighter told to stay stays** (`BotBrain.PvpStaysPut`), and the first swing of a peel
  is the Slam: a style queued for the last target is dropped when this one is to be stopped.
- **Intercept** (`Peel.InterceptWithin`, `Peel.Coming`): an enemy fighter running at a healer or
  caster is noticed from 1,200 units; the shield fighter goes to meet it and casters with a root
  treat it as a pursuer. The Druid now has its root among its scored casts.
- **Healers apart** (`Spread.HealerSpacing`): a group's healers keep 1,000 units from one
  another and travel at the two ends of the back row.
- **The edge of range** (`Position.EdgeDistance`, `PerUnitInsideEdge`, `KillTargetRangeWeight`,
  `DutyTargetRangeWeight`, `WalkInsideEdgePerHundred`): healers and casters want every enemy at
  least 1,350 units away while their work stays in reach; a walk that would end inside that
  distance of an enemy healer or caster costs score; being in range of the kill target counts
  for a tenth of what being in range of one's own target does. (1,200, a quarter and half the
  walk cost in the evening; the values here are the night's, below.)
- **Realm colours** (`PvpTemplateOutfitter.RealmColour`): armour and cloak red for Albion, blue
  for Midgard, green for Hibernia. Looks only.

**The referee** (`source/tools/pvp_referee.py`) is how these are checked from now on, in place
of the owner watching every fight. It prints a time budget for every build (where its living
seconds went, with a mark on the lines that look odd) and a list of rules, each from something
the owner said, with what was measured and the worst moments by match and second. First
readings are in the table further down.

### The night of 2026-11-19: what moved the two back lines apart, and what it cost

Positioning only, judged by the referee, in rounds of 12, 24 and 48 matches. In order:

1. **Four sets of weights, twelve matches each.** A wider edge, the edge counting more with a
   step every 2.5 s, and walks costing double each moved the lines a little. All three together
   passed the distance marks (1,514 units at 20 s, 1,651 at 45 s, casts from a median of 1,346)
   and became the defaults. They also cost the most: casters cast 17% of their living time
   against 25%, a third of their idle time was spent with no enemy within 1,500 units, and
   three matches of twelve ran to the time limit.
2. **A caster with no enemy in reach closes up to the edge** (`Position.PerUnitOutOfReach`).
   Nothing had brought a caster forward again but a cast worth a walk, and the walk was being
   charged for ending near *any* enemy, the fighters on the group's own front included; it is
   now charged for the enemy's healers and casters only. Idle time with nothing in range fell
   from 33% to 8%.
3. **A healer stepping away does not step beside the other healer** (the step's score now
   knows `Spread.HealerSpacing`). Healers within 800 units of each other: 30% of the time in
   the first set, 5 to 9% since.
4. **No speed spell or speed song once the fight is on.** They are pulsing spells, which the
   evening's rule let through: about one a match, with an enemy 600 to 1,300 units away. Now 0.
5. **A member that can cure mez takes no step while a groupmate sleeps**
   (`Position.CurerStaysForSleeper`). Recorded: a Bard walking to a sleeping Druid was sent the
   other way by five steps in a row, twelve seconds; a Pacification Healer stepped away for ten
   seconds with both other healers asleep.
6. **The wait after a step counts from the arrival** (`Position.StepWaitCountsFromArrival`) and
   **a caster on its way in to cast is not stepped back out** (`ApproachLeftAloneMilliseconds`).
   The six casters had taken a step straight after a step, nothing cast between, 20 times a
   match, and walked in and been sent back before casting 7.8 times a match. Casting went back
   up (20% to 28% of free time) and the lines came back together (1,157 and 939): the stepping
   had been what kept them apart.
7. **No step ends nearer the enemy's healers and casters than the edge**
   (`Position.StepNearerTheirBackLineAtMost`), and healers stand 150 units nearer the end of
   their heals' reach (`HealRange` 1,850). This was the cause under the others: a step is scored
   by what it gains from a fighter against what it costs in range to the member's work, range
   counts double, and so four steps in ten taken to get away from an enemy fighter went toward
   the enemy's back line. Such steps were half of all the times two back lines came within
   1,000 units of each other in the first minute (19 a match; 12.7 after).

| Rule | Before (150 matches) | Evening (12) | End of the night (48) |
|---|---|---|---|
| A group does not stand inside the enemy mezzer's reach before the fight | 2.2 s a match | 0.5 s | 1.0 s: at the mark |
| The two back lines keep their distance (20 s, 45 s after contact; mark 1,500) | 912, 695 units | 1,183, 875 | 1,578, 1,248: the second fails |
| Casters cast on healers and casters from the edge of their range | median 810, 40% under 700 | median 867, 36% | median 1,237, 14%: fails by the mark of 10% |
| A group's healers do not stand together (closer than 800) | 61% of the time | 19% | 8%, pass |
| A fighter that reaches a healer or caster is stopped within 3 s | 36% of 607 | 47% of 34 | 48% of 120: fail |
| A shield fighter told to stay has no attack target | 35% of the time | 0% | 1%, pass |
| Nobody stops to buff once the fight has started | 11.2 a match | 0.3 | 0, pass |
| A healer does not call its pet in a fight | 4.9 a match | 0 | 0, pass |
| The Bard does not spend the fight singing | 17 s a match | 0.8 s | 1.9 s, pass |
| A sleeping healer is woken within 8 s, when a curer is up | 22% of 101 | 45% of 11 | 35% of 62: fail |
| Casters still cast (of their free seconds: casting, walking; mark 30% and 40%) | 34%, 34% | 31%, 33% | 26%, 34%: fail |

The marks of 1,500 units and of two times in three are mine, not the owner's. The last rule is
mine too: it is the cost side of the distance rules, added that night because the first build
to pass them had the casters casting 20% of their free time and walking 43%.

What the table does not settle:

- **Distance was bought with casting.** Casters cast 26% of their free seconds against 34%
  before any of this. Part of it is the kill target standing beyond a caster's range (it did
  for 34 to 62% of the turns a caster found nothing to cast, by class): the fighters' target
  is in the enemy's back row, the casters now stand off, and damage off the kill target scores
  too low to be cast. Four matches of 48 ran to the five-minute limit; none of 150 did before.
- **Fighters reaching the healers.** In the 48: Midgard stops 24 of 34, Hibernia 24 of 53,
  Albion 10 of 33. Of the 62 not stopped, 32 were already immune to mez or stun when they
  arrived (from the opening), and 12 had nothing aimed at them at all. One shield fighter faces
  up to three fighters, and its Slam misses or is evaded often; Blademasters and the Armsman
  do not turn on them. This wants an answer by the whole group (the trained member running,
  the others slowing what follows), not a better peel.
- **Sleeping healers.** Counting each curer of a group whose healer slept more than eight
  seconds (in the 48; before the rule's "when a curer is up"): itself asleep or stunned 56
  times, began the cure and did not finish it in time 28, kept from casting 18, out of range
  16, free and doing nothing 15, dead 9.
- **Sample size.** Twelve matches swing a rule by a third either way; the same build measured
  52%, 50% and 35% on sleeping healers in three runs. The 48 are firmer, not firm.

The referee's page for that last run, with the time budget of every build, is written beside
the scrim server's logs (`runtime\logs\referee-2026-11-19.html`; not in the repository).

### The morning of 2026-11-19: what the owner saw, and what was built from it

The owner watched Albion against Hibernia as a GM. Each line below is what was said, what the
recordings showed about it, and what was changed. Everything is on the scrim server with 3,312
automated tests passing; the owner has seen none of these changes in the game yet, and the
figures "after" are from twelve watched matches only, so they are a first reading.

- **"The Hero definitely needs to use the level 50 Large Weapons style."** The Hero was built on
  50 Blades and never left its shield; the Armsman never left its polearm (no Slam in 908
  recorded hits). Now the Hero is 50 Large Weapons, 42 Shields, 39 Parry, and the Hero, the
  Warrior (two-handed hammer) and the Armsman fight two-handed and take the shield out for the
  Slam and, with nobody to attack, for guard (`PvpWeaponSwap.cs`). The Paladin stays one-hand
  and shield (owner). First reading: the Armsman lands 1.9 Slams a match, the Warrior 1.5, the
  Hero 2.2; four or five swaps each way a match.
- **The style from behind.** A bot stood wherever it had arrived from, so it had its style
  from behind only by chance: no Annihilation in the first eight matches of the new Hero. A
  fighter in reach of a stunned target now walks behind it (`Position.FightersStepBehindStunned`).
  The Armsman does so 3.5 times a match and the Warrior 1.5. The Hero hardly ever (five
  Annihilations in eight matches), because of the next point: once its Slam lands it goes for
  the next enemy. It also runs short of endurance: under 25 for 23% of its fights against 12%
  before, and a quarter of its swings are without a style.
- **"Minstrel using stealth. It should not."** The older bot logic walks any class with Stealth
  up to an enemy unseen. No 8v8 bot does now (`BotRvrAmbush.CanApproach`).
- **"Paladin went to peel both Blademasters. It slammed one and kept attacking it, not handling
  the second."** The one it was on kept 115 against 120 for the other, and 125 units more from
  the ward lost that. A stunned enemy now counts 40 less (`Peel.Stunned`). On a stunned enemy
  while another fighter was free on its healers and casters: 25% of the time it spent on
  stunned enemies before, 5% in the first reading.
- **"Once the second Blademaster got mezzed, the Paladin could have gone on another duty", and
  it stood beside a caster that enemy casters were hitting, "which was useless".** A shield
  fighter helped with the kill only within 625 units of its ward, and with the back lines
  apart that was never: the Paladin had nobody to go for during 52% of its fights. Distance
  from the ward now costs the kill target a quarter of what it did
  (`Peel.KillPerHundredUnitsFromWard`), an enemy on or running at the back line still scores
  higher, and a groupmate's missing health draws guard only when an enemy is in melee on it.
  First reading: no target 7% of the time. **To watch for:** this takes the shield fighter
  away from its healers; whether more fighters get through is not measured yet. The owner on
  the Hero the same day: when it peels it should "slam / snare / slow targets then back to his
  duty", so that the Druid can move away; an enemy immune to all of that is rare, is close to
  the back line, and is for the group's kill call. That is what the peel does now (it leaves
  a stunned enemy for the next one), so the Hero's Annihilation is for the kill target.
- **"The Enchanter was standing most of the fight, not doing anything."** It stood free to act
  for 24% of its life and cast for 17%. With the kill target beyond a spell's range, or none
  called, damage on another enemy now costs 5 and not 60 (`DamageOffKillTargetOutOfReach`),
  so the caster works on what it reaches, healers and casters that are free to cast first;
  never on a sleeping enemy, and not on one that a root holds (`DamageBreaksRoot`).
- **"The feed is receiving info from all lanes."** Two lanes' middles are 6,000 units apart and
  the feed reached 7,000. A watcher now reads one lane: the one asked for with `/8v8 watch`,
  else the one stood in (`PvpObserver.LaneOf`).
- **"Hibernia doesn't seem to know how to handle the Theurgist and clean pets. One of the things
  we were doing was to stack on the Eldritch and it would PBAoE the pets (and using quick cast
  if needed)."** In 16 recorded matches the pets attacked Hibernia 63 times a match, the Druids
  28 times and the Bard 12, and the only answers were a Druid's staff and its root. Now
  (`PvpPetClear.cs`): with three enemy pets on its healers and casters, those with a pet on
  them walk to the member that has a point-blank area damage spell, and every member with such
  a spell casts it once two pets are inside it, unless it would wake a mezzed enemy. Those
  gathered are left out of the spacing rules until no pet is left. **Not built:** quick cast.
  No bot has it (it is switched off for every kind of bot), so a caster that the pets are
  hitting cannot get the spell off.
- **The Enchanter's spec** (owner: "45 Light / 30 Mana / 4 Enchantment"; it had 29 Enchantments
  and 6 Mana, so no point-blank spell at all). 45 and 30 cost 1,498 of a caster's 1,494
  points on this server, so it is 45 Light, 29 Mana, 6 Enchantments: the same 176-damage
  point-blank spell as the Eldritch. The cost is the group's damage add, which falls from the
  level 26 one to the level 4 one.

- **"Two Albion casters had a Druid's pet on them, and they just ran away and stood still. They
  could always quickcast or staff or someone dd or attack the pet. They are really weak."** In
  that match a lynx was on the Cabalist for 71 seconds and attacked it 24 times; the Cabalist
  cast nothing in that time and nobody attacked the lynx. Now a pet in melee on a healer or
  caster is a target for the group's casters, above a kill target at full health
  (`DamageOnPetOnBackline`; the Theurgist's pets are left to the point-blank spells), and a
  healer or caster that cannot cast because a pet is hitting it hits the pet with its staff
  (`StaffThePetOnMe`). **Not built:** a fighter going for the pet, and quick cast.
- **"Albion will call a regroup and Hibernia will push, but both Druids stay way too far and are
  out of range to almost any of their teammates."** Recorded: a Druid stood still for 25 seconds
  while its group went after the enemy, until its fighters were 2,850 to 3,700 units away.
  Nothing moved a healer forward: a step was only ever taken to get away from an enemy. A
  healer with two or more living members beyond its heals' reach now follows them, without the
  wait between steps (`Position.HealerFollowsAtMembersOutOfRange`).
- **"We were always using speed 6 and sprinting. Everyone always has sprint on. One of the
  first realm abilities to get is Tireless, and you can perma sprint without issues."** And:
  "we were using speed + sprint all the time until initial contact". No bot sprinted: every
  class moved at 185 to 190 units a second in a fight, and a bot's sprint did not change its
  speed at all. Every 8v8 bot now sprints on the way in, on top of the speed song, and through
  the fight, 30% faster (`Position.SprintInFights`): 210 to 245 units a second in the first
  four matches. It costs a bot no endurance, like all of a
  bot's movement, which stands in for Tireless and the endurance songs; a player in the
  group pays for sprint as the game has it, which is to be settled before M9.
- **The start of a fight** (owner, on a match where Albion held and its Sorcerer put four of
  Hibernia to sleep in ten seconds): "the way the tests are designed is not what a real 8v8
  fight would look like... If you can AoE mez the Bard on initial contact then I assume it is
  normal you wipe them, if no one breaks the mez." The recording agreed with the mechanics:
  Albion held because its mez reaches 1,875 units against the Bard's 1,500, the first area mez
  caught the Bard, the Bard is Hibernia's only cure, and at realm rank 1 nobody has Purge. What
  is wrong is the setting: two groups placed 6,000 units apart that walk straight at each
  other and meet in six to ten seconds. Not changed yet; see the open questions.

- **"The two Blademasters are mostly running forward then back then forward then back. They are
  also stuck to each other."** Two causes in that match. Hibernia went Engage, Regroup, Engage,
  Regroup, Engage, Regroup, Engage in its first 28 seconds and called the Blademasters back 11
  times: "regroup" fired with fighters 1,800 units from the middle of the group's healers and
  casters, which a fighter on a target in the enemy's back row always is now that the back
  lines stand apart. It is now measured from the nearest healer, at 2,300
  (`Order.RegroupFighterIsFarAt`, `RegroupWithin`). And under Extend each fighter took the
  nearest enemy on its healers anew every second, so both went Armsman, Minstrel, Armsman,
  Minstrel, Paladin together for forty seconds; now a fighter stays on the one it has and two
  fighters take two (`PvpOrders.ChoosePursuer`). On the kill target they stand together by
  design: the same target 84% of the time.
- **"Both stood still, waiting for the other to push in. Then at some point Albs pushed and
  mezzed all 3 fighters."** The hold on a longer mez range, and the outranged group's wait,
  were the assistant's reading of an earlier remark. Both are off (`Order.HoldRangeAdvantage`
  0): both groups run in, and the mezzer casts when it is in range.
- **"Both Blademasters were back-styling (snare) the Armsman on the Druid, and the Druid just
  stood still. There was the Paladin also attacking the Druid and neither Blademaster switched
  target. The Druid could have kited away. If the Paladin would have followed and extended,
  Hibs could have collapsed on it."** A healer with a free enemy fighter on it and close was
  "pinned" and stood, because both run at the same speed. Now, with one of its attackers
  held or slowed, it runs from the others (`Position.RunsWhenOneAttackerIsHeld`); the two
  fighters taking two pursuers is the point above.
- **"Oserenmund stood way off during the fight and stood still, not doing anything."** The
  Theurgist, out of power after 48 seconds: a pet costs 12% of its pool. In 50 matches at
  rank 5 the Theurgist was under 10% power for 42% of its living fight time (dry at a median
  of 61 seconds), the Bard 39%, the Skald 31%, the Minstrel 30%, the Druid 12%, the Cleric
  10%. "If the Theurgist was out of mana, that explains it. I do not have that information
  when I observe": the feed now says when a member runs out of power. Nobody gave a group
  power regeneration: before the fight a member cast only what was its own, and in a fight no
  buff is begun. The owner: "Sorc should cast that buff before the fight, for sure. All realms
  that have a mana regen buff should cast it before fight." A power regeneration buff is now
  cast on the members that use power before the fight; in the first match after, the Sorcerer
  cast Cognition of Power seven times and all of Albion held it. The songs are no answer: "Neither Bard or Minstrel were playing the mana song. Back in 1.65
  we had to rely on mana potions (from alchemists, if I remember correctly), Mystic Crystal
  Lore and Raging Power. Before having access to MCL and RP was a rough time." So every 8v8
  class that has Mystic Crystal Lore now buys it to level 2 right after its Purge and uses it
  when it is low on power and out of combat (the ability cannot be used in combat), and
  Raging Power, which needs Mystic Crystal Lore 2 and ten points more, is bought when the
  points are there and used when the pool is nearly empty
  (`AutonomousBotRealmAbilities.PvpFirst`, `PvpActives`). Built and tested; not yet on the
  scrim server when this was written. **Not built:** mana potions. **Not solved:** the pace at
  which a Theurgist spends its pool.
- **"You would gain so much more insight from seeing the fight... a picture is worth 1000
  words."** `source/tools/pvp_replay.py` draws a recorded match from above: picture sheets
  (six moments each) that the assistant can look at, and a page to play and scrub in a browser
  with every member's health, power and state beside the map. The first two sheets showed
  four things no count had: both groups arrive in a rigid line abreast; a Cleric walked into a
  brawl because a healer going to a patient out of range walked to the patient's very place
  (now it stops where its spell reaches); a Druid with two fighters on it was told to gather
  on the Eldritch, to get away, and to hit a pet with its staff, in turn (the last two are now
  left out while a fighter is on it); and a group's casters falling back end up within 200
  units of each other.

### The loop of 2026-11-19: record, draw, look, measure, change

The owner, away for three hours: "Could you keep running tests, generate the images, review the
replay and generate insight to improve the bot skills?" Each round is twelve recorded matches at
realm rank 5, picture sheets of a few of them (`pvp_replay.py`), a count over all of them for
whatever the pictures suggest, and then a change. A picture shows one fight; two things seen in
pictures turned out to be rare when counted, and are noted as such.

1. **The back row stops at range before the first blow** (`Position.BackRowStopsAt`, 3,200). An
   opening drawn second by second: with the speed song and sprint the two groups close at about
   1,000 units a second, the travelling formation aims the back row at a place 700 units short
   of the middle of the enemy, and both back rows ran in with their fighters to about 1,000
   units from the enemy's. Two seconds after the first blow the fight was one ball 1,300 units
   across. Now healers and casters stop about 2,700 units out and their fighters go on; a
   caster does not walk in to cast before the first blow either. Twelve matches each side: a
   mez, root or stun was begun on an incoming fighter before it arrived 40% of the time before
   and 63% after (the Sorcerer: 1 cast, then 7); back lines apart at 20 s, 1,411 and 1,662.
   A fighter running at the healers was stopped within 3 s 47% of 60 before and 58% of 52
   after, and then 38% of 53 and 38% of 119 in the next two rounds: the 58% was a small
   sample, and the rule has not moved. A group's healers within 800 units of each other went
   from 4% to 12%.
2. **What becomes of a mez on a fighter at rank 5.** Of 78 mezzes begun on an enemy fighter in
   the first fifteen seconds (24 matches): 21 never finished, 19 were finished on a target that
   had become immune during the cast, 7 did not take, and of the 31 that did, 30 were purged
   within three seconds. None held three seconds. Of 71 roots, 34 held three seconds or longer.
   So the first mez on a fighter buys its Purge and nothing else, and a root after that holds.
   A caster now drops the mez, stun or root it is casting when the target has meanwhile been
   dealt with or has become immune (`DropsAWastedControlCast`).
3. **An order held at least eight seconds: tried, and taken back.** A group changed its order
   3.8 times a minute, an order lasted a median of five to seven seconds, and more than half
   the changes were Engage to Extend or back; in the pictures of one fight Midgard's fighters
   went to and fro between their kill target and their own healers for ninety seconds and did
   neither. With `Order.KeepSeconds` at 8 the changes fell only to 3.3 a minute. It is back at
   3; the to and fro is still there and wants another cure than time.
4. **The owner's bar, said during the loop:** "At some point, a healer will be out of position
   or out of mana or interrupted and someone will die. Similarly, a team has to wipe at some
   point. It just needs to not be from obvious 'dumb' play or within few seconds of contact."
   And: "the real difference was how each team reacted to the situation... 2 teams of equal
   skill could take 5-10 minutes. I've seen longer fights." The referee has the rule now
   (`rule_nobody_dies_at_once`): the first death comes at a median of 53 to 61 seconds and
   within 20 seconds in 0 or 1 of 12 matches, so that part holds at rank 5. A fight lasts
   about two and a half minutes.
5. **Each group's first death** (30 in 24 matches): in the six seconds before it the group's
   other healers were being interrupted and not casting for 27% of their seconds (the last
   thing to hit them: a pet 41 times, a spell 16, a fighter 11), walking 16% (half of it a
   step to stand off, keep range or keep apart), asleep 15%, casting 12%, out of range 11%,
   stunned 8%. No heal landed on the one that died in 17 of the 30. A healer on a step of
   position now breaks it off to heal a member under 60% health, unless a fighter is on it,
   and takes no step to keep apart then (`Position.HealerBreaksOffAStepToHeal`).
6. **Pets on the healers.** In Albion against Midgard Albion's pets attacked Midgard's three
   healers 144 times a match (the Theurgist's 100, the Cabalist's 44) and Midgard did nothing
   about them; on Hibernia's, 69. A mezzer may now put pets to sleep (a pet has no Purge): a
   single mez on a pet that is not the Theurgist's, an area mez on any, two pets counting as
   one enemy (`PvpPetClear.MezCount`). In 24 matches that was cast 1.3 times a match and the
   attacks on Midgard's healers did not move (143): the mezzing Healer is seldom free to do
   it. Then the other end: against Midgard the Theurgist lived 164 seconds a match, was
   being interrupted for 14% of them, called 21.6 pets, and Midgard's Savage and Berserker
   never had it as their target. Whoever owns the pets that are on the group's healers and
   casters is now a better kill (`Kill.PerPetOnOurBackline`, up to four pets). Round four, 24
   matches: Midgard's kill target was the Cleric 46% of the time and the Theurgist 36% (97%
   and 1% before), its three fighters each had the Theurgist as their target for 20 to 24
   seconds a match (0 before), and the Theurgist was being interrupted for 21% of its life
   against 13%. It still called 22.5 pets a match, died in one match of eight, and its pets'
   attacks on Midgard's healers went from 143 to 135. So the group now goes for it and does
   not get it killed: an open point, not a fixed one.
7. **Resurrection.** 8.5 deaths a match and 0.2 resurrections. Of 137 deaths with fifteen
   seconds of fight left: no healer of the group alive 48, a healer too far from the corpse
   (2,900 units at the median) 41, asleep or stunned 28, could have and did not 9, short of
   the half of its power it costs 8. Walking a healer to a corpse during a fight
   (`Heal.ResurrectWalkWithin`) was tried and is off again: 3.0 walks a match for 0.2 more
   resurrections, the back lines 300 units nearer each other, the healers together 17% of the
   time against 12%.
8. Nobody gathers on a caster that has a free enemy fighter beside it
   (`Pets.NoGatherWithFighterWithin`): 22% of Hibernia's walks to gather went to one.
9. **How a kill call ends** (467 calls in 48 matches): the target died in 68%, after a median
   of 9 seconds; it was given up alive in 31%, after a median of 27 seconds and at a median
   of 94% health, most often a Druid, Cleric or Healer. A leaning to give up a target that is
   not losing health (`Kill.PerStaleSecond`, `RecentlyAbandoned`) was tried and is off again:
   the calls were given up after 21 seconds against 25, there were 12 a match against 9 for
   the same number of kills, and a fighter running at the healers was stopped 31% of the time
   against 45%. Whom to call, and when, is the question; giving up sooner is not the answer.
10. **The shield fighter away on the kill.** The morning's change let a shield fighter with
   nobody to peel help with the kill at any distance. In 24 matches shield fighters were on
   the kill target 44% of their time, and for a quarter of that a free enemy fighter was on a
   healer or caster of their own group (11% of all their time); in the pictures of one fight
   a Warrior hit the enemy's Druid while a Blademaster killed a Healer it did not guard. Now
   the cheap distance applies only while no enemy that is awake and free is on, or running at,
   any healer or caster of the group (`PvpPeel.Threatened`).

Where the loop ended, on 24 matches of the last build against the 41 rank 5 matches of that
morning (many changes lie between the two columns, so no single one can be credited):

| Rule | That morning (41) | After the loop (24) |
|---|---|---|
| Nobody dies within seconds of contact | first death at a median of 40 s; within 20 s in 4 matches, the earliest at 2 s | 57 s; within 20 s in none, the earliest at 28 s |
| A fighter running at the healers is stopped within 3 s | 30% of 168 | 48% of 113 |
| A sleeping healer is woken within 8 s | 71% of 31 | 89% of 36 |
| Back lines apart at 20 s and 45 s | 1,539 and 1,344 | 1,512 and 1,421 |
| Casters cast from the edge of their range | median 1,376; under 700 units 7% | median 1,255; 13%: worse |
| A group's healers within 800 units of each other | 10% | 12% |
| Casters' free time: casting, walking | 32%, 26% | 33%, 26% |
| A shield fighter on the kill with a free enemy fighter on its own healers | 9% of its time | 7% |
| Length of a fight | 127 s | 143 s |

The automated tests stand at 3,321. The owner has seen none of the loop's changes in the game.

11. **The Runemaster's area nuke** (owner, on what Midgard did about a Theurgist: "the idea
   would be to have Midgard push hard on Albs and try to catch the Theurgist. Theurgist always
   been a tricky target... Runemaster should have an AoE, similar to the Eldritch"). On this
   server it is not point-blank: Odin's Hatred, level 44 of the Runecarving line, 158 damage
   350 units around an enemy up to 1,500 away. The Runemaster casts it on two or more pets that
   are on its group's healers and casters, when nothing sleeps inside it (a new kind of scored
   choice, `AreaDamage`; `AreaDamagePerPet`). Two things stood in the way and are fixed: the bot
   did not hold the spell at all (a caster bot keeps one spell of each type and target for a
   line, and the line's level 48 single nuke won), and the first version picked a
   ground-targeted spell of the same line that a bot cannot aim (21 casts begun in two matches,
   none finished). Checked in one match only: chosen twice, three hits on pets.

**The rule that swings most** is the fighter running at the healers being stopped
within three seconds: 47, 58, 38, 38, 45, 31 and 48% over the seven rounds. At rank 5 a stop is
purged at once (finding 2), so the mark may be asking for something a group with Purge on the
other side cannot do; what it should count instead (the fighter held after its Purge is
spent, or the healer getting away) is to be settled with the owner.

Seen in a picture and rare when counted: a group's fighters standing idle while stunned enemy
fighters sat on their healer (5% of such seconds under Extend); a caster that had drifted out
of the fight (no enemy within 1,600 units for 11% of a Theurgist's fight, 2 to 5% of the
others'). Seen and not yet acted on: the
mezzing Healer of Midgard stands level with its fighters at contact (it has to be within 1,500
units to mez) and is in melee within five seconds in 46% of matches; three or more of a group's
healers and casters stand inside one area mez 12% of the time for Hibernia and 6% for Albion.

Still open from that morning: power regeneration and the pace at which casters spend their
power; quick cast for 8v8 bots; a fighter going for a pet; whether the
Paladin away on the kill lets fighters through; the Hero's endurance with a two-hander; the
Slam that misses (a fifth to a quarter of them against fighters: plain misses, not blocks);
and how a fight should start.

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
- [x] **M1.4** (M) `/ready` for the owner's own character: level 50, respec to a chosen build,
  equip the template. Realm rank untouched.
  *Done 2026-11-19: used by the owner in a client the same evening on two characters, a Cleric and a
  Hero ("it
  worked. I have all the gear. Con/Dex/Piety are all 75/75. Resists are all cap"). One fault
  seen and fixed: the skill window showed a line trained to 33 as "1+43".*
  *Built 2026-11-19 as `/8v8 ready <build>`.* Without a build it
  lists the realm's builds; with one it says what will change and asks for
  `/8v8 ready <build> confirm`. It then sets the build's class, level 50 and the build's lines,
  and puts on a saved copy of the gear a bot of that build wears (each piece the character's own
  unique item); what was worn goes to the backpack where there is room. Realm rank and realm
  abilities are not touched, so the character has none: play the first sessions at rank 1. The
  companions leave out the build the player took (Albion has two Clerics). It changes a real
  character for good and nothing on the scrim server can check it: the tests cover only the
  choices (which build, which companion slot, the refusals).
- [x] **M1.5** (M) Realm rank for 8v8 bots and companions: stored realm points, earned from kills,
  realm abilities bought in the build's order. A scrim setting overrides rank.
  *Realm abilities, passive and active, built 2026-11-22 (owner: "let's finish realm abilities
  (passive and active)").*
  *Run with bots the same day, not seen with a player.* The three main groups spawned at ranks 5
  and 8 had bought what their orders say (a Druid at 5: Tireless, Group Purge, Purge, Mystic
  Crystal Lore 2, Augmented Acuity 2; at 8 also Mastery of Concentration and Battery of Life).
  Six matches at rank 8: Mastery of Concentration 42 times, Battery of Life 20, The Empty Mind
  10, Perfect Recovery 7, Speed of Sound and Avoid Pain 4 each, Ameliorating Melodies and
  Soldier's Barricade 3, Brilliant Aura and Bunker of Faith 2, Faith Healing once, and no error
  in the server's log. Whether each did what its text says (a group running, a heal landing)
  was not measured. One fault found and fixed: a nearsighted Healer tried Perfect Recovery on
  a body beyond its shortened reach every second, and the game refused each one. A player's
  group ability on its companions has run in no client.
  - **What a class buys** (`bots/pvp/PvpRealmAbilities.cs`, one order a class, 26 classes; the
    table is in the player guide). They are the assistant's reading of 1.65 builds with what the
    owner has said (Tireless among the first, Group Purge a Druid's first, power back through
    Mystic Crystal Lore and Raging Power), **for the owner to correct**. Before, every 8v8 bot
    bought by the gamebots' four builds with Purge and Mystic Crystal Lore put first.
  - **Passives that did nothing on a bot**, found by reading every calculator (a bot is an NPC
    type): Mastery of Arms and item haste (melee speed took the monster formula), Mastery of
    Healing (heal effectiveness was read for players only), Long Wind (a bot's sprint cost was
    flat), Wild Minion on a pet that is not a Necromancer's. Fixed for 8v8 bots. Veil Recovery is
    bought last by every bot: no bot is ill from a resurrection. Serenity and Ethereal Bond had
    no prerequisite in the bots' plan and have theirs now (all bots). The other twenty-odd
    passives were read and work.
  - **Active abilities a bot now buys and uses**, beside Purge, Group Purge, Ignore Pain, Second
    Wind, First Aid, Mystic Crystal Lore and Raging Power: Speed of Sound, Mastery of
    Concentration, Battery of Life, Bunker of Faith, Soldier's Barricade, Brilliant Aura of
    Deflection, Ameliorating Melodies, Faith Healing, Perfect Recovery, Avoid Pain, Armor of
    Faith, The Empty Mind. Each has one rule with its numbers in the weights (`Abilities`),
    and nothing is pressed outside a fight. Every one of their handlers was written for a
    player: with a bot they returned at once, or threw.
  - **A player's group abilities reach the bots of the group** (`AtlasRAHelpers.GroupInReach`).
    Group Purge, Speed of Sound, Bunker of Faith, Soldier's Barricade, Brilliant Aura, Ameliorating
    Melodies and Faith Healing looked for the *players* of the group: the owner's Group Purge
    freed none of its seven companions. This is engine code and holds for any player with any
    bot in its group, companions outside 8v8 too. Bunker of Faith also compared its range with
    the user's distance to itself; it is 1,500 now, as its text says.
  - **Not used by any bot, and so not bought:** the areas of damage or control, the melee
    abilities, the pet abilities, Majestic Will, Concentration (listed in the guide). Trip was
    read and not touched: a player's Trip is cast on every NPC around it, its own bots included
    as far as the code shows.
  - **For the owner to say:** the game's data gives Speed of Sound to the Minstrel alone; the
    owner remembers it as the Bard's and the Skald's too, and no source was found either way.
    The orders of the Bard and the Skald have it as their third step, so two rows in the class
    table are all it takes.
  *The player's side, 2026-11-19:* picking a rank in `/8v8 setup` above the character's own
  gives the character that rank and its points to spend with `/train` (the owner: "even if I
  pick realm rank 5, I have no realm points and can't train purge"). The owner's Hero stands at
  rank 5 in the scrim database after it; whether the points were spent as wanted is the
  owner's to say.
  *Earned and kept, 2026-11-20:* see M9.1, "a real session". A practice session still
  overrides the rank.
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

*Closed 2026-11-22 on the owner's word ("we can close M3").* The recorder, its formats 3 and 4,
the analyzer, the referee, the replay and the roam page are what every change since has been
judged with. M3.6 as written (thirty matches a pairing on the AI of November 16) was never run:
the referee's rules and the regression gate of M10.3 took its place.

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

- [x] **M3.1** (S) Trace format, versioned: match header (groups, builds, ranks, code version),
  events, decisions, snapshots.
- [x] **M3.2** (M) Event capture: attacks from both sides (a new attacker-side hook is needed),
  casts started, finished and interrupted, crowd control applied, broken and expired, heals,
  realm abilities, deaths.
- [x] **M3.3** (S) Snapshots twice a second: position, health, power, endurance, control state, target.
- [x] **M3.4** (M) Thread-safe writer, one JSONL file per match. Brains tick in parallel, so it
  must not block them.
- [x] **M3.5** (L) Analyzer: a per-match report, an aggregate over a batch, and an A/B comparison
  of two builds with the scorecard metrics.
- [x] **M3.6** (S) Baseline: at least 30 matches per realm pairing on today's AI. Fill the
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
  *Half done 2026-11-20: spotting (`PvpSight.cs`, weight `SightOverGround`). Casting is not
  checked against the ground.*
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
- **The learning tool, third layer: every match draws its weights** (`PvpWeightDraw`,
  `pvp_batch.py Vary=50`, `pvp_insight.py --weights`). Each team of each match fights with its
  own copy of the weights in which about half of them, picked at random, are moved one step down
  or up: a score by 40% and at least ten points, a distance, time or factor by 40%, a health
  percentage by fifteen points, a switch flipped in one team of four. Facts of the game (reaches
  and ranges) are never moved. About 170 weights can move, the class leanings included, and a
  team has about 85 moved. The recording keeps each draw in its first line. The report takes out
  what the pairing of realms explains by itself, then compares for each weight how teams did with
  it down, unmoved and up (a class's own weight only on the teams that have the class), and
  lists the weights by how surely they moved the number of members left alive. With several
  hundred matches only the weights that swing the win rate by six points or more can show; with
  about 170 weights tested, eight or nine pass the mark of 2 by chance, so a weight is believed
  when it passes 3 or shows again in the next batch. The tool reads one match at a time, so a
  night's batch fits in memory, and `--since` reads a night made of several batches together.
- **Re-mez when immunity ends: already there.** The scored choice looks at immunity on every
  turn, so a mez becomes an option again the moment it runs out. In 18 recorded matches enemies
  were mezzed a second time 28 times, every one of them 55 seconds or more after the first mez
  ended, none while immune.
- **Caster pets attack.** The Cabalist's and Enchanter's pets had followed their owners through
  every recorded fight without attacking. Cause: a pet's brain is started only when a player sees
  the pet, and in a scrim nobody does (the Theurgist's pets fought because their summoning spell
  starts their attack itself). An 8v8 bot's pet now thinks whether or not a player is near, as
  its owner does (`AutonomousSummonActivity.IsPvpBotSummon`). Check run of six matches: the
  Cabalist's pet lands about 90 attacks a match and the Enchanter's 14, most on healers and
  casters; before, none.
- **The Cabalist's role** (owner, 2026-11-18). A resistance debuff is a scored kind of its own
  (`Debuff`): on the kill target only, before the damage, and only against the kind of damage
  the caster's own spells do. On this server the tri-spec Cabalist's body debuff is in its Spirit
  line (30% at spec 33, eight seconds). Disease is worth more on a fighter, and more again on
  one that is on the group's healers and casters (`DiseaseOnFighter`, `DiseaseOnPursuer`).
  Two check runs of six matches: three and five debuffs a match; 43 of the Cabalist's 83
  diseases on fighters (before: 4 of 30), 30 of the Shaman's 51 (before: 5 of 15).
- **Regroup.** A fourth order: when half or more of the group's living fighters are farther than
  1,800 units from the middle of its healers and casters, every fighter beyond 1,100 leaves its
  target and comes back, the ones with interrupt duty too. In the lanes this is rare: fighters
  were within 1,350 units of their healers nine times in ten, and the order held for 1% of the
  fight in the check run. It is there for open ground and for the owner's play sessions.
- **The kill target's numbers are weights** (`PvpKillWeights`: class, health lost, distance,
  asleep, how readily to switch), so that the night batches vary them like all the others: the
  owner, 2026-11-18, "maybe sometimes a good target would be a melee while other times it might
  be a caster". New among them: an enemy standing far from its own healers is worth more (two
  points for each hundred units, up to 2,000), the owner's "out of position".
- **Seven more behaviours, 2026-11-18** (one check run of six matches for all of them; 3,261
  automated tests pass on the build):
  - *Healers hang back on incoming* (owner, 2026-11-17). A healer that only heals walks no closer
    than 2,300 units to the nearest enemy until five seconds after the first blow
    (`PvpPositionWeights.HealerHangsBack`). Seen 2.8 times a match, one or two seconds before
    contact.
  - *The Bard plays through the fight.* It keeps its drum out and plays the endurance song,
    twisting its resistance songs in; before, it put the drum away to fight with its blade and
    Hibernia's fighters starved. Under a fifth of their endurance: Blademasters 5% of the fight
    (42 to 65% before), Hero 32% (58 to 65% before). The Bard's mezzes: 2.8 a match against 3.5
    in the run before, which six matches cannot tell apart; the night batch will.
  - *A heal on a member already full is dropped* (`DropHealAtPercent`, not on the trained
    member). It fires 0.3 times a match, because there is little to drop: the "third of big
    heals restore nothing" that prompted it turned out to be the instant group heal reaching
    members who were not hurt and heals over time ticking on a full member. Single heals with a
    cast time that landed on a full member were 6 of 157 in the check run.
  - *The backup healer takes over* while none of the group's healers that only heal can heal
    (`BackupHealsBelowWhenHealersDown`). Unit-tested; not seen on its own in the run.
  - *Healers save power when low* (owner: power has to last the fight): under 30% of its power a
    healer heals a member only below 60% health and the trained member only below 85%.
    Unit-tested; not seen on its own.
  - *One line of retreat under Extend:* a healer's or caster's step is worth more the farther it
    goes straight away from the enemy (`PerUnitAlongRetreat`). Unit-tested; not measured on its own.
  - *Sprint* was looked at and left: on this server it speeds up players only.
- **A night's run** (`source/tools/pvp_night.py`): one plain batch and three with drawn weights,
  about 204 matches each and two and a half hours, the server started fresh for each, then both
  insight reports. `pvp_batch.py` now gives a batch up when the server has not answered for five
  minutes or the batch has run far past what its matches can take, so a night cannot hang.
- **First batch read with the learning tool, 2026-11-18** (150 plain matches, 50 a pairing,
  rank 1, built-in weights, 91 minutes, no error, every match a wipe; reports beside the batch
  logs as `pvp-insight-20261118-203508.txt`). Two readings were added to `pvp_insight.py` for
  it: why the first member died, and how long members sleep and what ends it.
  - *Results.* Albion beat Hibernia 50 to 0 and Midgard 45 to 5; Midgard beat Hibernia 29 to 21.
    In the M5 base of the day before, Midgard beat Albion 7 to 3. What changed since: Albion
    holds at its longer mez range, its Minstrel cures mez, and its two pet classes now attack.
  - *The first death decides.* The side that lost the first member lost the fight in 93% of
    them. It died 36 seconds after contact with four enemies on it.
  - *It dies because its healers are asleep:* 71% of first deaths came while the victim's own
    healers were asleep or stunned, 12% while they were kept from casting. Only 5% were "healers
    free and no heal came", so the healing routine itself is not the problem.
  - *Who wakes the healers.* Albion's healers sleep 3 seconds a match and are cured 93% of the
    time (the Sorcerer and the Minstrel both cure). Hibernia's sleep 33 seconds and are cured 2%
    of the time; Midgard's 21 seconds and 16%. While a Druid sleeps the Bard, Hibernia's only
    cure, is dead 52% of the time, being hit 19%, asleep or stunned 23%. Midgard's Pacification
    Healer: dead 42%, being hit 22%, asleep or stunned 31%.
  - *Who dies first.* Hibernia: the Bard in 60% of its fights. Midgard: the Pacification Healer
    in 51%. Albion, the ten times it lost a member first: the Enhancement Cleric nine times.
  - *Why those two.* Healers and casters walk into the fight. Twenty seconds after contact the
    Sorcerer, Theurgist, Enchanter, Eldritch, Bard and Runemaster stand 41 to 56% of the way to
    where the enemy group stood at contact, among the fighters; the healers that only heal stay
    back (-9 to 7%). They walk because an option on the enemy's back row, reached by a walk of
    1,000 to 1,900 units, outscores what they could cast from where they stand.
  - *Other gaps the report shows.* Hibernia and Midgard leave an enemy healer free to cast for
    about 150 seconds a match, Albion for 49. An enemy fighter is on Hibernia's healers and
    casters for 106 seconds a match, twice the others' figure.
- **Answer to the first finding: a walk that ends among enemy fighters costs score**
  (`WalkEndsNearFighter`, -30 for each free enemy fighter within 600 units of the place the
  caster would stop, up to three). It is a weight, so it is measured the designed way: new
  against old inside the same matches (`--versus`).
  *Result, 48 matches: no effect.* 24 wins each; casters stood 43% of the way to the enemy with
  it and 39% without; 17 walks a team in the first minute against 18. The scored walks are not
  what carries the casters forward (each gains about 110 units, and the place they end is
  seldom beside a free fighter). The weight stays, since it costs nothing and the night batches
  vary it, but it is not the answer.
- **Second answer: a caster with nothing worth casting stands** (`CastersStandWithNothingToCast`;
  the old brain was taking that turn and walking it to spell range of its attack target).
  *Result, 48 matches:* it does move them, 37% of the way to the enemy against 50%, and changes
  no result: 23 wins to 25, the curer first to die 12 times on each side. Kept, as the owner's
  line says casters stand at range; not what decides these fights.
- **What does decide them: the kill target** (found while writing the analysis document,
  `docs/PVP-8V8-ANALYSIS-2026-11-18.md`, which holds the whole reading and the tables from the
  new `pvp_report.py`). Against Albion, Hibernia's kill target in the first 45 seconds is a
  Cleric 100% of the time and Midgard's 99%: called before contact from 3,200 units, kept by
  the switch margin, at the back, guarded, and healed by the other Cleric, who is free 75% of
  the fight. Albion's is the Bard, Pacification Healer or Shaman 59 to 82% of the time: in the
  middle of the field, with their own healers asleep. The score never asks whether the target
  can be killed.
  *First answer, measured:* distance and being out of position weigh twice as much
  (`PvpKillWeights.PerHundredUnits` -5 from -2.5, `PerHundredUnitsFromItsHealers` 5 from 2).
  In 48 matches with the two scores on opposite teams the new one won 28 to 20; Midgard's
  target against Albion was a caster 75% of the time and Hibernia's 54% (a Cleric 99 to 100%
  before); by group, Albion 16 of 16 with it and 12 of 16 without, Midgard 6 and 3, Hibernia 6
  and 5. Now the default. 28 to 20 can still be chance, about one time in seven; the night
  batches vary both weights again. Albion still wins most fights.
  *Still open from the analysis:* a target that is not losing health should lose value; the
  healer that is not being killed is never shut (Albion's Clerics are asleep 1% of the fight);
  the Bard and the Pacification Healer, the only cure for their groups' mez, die first; nobody
  reacts to enemy pets.
Order of work agreed with the owner: finish the brain, with the readings, the group orders and
positioning (hold, kite, extend, push) first; then play matches and record everything; then
analyze and iterate on behavior as deep as wanted. No measure has to be fixed in advance: the
recordings keep every event, so new questions can be asked of old fights. `pvp_analyze.py`
reports the three readings per group from now on.

- [x] **M6.1** (L) Control plan: an area mez opener, assignments per controller, immunity timers
  tracked, re-mez, stuns, roots and snares used on purpose, never breaking the group's own mez.
  *Done 2026-11-18. Immunity is not timed: it is looked at on every turn, which gives the re-mez.*
- [x] **M6.2** (M) Interrupt duty: each enemy healer and caster gets an assigned interrupter, using
  instants, pets or melee. *Done 2026-11-18, the Cabalist's and Enchanter's pets included.*
- [x] **M6.3** (M) Protection: guard pairs set at fight start, slam and snare on enemies attacking
  the backline, cure mez and Purge used with judgement. *Done 2026-11-18.*
- [x] **M6.4** (L) Scored action choice for every role in place of the random pick: debuffs before
  damage, openers, burst on the called target, power and endurance budgeting.
  *Closed as it stands, owner, 2026-11-19: "Let's close and accept current behavior/status."
  The five things below that are still on the old routine are parked under M10 and are not
  work in progress.*
  *Open, 2026-11-18: the casters, the healers' control and the Skald choose by score, with
  debuffs before damage and power weighed. Still on the old routine with 8v8 rules laid over it:
  which heal a healer casts, the fighters' styles, and the Minstrel's shouts and mezzes (scoring
  those was tried and taken back). Nobody budgets endurance.*
- [x] **M6.5** (M) Upkeep during fights: bladeturn chants, endurance and speed songs, resists,
  and full buffing before a fight. *Done 2026-11-18: the Paladin twists three chants, the Bard
  plays endurance and twists resistances in a fight, groups are fully buffed before they march.*

**Exit check** (reworded 2026-11-19 with the owner, who accepted the candidate below): every
line of M6 is built and seen in a recorded match, and the first night's batch and its report
are on record. **Met.** The pass, over the twelve bots-only matches recorded on 2026-11-19
after 18:00 UTC (nine in lanes, three in the basin, realm rank 5):

| What M6 asks for | Seen as | In how many of 12 |
|---|---|---|
| An area mez to open (M6.1) | an area mez cast in the first 10 s | 12 |
| Control by assignment (M6.1) | decisions Mez, MezArea, Root (Stun in 8) | 12 |
| Interrupt duty (M6.2) | a member holding a duty target; nearsight, disease, amnesia chosen | 12 |
| Guard (M6.3) | guard decisions, and hits taken by the guard for its ward (136) | 12 |
| Peel and intercept (M6.3) | decisions Peel and Intercept; Slam landed (142) | 12 |
| Purge by score | decision Purge (Group Purge in 5) | 12 |
| Waking the mezzed | decision CureMez (the cure cast in 8) | 11 |
| Upkeep in a fight (M6.5) | endurance song, damage add, bladeturn (combat heal chant in 7) | 12 |
| Scored choice (M6.4) | Damage, Nearsight, Disease, Amnesia, Pet decisions with their scores | 12 |

One thing the pass turned up and nobody has looked into: 21 of 236 mez casts finished on a
target whose last snapshot showed mez immunity. It may be casts begun before the immunity
started. Parked with the rest.

*The check as first written:* wins at least 70% of mirrored matches against the M5 build; first-mez time and
enemy-healer free-cast share meet their targets.
*Note, 2026-11-18: this cannot be measured as written. Both teams of a match run in one server,
so two builds cannot fight each other; only two sets of weights can (`--versus`). For the owner
to reword, as M5's was. A candidate in line with the working direction: every playbook line for
M6 is built and seen once in a recorded match, and the first night's batch and its report are on
record.*

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

- [x] **M7.1** (M) Range bands per role and a backline anchor: healers and casters hold distance
  from enemy melee while staying in range of their group.
  *Built (`PvpPositioning`, `PvpSquad.StandOff`); measured 2026-11-20: a healer has an enemy
  fighter within 250 for 13 to 17% of the fight (target under 15%), and 8% of deaths are beyond
  heal range (target under 10%).*
- [x] **M7.2** (M) Reactions to pressure: kite when attacked in melee, sprint, step out of area
  effects, move to regain sight or range.
  *Built: kiting when pinned, sprint when pressed or with ground to cover, the walk back into
  range. Closed 2026-11-20 without stepping out of an area effect (parked under M10). The back
  line moves 34 to 41% of its time: a third of that is running from a fighter that is on it and
  a fifth is keeping its distance; making the group's order change its distance later
  (`Order.SafeFactorAfterSeconds`) changed nothing in 24 matches and is off.*
- [x] **M7.3** (M) Melee movement: stick to the target, reach the position a positional style
  needs, stop chasing past the group's support range.
  *Built: behind a stunned target for the style from behind; the Regroup order when fighters are
  far. Closed 2026-11-20 without side positions (parked under M10). Fighters are beyond 2,000 of
  every healer 4 to 5% of the time.*
- [x] **M7.4** (M) Cohesion: formation while roaming, a tight group on approach, regrouping during
  a fight.
  *On the road every member follows the driver, who waits for stragglers; the back row stops at
  3,200 and lets its fighters go on; Regroup and the owner's `/8v8 call regroup`.*

**Exit check:** healer-distance and overextension targets met; wins at least 65% against the M6 build.
*Closed on the owner's word, 2026-11-20 ("let's move on finishing this Milestone and let's move
with the remaining milestone work"): the two distance targets are met or within two points
(above); the win rate against the M6 build was never run and is not a target (2026-11-18).*

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

*Closed 2026-11-22 on the owner's word ("I think we can consider the exit also done"): sessions
from the launcher, a character readied, the group formed, roams and fights and rematches, with no
GM command. Built and run with bots, and not reported on by the owner from a client: the roam in
Thidranki, `/8v8 status`, a group of bots resting off its road, `/8v8 call control`, clicking and
raising a dead bot, a real session with the realm abilities of 2026-11-22, and the owner's own
group abilities reaching the companions. They are checked as they come up in play, not as steps.*

*Owner, 2026-11-17:* "I am by no means an 8v8 expert; I am going from memory." Once the owner can
play, every one of their fights is to be recorded, and the recordings and the analyzer used both
to improve the bots and to give the owner insight into their own play. Needed for that: the
recorder taking a human member (today a team is bots only), and a report written for the player
(time spent mezzed or interrupted, what the group's scored choice would have done in the same
spot, what the enemy did to them). The owner also remembers a captioned video explaining the
basics of an 8v8 fight and will look for it: an outside reference to check the bots against.

*The way to a first session the owner can play (2026-11-19).* What exists: `/8v8 group [rank]`
puts seven companions of the realm's main group around the player, buffed, following. What is
missing is that the 8v8 brain knows bots only: a squad is a list of bots, its enemies are a list
of bots, and a kill call, a mez, a peel or a guard reads a bot's build. An enemy group would
never call, mez or train the owner, and the owner's companions do not fight as a squad at all.
In order:

1. [x] A member is a bot or a player (`PvpMember`: class, archetype, role, build name). Every
   place that read a bot's build asks it now. No behaviour changes for bots.
2. [x] The enemy sees the player: the squad's lists of enemies, the kill call, the scored
   choices of crowd control and damage, interrupt duty and the peel take any living thing, so an
   enemy group calls, mezzes and trains the owner like any other healer. Done as far as the
   code goes (a player counts as an attacker and as a pet's owner too); six recorded matches of
   bots against bots ran as before. It has not been tried against a player yet: that comes with
   step 4.
3. [x] The owner's side fights as a squad: a squad is its bots and the players with them
   (`PvpSquad.Allies`), and a player counts among its healers and casters: one of those a
   shield fighter may guard, a patient when the healers share out their heals, the member the
   enemy is training, somebody to stay in heal range of, to wake, and whose pets are cleared.
   The squad still steers only its bots. Done as far as the code goes; three recorded matches
   of bots against bots ran as before. Not tried with a player yet: step 4.
4. [x] `/8v8 play <enemy realm> [rank]` (M9.4): both groups in the open basin of Emain Macha,
   the enemy roaming a route, the companions travelling with the owner until the enemy is in
   sight, a rematch after each fight, `/8v8 stop`.
   *Done 2026-11-19: played by the owner about ten times that evening, in the lane and in the
   basin, as a Cleric and as a Hibernian, against Midgard, Hibernia and Albion ("Basin works
   fine, the initial contact was ok"; "it's fun tho"). The first three tries failed and were
   fixed the same hour: the enemy squad took the player for a bot and the session stopped; the
   start after the teleport gave up while the zone loaded.* `/8v8 setup` opens a window with clickable
   choices (enemy realm, start, realm rank, Start), the way `/spawn` does; `/8v8 play mid 1 lane`
   is the same typed. Two starts (owner, 2026-11-19: "face to face, or roam and find each
   other?"): **lane**, face to face 6,000 units apart as in the scrims, the default and the one
   for a first try; **basin**, opposite ends 21,500 apart and out of sight, the enemy patrolling.
   Either takes the player to the start and begins when the player stands there, with seven
   companions of the realm's main group around the player and the enemy group at the other end. Both sides run the squad brain; the
   companions follow the player until an enemy is within reach, the enemy patrols the basin as
   in a scrim. One fight of at most 30 minutes, recorded as `<stamp>-play-<a>-vs-<b>`; the same
   command again is the rematch; `/8v8 stop` ends it. A GM character can be attacked while its
   session runs (it cannot otherwise). Checked only by the 3,322 automated tests and three
   bots-against-bots matches that ran as before; nothing here has run with a player in it.
   Known gap: the player's own character has to be level 50 with a build and gear, which is
   M1.4 and not built. Found on the way: a real player's class was not read at all
   (`GamePlayer` is not an `IGamePlayer`), so the player counted as a fighter of no class;
   fixed, with a test.
5. [x] The recorder takes the human member (M9.5): the owner in the match's list and in every
   second's rows, so the referee and `pvp_replay.py` read a session like a scrim.
   *Done 2026-11-19, in every session the owner played.* In a play session the player is in the
   match's member list (`player: true`, build `player-<class>`), has a row every second like a
   bot (what a brain decides stays empty) and is the named actor of its hits, casts and death.
   Three bots-against-bots recordings after the change hold the same rows as before. Whether
   the referee and `pvp_replay.py` read a session cleanly waits for the first real one.
6. [ ] The roaming start for both sides (M8.4): who sees whom first depends on where a group
   is facing. *Built 2026-11-20 (roam between nine points, sight over the ground, sight by
   facing); the roam and the ground seen in recorded matches, the facing not: see "The night of
   2026-11-19 to 20" near the top.*
   *Progress 2026-11-19, from the owner's first evening of play* (ten sessions as a Cleric
   and a Hibernian healer; "it's fun tho", "I think the bots are mathematically good"). Facing
   is still not in it; what changed is the shape of a group off the fight (`PvpFormation.cs`):
   - *"We are way too tight on initial contact."* Counted in six sessions: every companion
     within 160 units of the player eight seconds before the first blow, all eight inside one
     area mez, and two to seven still at the first blow (the enemy: one to three). With an
     enemy within 6,500 units the companions now take two rows around the player, the player's
     place left free; in a lane they start in them. **Not yet seen with a player.**
   - *"The opposite team runs as a two-lane rectangle in the basin."* On the road in the basin a
     group runs as a loose clump (neighbours about 300 apart) and takes its rows on sight.
     Checked in three bots-only basin matches: at the first blow neighbours 307 to 488 apart
     and two or three members in one mez, as in the lanes (450 and two). A first try with a
     tight clump (130) left both groups in a ball three seconds before contact, and was dropped.
   - *"The bard is not playing endurance as we roam... it does not need to cast speed over
     and over."* Off the fight the Bard holds one song: speed on the road (it had re-cast it 24
     times in 85 seconds, slipping resistance hymns in), endurance with an enemy within 9,000
     units. In the third match the song was up five seconds before the first blow.
   Found on the way: a companion healer walked back to the player's side between casts, in
   fights too (the owner: "stuck to me most of the fight doing nothing"; it moved 65% of the
   time and cast a third less than the same build among bots). Off in a fight since.
   Three things stood out in single fights and were then counted over every recording of the
   day before anything was changed. Two were not there:
   - *The bots healing the player very little* (116 and 186 points in two fights). Over the
     nine sessions that ran to an end the player got 53 points of healing from others for
     every 100 of damage taken, the bot healers beside the player 38, and bot healers among
     bots 37. Not a fault.
   - *The Armsman swinging without a style on full endurance* (10 of 16 hits in one window).
     Over all its landed hits the Armsman styles 98%. The builds that show 52 to 60% are the
     two-weapon ones (Blademaster, Berserker, Savage), whose off-hand swing never has a style.
     Not a fault.
   - *Casters out of power.* This one holds. At realm rank 5, over about 100 fights a build:
     the Bard runs under 10% power in 70% of its fights (median 58 s after the first blow),
     the Theurgist in 69% (63 s), the Skald in 68% (63 s), the Minstrel in 53% (89 s), the
     Cabalist in 42% (129 s); every other build in under a third. The Theurgist spends it on
     pets, 17 a fight at 12% of its power each. Not acted on: what a group does about power
     (pace, potions, who keeps a reserve) is the owner's to say.
7. [ ] Calls (M9.2, M9.3).

- [x] **M9.1** (M) A lasting companion roster built around the owner's class, earning realm points
  with the owner.
  *First half built 2026-11-20, not yet seen with a player:* a character's seven companions
  keep their names and sex from one session to the next (`PvpRoster.cs`,
  `pvp-rosters.json` beside the server, by character and by build). It is the player's own
  data: the release builder leaves it out and refuses a package that holds one.
  *Second half built 2026-11-20, not yet seen with a player: a real session.* The owner: "we
  have a nice way to playtest 8v8 in line and basin, but everything is just for testing. All
  abilities reset, no rps gain, only 2 groups, no real roaming". `/8v8 setup` has a Rules line
  (Practice or Real) and `/8v8 play <realm> real` is the same typed. Practice is what there
  was. In a real session (`PvpRealmPoints.cs`):
  - everybody starts at rank 1 and fights at what it has earned in real sessions (the owner,
    the same evening: "the real will have us start at rr1 and gain rps and see how it goes. We
    would need to persist bots and companions data (or at least, rps gained). I guess if we add
    teams, we'd need to make sure to persist all the bot / companions and their rps"): each
    companion, the enemy group (never below the average of the player's group,
    `Real.EnemyKeepsUp`), and the player's own character, whose earned points are kept in the
    roster beside the companions'. A real session puts the character at that rank, up or down,
    and going down hands its realm abilities back (a practice session may have raised it; the
    setup window says so before Start). *To look at before this goes into a release: a
    character with realm points from outside 8v8 would be put at its 8v8 rank too.*
  - what is kept, in `pvp-rosters.json` beside the server: for each character its own points
    and its seven companions by build and place, and for each enemy group it has fought that
    group's eight members, each with name, sex and points. A second team of the same group in
    one session will need its own roster name (not built: there is one enemy group today);
  - no timer is reset, at the start or when both groups are ready;
  - a death is paid for once (again if the member is raised and dies again): what a player of
    that level and rank is worth (900 for a level 50 of rank 1), times `Real.RealmPointRate`
    (5), shared evenly among the members of the killing group within 6,000 units, the dead
    among them too. The player's share goes on the character, a companion's into the roster,
    an enemy's into that enemy group's own roster (kept per character and group, by name);
  - a bot's rank moves when it next comes into a session, not in the middle of a fight;
  - `/8v8 ranks` lists the companions and the enemy groups fought for real.
  Not in it yet: more than two groups, a roam that goes on from fight to fight, what a wipe
  costs, and realm abilities bought in the build's order (a bot buys what its points allow by
  the rule the gamebots use).
- [x] **M9.7** (L) The roam: several groups in one zone, a session that goes on (added
  2026-11-20). The owner's answers that evening, which are the design:
  - *Zone.* "Are battlegrounds enabled? Navmesh good or should we generate them?" The four
    classic ones (Abermenai 253, Thidranki 252, Murdaigean 251, Caledon 250) are in the world
    data and have navmeshes; nobody has checked those meshes for 8v8 movement, and the world
    data caps them by level (Thidranki 20 to 24). Cathal Valley and New Frontiers have none.
    Emain Macha first (the basin is proven), Thidranki as a second map once its mesh is checked.
  - *Groups.* "There could be 2 group for each realm. PvP ruleset is not enable on this so they
    would be fighting any of the opposite realm team." Six teams: the player's, one more of the
    player's realm, two of each other realm; a team fights any team of another realm.
  - *Adding on a fight.* "should the 2nd team from a realm that is already engage join the fight
    or not. It could be random, like 30% would add 70% won't add or so. It always been a heated
    discussion, should we assjam or not an ongoing fight. Most of the respected 8v8 team were
    not." A bot team that comes on a fight its own realm's other team is in draws once: 30% it
    joins, 70% it leaves that enemy team alone until the fight is over (unless it is attacked).
    A setting. The player's team does what the player does.
  - *Rank and pace.* "Start at Realm Rank 1 Level 1. We could earn rps at x5 or x10." Built
    (M9.1): rank 1 level 1, five times.
  - *A wipe.* "We revive at our start point." Every team has a start point; a team with nobody
    left alive stands up there after a short wait, rested and buffed, the player's too.
  - *Line-ups.* "Let's start with the main group that we defined for now."
  - *Kept.* Every team of the session has its own roster (name, sex, points of each member):
    the player and the seven companions, and each of the five bot teams, the second team of a
    line-up under its own name.

  Steps:
  1. [x] The movement of a team (march, engage, rows, following a driver or the player) taken
     out of the two-team session into something a session can have any number of, the two-team
     session unchanged in what it does (a bots-only batch before and after).
     *Done 2026-11-20 (`PvpTeam.cs`). Six lane matches after it ran as before: a healer with an
     enemy fighter within 250 for 11 to 20% of the fight, the back line 500 apart, healers moving
     40% of the time, the stops at range and the healers hanging back recorded as before.*
  2. [x] A roam session of bot teams only, started from the control channel, to be read from
     its status and the log: six teams, start points, wipes that stand up again, the draw for
     adding on a fight.
     *Done 2026-11-20 (`PvpRoam.cs`; `roam-start`, `roam-status`). Where things are, from the
     installed navmesh (`UT_PvpRoamStartRoutes`, a probe): the two portal keeps are 48,500 units
     apart and there is no way between them over the mountains; the way goes west round them,
     through the open middle of the zone, whose two ends are 38,000 apart on foot. From the
     Midgard keep to the north end is 26,600 and from the Albion keep to the south end 35,500;
     the hill the owner picked for Hibernia (`/loc` 22984,51041) is 6,600 from the south end.
     Asked for the Albion keep in one go, a path from the Midgard keep ends in a dead end
     21,000 short, so a team walks from point to point (`PvpTeam.Route`).
     The first run, eight minutes: Hibernia's teams walked the middle in about 100 seconds a
     leg and the four teams that started in the middle of their keeps never left: the way out
     goes through two keep doors and a bot does not open one. They start in front of the outer
     gate now. The second run, eleven minutes: first blows at 138 s; fights of two, three and
     four teams at the north end of the middle; five wipes and each team standing up again;
     four draws on coming on a realm-mate's fight, all four "leaves them to it"; each team's
     points in its own roster (9,008 a member for the Albion team that had killed sixteen, 0
     for the Hibernian team it had wiped twice on its own hill). That last one is why a wiped
     team now waits while an enemy is within 5,000 of its start (two minutes at most).
     Not in it: a team with some members dead and nobody to raise them goes on short until it
     is wiped; the recorder (step 4).*
     *Two more runs that night.* The third, on the ranks the teams had earned (1L6 to 2L1): a
     Hibernian team waited its two minutes because the Albion team that had wiped it stood on
     its hill, stood up, and was attacked a second later. So a team of bots does not start a
     fight with a team within 3,500 of that team's own start (`Real.StartIsSafeWithin`: nobody
     camped a portal keep, whose guards saw to that); a fight already on goes on, and a team
     attacked from its start answers. The fourth, with that: both Hibernian teams wiped at 308
     and 323 s and stood up at 354 s with nobody on the hill; nine draws, four of them "joins".
     A draw is one for a fight now, not one for each enemy team in it.
  3. [x] The player in it (`/8v8 roam`): companions, the wipe and the walk back, realm points
     to whoever made the kill, the rosters.
     *Played by the owner 2026-11-21, twice, as a Hibernian:* "it seems to work. Mids went to
     Albion gate and albion went to mid gate. I fought both in 2 different fight somewhere on
     their way to the other realm gate". One fault, "The 2nd hib team never left the spawn
     point": it left 135 seconds in (45 of buffing and a gap of 90 behind the realm's first
     team), which is when the first Albion team reaches Hibernia's hill. A realm's teams of
     bots now leave 30 seconds apart, and the second team of the player's realm is its first
     team of bots and leaves after the 45 (`Real.BuffsAtStartSeconds`,
     `Real.NextTeamFollowsAfterSeconds`). Seen in the log of the same two roams: a draw of
     "leaves them to it" held for one side only (the Albion team the second Hibernian team had
     left to the owner's attacked it eight seconds later, and the owner's companions went for a
     Midgard team that had left them alone). It holds for both now, until one attacks the other
     or the player takes one of them as a target (`PvpRoam.LeavesAlone`). **Neither change has
     been seen with a player.** Not yet reported on by the owner: a wipe and the standing up.
     *Built 2026-11-20.* `/8v8 roam` takes the player to the
     start of its realm and starts the roam with the player at the head of the first team of
     that realm: seven companions that go where the player goes, the rules of a real session,
     and five teams of bots (the second of the player's realm among them). The player's team
     makes no draw on coming on a fight and is not kept from an enemy's start. A wipe: about
     thirty seconds later, and not while an enemy stands at the start, the player is alive
     again at the start without sickness, with the seven around ("do not release": a player
     who releases is not brought back). A kill pays the bots of the killing team into its
     roster and the player on the character and into the roster. `/8v8 ranks` lists both teams
     of every line-up met. To decide with the owner: neither portal keep has a healer (guards,
     a teleporter, a hastener, a merchant), a raised bot has no resurrection sickness while a
     player has, and the server's own note says sickness from a realm-war death is not one a
     healer cures.
     *The same day, from three roams of bots watched as they ran* (none of this seen with a
     player):
     - With the half-minute gap a realm's second team reaches its first team's fight while it
       is on. One that drew "leaves them to it" walked on along its route, which is the road
       the fight is on, and was in it twenty seconds later. It now **waits where the draw
       fell** (some 9,000 units off) until that fight is over or an enemy comes for it
       (`PvpRoam.WaitsOutAFight`; seen: a Midgard team standing 8,000 units from its
       realm-mate's fight for three minutes).
     - That Midgard team watched its realm-mate fight two Hibernian teams, the second of which
       had drawn "joins". A team that left a fair fight alone now **joins when the other side
       adds**: two or more enemy teams on its realm-mate (`PvpRoam.EvensTheOdds`). Built, not
       yet seen in a run.
     - **Release.** A player who released went to its bind point in another region, and a
       player out of the zone was taken for one who had left the game: the roam ended. A
       player at the head of a roam team now releases to its team's start, and its group
       stands up there once the seven are down too (`PvpRoam.ReleasePoint`, `IsWiped`); one
       who releases while companions still fight waits for them.
     - **The setup window has the roam**: Start is Lane, Basin or Roam (the owner, having
       picked Real there and found two groups: "There should be 6 teams total?"). The roam
       takes the "Your group" pick (`/8v8 roam hib-2bard` typed); the enemy, rules and rank
       lines are not asked. "Follow" is not in the roam: the player leads.
     *From the owner's third roam, 2026-11-21 (fourteen minutes in Emain Macha, recorded):*
     - "Dead body still doesn't work. Now when it dies, there's also a level 1 unarmored body
       that appears." The client does lay a dead player down. The monster beside it had been
       sent the removal of a player and stayed; the body came without its gear; and the client
       asked the server for the player it had been given and got no answer. All three changed
       (`PvpCorpse.cs`). **Not seen in a client.**
     - "When /8v8 roam, group should not spread / bard play endurance if we do not engage."
       In a roam the player's group reckons only with an enemy team it is fighting, one that
       attacks it, or one the player has targeted a member of within 6,500 units; any other is
       passed by in travel order, the Bard on its speed song (`PvpRoam.PlayerTakesItOn`). A
       single fight (lane, basin) is as before. **Not seen in a client.**
     - "I think I am also gaining rps for just being close to the fight." A team was counted
       in a fight when one of the two was in combat with anybody and the other within reach, and
       was paid for that team's deaths. Two teams are in a fight now when somebody of one is in
       combat and aimed at somebody of the other (`PvpRoam.AtEachOther`).
     - "My group, even though the bard was dead, were getting the other group bard endurance
       song. It should be per group." **Not found.** By the code a song goes to its singer's
       group only, the two Hibernian groups are two groups, and in the recording the other Bard
       was 4,700 to 12,500 units away (the song reaches 2,000) while the player's endurance
       came back at five a second after its own Bard died at 370 s. `roam-status` now lists
       any helpful effect on a member cast by a member of its realm's other team
       (`FromOtherTeams`), to settle it in a run.
     - "I just fought one of the albion team. For some reason, we had no endurance up during
       the fight... bard was not dead." In the recording (1337 to 1640 s, eight against eight):
       the Bard had its endurance song up at 1331 and 1375, was trained and ran out of power
       (23 amnesias, 9 mezzes begun), and **swung its blade** at 1399, 1401, 1410, 1412 and
       1465. Drawing the blade ends the song: from 1420 its Blademasters stood at 11 and 29
       endurance and the owner at 9, until it died at 1565. Two fallbacks written for a bot
       that fights monsters did it (hit in melee: defend with the blade; no power: walk in and
       fight). An 8v8 Bard takes neither now (`BotBrain.PlaysThroughTheFight`). **Not seen in a
       run.** Still as it was: in a fight it starts the song at most every twenty seconds, and
       not while it is being hit or a groupmate is asleep.
     - "I feel like 30% add 70% run by is too high. I feel like it always ends up like a huge
       brawl with all the teams." Counted over the 48 minutes of that roam: nine fights, three
       of them with all six teams in; 36 draws, 8 of them "joins" (22%); and "joins after all"
       had overturned 16 of the 28 others, so two draws in three ended in an add. That rule
       fired for a realm-mate that had itself jumped a fight (it then has two of one realm on
       it by its own doing): it is now for a realm-mate that was added on in a fair fight
       only. And the chance is **15** ("maybe 15% add 85% run by"). Emain Macha's one road
       still brings all six to the same place at the same time; Thidranki does not.
     - **`/8v8 call retreat`** ("retreat: everyone stop doing what they do and follow me"; also
       `run`, `follow`). Not a regroup, under which the group goes on fighting: every bot of
       the group drops its cast, its attack and its targets, answers no attack, and runs
       behind the player in travel order, the speed song on (`PvpSquad.CallRetreat`,
       `BotBrain.TryPvpRetreat`). It stands 30 seconds, is renewed by calling it again, and
       ends with any other call. "retreat" had been another word for regroup. **Not seen in a
       client or a run.**
     *The same afternoon, after the owner's fourth and fifth roams:*
     - **Two Bards, a song up, and no endurance.** "We had no endurance most (if not) all the
       fight but endurance song was up. It's like if I just never regain any." In the recording
       (the owner's group with two Bards): both Bards had begun the song, and for four minutes
       the owner landed no styled swing in 49 and the two Blademasters 1 and 3 in about 40
       each, against well over half before. An engine fault of two singers of one song: when
       both pulse in the same tick a member's effect is replaced in its list by the other's
       copy, and the copy that was replaced is later ended as if it were the live one, which
       takes the bonus away again. **Shown with bots:** a roam of twenty minutes with two Bards
       in each Hibernian team and the fix switched off had what the songs add to a member's
       regeneration at -80 to -105 for 12% of one team's time (it is 0 or 5). A replaced copy is
       now retired without that (`EffectListComponent`, counted as `StaleCopiesRetired`).
       *With the fix on:* the same roam for nineteen minutes had both teams at 0 or 5 and
       nothing below; and in the roam the owner played that evening with two Bards the server
       logged ten copies retired for the owner's own group in two and a half minutes (each one
       a bonus taken away twice before), with the group's bonus at 0 or 5 throughout and the
       endurance song running 98% of its fight time. A snapshot of a
       recording now has what a member regains (`er`), what its songs add (`erb`) and the song
       a singer has running (`song`).
     - Seen on the way: sprinting costs a bot 5 endurance a second on the move and a player
       the same by the game's rule, and the endurance song gives 5. Whoever sprints and moves
       regains nothing. Left as it is; the owner's to say.
     - "Let say mids are fighting alb and I walk by. Neither mids or albs will try to attack my
       group, as they are already fighting. Only if I decide to add on their fight, they will
       need to react." A team of bots that is in a fight takes on nobody new until that one
       attacks it (`PvpRoam.TooBusyFor`); before, only a team of bots that had drawn "leaves
       them to it" was spared, and the player's group was attacked by both. A player counts as
       attacking once it is in combat, not for looking at a target. **Not seen in a run.**
     - "Hibernia could start at Dun Crauchon to balance the starting location." It starts
       2,400 units in front of that keep: 26,400 on foot to the south end of the middle and
       33,000 to the north end, against 33,700 for Albion and 24,800 for Midgard to theirs
       (from the hill it was 6,600). Both ends are reached directly, so Hibernia's first team
       goes south first and its second north. *A roam of bots, five minutes:* both teams left
       the keep; the second met a Midgard team at the north end at 2:16 and the first, having
       found nobody at the south end, a second Midgard team at 3:46: two fights of eight
       against eight side by side, the two Albion teams waiting. Not seen with a player.
     *Seen by the owner in a client, 2026-11-22* (a roam of eleven minutes in Emain Macha):
     "Hib starting location is ok"; "Retreat call is ok"; "We died on our 2nd fight and I just
     waited. We all respawned at correct location, without rez sick (that's ok)"; "group no
     adding looks like it's working". So Dun Crauchon, `/8v8 call retreat`, the standing up
     after a wipe and the bots leaving a fight alone are confirmed with a player.
     "I am not sure if /8v8 call extend works. I used it but I can't tell." The call was taken
     (in the one roam it was used in, at 178 s), and the group had been on Extend by its own
     reading for eight seconds already, so nothing changed: its back line stood 500 units from
     the nearest enemy before and after. And the call was worded as a chase ("They are backing
     off: extend on them", the word `chase`), which is the opposite of the order: Extend is the
     kite of the playbook (the group falls back, healers and casters keep their distance, the
     fighters turn on whoever follows). Reworded, `kite` in place of `chase`.
     The owner then said what each call is to be: "Push should be us pushing into enemies.
     Extend should be us kiting back, in the opposite direction of the enemy team. Engage
     should be us just attacking. Retreat should be us all running away." Three were that
     already. **A called Extend is now a kite to be seen** (`PvpCalls.KiteStepWanted`): every
     healer and caster not in the middle of a cast steps 500 units straight away from the
     enemy, every two and a half seconds, while an enemy is within 1,800; the fighters cover
     as under any Extend. The group's own Extend is as it was. **Not seen in a run.**
     And of a fight that is over: "When the fight is always over, like my team only have 1-2
     members vs 6-8 enemies, there's no point in keeping all the rules actives. It just drags
     the fight for no reasons." The last stand of 2026-11-20 was for the losers only. Counted
     in that morning's roam: the last one or two of a beaten group lived a median of 15
     seconds more and 54 at the longest, mezzed or stunned 37% of that time, while the winners
     went on getting away, spreading, standing off and kiting. A group that has won now
     **mops up** (`PvpPositionWeights.MopsUp`, the mirror of the last stand): no mez, amnesia
     or nearsight, no keeping its distance, and every fighter on the kill target, its guard and
     its peel too. **Not seen in a run.**
     Still not reported on by the owner: the dead lying down, `/release`, the calls for a
     landed mez and a used Purge, `/8v8 call control`, Thidranki.
     *The same morning:* "For some reason, the bard is not playing endurance. We just killed 4
     midgard fighter and ran away." The recording (which now has the song a singer has
     running) says otherwise for endurance: in each of that roam's four fights the song was
     running within a second of the first blow and for as long as the Bard lived, and the
     owner had its bonus 60 to 100% of the time. What it shows is the speed song missing: from
     the fight at 16:41 on the Bard played endurance on the road and on a retreat too, four
     and a half minutes without speed. A group gets a new squad when a fight is over, and its
     bots were given it only when it first decided something, which needs an enemy. Until
     then they read the old one, whose fight never ends: the speed song is held back in a
     fight, and so is every buff with a cast time on somebody else, and a call made between
     fights went to a squad no bot listened to. A squad's bots are given it when it is made
     (`PvpSquad`). **Not seen in a run.** The owner, asked: "I meant to say speed, not endurance. It was playing only endurance."
     And of `/release`, used for the first time: "My group just wiped. I used /release and
     respawn at correct location, with rez sickness. The whole team did not release." The
     group was wiped at 21:49 of the roam, the owner was at the start ten seconds later, and
     the seven stood up there at 22:19, when the thirty seconds of the wipe were up: twenty
     seconds alone at the start, which read as the group not coming. A group whose player has
     released stands up at once (not under an enemy's feet), and a release in a roam gives no
     resurrection sickness, as the standing up by itself gives none. **Not seen in a client.**
  4. [x] The recorder with more than two teams, so a roam can be replayed and counted.
     *Done 2026-11-21.* A roam writes one recording for the session in `logs/roams` (format 4,
     `PvpRecorder.BeginRoam`): the header names every team, every line of a match is there with
     the team's own name where a match has A and B, and a roam adds a line a second for each
     team (state, place, whom it fights), its events (sets off, fight, draw, waits, wipe,
     stands up, points paid) and the members of a team that stood up again (new bots under the
     old names). A team with no enemy near is written every fifth second: five minutes of six
     teams came to 5 MB. `source/tools/pvp_roam.py <recording or folder>` prints the events,
     every fight (found from the blows: who began it, who came later and when, deaths, who was
     left standing, where) and a line a team (fights, time in fights, killing blows, deaths,
     wipes, points). `--cut` writes each fight of exactly two teams as a match recording, which
     the tools for a match read as they are: checked with `pvp_referee.py` on a fight cut from
     a roam of three teams. A fight of three teams or more is counted and not cut.
     Seen in the first recorded roam of six teams: all six met on the one road within ninety
     seconds, and the only fight had four teams in it. That is what Thidranki (step 5), with
     its three starts round a middle, may do better.
  5. [x] Thidranki as a second map.
     *Done 2026-11-21, with bots only: **not yet tried with a player**.* `/8v8 roam thid`, the
     **Thidranki** start of the setup window, `roam-start Map=thid`. A roam has a map now
     (`PvpRoamMap`: region, starts, routes), Emain Macha being the first.
     What the world data and the installed navmesh say of Thidranki (region 252): three portal
     keeps round a neutral keep with guards, 33,700 to 40,900 units from each other on foot
     and 16,300 to 27,700 from the middle, every way between them within 8% of the straight
     line. Its level cap (20 to 24) acts on the zone's own doors and on a logout there, and
     stops neither a level 50 who is put there nor the points a roam pays itself.
     Each realm starts 2,500 units in front of its portal keep. Three points to walk between,
     one between each two realms, none within 8,500 of the neutral keep; a realm's first team
     goes round them one way and its second the other, so each point is the first of two
     teams of two realms.
     The first run: the six met as two fights of eight against eight at 110 and 112 s, and a
     team of the third realm was in each within a minute. So **the draw is for any fight that
     is on**, a realm-mate's or one between the two other realms (`PvpRoam.ComesOnAFight`):
     three times in ten a team joins, and otherwise it waits where it is and takes on whoever
     is left ("should we assjam or not an ongoing fight. Most of the respected 8v8 team were
     not"). To ask the owner: a team that waited attacks the winner as soon as the fight is
     over, without letting it rest.
     The third run, seven and a half minutes: four fights, three of them of two teams from
     first blow to last (cut and read by `pvp_referee.py`); three wipes and each team standing
     up at its keep; a Midgard team that came on Albion fighting Hibernia waited 51 seconds and
     then fought the Hibernian team that had won; 9 MB of recording.
     Also from these runs:
     - "Joins after all" was taken for an add when its realm-mate was in a three-way fight (one
       team of each other realm on it). It is for two teams of *one* realm on its realm-mate.
     - **After a fight a team of bots sees to its dead before it walks on** (it walked on short
       before, the dead left where they fell): with a member alive that can raise, it gathers
       at its dead for up to 90 seconds; with nobody to raise them, or when that time is up, it
       goes back to its start and forms up again as after a wipe
       (`Real.RaisesItsDeadForSeconds`, `Real.ReformsAfterSeconds`). Built; the three runs had
       no team left in that state, so it is unseen.
     - A second cloak was refused with an error and a stack trace in the log for every 8v8 bot
       made, 2,000 times that morning: a bot that wears one is not given another.

- [x] **M9.2** (M) Bot shot-caller: calls announced in group chat (assist target, mez landed,
  retreat), with an assist key for the owner.
  *Seen by the owner in a client, 2026-11-22:* "The mez and purge in /g chat also works"; the
  assist call earlier the same weekend ("Assist works great. I've been using it").
  *First cut 2026-11-19, not yet seen in a client.* In a group with a player, its bots say in
  group chat what the squad already decides (`PvpCalls.cs`): the assist ("Assist Finnarar
  (Healer), at 62%."), the order when it changes (push, regroup on the healers, extend, hold),
  incoming on a member with two or more on it ("3 on you, Nayas!"), a member out of power, and
  a caster calling the group to stack for pets. Orders no more often than every 5 seconds and
  incoming every 8; `/8v8 calls off` silences them.
  *Added 2026-11-21, not yet seen in a client:* "3 of them are mezzed." when a mez puts two or
  more of the enemy to sleep (said by the member that mezzes, no more often than every six
  seconds), and "Purge used." / "Group Purge used." by the member that used it.
- [x] **M9.3** (M) Owner's calls: `/call` macros for target, push, retreat, control that one,
  regroup. The squad brain treats them as its own decisions.
  *Seen by the owner in a client:* assist and retreat (2026-11-21), the kite of `extend` and
  `rest` (2026-11-22: "The M9.2 extend (kite) and rest works"). `control` is the one call the
  owner has not reported on.
  *Built 2026-11-20 as `/8v8 call <assist|push|regroup|hold|engage|extend>`, not yet tried in a
  client.* *"Control that one" added 2026-11-21, not yet tried in a client:*
  `/8v8 call control` (or `mez`, `cc`) on the player's target: for twenty seconds a mez, stun,
  root or nearsight on that enemy scores 80 more (`ControlCalledByPlayer`), which puts it ahead
  of the group's own first choice without making a cast that would do nothing; the group's
  mezzer answers "Control on Finnarar (Healer)."
- [x] **M9.4** (M) `/8v8` play session: pick the enemy realm and group, enemy roams the arena,
  rematch after each fight. The enemy roster keeps its own realm points.
- [x] **M9.5** (S) Play sessions are recorded and analyzed like scrims.
  *Done 2026-11-19: about ten sessions recorded with the player in them; `pvp_replay.py` draws
  them with the player marked, and `pvp_referee.py` reads one as it reads a scrim.*
- [ ] **M9.6** (S) Check both clients: the original client and the new client's target and assist
  buttons.
  *Parked 2026-11-22, optional, not part of closing M9.* Neither install has the new client
  (it comes with the 0.37c download, about 60 GB while it installs), the owner plays 8v8 in the
  original client ("not sure how much I want to use it"), and nobody has looked at how the new
  client takes the `/8v8` commands, the setup window or the group-chat calls. The updater may
  refuse the scrim folder, which holds development builds; not tried.
  *The dead, 2026-11-22, the owner in the original client:* "I think the dead bot is now
  better." Not said: whether a dead bot can be clicked and raised.

**Exit check:** a full session from the launcher: ready a character, form the group, roam, fight,
rematch, with no GM commands. *Met, on the owner's word, 2026-11-22.*

## M10. Difficulty and tuning

*Parked here from M6 when it was closed (owner, 2026-11-19: target choice and crowd control are
good enough for where we stand). Not work in progress; to be picked up, if at all, in a tuning
round:*
- which heal a healer casts is still the old routine, not a scored choice;
- the fighters' choice of style, the same;
- the Minstrel's shouts and mezzes, the same (scoring them was tried and taken back);
- nobody budgets endurance;
- power: at rank 5 the Bard, the Theurgist and the Skald run under 10% in about 70% of their
  fights, a minute after the first blow (counted over about 100 fights a build, 2026-11-19);
- 21 of 236 mez casts in twelve matches finished on a target that showed mez immunity;
- the owner's reading that the bots know every timer too exactly ("mathematically good"): M10.1.
- **Midgard is the weak group, and why** (counted 2026-11-20, rank 5, after the resistances went
  in). Over 60 lane matches Midgard won 5 of 40 (1 of 20 against Albion, 4 of 20 against
  Hibernia; Hibernia 13 to 7 over Albion). Its members are mezzed or stunned 21% of the time
  they are alive (Albion 5%, Hibernia 12%). Ten mezzes a fight land on it against about five on
  the others, and since each member has one Purge the second ones stick: 46% of the mezzes on
  Midgard run their whole length (9% on Albion, 17% on Hibernia), a median of 15 seconds asleep
  against 1. In return its own mezzer, the Pacification Healer, finishes 2.8 to 3.8 mezzes a
  fight against 8.2 for the Sorcerer and 8.8 for the Bard: it is interrupted half the time and
  takes more damage than any other build (35 a second, a Cleric 15 to 18). Its group deals
  16,000 damage a fight against 20,000 and 23,000; the Runemaster 13 a second against the
  Cabalist's 32.
  *Tried and left as it was:* letting the Pacification Healer control until somebody is below
  50% health and not 75% (`HealsFirstBelow`). 24 matches, Midgard with either setting twelve
  times: it did not mez more (3.3 a fight against 3.8), healed 1,750 a fight against 3,190, and
  Midgard won 0 of 12 against 4 of 12. The mark is not what keeps it from controlling; being
  the enemy's first target is.
  For the owner, who played the Mending Healer: what kept a Pacification Healer alive and
  casting in a real group, and is this the group Midgard ran (two Healers, an Augmentation
  Shaman, a Skald, a Runemaster, a Savage, a Berserker, a Warrior)?

- *Parked from M7 and M8 when they were closed (2026-11-20):* stepping out of an area effect;
  side positions for positional styles; plans per opponent (M8.5); cooldown timing beyond Purge
  (M8.3); the amount of nearsight (above, "How exact the bots are").

- [ ] **M10.1** (M) Human-like limits as settings: reaction delay, attention, mistake rate.
  *First piece 2026-11-20: the end of an enemy's mez or stun immunity is noticed 4 to 20 s late
  (`PvpNotice`).*
  *Built 2026-11-22, **not yet run in a fight**:* reaction time (a bot answers a groupmate put
  to sleep, its own mez or stun, an enemy's cast begun and an enemy fighter on its healers and
  casters after a moment drawn between 400 and 1,200 ms), attention (15% of the time three
  times as long) and mistakes (every option weighed is off by up to 6 points). All in
  `PvpHumanWeights`; all 0 is the bots as they were.
- *The levels, as the owner met them (2026-11-22).* "I do not seem to find a difference between
  Casual and Sharp." Three of the owner's matches: the enemy's Purge came a median 3.6 s after
  a mez in one and 0.9 s in the two others, its cure 2.1 s and 0.5 s: the setting reached the
  bots (the recordings did not say which level each was; a header has `skillA`/`skillB` now).
  But a level moved four reactions by a second or two and nothing a player sees. Added on the
  owner's choice: **assist discipline** (a member goes for a newly called kill target after its
  own time: Sharp 0.1 to 0.35 s, Seasoned 0.4 to 1.2 s, Casual 2 to 4 s and one time in four
  8 s) and **healer attention** (a healer sees a member hurt its reaction time after that
  member went below 90%). `scrim-start SkillA= SkillB=` puts two levels against each other.
  **Twelve matches, a Casual side against a Sharp one, each realm on both sides, rank 5:**
  - the first heal on a member came a median 4.2 s after it went below 90% on the Casual
    side and 1.7 s on the Sharp side (snapshots are a second apart);
  - a side's members were on a newly called target a median 5.2 s (Casual) and 4.7 s (Sharp)
    after the call: most of that time is the way there and the cast, and the delay shows
    little. Not looked into: a member whose old target has died has no kill target meanwhile
    and may pick the same enemy by itself;
  - **Casual won 6 and Sharp 6; 50 deaths against 51.** The level decided nothing. The realm
    decided everything: Albion won its 8, Midgard lost its 8, Hibernia beat Midgard 4 of 4,
    most of them 8 dead to none or one.
  So the levels are a matter of how a group feels, not of how hard it is, and what makes a
  fight hard or easy today is which realm stands opposite. Midgard losing is not new (rank 5,
  2026-11-20: 2 of 18 against Albion, 6 of 24 against Hibernia). Albion against Hibernia was
  9 to 15 then and is 6 to 0 over today's two runs: six matches, after the realm abilities
  and at a different default level, so a sign and not a finding.
- [ ] **M10.2** (S) Named difficulty levels for the enemy group and, separately, for companions.
  *Built 2026-11-22, **not yet run in a fight**:* Casual, Seasoned (the weights as they are)
  and Sharp, one for the player's seven and one for every other group: `/8v8 skill`, and two
  lines of the setup window. A bot is given its level when it is made.
- [ ] **M10.3** (M) Regression gate: each new AI build plays a fixed scrim set against the
  previous one, and a drop blocks it.
  *Tool written 2026-11-22 (`source/tools/pvp_gate.py`):* the three main pairings, four
  matches each, judged by the referee's rules and compared with a baseline kept in the
  repository; a rule that passed and now fails makes it fail. Two builds cannot fight in one
  match, so it is the rules that are compared, not wins. **No baseline is stored yet.**
- *Also 2026-11-22:* "Could you remove all monsters from Emain Macha? useless for our tests."
  The monsters of the zone a session or a roam is held in are taken out of the world when it
  starts and once a minute while a roam runs (`PvpArena.ClearMonsters`; guards, merchants and
  whoever is of a realm or peaceful stay; nothing leaves the database). **Not seen in a run.**
- *Also 2026-11-22, **`/8v8 call rest`*** (also `sit`, `buff`): "to tell the bot to redo their
  self-timed buffs, druid resists, sit to regen endurance/mana. After a fight, I try to go hide
  somewhere to regain mana/endurance, but bot just stand up idle". The companions of the other
  game modes rest and keep their buffs up when their player stands still; an 8v8 bot is not
  one of those and did neither. While the call stands (two minutes; until the player walks
  1,500 units on, calls something else, or somebody gives or takes a blow) every bot of the
  group casts what it keeps up, one buff a turn, and then rests where it stands: a twentieth
  of its health, power and endurance a second (`Real.RestRegainsPercentPerSecond`), the Bard
  on its endurance song. A team of bots in a roam does the same by itself after a fight when
  a member is under 60%, for forty seconds at most, before it walks on; it used to meet its
  next enemy with what it had left. **Not seen in a run or a client.**
- *Also 2026-11-22:* "it would be nice a team decides to go hide somewhere off, or out of the
  route the teams make to meet each other and rest." Before it rests a team of bots walks off
  its road, straight to one side, 7,500 units or else 5,500 (a group sees another from 6,500),
  to a place that the navmesh has ground at and that it can walk to in no more than 1.8 times
  the straight line; with no such place it rests where it is. It gets up when an enemy team is
  within 5,000 (`PvpRoam.HidePlaces`, `Real.RestsOffTheRoadBy`). **Not seen in a run.**
- *Also 2026-11-22, **`/8v8 status`*** (also `roam-status`, `teams`): "it's hard to know if
  both opposite teams are at keep buffing, fighting or roaming". A line for each team of the
  roam: what it is doing (at its start buffing and when it leaves, roaming, fighting whom,
  resting, walking off to rest, raising its dead, waiting by a fight, wiped and when it stands
  up), how many of it are alive, and how far it is from the player. **Not seen in a client.**
- *Emain Macha as one long fight, and what was done about it (2026-11-22, three roams of
  bots on the M10 build, the owner away).* The first, seventeen minutes: one fight at the
  north end of the road from the third minute to the end, all six teams going through it, and
  no team ever out of an enemy's reach long enough to rest. How it formed, from the events: a
  fight of two teams; two teams waiting beside it met and fought each other; a bot of one
  fight, hit by an area spell of the other, took its attacker as a target, which made the
  teams "at each other"; and "joins after all" brought the rest in. Three changes:
  - A bot of a roam does not answer a hit from somebody of the roam that its group is not
    fighting (`PvpRoam.IgnoresHit`). The second roam: the fights stayed apart and the two
    Albion teams waited four minutes for theirs.
  - A team that is in a fight makes no draw on another one; and a team on its own does not
    start on a team that has its realm-mate beside it (`AvoidsTwo`: a Hibernian team had taken
    on both Midgard teams in the same second).
  - **Six points in place of two.** Every team walked the one road between its two ends. The
    navmesh has open ground west of it (three points 10,000 west are within 10% of the
    straight line from either end and from Dun Crauchon; a point 10,000 east is 47,000 on
    foot). The road's middle and three points on the west side are added, and a realm's two
    teams go round the six different ways. The third roam, nine minutes: two fights of eight
    against eight from the start, 8,000 units apart, the Albion teams each taking on a winner
    in turn, and the first rest (an Albion team at 2% walking off its road at 8:17).
  The monster clearing took 227 monsters out of Emain Macha at the first start. `/8v8 status`
  as the control channel prints it read as meant. **None of this seen with a player.**
- *A roam of bots on the merged build, ten minutes (2026-11-22):* out of a fight the Bards
  and Skalds were back on their speed song (the squad handed over between fights); the mop-up
  began four times, and the last one or two of a beaten group still lived a median of 16
  seconds, mezzed or stunned 44% of it, 185 seconds once. The mop-up stops new mezzes, not one
  that is already on; that longest case is to be looked at.
- [ ] **M10.4** (M) Tuning rounds with the owner: 20 fights, review the analyzer's failure list,
  fix, repeat.
- [x] **M10.5** (S) Player guide for the 8v8 mode and an update of the developer docs.
  *Player guide written 2026-11-21: [PVP-8V8-GUIDE.md](PVP-8V8-GUIDE.md)* (the three ways to
  play, the roam's rules, the calls, what has and has not been tried in a client). The owner
  had twice taken one mode for another ("I misunderstood how to use the /8v8 practice vs real";
  "There should be 6 teams total?"). *Developer docs the same day:* an 8v8 section in
  [DEVELOPMENT.md](DEVELOPMENT.md) (where the code is, file by file; the scrim server and its
  commands; the tools; the rules of the work). Both are to be kept up as the mode changes.
  Also `pvp_roam.py --html`: a roam as a page for the browser, the zone from above with every
  member a dot, to play and scrub, the events and fights to jump to, a view that follows one
  team.

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
- **How a fight starts (owner, 2026-11-19):** the scrims' opening "is not what a real 8v8 fight
  would look like". Today the groups stand 6,000 units apart, buff, and run straight at each
  other; the side whose mez reaches farther holds and the other runs into it. What the owner
  described, to build from:
  - Groups roamed Emain Macha on known routes (in the Old Frontier: Albion's main keep, the
    Albion gate, the Midgard gate, Midgard's main keep, and back), with a few routes that
    left that line; "it was mainly a game of cat and mouse".
  - Contact "really depends on if we sneaked in, head-on, angle on contact". Players pan the
    camera so as not to be jumped, so the angle matters. "Mostly the team getting jumped would
    spread in the opposite direction and try to react while the aggressor team would try to
    push and pick a target."
  - "There's no black and white answers. It's a game of reaction, crowd controlling and the
    right timing to collapse on a target." The group that is jumped may use an instant area
    mez to buy time or to break up the aggressor, or Speed of Sound to take its places or to
    run through the enemy. "Sometimes it works, sometimes it doesn't... Sometimes you will lose
    because your realm abilities are down and you just have to accept it."
  So: no single scripted answer. The opening is to be varied (who sees whom first and from
  which side), each answer is one more scored choice, and realm abilities are sometimes down
  at the start of a match. Not built yet.
- **Realm rank for testing (owner, 2026-11-19):** "testing with no realm abilities might be
  silly." Scrims for watching now run at realm rank 5 (40 points): everyone has Purge, the
  Druids Group Purge, Blademasters, Berserkers and the Savage Charge, and the passives.
  Missing at any rank, because bots cannot use them: Speed of Sound, Mastery of
  Concentration, quick cast. The night's referee figures were all at rank 1.
- **Speed of Sound (owner, several times):** the Bard, Skald and Minstrel's realm ability; a
  group that is jumped uses it to reposition or to run through, and a group that is kited
  uses it to get in. No bot has it.
- **Quick cast (owner, 2026-11-19):** used by the Eldritch to get its point-blank spell off
  on enemy pets while they hit it. Bots have none: it is switched off for every kind of bot
  (`GameBot`, "Quickcast is explicitly disabled for all bot kinds"), and the ability's handler
  takes a player. To be built for 8v8 bots only.
- **Races (owner, 2026-11-18, "for later"):** the race of each class needs reworking. Today every
  class has one fixed race (`PvpBuildCatalog.RaceOf`): Hibernia's casters are Elves and Albion's
  are Avalonians. The owner: a Hibernian caster should be a Lurikeen, and an Albion caster a
  Saracen, not an Avalonian. To check against the server's own table of which race may be which
  class before changing anything: from the assistant's memory of 1.65, unverified, a Saracen can
  be a Sorcerer or Cabalist but not a Theurgist or Wizard. The rest of the table is to be gone
  through with the owner class by class; nothing is changed yet.

# 8v8 fight analysis: 150 matches, 2026-11-18

What happened in the first batch read with the learning tool, why Albion won almost all of it,
and what that suggests changing. Part 2 is the reading; the full tables are at the end.

## 1. What was fought

| | |
|---|---|
| Batch | `20261118-203508`, 150 matches, 50 for each pairing, in six lanes of the Emain arena |
| Groups | Albion: 2 Clerics, Sorcerer, Minstrel, Theurgist, Paladin, Armsman, Cabalist. Midgard: Pacification Healer, Mending Healer, Shaman, Skald, Runemaster, Savage, Berserker, Warrior. Hibernia: 2 Druids, Bard, Hero, 2 Blademasters, Eldritch, Enchanter |
| Rules | Level 50, realm rank 1 (no realm abilities), buffbot buffs, built-in weights, both sides on the squad brain |
| Build | Commit `06f4753`: everything built up to the afternoon of 2026-11-18, before the two fixes described in part 5 |
| Run | 91 minutes, no server error, no bot left behind; every match ended with one side dead |

To make this report again for another batch:
`python source/tools/pvp_report.py <recordings folder> --batch <name> --out report.md`.

## 2. The short version

1. **Albion won 95 of its 100 fights.** It beat Hibernia 50 to 0 and Midgard 45 to 5. Midgard
   beat Hibernia 29 to 21. The day before, with an older build, Midgard beat Albion 7 to 3.
2. **The opening mez does not decide the fight.** Both groups land their first mez within four
   seconds of contact, and ten seconds in, the side with fewer members asleep wins only 58% of
   the time. Thirty seconds in, it wins 91%. The fight is decided between the tenth and the
   thirtieth second.
3. **What happens in those twenty seconds is waking up.** The winner goes from 1.3 members asleep
   to 0.4; the loser from 1.9 to 3.0. Albion's healers are cured 93% of the times they are
   mezzed and sleep six seconds. Hibernia's are cured 2% of the time and Midgard's 16%, and
   they sleep 22 seconds.
4. **The first death settles it.** The side that loses the first member loses the fight 93% of
   the time. In 71% of fights that member died while its own healers were asleep or stunned.
5. **The three groups do not hunt the same kind of target.** Against Albion, Hibernia's kill
   target in the first 45 seconds is a Cleric 100% of the time and Midgard's 99%. Albion's is
   the enemy's Bard, Pacification Healer or Shaman 59 to 82% of the time. The Clerics stand at
   the back, heal each other and are guarded; Albion lost a member first in 10 fights of 100.
   The Bard and the Pacification Healer stand in the middle of the field and die first.
6. **The one who could wake the healers is the one being killed.** The Bard is the enemy's kill
   target for 27 of the first 40 seconds and takes 25 hits in them. It finishes one cure mez
   every ten matches. While a Druid sleeps, the Bard is dead 52% of the time.
7. **Hibernia hardly controls anyone.** Albion keeps enemy members asleep for 206 member-seconds
   a match, Midgard 108, Hibernia 33.
8. **Two fixes aimed at where the casters stand were tried the same day, and neither changed a
   result.** The second did move them back (37% of the way to the enemy against 50%) and still
   won 23 fights to 25. Standing farther back does not help while the enemy's train is sent at
   the same member either way, which points back to item 5.
9. **A third fix, aimed at the kill target, helped.** With distance and being out of position
   weighing twice as much, Midgard's and Hibernia's trains went for Albion's casters in place
   of its Clerics, and the new score won 28 fights of 48 against the old one. It is now the
   default. Albion still wins most fights; this is a first step, not the end of it.

## 3. How a fight goes

Times are from first contact. Medians over the 150 matches.

| Moment | What happens |
|---|---|
| Before contact | The groups walk 7 to 19 seconds. Albion holds at its mez range for a few seconds in most matches (6% of its fight is under the Hold order) |
| 0 to 4 s | The first mez lands on each side, about at the same time |
| 10 s | 1.3 of the eventual winner's members are asleep, 1.9 of the loser's. Nobody has died. The side with fewer asleep wins 58% of these fights: close to a coin toss |
| 20 s | Winner 0.8 asleep, loser 2.6. The loser's healers are free to heal 41% of the time, the winner's 69% |
| 23 s | The turning point: from here the eventual winner stays ahead |
| 30 s | Winner 0.4 asleep, loser 3.0. Loser's healers free 24%. The side with fewer asleep wins 91% |
| 36 s | The first death, with four enemies on the victim |
| 60 s | Winner 7.9 alive, loser 5.7 |
| 110 to 144 s | The match ends (walk included). The winner has all eight alive in the median fight, and six or more in 86 to 100% of fights: once decided, a fight is one-sided |

How many are asleep ten seconds in, by pairing (from `pvp_analyze.py`):

| Pairing | First group asleep | Second group asleep | Who won |
|---|---|---|---|
| Albion against Hibernia | 1.1 | 1.9 | Albion 50 to 0 |
| Albion against Midgard | 2.1 | 1.0 | Albion 45 to 5 |
| Midgard against Hibernia | 0.8 | 2.5 | Midgard 29 to 21 |

Midgard wins the opening against Albion, with half as many asleep at ten seconds, and still
loses nine fights in ten. That is the clearest sign that the opening is not where these fights
are lost.

## 4. Findings

Each finding says what the numbers show, then how I read them. Where the reading is a guess, it
says so.

### 4.1 The fight is decided between the tenth and the thirtieth second

| At | The side with fewer asleep wins | The side with more alive wins |
|---|---|---|
| 5 s | 54% | nobody has died |
| 10 s | 58% | nobody has died |
| 20 s | 75% | 92% (12 fights only) |
| 30 s | 91% | 89% (44 fights) |
| 45 s | 90% | 97% |
| 60 s | 92% | 99% |

The three readings you named, for the eventual winner and loser:

| At | Side | Asleep | Healers free to heal | Healers and casters able to cast | Fighters held |
|---|---|---|---|---|---|
| 10 s | winner | 1.3 | 77% | 62% | 48% |
| 10 s | loser | 1.9 | 65% | 55% | 49% |
| 20 s | winner | 0.8 | 69% | 63% | 30% |
| 20 s | loser | 2.6 | 41% | 33% | 51% |
| 30 s | winner | 0.4 | 68% | 67% | 18% |
| 30 s | loser | 3.0 | 24% | 22% | 48% |

*Reading.* At ten seconds the two sides look alike on all three readings. By twenty they have
split on all three at once, and "healers free to heal" splits the widest. Your three readings do
tell the result, from about the twentieth second.

### 4.2 The difference is who gets woken

| Group | Who slept | Mezzes sat through | Median length | Cured by a groupmate | Woken by an enemy's hit | Ran its course |
|---|---|---|---|---|---|---|
| Albion | healers | 0.3 | 6 s | 93% | 4% | 4% |
| Albion | casters | 1.6 | 6 s | 79% | 2% | 19% |
| Albion | fighters | 1.9 | 16 s | 57% | 2% | 41% |
| Hibernia | healers | 1.3 | 22 s | 2% | 30% | 68% |
| Hibernia | casters | 2.1 | 23 s | 2% | 14% | 84% |
| Hibernia | fighters | 3.4 | 22 s | 1% | 16% | 84% |
| Midgard | healers | 0.9 | 22 s | 16% | 53% | 30% |
| Midgard | casters | 1.1 | 26 s | 20% | 25% | 55% |
| Midgard | fighters | 3.7 | 20 s | 32% | 9% | 58% |

Cure mez finished, per match: Albion's Minstrel 2.2 and Sorcerer 0.6; Midgard's Mending Healer
1.5 and Pacification Healer 0.5; Hibernia's Bard 0.1.

*Reading.* Albion undoes a mez in six seconds; the others sit theirs out for twenty-two. Every
class's cure mez is the same spell on this server: 1,500 range, a three-second cast. What
differs is whether the curer is free to cast it for three seconds. Albion's Minstrel is almost
never the enemy's target (4 hits in the first 40 seconds). The Bard is (see 4.5).

### 4.3 The first death settles the fight, and it comes while the healers sleep

The side that lost the first member lost 93% of the 150 fights. Why that member died, looking
at its own healers in its last ten seconds:

| Why | Fights | Share |
|---|---|---|
| Its healers were asleep or stunned | 106 | 71% |
| Its healers were kept from casting | 18 | 12% |
| It was healed and died all the same | 8 | 5% |
| Its healers were free and no heal came | 7 | 5% |
| It was healed and out-damaged | 7 | 5% |
| It was out of its healers' reach | 4 | 3% |

| Group | Lost the first member in | Who it was | When |
|---|---|---|---|
| Albion | 10 of 100 fights | Enhancement Cleric 9, Minstrel 1 | 27 s |
| Hibernia | 81 of 100 | Bard 60%, Regrowth Druid 22%, Nurture Druid 11% | 35 s |
| Midgard | 59 of 100 | Pacification Healer 51%, Shaman 22%, Runemaster 12% | 40 s |

*Reading.* The healing routine is not what fails: "free and no heal came" is 5%. Healers that
are awake keep their group alive; a group dies when its healers are asleep. This matches what
you said about healers being the centre of the fight.

### 4.4 The groups do not hunt the same kind of target

The group's kill target during the first 45 seconds of the fight:

| Group, against | A healer that only heals | Bard, Pacification Healer or Shaman | A caster | A fighter |
|---|---|---|---|---|
| Albion, against Hibernia | 35% | 59% | 5% | 1% |
| Albion, against Midgard (the 45 it won) | 12% | 82% | 3% | 3% |
| Hibernia, against Albion | 100% | Albion has none | 0% | 0% |
| Midgard, against Albion (the 45 it lost) | 99% | Albion has none | 1% | 0% |
| Midgard, against Albion (the 5 it won) | 78% | Albion has none | 14% | 9% |
| Midgard, against Hibernia | 35 to 36% | 58 to 65% | 0 to 6% | 0 to 1% |
| Hibernia, against Midgard | 2 to 11% | 81 to 98% | 0 to 3% | 0 to 5% |

Counting both of its opponents, Hibernia's target is Albion's Enhancement Cleric for 46% of
all its fighting time and Midgard's for 48%. They reach their targets (their fighters are a
median 700 to 760 units away) and seldom kill them: in a sample of 50 fights each, against
both opponents, Hibernia killed anybody in 18 and Midgard in 21, where Albion did in 48.

*Reading.* This is my best candidate for the main cause, and it comes from how the kill target
is scored, not from the classes. A healer is worth 200 points, a caster 150, a fighter 50, and
distance costs little. So every group calls a healer. Against Albion that is a Cleric: in
chain, at the back since this afternoon's change (healers hang back on incoming), guarded by
the Paladin, and healed by the second Cleric, who is free 75% of the fight. Albion calls a
"healer" too, but the ones it finds are the Bard, the Pacification Healer and the Shaman, who
wear less, stand in the middle of the field and whose own healers are asleep.

Nothing in the score asks "can we actually kill this one?". The five fights Midgard won against
Albion are the ones where its target was more often a caster or a fighter. Five is far too few
to prove it, but it points the same way.

Albion's Sorcerer stands 56% of the way to the enemy twenty seconds in, in cloth, and was the
enemy's kill target for none of the first 40 seconds in the sampled matches. You wrote that the
kill target is "whoever is easiest to kill and out of position", and that "a caster or healer
that came too far forward is jumped". The bots do not do that yet.

### 4.5 The one who could wake the healers is the one being killed

| | Bard | Pacification Healer | Albion's Minstrel | Albion's Sorcerer |
|---|---|---|---|---|
| The enemy's kill target, of the first 40 s | 27 s | 32 s | 0 s | 0 s |
| Attacks landing on it in the first 40 s | 25 | 18 | 4 | 3 |
| Dies in | 88% of matches | 71% | 6% | 7% |
| Median death | 43 s | 55 s | 122 s | 53 s |
| First of its group to die | 49% of fights | 30% | 1% | 0% |
| Cure mez finished a match | 0.1 | 0.5 | 2.2 | 0.6 |

While one of the group's healers is asleep, what its curer is doing:

| Curer | A healer is asleep for | Dead | Asleep or stunned | Being hit, cannot cast | Casting | Walking or idle |
|---|---|---|---|---|---|---|
| Hibernia's Bard | 28 s a match | 52% | 22% | 19% | 4% | 2% |
| Midgard's Pacification Healer | 17 s | 42% | 31% | 22% | 2% | 3% |
| Albion's Minstrel | 3 s | 0% | 61% | 15% | 16% | 8% |
| Albion's Sorcerer | 3 s | 3% | 30% | 19% | 20% | 28% |

In the Bard's recorded decisions while a groupmate was asleep (536 of them in 25 matches), cure
mez was blocked by "interrupted" 197 times and was among its five best options 5 times.

*Reading.* The Bard is not choosing badly; it is being hit, so a three-second cast cannot
start, and then it is dead. Two things put it there: it is the enemy's first choice of target
(4.4), and it stands forward (4.6).

### 4.6 Casters and control healers stand in the middle of the field

Where each member stands along the line from its own group's middle at contact (0) to the
enemy's (100). The two middles are about 1,700 units apart; 50 is the middle of the field.

| Member | 5 s | 10 s | 20 s |
|---|---|---|---|
| Fighters on the kill target (Armsman, Blademaster, Berserker, Savage) | 49 to 70 | 60 to 71 | 63 to 70 |
| Skald | 69 | 84 | 85 |
| Albion's Sorcerer | 34 | 47 | 56 |
| Albion's Theurgist | 23 | 44 | 55 |
| Hibernia's Enchanter | 41 | 49 | 56 |
| Hibernia's Eldritch | 5 | 33 | 46 |
| Albion's Cabalist | 4 | 23 | 43 |
| Hibernia's Bard | 17 | 27 | 41 |
| Midgard's Runemaster | -3 | 21 | 41 |
| Midgard's Pacification Healer | 1 | 10 | 23 |
| Midgard's Shaman | -7 | 6 | 22 |
| Shield fighters (Paladin, Hero, Warrior) | 14 to 37 | 17 to 39 | 26 to 40 |
| Healers that only heal (Clerics, Druids, Mending Healer) | -35 to -28 | -28 to -15 | -13 to 7 |

*Reading.* Every caster's spells reach 1,500 units or more, so none of them needs to leave its
own side of the field to cast on enemies who are coming to it. They end up beside the fighters.
You described the opposite: "casters and healers stand near the edge of their range and away
from enemy fighters". The healers that only heal do stay back, since this afternoon.

I also checked the "stand off" steps built earlier this week: 40% of them go toward the enemy's
middle and 34% away. They shuffle; they do not gain distance.

### 4.7 Hibernia hardly controls anyone

Seconds a match that enemy members spent in each state, added over the members:

| Done by | Healers asleep | Casters asleep | Fighters asleep | All asleep | Fighters rooted or slowed | Casters nearsighted |
|---|---|---|---|---|---|---|
| Albion | 37 | 63 | 106 | 206 | 33 | 66 |
| Midgard | 14 | 41 | 53 | 108 | 23 | 163 |
| Hibernia | 4 | 6 | 23 | 33 | 30 | 150 |

Mezzes finished a match: Albion's Sorcerer 4.7 and Minstrel 4.9; Midgard's Pacification Healer
2.2, Skald 2.3 and Mending Healer 1.2; Hibernia's Bard 2.0. The Bard casts its instant amnesia
7.2 times a match and records "nothing worth casting" 11.4 times.

*Reading.* Hibernia has one mezzer, and it is dead by the 43rd second in nine matches of ten.
Before that it is being hit and cannot start its cast-time mezzes, for the same reason it
cannot cure. So this finding is largely the same one as 4.5.

Hibernia and Midgard nearsight twice as much as Albion. It does not win them the fight: a
nearsighted Cleric still heals a group that stands close to it.

### 4.8 Albion's train is together; the others' is not

| Group | Kill-target calls | Its hits that landed on the called target | Its damage on the called target |
|---|---|---|---|
| Albion | 9.6 | 79% | 87% |
| Midgard | 4.7 | 57% | 59% |
| Hibernia | 3.6 | 45% | 54% |

*Reading.* Part of this is by design: a Skald interrupting and a shield fighter peeling do not
hit the kill target. Part is the result of losing: fighters who are asleep 16 to 23% of the
fight and are being hit on the way do not arrive together. And part is 4.4: a target at the
back of the enemy group is reached by one fighter at a time.

### 4.9 Pets matter, but they are not the main thing

Albion's pets began attacking this afternoon (they had never attacked before; part 5).

| | |
|---|---|
| Share of Albion's damage done by pets | 26% (the Cabalist's pet 17% with 66 hits a match, the Theurgist's 9% with 33) |
| Attacks landing on Hibernia's healers a match | 58, of which 24 by pets |
| Attacks landing on Midgard's healers a match | 61, of which 27 by pets |
| Attacks landing on Albion's healers a match | 32, of which 2 by pets |
| Healers "being hit, cannot cast" | Albion 17% of the fight, Hibernia 14%, Midgard 20% |

*Reading.* Pets put nearly half of the hits on the other groups' healers, yet those healers
are kept from casting no more of the time than Albion's are. What the others' healers lose
their time to is sleep and death: Hibernia's are free 31% of the fight, asleep or stunned 22%,
dead 34%. So pets add pressure, but removing them would not close the gap. The bots do not
react to enemy pets in any way: no one attacks, mezzes or roots one.

### 4.10 Smaller things the tables show

- **Druids lose six casts a match to the game** ("refused by the game": the target went out of
  range or sight, died, or the caster moved), against under two for the other healers. They
  begin 28 casts and finish 17. Not looked into yet.
- **The Hero is under a fifth of its endurance 24% of the fight** and the Blademasters 11%,
  after today's change that keeps the Bard's endurance song up (it was 58 to 65% and 42 to 65%).
  Albion's and Midgard's fighters are at 0 to 7%.
- **Orders follow the result; they do not make it.** A winning group spends 56 to 78% of the
  fight under Push; a losing one splits between Extend and Engage. Regroup is 0 to 3%. (Hold
  comes before contact and is not in that table.)
- **Fighters land few damaging hits:** 16 to 29 a match each, in fights of about two minutes.
  They are asleep, rooted, walking between targets, or blocked.
- **Healing wasted.** A healer's heals of 100 and more restore 33 to 62% of what they could.
  Most of the rest is by design (an instant group heal reaches members who are not hurt).

## 5. What was tried the same day

| Change | How it was measured | Result |
|---|---|---|
| Caster pets attack (the Cabalist's and Enchanter's never had: with no player near, a pet's brain was not started) | Six matches before and after | Works: 66 hits a match from the Cabalist's pet. Probably part of Albion's swing, since Albion has two pet classes |
| Healers that only heal hang back on incoming | Six matches | Works: they stand at -35 to -28 five seconds in. Probably also part of Albion's swing: its Clerics became harder to reach and are still every enemy's kill target |
| A walk that would end beside free enemy fighters costs score | 48 matches, the change on one team and the old behaviour on the other | No effect: 24 wins each; casters stood 43% forward with it, 39% without |
| A caster or control healer with nothing worth casting stands where it is (the old brain walked it toward its attack target) | 48 matches, same method | It moves them and changes nothing: casters stood 37% forward with it against 50% without, and it won 23 fights to 25. The curer was the first to die 12 times on each side |
| The kill target weighs distance and being out of position twice as much (for each hundred units, -5 and +5 in place of -2.5 and +2) | 48 matches, same method | Helps: 28 wins to 20. Against Albion, Midgard's target was a caster 75% of the time (a Cleric 99% with the old score) and it drew first blood in 3 fights of 8 (1 of 8 with the old score); Hibernia's was a caster 54% of the time, first blood in 2 of 8 (0 of 8). By group: Albion won 16 of 16 with it and 12 of 16 without, Midgard 6 and 3, Hibernia 6 and 5. Now the default |

A difference of 28 to 20 in 48 matches can still be chance (about one time in seven). I kept it
because all three groups moved the same way and the targets changed as intended; the night's
batch varies these two weights again and will confirm it or not.

## 6. What I would change, in order

These are proposals. Each is a soft weight in the scored choice, measured new against old
inside the same matches before it is kept.

1. **The kill target: ask whether it can be killed.** The first half is done (part 5): distance
   and being out of position weigh more against class, so a Sorcerer in cloth in the middle of
   the field outranks a Cleric at the back. Still to add, one new consideration: a target whose health has not dropped after several
   seconds of the train being on it is losing value, so the group moves on instead of spending
   the fight on a healer that is being healed.
2. **Shut the healer that is not being killed.** Albion's Clerics are asleep 1% of the fight.
   A group that goes for one healer has to mez, stun or interrupt the other; your playbook
   already says enemies that are not being killed are kept mezzed, rooted or interrupted.
   Hibernia and Midgard leave an enemy healer free to cast for about 150 seconds a match.
3. **Keep the curer alive and free.** The Bard and the Pacification Healer are the only cure
   for their groups' mez. Whether they stay back like the healers, are guarded first, or keep
   going in is your call (you wrote that a Bard should "probably not" always rush in).
4. **Casters hold their range.** Part 5's second fix does this in part (37% forward against
   50%) without changing a result, so it is not urgent. The stand-off steps should gain
   distance from the enemy group, not shuffle.
5. **Enemy pets.** Nothing reacts to them today.

What I need from you:

- When a Cabalist's pet or Theurgist pets were on you as a healer, what did your group do:
  kill them, mez or root them, or ignore them and go for the owner?
- Should the Bard and the Pacification Healer hang back with the healers until the opening
  mezzes are spent, or go in?
- Is Albion at 95% a problem to fix first, or do you want the groups improved evenly and the
  balance looked at afterwards? At realm rank 1 nobody has Purge, which is the usual answer to
  the Sorcerer's range.

## 7. What this analysis cannot tell

- **It describes; it rarely proves.** Only the two fixes in part 5 were tested by putting the
  change on one team and not the other. Everything else is what happened in fights that all
  used the same build; the causes are my reading.
- **One build, one day.** Three changes went in on the day (pets, healers hanging back, the
  Bard's songs). I cannot say from this batch how much of Albion's swing each one caused.
- **Bots against bots.** Both sides share the same blind spots: nobody reacts to pets, and
  everybody calls a healer. A human group would punish different things.
- **Lanes.** The groups meet head-on over 6,000 units of open ground. Nothing here says how
  they would do with hills, cover or an approach from the side.
- **Realm rank 1.** No Purge, no Group Purge, no realm abilities at all.
- **Fifty matches a pairing** is enough for the large effects reported here. Differences of a
  few points between rows are within chance.

## 8. The tables

Every figure is per match unless it says otherwise; "s" is seconds, counted from first contact.

### 8.1 Results

| Pairing | Matches | Wins | Median length | Walk to contact | Winner's survivors (median) | Won with 6 or more alive |
|---|---|---|---|---|---|---|
| alb-main against hib-main | 50 | alb-main 50 | 142 s | 19 s | 8 of 8 | 96% |
| alb-main against mid-main | 50 | alb-main 45, mid-main 5 | 144 s | 12 s | 8 of 8 | 100% |
| mid-main against hib-main | 50 | mid-main 29, hib-main 21 | 110 s | 7 s | 8 of 8 | 86% |

A winner with six or more alive means the fight was one-sided; few such fights means they were close.

### 8.2 The course of a fight

| Pairing | First mez lands on the first / second group | Turning point | First death | Match length (with the walk in) |
|---|---|---|---|---|
| alb-main against hib-main | 1 s / 4 s | 20 s | 36 s | 142 s |
| alb-main against mid-main | 1 s / 2 s | 22 s | 41 s | 144 s |
| mid-main against hib-main | 0 s / 1 s | 31 s | 32 s | 110 s |

The turning point is the first second from which the eventual winner stays ahead to the end, by numbers alive and the three readings.

### 8.3 When is the fight decided?

For each moment: in how many fights the side that was ahead on that measure went on to win.

| Measure | At | Fights where one side was ahead | The side ahead won | Fights level on it |
|---|---|---|---|---|
| Fewer members asleep | 5 s | 119 | 54% | 30 |
| Fewer members asleep | 10 s | 133 | 58% | 16 |
| Fewer members asleep | 20 s | 130 | 75% | 19 |
| Fewer members asleep | 30 s | 130 | 91% | 20 |
| Fewer members asleep | 45 s | 144 | 90% | 6 |
| Fewer members asleep | 60 s | 119 | 92% | 31 |
| More members alive | 5 s | 0 | - | 149 |
| More members alive | 10 s | 0 | - | 149 |
| More members alive | 20 s | 12 | 92% | 137 |
| More members alive | 30 s | 44 | 89% | 106 |
| More members alive | 45 s | 104 | 97% | 46 |
| More members alive | 60 s | 134 | 99% | 16 |

The side that lost the first member lost the fight in 93% of 150 fights.

| At | Side | Alive | Asleep | Healers free to heal | Healers and casters able to cast | Fighters held |
|---|---|---|---|---|---|---|
| 10 s | winner | 8.0 | 1.3 | 77% | 62% | 48% |
| 10 s | loser | 8.0 | 1.9 | 65% | 55% | 49% |
| 20 s | winner | 8.0 | 0.8 | 69% | 63% | 30% |
| 20 s | loser | 7.9 | 2.6 | 41% | 33% | 51% |
| 30 s | winner | 8.0 | 0.4 | 68% | 67% | 18% |
| 30 s | loser | 7.7 | 3.0 | 24% | 22% | 48% |
| 60 s | winner | 7.9 | 0.2 | 70% | 74% | 14% |
| 60 s | loser | 5.7 | 1.4 | 12% | 18% | 40% |

The owner's three readings, for the eventual winner and loser. The earlier a row pair differs, the earlier that reading tells the result.

### 8.4 The first death

| Why the first member died | Fights | Share |
|---|---|---|
| its healers were asleep or stunned | 106 | 71% |
| its healers were kept from casting | 18 | 12% |
| it was healed and died all the same | 8 | 5% |
| its healers were free and no heal came | 7 | 5% |
| it was healed and out-damaged | 7 | 5% |
| it was out of its healers' reach | 4 | 3% |

It died 36 s after contact (median) with 4 enemies on it; 57% were themselves asleep or stunned for a third or more of their last ten seconds. "Why" looks at the victim's own healers in its last ten seconds.

| Group | Lost the first member in | Who it was | Why | When |
|---|---|---|---|---|
| alb-main | 10 of 100 | cleric-enhance 90%, minstrel 10% | its healers were asleep or stunned 70%, its healers were kept from casting 30% | 27 s |
| hib-main | 81 of 100 | bard 60%, druid-regrowth 22%, druid-nurture 11%, enchanter-light 4% | its healers were asleep or stunned 73%, its healers were kept from casting 12%, it was healed and died all the same 6% | 35 s |
| mid-main | 59 of 100 | healer-pac 51%, shaman-aug 22%, runemaster-rune 12%, healer-mend 8% | its healers were asleep or stunned 68%, its healers were kept from casting 8%, it was healed and out-damaged 8% | 40 s |

### 8.5 What the healers were doing

| Healers (Cleric, Druid, Healer) | free | dead | being hit, cannot cast | asleep | stunned |
|---|---|---|---|---|---|
| alb-main | 75% | 5% | 17% | 1% | 2% |
| hib-main | 31% | 34% | 14% | 14% | 8% |
| mid-main | 32% | 33% | 20% | 8% | 6% |

| Casters and support (Bard, Shaman included) | free | dead | being hit, cannot cast | asleep | stunned |
|---|---|---|---|---|---|
| alb-main | 74% | 3% | 17% | 4% | 1% |
| hib-main | 22% | 39% | 14% | 18% | 6% |
| mid-main | 33% | 30% | 19% | 12% | 6% |

| Fighters | free | asleep | dead | stunned | rooted or slowed |
|---|---|---|---|---|---|
| alb-main | 81% | 7% | 1% | 5% | 6% |
| hib-main | 53% | 23% | 12% | 7% | 6% |
| mid-main | 58% | 16% | 14% | 6% | 6% |

Share of the fight from contact to the end, the dead included. "Being hit" means an attack landed in the last moments, so no cast can start.

| Group | Healers' casts begun | Finished | Stopped by an attack | Refused by the game | Dropped by the caster |
|---|---|---|---|---|---|
| alb-main | 30 | 25 | 2.2 | 1.9 | 0.6 |
| hib-main | 28 | 17 | 2.2 | 6.0 | 1.6 |
| mid-main | 19 | 14 | 2.0 | 1.6 | 0.7 |

"Refused by the game" is a cast the game ended itself: the target went out of range or sight, died, or the caster moved.

| Attacks landing on its healers | All | Enemy pets | Fighters, melee | Fighters, shouts | Casters' spells | Casters, melee |
|---|---|---|---|---|---|---|
| alb-main | 32 | 2 | 16 | 7 | 7 | 0 |
| hib-main | 58 | 24 | 16 | 9 | 9 | 0 |
| mid-main | 61 | 27 | 13 | 8 | 13 | 0 |

| Group | Build | Heals of 100 and more landing | Health restored | Of what the heals could give |
|---|---|---|---|---|
| alb-main | cabalist-tri | 8.9 | 242 | 12% |
| alb-main | cleric-enhance | 7.5 | 2568 | 62% |
| alb-main | cleric-rejuv | 9.1 | 3463 | 43% |
| alb-main | sorcerer-mind | 3.3 | 37 | 5% |
| hib-main | druid-nurture | 10.0 | 2404 | 35% |
| hib-main | druid-regrowth | 9.6 | 2960 | 33% |
| mid-main | healer-mend | 7.8 | 2655 | 46% |
| mid-main | healer-pac | 6.3 | 1851 | 52% |
| mid-main | shaman-aug | 4.8 | 603 | 85% |

Per bot and match. The last column is low partly by design: an instant group heal reaches members who are not hurt, and heals over time tick on a full member.

### 8.6 Sleep, and who ends it

| Group | Who slept | Mezzes sat through | In the first 15 s | Median length | Seconds asleep | Cured | Woken by a hit | Ran out | Died asleep |
|---|---|---|---|---|---|---|---|---|---|
| alb-main | healers | 0.3 | 0.0 | 6 s | 3 s | 93% | 4% | 4% | 0% |
| alb-main | casters | 1.6 | 0.7 | 6 s | 17 s | 79% | 2% | 19% | 0% |
| alb-main | fighters | 1.9 | 1.5 | 16 s | 28 s | 57% | 2% | 41% | 0% |
| hib-main | healers | 1.3 | 0.1 | 22 s | 33 s | 2% | 30% | 68% | 0% |
| hib-main | casters | 2.1 | 1.0 | 23 s | 65 s | 2% | 14% | 84% | 0% |
| hib-main | fighters | 3.4 | 1.7 | 22 s | 82 s | 1% | 16% | 84% | 0% |
| mid-main | healers | 0.9 | 0.2 | 22 s | 21 s | 16% | 53% | 30% | 1% |
| mid-main | casters | 1.1 | 0.2 | 26 s | 29 s | 20% | 25% | 55% | 1% |
| mid-main | fighters | 3.7 | 1.9 | 20 s | 76 s | 32% | 9% | 58% | 1% |

| Group | Build | Cure mez begun | Cure mez finished |
|---|---|---|---|
| alb-main | minstrel | 2.7 | 2.2 |
| alb-main | sorcerer-mind | 0.7 | 0.6 |
| hib-main | bard | 0.1 | 0.1 |
| mid-main | healer-mend | 1.9 | 1.5 |
| mid-main | healer-pac | 0.8 | 0.5 |

Per bot and match. An instant cure shows as finished without having begun.

| Group | Who could wake it | A healer is asleep for | Dead | Asleep | Stunned | Being hit | Casting | Walking | Idle |
|---|---|---|---|---|---|---|---|---|---|
| alb-main | minstrel | 3 s | 0% | 52% | 9% | 15% | 16% | 6% | 2% |
| alb-main | sorcerer-mind | 3 s | 3% | 24% | 6% | 19% | 20% | 21% | 7% |
| hib-main | bard | 28 s | 52% | 12% | 10% | 19% | 4% | 1% | 1% |
| mid-main | healer-pac | 17 s | 42% | 13% | 18% | 22% | 2% | 3% | 0% |

While one of the group's healers that only heal is asleep: what each groupmate with a cure mez is doing during those seconds.

### 8.7 Each member: where it stands, when it dies

| alb-main | Dies in | Median death | First to die (of all its fights) | Forward at 5 / 10 / 20 s | Damage (share of the group's) | Damaging hits | Under 20% endurance |
|---|---|---|---|---|---|---|---|
| armsman-polearm | 8% | 100 s | 0% | 70 / 71 / 69 | 4351 (23%) | 18 | 2% |
| cabalist-tri | 5% | 72 s | 0% | 4 / 23 / 43 | 3306 (18%) | 12 | - |
| cleric-enhance | 11% | 28 s | 9% | -34 / -25 / -3 | 0 (0%) | 0 | - |
| cleric-rejuv | 7% | 108 s | 0% | -28 / -15 / 5 | 0 (0%) | 0 | - |
| minstrel | 6% | 122 s | 1% | 47 / 49 / 52 | 2827 (15%) | 29 | 5% |
| paladin | 5% | 135 s | 0% | 32 / 17 / 27 | 1706 (9%) | 19 | 7% |
| sorcerer-mind | 7% | 53 s | 0% | 34 / 47 / 56 | 1164 (6%) | 4 | - |
| theurgist-air | 9% | 70 s | 0% | 23 / 44 / 55 | 350 (2%) | 5 | - |

| hib-main | Dies in | Median death | First to die (of all its fights) | Forward at 5 / 10 / 20 s | Damage (share of the group's) | Damaging hits | Under 20% endurance |
|---|---|---|---|---|---|---|---|
| bard | 88% | 43 s | 49% | 17 / 27 / 41 | 208 (2%) | 3 | - |
| blademaster | 80% | 98 s | 0% | 65 / 67 / 63 | 2508 (45%) | 23 | 11% |
| druid-nurture | 84% | 74 s | 9% | -35 / -26 / -13 | 2 (0%) | 1 | - |
| druid-regrowth | 84% | 69 s | 18% | -35 / -28 / -9 | 5 (0%) | 1 | - |
| eldritch-light | 82% | 74 s | 2% | 5 / 33 / 46 | 707 (6%) | 3 | - |
| enchanter-light | 84% | 72 s | 3% | 41 / 49 / 56 | 1915 (17%) | 6 | - |
| hero | 79% | 107 s | 0% | 37 / 39 / 40 | 1982 (18%) | 16 | 24% |

| mid-main | Dies in | Median death | First to die (of all its fights) | Forward at 5 / 10 / 20 s | Damage (share of the group's) | Damaging hits | Under 20% endurance |
|---|---|---|---|---|---|---|---|
| berserker | 67% | 102 s | 2% | 49 / 60 / 70 | 3442 (28%) | 26 | 0% |
| healer-mend | 67% | 79 s | 5% | -30 / -18 / 7 | 95 (1%) | 5 | - |
| healer-pac | 71% | 55 s | 30% | 1 / 10 / 23 | 0 (0%) | 0 | - |
| runemaster-rune | 67% | 83 s | 7% | -3 / 21 / 41 | 865 (7%) | 6 | - |
| savage | 67% | 91 s | 2% | 52 / 64 / 63 | 2919 (23%) | 22 | 0% |
| shaman-aug | 69% | 63 s | 13% | -7 / 6 / 22 | 98 (1%) | 2 | - |
| skald | 66% | 106 s | 0% | 69 / 84 / 85 | 2584 (21%) | 17 | 0% |
| warrior | 66% | 122 s | 0% | 14 / 20 / 26 | 2429 (20%) | 11 | 0% |

"Forward" is the member's place along the line from where its own group's middle was at contact (0) to where the enemy's was (100). The two middles are about 1,700 units apart, so 50 is the middle of the field. Damage is per bot and match.

| Group | Damage dealt | Of which its pets | The damage it took fell on its |
|---|---|---|---|
| alb-main | 18543 | cabalist-tri (pet) 17%, 66 hits, theurgist-air (pet) 9%, 33 hits | healers 38%, casters 17%, fighters 45% |
| hib-main | 11162 | enchanter-light (pet) 12%, 11 hits, druid-regrowth (pet) 0%, 5 hits, druid-nurture (pet) 0%, 5 hits | healers 27%, casters 37%, fighters 36% |
| mid-main | 12432 | none | healers 33%, casters 20%, fighters 47% |

### 8.8 Kill targets and assist

| Group | Kill-target calls | Called on | Its hits that landed on the called target | Its damage on the called target |
|---|---|---|---|---|
| alb-main | 9.6 | healers 25%, casters 27%, fighters 48% | 79% | 87% |
| hib-main | 3.6 | healers 35%, casters 16%, fighters 49% | 45% | 54% |
| mid-main | 4.7 | healers 29%, casters 28%, fighters 43% | 57% | 59% |

Pets are left out of the last two columns. A low share means the group's damage is spread: interrupts and peels count against it, by design, but so does a train that is not together.

### 8.9 Control done to the enemy

| Done by | Healers asleep | Casters asleep | Fighters asleep | Healers stunned | Casters stunned | Fighters stunned | Fighters rooted or slowed | Healers nearsighted | Casters nearsighted | Fighters diseased | Healers diseased |
|---|---|---|---|---|---|---|---|---|---|---|---|
| alb-main | 37 | 63 | 106 | 29 | 29 | 32 | 33 | 78 | 66 | 167 | 27 |
| hib-main | 4 | 6 | 23 | 4 | 3 | 23 | 30 | 97 | 150 | 73 | 27 |
| mid-main | 14 | 41 | 53 | 6 | 12 | 22 | 23 | 99 | 163 | 109 | 20 |

Seconds in a match that enemy members spent in each state, added over the members (two healers asleep for ten seconds is twenty).

### 8.10 Orders

| Group | In fights it | Push | Extend | Engage | Regroup | Hold |
|---|---|---|---|---|---|---|
| alb-main | won | 78% | 8% | 14% | 0% | 0% |
| alb-main | lost | 3% | 41% | 56% | 0% | 0% |
| hib-main | won | 56% | 29% | 15% | 1% | 0% |
| hib-main | lost | 3% | 53% | 42% | 3% | 0% |
| mid-main | won | 69% | 14% | 17% | 0% | 0% |
| mid-main | lost | 5% | 47% | 47% | 1% | 0% |

Share of the fight under each order, from first contact. Hold comes before contact and is not counted here.

### 8.11 What each member decided

| alb-main | Decisions recorded | Casts finished, by type |
|---|---|---|
| armsman-polearm | Attack 12.5, Trained 1.7, FallBack 0.5 |  |
| cabalist-tri | nothing worth casting 19.6, PetOn 13.4, Damage 12.7, StandOff 9.6, Approach 8.7, Disease 8.3, Debuff 5.3 | Lifedrain 9.9, Disease 6.5, BodyResistDebuff 4.8, Nearsight 4.6, SpeedEnhancement 1.2 |
| cleric-enhance | Heal 8.1, GetAway 4.0, Spread 3.8, StandOff 2.8, Trained 1.5, HangBack 0.4, HealDropped 0.1 | Heal 4.7, DefensiveProc 2.9, HealthRegenBuff 2.4, CureDisease 2.0, Mesmerize 0.6 |
| cleric-rejuv | Heal 9.9, StandOff 4.1, Spread 2.8, GetAway 1.6, HangBack 0.4, Trained 0.3, HealDropped 0.1 | Heal 5.7, HealthRegenBuff 4.1, CureDisease 2.6, BaseArmorFactorBuff 0.5, ConstitutionBuff 0.4 |
| minstrel | Attack 9.3, Duty 3.2, CureMez 2.6, Trained 1.4, Approach 0.7, FallBack 0.1 | DirectDamage 5.0, Mesmerize 4.9, Stun 4.1, CureMezz 2.2, SpeedEnhancement 0.0 |
| paladin | Peel 8.4, Hold 6.7, Assist 4.7, Guard 3.6, Trained 0.6, Defend 0.5 | EnduranceRegenBuff 42.8, SpecArmorFactorBuff 11.6, DamageAdd 9.9, CombatHeal 6.8, PaladinArmorFactorBuff 0.9 |
| sorcerer-mind | nothing worth casting 24.6, StandOff 10.7, Approach 10.3, Damage 6.1, Spread 5.2, MezArea 5.0, Amnesia 2.3 | Mesmerize 4.7, Lifedrain 3.8, Amnesia 2.1, SpeedDecrease 1.4, DexterityDebuff 0.7 |
| theurgist-air | nothing worth casting 24.6, Pet 9.3, StandOff 9.0, Approach 5.8, Root 4.6, Spread 4.5, Duty 2.8 | SummonTheurgistPet 13.5, SpeedDecrease 3.4, DamageSpeedDecrease 3.2, CombatSpeedDebuff 0.7, DirectDamage 0.7 |

| hib-main | Decisions recorded | Casts finished, by type |
|---|---|---|
| bard | nothing worth casting 11.4, Amnesia 7.2, Approach 4.9, GetAway 1.9, Trained 1.8, StandOff 1.7, Damage 1.4 | Amnesia 7.2, EnduranceRegenBuff 5.5, Mesmerize 2.0, EnergyResistBuff 1.9, DirectDamage 1.7 |
| blademaster | Attack 16.0, FallBack 2.0, Trained 1.7 |  |
| druid-nurture | Heal 6.3, GetAway 2.0, StandOff 1.9, Trained 1.0, Spread 0.9, HangBack 0.3, HealDropped 0.1 | Heal 3.9, SpeedDecrease 2.8, CureDisease 1.0, BaseArmorFactorBuff 0.4, DamageShield 0.4 |
| druid-regrowth | Heal 6.0, GetAway 1.9, StandOff 1.7, Trained 1.1, Spread 0.8, HangBack 0.4, HealDropped 0.1 | Heal 3.7, SpeedDecrease 1.8, CureDisease 1.0, DamageShield 0.5, DexterityQuicknessBuff 0.5 |
| eldritch-light | Duty 7.6, nothing worth casting 7.2, Disease 4.4, Nearsight 4.4, StandOff 4.1, Damage 3.5, Approach 2.9 | Nearsight 3.9, Disease 3.3, DirectDamage 2.6, StrengthDebuff 0.2, DexterityDebuff 0.2 |
| enchanter-light | PetOn 12.8, Damage 9.5, Duty 9.0, nothing worth casting 7.0, StandOff 5.5, Approach 4.5, GetAway 1.2 | DirectDamage 6.7, CombatSpeedDebuff 0.3, MeleeDamageDebuff 0.2, SpeedEnhancement 0.1, Heal 0.1 |
| hero | Peel 9.7, Guard 6.5, Hold 5.2, Assist 1.9, Trained 1.5, Defend 0.4 |  |

| mid-main | Decisions recorded | Casts finished, by type |
|---|---|---|
| berserker | Attack 12.5, FallBack 2.5, Trained 1.4 |  |
| healer-mend | nothing worth casting 21.4, Heal 6.1, Approach 4.0, StandOff 3.9, Spread 2.2, CureMez 1.7, Mez 1.7 | Heal 3.7, CureMezz 1.5, Mesmerize 1.2, Amnesia 1.0, Stun 1.0 |
| healer-pac | nothing worth casting 17.1, Approach 9.6, GetAway 4.4, Heal 4.1, Stun 2.8, StandOff 2.8, Spread 2.1 | Heal 2.5, Stun 2.5, Mesmerize 2.2, CureDisease 0.6, CureMezz 0.5 |
| runemaster-rune | nothing worth casting 11.2, Damage 10.9, Nearsight 6.6, StandOff 5.8, Approach 5.2, Duty 4.8, Spread 1.6 | DirectDamage 7.0, Nearsight 5.4, DirectDamageWithDebuff 0.5, SpeedDecrease 0.4, DexterityDebuff 0.3 |
| savage | Attack 12.1, FallBack 2.1, Trained 1.6 | SavageDPSBuff 3.6, SavageCombatSpeedBuff 3.0, SavageEvadeBuff 1.8 |
| shaman-aug | nothing worth casting 19.9, Heal 7.8, Disease 5.8, StandOff 4.4, Approach 4.1, Duty 3.8, Spread 2.1 | Heal 3.9, Disease 3.5, SpeedDecrease 1.2, DamageShield 0.1, DamageAdd 0.0 |
| skald | nothing worth casting 36.5, Duty 8.9, Damage 8.4, Attack 3.7, Mez 2.3, Root 2.3, Trained 1.6 | DirectDamage 8.7, Mesmerize 2.3, SpeedDecrease 2.2, SpeedEnhancement 0.0 |
| warrior | Peel 7.2, Hold 5.3, Guard 4.2, Assist 2.5, Trained 1.3, Defend 0.3 |  |

Per bot and match, from contact. Decisions are the squad's and the scored choice's; a fighter's swings and a healer's heals are not decisions here.

### 8.12 Missed chances

| Seconds a match | alb-main | hib-main | mid-main |
|---|---|---|---|
| enemy healer free | 49 | 157 | 142 |
| enemy healer free, a member idle in range | 7 | 7 | 7 |
| pursuer on the backline | 51 | 106 | 57 |
| pursuer rootable, a rooter free in range | 22 | 13 | 20 |
| enemy mezzable, a mezzer free in range | 50 | 36 | 70 |
| trained 3 s and more with no heal | 3 | 14 | 14 |
| trained with no heal, a healer free in range | 1 | 1 | 1 |

Each line counts member-seconds: two enemy healers free for ten seconds is twenty.

### 8.13 What went wrong for the loser around the turning point

| Group | Fights lost | State in the ten seconds before the turning point (a fight can carry several) |
|---|---|---|
| alb-main | 5 | healers could not heal 80%, three or more asleep 40%, lost a caster 20%, casters could not cast 20% |
| hib-main | 79 | three or more asleep 49%, fighters held 37%, casters could not cast 28%, no single cause 25%, healers could not heal 23%, lost a caster 10% |
| mid-main | 66 | healers could not heal 62%, three or more asleep 35%, casters could not cast 26%, no single cause 21%, fighters held 15%, a healer died 14% |

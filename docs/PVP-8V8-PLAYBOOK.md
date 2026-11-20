# 8v8 playbook: how the bots are meant to fight

**This is not a rulebook.** The owner, 2026-11-18: "The name of the game is how to react to what
is going on. There are no hard rules, only soft rules, and any soft rule could overrule any other
based on the current situation." Should a Bard always run in to stop the Sorcerer on contact?
Probably not; in some fights the better answer is to kite and work on them as they push in.

So this file does not say what a class does. It says:

1. how a fight is read (section 2),
2. which orders a group can be under (section 3),
3. what each role has, what it leans toward, and what shifts that (sections 4 and 5),
4. what is built today, line by line.

It will be wrong in places. Recorded fights correct it, not argument: when a recording shows a
line losing fights, the line changes. [PVP-8V8-ROADMAP.md](PVP-8V8-ROADMAP.md) says when each
missing line gets built.

**Status:** `done` the bots do it and it was seen in recorded matches · `partly` something is
there but not the whole line · `missing` not built.

**Source:** `owner` the owner said so (they played Druid, Cleric and Mending/Augmentation Healer,
and give the rest from memory) · `draft` the owner's review draft of 2026-11-18, general intent
not yet checked against recordings · `data` this server's spell data or recordings · `recall`
the assistant's memory of 1.65 play, unverified.

Merged on 2026-11-18 from the first checklist and the owner's review draft.

## 1. How a bot decides

| Line | Source | Status |
|---|---|---|
| Each turn a bot lists what it could do, scores every option for the situation, and does the best one, or nothing if nothing is worth it. No rotations | owner, draft | done for control, interrupts, cures and caster damage; partly: healing and fighters' styles still run on the old fixed routine |
| The only fixed limits are the ones the game enforces: range, sight, power, recast, immunity, and being dead, mezzed, stunned, silenced or interrupted | draft | done |
| Everything else is weighed, including things that sound like rules: leave a mez alone, stay on the kill target, heal before control | owner | partly: "leave a mezzed enemy asleep unless it is the kill target" is still fixed |
| Weights differ by class and by build (a Pacification Healer and a Mending Healer are one class with two leanings), and can be changed in a file without a new build | owner | done |
| A bot only acts on enemies its group can see; an enemy out of sight is remembered for ten seconds | owner | done |
| One group brain hands out the jobs so eight bots do not all do the same one: kill target, interrupt duty, guard, who heals whom, who mezzes whom. A bot that cannot act (asleep, out of range, dead) loses the job | owner, draft | done |
| Every decision is recorded with what was picked, the runners-up and what stood in the way | draft | done; the parts of each score are not recorded |

## 2. Reading the fight

The owner: "It really comes down to how the enemies' melee are controlled (stun, snare, mez),
how casters are interrupted and how busy the opposite healers are."

Three readings, each taken for both groups:

| Reading | Good for a group when | What goes into it |
|---|---|---|
| **Fighters controlled** | the enemy's fighters are held and its own are free | mezzed, stunned, rooted, snared |
| **Casters interrupted** | the enemy's casters cannot cast and its own can | being interrupted, asleep, stunned, silenced, nearsighted |
| **Healers busy** | the enemy's healers cannot heal and its own can | asleep, interrupted, dead, out of power, or tied up healing one trained member |

With them: how many are alive on each side, and who is out of position (a caster or healer
close to enemy fighters and far from its own).

| Line | Source | Status |
|---|---|---|
| The group brain takes these readings all through the fight and they shift every weight below | owner | partly: taken every second for both groups (an enemy out of sight counts as free) and recorded; they choose the group's order. They do not yet shift a single bot's weights |
| The same readings are reported after the fight for both groups | owner | done in `pvp_analyze.py` |

## 3. Group orders

An order is what the whole group is trying to do right now. It shifts weights; it is not a
script. Under "extend" a Skald still shouts at a caster if that is the best thing it can do.

| Order | What the group does | What tips toward it | Source | Status |
|---|---|---|---|---|
| Hold | Stands at its range and lets the enemy come | Its control outranges theirs (the Sorcerer's mez: 1,875 against 1,500); the enemy has to cross open ground | owner | partly: before the fight a group whose longest mez outranges the enemy realm's by 200 units stands still, its fighters go for nobody and its casters cast what reaches; it ends when an enemy is within 1,200 units, after ten seconds, or at the first blow. In the runs only Albion holds, for about nine seconds, and its Sorcerer's area mez and its Cabalist's nearsight land before Midgard's mezzer is in range. The ground and who saw whom first are not weighed. The other side's answer: in half its matches it stands as well and lets the holder come, nearsighting the enemy mezzer first (nearsight outranges every mez); in the other half it walks in |
| Engage | Closes, mezzes, calls a kill target | The default; equal readings | owner | done |
| Extend (kite) | Falls back together; fighters snare and slam whoever follows; healers and casters keep their distance and keep working | Its healers are alive but cannot heal; enemy fighters are free and on its backline | owner, draft | partly: healers and casters want 1.4 times the usual distance and lean their steps along one line of retreat, straight away from the enemy; the fighters leave the kill target for an enemy fighter that is on them, stun or snare it, and come back to within 1,000 units when nobody follows; casters root and disease the pursuers. Not shown yet to save a fight |
| Push | Goes onto them; fighters do not outrun their healers | Enemy healers asleep, interrupted or dead; enemy fighters held; numbers ahead | draft | partly: healers and casters accept less distance to keep up. Fighters that get out of their healers' reach are called back by Regroup, which is rare in the lanes |
| Peel | Protects the member the enemy's fighters are on | A member is trained | owner | done as a duty of the shield fighter, not as a group order |
| Regroup | Stops chasing and comes back together | The group is strung out; a kill pulled the fighters far from the healers | draft | partly: built. When half or more of the group's living fighters are farther than 1,800 units from the middle of its healers and casters, the order becomes Regroup and every fighter beyond 1,100 leaves its target and comes back. Hardly seen yet: in the lanes the fighters stay close (nine times in ten within 1,350 units), and the order held for 1% of the fight in the check run |

| Line | Source | Status |
|---|---|---|
| The group brain picks the order from the readings and changes it as they change; an order stands at least three seconds | owner | done for Hold, Engage, Push and Extend (runs of 2026-11-18: about 35% engage, 35% push, 28% extend, 2% hold; the winning side mostly pushes, the losing side mostly extends) |
| A retreat is one group moving one way, not eight paths | draft | partly: under Extend a healer's or caster's step is worth more the farther it goes along the group's line of retreat, so they fall back the same way; the fighters come back to them. Built and unit-tested; not measured on its own |
| While kiting, the fighters snare or slam whoever follows as they move back | owner | partly: a fighter going for an enemy that is on its healers and casters uses a stun style, then a snare style, before damage (14 to 19 stuns and snares land a match, no more than before within the noise of six matches); under Extend a fighter with nobody to turn on comes back to within 1,000 units of its healers and casters (about four or five do each match) |
| While kiting, a caster that is already far away casts its snare, area snare or root on the pursuers | owner | done: Shaman, Sorcerer, Theurgist, Cabalist, Runemaster and both Healers root an enemy fighter that is on their healers and casters; the Eldritch uses its snare nuke; whichever of root, area root or snare nuke is the class's highest spell is taken. About seven a match in the run of 2026-11-18, mostly the Theurgist and Sorcerer. A root is refused on a fighter that holds snare immunity (a quarter of the time), and the bots do not try. Not yet shown to save anyone: pursuers were slowed or rooted 8% of the time, against 5% before |
| When the owner plays, their commands set the same order (section 8) | draft | missing |

## 4. The fight, phase by phase

| Phase | What the group does | Source | Status |
|---|---|---|---|
| Before | Buffbot buffs everyone; each member puts up only its own buffs, pets and songs; both groups ready before the match starts | owner | done |
| Travelling | Two rows: fighters in front, healers and casters behind, a shield fighter beside the one it guards | owner | done |
| On incoming | The group sees the enemy at about 4,500 units | owner | done |
| On incoming | Healers and casters keep apart so one area mez catches two at most | owner | done |
| On incoming | **The opening is a choice, not a habit.** Options: the Bard or Pacification Healer runs in to put an instant mez or amnesia on the enemy mezzer; the Sorcerer's group holds at its longer range; the group kites and works on the enemy as it pushes in. Which one depends on the enemy's realm and tools, who saw whom first, and the ground | owner | partly: the Sorcerer's group holds; the Bard and Healer always run in; nobody kites on incoming |
| On incoming | Healers step back out of reach and come back once the fight has settled | owner | done: a healer that only heals (Cleric, Druid, Mending Healer) walks no closer than 2,300 units to the nearest enemy until five seconds after the first blow, then comes in. Seen about three times a match, one or two seconds before contact. It stays back; it does not yet turn and run from an enemy that keeps coming |
| Opening | The main mezzer mezzes as many as it can; an instant mez goes on the enemy's mezzer while it is casting | owner | done |
| Opening | Amnesia on enemies that are casting, also in the middle of one's own cast | owner | done |
| During | One kill target for the whole group, kept until it is dead or something is clearly better | owner | done |
| During | The kill target is whoever is easiest to kill and out of position. A caster or healer that came too far forward is jumped (a nearsighted one has to come forward) | owner | partly: every number behind the call is now a weight (class, health, distance, asleep, how readily to switch), and an enemy standing far from its own healers is worth more, up to 40 points at 2,000 units. Not checked on its own; the night batches vary these weights |
| During | Enemies that are not being killed are kept mezzed, rooted or interrupted | owner | done: an enemy is mezzed again once its immunity has run out (28 times in 18 recorded matches, never while immune) |
| During | A mezzed enemy is usually left asleep. Waking one on purpose can be right: a mezzed healer that is out of position | owner | partly: fixed, not weighed |
| During | Casters and healers stand near the edge of their range and away from enemy fighters | owner | partly: one with a fighter nearer than 900 units steps to a better place, staying in range of its work and of its real range when nearsighted (two steps in three gain distance). The time with no enemy fighter within 500 units went from 44% to 59%. Still short of the line: the median distance is about 550 units, and they walk for a quarter of the fight |
| A member is trained | The group knows who has the enemy's assist train on it | owner | done |
| A member is trained | Healers spam heals on it from the first blow | owner | done |
| A member is trained | A shield fighter guards it and goes for the enemies on it; they are slammed, snared or mezzed | owner | partly: guard, peel and slam; no snare |
| A member is trained | Once peeled, it runs to get distance | owner | partly: it runs once every fighter on it is mezzed, stunned, rooted or slowed (three steps in four gain distance); with a free fighter on it and close it stays, because running from an equally fast fighter gained nothing. Not yet shown to save anyone |
| A member is trained | The group's fighters turn on enemies that pushed deep to reach its healer | owner | partly: under the Extend order |
| Mezzed members | Woken by whoever has cure mez; the group's own mezzer and healers first | owner | done |
| Rank 2 and up | Purge on a long mez; the Druid's Group Purge when several are asleep | owner | partly: Group Purge rarely fires because members purge themselves first |
| During | Buffs, songs and chants kept up; a member that died and was raised is rebuffed | owner | partly: songs and chants are kept up (the Paladin twists three chants, all instants); rebuffing a raised member is not checked in fights |
| During | Dead members are raised when it is safe | recall | done in the shared healing routine: a healer that is not under attack, has half its power (what a resurrection costs) and has a corpse in reach and sight raises it, while at least half the healers keep healing. Rare in fights for those reasons: 0.3 begun a match against 8 deaths |

## 4a. Situations and answers

The owner, 2026-11-18: "There are general basics but then everything goes out the window and you
need to react. The enemies are kiting? Let's SoS in. They push you? Should we kite? Push to
somewhat nullify their push? Lots of variables for a good answer."

This table is where those reactions are collected. A row is a situation the group brain must be
able to recognise, and the answers a real group has for it. **Nobody has to know in advance which
answer is right:** the brain gets every answer, weights choose between them, and recorded batches
show which answer wins under which conditions. What the owner adds here is situations and
answers ("when X, we did Y"); when each answer is the right one is for the recordings to show.

First draft by the assistant, 2026-11-18. Answers marked `recall` are its memory of 1.65 play and
are unverified.

| Situation | Answers a group has | Source | What the bots do today |
|---|---|---|---|
| The enemy stands at its range and will not come | Speed of Sound in; sprint in; hold as well and trade at range; nearsight its mezzer first | owner, recall | Half the time they hold as well and nearsight the mezzer, half the time they walk in. Speed of Sound and sprint are missing |
| The enemy pushes onto the group's healers and casters | Kite together; push back onto their healers so both sides are under the same pressure; hold the ground and peel | owner | Kite (the Extend order) when its healers cannot heal and fighters are on them. Pushing back is not weighed as an answer to a push: Push is only chosen when ahead |
| A member is trained | All heals on it; guard and peel; mez, stun or root the ones on it; once they are held it runs | owner | Heals, guard, peel and running are built. Until 2026-11-18 the train was only seen when two fighters were in melee on the member: a train of one fighter, casters and pets went unseen (the Bard was Albion's kill target 63% of its life and its group saw it 9%). Now every enemy that hit it counts, and control leans toward those enemies. Both are being measured |
| The group's healers are asleep | Whoever has cure mez wakes them; Purge, Group Purge; keep that member alive and free to cast; kite until they are awake | owner, data | The cure is cast when its holder is free. Against Albion it rarely is: while a Druid sleeps the Bard is dead 52% of the time and interrupted, asleep or stunned for most of the rest. Nothing protects it in particular |
| The kill target is not dropping | Switch to one that can be killed; shut the healer that keeps it up (mez, nearsight, interrupt, pets) | data, recall | Interrupt duty is handed out. The call is kept until something beats it by a margin, with no look at whether the target is losing health. Nobody is told to shut the second healer |
| An enemy healer or caster came too far forward | Jump it | owner | Weighed in the kill target (distance and distance from its own healers) |
| The group killed one | Push | recall | Push is chosen when ahead in numbers and readings |
| The group lost one | Unknown: kite and reset, or trade a kill quickly | none | Nothing in particular. The side that loses the first member loses 93% of fights |
| Enemy pets are on the group's healers | Kill them; mez or root them; kill their owner; ignore them | none: asked of the owner | Nothing. From 2026-11-18 a pet's owner counts as one of the enemies on a trained member |
| The enemy mezzer is about to cast | Instant mez or amnesia on it; nearsight it from range | owner | Done |
| The group's fighters are strung out after a kill | Regroup | draft | Done, rarely needed in the lanes |
| The group is out of power or endurance | Unknown | none | Healers save power below 30%; fighters do not budget endurance |

## 5. Roles

For each role: what it leans toward, and what shifts it. "Leans" means a weight, per section 1.

### Healers: Cleric, Druid, Mending Healer

The owner played these and described them first-hand.

| What it does | Source | Status |
|---|---|---|
| Heals almost all the time; about a tenth of the fight goes to root, stun, mez or cure mez to help or peel | owner | done for the Cleric and Druid; partly for the Mending Healer (its mez and stun are weighed low, as intended) |
| The hard part is knowing who is being trained; once that is known all focus goes to that member | owner | done |
| Both healers heal the trained member; one healer cannot keep up with an assist train | owner | done |
| Two heals: the spec line's Major heal for any time, its Greater heal when big heals are needed. Both cost a lot of power | owner, data | done |
| Instant single heal and instant group heal for emergencies | owner, data | done (the old routine uses them below 40% health) |
| When several are hurt at once: the spread heal or a group heal, whichever restores the most health for its power | owner | done: with three or more members in reach below 80% health and nobody trained, the healer works out what each of its spread and group heals would restore for its power and casts the best. Rare in the first run (0.2 a match): somebody is nearly always being trained |
| No group heal for one member | owner | done: the cast group heal is no longer the single-heal routine's last resort; it is only a candidate when several are hurt |
| Does not heal a member that is already full. In the first check run the healers wasted more health than they restored | draft, data | done: a heal on one member is dropped when that member is at 98% or more before it lands (not on the trained member). It fires seldom, 0.3 times a match, because there is little to drop: of 157 big heals in the check run, 6 were single heals with a cast time that landed on a full member. The rest of the "restored nothing" count is the instant group heal reaching members who were not hurt, and heals over time ticking on a full member |
| Who to heal comes from how fast a member is losing health and who is on it, not from the lowest health bar alone | draft | partly: the trained member comes first; damage rate is not used |
| The second healer takes over at once when the first is asleep, interrupted or dead | draft | partly: a backup healer (Pacification Healer, Shaman, Bard) takes over, see below. The two main healers both always heal, so neither has to take over from the other |
| Stands where it is interrupted least; moves early when enemy fighters come for it instead of waiting to be hit; does not follow a fighter into the enemy to keep a heal in range | owner, draft | partly: it steps away from fighters like every caster, but not while a member in reach is below 60% health |
| The two healers do not stand together, so one push or one area mez does not take both | draft | done (keep apart) |
| Does not restart or drop the heal it is casting | data | done |
| Cure nearsight on its casters and healers | owner, data | partly: built, switched off until measured again |
| Power has to last the fight | owner | partly: a healer under 30% of its power heals a member only below 60% health, and the trained member only below 85%. Built and unit-tested; not measured on its own |
| Raises the dead when it is safe | recall | done, and rare: see section 4 |
| Second healer (Enhancement Cleric, Nurture Druid): rebuffs a member that was raised. With a buffbot it has no other buffing to do in a fight | recall | partly |
| Their own utility: the Druid's root, the Cleric's instant point-blank mez | data | partly: cast by the old routine, not weighed |

**What shifts a healer:** nobody trained and nobody hurt → more room for utility. A member
trained → heals only. Itself trained → it runs and the other healer carries (missing).

### Midgard's two Healers and the Shaman

The owner: in Albion and Hibernia the roles are split and known; in Midgard the two Healers are
in a tight spot, and that is what makes the class tricky.

| Who | What it does | Source | Status |
|---|---|---|---|
| Pacification Healer | Interrupt, mez, cure mez and stun come before heals, while knowing when to heal | owner | done: control first unless someone is below 75% health |
| Pacification Healer | When the Mending Healer is asleep, interrupted or dead, it turns to healing | draft | done in the code, not seen on its own: while none of the group's healers that only heal can heal (dead, asleep, stunned, silenced or being interrupted), a backup healer heals first below 95% health in place of 75%. The same goes for the Shaman and the Bard |
| Pacification Healer | May go in to put its instant mez on the enemy's Sorcerer, whose mez outranges its own | owner | partly: it always does; it should be a choice (section 4) |
| Pacification Healer | Area mez, single mez, instant mez and instant stun (single and small area), amnesia, cure mez | data | done |
| Mending Healer | Heals most of the time; has cast mez, stun and cure mez as well | owner | done |
| Mending Healer | Leaves waking people to the Pacification Healer while that one can act | owner | done; no measurable effect on results |
| Mending Healer | Positioned to avoid interrupts. If the enemy pushes deep for it, Midgard turns that against them | owner | partly: it hangs back on incoming, steps away from fighters like every healer, and under Extend the group's fighters turn on whoever came for it |
| Shaman | Interrupts first: disease on everyone, then root, cast even on an immune target because the cast still interrupts | owner | done |
| Shaman | Disease and root go first to enemy fighters that are on the backline, and to whoever follows when the group kites | draft | partly: disease is worth more on a fighter and most on one that is on the backline, and a root likewise; half of its diseases went on fighters in the check runs (a third before) |
| Shaman | Does not root the kill target out of the fighters' reach | draft | done |
| Shaman | Backup heals when the Pacification Healer is too busy | owner | done (below 75% health) |

### Mezzers

Main: Sorcerer, Bard, Pacification Healer. Backup: Minstrel, Mending Healer. (owner)

| Who | What it does | Source | Status |
|---|---|---|---|
| Sorcerer | Area mez from 1,875 units, farther than any Healer or Bard mez (1,500) | owner, data | done: it casts from where it stands before walking for a better target |
| Sorcerer | Its group may hold at that range so the enemy has to come to it | owner | done: see Hold in section 3 |
| Sorcerer | Single mez, cure mez, amnesia (two-second cast), root | data | done |
| Sorcerer | Controls and interrupts; joins the kill only when the target is nearly dead | owner | done |
| Sorcerer | Is woken first when mezzed (it then has a minute of immunity) | data | done |
| Bard | May run in and use instant amnesia while it casts its mez. Not always: see section 4 | owner | partly: it always runs in |
| Bard | Instant mez (single and small area) on the enemy mezzer; cast mez and area mez; cure mez | data | done |
| Bard | Speed and endurance songs while the group runs around, until contact. In the fight only the endurance song matters: if it is already up it stays up and needs no recasting, unlike a Paladin's chants | owner, 2026-11-18 | partly: the first version (morning of 2026-11-18) kept every song up in the fight and was a mistake: each song is a three-second cast, the Bard cycled endurance and three resistance songs from the first second, and spent about 22 of the 46 seconds it lived singing, with no cure mez against Albion. Since that evening it starts only the endurance song in a fight, only when it is not playing, at most once in 20 seconds, and not while it is being hit or a groupmate is asleep. Unit-tested build, not yet measured in matches. Not checked: whether the song stops when the Bard changes instrument to mez |
| Bard | Heals only as a last backup; it is a mezzer first | recall | done as a guess: control comes first unless somebody in reach is below 75% health. In the first run it cast no heal at all. Owner to confirm |
| Any mezzer | Single mez when an area mez would catch the kill target or wake nobody new; area mez when it catches more | draft | done |
| Any mezzer | Re-mezzes an enemy when its mez immunity ends | recall | done: the scored choice looks at immunity every turn, so the mez is an option again the moment it ends (28 times in 18 recorded matches) |
| Any mezzer | Moves after the opening instead of standing where it cast | draft | partly: like every caster it steps away from enemy fighters that come near (the Sorcerer takes about eight steps a match). It does not move for the sake of moving |

**What shifts a mezzer:** running in is worth it when the enemy mezzer is free to cast and
outranges it, and its own group follows. It is not worth it when enemy fighters are free and
between it and the target, or when its group is holding or kiting.

### Interrupters

Any caster, Skald, Minstrel, the Theurgist's pets, Shaman. (owner)

A hit does two things here: it stops a cast under way, and it keeps the target from starting
one for a few seconds. So an interrupter's job is to keep casters hit, not to wait for a cast
and stop it. (data)

| Who | What it does | Source | Status |
|---|---|---|---|
| Each enemy healer and caster that is neither the kill target nor asleep has one interrupter of its own | owner | done |
| Skald | Not part of the assist train unless there is a critical kill. Targets a caster, instant damage shout, switches to another caster, healer or mezzer, second shout, and so on. Changing target often is correct | owner | done: its shouts are scored as interrupts. About ten a match, eight of them on healers and casters that are free to cast, and half go to another enemy than the shout before. It still walks to one enemy at a time for its melee |
| Skald | Running past an enemy fighter, may swing with its snare style, then back to interrupting | owner | partly: its instant snare is cast on a fighter that is on its healers and casters; no snare swing in passing |
| Skald | Instant mez on a free enemy that is not a fighter | owner | partly: scored, 2.5 a match, but more often on a fighter than on a healer or caster |
| Skald, Minstrel | Peels an enemy fighter off its own healers when that matters more than a far caster; does not chase a caster so far that the group loses it | draft | partly: the Skald's instant snare goes on a fighter that is on its healers and casters, and Regroup calls both back when the group is strung out. They do not weigh a peel against a far caster |
| Skald | Speed song for the group | recall | done |
| Minstrel | Runs to enemy healers and casters; instant stun shout and damage shouts | owner, data | partly: cast by the old routine on whatever it is attacking (about six shouts and five stun shouts a match). Scoring them like the Skald's was tried and taken back: it broke the Minstrel's mez |
| Minstrel | Backup mez (cast, and a small area one) on targets the Sorcerer has not taken; cure mez | owner, data | partly: cure mez is weighed; its mezzes are still the old routine |
| Theurgist | Keeps pets on enemy healers and casters, spread over them and not all on one; they stun and interrupt | owner, draft | done |
| Theurgist | Does not send a pet at a mezzed enemy; sends a new one when the target dies or the pet is stuck | draft | partly: mezzed targets are skipped; stuck pets are not noticed |
| Theurgist | Root on a caster that is casting; joins the kill when the target is nearly dead | owner | done |

### Casters

| Who | What it does | Source | Status |
|---|---|---|---|
| Eldritch | Nearsight on casters, then disease, then damage on the kill target | owner | done |
| Runemaster | Nearsight on casters, then damage on the kill target | owner | done |
| Cabalist | The three-line utility build: Body 34, Spirit 33, Matter 25 | owner | done |
| Cabalist | Nearsight on enemy casters, healers and high-impact casters first | owner | done: about five a match; enemy healers and casters were nearsighted more than half of the fight in the first run. Partly: no order among them |
| Cabalist | Disease on enemy fighters and other targets where it matters | owner | done: disease is worth more on a fighter, and more again on one that is on the group's healers and casters. In two check runs 43 of the Cabalist's 83 went on fighters (before: 4 of 30), and 30 of the Shaman's 51 (before: 5 of 15) |
| Cabalist | Keeps its pet on an enemy and takes interrupt assignments | owner | done: the pet goes to its owner's duty target, else the kill target, and lands about 90 attacks a match, more than half on healers and casters. (It had never attacked: with no player near, a pet's brain was never started. An 8v8 bot's pet now thinks like its owner does.) |
| Cabalist | Damage on the kill target when the group has a kill | owner | done (lifedrain) |
| Cabalist | Body resistance debuff on the kill target when the casters are killing it together | owner | done: cast on the kill target before the damage, three to five times a match. On this server it is in the Spirit line (30% at spec 33) and lasts eight seconds |
| Cabalist | No needless damage into a mez | owner | done |
| Enchanter | Damage on the kill target | owner | done |
| Enchanter | Its pet used in the fight; point-blank area damage when enemies are on top of it | recall | partly: the pet attacks (14 attacks landing a match, most on healers and casters). The Light build has no point-blank area damage; that belongs to the Mana line |
| Any caster | Damage only on the kill target; off it, damage kills nobody | owner | done |
| Any caster | Nearsight goes to the enemy whose range matters most, not to one about to die or one safely asleep | draft | partly: casters first; no further choice |
| Any caster | Stands at range; moves away when a fighter comes for it; does not start a long cast when it has to move | owner, draft | partly: see section 4. The Cabalist and Sorcerer take the most steps (11 and 8 a match) and walk a third of the fight |
| Any caster | Keeps enough power for the whole fight | recall | partly: damage is dropped when power is low |

### Shield fighters: Paladin, Warrior, Hero

| What it does | Source | Status |
|---|---|---|
| Guards the healer or caster that needs it most and stays beside it (guard reaches 256 units) | owner | done |
| Changes ward when the threat changes, not on every small drop in health | draft | done |
| Goes for the enemy that is on its ward; Slam on one that can be stunned | owner | done |
| Does not chase that enemy away from the ward | draft | done |
| Helps with the kill only while the target is near its ward | recall | done |
| Snares enemies that are on the backline: a snare when the aim is distance, a stun when the aim is a few safe seconds | owner, draft | partly: going for an enemy that is on a healer or caster it uses Slam first, then a snare style. It does not yet choose between the two by aim |
| Paladin: endurance, armor and damage chants kept up without spending every turn on them | recall | done: it twists three chants (endurance about 45 casts a match, armor 10, damage 8). They are instants and cost it no swing, so this is not the waste it was first taken for |

### Assist train: Armsman, Savage, Berserker, two Blademasters

| What it does | Source | Status |
|---|---|---|
| All on the called target until it dies; nobody picks a target of its own because it is closer or lower | owner, draft | done |
| No taunt styles; stun styles only on a target that can be stunned | data | done |
| Styles chosen for the moment: damage when the kill is on, snare when the target runs, a positional style only when actually behind or beside, a follow-up only after its opener landed | draft | partly: the old routine picks styles; positions are not sought |
| The two Blademasters (and Savage with Berserker) do not both spend the same snare or stun on one target | draft | partly: a stun style is refused on a target that is stunned or immune, and a snare style is passed over on one already slowed. Two swinging in the same half second can still double up |
| Does not outrun its healers; comes back on a kite or regroup order | draft | partly: comes back under Extend when nobody follows, and under Regroup when half the fighters are out of the healers' reach (rare in the lanes) |
| Stays on a target that runs; sprint to close | recall | missing: on this server sprint speeds up players only, not bots |
| Endurance lasts the fight | recall | partly: nobody budgets endurance, but Hibernia's fighters now get the Bard's endurance song in fights. Under a fifth of their endurance: Blademasters 5% of the fight (42 to 65% before), Hero 32% (58 to 65% before) |
| Savage: its self buffs (paid in health) kept up, without killing itself | data, draft | done |

## 6. Realm abilities

| Line | Source | Status |
|---|---|---|
| Rank 1 has no points: no Purge. Fights at rank 1 and at rank 5 are different fights | owner | done (ranks can be set per run) |
| Purge on a long mez | owner | done |
| The Druid's Group Purge when several are asleep; in 1.65 only the Druid has it | owner, data | partly: rarely fires |
| Every match starts with Purge ready; a real group may meet an enemy with Purge already spent | data | missing: no option to start with it spent |
| Speed of Sound: on this server only the Minstrel has it, and it costs points | data | missing: the bots do not use it |

## 7. What this server's data says

Checked on 2026-11-18 against the spell tables and the build catalog. These settle the "verify"
points of the review draft.

| Question | Answer |
|---|---|
| Cure mez | Sorcerer, Minstrel, Bard, Pacification Healer (and Spiritmaster). Not Cleric, not Druid |
| Cure nearsight | Cleric, Druid and Healer healing spec lines: a 6-second cast at spec 20, 3.5 seconds at spec 35. All six healers have it; the three main healers have the fast one. The Shaman has none |
| Spread heal | All six healers (spec 30 and up) |
| Amnesia | Sorcerer and Pacification Healer (cast), Bard (instant) |
| Nearsight | 2,300 range, 2-second cast, two minutes. It sits in a spec line, so its strength follows the spec: Eldritch 65% (Light 45), Runemaster 45% (Suppression 26), Cabalist 45% (Matter 25, from 2026-11-18; before that Albion's group had none). The Sorcerer has no nearsight at any spec |
| Cabalist's spec | Body 34 (disease), Spirit 33 (30% body resistance debuff, best simulacrum), Matter 25 (nearsight). Its lifedrain is baseline |
| Mez range | Sorcerer area mez 1,875; every Healer and Bard mez 1,500 |
| Instant mezzes and stuns | Ten-minute recast: once a fight |
| Heals | Spec Greater 549 health for 109 power; spec Major 387 for 57 |
| Enchanter's spec | Light 45, Enchantments 29, Mana 6 |
| Speed of Sound | Minstrel only |
| Pets | A pet's brain runs only while a player sees the pet. An 8v8 bot's pet is exempt, so pets fight in scrims that nobody watches |
| Sprint | Speeds up players only; a bot that sprints goes no faster |
| Body resistance debuff | In the Cabalist's Spirit line here (30% at spec 33), eight seconds |
| Heals wasted | A quarter of the healers' heals of 100 and more restore nothing, but nearly all of those are the instant group heal reaching members who were not hurt and heals over time ticking on a full member. Single heals with a cast time landing on a full member: 6 of 157 in a check run |
| Endurance | Hibernia's Hero and Blademasters spent a third to two thirds of each fight under a fifth of their endurance, because the Bard put its drum away to fight. Albion's and Midgard's fighters hardly ever ran low |

## 8. The owner's commands (for when the owner plays)

Short commands that stand in for voice calls. Each sets the group order of section 3, so the
bots react to the owner's call the way they react to their own group brain's.

| Command | Order |
|---|---|
| `/extend` | Extend: fall back together, fighters snare whoever follows |
| `/push` | Push: go onto them |
| `/hold` | Hold: stand, do not chase |
| `/assist` | Kill target: the owner's target |
| `/peel` | Peel: protect the member under attack |
| `/regroup` | Regroup: stop chasing, come back together |

Status: missing. Open: whether the server can read such commands from the game client's macros.

**Watching a scrim (built 2026-11-18, not yet tried in the game).** On the scrim install, as a
GM ("Make me GM" in the launcher, with the server stopped):

1. Start the server from the scrim folder's launcher and enter the game.
2. In a terminal: `python source/tools/pvp_watch.py` starts the three pairings in three lanes,
   twenty matches each, and prints a `/jump` line for each lane.
3. In the game: `/8v8 lanes` lists what is fighting, `/8v8 watch 0` takes you to the middle of
   lane 0 and turns the feed on, `/fly` to hover.
4. The feed prints each group's calls near you in the chat window: its order and why, its kill
   target, who is being trained, an area mez being cast, each death, and the match's number and
   recording, so that what you saw can be looked up afterwards. `/8v8 feed off` silences it.
5. Ctrl+C in the terminal, or `pvp_watch.py stop`, ends the fights. The server stays up.

## 9. What is measured

**The referee** (`python source/tools/pvp_referee.py <scrims folder> --batch <prefix>`, from
2026-11-18) is the first thing to run on any batch. It needs nobody to watch a fight:

- a **time budget** for every build: where its living seconds went (casting which kind of
  spell, walking, standing free, unable to act, dead), with a `?` on lines that look odd;
- a list of **rules**, each from something the owner said or saw, with what was expected, what
  was measured, PASS or FAIL, and the worst moments by match and second. One rule is not the
  owner's: how much of their free time the casters spend casting and walking, which is what
  the distance rules cost.

`--html page.html` writes the same report as a page for the browser, and `--before <prefix>`
adds a column with the same rules measured on an earlier batch.

`python source/tools/pvp_replay.py <recording.jsonl>` draws one recorded match from above:
picture sheets of the fight, six moments to a sheet, and `replay.html`, the fight to play and
scrub in a browser with each member's health, power, endurance, state and target beside the
map. It reads the recording only; pets are not recorded and are not drawn. In a play session
the player is marked YOU.

`python source/tools/pvp_sessions.py` lists every play session on one page
(`runtime\logs\replay\index.html` of the scrim install): when, what the player played, against
whom, at what rank, how it ended, with a link to its replay. It draws the replays that are
missing; run it again after playing.

A new observation becomes a new rule there (a function in `RULES`), so that a thing once fixed
cannot come back unnoticed. Who beats whom is not among the rules: see the roadmap's "Current
focus". Lines of sections 3 to 5 whose status changed on the evening of 2026-11-18 are listed
there too, under "Built the same evening and night", until they are folded into the tables here.

Everything that happens in a fight is recorded, so a new question can be asked of old
recordings. Nothing has to be decided in advance. The measures follow the three readings:

| Reading | Measure, per group | In `pvp_analyze.py` |
|---|---|---|
| Fighters controlled | Share of the time its fighters are mezzed, stunned, rooted or slowed | yes |
| Casters interrupted | Share of the time its healers and casters are free to cast; casts begun against finished and what stopped them; time nearsighted | yes |
| Healers busy | Share of the time its healers are free to heal, and dead; health restored and wasted; share that went to the trained member | yes |
| Healers busy | Seconds from a member being trained to the first heal landing on it | no |
| Position | Distance from its healers and casters to the nearest enemy fighter; distance between the groups; how far apart its healers and casters keep | yes |
| Control | Opening mezzes, most caught by one area mez, mezzes broken on the kill target and on somebody else | yes |
| Assist | Share of the time each fighter is on the called target; time to the first kill; who dies first | no |
| Peel | Seconds from a member being trained to a shield fighter reaching the enemies on it | no |
| Orders | Share of the fight spent under each order; each change with the readings behind it (`pvp_review.py`) | yes |

`pvp_review.py` prints one fight second by second, for when a number needs explaining. `pvp_insight.py` reads a whole batch as a coach would: each fight's turning point and what went wrong for the loser just before it, and the chances each group missed (an enemy healer left free, a pursuer nobody rooted, an enemy nobody mezzed, a trained member nobody healed).

**Which weights matter.** Every number in this playbook's "done" lines is a weight, about 180
of them. In a varied batch (`pvp_batch.py Vary=50`) each team of each match fights with its own
draw: about half of the weights, picked at random, moved one step down or up. `pvp_insight.py
--weights` then lists the weights by how surely they moved the result. It needs several hundred
matches, and even then shows only weights that swing the win rate by six points or more; a
weight is believed when it shows again in the next batch. `pvp_night.py` runs a night of it: one
plain batch for the stories and missed chances, three varied ones for the weights.

## 10. Open questions for the owner

Answer only what you remember; recordings of your own fights will answer the rest.

1. **Bard:** do its heals come after control, like the Pacification Healer's?
2. **Spread heal:** when two or three are hurt at once, or in other cases too?
3. **Healer position:** roughly how far behind your fighters did you stand, and what made you move?
4. **Group Purge:** used on the opening mez, or kept?

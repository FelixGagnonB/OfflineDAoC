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
| Extend (kite) | Falls back together; fighters snare and slam whoever follows; healers and casters keep their distance and keep working | Its healers are alive but cannot heal; enemy fighters are free and on its backline | owner, draft | partly: healers and casters want 1.4 times the usual distance, and the fighters leave the kill target for an enemy fighter that is on them. The group does not fall back as one, and nobody snares |
| Push | Goes onto them; fighters do not outrun their healers | Enemy healers asleep, interrupted or dead; enemy fighters held; numbers ahead | draft | partly: healers and casters accept less distance to keep up. The fighters do nothing different, and nothing holds them back for their healers |
| Peel | Protects the member the enemy's fighters are on | A member is trained | owner | done as a duty of the shield fighter, not as a group order |
| Regroup | Stops chasing and comes back together | The group is strung out; a kill pulled the fighters far from the healers | draft | missing |

| Line | Source | Status |
|---|---|---|
| The group brain picks the order from the readings and changes it as they change; an order stands at least three seconds | owner | done for Hold, Engage, Push and Extend (runs of 2026-11-18: about 35% engage, 35% push, 28% extend, 2% hold; the winning side mostly pushes, the losing side mostly extends) |
| A retreat is one group moving one way, not eight paths | draft | missing |
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
| On incoming | Healers step back out of reach and come back once the fight has settled | owner | missing |
| Opening | The main mezzer mezzes as many as it can; an instant mez goes on the enemy's mezzer while it is casting | owner | done |
| Opening | Amnesia on enemies that are casting, also in the middle of one's own cast | owner | done |
| During | One kill target for the whole group, kept until it is dead or something is clearly better | owner | done |
| During | The kill target is whoever is easiest to kill and out of position. A caster or healer that came too far forward is jumped (a nearsighted one has to come forward) | owner | missing: class, health and distance are weighed, exposure is not |
| During | Enemies that are not being killed are kept mezzed, rooted or interrupted | owner | partly: no re-mez when immunity ends |
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
| Does not heal a member that is already full. In the first check run the healers wasted more health than they restored | draft, data | missing |
| Who to heal comes from how fast a member is losing health and who is on it, not from the lowest health bar alone | draft | partly: the trained member comes first; damage rate is not used |
| The second healer takes over at once when the first is asleep, interrupted or dead | draft | partly: both always heal; nothing checks the other's state |
| Stands where it is interrupted least; moves early when enemy fighters come for it instead of waiting to be hit; does not follow a fighter into the enemy to keep a heal in range | owner, draft | partly: it steps away from fighters like every caster, but not while a member in reach is below 60% health |
| The two healers do not stand together, so one push or one area mez does not take both | draft | done (keep apart) |
| Does not restart or drop the heal it is casting | data | done |
| Cure nearsight on its casters and healers | owner, data | partly: built, switched off until measured again |
| Power has to last the fight | owner | missing: heals are not budgeted |
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
| Pacification Healer | When the Mending Healer is asleep, interrupted or dead, it turns to healing | draft | missing: it reacts to health, not to the other Healer's state |
| Pacification Healer | May go in to put its instant mez on the enemy's Sorcerer, whose mez outranges its own | owner | partly: it always does; it should be a choice (section 4) |
| Pacification Healer | Area mez, single mez, instant mez and instant stun (single and small area), amnesia, cure mez | data | done |
| Mending Healer | Heals most of the time; has cast mez, stun and cure mez as well | owner | done |
| Mending Healer | Leaves waking people to the Pacification Healer while that one can act | owner | done; no measurable effect on results |
| Mending Healer | Positioned to avoid interrupts. If the enemy pushes deep for it, Midgard turns that against them | owner | missing |
| Shaman | Interrupts first: disease on everyone, then root, cast even on an immune target because the cast still interrupts | owner | done |
| Shaman | Disease and root go first to enemy fighters that are on the backline, and to whoever follows when the group kites | draft | missing: targets are not chosen that way |
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
| Bard | Speed and endurance songs kept up | recall | partly: songs run, upkeep is not managed |
| Bard | Heals only as a last backup; it is a mezzer first | recall | done as a guess: control comes first unless somebody in reach is below 75% health. In the first run it cast no heal at all. Owner to confirm |
| Any mezzer | Single mez when an area mez would catch the kill target or wake nobody new; area mez when it catches more | draft | done |
| Any mezzer | Re-mezzes an enemy when its mez immunity ends | recall | missing: immunities are not timed |
| Any mezzer | Moves after the opening instead of standing where it cast | draft | missing |

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
| Skald, Minstrel | Peels an enemy fighter off its own healers when that matters more than a far caster; does not chase a caster so far that the group loses it | draft | missing |
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
| Cabalist | Disease on enemy fighters and other targets where it matters | owner | partly: it is cast, but three in four go on healers and casters, not fighters |
| Cabalist | Keeps its pet on an enemy and takes interrupt assignments | owner | missing: the group orders the pet onto the owner's duty or kill target and the pet accepts the order, but it stays beside its owner with no target and never attacks. Not working; cause not found |
| Cabalist | Damage on the kill target when the group has a kill | owner | done (lifedrain) |
| Cabalist | Body resistance debuff on the kill target when the casters are killing it together | owner | missing: never cast |
| Cabalist | No needless damage into a mez | owner | done |
| Enchanter | Damage on the kill target | owner | done |
| Enchanter | Its pet used in the fight; point-blank area damage when enemies are on top of it | recall | missing |
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
| The two Blademasters (and Savage with Berserker) do not both spend the same snare or stun on one target | draft | missing |
| Does not outrun its healers; comes back on a kite or regroup order | draft | missing |
| Stays on a target that runs; sprint to close | recall | missing |
| Endurance lasts the fight | recall | missing: not managed |
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

## 9. What is measured

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

## 10. Open questions for the owner

Answer only what you remember; recordings of your own fights will answer the rest.

1. **Bard:** do its heals come after control, like the Pacification Healer's?
2. **Spread heal:** when two or three are hurt at once, or in other cases too?
3. **Healer position:** roughly how far behind your fighters did you stand, and what made you move?
4. **Group Purge:** used on the opening mez, or kept?

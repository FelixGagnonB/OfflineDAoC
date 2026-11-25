# 8v8: what each class buys with its realm ability points

The order each class of 8v8 bot spends its realm ability points in. The orders are the owner's, written in
[DAoC-1.65-8v8-RA-Progressions-v3.md](DAoC-1.65-8v8-RA-Progressions-v3.md); `source/tools/pvp_ra_orders.py` puts them into the game
(`source/server/GameServer/bots/pvp/PvpRealmAbilities.cs`) and writes this page. Change an order there and run the
tool; `source/tools/pvp_ra_check.py` checks the file without changing anything. What each class may train at all is
in [PVP-8V8-REALM-ABILITY-MENU.md](PVP-8V8-REALM-ABILITY-MENU.md).

## How to read it

- A character has one point for each realm level: 10 at rank 2, 40 at rank 5, 70 at rank 8, 90 at rank 10, 130 at
  the cap (rank 14 level 0).
- **Cost** is what the step costs on top of the levels the list already has; **Total** is the list up to and with
  that step.
- **Held from** is the rank from which a bot has the step for good. A bot does not save up: it goes down its
  list and raises each step as far as its points go, and the next steps are bought with what is left. Its
  purchases are worked out afresh from all its points at each rank, as if a respec were free, so a cheap step
  low in the list can be held for a few levels, given up for a dearer one above it, and bought back later.
- A step past the 130 points of the cap is never bought (*never*). A list that ends short of the cap is followed
  by the build the gamebots use for the class's kind (fighter, caster, healer), cheapest next level first.
- When a bot presses each ability it has bought is in [PVP-8V8-GUIDE.md](PVP-8V8-GUIDE.md), *Realm abilities*.

## Albion

### Cleric -- primary healer (Rejuvenation) / buffer (Enhancement)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Battery of Life | 10 | 54 | 6L4 |
| 7 | Bunker of Faith | 10 | 64 | 7L4 |
| 8 | Raging Power | 10 | 74 | 8L4 |
| 9 | Wild Healing 2 | 4 | 78 | 8L8 |
| 10 | Mastery of Healing 2 | 4 | 82 | 9L2 |
| 11 | Serenity 2 | 4 | 86 | 9L6 |
| 12 | Toughness 2 | 4 | 90 | 10L0 |
| 13 | Wild Healing 3 | 6 | 96 | 10L6 |
| 14 | Mastery of Healing 3 | 6 | 102 | 11L2 |
| 15 | Serenity 3 | 6 | 108 | 11L8 |
| 16 | Toughness 3 | 6 | 114 | 12L4 |
| 17 | Augmented Constitution 2 | 4 | 118 | 12L8 |
| 18 | Augmented Dexterity 2 | 4 | 122 | 13L2 |
| 19 | Augmented Acuity 4 | 10 | 132 | never |
| 20 | Wild Healing 4 | 10 | 142 | never |
| 21 | Mastery of Healing 4 | 10 | 152 | never |
| 22 | Augmented Constitution 3 | 6 | 158 | 13L8 |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Battery Of Life.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Battery Of Life, Bunker of Faith, Wild Healing 2, Mastery of Healing 1, Serenity 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Battery Of Life, Bunker of Faith, Raging Power, Wild Healing 3, Mastery of Healing 2, Serenity 2, Toughness 2, Augmented Constitution 2.

*The list costs 158 points: 28 more than the cap gives.*

### Sorcerer -- main crowd control

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Augmented Acuity 3 | 10 | 21 | 3L1 |
| 4 | Mastery of Concentration | 14 | 35 | 4L5 |
| 5 | Mystic Crystal Lore 2 | 9 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | The Empty Mind 2 | 9 | 63 | 7L3 |
| 8 | Serenity 2 | 4 | 67 | 7L7 |
| 9 | Toughness 2 | 4 | 71 | 8L1 |
| 10 | The Empty Mind 3 | 10 | 81 | 9L1 |
| 11 | Augmented Constitution 2 | 4 | 85 | 9L5 |
| 12 | Augmented Dexterity 2 | 4 | 89 | 9L9 |
| 13 | Toughness 3 | 6 | 95 | 10L5 |
| 14 | Serenity 3 | 6 | 101 | 11L1 |
| 15 | Augmented Acuity 4 | 10 | 111 | 12L1 |
| 16 | Augmented Constitution 3 | 6 | 117 | 12L7 |
| 17 | Augmented Dexterity 3 | 6 | 123 | 13L3 |
| 18 | Toughness 4 | 10 | 133 | never |

*At rank 5 (40 points):* Purge, Tireless, Augmented Acuity 3, Mastery of Concentration, Mystic Crystal Lore 1, Serenity 1, Toughness 1.

*At rank 8 (70 points):* Purge, Tireless, Augmented Acuity 3, Mastery of Concentration, Mystic Crystal Lore 2, Raging Power, The Empty Mind 2, Serenity 2, Toughness 1, Augmented Constitution 1, Augmented Dexterity 1.

*At rank 11 (100 points):* Purge, Tireless, Augmented Acuity 3, Mastery of Concentration, Mystic Crystal Lore 2, Raging Power, The Empty Mind 3, Serenity 2, Toughness 3, Augmented Constitution 2, Augmented Dexterity 2; 5 to spare.

*The list costs 133 points: 3 more than the cap gives.*

### Minstrel -- speed / cure mez / area mez / shouts

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Speed of Sound | 10 | 21 | 3L1 |
| 4 | Mystic Crystal Lore 2 | 9 | 30 | 4L0 |
| 5 | First Aid 2 | 9 | 39 | 4L9 |
| 6 | Augmented Constitution 2 | 4 | 43 | 5L3 |
| 7 | Augmented Dexterity 2 | 4 | 47 | 5L7 |
| 8 | Ignore Pain | 14 | 61 | 7L1 |
| 9 | Toughness 2 | 4 | 65 | 7L5 |
| 10 | Mastery of Pain 2 | 4 | 69 | 7L9 |
| 11 | Augmented Constitution 3 | 6 | 75 | 8L5 |
| 12 | Toughness 3 | 6 | 81 | 9L1 |
| 13 | Mastery of Pain 3 | 6 | 87 | 9L7 |
| 14 | Augmented Dexterity 3 | 6 | 93 | 10L3 |
| 15 | Toughness 4 | 10 | 103 | 11L3 |
| 16 | Augmented Constitution 4 | 10 | 113 | 12L3 |
| 17 | Mastery of Pain 4 | 10 | 123 | 13L3 |

*At rank 5 (40 points):* Tireless, Purge, Speed of Sound, Mystic Crystal Lore 2, First Aid 2, Augmented Constitution 1.

*At rank 8 (70 points):* Tireless, Purge, Speed of Sound, Mystic Crystal Lore 2, First Aid 2, Augmented Constitution 2, Augmented Dexterity 2, Ignore Pain, Toughness 2, Mastery of Pain 2; 1 to spare.

*At rank 11 (100 points):* Tireless, Purge, Speed of Sound, Mystic Crystal Lore 2, First Aid 2, Augmented Constitution 3, Augmented Dexterity 3, Ignore Pain, Toughness 3, Mastery of Pain 3; 7 to spare.

*The list costs 123 points; 7 are left at the cap for the kind's build.*

### Theurgist -- interrupts / pets

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Augmented Acuity 3 | 10 | 21 | 3L1 |
| 4 | Mastery of Concentration | 14 | 35 | 4L5 |
| 5 | Mystic Crystal Lore 2 | 9 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | The Empty Mind 2 | 9 | 63 | 7L3 |
| 8 | Serenity 2 | 4 | 67 | 7L7 |
| 9 | Toughness 2 | 4 | 71 | 8L1 |
| 10 | The Empty Mind 3 | 10 | 81 | 9L1 |
| 11 | Augmented Constitution 2 | 4 | 85 | 9L5 |
| 12 | Augmented Dexterity 2 | 4 | 89 | 9L9 |
| 13 | Toughness 3 | 6 | 95 | 10L5 |
| 14 | Serenity 3 | 6 | 101 | 11L1 |
| 15 | Augmented Acuity 4 | 10 | 111 | 12L1 |
| 16 | Augmented Constitution 3 | 6 | 117 | 12L7 |
| 17 | Augmented Dexterity 3 | 6 | 123 | 13L3 |
| 18 | Toughness 4 | 10 | 133 | never |

*At rank 5 (40 points):* Purge, Tireless, Augmented Acuity 3, Mastery of Concentration, Mystic Crystal Lore 1, Serenity 1, Toughness 1.

*At rank 8 (70 points):* Purge, Tireless, Augmented Acuity 3, Mastery of Concentration, Mystic Crystal Lore 2, Raging Power, The Empty Mind 2, Serenity 2, Toughness 1, Augmented Constitution 1, Augmented Dexterity 1.

*At rank 11 (100 points):* Purge, Tireless, Augmented Acuity 3, Mastery of Concentration, Mystic Crystal Lore 2, Raging Power, The Empty Mind 3, Serenity 2, Toughness 3, Augmented Constitution 2, Augmented Dexterity 2; 5 to spare.

*The list costs 133 points: 3 more than the cap gives.*

### Paladin -- peel (shield)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Determination (hybrid) 3 | 6 | 17 | 2L7 |
| 4 | Faith Healing | 14 | 31 | 4L1 |
| 5 | Augmented Constitution 3 | 10 | 41 | 5L1 |
| 6 | Avoid Pain 1 | 3 | 44 | 5L4 |
| 7 | Augmented Strength 2 | 4 | 48 | 5L8 |
| 8 | Toughness 2 | 4 | 52 | 6L2 |
| 9 | First Aid 2 | 9 | 61 | 7L1 |
| 10 | Ignore Pain | 14 | 75 | 8L5 |
| 11 | Avoid Pain 2 | 6 | 81 | 9L1 |
| 12 | Augmented Strength 3 | 6 | 87 | 9L7 |
| 13 | Augmented Dexterity 2 | 4 | 91 | 10L1 |
| 14 | Toughness 3 | 6 | 97 | 10L7 |
| 15 | Avoid Pain 3 | 10 | 107 | 11L7 |
| 16 | Augmented Constitution 4 | 10 | 117 | 12L7 |
| 17 | Augmented Strength 4 | 10 | 127 | 13L7 |
| 18 | Toughness 4 | 10 | 137 | never |

*At rank 5 (40 points):* Tireless, Purge, Determination 3, Faith Healing, Augmented Constitution 2, Augmented Strength 2, Toughness 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 3, Faith Healing, Augmented Constitution 3, Avoid Pain 2, Augmented Strength 2, Toughness 2, First Aid 2, Augmented Dexterity 1; 2 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 3, Faith Healing, Augmented Constitution 3, Avoid Pain 2, Augmented Strength 3, Toughness 3, First Aid 2, Ignore Pain, Augmented Dexterity 2; 3 to spare.

*The list costs 137 points: 7 more than the cap gives.*

### Armsman -- assist (polearm on the kill target, shield out only to Slam)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge (fighter) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | Soldier's Barricade | 10 | 21 | 3L1 |
| 5 | Augmented Strength 2 | 4 | 25 | 3L5 |
| 6 | Augmented Dexterity 2 | 4 | 29 | 3L9 |
| 7 | Mastery of Pain 2 | 4 | 33 | 4L3 |
| 8 | Determination 4 | 6 | 39 | 4L9 |
| 9 | Augmented Constitution 3 | 10 | 49 | 5L9 |
| 10 | Avoid Pain 1 | 3 | 52 | 6L2 |
| 11 | Determination 5 | 10 | 62 | 7L2 |
| 12 | Toughness 2 | 4 | 66 | 7L6 |
| 13 | First Aid 2 | 9 | 75 | 8L5 |
| 14 | Ignore Pain | 8 | 83 | 9L3 |
| 15 | Avoid Pain 2 | 6 | 89 | 9L9 |
| 16 | Augmented Strength 3 | 6 | 95 | 10L5 |
| 17 | Augmented Dexterity 3 | 6 | 101 | 11L1 |
| 18 | Toughness 3 | 6 | 107 | 11L7 |
| 19 | Mastery of Pain 3 | 6 | 113 | 12L3 |
| 20 | Avoid Pain 3 | 10 | 123 | 13L3 |

*At rank 5 (40 points):* Tireless, Purge, Determination 4, Soldier's Barricade, Augmented Strength 2, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 5, Soldier's Barricade, Augmented Strength 2, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 3, Avoid Pain 1, Toughness 2, First Aid 1; 1 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 5, Soldier's Barricade, Augmented Strength 3, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 3, Avoid Pain 2, Toughness 2, First Aid 2, Ignore Pain; 5 to spare.

*The list costs 123 points; 7 are left at the cap for the kind's build.*

### Cabalist -- debuff / disease / lifedrain

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | The Empty Mind 1 | 3 | 57 | 6L7 |
| 8 | Mastery of Magery 2 | 4 | 61 | 7L1 |
| 9 | Wild Power 2 | 4 | 65 | 7L5 |
| 10 | Serenity 2 | 4 | 69 | 7L9 |
| 11 | Toughness 2 | 4 | 73 | 8L3 |
| 12 | Mastery of Magery 3 | 6 | 79 | 8L9 |
| 13 | The Empty Mind 2 | 6 | 85 | 9L5 |
| 14 | Augmented Constitution 2 | 4 | 89 | 9L9 |
| 15 | Augmented Dexterity 2 | 4 | 93 | 10L3 |
| 16 | Toughness 3 | 6 | 99 | 10L9 |
| 17 | Serenity 3 | 6 | 105 | 11L5 |
| 18 | Wild Power 3 | 6 | 111 | 12L1 |
| 19 | Augmented Acuity 4 | 10 | 121 | 13L1 |
| 20 | Wild Power 4 | 10 | 131 | never |
| 21 | Mastery of Magery 4 | 10 | 141 | never |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, The Empty Mind 1, Mastery of Magery 2, Wild Power 2, Serenity 2, Toughness 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, The Empty Mind 2, Mastery of Magery 3, Wild Power 2, Serenity 2, Toughness 3, Augmented Constitution 2, Augmented Dexterity 2; 1 to spare.

*The list costs 141 points: 11 more than the cap gives.*

### Mercenary -- assist (not in the current roster)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge (fighter) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | Augmented Dexterity 2 | 4 | 15 | 2L5 |
| 5 | Mastery of Pain 2 | 4 | 19 | 2L9 |
| 6 | Dualist's Reflexes 2 | 4 | 23 | 3L3 |
| 7 | Determination 4 | 6 | 29 | 3L9 |
| 8 | Augmented Strength 2 | 4 | 33 | 4L3 |
| 9 | Determination 5 | 10 | 43 | 5L3 |
| 10 | Mastery of Pain 3 | 6 | 49 | 5L9 |
| 11 | Augmented Constitution 2 | 4 | 53 | 6L3 |
| 12 | Toughness 2 | 4 | 57 | 6L7 |
| 13 | First Aid 2 | 9 | 66 | 7L6 |
| 14 | Ignore Pain | 8 | 74 | 8L4 |
| 15 | Augmented Constitution 3 | 6 | 80 | 9L0 |
| 16 | Augmented Dexterity 3 | 6 | 86 | 9L6 |
| 17 | Toughness 3 | 6 | 92 | 10L2 |
| 18 | Augmented Strength 3 | 6 | 98 | 10L8 |
| 19 | Mastery of Pain 4 | 10 | 108 | 11L8 |
| 20 | Augmented Constitution 4 | 10 | 118 | 12L8 |
| 21 | Toughness 4 | 10 | 128 | 13L8 |

*At rank 5 (40 points):* Tireless, Purge, Determination 4, Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2, Augmented Constitution 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 5, Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2, Augmented Constitution 2, Toughness 2, First Aid 2; 4 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 5, Augmented Dexterity 3, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 3, Augmented Constitution 3, Toughness 3, First Aid 2, Ignore Pain; 2 to spare.

*The list costs 128 points; 2 are left at the cap for the kind's build.*

### Wizard -- ranged damage (not in the current roster)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | Mastery of Magery 2 | 4 | 58 | 6L8 |
| 8 | Wild Power 2 | 4 | 62 | 7L2 |
| 9 | The Empty Mind 1 | 3 | 65 | 7L5 |
| 10 | Serenity 2 | 4 | 69 | 7L9 |
| 11 | Toughness 2 | 4 | 73 | 8L3 |
| 12 | Mastery of Magery 3 | 6 | 79 | 8L9 |
| 13 | The Empty Mind 2 | 6 | 85 | 9L5 |
| 14 | Augmented Constitution 2 | 4 | 89 | 9L9 |
| 15 | Augmented Dexterity 2 | 4 | 93 | 10L3 |
| 16 | Toughness 3 | 6 | 99 | 10L9 |
| 17 | Serenity 3 | 6 | 105 | 11L5 |
| 18 | Wild Power 3 | 6 | 111 | 12L1 |
| 19 | Augmented Acuity 4 | 10 | 121 | 13L1 |
| 20 | Wild Power 4 | 10 | 131 | never |
| 21 | Mastery of Magery 4 | 10 | 141 | never |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 2, Wild Power 2, The Empty Mind 1, Serenity 2, Toughness 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 3, Wild Power 2, The Empty Mind 2, Serenity 2, Toughness 3, Augmented Constitution 2, Augmented Dexterity 2; 1 to spare.

*The list costs 141 points: 11 more than the cap gives.*

## Midgard

### Healer -- main healer (Mending) / control-leaning (Pacification)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Perfect Recovery | 14 | 34 | 4L4 |
| 5 | Augmented Acuity 3 | 10 | 44 | 5L4 |
| 6 | Mastery of Concentration | 14 | 58 | 6L8 |
| 7 | Battery of Life | 10 | 68 | 7L8 |
| 8 | Raging Power | 10 | 78 | 8L8 |
| 9 | Wild Healing 2 | 4 | 82 | 9L2 |
| 10 | Mastery of Healing 2 | 4 | 86 | 9L6 |
| 11 | Serenity 2 | 4 | 90 | 10L0 |
| 12 | Toughness 2 | 4 | 94 | 10L4 |
| 13 | Wild Healing 3 | 6 | 100 | 11L0 |
| 14 | Mastery of Healing 3 | 6 | 106 | 11L6 |
| 15 | Serenity 3 | 6 | 112 | 12L2 |
| 16 | Toughness 3 | 6 | 118 | 12L8 |
| 17 | Augmented Constitution 2 | 4 | 122 | 13L2 |
| 18 | Augmented Dexterity 2 | 4 | 126 | 13L6 |
| 19 | Augmented Acuity 4 | 10 | 136 | never |
| 20 | Wild Healing 4 | 10 | 146 | never |
| 21 | Mastery of Healing 4 | 10 | 156 | never |
| 22 | Augmented Constitution 3 | 6 | 162 | never |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Perfect Recovery, Augmented Acuity 2, Wild Healing 1, Mastery of Healing 1.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Perfect Recovery, Augmented Acuity 3, Mastery of Concentration, Battery Of Life, Wild Healing 1, Mastery of Healing 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Perfect Recovery, Augmented Acuity 3, Mastery of Concentration, Battery Of Life, Raging Power, Wild Healing 3, Mastery of Healing 2, Serenity 2, Toughness 2.

*The list costs 162 points: 32 more than the cap gives.*

### Shaman -- buffer / disease / secondary heal

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | First Aid 2 | 9 | 63 | 7L3 |
| 8 | Ignore Pain | 14 | 77 | 8L7 |
| 9 | Wild Healing 2 | 4 | 81 | 9L1 |
| 10 | Mastery of Healing 2 | 4 | 85 | 9L5 |
| 11 | Serenity 2 | 4 | 89 | 9L9 |
| 12 | Toughness 2 | 4 | 93 | 10L3 |
| 13 | Wild Healing 3 | 6 | 99 | 10L9 |
| 14 | Mastery of Healing 3 | 6 | 105 | 11L5 |
| 15 | Serenity 3 | 6 | 111 | 12L1 |
| 16 | Toughness 3 | 6 | 117 | 12L7 |
| 17 | Augmented Constitution 2 | 4 | 121 | 13L1 |
| 18 | Augmented Dexterity 2 | 4 | 125 | 13L5 |
| 19 | Augmented Acuity 4 | 10 | 135 | never |
| 20 | Wild Healing 4 | 10 | 145 | never |
| 21 | Mastery of Healing 4 | 10 | 155 | never |
| 22 | Augmented Constitution 3 | 6 | 161 | never |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, First Aid 2, Wild Healing 2, Mastery of Healing 1, Serenity 1, Toughness 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, First Aid 2, Ignore Pain, Wild Healing 3, Mastery of Healing 2, Serenity 2, Toughness 2, Augmented Constitution 1.

*The list costs 161 points: 31 more than the cap gives.*

### Skald -- speed / shouts / mez / melee

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Speed of Sound | 10 | 21 | 3L1 |
| 4 | Augmented Dexterity 2 | 4 | 25 | 3L5 |
| 5 | Augmented Strength 2 | 4 | 29 | 3L9 |
| 6 | Mastery of Pain 2 | 4 | 33 | 4L3 |
| 7 | Augmented Constitution 2 | 4 | 37 | 4L7 |
| 8 | Toughness 2 | 4 | 41 | 5L1 |
| 9 | First Aid 2 | 9 | 50 | 6L0 |
| 10 | Ignore Pain | 14 | 64 | 7L4 |
| 11 | Augmented Strength 3 | 6 | 70 | 8L0 |
| 12 | Augmented Dexterity 3 | 6 | 76 | 8L6 |
| 13 | Augmented Constitution 3 | 6 | 82 | 9L2 |
| 14 | Mastery of Pain 3 | 6 | 88 | 9L8 |
| 15 | Toughness 3 | 6 | 94 | 10L4 |
| 16 | Toughness 4 | 10 | 104 | 11L4 |
| 17 | Augmented Constitution 4 | 10 | 114 | 12L4 |
| 18 | Mastery of Pain 4 | 10 | 124 | 13L4 |
| 19 | Augmented Strength 4 | 10 | 134 | never |

*At rank 5 (40 points):* Tireless, Purge, Speed of Sound, Augmented Dexterity 2, Augmented Strength 2, Mastery of Pain 2, Augmented Constitution 2, Toughness 1; 2 to spare.

*At rank 8 (70 points):* Tireless, Purge, Speed of Sound, Augmented Dexterity 2, Augmented Strength 3, Mastery of Pain 2, Augmented Constitution 2, Toughness 2, First Aid 2, Ignore Pain.

*At rank 11 (100 points):* Tireless, Purge, Speed of Sound, Augmented Dexterity 3, Augmented Strength 3, Mastery of Pain 3, Augmented Constitution 3, Toughness 3, First Aid 2, Ignore Pain; 6 to spare.

*The list costs 134 points: 4 more than the cap gives.*

### Runemaster -- nukes / nearsight / area damage

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | The Empty Mind 1 | 3 | 57 | 6L7 |
| 8 | Mastery of Magery 2 | 4 | 61 | 7L1 |
| 9 | Wild Power 2 | 4 | 65 | 7L5 |
| 10 | Serenity 2 | 4 | 69 | 7L9 |
| 11 | Toughness 2 | 4 | 73 | 8L3 |
| 12 | Mastery of Magery 3 | 6 | 79 | 8L9 |
| 13 | The Empty Mind 2 | 6 | 85 | 9L5 |
| 14 | Augmented Constitution 2 | 4 | 89 | 9L9 |
| 15 | Augmented Dexterity 2 | 4 | 93 | 10L3 |
| 16 | Toughness 3 | 6 | 99 | 10L9 |
| 17 | Serenity 3 | 6 | 105 | 11L5 |
| 18 | Wild Power 3 | 6 | 111 | 12L1 |
| 19 | Augmented Acuity 4 | 10 | 121 | 13L1 |
| 20 | Wild Power 4 | 10 | 131 | never |
| 21 | Mastery of Magery 4 | 10 | 141 | never |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, The Empty Mind 1, Mastery of Magery 2, Wild Power 2, Serenity 2, Toughness 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, The Empty Mind 2, Mastery of Magery 3, Wild Power 2, Serenity 2, Toughness 3, Augmented Constitution 2, Augmented Dexterity 2; 1 to spare.

*The list costs 141 points: 11 more than the cap gives.*

### Savage -- assist

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge (fighter) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | Augmented Dexterity 2 | 4 | 15 | 2L5 |
| 5 | Mastery of Pain 2 | 4 | 19 | 2L9 |
| 6 | Dualist's Reflexes 2 | 4 | 23 | 3L3 |
| 7 | Determination 4 | 6 | 29 | 3L9 |
| 8 | Augmented Strength 2 | 4 | 33 | 4L3 |
| 9 | Determination 5 | 10 | 43 | 5L3 |
| 10 | Mastery of Pain 3 | 6 | 49 | 5L9 |
| 11 | Augmented Constitution 2 | 4 | 53 | 6L3 |
| 12 | Toughness 2 | 4 | 57 | 6L7 |
| 13 | First Aid 2 | 9 | 66 | 7L6 |
| 14 | Ignore Pain | 8 | 74 | 8L4 |
| 15 | Augmented Constitution 3 | 6 | 80 | 9L0 |
| 16 | Augmented Dexterity 3 | 6 | 86 | 9L6 |
| 17 | Toughness 3 | 6 | 92 | 10L2 |
| 18 | Augmented Strength 3 | 6 | 98 | 10L8 |
| 19 | Mastery of Pain 4 | 10 | 108 | 11L8 |
| 20 | Augmented Constitution 4 | 10 | 118 | 12L8 |
| 21 | Toughness 4 | 10 | 128 | 13L8 |

*At rank 5 (40 points):* Tireless, Purge, Determination 4, Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2, Augmented Constitution 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 5, Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2, Augmented Constitution 2, Toughness 2, First Aid 2; 4 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 5, Augmented Dexterity 3, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 3, Augmented Constitution 3, Toughness 3, First Aid 2, Ignore Pain; 2 to spare.

*The list costs 128 points; 2 are left at the cap for the kind's build.*

### Berserker -- assist

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge (fighter) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | Augmented Dexterity 2 | 4 | 15 | 2L5 |
| 5 | Mastery of Pain 2 | 4 | 19 | 2L9 |
| 6 | Dualist's Reflexes 2 | 4 | 23 | 3L3 |
| 7 | Determination 4 | 6 | 29 | 3L9 |
| 8 | Augmented Strength 2 | 4 | 33 | 4L3 |
| 9 | Determination 5 | 10 | 43 | 5L3 |
| 10 | Mastery of Pain 3 | 6 | 49 | 5L9 |
| 11 | Augmented Constitution 2 | 4 | 53 | 6L3 |
| 12 | Toughness 2 | 4 | 57 | 6L7 |
| 13 | First Aid 2 | 9 | 66 | 7L6 |
| 14 | Ignore Pain | 8 | 74 | 8L4 |
| 15 | Augmented Constitution 3 | 6 | 80 | 9L0 |
| 16 | Augmented Dexterity 3 | 6 | 86 | 9L6 |
| 17 | Toughness 3 | 6 | 92 | 10L2 |
| 18 | Augmented Strength 3 | 6 | 98 | 10L8 |
| 19 | Mastery of Pain 4 | 10 | 108 | 11L8 |
| 20 | Augmented Constitution 4 | 10 | 118 | 12L8 |
| 21 | Toughness 4 | 10 | 128 | 13L8 |

*At rank 5 (40 points):* Tireless, Purge, Determination 4, Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2, Augmented Constitution 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 5, Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2, Augmented Constitution 2, Toughness 2, First Aid 2; 4 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 5, Augmented Dexterity 3, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 3, Augmented Constitution 3, Toughness 3, First Aid 2, Ignore Pain; 2 to spare.

*The list costs 128 points; 2 are left at the cap for the kind's build.*

### Warrior -- peel (hammer and shield)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge (fighter) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | Determination 4 | 6 | 17 | 2L7 |
| 5 | Augmented Constitution 3 | 10 | 27 | 3L7 |
| 6 | Avoid Pain 1 | 3 | 30 | 4L0 |
| 7 | Augmented Dexterity 2 | 4 | 34 | 4L4 |
| 8 | Determination 5 | 10 | 44 | 5L4 |
| 9 | Augmented Strength 2 | 4 | 48 | 5L8 |
| 10 | Mastery of Pain 2 | 4 | 52 | 6L2 |
| 11 | Toughness 2 | 4 | 56 | 6L6 |
| 12 | First Aid 2 | 9 | 65 | 7L5 |
| 13 | Ignore Pain | 8 | 73 | 8L3 |
| 14 | Avoid Pain 2 | 6 | 79 | 8L9 |
| 15 | Augmented Strength 3 | 6 | 85 | 9L5 |
| 16 | Mastery of Pain 3 | 6 | 91 | 10L1 |
| 17 | Augmented Dexterity 3 | 6 | 97 | 10L7 |
| 18 | Toughness 3 | 6 | 103 | 11L3 |
| 19 | Avoid Pain 3 | 10 | 113 | 12L3 |
| 20 | Augmented Constitution 4 | 10 | 123 | 13L3 |

*At rank 5 (40 points):* Tireless, Purge, Determination 4, Augmented Constitution 3, Avoid Pain 1, Augmented Dexterity 2, Augmented Strength 2, Mastery of Pain 1, Toughness 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 5, Augmented Constitution 3, Avoid Pain 1, Augmented Dexterity 2, Augmented Strength 2, Mastery of Pain 2, Toughness 2, First Aid 2; 5 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 5, Augmented Constitution 3, Avoid Pain 2, Augmented Dexterity 3, Augmented Strength 3, Mastery of Pain 3, Toughness 2, First Aid 2, Ignore Pain; 3 to spare.

*The list costs 123 points; 7 are left at the cap for the kind's build.*

### Spiritmaster -- utility / debuff / pets (not in the current roster)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | The Empty Mind 1 | 3 | 57 | 6L7 |
| 8 | Mastery of Magery 2 | 4 | 61 | 7L1 |
| 9 | Wild Power 2 | 4 | 65 | 7L5 |
| 10 | Serenity 2 | 4 | 69 | 7L9 |
| 11 | Toughness 2 | 4 | 73 | 8L3 |
| 12 | Mastery of Magery 3 | 6 | 79 | 8L9 |
| 13 | The Empty Mind 2 | 6 | 85 | 9L5 |
| 14 | Augmented Constitution 2 | 4 | 89 | 9L9 |
| 15 | Augmented Dexterity 2 | 4 | 93 | 10L3 |
| 16 | Toughness 3 | 6 | 99 | 10L9 |
| 17 | Serenity 3 | 6 | 105 | 11L5 |
| 18 | Wild Power 3 | 6 | 111 | 12L1 |
| 19 | Augmented Acuity 4 | 10 | 121 | 13L1 |
| 20 | Wild Power 4 | 10 | 131 | never |
| 21 | Mastery of Magery 4 | 10 | 141 | never |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, The Empty Mind 1, Mastery of Magery 2, Wild Power 2, Serenity 2, Toughness 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, The Empty Mind 2, Mastery of Magery 3, Wild Power 2, Serenity 2, Toughness 3, Augmented Constitution 2, Augmented Dexterity 2; 1 to spare.

*The list costs 141 points: 11 more than the cap gives.*

## Hibernia

### Druid -- primary healer / group sustain (Regrowth main, Nurture secondary)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Group Purge | 14 | 14 | 2L4 |
| 2 | Purge | 10 | 24 | 3L4 |
| 3 | Tireless 1 | 1 | 25 | 3L5 |
| 4 | Mystic Crystal Lore 2 | 9 | 34 | 4L4 |
| 5 | Augmented Acuity 3 | 10 | 44 | 5L4 |
| 6 | Mastery of Concentration | 14 | 58 | 6L8 |
| 7 | Battery of Life | 10 | 68 | 7L8 |
| 8 | Raging Power | 10 | 78 | 8L8 |
| 9 | Wild Healing 2 | 4 | 82 | 9L2 |
| 10 | Mastery of Healing 2 | 4 | 86 | 9L6 |
| 11 | Serenity 2 | 4 | 90 | 10L0 |
| 12 | Toughness 2 | 4 | 94 | 10L4 |
| 13 | Wild Healing 3 | 6 | 100 | 11L0 |
| 14 | Mastery of Healing 3 | 6 | 106 | 11L6 |
| 15 | Serenity 3 | 6 | 112 | 12L2 |
| 16 | Toughness 3 | 6 | 118 | 12L8 |
| 17 | Augmented Constitution 2 | 4 | 122 | 13L2 |
| 18 | Augmented Dexterity 2 | 4 | 126 | 13L6 |
| 19 | Augmented Acuity 4 | 10 | 136 | never |
| 20 | Wild Healing 4 | 10 | 146 | never |
| 21 | Mastery of Healing 4 | 10 | 156 | never |
| 22 | Augmented Constitution 3 | 6 | 162 | never |

*At rank 5 (40 points):* Group Purge, Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 2, Wild Healing 1, Mastery of Healing 1.

*At rank 8 (70 points):* Group Purge, Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Battery Of Life, Wild Healing 1, Mastery of Healing 1.

*At rank 11 (100 points):* Group Purge, Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Battery Of Life, Raging Power, Wild Healing 3, Mastery of Healing 2, Serenity 2, Toughness 2.

*The list costs 162 points: 32 more than the cap gives.*

### Bard -- control / speed / group sustain

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Speed of Sound | 10 | 20 | 3L0 |
| 3 | Tireless 1 | 1 | 21 | 3L1 |
| 4 | Ameliorating Melodies 1 | 14 | 35 | 4L5 |
| 5 | Mystic Crystal Lore 2 | 9 | 44 | 5L4 |
| 6 | Ameliorating Melodies 3 | 0 | 44 | 4L5 |
| 7 | Raging Power | 10 | 54 | 6L4 |
| 8 | First Aid 2 | 9 | 63 | 7L3 |
| 9 | Augmented Constitution 2 | 4 | 67 | 7L7 |
| 10 | Toughness 2 | 4 | 71 | 8L1 |
| 11 | Ignore Pain | 14 | 85 | 9L5 |
| 12 | Augmented Constitution 3 | 6 | 91 | 10L1 |
| 13 | Augmented Dexterity 2 | 4 | 95 | 10L5 |
| 14 | Toughness 3 | 6 | 101 | 11L1 |
| 15 | Augmented Dexterity 3 | 6 | 107 | 11L7 |
| 16 | Toughness 4 | 10 | 117 | 12L7 |
| 17 | Augmented Constitution 4 | 10 | 127 | 13L7 |

*At rank 5 (40 points):* Purge, Speed of Sound, Tireless, Ameliorating Melodies, Mystic Crystal Lore 1, Augmented Constitution 1, Toughness 1.

*At rank 8 (70 points):* Purge, Speed of Sound, Tireless, Ameliorating Melodies, Mystic Crystal Lore 2, Raging Power, First Aid 2, Augmented Constitution 2, Toughness 1, Augmented Dexterity 1; 1 to spare.

*At rank 11 (100 points):* Purge, Speed of Sound, Tireless, Ameliorating Melodies, Mystic Crystal Lore 2, Raging Power, First Aid 2, Augmented Constitution 3, Toughness 2, Ignore Pain, Augmented Dexterity 2; 5 to spare.

*The list costs 127 points; 3 are left at the cap for the kind's build.*

*Not as written:* #6 Ameliorating Melodies 3: it has 1 level here (cost 14); level 3 does not exist.

### Hero -- peel (default)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge (fighter) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | Determination 4 | 6 | 17 | 2L7 |
| 5 | Augmented Constitution 3 | 10 | 27 | 3L7 |
| 6 | Avoid Pain 1 | 3 | 30 | 4L0 |
| 7 | Augmented Dexterity 2 | 4 | 34 | 4L4 |
| 8 | Determination 5 | 10 | 44 | 5L4 |
| 9 | Augmented Strength 2 | 4 | 48 | 5L8 |
| 10 | Toughness 2 | 4 | 52 | 6L2 |
| 11 | Mastery of Pain 2 | 4 | 56 | 6L6 |
| 12 | Avoid Pain 2 | 6 | 62 | 7L2 |
| 13 | Augmented Strength 3 | 6 | 68 | 7L8 |
| 14 | Mastery of Pain 3 | 6 | 74 | 8L4 |
| 15 | Augmented Dexterity 3 | 6 | 80 | 9L0 |
| 16 | Toughness 3 | 6 | 86 | 9L6 |
| 17 | First Aid 2 | 9 | 95 | 10L5 |
| 18 | Ignore Pain | 8 | 103 | 11L3 |
| 19 | Avoid Pain 3 | 10 | 113 | 12L3 |
| 20 | Augmented Constitution 4 | 10 | 123 | 13L3 |

*At rank 5 (40 points):* Tireless, Purge, Determination 4, Augmented Constitution 3, Avoid Pain 1, Augmented Dexterity 2, Augmented Strength 2, Toughness 1, Mastery of Pain 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 5, Augmented Constitution 3, Avoid Pain 2, Augmented Dexterity 2, Augmented Strength 3, Toughness 2, Mastery of Pain 2; 2 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 5, Augmented Constitution 3, Avoid Pain 2, Augmented Dexterity 3, Augmented Strength 3, Toughness 3, Mastery of Pain 3, First Aid 2; 5 to spare.

*The list costs 123 points; 7 are left at the cap for the kind's build.*

### Hero -- assist-train variant *(written, not in the game: the first list of the class is)*

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge (fighter) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | Determination 4 | 6 | 17 | 2L7 |
| 5 | Augmented Strength 2 | 4 | 21 | 3L1 |
| 6 | Augmented Dexterity 2 | 4 | 25 | 3L5 |
| 7 | Mastery of Pain 2 | 4 | 29 | 3L9 |
| 8 | Determination 5 | 10 | 39 | 4L9 |
| 9 | Augmented Constitution 3 | 10 | 49 | 5L9 |
| 10 | Avoid Pain 1 | 3 | 52 | 6L2 |
| 11 | Toughness 2 | 4 | 56 | 6L6 |
| 12 | Avoid Pain 2 | 6 | 62 | 7L2 |
| 13 | Augmented Strength 3 | 6 | 68 | 7L8 |
| 14 | Augmented Dexterity 3 | 6 | 74 | 8L4 |
| 15 | Mastery of Pain 3 | 6 | 80 | 9L0 |
| 16 | Toughness 3 | 6 | 86 | 9L6 |
| 17 | Augmented Constitution 4 | 10 | 96 | 10L6 |
| 18 | Toughness 4 | 10 | 106 | 11L6 |

*At rank 5 (40 points):* Tireless, Purge, Determination 5, Augmented Strength 2, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 5, Augmented Strength 3, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 3, Avoid Pain 2, Toughness 2; 2 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 5, Augmented Strength 3, Augmented Dexterity 3, Mastery of Pain 3, Augmented Constitution 4, Avoid Pain 2, Toughness 3; 4 to spare.

*The list costs 106 points; 24 are left at the cap for the kind's build.*

### Blademaster -- assist (two in the group)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge (fighter) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | Augmented Dexterity 2 | 4 | 15 | 2L5 |
| 5 | Mastery of Pain 2 | 4 | 19 | 2L9 |
| 6 | Dualist's Reflexes 2 | 4 | 23 | 3L3 |
| 7 | Determination 4 | 6 | 29 | 3L9 |
| 8 | Augmented Strength 2 | 4 | 33 | 4L3 |
| 9 | Determination 5 | 10 | 43 | 5L3 |
| 10 | Mastery of Pain 3 | 6 | 49 | 5L9 |
| 11 | Augmented Constitution 2 | 4 | 53 | 6L3 |
| 12 | Toughness 2 | 4 | 57 | 6L7 |
| 13 | Augmented Constitution 3 | 6 | 63 | 7L3 |
| 14 | Augmented Dexterity 3 | 6 | 69 | 7L9 |
| 15 | Toughness 3 | 6 | 75 | 8L5 |
| 16 | Augmented Strength 3 | 6 | 81 | 9L1 |
| 17 | Mastery of Pain 4 | 10 | 91 | 10L1 |
| 18 | Augmented Constitution 4 | 10 | 101 | 11L1 |
| 19 | Toughness 4 | 10 | 111 | 12L1 |

*At rank 5 (40 points):* Tireless, Purge, Determination 4, Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2, Augmented Constitution 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 5, Augmented Dexterity 3, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2, Augmented Constitution 3, Toughness 2; 1 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 5, Augmented Dexterity 3, Mastery of Pain 4, Dualist's Reflexes 2, Augmented Strength 3, Augmented Constitution 3, Toughness 3; 9 to spare.

*The list costs 111 points; 19 are left at the cap for the kind's build.*

### Eldritch -- nukes, nearsight, snare nuke

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | Mastery of Magery 2 | 4 | 58 | 6L8 |
| 8 | Wild Power 2 | 4 | 62 | 7L2 |
| 9 | The Empty Mind 1 | 3 | 65 | 7L5 |
| 10 | Serenity 2 | 4 | 69 | 7L9 |
| 11 | Toughness 2 | 4 | 73 | 8L3 |
| 12 | Mastery of Magery 3 | 6 | 79 | 8L9 |
| 13 | The Empty Mind 2 | 6 | 85 | 9L5 |
| 14 | Augmented Constitution 2 | 4 | 89 | 9L9 |
| 15 | Augmented Dexterity 2 | 4 | 93 | 10L3 |
| 16 | Toughness 3 | 6 | 99 | 10L9 |
| 17 | Serenity 3 | 6 | 105 | 11L5 |
| 18 | Wild Power 3 | 6 | 111 | 12L1 |
| 19 | Augmented Acuity 4 | 10 | 121 | 13L1 |
| 20 | Wild Power 4 | 10 | 131 | never |
| 21 | Mastery of Magery 4 | 10 | 141 | never |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 2, Wild Power 2, The Empty Mind 1, Serenity 2, Toughness 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 3, Wild Power 2, The Empty Mind 2, Serenity 2, Toughness 3, Augmented Constitution 2, Augmented Dexterity 2; 1 to spare.

*The list costs 141 points: 11 more than the cap gives.*

### Enchanter -- debuff / damage / Brilliant Aura of Deflection

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Brilliant Aura of Deflection | 14 | 34 | 4L4 |
| 5 | Augmented Acuity 3 | 10 | 44 | 5L4 |
| 6 | Mastery of Concentration | 14 | 58 | 6L8 |
| 7 | Raging Power | 10 | 68 | 7L8 |
| 8 | Mastery of Magery 2 | 4 | 72 | 8L2 |
| 9 | Wild Power 2 | 4 | 76 | 8L6 |
| 10 | The Empty Mind 1 | 3 | 79 | 8L9 |
| 11 | Serenity 2 | 4 | 83 | 9L3 |
| 12 | Toughness 2 | 4 | 87 | 9L7 |
| 13 | The Empty Mind 2 | 6 | 93 | 10L3 |
| 14 | Augmented Constitution 2 | 4 | 97 | 10L7 |
| 15 | Augmented Dexterity 2 | 4 | 101 | 11L1 |
| 16 | Toughness 3 | 6 | 107 | 11L7 |
| 17 | Serenity 3 | 6 | 113 | 12L3 |
| 18 | Wild Power 3 | 6 | 119 | 12L9 |
| 19 | Augmented Acuity 4 | 10 | 129 | 13L9 |
| 20 | Wild Power 4 | 10 | 139 | never |
| 21 | Mastery of Magery 4 | 16 | 155 | never |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Brilliant Aura Of Deflection, Augmented Acuity 2, Mastery of Magery 1, Wild Power 1.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Brilliant Aura Of Deflection, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 1, Wild Power 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Brilliant Aura Of Deflection, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 2, Wild Power 2, The Empty Mind 2, Serenity 2, Toughness 2, Augmented Constitution 2, Augmented Dexterity 1; 2 to spare.

*The list costs 155 points: 25 more than the cap gives.*

### Warden -- defensive support (not in the current roster)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | First Aid 2 | 9 | 29 | 3L9 |
| 5 | Ignore Pain | 14 | 43 | 5L3 |
| 6 | Augmented Constitution 3 | 10 | 53 | 6L3 |
| 7 | Armor of Faith 1 | 3 | 56 | 6L6 |
| 8 | Augmented Dexterity 2 | 4 | 60 | 7L0 |
| 9 | Mastery of Parrying 2 | 4 | 64 | 7L4 |
| 10 | Toughness 2 | 4 | 68 | 7L8 |
| 11 | Armor of Faith 2 | 6 | 74 | 8L4 |
| 12 | Mastery of Parrying 3 | 6 | 80 | 9L0 |
| 13 | Augmented Dexterity 3 | 6 | 86 | 9L6 |
| 14 | Toughness 3 | 6 | 92 | 10L2 |
| 15 | Armor of Faith 3 | 10 | 102 | 11L2 |
| 16 | Augmented Constitution 4 | 10 | 112 | 12L2 |

*At rank 5 (40 points):* Tireless, Purge, Mystic Crystal Lore 2, First Aid 2, Augmented Constitution 3, Augmented Dexterity 1.

*At rank 8 (70 points):* Tireless, Purge, Mystic Crystal Lore 2, First Aid 2, Ignore Pain, Augmented Constitution 3, Armor of Faith 1, Augmented Dexterity 2, Mastery of Parrying 2, Toughness 2; 2 to spare.

*At rank 11 (100 points):* Tireless, Purge, Mystic Crystal Lore 2, First Aid 2, Ignore Pain, Augmented Constitution 3, Armor of Faith 2, Augmented Dexterity 3, Mastery of Parrying 3, Toughness 3; 8 to spare.

*The list costs 112 points; 18 are left at the cap for the kind's build.*

### Champion -- hybrid melee / debuff (not in the current roster)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Tireless 1 | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Determination (hybrid) 3 | 6 | 17 | 2L7 |
| 4 | Augmented Strength 2 | 4 | 21 | 3L1 |
| 5 | Augmented Dexterity 2 | 4 | 25 | 3L5 |
| 6 | Mastery of Pain 2 | 4 | 29 | 3L9 |
| 7 | Augmented Constitution 3 | 10 | 39 | 4L9 |
| 8 | Avoid Pain 1 | 3 | 42 | 5L2 |
| 9 | Toughness 2 | 4 | 46 | 5L6 |
| 10 | First Aid 2 | 9 | 55 | 6L5 |
| 11 | Ignore Pain | 14 | 69 | 7L9 |
| 12 | Avoid Pain 2 | 6 | 75 | 8L5 |
| 13 | Augmented Strength 3 | 6 | 81 | 9L1 |
| 14 | Mastery of Pain 3 | 6 | 87 | 9L7 |
| 15 | Augmented Dexterity 3 | 6 | 93 | 10L3 |
| 16 | Toughness 3 | 6 | 99 | 10L9 |
| 17 | Avoid Pain 3 | 10 | 109 | 11L9 |
| 18 | Augmented Constitution 4 | 10 | 119 | 12L9 |

*At rank 5 (40 points):* Tireless, Purge, Determination 3, Augmented Strength 2, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 3, Toughness 1.

*At rank 8 (70 points):* Tireless, Purge, Determination 3, Augmented Strength 2, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 3, Avoid Pain 1, Toughness 2, First Aid 2, Ignore Pain; 1 to spare.

*At rank 11 (100 points):* Tireless, Purge, Determination 3, Augmented Strength 3, Augmented Dexterity 3, Mastery of Pain 3, Augmented Constitution 3, Avoid Pain 2, Toughness 3, First Aid 2, Ignore Pain; 1 to spare.

*The list costs 119 points; 11 are left at the cap for the kind's build.*

### Mentalist -- ranged damage / secondary heal (not in the current roster)

| # | Step | Cost | Total | Held from |
|---|---|---|---|---|
| 1 | Purge | 10 | 10 | 2L0 |
| 2 | Tireless 1 | 1 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | Mastery of Magery 2 | 4 | 58 | 6L8 |
| 8 | Wild Power 2 | 4 | 62 | 7L2 |
| 9 | The Empty Mind 1 | 3 | 65 | 7L5 |
| 10 | Serenity 2 | 4 | 69 | 7L9 |
| 11 | Toughness 2 | 4 | 73 | 8L3 |
| 12 | Mastery of Magery 3 | 6 | 79 | 8L9 |
| 13 | The Empty Mind 2 | 6 | 85 | 9L5 |
| 14 | Augmented Constitution 2 | 4 | 89 | 9L9 |
| 15 | Augmented Dexterity 2 | 4 | 93 | 10L3 |
| 16 | Toughness 3 | 6 | 99 | 10L9 |
| 17 | Serenity 3 | 6 | 105 | 11L5 |
| 18 | Wild Power 3 | 6 | 111 | 12L1 |
| 19 | Augmented Acuity 4 | 10 | 121 | 13L1 |
| 20 | Wild Power 4 | 10 | 131 | never |
| 21 | Mastery of Magery 4 | 10 | 141 | never |

*At rank 5 (40 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 2, Wild Power 2, The Empty Mind 1, Serenity 2, Toughness 1.

*At rank 11 (100 points):* Purge, Tireless, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 3, Wild Power 2, The Empty Mind 2, Serenity 2, Toughness 3, Augmented Constitution 2, Augmented Dexterity 2; 1 to spare.

*The list costs 141 points: 11 more than the cap gives.*

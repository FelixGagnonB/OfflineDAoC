# 8v8: what each class buys with its realm ability points

The order each class of 8v8 bot spends its realm ability points in. It is written from
`source/server/GameServer/bots/pvp/PvpRealmAbilities.cs`, which is what the game runs: change an
order there, and this page with it. The orders are a first reading of 1.65 builds and are the
owner's to correct.

## How to read it

- A character has one point for each realm level: 10 at rank 2, 20 at rank 3, 40 at rank 5, 70 at rank 8.
- **Cost** is what the step costs; **Total** is what the order has cost up to and with that step;
  **Rank** is where a bot that buys strictly in order has it (2L5 is rank 2, level 5).
- A bot does not save up. A step it cannot pay for yet is passed over and the next ones are
  bought with what is left; when the points are there, the step comes first again, as if a respec
  were free. So a bot often has a cheaper, later step before a dear, earlier one: the lines
  *At rank 5* and *At rank 8* under each table show what it holds then.
- After its order a class goes on with the build the gamebots use for its kind (fighter, caster,
  healer), cheapest next level first.
- Costs of the passives with five levels: 1, 3, 6, 10, 14. Determination: 1, 2, 3, 6, 10. Mystic
  Crystal Lore, First Aid, Avoid Pain, Armor of Faith, The Empty Mind: 3, 6, 10.
- When a bot uses each active ability is in [PVP-8V8-GUIDE.md](PVP-8V8-GUIDE.md), *Realm abilities*.

## Hibernia

### Druid

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Group Purge | 14 | 15 | 2L5 |
| 3 | Purge | 10 | 25 | 3L5 |
| 4 | Mystic Crystal Lore 2 | 9 | 34 | 4L4 |
| 5 | Augmented Acuity 3 | 10 | 44 | 5L4 |
| 6 | Mastery of Concentration | 14 | 58 | 6L8 |
| 7 | Battery of Life | 10 | 68 | 7L8 |
| 8 | Raging Power | 10 | 78 | 8L8 |
| 9 | Serenity 2 | 4 | 82 | 9L2 |
| 10 | Wild Healing 2 | 4 | 86 | 9L6 |
| 11 | Mastery of Healing 2 | 4 | 90 | 10L0 |
| 12 | Toughness 2 | 4 | 94 | 10L4 |

*At rank 5 (40 points), from its order:* Tireless, Group Purge, Purge, Mystic Crystal Lore 2, Augmented Acuity 2, Serenity 1, Wild Healing 1.

*At rank 8 (70 points), from its order:* Tireless, Group Purge, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Battery of Life, Serenity 1, Wild Healing 1.

### Bard

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Speed of Sound | (10) | | not in the game's data for this class: skipped |
| 4 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 5 | Ameliorating Melodies | 14 | 34 | 4L4 |
| 6 | Raging Power | 10 | 44 | 5L4 |
| 7 | First Aid 2 | 9 | 53 | 6L3 |
| 8 | Ignore Pain | 14 | 67 | 7L7 |
| 9 | Augmented Constitution 2 | 4 | 71 | 8L1 |
| 10 | Toughness 2 | 4 | 75 | 8L5 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Ameliorating Melodies, First Aid 1, Augmented Constitution 1, Toughness 1; 1 left for the build of its kind.

*At rank 8 (70 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Ameliorating Melodies, Raging Power, First Aid 2, Ignore Pain, Augmented Constitution 1, Toughness 1; 1 left for the build of its kind.

### Hero

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge (fighter's) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | First Aid 2 | 9 | 20 | 3L0 |
| 5 | Ignore Pain (fighter's) | 8 | 28 | 3L8 |
| 6 | Determination 4 | 6 | 34 | 4L4 |
| 7 | Augmented Strength 2 | 4 | 38 | 4L8 |
| 8 | Augmented Dexterity 2 | 4 | 42 | 5L2 |
| 9 | Mastery of Pain 2 | 4 | 46 | 5L6 |
| 10 | Determination 5 | 10 | 56 | 6L6 |
| 11 | Augmented Constitution 3 | 10 | 66 | 7L6 |
| 12 | Avoid Pain 1 | 3 | 69 | 7L9 |
| 13 | Toughness 2 | 4 | 73 | 8L3 |

*At rank 5 (40 points), from its order:* Tireless, Purge (fighter's), Determination 4, First Aid 2, Ignore Pain (fighter's), Augmented Strength 2, Augmented Dexterity 1, Augmented Constitution 1.

*At rank 8 (70 points), from its order:* Tireless, Purge (fighter's), Determination 5, First Aid 2, Ignore Pain (fighter's), Augmented Strength 2, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 3, Avoid Pain 1, Toughness 1.

### Blademaster

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge (fighter's) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | First Aid 2 | 9 | 20 | 3L0 |
| 5 | Ignore Pain (fighter's) | 8 | 28 | 3L8 |
| 6 | Augmented Dexterity 2 | 4 | 32 | 4L2 |
| 7 | Mastery of Pain 2 | 4 | 36 | 4L6 |
| 8 | Determination 4 | 6 | 42 | 5L2 |
| 9 | Dualist's Reflexes 2 | 4 | 46 | 5L6 |
| 10 | Augmented Strength 2 | 4 | 50 | 6L0 |
| 11 | Determination 5 | 10 | 60 | 7L0 |
| 12 | Mastery of Pain 3 | 6 | 66 | 7L6 |

*At rank 5 (40 points), from its order:* Tireless, Purge (fighter's), Determination 3, First Aid 2, Ignore Pain (fighter's), Augmented Dexterity 2, Mastery of Pain 2, Dualist's Reflexes 2.

*At rank 8 (70 points), from its order:* Tireless, Purge (fighter's), Determination 5, First Aid 2, Ignore Pain (fighter's), Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2; 4 left for the build of its kind.

### Eldritch, Mentalist

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | Mastery of Magery 2 | 4 | 58 | 6L8 |
| 8 | Wild Power 2 | 4 | 62 | 7L2 |
| 9 | Serenity 2 | 4 | 66 | 7L6 |
| 10 | The Empty Mind 1 | 3 | 69 | 7L9 |
| 11 | Toughness 2 | 4 | 73 | 8L3 |
| 12 | Mastery of Magery 3 | 6 | 79 | 8L9 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 2, Wild Power 2, Serenity 2, The Empty Mind 1, Toughness 1.

### Enchanter

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Brilliant Aura of Deflection | 14 | 58 | 6L8 |
| 7 | Raging Power | 10 | 68 | 7L8 |
| 8 | Mastery of Magery 2 | 4 | 72 | 8L2 |
| 9 | Wild Power 2 | 4 | 76 | 8L6 |
| 10 | Serenity 2 | 4 | 80 | 9L0 |
| 11 | The Empty Mind 1 | 3 | 83 | 9L3 |
| 12 | Toughness 2 | 4 | 87 | 9L7 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Brilliant Aura of Deflection, Raging Power, Mastery of Magery 1, Wild Power 1.

### Champion

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Determination (hybrid) 3 | 6 | 17 | 2L7 |
| 4 | First Aid 2 | 9 | 26 | 3L6 |
| 5 | Ignore Pain | 14 | 40 | 5L0 |
| 6 | Augmented Strength 3 | 10 | 50 | 6L0 |
| 7 | Augmented Dexterity 2 | 4 | 54 | 6L4 |
| 8 | Mastery of Pain 2 | 4 | 58 | 6L8 |
| 9 | Augmented Constitution 2 | 4 | 62 | 7L2 |
| 10 | Toughness 2 | 4 | 66 | 7L6 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Determination (hybrid) 3, First Aid 2, Ignore Pain.

*At rank 8 (70 points), from its order:* Tireless, Purge, Determination (hybrid) 3, First Aid 2, Ignore Pain, Augmented Strength 3, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 2, Toughness 2; 4 left for the build of its kind.

### Warden

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | First Aid 2 | 9 | 29 | 3L9 |
| 5 | Ignore Pain | 14 | 43 | 5L3 |
| 6 | Augmented Constitution 3 | 10 | 53 | 6L3 |
| 7 | Armor of Faith 1 | 3 | 56 | 6L6 |
| 8 | Augmented Dexterity 2 | 4 | 60 | 7L0 |
| 9 | Mastery of Parrying 2 | 4 | 64 | 7L4 |
| 10 | Toughness 2 | 4 | 68 | 7L8 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, First Aid 2, Augmented Constitution 3, Augmented Dexterity 1.

*At rank 8 (70 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, First Aid 2, Ignore Pain, Augmented Constitution 3, Armor of Faith 1, Augmented Dexterity 2, Mastery of Parrying 2, Toughness 2; 2 left for the build of its kind.

## Albion

### Cleric

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Battery of Life | 10 | 54 | 6L4 |
| 7 | Bunker of Faith | 10 | 64 | 7L4 |
| 8 | Raging Power | 10 | 74 | 8L4 |
| 9 | Serenity 2 | 4 | 78 | 8L8 |
| 10 | Wild Healing 2 | 4 | 82 | 9L2 |
| 11 | Mastery of Healing 2 | 4 | 86 | 9L6 |
| 12 | Toughness 2 | 4 | 90 | 10L0 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Battery of Life.

*At rank 8 (70 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Battery of Life, Bunker of Faith, Serenity 2, Wild Healing 1, Mastery of Healing 1.

### Minstrel

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Speed of Sound | 10 | 21 | 3L1 |
| 4 | Mystic Crystal Lore 1 | 3 | 24 | 3L4 |
| 5 | First Aid 2 | 9 | 33 | 4L3 |
| 6 | Ignore Pain | 14 | 47 | 5L7 |
| 7 | Augmented Constitution 2 | 4 | 51 | 6L1 |
| 8 | Augmented Dexterity 2 | 4 | 55 | 6L5 |
| 9 | Mastery of Pain 2 | 4 | 59 | 6L9 |
| 10 | Toughness 2 | 4 | 63 | 7L3 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Speed of Sound, Mystic Crystal Lore 1, First Aid 2, Augmented Constitution 2, Augmented Dexterity 1, Toughness 1; 1 left for the build of its kind.

*At rank 8 (70 points), from its order:* Tireless, Purge, Speed of Sound, Mystic Crystal Lore 1, First Aid 2, Ignore Pain, Augmented Constitution 2, Augmented Dexterity 2, Mastery of Pain 2, Toughness 2; 7 left for the build of its kind.

### Paladin

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Determination (hybrid) 3 | 6 | 17 | 2L7 |
| 4 | First Aid 2 | 9 | 26 | 3L6 |
| 5 | Ignore Pain | 14 | 40 | 5L0 |
| 6 | Faith Healing | 14 | 54 | 6L4 |
| 7 | Augmented Constitution 3 | 10 | 64 | 7L4 |
| 8 | Avoid Pain 1 | 3 | 67 | 7L7 |
| 9 | Augmented Strength 2 | 4 | 71 | 8L1 |
| 10 | Toughness 2 | 4 | 75 | 8L5 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Determination (hybrid) 3, First Aid 2, Ignore Pain.

*At rank 8 (70 points), from its order:* Tireless, Purge, Determination (hybrid) 3, First Aid 2, Ignore Pain, Faith Healing, Augmented Constitution 3, Avoid Pain 1, Augmented Strength 1, Toughness 1; 1 left for the build of its kind.

### Armsman

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge (fighter's) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | First Aid 2 | 9 | 20 | 3L0 |
| 5 | Ignore Pain (fighter's) | 8 | 28 | 3L8 |
| 6 | Soldier's Barricade | 10 | 38 | 4L8 |
| 7 | Determination 4 | 6 | 44 | 5L4 |
| 8 | Augmented Strength 2 | 4 | 48 | 5L8 |
| 9 | Augmented Dexterity 2 | 4 | 52 | 6L2 |
| 10 | Mastery of Pain 2 | 4 | 56 | 6L6 |
| 11 | Determination 5 | 10 | 66 | 7L6 |
| 12 | Augmented Constitution 3 | 10 | 76 | 8L6 |
| 13 | Avoid Pain 1 | 3 | 79 | 8L9 |
| 14 | Toughness 2 | 4 | 83 | 9L3 |

*At rank 5 (40 points), from its order:* Tireless, Purge (fighter's), Determination 3, First Aid 2, Ignore Pain (fighter's), Soldier's Barricade, Augmented Strength 1, Augmented Dexterity 1.

*At rank 8 (70 points), from its order:* Tireless, Purge (fighter's), Determination 5, First Aid 2, Ignore Pain (fighter's), Soldier's Barricade, Augmented Strength 2, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 2.

### Mercenary

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge (fighter's) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | First Aid 2 | 9 | 20 | 3L0 |
| 5 | Ignore Pain (fighter's) | 8 | 28 | 3L8 |
| 6 | Augmented Dexterity 2 | 4 | 32 | 4L2 |
| 7 | Mastery of Pain 2 | 4 | 36 | 4L6 |
| 8 | Determination 4 | 6 | 42 | 5L2 |
| 9 | Dualist's Reflexes 2 | 4 | 46 | 5L6 |
| 10 | Augmented Strength 2 | 4 | 50 | 6L0 |
| 11 | Determination 5 | 10 | 60 | 7L0 |
| 12 | Mastery of Pain 3 | 6 | 66 | 7L6 |

*At rank 5 (40 points), from its order:* Tireless, Purge (fighter's), Determination 3, First Aid 2, Ignore Pain (fighter's), Augmented Dexterity 2, Mastery of Pain 2, Dualist's Reflexes 2.

*At rank 8 (70 points), from its order:* Tireless, Purge (fighter's), Determination 5, First Aid 2, Ignore Pain (fighter's), Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2; 4 left for the build of its kind.

### Sorcerer, Theurgist, Cabalist, Wizard

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | Mastery of Magery 2 | 4 | 58 | 6L8 |
| 8 | Wild Power 2 | 4 | 62 | 7L2 |
| 9 | Serenity 2 | 4 | 66 | 7L6 |
| 10 | The Empty Mind 1 | 3 | 69 | 7L9 |
| 11 | Toughness 2 | 4 | 73 | 8L3 |
| 12 | Mastery of Magery 3 | 6 | 79 | 8L9 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 2, Wild Power 2, Serenity 2, The Empty Mind 1, Toughness 1.

## Midgard

### Healer

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Perfect Recovery | 14 | 58 | 6L8 |
| 7 | Battery of Life | 10 | 68 | 7L8 |
| 8 | Raging Power | 10 | 78 | 8L8 |
| 9 | Serenity 2 | 4 | 82 | 9L2 |
| 10 | Wild Healing 2 | 4 | 86 | 9L6 |
| 11 | Mastery of Healing 2 | 4 | 90 | 10L0 |
| 12 | Toughness 2 | 4 | 94 | 10L4 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Battery of Life.

*At rank 8 (70 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Perfect Recovery, Battery of Life, Serenity 1, Wild Healing 1.

### Shaman

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | Serenity 2 | 4 | 58 | 6L8 |
| 8 | Wild Healing 2 | 4 | 62 | 7L2 |
| 9 | Mastery of Healing 2 | 4 | 66 | 7L6 |
| 10 | Toughness 2 | 4 | 70 | 8L0 |
| 11 | First Aid 2 | 9 | 79 | 8L9 |
| 12 | Ignore Pain | 14 | 93 | 10L3 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Serenity 2, Wild Healing 2, Mastery of Healing 2, Toughness 2.

### Skald

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Speed of Sound | (10) | | not in the game's data for this class: skipped |
| 4 | First Aid 2 | 9 | 20 | 3L0 |
| 5 | Ignore Pain | 14 | 34 | 4L4 |
| 6 | Augmented Strength 3 | 10 | 44 | 5L4 |
| 7 | Augmented Dexterity 2 | 4 | 48 | 5L8 |
| 8 | Mastery of Pain 2 | 4 | 52 | 6L2 |
| 9 | Augmented Constitution 2 | 4 | 56 | 6L6 |
| 10 | Toughness 2 | 4 | 60 | 7L0 |

*At rank 5 (40 points), from its order:* Tireless, Purge, First Aid 2, Ignore Pain, Augmented Strength 2, Augmented Dexterity 1, Augmented Constitution 1.

*At rank 8 (70 points), from its order:* Tireless, Purge, First Aid 2, Ignore Pain, Augmented Strength 3, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 2, Toughness 2; 10 left for the build of its kind.

### Warrior

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge (fighter's) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | First Aid 2 | 9 | 20 | 3L0 |
| 5 | Ignore Pain (fighter's) | 8 | 28 | 3L8 |
| 6 | Determination 4 | 6 | 34 | 4L4 |
| 7 | Augmented Strength 2 | 4 | 38 | 4L8 |
| 8 | Augmented Dexterity 2 | 4 | 42 | 5L2 |
| 9 | Mastery of Pain 2 | 4 | 46 | 5L6 |
| 10 | Determination 5 | 10 | 56 | 6L6 |
| 11 | Augmented Constitution 3 | 10 | 66 | 7L6 |
| 12 | Avoid Pain 1 | 3 | 69 | 7L9 |
| 13 | Toughness 2 | 4 | 73 | 8L3 |

*At rank 5 (40 points), from its order:* Tireless, Purge (fighter's), Determination 4, First Aid 2, Ignore Pain (fighter's), Augmented Strength 2, Augmented Dexterity 1, Augmented Constitution 1.

*At rank 8 (70 points), from its order:* Tireless, Purge (fighter's), Determination 5, First Aid 2, Ignore Pain (fighter's), Augmented Strength 2, Augmented Dexterity 2, Mastery of Pain 2, Augmented Constitution 3, Avoid Pain 1, Toughness 1.

### Berserker, Savage

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge (fighter's) | 4 | 5 | 1L5 |
| 3 | Determination 3 | 6 | 11 | 2L1 |
| 4 | First Aid 2 | 9 | 20 | 3L0 |
| 5 | Ignore Pain (fighter's) | 8 | 28 | 3L8 |
| 6 | Augmented Dexterity 2 | 4 | 32 | 4L2 |
| 7 | Mastery of Pain 2 | 4 | 36 | 4L6 |
| 8 | Determination 4 | 6 | 42 | 5L2 |
| 9 | Dualist's Reflexes 2 | 4 | 46 | 5L6 |
| 10 | Augmented Strength 2 | 4 | 50 | 6L0 |
| 11 | Determination 5 | 10 | 60 | 7L0 |
| 12 | Mastery of Pain 3 | 6 | 66 | 7L6 |

*At rank 5 (40 points), from its order:* Tireless, Purge (fighter's), Determination 3, First Aid 2, Ignore Pain (fighter's), Augmented Dexterity 2, Mastery of Pain 2, Dualist's Reflexes 2.

*At rank 8 (70 points), from its order:* Tireless, Purge (fighter's), Determination 5, First Aid 2, Ignore Pain (fighter's), Augmented Dexterity 2, Mastery of Pain 3, Dualist's Reflexes 2, Augmented Strength 2; 4 left for the build of its kind.

### Runemaster, Spiritmaster

| # | Step | Cost | Total | Rank |
|---|---|---|---|---|
| 1 | Tireless | 1 | 1 | 1L1 |
| 2 | Purge | 10 | 11 | 2L1 |
| 3 | Mystic Crystal Lore 2 | 9 | 20 | 3L0 |
| 4 | Augmented Acuity 3 | 10 | 30 | 4L0 |
| 5 | Mastery of Concentration | 14 | 44 | 5L4 |
| 6 | Raging Power | 10 | 54 | 6L4 |
| 7 | Mastery of Magery 2 | 4 | 58 | 6L8 |
| 8 | Wild Power 2 | 4 | 62 | 7L2 |
| 9 | Serenity 2 | 4 | 66 | 7L6 |
| 10 | The Empty Mind 1 | 3 | 69 | 7L9 |
| 11 | Toughness 2 | 4 | 73 | 8L3 |
| 12 | Mastery of Magery 3 | 6 | 79 | 8L9 |

*At rank 5 (40 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Raging Power.

*At rank 8 (70 points), from its order:* Tireless, Purge, Mystic Crystal Lore 2, Augmented Acuity 3, Mastery of Concentration, Raging Power, Mastery of Magery 2, Wild Power 2, Serenity 2, The Empty Mind 1, Toughness 1.

## Needs before it can be bought

| Ability | Needs |
|---|---|
| Mastery of Concentration | Augmented Acuity 3 |
| Raging Power | Mystic Crystal Lore 2 |
| Ignore Pain | First Aid 2 |
| Ignore Pain (fighter's) | First Aid 2 |
| Avoid Pain | Augmented Constitution 3 |
| Armor of Faith | Augmented Constitution 3 |
| Mastery of Pain | Augmented Dexterity 2 |
| Mastery of Parrying | Augmented Dexterity 2 |
| Dualist's Reflexes | Augmented Dexterity 2 |
| Mastery of Magery | Augmented Acuity 2 |
| Wild Power | Augmented Acuity 2 |
| Wild Healing | Augmented Acuity 2 |
| Mastery of Healing | Augmented Acuity 2 |
| Serenity | Augmented Acuity 2 |

## Open

- **Speed of Sound for the Bard and the Skald.** The game's data gives it to the Minstrel alone. The
  two orders have it as their third step and buy it the moment the class is given it.
- **Bought by no bot**, because no bot knows how to use it: Volcanic Pillar, Negative Maelstrom, Rune of
  Decimation, Thornweed Field, Ichor of the Deep, Static Tempest, Wrath of the Champion, Bladedance,
  Whirling Dervish, Corporeal Disintegration, Trip, Grapple, Battle Yell, the style abilities, Rain of
  Fire, Ice and Annihilation, Fury of the Gods, Hail of Blows, Juggernaut, Excited Frenzy, Whip of
  Encouragement, Severing the Tether, Majestic Will, Concentration.

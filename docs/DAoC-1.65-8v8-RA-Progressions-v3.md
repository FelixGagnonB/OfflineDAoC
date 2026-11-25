# DAoC 1.65 -- 8v8 realm ability progression (v3)

## Scope

Priority orders for bot realm-ability purchases on the custom 1.65 build,
for organized 8v8. Roles come from the group table (not from class
defaults). Costs, class access and prerequisites come from
`PVP-8V8-REALM-ABILITIES.md` and the prerequisite table below.

Purchase algorithm is unchanged: scan from the top, buy the first unowned
step that is affordable and whose prerequisites are met, then scan again.

Run `validate_ra.py` on this file after every edit.

## Budget (this server)

| Realm rank | Ability points |
|---|---|
| 1L1 (start) | 1 |
| 2L0 | 10 |
| 3L0 | 20 |
| 5L0 | 40 |
| 8L0 | 70 |
| 10L0 | 90 |
| 11L0 | 100 |
| 12L0 | 110 |
| 13L0 | 120 |
| 14L0 (cap) | 130 |

The first 40-60 points of each list decide how a bot plays RR5-RR9. The
rest of each list is the **tail**: it spends the remaining points up to
the 130-point cap, and the order matters little there. Tails only raise
levels of abilities already in the list or add cheap passives; actives are
not upgraded past their listed level until their max level and cost are
confirmed.

## Reading the lists

- The level after a name is the **target level**. The server buys one level
  at a time at the incremental cost.
- A name with no level is level 1 (actives, or the single purchase).
- `(fighter)` / `(hybrid)` only select the class-appropriate version.
- Costs: five-level passives 1/3/6/10/14, Determination 1/2/3/6/10,
  MCL / First Aid / Avoid Pain / Armor of Faith / The Empty Mind 3/6/10.
- Gate items are bought for what they unlock. Their total cost is in the
  table below, so judge an entry by its total, not by its own cost.

## Prerequisites (this build)

| Needs | Unlocks |
|---|---|
| Augmented Dexterity 2 | Mastery of Pain, Mastery of Parrying, Mastery of Blocking, Dualist's Reflexes, Falcon's Eye |
| Augmented Dexterity 3 | Mastery of Archery, Bladedance, Hail of Blows, Whirling Dervish |
| Augmented Strength 3 | Mastery of Arms (Friar: Augmented Dexterity 3) |
| Augmented Constitution 3 | Avoid Pain, Armor of Faith, Battle Yell, Second Wind |
| Augmented Acuity 2 | Mastery of Magery, Mastery of Healing, Mastery of the Arcane, Wild Power, Wild Healing, Wild Arcana, Wild Minion, Serenity |
| Augmented Acuity 3 | Mastery of Concentration, Concentration, Mastery of the Art |
| Augmented Quickness 2 | Dodger, Mastery of Stealth |
| First Aid 2 | Ignore Pain |
| Mystic Crystal Lore 2 | Raging Power |
| Serenity 2 | Ethereal Bond |
| Trip | Grapple |
| Longshot | Volley |
| Character level 40 | Striking the Soul |

Effective totals: Aug Dex 2 = 4 points, Aug Dex 3 / Con 3 / Str 3 / Acu 3 =
10, Aug Acu 2 = 4, MCL 2 = 9, First Aid 2 = 9, so **Avoid Pain 1 = 13** and
**Mastery of Concentration = 10 + its own cost**.

## Hibernia

### Druid -- primary healer / group sustain (Regrowth main, Nurture secondary)

1.  Group Purge
2.  Purge
3.  Tireless 1
4.  Mystic Crystal Lore 2
5.  Augmented Acuity 3
6.  Mastery of Concentration
7.  Battery of Life
8.  Raging Power
9.  Wild Healing 2
10. Mastery of Healing 2
11. Serenity 2
12. Toughness 2
13. Wild Healing 3
14. Mastery of Healing 3
15. Serenity 3
16. Toughness 3
17. Augmented Constitution 2
18. Augmented Dexterity 2
19. Augmented Acuity 4
20. Wild Healing 4
21. Mastery of Healing 4
22. Augmented Constitution 3

### Bard -- control / speed / group sustain

Ameliorating Melodies heals every groupmate except the Bard for 30 s on a
15 min reuse, so rank 1 comes before the power purchase.

1.  Purge
2.  Speed of Sound
3.  Tireless 1
4.  Ameliorating Melodies 1
5.  Mystic Crystal Lore 2
6.  Ameliorating Melodies 3
7.  Raging Power
8.  First Aid 2
9.  Augmented Constitution 2
10. Toughness 2
11. Ignore Pain
12. Augmented Constitution 3
13. Augmented Dexterity 2
14. Toughness 3
15. Augmented Dexterity 3
16. Toughness 4
17. Augmented Constitution 4

### Hero -- peel (default)

Avoid Pain 1 costs 13 points with its gate. It sits after Determination 4,
before Determination 5.

1.  Tireless 1
2.  Purge (fighter)
3.  Determination 3
4.  Determination 4
5.  Augmented Constitution 3
6.  Avoid Pain 1
7.  Augmented Dexterity 2
8.  Determination 5
9.  Augmented Strength 2
10. Toughness 2
11. Mastery of Pain 2
12. Avoid Pain 2
13. Augmented Strength 3
14. Mastery of Pain 3
15. Augmented Dexterity 3
16. Toughness 3
17. First Aid 2
18. Ignore Pain
19. Avoid Pain 3
20. Augmented Constitution 4

### Hero -- assist-train variant

1.  Tireless 1
2.  Purge (fighter)
3.  Determination 3
4.  Determination 4
5.  Augmented Strength 2
6.  Augmented Dexterity 2
7.  Mastery of Pain 2
8.  Determination 5
9.  Augmented Constitution 3
10. Avoid Pain 1
11. Toughness 2
12. Avoid Pain 2
13. Augmented Strength 3
14. Augmented Dexterity 3
15. Mastery of Pain 3
16. Toughness 3
17. Augmented Constitution 4
18. Toughness 4

### Blademaster -- assist (two in the group)

1.  Tireless 1
2.  Purge (fighter)
3.  Determination 3
4.  Augmented Dexterity 2
5.  Mastery of Pain 2
6.  Dualist's Reflexes 2
7.  Determination 4
8.  Augmented Strength 2
9.  Determination 5
10. Mastery of Pain 3
11. Augmented Constitution 2
12. Toughness 2
13. Augmented Constitution 3
14. Augmented Dexterity 3
15. Toughness 3
16. Augmented Strength 3
17. Mastery of Pain 4
18. Augmented Constitution 4
19. Toughness 4

### Champion -- hybrid melee / debuff (not in the current roster)

1.  Tireless 1
2.  Purge
3.  Determination (hybrid) 3
4.  Augmented Strength 2
5.  Augmented Dexterity 2
6.  Mastery of Pain 2
7.  Augmented Constitution 3
8.  Avoid Pain 1
9.  Toughness 2
10. First Aid 2
11. Ignore Pain
12. Avoid Pain 2
13. Augmented Strength 3
14. Mastery of Pain 3
15. Augmented Dexterity 3
16. Toughness 3
17. Avoid Pain 3
18. Augmented Constitution 4

### Warden -- defensive support (not in the current roster)

1.  Tireless 1
2.  Purge
3.  Mystic Crystal Lore 2
4.  First Aid 2
5.  Ignore Pain
6.  Augmented Constitution 3
7.  Armor of Faith 1
8.  Augmented Dexterity 2
9.  Mastery of Parrying 2
10. Toughness 2
11. Armor of Faith 2
12. Mastery of Parrying 3
13. Augmented Dexterity 3
14. Toughness 3
15. Armor of Faith 3
16. Augmented Constitution 4

### Eldritch -- nukes, nearsight, snare nuke

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Augmented Acuity 3
5.  Mastery of Concentration
6.  Raging Power
7.  Mastery of Magery 2
8.  Wild Power 2
9.  The Empty Mind 1
10. Serenity 2
11. Toughness 2
12. Mastery of Magery 3
13. The Empty Mind 2
14. Augmented Constitution 2
15. Augmented Dexterity 2
16. Toughness 3
17. Serenity 3
18. Wild Power 3
19. Augmented Acuity 4
20. Wild Power 4
21. Mastery of Magery 4

### Mentalist -- ranged damage / secondary heal (not in the current roster)

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Augmented Acuity 3
5.  Mastery of Concentration
6.  Raging Power
7.  Mastery of Magery 2
8.  Wild Power 2
9.  The Empty Mind 1
10. Serenity 2
11. Toughness 2
12. Mastery of Magery 3
13. The Empty Mind 2
14. Augmented Constitution 2
15. Augmented Dexterity 2
16. Toughness 3
17. Serenity 3
18. Wild Power 3
19. Augmented Acuity 4
20. Wild Power 4
21. Mastery of Magery 4

### Enchanter -- debuff / damage / Brilliant Aura of Deflection

Position of Brilliant Aura of Deflection is provisional: it is not a 1.65
ability (removed and refunded in 1.65), so its real effect in this build
must be confirmed. Keep it at #4 if it is a group magic-resist or absorb
buff and enemy casters are a large share of the damage.

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Brilliant Aura of Deflection
5.  Augmented Acuity 3
6.  Mastery of Concentration
7.  Raging Power
8.  Mastery of Magery 2
9.  Wild Power 2
10. The Empty Mind 1
11. Serenity 2
12. Toughness 2
13. The Empty Mind 2
14. Augmented Constitution 2
15. Augmented Dexterity 2
16. Toughness 3
17. Serenity 3
18. Wild Power 3
19. Augmented Acuity 4
20. Wild Power 4
21. Mastery of Magery 4

## Albion

### Cleric -- primary healer (Rejuvenation) / buffer (Enhancement)

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Augmented Acuity 3
5.  Mastery of Concentration
6.  Battery of Life
7.  Bunker of Faith
8.  Raging Power
9.  Wild Healing 2
10. Mastery of Healing 2
11. Serenity 2
12. Toughness 2
13. Wild Healing 3
14. Mastery of Healing 3
15. Serenity 3
16. Toughness 3
17. Augmented Constitution 2
18. Augmented Dexterity 2
19. Augmented Acuity 4
20. Wild Healing 4
21. Mastery of Healing 4
22. Augmented Constitution 3

### Minstrel -- speed / cure mez / area mez / shouts

1.  Tireless 1
2.  Purge
3.  Speed of Sound
4.  Mystic Crystal Lore 2
5.  First Aid 2
6.  Augmented Constitution 2
7.  Augmented Dexterity 2
8.  Ignore Pain
9.  Toughness 2
10. Mastery of Pain 2
11. Augmented Constitution 3
12. Toughness 3
13. Mastery of Pain 3
14. Augmented Dexterity 3
15. Toughness 4
16. Augmented Constitution 4
17. Mastery of Pain 4

### Paladin -- peel (shield)

1.  Tireless 1
2.  Purge
3.  Determination (hybrid) 3
4.  Faith Healing
5.  Augmented Constitution 3
6.  Avoid Pain 1
7.  Augmented Strength 2
8.  Toughness 2
9.  First Aid 2
10. Ignore Pain
11. Avoid Pain 2
12. Augmented Strength 3
13. Augmented Dexterity 2
14. Toughness 3
15. Avoid Pain 3
16. Augmented Constitution 4
17. Augmented Strength 4
18. Toughness 4

### Armsman -- assist (polearm on the kill target, shield out only to Slam)

1.  Tireless 1
2.  Purge (fighter)
3.  Determination 3
4.  Soldier's Barricade
5.  Augmented Strength 2
6.  Augmented Dexterity 2
7.  Mastery of Pain 2
8.  Determination 4
9.  Augmented Constitution 3
10. Avoid Pain 1
11. Determination 5
12. Toughness 2
13. First Aid 2
14. Ignore Pain
15. Avoid Pain 2
16. Augmented Strength 3
17. Augmented Dexterity 3
18. Toughness 3
19. Mastery of Pain 3
20. Avoid Pain 3

### Mercenary -- assist (not in the current roster)

1.  Tireless 1
2.  Purge (fighter)
3.  Determination 3
4.  Augmented Dexterity 2
5.  Mastery of Pain 2
6.  Dualist's Reflexes 2
7.  Determination 4
8.  Augmented Strength 2
9.  Determination 5
10. Mastery of Pain 3
11. Augmented Constitution 2
12. Toughness 2
13. First Aid 2
14. Ignore Pain
15. Augmented Constitution 3
16. Augmented Dexterity 3
17. Toughness 3
18. Augmented Strength 3
19. Mastery of Pain 4
20. Augmented Constitution 4
21. Toughness 4

### Sorcerer -- main crowd control

An interrupted mez is a lost fight, so Aug Acuity 3 and Mastery of
Concentration come before Mystic Crystal Lore 2.

1.  Purge
2.  Tireless 1
3.  Augmented Acuity 3
4.  Mastery of Concentration
5.  Mystic Crystal Lore 2
6.  Raging Power
7.  The Empty Mind 2
8.  Serenity 2
9.  Toughness 2
10. The Empty Mind 3
11. Augmented Constitution 2
12. Augmented Dexterity 2
13. Toughness 3
14. Serenity 3
15. Augmented Acuity 4
16. Augmented Constitution 3
17. Augmented Dexterity 3
18. Toughness 4

### Theurgist -- interrupts / pets

1.  Purge
2.  Tireless 1
3.  Augmented Acuity 3
4.  Mastery of Concentration
5.  Mystic Crystal Lore 2
6.  Raging Power
7.  The Empty Mind 2
8.  Serenity 2
9.  Toughness 2
10. The Empty Mind 3
11. Augmented Constitution 2
12. Augmented Dexterity 2
13. Toughness 3
14. Serenity 3
15. Augmented Acuity 4
16. Augmented Constitution 3
17. Augmented Dexterity 3
18. Toughness 4

### Cabalist -- debuff / disease / lifedrain

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Augmented Acuity 3
5.  Mastery of Concentration
6.  Raging Power
7.  The Empty Mind 1
8.  Mastery of Magery 2
9.  Wild Power 2
10. Serenity 2
11. Toughness 2
12. Mastery of Magery 3
13. The Empty Mind 2
14. Augmented Constitution 2
15. Augmented Dexterity 2
16. Toughness 3
17. Serenity 3
18. Wild Power 3
19. Augmented Acuity 4
20. Wild Power 4
21. Mastery of Magery 4

### Wizard -- ranged damage (not in the current roster)

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Augmented Acuity 3
5.  Mastery of Concentration
6.  Raging Power
7.  Mastery of Magery 2
8.  Wild Power 2
9.  The Empty Mind 1
10. Serenity 2
11. Toughness 2
12. Mastery of Magery 3
13. The Empty Mind 2
14. Augmented Constitution 2
15. Augmented Dexterity 2
16. Toughness 3
17. Serenity 3
18. Wild Power 3
19. Augmented Acuity 4
20. Wild Power 4
21. Mastery of Magery 4

## Midgard

### Healer -- main healer (Mending) / control-leaning (Pacification)

Perfect Recovery is an instant res with no res effects, so it comes right
after Mystic Crystal Lore 2.

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Perfect Recovery
5.  Augmented Acuity 3
6.  Mastery of Concentration
7.  Battery of Life
8.  Raging Power
9.  Wild Healing 2
10. Mastery of Healing 2
11. Serenity 2
12. Toughness 2
13. Wild Healing 3
14. Mastery of Healing 3
15. Serenity 3
16. Toughness 3
17. Augmented Constitution 2
18. Augmented Dexterity 2
19. Augmented Acuity 4
20. Wild Healing 4
21. Mastery of Healing 4
22. Augmented Constitution 3

### Shaman -- buffer / disease / secondary heal

First Aid 2 and Ignore Pain stay early here: the Shaman spams point-blank
disease inside melee range.

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Augmented Acuity 3
5.  Mastery of Concentration
6.  Raging Power
7.  First Aid 2
8.  Ignore Pain
9.  Wild Healing 2
10. Mastery of Healing 2
11. Serenity 2
12. Toughness 2
13. Wild Healing 3
14. Mastery of Healing 3
15. Serenity 3
16. Toughness 3
17. Augmented Constitution 2
18. Augmented Dexterity 2
19. Augmented Acuity 4
20. Wild Healing 4
21. Mastery of Healing 4
22. Augmented Constitution 3

### Skald -- speed / shouts / mez / melee

If the access file gives Skald Mystic Crystal Lore, add it around #4 (mez
and shouts cost power). Raise Augmented Strength to 3 only if Mastery of
Arms is available.

1.  Tireless 1
2.  Purge
3.  Speed of Sound
4.  Augmented Dexterity 2
5.  Augmented Strength 2
6.  Mastery of Pain 2
7.  Augmented Constitution 2
8.  Toughness 2
9.  First Aid 2
10. Ignore Pain
11. Augmented Strength 3
12. Augmented Dexterity 3
13. Augmented Constitution 3
14. Mastery of Pain 3
15. Toughness 3
16. Toughness 4
17. Augmented Constitution 4
18. Mastery of Pain 4
19. Augmented Strength 4

### Warrior -- peel (hammer and shield)

1.  Tireless 1
2.  Purge (fighter)
3.  Determination 3
4.  Determination 4
5.  Augmented Constitution 3
6.  Avoid Pain 1
7.  Augmented Dexterity 2
8.  Determination 5
9.  Augmented Strength 2
10. Mastery of Pain 2
11. Toughness 2
12. First Aid 2
13. Ignore Pain
14. Avoid Pain 2
15. Augmented Strength 3
16. Mastery of Pain 3
17. Augmented Dexterity 3
18. Toughness 3
19. Avoid Pain 3
20. Augmented Constitution 4

### Berserker -- assist

1.  Tireless 1
2.  Purge (fighter)
3.  Determination 3
4.  Augmented Dexterity 2
5.  Mastery of Pain 2
6.  Dualist's Reflexes 2
7.  Determination 4
8.  Augmented Strength 2
9.  Determination 5
10. Mastery of Pain 3
11. Augmented Constitution 2
12. Toughness 2
13. First Aid 2
14. Ignore Pain
15. Augmented Constitution 3
16. Augmented Dexterity 3
17. Toughness 3
18. Augmented Strength 3
19. Mastery of Pain 4
20. Augmented Constitution 4
21. Toughness 4

### Savage -- assist

1.  Tireless 1
2.  Purge (fighter)
3.  Determination 3
4.  Augmented Dexterity 2
5.  Mastery of Pain 2
6.  Dualist's Reflexes 2
7.  Determination 4
8.  Augmented Strength 2
9.  Determination 5
10. Mastery of Pain 3
11. Augmented Constitution 2
12. Toughness 2
13. First Aid 2
14. Ignore Pain
15. Augmented Constitution 3
16. Augmented Dexterity 3
17. Toughness 3
18. Augmented Strength 3
19. Mastery of Pain 4
20. Augmented Constitution 4
21. Toughness 4

### Runemaster -- nukes / nearsight / area damage

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Augmented Acuity 3
5.  Mastery of Concentration
6.  Raging Power
7.  The Empty Mind 1
8.  Mastery of Magery 2
9.  Wild Power 2
10. Serenity 2
11. Toughness 2
12. Mastery of Magery 3
13. The Empty Mind 2
14. Augmented Constitution 2
15. Augmented Dexterity 2
16. Toughness 3
17. Serenity 3
18. Wild Power 3
19. Augmented Acuity 4
20. Wild Power 4
21. Mastery of Magery 4

### Spiritmaster -- utility / debuff / pets (not in the current roster)

1.  Purge
2.  Tireless 1
3.  Mystic Crystal Lore 2
4.  Augmented Acuity 3
5.  Mastery of Concentration
6.  Raging Power
7.  The Empty Mind 1
8.  Mastery of Magery 2
9.  Wild Power 2
10. Serenity 2
11. Toughness 2
12. Mastery of Magery 3
13. The Empty Mind 2
14. Augmented Constitution 2
15. Augmented Dexterity 2
16. Toughness 3
17. Serenity 3
18. Wild Power 3
19. Augmented Acuity 4
20. Wild Power 4
21. Mastery of Magery 4

## Open items

- Confirm what Brilliant Aura of Deflection, Bunker of Faith and Battery of
  Life do in this build; their positions are not verified.
- Mastery of Blocking: if available to Hero, Warrior or Paladin, it fits
  the shield peels and needs only Augmented Dexterity 2.
- Aug Dex 3 unlocks Bladedance, Hail of Blows and Whirling Dervish; Aug
  Con 3 also unlocks Battle Yell and Second Wind. Check class access before
  adding any of them.
- Active RAs with three levels have no target level in most lists. Add one
  per entry once you decide how far each is worth taking.

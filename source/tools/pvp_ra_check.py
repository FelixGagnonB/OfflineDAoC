"""Checks a written realm ability progression against the server, and works out what a bot would hold with it.

    python pvp_ra_check.py <progressions.md> [game database]

The file has one section a class ("### Druid -- ...") with a numbered list of steps ("4.  Mystic Crystal Lore 2").
For each list: every step is looked up in the class's own ability list (the game database, read only), its level
against the ability's levels and its need against the server's prerequisites (the ability handlers); then the list
is bought as an 8v8 bot buys it (AutonomousBotRealmAbilities.Plan: from the top, each step raised as far as the
points go when what it needs is owned, and worked out afresh from all the points at each rank), which gives the
rank at which each step is really held. Nothing is written.
"""
import glob
import os
import re
import sqlite3
import sys

SERVER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "server", "GameServer")
DB = "file:" + (sys.argv[2].replace("\\", "/") if len(sys.argv) > 2 else "D:/OfflineDAoC-scrim/runtime/data/opendaoc.sqlite3.db") + "?mode=ro"

# ---- the handlers: cost and levels of every ability
classes = {}
for path in glob.glob(os.path.join(SERVER, "realmabilities*", "**", "*.cs"), recursive=True):
    text = open(path, encoding="utf-8-sig", errors="replace").read()
    found = list(re.finditer(r"^[ \t]*(?:public|internal|private)?[ \t]*(?:sealed[ \t]+|abstract[ \t]+|static[ \t]+)*class[ \t]+(\w+)[ \t]*(?::[ \t]*([\w.]+))?", text, re.M))
    for i, m in enumerate(found):
        body = text[m.end(): found[i + 1].start() if i + 1 < len(found) else len(text)]
        classes.setdefault(m.group(1).lower(), ((m.group(2) or "").split(".")[-1].lower(), body))


def chain(name):
    name, seen = name.lower(), []
    while name in classes and name not in seen:
        seen.append(name)
        name = classes[name][0]
    return seen


def find(name, pattern):
    for cls in chain(name):
        m = re.search(pattern, classes[cls][1], re.S)
        if m:
            return m
    return None


def costs(impl):
    m = find(impl, r"int\s+CostForUpgrade\s*\(\s*int\s+\w+\s*\)\s*(\{.*?\n\s{0,12}\}|\{[^\n]*\}|=>[^;]*;)")
    if not m:
        return []
    body = m.group(1)
    if "For5LevelsRA" in body:
        return [1, 3, 6, 10, 14]
    if "For3LevelsRA" in body:
        return [3, 6, 10]
    cases = re.findall(r"case\s+(\d+)\s*:\s*return\s+(\d+)\s*;", body)
    if cases:
        return [int(v) for _, v in sorted((int(k), v) for k, v in cases)]
    single = re.search(r"return\s+(\d+)\s*;|=>\s*(\d+)\s*;", body)
    return [int(single.group(1) or single.group(2))] if single else []


def levels(impl):
    c = costs(impl)
    m = find(impl, r"int\s+MaxLevel\s*(?:\{\s*get\s*\{\s*return\s+(\d+)\s*;|=>\s*(\d+)\s*;)")
    top = int(m.group(1) or m.group(2)) if m else 0
    if len(c) > 1:
        return c[:top] if 0 < top < len(c) else c
    return c * (top or 1)


# ---- what an ability needs (the handlers' CheckRequirement, as AutonomousBotRealmAbilities.Requirement has them)
A = "AtlasOF_"
REQ = {}
for keys, need in (
        (("MasteryOfPain", "MasteryOfParrying", "MasteryOfBlocking", "DualistsReflexes", "FalconsEye"), ("AugDex", 2)),
        (("MasteryOfArchery", "HailOfBlows", "Bladedance", "WhirlingDervish"), ("AugDex", 3)),
        (("MasteryOfHealing", "MasteryOfMagery", "MasteryOfTheArcane", "WildPower", "WildHealing", "WildArcana", "WildMinion", "Serenity"), ("AugAcuity", 2)),
        (("MasteryOfTheArt", "MasteryOfConcentration", "Concentration"), ("AugAcuity", 3)),
        (("MasteryOfArms",), ("AugStr", 3)), (("Dodger", "MasteryOfStealth"), ("AugQui", 2)),
        (("SecondWind", "ArmorOfFaith", "AvoidPain", "BattleYell"), ("AugCon", 3)),
        (("IgnorePain", "IgnorePainTank"), ("FirstAid", 2)), (("RagingPower",), ("MCL", 2)),
        (("EtherealBond",), ("Serenity", 2)), (("Grapple",), ("Trip", 1)), (("Volley",), ("Longshot", 1))):
    for key in keys:
        REQ[A + key] = (A + need[0], need[1])

# ---- the actives a bot presses (AutonomousBotRealmAbilities.UsableActives, PvpRealmAbilities.Actives): any other active is never bought
PRESSED = {A + k for k in ("Purge", "PurgeReduced", "IgnorePainTank", "IgnorePain", "SecondWind", "FirstAid", "GroupPurge", "SpeedOfSound",
                           "PerfectRecovery", "FaithHealing", "BatteryOfLife", "BunkerOfFaith", "SoldiersBarricade", "BrilliantAura",
                           "AmelioratingMelodies", "MasteryOfConcentration", "AvoidPain", "ArmorOfFaith", "EmptyMind", "MCL", "RagingPower")}

CLASS_ID = {"Cleric": 6, "Sorcerer": 8, "Minstrel": 4, "Theurgist": 5, "Paladin": 1, "Armsman": 2, "Cabalist": 13, "Mercenary": 11, "Wizard": 7,
            "Friar": 10, "Reaver": 19, "Healer": 26, "Shaman": 28, "Skald": 24, "Runemaster": 29, "Savage": 32, "Berserker": 31, "Warrior": 22,
            "Spiritmaster": 27, "Thane": 21, "Bonedancer": 30, "Druid": 47, "Bard": 48, "Hero": 44, "Blademaster": 43, "Eldritch": 40,
            "Enchanter": 41, "Warden": 46, "Champion": 45, "Mentalist": 42, "Valewalker": 56, "Animist": 55}

db = sqlite3.connect(DB, uri=True)
ABILITY = {key: (name.strip(), (impl or "").split(".")[-1]) for key, name, impl in db.execute("select KeyName, Name, Implementation from Ability")}


def norm(name):
    return re.sub(r"[^a-z]", "", name.lower().replace("mystic crystal lore", "mystic crystal lore"))


def rank(points):
    return f"{points // 10 + 1}L{points % 10}"


def plan(steps, points):
    """As AutonomousBotRealmAbilities.Plan buys the steps that come before its build."""
    owned = {}
    for key, target, ladder in steps:
        need = REQ.get(key)
        if need and owned.get(need[0], 0) < need[1]:
            continue
        while owned.get(key, 0) < target:
            price = ladder[owned.get(key, 0)]
            if price > points:
                break
            points -= price
            owned[key] = owned.get(key, 0) + 1
    return owned, points


text = open(sys.argv[1], encoding="utf-8").read()
sections = re.split(r"^### ", text, flags=re.M)[1:]
for section in sections:
    title, _, body = section.partition("\n")
    label = title.split("--")[0].strip()
    if label not in CLASS_ID:
        continue
    available = {}
    for (key,) in db.execute("select AbilityKey from ClassXRealmAbility_Atlas where CharClass=?", (CLASS_ID[label],)):
        if key in ABILITY:
            available[key] = ABILITY[key]
    # The fork gives Speed of Sound to the Bard and the Skald in code, not in the table (gameutils/SkillBase.cs).
    if label in ("Bard", "Skald") and A + "SpeedOfSound" in ABILITY:
        available[A + "SpeedOfSound"] = ABILITY[A + "SpeedOfSound"]
    by_name = {}
    for key, (name, impl) in available.items():
        by_name.setdefault(norm(name), []).append(key)
    problems, steps, listed = [], [], []
    for number, raw in re.findall(r"^(\d+)\.\s+(.+?)\s*$", body, re.M):
        if re.match(r"^## ", raw):
            break
        tag = re.search(r"\((fighter|hybrid)\)", raw)
        name = re.sub(r"\((fighter|hybrid)\)", "", raw).strip()
        m = re.match(r"^(.*?)(?:\s+(\d+))?$", name)
        name, target = m.group(1).strip(), int(m.group(2) or 1)
        keys = by_name.get(norm(name))
        if not keys:
            problems.append(f"#{number} {raw}: the {label} cannot train it (not in its class list)")
            continue
        key = keys[0]
        if len(keys) > 1:
            # Two spellings of one ability: the class's cheaper Purge or Ignore Pain, the hybrid Determination.
            key = sorted(keys, key=lambda k: sum(levels(available[k][1])))[0]
        if tag and tag.group(1) == "fighter" and key != A + "PurgeReduced":
            problems.append(f"#{number} {raw}: the {label} has the Purge at {levels(available[key][1])[0]} points, not the fighters' at 4")
        if tag and tag.group(1) == "hybrid" and key != A + "DeterminationHybrid":
            problems.append(f"#{number} {raw}: the {label} has no hybrid Determination ({key})")
        ladder = levels(available[key][1])
        if not ladder:
            problems.append(f"#{number} {raw}: its cost could not be read")
            continue
        if target > len(ladder):
            problems.append(f"#{number} {raw}: it has {len(ladder)} level{'s' if len(ladder) > 1 else ''} here (cost {', '.join(map(str, ladder))}); level {target} does not exist")
            target = len(ladder)
        timed = any("timed" in c for c in chain(available[key][1]))
        if timed and key not in PRESSED:
            problems.append(f"#{number} {raw}: an ability to press that the bots have no rule for; a bot never buys it")
        steps.append((key, target, ladder))
        listed.append((number, raw, key, target))
    if not steps:
        continue
    # What each step needs, and whether the list ever has it.
    final = {}
    for key, target, ladder in steps:
        final[key] = max(final.get(key, 0), target)
    for number, raw, key, target in listed:
        need = REQ.get(key)
        if need and final.get(need[0], 0) < need[1]:
            problems.append(f"#{number} {raw}: needs {ABILITY[need[0]][0]} {need[1]}, which the list never reaches; it is never bought")
    total = sum(sum(steps_ladder[:final[key]]) for key, steps_ladder in {k: l for k, _, l in steps}.items())
    print(f"\n=== {title.strip()}")
    print(f"    {len(steps)} steps, {total} points in all" + (f": {total - 130} more than the cap gives" if total > 130 else f"; {130 - total} left at the cap" if total < 130 else ": exactly the cap"))
    # When each step is really held.
    # (Held for good: a bot's purchases are worked out afresh at each rank, so a cheap step bought with small change
    # can be gone again at the next rank, when a dearer step above it is in reach.)
    held_at = {}
    for points in range(130, 0, -1):
        owned, _ = plan(steps, points)
        for number, raw, key, target in listed:
            if owned.get(key, 0) >= target:
                if held_at.get(number) == points + 1 or points == 130:
                    held_at[number] = points
            elif number in held_at and held_at[number] <= points:
                del held_at[number]
    order = sorted(listed, key=lambda s: (held_at.get(s[0], 999), int(s[0])))
    print("    held for good from (as a bot buys): " + "; ".join(f"{raw} {rank(held_at[n]) if n in held_at else 'never'}" for n, raw, _, _ in order))
    out_of_order = [(n, raw) for i, (n, raw, _, _) in enumerate(order) if any(int(m) < int(n) and held_at.get(m, 999) > held_at.get(n, 999) for m, _, _, _ in listed)]
    for points in (20, 40, 70, 100):
        owned, left = plan(steps, points)
        print(f"    at {rank(points)} ({points}): " + ", ".join(f"{ABILITY[k][0]}{' ' + str(v) if len(dict((kk, l) for kk, _, l in steps)[k]) > 1 else ''}" for k, v in owned.items()) + (f"; {left} unspent" if left else ""))
    for p in problems:
        print("    !! " + p)

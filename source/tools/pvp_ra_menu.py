"""What each 8v8 class can train: every realm ability its class list has, with levels, costs and what it needs.
Read from the server's handlers (cost, levels) and the game database (which class has what)."""
import glob
import re
import sqlite3
import sys

import os
SERVER = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "server", "GameServer")
# python pvp_ra_menu.py <out.md> [game database]   (the database is only read)
DB = "file:" + (sys.argv[2].replace("\\", "/") if len(sys.argv) > 2 else "D:/OfflineDAoC-scrim/runtime/data/opendaoc.sqlite3.db") + "?mode=ro"

classes = {}   # name -> (base, body)
for path in glob.glob(SERVER + r"\realmabilities*\**\*.cs", recursive=True):
    text = open(path, encoding="utf-8-sig", errors="replace").read()
    found = list(re.finditer(r"^[ 	]*(?:public|internal|private)?[ 	]*(?:sealed[ 	]+|abstract[ 	]+|static[ 	]+)*class[ 	]+(\w+)[ 	]*(?::[ 	]*([\w.]+))?", text, re.M))
    for i, m in enumerate(found):
        body = text[m.end(): found[i + 1].start() if i + 1 < len(found) else len(text)]
        classes.setdefault(m.group(1).lower(), ((m.group(2) or "").split(".")[-1].lower(), body))


def chain(name):
    name = name.lower()
    seen = []
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


FIVE, THREE = [1, 3, 6, 10, 14], [3, 6, 10]


def costs(name):
    m = find(name, r"int\s+CostForUpgrade\s*\(\s*int\s+\w+\s*\)\s*(\{.*?\n\s{0,12}\}|\{[^\n]*\}|=>[^;]*;)")
    if not m:
        return None
    body = m.group(1)
    if "For5LevelsRA" in body:
        return FIVE
    if "For3LevelsRA" in body:
        return THREE
    cases = re.findall(r"case\s+(\d+)\s*:\s*return\s+(\d+)\s*;", body)
    if cases:
        return [int(v) for _, v in sorted((int(k), v) for k, v in cases)]
    single = re.search(r"return\s+(\d+)\s*;|=>\s*(\d+)\s*;", body)
    if single:
        return [int(single.group(1) or single.group(2))]
    return None


def max_level(name):
    m = find(name, r"int\s+MaxLevel\s*(?:\{\s*get\s*\{\s*return\s+(\d+)\s*;|=>\s*(\d+)\s*;)")
    return int(m.group(1) or m.group(2)) if m else None


NEEDS = {
    "AtlasOF_MasteryOfPain": "Aug Dex 2", "AtlasOF_MasteryOfParrying": "Aug Dex 2", "AtlasOF_MasteryOfBlocking": "Aug Dex 2",
    "AtlasOF_DualistsReflexes": "Aug Dex 2", "AtlasOF_FalconsEye": "Aug Dex 2",
    "AtlasOF_MasteryOfArchery": "Aug Dex 3", "AtlasOF_HailOfBlows": "Aug Dex 3", "AtlasOF_Bladedance": "Aug Dex 3", "AtlasOF_WhirlingDervish": "Aug Dex 3",
    "AtlasOF_MasteryOfHealing": "Aug Acuity 2", "AtlasOF_MasteryOfMagery": "Aug Acuity 2", "AtlasOF_MasteryOfTheArcane": "Aug Acuity 2",
    "AtlasOF_WildPower": "Aug Acuity 2", "AtlasOF_WildHealing": "Aug Acuity 2", "AtlasOF_WildArcana": "Aug Acuity 2", "AtlasOF_WildMinion": "Aug Acuity 2",
    "AtlasOF_Serenity": "Aug Acuity 2",
    "AtlasOF_MasteryOfTheArt": "Aug Acuity 3", "AtlasOF_MasteryOfConcentration": "Aug Acuity 3", "AtlasOF_Concentration": "Aug Acuity 3",
    "AtlasOF_MasteryOfArms": "Aug Str 3", "AtlasOF_Dodger": "Aug Qui 2", "AtlasOF_MasteryOfStealth": "Aug Qui 2",
    "AtlasOF_SecondWind": "Aug Con 3", "AtlasOF_ArmorOfFaith": "Aug Con 3", "AtlasOF_AvoidPain": "Aug Con 3", "AtlasOF_BattleYell": "Aug Con 3",
    "AtlasOF_IgnorePain": "First Aid 2", "AtlasOF_IgnorePainTank": "First Aid 2", "AtlasOF_RagingPower": "Mystic Crystal Lore 2",
    "AtlasOF_EtherealBond": "Serenity 2", "AtlasOF_Grapple": "Trip", "AtlasOF_Volley": "Longshot",
}

# The classes of the 8v8 catalogue, a realm at a time.
REALMS = [
    ("Albion", [(6, "Cleric"), (8, "Sorcerer"), (4, "Minstrel"), (5, "Theurgist"), (1, "Paladin"), (2, "Armsman"), (13, "Cabalist"), (11, "Mercenary"), (7, "Wizard")]),
    ("Midgard", [(26, "Healer"), (28, "Shaman"), (24, "Skald"), (29, "Runemaster"), (32, "Savage"), (31, "Berserker"), (22, "Warrior"), (27, "Spiritmaster")]),
    ("Hibernia", [(47, "Druid"), (48, "Bard"), (44, "Hero"), (43, "Blademaster"), (40, "Eldritch"), (41, "Enchanter"), (46, "Warden"), (45, "Champion"), (42, "Mentalist")]),
]

db = sqlite3.connect(DB, uri=True)
ability = {key: (name.strip(), (impl or "").split(".")[-1]) for key, name, impl in db.execute("select KeyName, Name, Implementation from Ability")}
out = ["# 8v8: what each class can train",
       "",
       "Every realm ability each class of the 8v8 groups may buy on this server, with its levels, what each",
       "level costs and what it needs first. Made by `source/tools/pvp_ra_menu.py` from the server's ability handlers and the",
       "class lists in the game database; nothing here is a choice of the fork's. It is the menu the orders in",
       "[PVP-8V8-REALM-ABILITIES.md](PVP-8V8-REALM-ABILITIES.md) are picked from.",
       "",
       "- One point for each realm level: 10 at rank 2, 40 at rank 5, 70 at rank 8, 90 at rank 10, 130 at the",
       "  cap (rank 14 level 0).",
       "- **Costs** are for each level in turn; **All** is what every level together costs.",
       "- **Kind**: *use* is an ability that is pressed and has a reuse timer; the others are always on.",
       "- A class has either Purge at 10 points or the fighters' at 4, and one Ignore Pain; both spellings",
       "  are listed where the class list has both.",
       ""]
problems = []
for realm, members in REALMS:
    out += [f"## {realm}", ""]
    for class_id, label in members:
        keys = [r[0] for r in db.execute("select AbilityKey from ClassXRealmAbility_Atlas where CharClass=?", (class_id,))]
        # The fork gives Speed of Sound to the Bard and the Skald in code, not in the table (gameutils/SkillBase.cs).
        if label in ("Bard", "Skald") and "AtlasOF_SpeedOfSound" not in keys:
            keys.append("AtlasOF_SpeedOfSound")
        rows = []
        for key in keys:
            name, impl = ability.get(key, (key, ""))
            c = costs(impl)
            top = max_level(impl)
            if c is None:
                problems.append((label, key, impl, "no cost"))
                c = []
            if len(c) > 1:
                top = len(c) if not top or top > len(c) else top
            else:
                top = top or 1
                c = c * top
            c = c[:top]
            timed = any("timed" in x for x in chain(impl))
            rows.append((sum(c) if c else 0, name, top, c, NEEDS.get(key, "Aug Dex 3" if key == "AtlasOF_MasteryOfArms" and label == "Friar" else NEEDS.get(key, "")), "use" if timed else ""))
        rows.sort(key=lambda r: (r[5] != "use", r[1]))
        out += [f"### {label}", "", "| Ability | Kind | Levels | Costs | All | Needs |", "|---|---|---|---|---|---|"]
        for total, name, top, c, needs, kind in rows:
            out.append(f"| {name} | {kind} | {top} | {', '.join(map(str, c)) if c else '?'} | {total if c else '?'} | {needs} |")
        out += ["", f"*Everything on this list: {sum(r[0] for r in rows)} points.*", ""]
open(sys.argv[1], "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
print("written", sys.argv[1], "problems:", problems[:20], len(problems))

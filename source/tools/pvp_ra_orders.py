"""Puts a written realm ability progression into the game, and writes the page that shows it.

    python pvp_ra_orders.py <progressions.md> [game database]

The progression file (docs/DAoC-1.65-8v8-RA-Progressions-v3.md) is the one place the orders are written: one
section a class, a numbered list of steps. This tool checks it (pvp_ra_check.py), writes each class's list between
the "<orders>" marks of source/server/GameServer/bots/pvp/PvpRealmAbilities.cs, and writes
docs/PVP-8V8-REALM-ABILITIES.md from the same lists. A class with two sections (the Hero) has its first one put in
the game; the others are shown on the page as variants. Build and test afterwards.
"""
import os
import re
import sys

import pvp_ra_check as check

HERE = os.path.dirname(os.path.abspath(__file__))
CODE = os.path.join(HERE, "..", "server", "GameServer", "bots", "pvp", "PvpRealmAbilities.cs")
PAGE = os.path.join(HERE, "..", "..", "docs", "PVP-8V8-REALM-ABILITIES.md")
REALM = {"Albion": ["Cleric", "Sorcerer", "Minstrel", "Theurgist", "Paladin", "Armsman", "Cabalist", "Mercenary", "Wizard", "Friar", "Reaver"],
         "Midgard": ["Healer", "Shaman", "Skald", "Runemaster", "Savage", "Berserker", "Warrior", "Spiritmaster", "Thane", "Bonedancer"],
         "Hibernia": ["Druid", "Bard", "Hero", "Blademaster", "Eldritch", "Enchanter", "Warden", "Champion", "Mentalist", "Valewalker", "Animist"]}


def main():
    source = sys.argv[1]
    lists = list(check.read(source, sys.argv[2] if len(sys.argv) > 2 else None))
    name = os.path.basename(source)

    # ---- the game
    seen, rows = set(), []
    for title, label, steps, listed, problems, total, held_at in lists:
        if label in seen:
            continue
        seen.add(label)
        owned, parts = {}, []
        for key, target, ladder in steps:
            if target <= owned.get(key, 0):
                continue   # a level the ability does not have, written twice
            owned[key] = target
            parts.append(f'("{key[len(check.A):]}", {target})')
        lines, line = [], f"            [eCharacterClass.{label}] = Order("
        for i, part in enumerate(parts):
            piece = part + (", " if i + 1 < len(parts) else "),")
            if len(line) + len(piece) > 150:
                lines.append(line.rstrip())
                line = "                "
            line += piece
        lines.append(line)
        rows.append(f"            // {title}\n" + "\n".join(lines))
    block = ("        // <orders> Written by source/tools/pvp_ra_orders.py from docs/" + name + ": the owner's lists. Change them\n"
             "        // there and run the tool; what is between these two marks is written over.\n"
             "        private static readonly Dictionary<eCharacterClass, Step[]> Orders = new()\n        {\n" + "\n".join(rows) + "\n        };\n        // </orders>")
    data = open(CODE, "rb").read()
    bom = data[:3] == b"\xef\xbb\xbf"
    raw = data.decode("utf-8-sig")
    crlf = "\r\n" in raw
    text = raw.replace("\r\n", "\n")
    start, end = text.index("        // <orders>"), text.index("// </orders>") + len("// </orders>")
    text = text[:start] + block + text[end:]
    open(CODE, "wb").write((b"\xef\xbb\xbf" if bom else b"") + (text.replace("\n", "\r\n") if crlf else text).encode("utf-8"))

    # ---- the page
    out = ["# 8v8: what each class buys with its realm ability points",
           "",
           f"The order each class of 8v8 bot spends its realm ability points in. The orders are the owner's, written in",
           f"[{name}]({name}); `source/tools/pvp_ra_orders.py` puts them into the game",
           "(`source/server/GameServer/bots/pvp/PvpRealmAbilities.cs`) and writes this page. Change an order there and run the",
           "tool; `source/tools/pvp_ra_check.py` checks the file without changing anything. What each class may train at all is",
           "in [PVP-8V8-REALM-ABILITY-MENU.md](PVP-8V8-REALM-ABILITY-MENU.md).",
           "",
           "## How to read it",
           "",
           "- A character has one point for each realm level: 10 at rank 2, 40 at rank 5, 70 at rank 8, 90 at rank 10, 130 at",
           "  the cap (rank 14 level 0).",
           "- **Cost** is what the step costs on top of the levels the list already has; **Total** is the list up to and with",
           "  that step.",
           "- **Held from** is the rank from which a bot has the step for good. A bot does not save up: it goes down its",
           "  list and raises each step as far as its points go, and the next steps are bought with what is left. Its",
           "  purchases are worked out afresh from all its points at each rank, as if a respec were free, so a cheap step",
           "  low in the list can be held for a few levels, given up for a dearer one above it, and bought back later.",
           "- A step past the 130 points of the cap is never bought (*never*). A list that ends short of the cap is followed",
           "  by the build the gamebots use for the class's kind (fighter, caster, healer), cheapest next level first.",
           "- When a bot presses each ability it has bought is in [PVP-8V8-GUIDE.md](PVP-8V8-GUIDE.md), *Realm abilities*.",
           ""]
    by_label = {}
    for entry in lists:
        by_label.setdefault(entry[1], []).append(entry)
    for realm, members in REALM.items():
        mine = [label for label in members if label in by_label]
        if not mine:
            continue
        out += [f"## {realm}", ""]
        for label in mine:
            for index, (title, _, steps, listed, problems, total, held_at) in enumerate(by_label[label]):
                out += [f"### {title}" + (" *(written, not in the game: the first list of the class is)*" if index else ""), "",
                        "| # | Step | Cost | Total | Held from |", "|---|---|---|---|---|"]
                owned, running, ladders = {}, 0, {k: l for k, _, l in steps}
                for number, raw, key, target in listed:
                    cost = sum(ladders[key][owned.get(key, 0):target])
                    owned[key] = max(owned.get(key, 0), target)
                    running += cost
                    out.append(f"| {number} | {raw} | {cost} | {running} | {check.rank(held_at[number]) if number in held_at else 'never'} |")
                out.append("")
                for points in (40, 70, 100):
                    have, left = check.plan(steps, points)
                    out.append(f"*At rank {points // 10 + 1} ({points} points):* " +
                               ", ".join(f"{check.ABILITY[k][0]}{' ' + str(v) if len(ladders[k]) > 1 else ''}" for k, v in have.items()) +
                               (f"; {left} to spare." if left else "."))
                    out.append("")
                out.append(f"*The list costs {total} points" + (f": {total - 130} more than the cap gives.*" if total > 130 else f"; {130 - total} are left at the cap for the kind's build.*" if total < 130 else ", the cap exactly.*"))
                for problem in problems:
                    out.append("")
                    out.append(f"*Not as written:* {problem}.")
                out.append("")
    open(PAGE, "w", encoding="utf-8", newline="\n").write("\n".join(out))
    print(f"{len(seen)} classes written to PvpRealmAbilities.cs; {len(lists)} lists on the page.")
    for title, label, steps, listed, problems, total, held_at in lists:
        for problem in problems:
            print(f"  {label}: {problem}")


if __name__ == "__main__":
    main()

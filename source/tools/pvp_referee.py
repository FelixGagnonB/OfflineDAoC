"""Where every bot's time went, and whether a group did what a competent group does.

    python pvp_referee.py <scrims folder> [--batch prefix] [--since prefix] [--until prefix] [--out file.txt]

Two reports from recorded 8v8 matches, made so that nobody has to watch fights to find what is
wrong (docs/PVP-8V8-ROADMAP.md, "Current focus", 2026-11-18).

1. The time budget. For each build, what it spent its living seconds on after first contact:
   casting (by kind of spell), walking, standing free with nothing to do, unable to act, and how
   long it was dead. It needs no rule written in advance: a line that looks odd is a question to
   ask ("the Cleric spends 14 seconds a match buffing after contact: is that wrong?").

2. The rules. Things a competent group always or never does, each from something the owner said
   or saw. For each: what is expected, what was measured, PASS or FAIL, and the worst moments
   with match and second, to be looked up in the recording. A rule is a soft rule: the mark is
   where it starts to look wrong, not a law of the game.

Add a rule by adding a function to RULES. Keep the wording plain: the owner reads this.
"""
import collections
import glob
import json
import math
import os
import sys

SOFT = {"Cleric", "Friar", "Druid", "Bard", "Healer", "Shaman", "Sorcerer", "Theurgist", "Cabalist", "Wizard",
        "Runemaster", "Spiritmaster", "Eldritch", "Enchanter", "Mentalist"}
ONLY_HEAL = ("cleric-enhance", "cleric-rejuv", "druid-regrowth", "druid-nurture", "healer-mend")
CURERS = ("bard", "healer-pac", "healer-mend", "sorcerer-mind", "minstrel")
SHIELD = ("warrior", "hero", "paladin")
MEZ_REACH = {"alb-main": 1875, "mid-main": 1500, "hib-main": 1500}


def kind_of(spell_type):
    """The kind of work a spell is, from the server's spell type."""
    st = spell_type or ""
    if st.startswith("Summon"):
        return "calling a pet"
    if st.startswith("Cure"):
        return "curing"
    if "Heal" in st or st == "Resurrect":
        return "healing"
    if st in ("Mesmerize", "Stun", "SpeedDecrease", "UnbreakableSpeedDecrease", "Nearsight", "Amnesia", "Disease") or st.endswith("Debuff"):
        return "control and debuffs"
    if st in ("DirectDamage", "Lifedrain", "DamageOverTime", "Bolt", "DamageSpeedDecrease", "DirectDamageWithDebuff"):
        return "damage"
    if st.endswith("Buff") or st in ("SpeedEnhancement", "Bladeturn", "DamageAdd", "DamageShield", "DefensiveProc", "OffensiveProc", "AblativeArmor"):
        return "buffs and songs"
    return "other casts"


class Match:
    def __init__(self, path):
        rows = [json.loads(line) for line in open(path, encoding="utf-8") if line.strip()]
        self.ok = bool(rows) and rows[0].get("k") == "match" and rows[-1].get("k") == "end"
        if not self.ok:
            return
        self.rows, self.head, self.end = rows, rows[0], rows[-1]
        self.bots = {b["n"]: b for b in self.head["bots"]}
        self.contact = next((r["t"] for r in rows if r["k"] == "snap" and r.get("combat")), None)
        self.ok = self.contact is not None
        if not self.ok:
            return
        self.group = {"A": self.head["a"], "B": self.head["b"]}
        base = os.path.basename(path)
        self.name = f"{self.head['a']} against {self.head['b']}, match {self.head['number']} ({base[:15]})"
        self.per = collections.defaultdict(dict)
        self.squads = collections.defaultdict(dict)
        self.spell_type = {}
        for r in rows:
            if r["k"] == "snap" and r["n"] in self.bots:
                self.per[(r["t"] - self.contact) // 1000].setdefault(r["n"], r)
            elif r["k"] == "squad":
                self.squads[(r["t"] - self.contact) // 1000].setdefault(r["team"], r)
            elif r["k"] in ("begin", "cast") and r.get("spell"):
                self.spell_type.setdefault(r["spell"], r.get("st"))
        self.last = max(self.per) if self.per else 0

    def team(self, name):
        return self.bots[name]["team"]

    def soft(self, name):
        return self.bots[name]["cls"] in SOFT

    def alive_each(self, second):
        now = self.per.get(second, {})
        return (sum(1 for n, s in now.items() if s["alive"] and self.team(n) == "A"),
                sum(1 for n, s in now.items() if s["alive"] and self.team(n) == "B"))

    def undecided(self, second):
        """True while both sides still have five alive: positions in the tail of a decided fight say nothing."""
        a, b = self.alive_each(second)
        return a >= 5 and b >= 5


def dist(a, b):
    return math.dist((a["x"], a["y"]), (b["x"], b["y"]))


def free(s):
    return s["alive"] and not (s["mez"] or s["stun"] or s.get("intr") or s.get("sil"))


def median(xs):
    xs = sorted(xs)
    return xs[len(xs) // 2] if xs else float("nan")


# ---------------------------------------------------------------- the time budget

def time_budget(matches, out):
    spent = collections.defaultdict(collections.Counter)
    begun = collections.defaultdict(collections.Counter)
    done = collections.defaultdict(collections.Counter)
    played = collections.Counter()
    for m in matches:
        for name, bot in m.bots.items():
            played[bot["build"]] += 1
        for second in range(0, m.last + 1):
            for name, s in m.per.get(second, {}).items():
                build = m.bots[name]["build"]
                c = spent[build]
                c["seconds"] += 1
                if not s["alive"]:
                    c["dead"] += 1
                elif s["mez"] or s["stun"]:
                    c["asleep or stunned"] += 1
                elif s.get("casting"):
                    c["casting: " + kind_of(m.spell_type.get(s["casting"]))] += 1
                elif s.get("intr") or s.get("sil"):
                    c["kept from casting, not casting"] += 1
                elif s.get("moving"):
                    c["walking"] += 1
                else:
                    c["standing, free to act"] += 1
        for r in m.rows:
            if r["k"] in ("begin", "cast") and r.get("n") in m.bots and not r.get("pet") and r["t"] > m.contact and r.get("castTime", 0) > 0:
                (begun if r["k"] == "begin" else done)[m.bots[r["n"]]["build"]][kind_of(r.get("st"))] += 1
    out("WHERE THE TIME WENT (from first contact to the end of the match, a match)")
    out("A line marked ? is worth a look: it may be right, it may be a bug.\n")
    for build in sorted(spent):
        c, n = spent[build], max(1, played[build])
        living = max(1, c["seconds"] - c["dead"])
        out(f"{build}: alive {living / n:.0f} s a match, dead {100 * c['dead'] / max(1, c['seconds']):.0f}% of it")
        soft = not build.startswith(SHIELD) and any(build.startswith(p) for p in
               ("cleric", "druid", "healer", "bard", "shaman", "sorcerer", "theurgist", "cabalist", "runemaster", "eldritch", "enchanter", "minstrel"))
        for what, seconds in sorted(((k, v) for k, v in c.items() if k not in ("seconds", "dead")), key=lambda kv: -kv[1]):
            share = 100 * seconds / living
            if share < 1:
                continue
            odd = (what == "casting: buffs and songs" and share >= 4) or (what == "casting: calling a pet" and build in ONLY_HEAL) \
                or (what == "standing, free to act" and soft and share >= 30) or (what == "walking" and soft and share >= 35) \
                or (what == "kept from casting, not casting" and share >= 35)
            out(f"    {share:3.0f}%  {what}{'   ?' if odd else ''}")
        casts = ", ".join(f"{k} {begun[build][k] / n:.1f} begun, {done[build][k] / n:.1f} finished" + (" ?" if begun[build][k] >= 2 * n and done[build][k] * 2 < begun[build][k] else "")
                          for k in sorted(begun[build], key=lambda k: -begun[build][k]))
        if casts:
            out(f"    casts with a cast time, a match: {casts}")
    out("")


# ---------------------------------------------------------------- the rules

class Result:
    def __init__(self, title, source, expected):
        self.title, self.source, self.expected = title, source, expected
        self.measured, self.passed, self.examples = "", None, []

    def example(self, weight, text):
        self.examples.append((weight, text))


def rule_no_buffs_in_a_fight(matches):
    r = Result("Nobody stops to buff once the fight has started",
               "owner, 2026-11-18: a Cabalist cast its bubble during a fight, \"which should not happen\"",
               "no buff with a cast time begun after contact (songs are counted under the Bard's rule)")
    total, who = 0, collections.Counter()
    for m in matches:
        for x in m.rows:
            if x["k"] == "begin" and x["t"] > m.contact + 2000 and not x.get("pet") and x.get("n") in m.bots and x.get("castTime", 0) > 0 \
                    and kind_of(x.get("st")) == "buffs and songs" and m.bots[x["n"]]["build"] != "bard":
                total += 1
                who[(m.bots[x["n"]]["build"], x.get("st"))] += 1
                r.example(x["castTime"], f"{m.name}, {(x['t'] - m.contact) // 1000} s: {m.bots[x['n']]['build']} begins {x['spell']}")
    per = total / max(1, len(matches))
    r.measured = f"{per:.1f} a match" + ("; most: " + ", ".join(f"{b} {st} {n / len(matches):.1f}" for (b, st), n in who.most_common(4)) if who else "")
    r.passed = per <= 0.5
    return r


def rule_healers_call_no_pets(matches):
    r = Result("A healer does not call its pet in a fight",
               "owner, 2026-11-18: \"the Druid recast its pet mid fight and both Druid and Bard stopped to buff it\"",
               "no pet called by a healer after contact")
    total = 0
    for m in matches:
        for x in m.rows:
            if x["k"] == "begin" and x["t"] > m.contact + 2000 and x.get("n") in m.bots and m.bots[x["n"]]["build"] in ONLY_HEAL \
                    and (x.get("st") or "").startswith("Summon"):
                total += 1
                r.example(1, f"{m.name}, {(x['t'] - m.contact) // 1000} s: {m.bots[x['n']]['build']} begins {x['spell']}")
    r.measured = f"{total / max(1, len(matches)):.1f} a match"
    r.passed = total == 0
    return r


def rule_bard_plays_not_sings(matches):
    r = Result("The Bard keeps its endurance song up without spending the fight on it",
               "owner, 2026-11-18: only the endurance song needs to be up, and one that is up stays up",
               "at most 6 seconds a match casting songs after contact")
    seconds = n = 0
    for m in matches:
        for name, bot in m.bots.items():
            if bot["build"] != "bard":
                continue
            n += 1
            mine = sum(1 for second in range(0, m.last + 1) for who, s in m.per.get(second, {}).items()
                       if who == name and s["alive"] and s.get("casting") and kind_of(m.spell_type.get(s["casting"])) == "buffs and songs")
            seconds += mine
            r.example(mine, f"{m.name}: {mine} s")
    if n == 0:
        r.measured = "no Bard in these matches"
        return r
    r.measured = f"{seconds / n:.1f} s a match"
    r.passed = seconds / n <= 6
    return r


def rule_back_lines_apart(matches):
    r = Result("The two groups' healers and casters keep their distance",
               "owner, 2026-11-18: \"The fight is way too tight.\"",
               "the middles of the two back lines at least 1,500 units apart 20 and 45 seconds after contact")
    gaps = collections.defaultdict(list)
    for m in matches:
        for second in (20, 45):
            now = {n: s for n, s in m.per.get(second, {}).items() if s["alive"] and m.soft(n)}
            if not m.undecided(second):
                continue
            mids = {}
            for team in "AB":
                mine = [s for n, s in now.items() if m.team(n) == team]
                if mine:
                    mids[team] = {"x": sum(s["x"] for s in mine) / len(mine), "y": sum(s["y"] for s in mine) / len(mine)}
            if len(mids) == 2:
                gap = dist(mids["A"], mids["B"])
                gaps[second].append(gap)
                r.example(-gap, f"{m.name}, {second} s: {gap:.0f} units")
    if not gaps:
        r.measured = "no fight was still undecided at 20 s"
        return r
    r.measured = ", ".join(f"{median(gaps[s]):.0f} at {s} s" for s in sorted(gaps))
    r.passed = all(median(gaps[s]) >= 1500 for s in gaps)
    return r


def rule_casters_at_range(matches):
    r = Result("Casters cast from the edge of their range",
               "owner, 2026-11-18: casters and healers stand near the edge of their range; \"there are casters in the middle of melee\"",
               "a cast on an enemy healer or caster begins from 1,000 units or more at the median; under 700 in no more than one in ten "
               "(casts on a fighter are left out: one that has come to the caster is cast on from where it stands)")
    xs = []
    last = {}
    for m in matches:
        last.clear()
        for x in m.rows:
            if x["k"] == "snap":
                last[x["n"]] = x
            elif x["k"] == "begin" and x.get("n") in m.bots and not x.get("pet") and x["t"] >= m.contact and x.get("d") in m.bots \
                    and m.team(x["d"]) != m.team(x["n"]) and m.soft(x["n"]) and m.soft(x["d"]) and x["n"] in last and x["d"] in last                     and x.get("castTime", 0) > 0:
                d = dist(last[x["n"]], last[x["d"]])
                xs.append(d)
                r.example(-d, f"{m.name}, {(x['t'] - m.contact) // 1000} s: {m.bots[x['n']]['build']} begins {x['spell']} from {d:.0f} units")
    if not xs:
        r.measured = "no casts"
        return r
    close = 100 * sum(1 for d in xs if d < 700) / len(xs)
    r.measured = f"median {median(xs):.0f} units; under 700 units {close:.0f}%"
    r.passed = median(xs) >= 1000 and close <= 10
    return r


def rule_healers_apart(matches):
    r = Result("A group's healers do not stand together",
               "owner, 2026-11-18: \"Both Druids are way too close to each other.\"",
               "two healers of a group at least 800 units apart nine tenths of the time (first 45 seconds, both alive)")
    near = total = 0
    for m in matches:
        for team in "AB":
            healers = [n for n, b in m.bots.items() if b["team"] == team and b["build"] in ONLY_HEAL]
            if len(healers) < 2:
                continue
            for second in range(0, 46):
                now = m.per.get(second, {})
                both = [now[n] for n in healers if n in now and now[n]["alive"]]
                if len(both) < 2:
                    continue
                total += 1
                d = dist(both[0], both[1])
                if d < 800:
                    near += 1
                    r.example(-d, f"{m.name}, {second} s: {m.group[team]}'s healers {d:.0f} units apart")
    if not total:
        r.measured = "no group with two healers"
        return r
    r.measured = f"closer than 800 units {100 * near / total:.0f}% of the time"
    r.passed = near / total <= 0.1
    return r


def rule_divers_are_stopped(matches):
    r = Result("A fighter running at the healers is stopped",
               "owner, 2026-11-18: \"the Skald simply ran past everything to reach both Druids\"",
               "a fighter that reaches an enemy healer or caster in the first 25 seconds is stunned, mezzed or rooted within 3 seconds, two times in three")
    dives = quick = 0
    for m in matches:
        arrived = set()
        for second in range(-10, 26):
            now = m.per.get(second, {})
            for n, s in now.items():
                if m.soft(n) or not s["alive"] or n in arrived:
                    continue
                aim = s.get("aim") or s.get("target")
                if aim not in m.bots or m.team(aim) == m.team(n) or not m.soft(aim) or aim not in now or not now[aim]["alive"]:
                    continue
                if dist(s, now[aim]) > 350:
                    continue
                arrived.add(n)
                dives += 1
                first = next((x - second for x in range(second, second + 16)
                              if n in m.per.get(x, {}) and (m.per[x][n]["mez"] or m.per[x][n]["stun"] or m.per[x][n]["root"])), None)
                if first is not None and first <= 3:
                    quick += 1
                else:
                    r.example(first if first is not None else 99,
                              f"{m.name}, {second} s: {m.group[m.team(n)]}'s {m.bots[n]['build']} reaches {m.bots[aim]['build']}, "
                              + (f"first stopped {first} s later" if first is not None else "not stopped in 15 s"))
    if not dives:
        r.measured = "nobody reached a healer or caster in the opening"
        return r
    r.measured = f"{100 * quick / dives:.0f}% of {dives} stopped within 3 s"
    r.passed = quick / dives >= 0.66
    return r


def rule_guard_stays(matches):
    r = Result("A shield fighter told to stay beside its healer stays",
               "owner, 2026-11-18: the Warrior \"would walk back and forth between the enemy and the healer without doing anything\"",
               "while told to stay: an attack target no more than a tenth of the time")
    told = aiming = 0
    for m in matches:
        last = {}
        for x in m.rows:
            if x.get("t", 0) < m.contact:
                continue
            if x["k"] == "dec" and x.get("n") in m.bots and m.bots[x["n"]]["build"].startswith(SHIELD):
                kind = (x.get("pick") or {}).get("kind")
                if kind in ("Peel", "Assist", "Defend", "Hold", "Intercept"):
                    last[x["n"]] = kind
            elif x["k"] == "snap" and last.get(x["n"]) == "Hold" and x["alive"] and not (x["mez"] or x["stun"]):
                told += 1
                if x.get("target") is not None and x.get("combat"):
                    aiming += 1
    if not told:
        r.measured = "no shield fighter was told to stay"
        return r
    r.measured = f"an attack target {100 * aiming / told:.0f}% of the time it was told to stay"
    r.passed = aiming / told <= 0.1
    return r


def rule_no_standing_in_reach(matches):
    r = Result("A group does not stand still inside the enemy mezzer's reach before the fight",
               "owner, 2026-11-18: \"Mids are just standing there, doing nothing, until the Sorcerer is in range\"",
               "no second spent under the order Hold with an enemy within the enemy's mez reach plus 300")
    seconds = 0
    for m in matches:
        for second in range(-40, 0):
            for team, line in m.squads.get(second, {}).items():
                if line.get("order") != "Hold":
                    continue
                reach = MEZ_REACH.get(m.group["B" if team == "A" else "A"], 1500)
                now = m.per.get(second, {})
                mine = [s for n, s in now.items() if m.team(n) == team and s["alive"]]
                theirs = [s for n, s in now.items() if m.team(n) != team and s["alive"]]
                if not mine or not theirs:
                    continue
                nearest = min(dist(a, b) for a in mine for b in theirs)
                if nearest < reach + 300:
                    seconds += 1
                    r.example(-nearest, f"{m.name}, {-second} s before contact: {m.group[team]} holds with an enemy {nearest:.0f} units away (their mez reaches {reach})")
    r.measured = f"{seconds / max(1, len(matches)):.1f} s a match"
    r.passed = seconds / max(1, len(matches)) <= 1
    return r


def rule_sleeping_healers_are_woken(matches):
    r = Result("A sleeping healer is woken",
               "owner, 2026-11-16: mezzed members are woken by whoever has cure mez, healers first",
               "a healer's mez ends within 8 seconds, two times in three, while a groupmate with cure mez is alive")
    spans = quick = 0
    for m in matches:
        asleep = {}
        for second in range(0, m.last + 2):
            now = m.per.get(second, {})
            for n, b in m.bots.items():
                if b["build"] not in ONLY_HEAL:
                    continue
                s = now.get(n)
                mezzed = bool(s and s["alive"] and s["mez"])
                if mezzed and n not in asleep:
                    asleep[n] = second
                elif not mezzed and n in asleep:
                    start = asleep.pop(n)
                    curer = any(o != n and m.team(o) == m.team(n) and m.bots[o]["build"].startswith(CURERS) and so["alive"]
                                for o, so in m.per.get(start, {}).items())
                    if not curer or (s and not s["alive"]):
                        continue
                    spans += 1
                    if second - start <= 8:
                        quick += 1
                    else:
                        r.example(second - start, f"{m.name}, {start} s: {m.group[m.team(n)]}'s {b['build']} sleeps {second - start} s")
    if not spans:
        r.measured = "no healer slept with a curer alive"
        return r
    r.measured = f"{100 * quick / spans:.0f}% of {spans} mezzes ended within 8 s"
    r.passed = quick / spans >= 0.66
    return r


RULES = [rule_no_standing_in_reach, rule_back_lines_apart, rule_casters_at_range, rule_healers_apart, rule_divers_are_stopped,
         rule_guard_stays, rule_no_buffs_in_a_fight, rule_healers_call_no_pets, rule_bard_plays_not_sings, rule_sleeping_healers_are_woken]


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    folder, batch, since, until, target = argv[1], "", "", "￿", None
    rest = argv[2:]
    while rest:
        item = rest.pop(0)
        if item == "--batch":
            batch = rest.pop(0)
        elif item == "--since":
            since = rest.pop(0)
        elif item == "--until":
            until = rest.pop(0)
        elif item == "--out":
            target = rest.pop(0)
    matches = []
    for path in sorted(glob.glob(os.path.join(folder, "*.jsonl"))):
        base = os.path.basename(path)
        if not base.startswith(batch) or not (since <= base < until):
            continue
        m = Match(path)
        if m.ok:
            matches.append(m)
    lines = []
    out = lines.append
    out(f"8v8 referee: {len(matches)} matches from {folder}" + (f", batch {batch}" if batch else ""))
    if not matches:
        print(lines[0] + "\nNo finished match found.")
        return 1
    out("")
    out("THE RULES")
    out("Each is a soft rule: the mark is where it starts to look wrong. Small samples swing; six matches are a check, not a result.\n")
    results = [rule(matches) for rule in RULES]
    for r in results:
        mark = "PASS" if r.passed else "FAIL" if r.passed is not None else "n/a "
        out(f"[{mark}] {r.title}")
        out(f"        expected: {r.expected}")
        out(f"        measured: {r.measured}")
        if r.passed is False:
            for _, text in sorted(r.examples, key=lambda e: -e[0])[:3]:
                out(f"        look at:  {text}")
        out(f"        from:     {r.source}")
        out("")
    failed = sum(1 for r in results if r.passed is False)
    out(f"{failed} of {sum(1 for r in results if r.passed is not None)} rules fail.\n")
    time_budget(matches, out)
    text = "\n".join(lines)
    if target:
        with open(target, "w", encoding="utf-8") as handle:
            handle.write(text + "\n")
        print(f"{failed} rules fail; report written to {target}")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

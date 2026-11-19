"""Where every bot's time went, and whether a group did what a competent group does.

    python pvp_referee.py <scrims folder> [--batch prefix] [--since prefix] [--until prefix]
                          [--out file.txt] [--html page.html] [--before prefix]

--html writes the same report as a page for the browser. --before adds a column with the same
rules measured on an earlier batch, to see what a change did.

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
# The casters that are neither healers nor singers.
PURE_CASTERS = ("sorcerer", "theurgist", "cabalist", "wizard", "runemaster", "spiritmaster", "eldritch", "enchanter", "mentalist")
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

CASTERS = ("cleric", "druid", "healer", "bard", "shaman", "sorcerer", "theurgist", "cabalist", "runemaster", "eldritch", "enchanter", "minstrel")


def time_budget(matches):
    """Per build: seconds alive a match, share dead, the shares of its living time, and its casts begun and finished."""
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
                    c["being hit, not casting"] += 1
                elif s.get("moving"):
                    c["walking"] += 1
                else:
                    c["standing, free to act"] += 1
        for r in m.rows:
            if r["k"] in ("begin", "cast") and r.get("n") in m.bots and not r.get("pet") and r["t"] > m.contact and r.get("castTime", 0) > 0:
                (begun if r["k"] == "begin" else done)[m.bots[r["n"]]["build"]][kind_of(r.get("st"))] += 1
    budget = []
    for build in sorted(spent):
        c, n = spent[build], max(1, played[build])
        living = max(1, c["seconds"] - c["dead"])
        soft = build.startswith(CASTERS)
        lines = []
        for what, seconds in sorted(((k, v) for k, v in c.items() if k not in ("seconds", "dead")), key=lambda kv: -kv[1]):
            share = 100 * seconds / living
            if share < 1:
                continue
            # A fighter is "being hit, not casting" all fight long: that line only means something for a caster.
            odd = (what == "casting: buffs and songs" and share >= 4) or (what == "casting: calling a pet" and build in ONLY_HEAL) \
                or (what == "standing, free to act" and soft and share >= 30) or (what == "walking" and soft and share >= 35) \
                or (what == "being hit, not casting" and soft and share >= 35)
            lines.append((share, what, odd))
        casts = [(k, begun[build][k] / n, done[build][k] / n, begun[build][k] >= 2 * n and done[build][k] * 2 < begun[build][k])
                 for k in sorted(begun[build], key=lambda k: -begun[build][k])]
        budget.append({"build": build, "alive": living / n, "dead": 100 * c["dead"] / max(1, c["seconds"]), "lines": lines, "casts": casts})
    return budget


def budget_text(budget, out):
    out("WHERE THE TIME WENT (from first contact to the end of the match, a match)")
    out("A line marked ? is worth a look: it may be right, it may be a bug.\n")
    for item in budget:
        out(f"{item['build']}: alive {item['alive']:.0f} s a match, dead {item['dead']:.0f}% of it")
        for share, what, odd in item["lines"]:
            out(f"    {share:3.0f}%  {what}{'   ?' if odd else ''}")
        if item["casts"]:
            out("    casts with a cast time, a match: " + ", ".join(f"{k} {b:.1f} begun, {d:.1f} finished" + (" ?" if odd else "") for k, b, d, odd in item["casts"]))
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
               "a healer's mez ends within 8 seconds, two times in three, when a groupmate with cure mez is "
               "up (alive, not asleep, not stunned) for most of those seconds")
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
                    # A curer that is itself asleep, stunned or dead wakes nobody: counted only when one of the
                    # group's curers was up for most of the first eight seconds.
                    up = collections.Counter()
                    for moment in range(start, start + 9):
                        for o, so in m.per.get(moment, {}).items():
                            if o != n and m.team(o) == m.team(n) and m.bots[o]["build"].startswith(CURERS) and so["alive"]                                     and not so["mez"] and not so["stun"]:
                                up[o] += 1
                    if not any(seconds >= 5 for seconds in up.values()) or (s and not s["alive"]):
                        continue
                    spans += 1
                    if second - start <= 8:
                        quick += 1
                    else:
                        r.example(second - start, f"{m.name}, {start} s: {m.group[m.team(n)]}'s {b['build']} sleeps {second - start} s")
    if not spans:
        r.measured = "no healer slept with a curer up"
        return r
    r.measured = f"{100 * quick / spans:.0f}% of {spans} mezzes ended within 8 s"
    r.passed = quick / spans >= 0.66
    return r


def rule_casters_keep_casting(matches):
    r = Result("Keeping their distance does not stop the casters casting",
               "not from the owner: added in the night of 2026-11-19 as the cost side of the distance rules above",
               "of the seconds a caster is alive, awake and not stunned after contact: at least 30% casting, at most 40% walking")
    free = casting = walking = 0
    by = collections.defaultdict(lambda: [0, 0, 0])
    for m in matches:
        for second in range(1, m.last + 1):
            for n, s in m.per.get(second, {}).items():
                build = m.bots[n]["build"]
                if not build.startswith(PURE_CASTERS) or not s["alive"] or s["mez"] or s["stun"]:
                    continue
                free += 1
                by[build][0] += 1
                if s.get("casting"):
                    casting += 1
                    by[build][1] += 1
                elif s.get("moving"):
                    walking += 1
                    by[build][2] += 1
    if not free:
        r.measured = "no caster seconds"
        return r
    worst = sorted(by.items(), key=lambda kv: kv[1][1] / max(1, kv[1][0]))[:3]
    r.measured = (f"casting {100 * casting / free:.0f}%, walking {100 * walking / free:.0f}%; least casting: "
                  + ", ".join(f"{b} {100 * v[1] / max(1, v[0]):.0f}%" for b, v in worst))
    r.passed = casting / free >= 0.30 and walking / free <= 0.40
    return r


def rule_nobody_dies_at_once(matches):
    r = Result("Nobody dies within seconds of contact",
               "owner, 2026-11-19: somebody will die and a team has to wipe at some point; \"it just needs to not be from obvious "
               "'dumb' play or within few seconds of contact\"",
               "at most one match in ten with a death in the first 20 seconds (the mark is the assistant's)")
    firsts = []
    for m in matches:
        deaths = [x for x in m.rows if x["k"] == "death" and x.get("n") in m.bots]
        if not deaths:
            continue
        first = deaths[0]
        seconds = (first["t"] - m.contact) // 1000
        firsts.append(seconds)
        r.example(-seconds, f"{m.name}: the first to die is {m.bots[first['n']]['build']}, {seconds} s after contact")
    if not firsts:
        r.measured = "nobody died"
        return r
    firsts.sort()
    early = sum(1 for s in firsts if s < 20)
    r.measured = f"first death at a median of {firsts[len(firsts) // 2]} s; within 20 s in {early} of {len(firsts)} matches; the earliest at {firsts[0]} s"
    r.passed = early <= len(firsts) / 10
    return r


RULES = [rule_no_standing_in_reach, rule_back_lines_apart, rule_casters_at_range, rule_healers_apart, rule_divers_are_stopped,
         rule_guard_stays, rule_no_buffs_in_a_fight, rule_healers_call_no_pets, rule_bard_plays_not_sings, rule_sleeping_healers_are_woken,
         rule_casters_keep_casting, rule_nobody_dies_at_once]


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>8v8 referee</title>
<style>
:root { --bg:#fbfaf7; --ink:#1d1d1b; --soft:#666; --line:#dcd8cf; --pass:#1d6b3a; --fail:#a3271c; --na:#777; --mark:#fff3c4; --card:#fff; }
@media (prefers-color-scheme: dark) { :root { --bg:#17181a; --ink:#e8e6e1; --soft:#a09d96; --line:#35373b; --pass:#6fcf8f; --fail:#f08a7e; --na:#999; --mark:#4a4014; --card:#1f2124; } }
body { background:var(--bg); color:var(--ink); font:16px/1.5 system-ui, Segoe UI, sans-serif; margin:0; padding:24px 16px 60px; }
main { max-width:1100px; margin:0 auto; }
h1 { font-size:26px; margin:0 0 4px; } h2 { font-size:20px; margin:36px 0 8px; }
p.note { color:var(--soft); margin:4px 0 16px; max-width:75ch; }
table { border-collapse:collapse; width:100%; background:var(--card); }
th, td { text-align:left; vertical-align:top; padding:8px 10px; border-bottom:1px solid var(--line); }
th { font-size:13px; text-transform:uppercase; letter-spacing:.04em; color:var(--soft); }
.pass { color:var(--pass); font-weight:700; } .fail { color:var(--fail); font-weight:700; } .na { color:var(--na); }
.small { font-size:13px; color:var(--soft); } ul { margin:4px 0 0; padding-left:18px; }
.grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(300px, 1fr)); gap:12px; }
.card { background:var(--card); border:1px solid var(--line); border-radius:6px; padding:10px 12px; }
.card h3 { margin:0 0 2px; font-size:16px; } .bar { display:flex; gap:8px; align-items:baseline; }
.bar span.n { width:42px; text-align:right; font-variant-numeric:tabular-nums; } .odd { background:var(--mark); border-radius:3px; padding:0 4px; }
.wrap { overflow-x:auto; }
</style></head><body><main>
<h1>8v8 referee</h1>
<p class="note">__HEAD__</p>
<h2>The rules</h2>
<p class="note">Each rule comes from something the owner said or saw. The mark is where it starts to look wrong, not a law of the game.
Small samples swing: a dozen matches are a check, not a result. Who won is not on this page on purpose.</p>
<div class="wrap"><table><tr><th></th><th>Rule</th>__BEFOREHEAD__<th>Measured</th><th>Expected</th></tr>
__RULES__
</table></div>
<h2>Where the time went</h2>
<p class="note">For each build: how its living seconds were spent from first contact to the end of the match. A highlighted line is worth a
question; it may be right, it may be a bug.</p>
<div class="grid">
__BUDGET__
</div>
</main></body></html>
"""


def esc(text):
    return str(text).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def page(head, results, before, budget):
    rows = []
    for i, r in enumerate(results):
        mark = ("pass", "PASS") if r.passed else ("fail", "FAIL") if r.passed is not None else ("na", "n/a")
        looks = "".join(f"<li>{esc(text)}</li>" for _, text in sorted(r.examples, key=lambda e: -e[0])[:3]) if r.passed is False else ""
        was = f"<td>{esc(before[i].measured)}</td>" if before else ""
        rows.append(f"<tr><td class='{mark[0]}'>{mark[1]}</td><td><b>{esc(r.title)}</b><div class='small'>{esc(r.source)}</div></td>{was}"
                    f"<td>{esc(r.measured)}" + (f"<div class='small'>Look at:<ul>{looks}</ul></div>" if looks else "") + f"</td><td class='small'>{esc(r.expected)}</td></tr>")
    cards = []
    for item in budget:
        lines = "".join(f"<div class='bar'><span class='n'>{share:.0f}%</span><span{' class=\"odd\"' if odd else ''}>{esc(what)}</span></div>" for share, what, odd in item["lines"])
        casts = "; ".join(f"{esc(k)} {b:.1f} begun, {d:.1f} finished" for k, b, d, _ in item["casts"])
        cards.append(f"<div class='card'><h3>{esc(item['build'])}</h3><div class='small'>alive {item['alive']:.0f} s a match, dead {item['dead']:.0f}% of it</div>{lines}"
                     + (f"<div class='small'>Casts a match: {casts}</div>" if casts else "") + "</div>")
    return (PAGE.replace("__HEAD__", esc(head)).replace("__BEFOREHEAD__", "<th>Before</th>" if before else "")
            .replace("__RULES__", "\n".join(rows)).replace("__BUDGET__", "\n".join(cards)))


def load(folder, batch, since="", until="￿"):
    matches = []
    for path in sorted(glob.glob(os.path.join(folder, "*.jsonl"))):
        base = os.path.basename(path)
        if not base.startswith(batch) or not (since <= base < until):
            continue
        m = Match(path)
        if m.ok:
            matches.append(m)
    return matches


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    folder, batch, since, until, target, html, earlier = argv[1], "", "", "￿", None, None, None
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
        elif item == "--html":
            html = rest.pop(0)
        elif item == "--before":
            earlier = rest.pop(0)
    matches = load(folder, batch, since, until)
    lines = []
    out = lines.append
    head = f"{len(matches)} matches from {folder}" + (f", batch {batch}" if batch else "")
    out("8v8 referee: " + head)
    if not matches:
        print(lines[0] + "\nNo finished match found.")
        return 1
    results = [rule(matches) for rule in RULES]
    before = None
    if earlier:
        old = load(folder, earlier)
        before = [rule(old) for rule in RULES] if old else None
        head += f". Before: {len(old)} matches of batch {earlier}"
        del old
    out("")
    out("THE RULES")
    out("Each is a soft rule: the mark is where it starts to look wrong. Small samples swing; a dozen matches are a check, not a result.\n")
    for i, r in enumerate(results):
        mark = "PASS" if r.passed else "FAIL" if r.passed is not None else "n/a "
        out(f"[{mark}] {r.title}")
        out(f"        expected: {r.expected}")
        if before:
            out(f"        before:   {before[i].measured}")
        out(f"        measured: {r.measured}")
        if r.passed is False:
            for _, text in sorted(r.examples, key=lambda e: -e[0])[:3]:
                out(f"        look at:  {text}")
        out(f"        from:     {r.source}")
        out("")
    failed = sum(1 for r in results if r.passed is False)
    out(f"{failed} of {sum(1 for r in results if r.passed is not None)} rules fail.\n")
    budget = time_budget(matches)
    budget_text(budget, out)
    text = "\n".join(lines)
    if html:
        with open(html, "w", encoding="utf-8") as handle:
            handle.write(page(head, results, before, budget))
    if target:
        with open(target, "w", encoding="utf-8") as handle:
            handle.write(text + "\n")
    if target or html:
        print(f"{failed} rules fail; written to {', '.join(p for p in (target, html) if p)}")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

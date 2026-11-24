"""Draws one team's fight from a recorded evening, close up: where it stood, how it moved, who followed.

    python pvp_fight_draw.py <recording.jsonl> "<team>" <from second> <to second> <out.png>

The team's middle is a thick line in its realm's colour, with the time written along it every ten
seconds; its members are drawn every five seconds (a square: a fighter; a dot: a healer or
caster). Each other team near it has a thin line for its middle and fainter marks for its
members. A yellow ring: the group's place went back a leg there (bots/pvp/PvpKite.cs). A white
ring: it broke off there. A cross: somebody died there, in the colour of its realm. The grid is
1,000 units. Nothing of the game client is used.
"""
import collections
import json
import math
import sys

from PIL import Image, ImageDraw, ImageFont

SIZE, STRIP, EDGE = 1400, 130, 60
REALM = {"Albion": (255, 80, 70), "Midgard": (90, 160, 255), "Hibernia": (150, 255, 90)}
WRITTEN = {"_FirstPlayerRealm": "Albion", "_LastPlayerRealm": "Hibernia"}
FIGHTERS = {"Armsman", "Paladin", "Mercenary", "Reaver", "Warrior", "Berserker", "Savage", "Thane", "Skald", "Hero", "Champion",
            "Blademaster", "Warden", "Valewalker", "Minstrel", "Friar", "Infiltrator", "Shadowblade", "Nightshade", "Ranger", "Scout", "Hunter"}


def font(size, bold=False):
    for name in (("segoeuib.ttf", "arialbd.ttf") if bold else ("segoeui.ttf", "arial.ttf")):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def middle(points):
    points = list(points)
    return (sum(p[0] for p in points) / len(points), sum(p[1] for p in points) / len(points)) if points else None


def main():
    if len(sys.argv) < 6:
        sys.exit(__doc__)
    path, name, begin, end, out = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
    realms, kinds, classes = {}, {}, {}
    where = collections.defaultdict(lambda: collections.defaultdict(dict))   # team -> second -> member -> (x, y)
    foes, deaths, marks, last_legs = set(), [], [], 0
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            kind = line[6:10]
            if kind == "roam":
                for team in json.loads(line)["teams"]:
                    realms[team["name"]] = WRITTEN.get(team["realm"], team["realm"])
                    kinds[team["name"]] = team.get("kind") or ""
            elif kind == "spaw":
                for bot in json.loads(line)["bots"]:
                    classes[bot["n"]] = bot["cls"]
            elif kind == "ev\",":
                ev = json.loads(line).get("ev")
                if isinstance(ev, dict) and ev.get("what") == "join":
                    realms[ev["team"]] = WRITTEN.get(ev.get("realm"), ev.get("realm"))
                    kinds[ev["team"]] = ev.get("kind") or ""
            elif kind in ("snap", "team", "squa", "deat"):
                row = json.loads(line)
                second = row["t"] // 1000
                if not begin <= second <= end:
                    continue
                if kind == "snap":
                    if row["alive"]:
                        where[row["team"]][second][row["n"]] = (row["x"], row["y"])
                elif kind == "team":
                    if row["name"] == name:
                        foes |= set(row["fighting"])
                elif kind == "squa":
                    if row["team"] == name:
                        legs = row.get("legs") or 0
                        if legs > last_legs:
                            marks.append((second, "leg"))
                        last_legs = legs
                        if row.get("leaving") and not any(what == "off" for _, what in marks):
                            marks.append((second, "off"))
                else:
                    deaths.append((second, row["n"], row["team"]))
    own = where.get(name)
    if not own:
        sys.exit(f"No sample of '{name}' between {begin} and {end} s.")
    track = [(second, middle(own[second].values())) for second in sorted(own) if own[second]]
    centre = middle(point for _, point in track)
    # The enemy teams it fought, and any other team that came within 4,000 units of it.
    others = [team for team in where if team != name and (team in foes or any(
        second in where[team] and where[team][second] and math.dist(middle(where[team][second].values()), at) < 4000 for second, at in track))]
    points = [point for _, point in track] + [point for team in others for second in where[team] for point in where[team][second].values()
                                              if math.dist(point, centre) < 9000]
    min_x, max_x = min(p[0] for p in points) - 500, max(p[0] for p in points) + 500
    min_y, max_y = min(p[1] for p in points) - 500, max(p[1] for p in points) + 500
    span = max(max_x - min_x, max_y - min_y)
    scale = (SIZE - 2 * EDGE) / span

    def px(point):
        return (EDGE + (point[0] - min_x) * scale, EDGE + (point[1] - min_y) * scale)

    image = Image.new("RGBA", (SIZE, SIZE + STRIP), (16, 17, 20, 255))
    draw = ImageDraw.Draw(image, "RGBA")
    small, normal, big = font(18), font(21, True), font(28, True)
    for unit in range(int(min_x // 1000) * 1000, int(min_x + span) + 1000, 1000):
        x = px((unit, min_y))[0]
        if EDGE <= x <= SIZE - EDGE:
            draw.line([(x, EDGE), (x, SIZE - EDGE)], fill=(46, 48, 56, 255))
    for unit in range(int(min_y // 1000) * 1000, int(min_y + span) + 1000, 1000):
        y = px((min_x, unit))[1]
        if EDGE <= y <= SIZE - EDGE:
            draw.line([(EDGE, y), (SIZE - EDGE, y)], fill=(46, 48, 56, 255))

    def members(team, alpha, radius):
        colour = REALM.get(realms.get(team), (220, 220, 220))
        for second in sorted(where[team]):
            if (second - begin) % 5:
                continue
            for member, point in where[team][second].items():
                x, y = px(point)
                box = [x - radius, y - radius, x + radius, y + radius]
                if classes.get(member) in FIGHTERS:
                    draw.rectangle(box, fill=colour + (alpha,))
                else:
                    draw.ellipse(box, fill=colour + (alpha,))

    def line(team, width, alpha):
        colour = REALM.get(realms.get(team), (220, 220, 220))
        path = [(second, middle(where[team][second].values())) for second in sorted(where[team]) if where[team][second]]
        for (s0, a), (s1, b) in zip(path, path[1:]):
            if s1 - s0 <= 3:
                draw.line([px(a), px(b)], fill=colour + (alpha,), width=width)
        return path

    for team in others:
        members(team, 60, 3)
        path = line(team, 2, 150)
        if path:
            at = px(path[0][1])
            draw.text((at[0] + 8, at[1] + 4), f"{team} ({kinds.get(team, '')})", font=small, fill=REALM.get(realms.get(team), (220, 220, 220)) + (200,),
                      stroke_width=2, stroke_fill=(0, 0, 0))
    members(name, 150, 4)
    line(name, 6, 255)
    for second, at in track:
        if (second - begin) % 10 == 0:
            x, y = px(at)
            draw.ellipse([x - 5, y - 5, x + 5, y + 5], fill=(255, 255, 255, 255))
            draw.text((x + 9, y - 24), f"{second - begin} s", font=normal, fill=(255, 255, 255, 255), stroke_width=3, stroke_fill=(0, 0, 0))
    at_second = dict(track)
    for second, what in marks:
        if second in at_second:
            x, y = px(at_second[second])
            colour = (255, 220, 60, 255) if what == "leg" else (255, 255, 255, 255)
            draw.ellipse([x - 14, y - 14, x + 14, y + 14], outline=colour, width=3)
            if what == "off":
                draw.text((x + 18, y + 6), "breaks off", font=normal, fill=colour, stroke_width=3, stroke_fill=(0, 0, 0))
    shown = set(others) | {name}
    for second, member, team in deaths:
        point = where.get(team, {}).get(second - 1, {}).get(member) or where.get(team, {}).get(second, {}).get(member)
        if team in shown and point:
            x, y = px(point)
            colour = REALM.get(realms.get(team), (220, 220, 220)) + (255,)
            size = 9 if team == name else 6
            for a, b in (((x - size, y - size), (x + size, y + size)), ((x - size, y + size), (x + size, y - size))):
                draw.line([a, b], fill=(0, 0, 0, 255), width=7)
                draw.line([a, b], fill=colour, width=3)
    bar = 1000 * scale
    draw.line([(SIZE - EDGE - bar, SIZE - 34), (SIZE - EDGE, SIZE - 34)], fill=(255, 255, 255, 255), width=4)
    draw.text((SIZE - EDGE - bar / 2, SIZE - 30), "1,000 units", font=small, fill=(255, 255, 255, 255), anchor="ma")
    went = math.dist(track[0][1], track[-1][1])
    own_dead = sum(1 for _, _, team in deaths if team == name)
    their_dead = sum(1 for _, _, team in deaths if team in others)
    lines = [f"{name} ({realms.get(name, '?')} {kinds.get(name, '')}), {begin} to {end} s: its middle went {went:,.0f} units",
             f"Thick line: its middle, the time along it. Squares: fighters; dots: healers and casters (every 5 s). Thin lines: the {len(others)} other teams near it.",
             f"Yellow ring: its place went back a leg ({sum(1 for _, what in marks if what == 'leg')}). White ring: it broke off. "
             f"Crosses: deaths (its own {own_dead}, theirs {their_dead})."]
    for index, text in enumerate(lines):
        draw.text((18, SIZE + 10 + (0 if index == 0 else 14 + index * 27)), text, font=big if index == 0 else small,
                  fill=(255, 255, 255, 255) if index == 0 else (200, 200, 205, 255))
    image.convert("RGB").save(out)
    print("wrote", out)


if __name__ == "__main__":
    main()

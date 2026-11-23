"""Draws where the teams of a roam or a prime-time evening walk, on the game client's own map of the zone.

    python pvp_map_draw.py <client app folder> <zone> <out.png> [recording.jsonl]

zone: emain, odin or hadrian. Without a recording: the points the teams walk between, the legs
between them and where each realm starts. With one (logs/roams/*.jsonl beside the server): the
way each team's middle went through that zone, in its realm's colour, a ring where a fight began
and a cross where a team was wiped.

The points are those of PvpRoam.cs (Emain, OdinsGate, HadriansWall): keep the two alike. A leg is
drawn as a straight line; a team walks it on the navigation mesh, which is what a recording shows.
The picture is made from the client's map (ui/maps/zNNN.dds), so it is not for the repository.
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ZONE = 65536
SIZE = 1280
STRIP = 150
REALM = {"Albion": (255, 80, 70), "Midgard": (90, 160, 255), "Hibernia": (150, 255, 90)}
WRITTEN = {"_FirstPlayerRealm": "Albion", "_LastPlayerRealm": "Hibernia"}

EMAIN = dict(North=(449336, 299008), South=(445970, 334336), Middle=(447500, 316500),
             WestNorth=(438000, 305000), WestMiddle=(438000, 316000), WestSouth=(438000, 328000))
HW = dict(NorthWest=(615516, 304220), WestGate=(608516, 318220), SouthWest=(615516, 332220), South=(629516, 332220),
          SouthEast=(643516, 332220), BenowycRoad=(636516, 339220), EastGate=(650516, 318220), NorthEast=(643516, 304220))
OG = dict(NorthWest=(607324, 590940), NorthRoad=(628324, 583940), NorthEast=(642324, 590940), East=(642324, 604940),
          Middle=(628324, 604940), SouthEast=(642324, 618940), SouthRoad=(628324, 625940), SouthWest=(607324, 618940))

ZONES = {
    "emain": dict(
        name="Emain Macha", map="z214.dds", offset=(51 * 8192, 35 * 8192), points=EMAIN,
        names=dict(North="north road", South="south road", Middle="mid road", WestNorth="north-west hills",
                   WestMiddle="west hills", WestSouth="south-west hills"),
        ways=[("South", "Middle"), ("Middle", "North"), ("North", "WestNorth"), ("WestNorth", "WestMiddle"),
              ("WestMiddle", "WestSouth"), ("WestSouth", "South"), ("Middle", "WestMiddle")],
        quiet=["WestNorth", "WestMiddle", "WestSouth"],
        starts=[("Albion", (473395, 342169), ["South"]), ("Midgard", (472166, 297011), ["North"]),
                ("Hibernia", (427772, 320193), ["WestSouth", "WestNorth"])]),
    "hadrian": dict(
        name="Hadrian's Wall", map="z015.dds", offset=(73 * 8192, 35 * 8192), points=HW,
        names=dict(NorthWest="north-west field", WestGate="west gate", SouthWest="south-west field", South="south field",
                   SouthEast="south-east field", BenowycRoad="benowyc road", EastGate="east gate", NorthEast="north-east field"),
        ways=[("NorthWest", "WestGate"), ("WestGate", "SouthWest"), ("SouthWest", "South"), ("South", "SouthEast"),
              ("SouthEast", "EastGate"), ("EastGate", "NorthEast"), ("BenowycRoad", "South"), ("BenowycRoad", "SouthEast")],
        quiet=["SouthWest"],
        starts=[("Albion", (651802, 343992), ["BenowycRoad"]), ("Midgard", (653567, 294974), ["NorthEast", "EastGate"]),
                ("Hibernia", (607339, 295574), ["NorthWest", "WestGate"])]),
    "odin": dict(
        name="Odin's Gate", map="z115.dds", offset=(72 * 8192, 70 * 8192), points=OG,
        names=dict(NorthWest="north-west field", NorthRoad="north road", NorthEast="north-east field", East="east field",
                   Middle="middle", SouthEast="south-east field", SouthRoad="south road", SouthWest="south-west field"),
        ways=[("NorthWest", "NorthRoad"), ("NorthRoad", "NorthEast"), ("NorthRoad", "Middle"), ("Middle", "East"),
              ("East", "NorthEast"), ("East", "SouthEast"), ("SouthEast", "Middle"), ("SouthRoad", "SouthEast"),
              ("SouthRoad", "Middle"), ("SouthWest", "SouthRoad")],
        quiet=["SouthEast"],
        starts=[("Albion", (598175, 629643), ["SouthWest"]), ("Midgard", (646847, 585287), ["NorthEast", "NorthRoad"]),
                ("Hibernia", (597987, 583313), ["NorthWest", "NorthRoad"])]),
}


def font(size, bold=False):
    for name in (("segoeuib.ttf", "arialbd.ttf") if bold else ("segoeui.ttf", "arial.ttf")):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def dashed(draw, a, b, fill, width, dash=14, gap=10):
    length = ((b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2) ** 0.5
    at = 0.0
    while at < length:
        end = min(at + dash, length)
        draw.line([(a[0] + (b[0] - a[0]) * at / length, a[1] + (b[1] - a[1]) * at / length),
                   (a[0] + (b[0] - a[0]) * end / length, a[1] + (b[1] - a[1]) * end / length)], fill=fill, width=width)
        at = end + gap


def label(draw, at, text, face, fill=(255, 255, 255), anchor="la"):
    draw.text(at, text, font=face, fill=fill, anchor=anchor, stroke_width=3, stroke_fill=(0, 0, 0))


def read(path, inside):
    """The teams' realms, each team's samples in the zone (second, x, y, state), where fights began and where teams were wiped."""
    realms, tracks, last, fights, wipes, seconds = {}, {}, {}, [], [], 0
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            if not line.startswith(('{"k":"team"', '{"k":"ev"', '{"k":"roam"')):
                continue
            row = json.loads(line)
            if row["k"] == "roam":
                for team in row["teams"]:
                    realms[team["name"]] = WRITTEN.get(team["realm"], team["realm"])
            elif row["k"] == "team":
                seconds = max(seconds, row["t"] / 1000)
                last[row["name"]] = (row["x"], row["y"])
                if row["state"] in ("Roaming", "Fighting") and row["alive"] > 0 and inside(row["x"], row["y"]):
                    tracks.setdefault(row["name"], []).append((row["t"] / 1000, row["x"], row["y"]))
            elif isinstance(row.get("ev"), dict):
                ev = row["ev"]
                what = ev.get("what")
                if what == "join" and ev.get("realm"):
                    realms[ev["team"]] = WRITTEN.get(ev["realm"], ev["realm"])
                elif what in ("fight", "wipe") and ev.get("team") in last and inside(*last[ev["team"]]):
                    (fights if what == "fight" else wipes).append((ev["team"], *last[ev["team"]]))
    return realms, tracks, fights, wipes, seconds


def main():
    if len(sys.argv) < 4 or sys.argv[2] not in ZONES:
        sys.exit(__doc__)
    zone = ZONES[sys.argv[2]]
    recording = sys.argv[4] if len(sys.argv) > 4 else None
    ox, oy = zone["offset"]

    def px(point):
        return ((point[0] - ox) * SIZE / ZONE, (point[1] - oy) * SIZE / ZONE)

    def inside(x, y):
        return ox <= x < ox + ZONE and oy <= y < oy + ZONE

    source = Image.open(Path(sys.argv[1]) / "ui" / "maps" / zone["map"]).convert("RGB").resize((SIZE, SIZE), Image.BICUBIC)
    # Darker when tracks go over it, so that the three colours stand out.
    source = Image.eval(source, lambda value: int(value * (0.6 if recording else 0.85)))
    canvas = Image.new("RGBA", (SIZE, SIZE + STRIP), (18, 18, 20, 255))
    canvas.paste(source, (0, 0))
    over = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(over)
    points = {key: px(value) for key, value in zone["points"].items()}
    small, normal, big = font(19), font(22, True), font(30, True)
    lines = []

    if recording:
        realms, tracks, fights, wipes, seconds = read(recording, inside)
        for team, samples in tracks.items():
            colour = REALM.get(realms.get(team), (230, 230, 230))
            for (t0, x0, y0), (t1, x1, y1) in zip(samples, samples[1:]):
                # (A team that stood up at its start, or came from another zone, did not walk there.)
                if t1 - t0 < 8 and abs(x1 - x0) + abs(y1 - y0) < 3000:
                    draw.line([px((x0, y0)), px((x1, y1))], fill=colour + (120,), width=3)
        canvas = Image.alpha_composite(canvas, over)
        over = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(over)
        for a, b in zone["ways"]:
            dashed(draw, points[a], points[b], (255, 220, 60, 150), 2)
        for _, x, y in fights:
            cx, cy = px((x, y))
            draw.ellipse([cx - 11, cy - 11, cx + 11, cy + 11], outline=(255, 255, 255, 190), width=2)
        for team, x, y in wipes:
            cx, cy = px((x, y))
            colour = REALM.get(realms.get(team), (230, 230, 230)) + (255,)
            draw.line([cx - 8, cy - 8, cx + 8, cy + 8], fill=(0, 0, 0, 255), width=7)
            draw.line([cx - 8, cy + 8, cx + 8, cy - 8], fill=(0, 0, 0, 255), width=7)
            draw.line([cx - 8, cy - 8, cx + 8, cy + 8], fill=colour, width=3)
            draw.line([cx - 8, cy + 8, cx + 8, cy - 8], fill=colour, width=3)
        for key, at in points.items():
            draw.ellipse([at[0] - 6, at[1] - 6, at[0] + 6, at[1] + 6], fill=(255, 220, 60, 255), outline=(0, 0, 0, 255))
            label(draw, (at[0] + 10, at[1] + 6), zone["names"][key], small, (255, 235, 150))
        per = {realm: sum(1 for team in tracks if realms.get(team) == realm) for realm in REALM}
        lines = [f"{zone['name']}: where {len(tracks)} teams went in {seconds / 60:.0f} minutes ({Path(recording).name})",
                 "A line: the way a team's middle went, in its realm's colour "
                 f"(Albion {per['Albion']}, Midgard {per['Midgard']}, Hibernia {per['Hibernia']}).",
                 f"A white ring: a fight began there ({len(fights)}). A cross: a team was wiped there ({len(wipes)}). "
                 "Yellow: the points and the legs between them."]
    else:
        for realm, start, firsts in zone["starts"]:
            for first in firsts:
                dashed(draw, px(start), points[first], REALM[realm] + (230,), 4)
        for a, b in zone["ways"]:
            draw.line([points[a], points[b]], fill=(0, 0, 0, 200), width=9)
            draw.line([points[a], points[b]], fill=(255, 220, 60, 255), width=5)
        for key, at in points.items():
            quiet = key in zone["quiet"]
            draw.ellipse([at[0] - 11, at[1] - 11, at[0] + 11, at[1] + 11], fill=(90, 230, 255, 255) if quiet else (255, 220, 60, 255),
                         outline=(0, 0, 0, 255), width=3)
            label(draw, (at[0] + 16, at[1] + 4), zone["names"][key], normal, (170, 240, 255) if quiet else (255, 235, 150))
        for realm, start, _ in zone["starts"]:
            at = px(start)
            draw.rectangle([at[0] - 11, at[1] - 11, at[0] + 11, at[1] + 11], fill=REALM[realm] + (255,), outline=(0, 0, 0, 255), width=3)
            right = at[0] > SIZE * 0.7
            label(draw, (at[0] + (-16 if right else 16), at[1] - 34), f"{realm} starts", normal, REALM[realm], "ra" if right else "la")
        lines = [f"{zone['name']}: the points the teams walk between",
                 "Square: where a realm's teams come out and stand up again. Dashed: their way to their first point.",
                 "Yellow: a leg (drawn straight; walked on the ground). At each point a team picks any leg but the one it came by.",
                 "Blue point: the quiet ground, which an elite team leans to."]

    bar = 10000 * SIZE / ZONE
    draw.line([(SIZE - 40 - bar, SIZE - 30), (SIZE - 40, SIZE - 30)], fill=(255, 255, 255, 255), width=4)
    label(draw, (SIZE - 40 - bar / 2, SIZE - 62), "10,000 units", small, anchor="ma")
    for index, text in enumerate(lines):
        draw.text((18, SIZE + 12 + (0 if index == 0 else 12 + index * 28)), text, font=big if index == 0 else small,
                  fill=(255, 255, 255, 255) if index == 0 else (200, 200, 205, 255))
    Image.alpha_composite(canvas, over).convert("RGB").save(sys.argv[3])
    print("wrote", sys.argv[3])


if __name__ == "__main__":
    main()

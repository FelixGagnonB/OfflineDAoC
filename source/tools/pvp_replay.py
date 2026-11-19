"""Draw a recorded 8v8 match from above (docs/PVP-8V8-PLAYBOOK.md, section 9).

    python pvp_replay.py <recording.jsonl> [--out <folder>] [--from -3] [--step 5] [--panels 12]

Writes picture sheets (sheet-01.png, ...) of the fight seen from above, six moments to a sheet,
and replay.html, the same fight to play and scrub in a browser with every member's health,
power and state beside it. The owner, 2026-11-19: "you would gain so much more insight from
seeing the fight... a picture is worth 1000 words". The numbers of the referee say how often
something happens; these show where everybody stood when it did.

Read from the recording only: positions once a second, who targets whom, who is mezzed,
stunned, rooted or casting, each group's order and kill target. Pets are not recorded and so
are not drawn. North is up. Needs Pillow for the pictures; the page needs nothing.
"""
import json
import math
import os
import sys

FIGHTERS = ("Armsman", "Paladin", "Mercenary", "Reaver", "Minstrel", "Warrior", "Berserker", "Savage", "Skald", "Thane",
            "Hero", "Champion", "Blademaster", "Valewalker", "Friar")
HEALERS = ("Cleric", "Druid", "Healer", "Shaman")
REALM_COLOUR = {"alb": (205, 45, 45), "mid": (45, 95, 225), "hib": (30, 150, 60)}
GREY = (150, 150, 150)


def load(path):
    rows = [json.loads(line) for line in open(path, encoding="utf-8") if line.strip()]
    head = rows[0]
    bots = {b["n"]: b for b in head["bots"]}
    realm = {"A": head["a"][:3], "B": head["b"][:3]}
    first = next((r["t"] for r in rows if r["k"] == "hit"), None)
    if first is None:
        sys.exit("No fight in this recording yet.")
    seconds = {}
    squads = {}
    notes = {}
    for r in rows:
        if "t" not in r:
            continue
        s = math.floor((r["t"] - first) / 1000)
        if r["k"] == "snap":
            seconds.setdefault(s, {})[r["n"]] = r
        elif r["k"] == "squad":
            squads.setdefault(s, {})[r["team"]] = r
        elif r["k"] == "dec" and r.get("pick"):
            pick = r["pick"]
            notes.setdefault(s, []).append((r["n"], pick["kind"], pick.get("spell") or pick.get("d") or ""))
        elif r["k"] == "begin":
            notes.setdefault(s, []).append((r["n"], "casts", "%s on %s" % (r["spell"], r.get("d"))))
    end = next((r for r in rows if r["k"] == "end"), None)
    return head, bots, realm, seconds, squads, notes, end


def squad_at(squads, s, team):
    for back in range(s, s - 30, -1):
        if back in squads and team in squads[back]:
            return squads[back][team]
    return None


def shape(cls):
    return "fighter" if cls in FIGHTERS else "healer" if cls in HEALERS else "caster"


def sheets(path, out, start, step, panels):
    from PIL import Image, ImageDraw, ImageFont
    head, bots, realm, seconds, squads, notes, end = load(path)
    last = max(seconds)
    moments = [s for s in range(start, last + 1, step)][:panels]

    def font(size):
        for name in ("arial.ttf", "DejaVuSans.ttf"):
            try:
                return ImageFont.truetype(name, size)
            except OSError:
                pass
        return ImageFont.load_default()

    small, normal, big = font(15), font(18), font(22)
    width, height, top = 900, 760, 64
    files = []
    for sheet in range(0, len(moments), 6):
        chunk = moments[sheet:sheet + 6]
        # One view for the whole sheet, so that movement from panel to panel can be read.
        xs, ys = [], []
        for s in range(chunk[0] - 3, chunk[-1] + 1):
            for snap in seconds.get(s, {}).values():
                if snap["alive"]:
                    xs.append(snap["x"])
                    ys.append(snap["y"])
        if not xs:
            continue
        margin = 250
        x0, x1, y0, y1 = min(xs) - margin, max(xs) + margin, min(ys) - margin, max(ys) + margin
        scale = min(width / (x1 - x0), (height - top) / (y1 - y0))
        ox = (width - (x1 - x0) * scale) / 2
        oy = top + (height - top - (y1 - y0) * scale) / 2
        image = Image.new("RGB", (width * 3, height * 2 + 40), (250, 250, 247))
        draw = ImageDraw.Draw(image)
        for index, s in enumerate(chunk):
            px, py = (index % 3) * width, (index // 3) * height

            def at(snap):
                return px + ox + (snap["x"] - x0) * scale, py + oy + (snap["y"] - y0) * scale

            draw.rectangle([px, py, px + width - 1, py + height - 1], outline=(190, 190, 190))
            draw.text((px + 10, py + 6), ("%+d s" % s) + ("   (before the first blow)" if s < 0 else ""), fill=(20, 20, 20), font=big)
            for line, team in enumerate("AB"):
                squad = squad_at(squads, s, team)
                kill = squad.get("kill") if squad else None
                text = "%s: %s" % (realm[team].capitalize(), squad["order"] if squad else "?")
                if kill:
                    text += ", kill %s" % bots[kill]["cls"] if kill in bots else ""
                if squad and squad.get("trained") in bots:
                    text += ", trained %s" % bots[squad["trained"]]["cls"]
                draw.text((px + 10 + line * 440, py + 36), text, fill=REALM_COLOUR.get(realm[team], GREY), font=normal)
            # A scale: 1,000 units.
            draw.line([px + 12, py + height - 14, px + 12 + 1000 * scale, py + height - 14], fill=(60, 60, 60), width=3)
            draw.text((px + 18 + 1000 * scale, py + height - 24), "1,000", fill=(60, 60, 60), font=small)
            now = seconds.get(s, {})
            # Where each has come from in the last three seconds.
            for name, snap in now.items():
                if not snap["alive"]:
                    continue
                trail = [seconds.get(back, {}).get(name) for back in range(s - 3, s + 1)]
                points = [at(p) for p in trail if p and p["alive"]]
                if len(points) > 1:
                    colour = REALM_COLOUR.get(realm[bots[name]["team"]], GREY)
                    draw.line(points, fill=tuple(int(c + (255 - c) * 0.55) for c in colour), width=4)
            # Whom each is on: a thin line to an enemy target.
            for name, snap in now.items():
                target = now.get(snap.get("target") or "")
                if not snap["alive"] or not target or bots[snap["target"]]["team"] == bots[name]["team"]:
                    continue
                colour = REALM_COLOUR.get(realm[bots[name]["team"]], GREY)
                draw.line([at(snap), at(target)], fill=tuple(int(c + (255 - c) * 0.35) for c in colour), width=1)
            called = {(squad_at(squads, s, team) or {}).get("kill") for team in "AB"}
            for name, snap in now.items():
                x, y = at(snap)
                cls = bots[name]["cls"]
                # The enemy's kill target: a black frame.
                if name in called and snap["alive"]:
                    draw.rectangle([x - 21, y - 21, x + 21, y + 21], outline=(0, 0, 0), width=2)
                colour = REALM_COLOUR.get(realm[bots[name]["team"]], GREY)
                if not snap["alive"]:
                    draw.line([x - 7, y - 7, x + 7, y + 7], fill=GREY, width=3)
                    draw.line([x - 7, y + 7, x + 7, y - 7], fill=GREY, width=3)
                    draw.text((x + 9, y - 9), cls[:3], fill=GREY, font=small)
                    continue
                ring = (235, 200, 0) if snap["mez"] else (240, 120, 0) if snap["stun"] else (120, 70, 20) if snap["root"] else None
                if ring:
                    draw.ellipse([x - 16, y - 16, x + 16, y + 16], outline=ring, width=5)
                kind = shape(cls)
                if kind == "fighter":
                    draw.polygon([(x, y - 11), (x - 10, y + 9), (x + 10, y + 9)], fill=colour, outline=(0, 0, 0))
                elif kind == "healer":
                    draw.rectangle([x - 9, y - 9, x + 9, y + 9], fill=colour, outline=(0, 0, 0))
                    draw.line([x - 6, y, x + 6, y], fill=(255, 255, 255), width=3)
                    draw.line([x, y - 6, x, y + 6], fill=(255, 255, 255), width=3)
                else:
                    draw.ellipse([x - 9, y - 9, x + 9, y + 9], fill=colour, outline=(0, 0, 0))
                if snap.get("casting"):
                    draw.ellipse([x - 3, y - 3, x + 3, y + 3], fill=(255, 255, 255))
                health = 100 * snap["hp"] // max(1, snap_max(seconds, name))
                label = "%s %d" % (cls[:3], health)
                draw.text((x + 12, y - 9), label, fill=(25, 25, 25), font=small)
        legend = ("triangle fighter, square healer, circle caster; white dot casting; ring: yellow mezzed, orange stunned, brown rooted or snared; "
                  "x dead; black frame = a kill target; number = health %; thin line = its target; pale tail = the last 3 s; north is up")
        draw.text((12, height * 2 + 10), "%s match %s.   %s" % (head["batch"], head["number"], legend), fill=(40, 40, 40), font=small)
        name = os.path.join(out, "sheet-%02d.png" % (sheet // 6 + 1))
        image.save(name)
        files.append(name)
    return files


_MAX = {}


def snap_max(seconds, name):
    if name not in _MAX:
        _MAX[name] = max((snap[name]["hp"] for snap in seconds.values() if name in snap), default=1)
    return _MAX[name]


PAGE = """<!doctype html><html><head><meta charset="utf-8"><title>8v8 replay</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;margin:16px;background:#fafaf7;color:#222}
#wrap{display:flex;gap:16px;flex-wrap:wrap}
canvas{background:#fff;border:1px solid #bbb}
table{border-collapse:collapse;font-size:13px}
td,th{padding:2px 7px;border-bottom:1px solid #e3e3e3;text-align:left;white-space:nowrap}
.bar{display:inline-block;height:9px;vertical-align:middle}
#notes{font-size:13px;max-width:880px;min-height:5em}
button{font-size:15px;padding:3px 12px}
input[type=range]{width:620px;vertical-align:middle}
</style></head><body>
<h2 id="title"></h2>
<p><button id="play">Play</button> <input type="range" id="time"> <b id="clock"></b>
 &nbsp; speed <select id="speed"><option value="1">1x</option><option value="2" selected>2x</option><option value="4">4x</option></select></p>
<p id="orders"></p>
<div id="wrap"><canvas id="map" width="880" height="700"></canvas><table id="roster"></table></div>
<p id="notes"></p>
<p style="font-size:12px;color:#555">Triangle fighter, square healer, circle caster. White dot: casting. Ring: yellow mezzed, orange stunned,
brown rooted or snared. Thin line: its target. Grey cross: dead. North is up; the bar under the map is 1,000 units. Pets are not recorded.</p>
<script>
const D = __DATA__;
const colour = {alb:"#cd2d2d", mid:"#2d5fe1", hib:"#1e963c"};
const fighters = new Set(__FIGHTERS__), healers = new Set(__HEALERS__);
const cv = document.getElementById("map"), g = cv.getContext("2d"), slider = document.getElementById("time");
slider.min = D.from; slider.max = D.to; slider.value = D.from;
document.getElementById("title").textContent = D.title;
let xs=[],ys=[]; for (const s in D.seconds) for (const n in D.seconds[s]) { const p=D.seconds[s][n]; if (p.a) {xs.push(p.x); ys.push(p.y);} }
const x0=Math.min(...xs)-250,x1=Math.max(...xs)+250,y0=Math.min(...ys)-250,y1=Math.max(...ys)+250;
const sc=Math.min(cv.width/(x1-x0),(cv.height-30)/(y1-y0));
const at=p=>[(p.x-x0)*sc+(cv.width-(x1-x0)*sc)/2,(p.y-y0)*sc+(cv.height-30-(y1-y0)*sc)/2];
function squad(s,t){for(let b=s;b>s-30;b--){if(D.squads[b]&&D.squads[b][t])return D.squads[b][t];}return null;}
function draw(s){
  const now=D.seconds[s]||{}; g.clearRect(0,0,cv.width,cv.height);
  g.strokeStyle="#333"; g.lineWidth=3; g.beginPath(); g.moveTo(12,cv.height-12); g.lineTo(12+1000*sc,cv.height-12); g.stroke();
  for(const n in now){const p=now[n]; if(!p.a) continue; const pts=[]; for(let b=s-3;b<=s;b++){const q=(D.seconds[b]||{})[n]; if(q&&q.a) pts.push(at(q));}
    if(pts.length>1){g.strokeStyle=colour[D.realm[D.bots[n].team]]+"55"; g.lineWidth=4; g.beginPath(); g.moveTo(...pts[0]); for(const q of pts.slice(1)) g.lineTo(...q); g.stroke();}}
  for(const n in now){const p=now[n], t=now[p.t]; if(!p.a||!t||D.bots[p.t].team===D.bots[n].team) continue;
    g.strokeStyle=colour[D.realm[D.bots[n].team]]+"99"; g.lineWidth=1; g.beginPath(); g.moveTo(...at(p)); g.lineTo(...at(t)); g.stroke();}
  for(const n in now){const p=now[n], [x,y]=at(p), c=D.bots[n].cls, col=colour[D.realm[D.bots[n].team]];
    if(!p.a){g.strokeStyle="#999"; g.lineWidth=3; g.beginPath(); g.moveTo(x-7,y-7); g.lineTo(x+7,y+7); g.moveTo(x-7,y+7); g.lineTo(x+7,y-7); g.stroke(); continue;}
    const ring=p.m?"#ebc800":p.s?"#f07800":p.r?"#784614":null;
    if(ring){g.strokeStyle=ring; g.lineWidth=5; g.beginPath(); g.arc(x,y,15,0,7); g.stroke();}
    g.fillStyle=col; g.strokeStyle="#000"; g.lineWidth=1; g.beginPath();
    if(fighters.has(c)){g.moveTo(x,y-11); g.lineTo(x-10,y+9); g.lineTo(x+10,y+9); g.closePath();}
    else if(healers.has(c)){g.rect(x-9,y-9,18,18);} else {g.arc(x,y,9,0,7);}
    g.fill(); g.stroke();
    if(p.c){g.fillStyle="#fff"; g.beginPath(); g.arc(x,y,3.5,0,7); g.fill();}
    g.fillStyle="#222"; g.font="13px Segoe UI"; g.fillText(c.slice(0,3),x+12,y+4);}
  document.getElementById("clock").textContent=(s>=0?"+":"")+s+" s";
  let o=""; for(const t of ["A","B"]){const q=squad(s,t); const r=D.realm[t];
    o+='<b style="color:'+colour[r]+'">'+r[0].toUpperCase()+r.slice(1)+"</b>: "+(q?q.order:"?")+(q&&q.kill&&D.bots[q.kill]?", kill "+D.bots[q.kill].cls+" ("+q.kill+")":"")+(q&&q.trained&&D.bots[q.trained]?", trained "+D.bots[q.trained].cls:"")+" &nbsp; ";}
  document.getElementById("orders").innerHTML=o;
  let h="<tr><th></th><th>build</th><th>name</th><th>health</th><th>power</th><th>end</th><th>state</th><th>target</th></tr>";
  for(const n of D.order){const b=D.bots[n], p=now[n]; if(!p) continue; const col=colour[D.realm[b.team]];
    const bar=(v,c)=>'<span class="bar" style="width:'+Math.round(Math.max(0,v)*0.6)+'px;background:'+c+'"></span> '+Math.round(v);
    const st=!p.a?"dead":p.m?"mezzed":p.s?"stunned":p.c?"casting "+p.c:p.mv?"moving":p.r?"rooted/snared":"";
    h+='<tr style="'+(p.a?"":"color:#999")+'"><td style="background:'+col+'">&nbsp;</td><td>'+b.build+"</td><td>"+n+"</td><td>"+bar(100*p.h/b.hp,"#3a3")+"</td><td>"+(b.pow?bar(100*p.p/b.pow,"#36c"):"")+"</td><td>"+bar(p.e,"#c93")+"</td><td>"+st+"</td><td>"+(p.t&&D.bots[p.t]?D.bots[p.t].cls+(D.bots[p.t].team===b.team?" (own)":""):"")+"</td></tr>";}
  document.getElementById("roster").innerHTML=h;
  document.getElementById("notes").innerHTML=(D.notes[s]||[]).map(e=>"<b>"+D.bots[e[0]].cls+"</b> "+e[1]+" "+e[2]).join(" &middot; ");
}
let timer=null;
function step(){let s=+slider.value; if(s>=D.to){stop();return;} slider.value=s+1; draw(s+1);}
function stop(){clearInterval(timer); timer=null; document.getElementById("play").textContent="Play";}
document.getElementById("play").onclick=()=>{if(timer){stop();}else{timer=setInterval(step,1000/+document.getElementById("speed").value); document.getElementById("play").textContent="Pause";}};
document.getElementById("speed").onchange=()=>{if(timer){stop(); document.getElementById("play").click();}};
slider.oninput=()=>draw(+slider.value);
draw(D.from);
</script></body></html>"""


def page(path, out):
    head, bots, realm, seconds, squads, notes, end = load(path)
    start, last = min(s for s in seconds if s >= -15), max(seconds)
    data = {
        "title": "%s, match %s: %s against %s%s" % (head["batch"], head["number"], head["a"], head["b"],
                                                  ("; %s, alive %s/%s" % (end.get("End"), end.get("AliveA"), end.get("AliveB"))) if end else " (not finished)"),
        "from": start, "to": last, "realm": realm,
        "order": sorted(bots, key=lambda n: (bots[n]["team"], shape(bots[n]["cls"]), bots[n]["cls"])),
        "bots": {n: {"cls": b["cls"], "build": b["build"], "team": b["team"], "hp": snap_max(seconds, n),
                     "pow": max((snap[n]["pow"] for snap in seconds.values() if n in snap), default=0)} for n, b in bots.items()},
        "seconds": {s: {n: {"x": p["x"], "y": p["y"], "a": p["alive"], "h": p["hp"], "p": p["pow"], "e": p["end"], "m": p["mez"], "s": p["stun"],
                            "r": p["root"], "c": p["casting"], "mv": p["moving"], "t": p.get("target")} for n, p in snaps.items()}
                    for s, snaps in seconds.items() if s >= start},
        "squads": {s: {t: {"order": q["order"], "kill": q.get("kill"), "trained": q.get("trained")} for t, q in teams.items()}
                   for s, teams in squads.items() if s >= start - 30},
        "notes": {s: [e for e in events if e[1] not in ("Spread", "KeepRange")][:14] for s, events in notes.items() if s >= start},
    }
    text = (PAGE.replace("__DATA__", json.dumps(data, separators=(",", ":")))
            .replace("__FIGHTERS__", json.dumps(FIGHTERS)).replace("__HEALERS__", json.dumps(HEALERS)))
    name = os.path.join(out, "replay.html")
    open(name, "w", encoding="utf-8", newline="\n").write(text)
    return name


def main():
    args = sys.argv[1:]
    if not args or args[0] in ("-h", "--help"):
        sys.exit(__doc__)
    path = args[0]

    def option(name, default):
        return args[args.index(name) + 1] if name in args else default

    out = option("--out", os.path.join(os.path.dirname(os.path.abspath(path)), "..", "..", "..", "logs", "replay",
                                       os.path.splitext(os.path.basename(path))[0]))
    out = os.path.abspath(out)
    os.makedirs(out, exist_ok=True)
    print(page(path, out))
    try:
        for name in sheets(path, out, int(option("--from", "-3")), int(option("--step", "5")), int(option("--panels", "12"))):
            print(name)
    except ImportError:
        print("No picture sheets: Pillow is not installed (pip install pillow). The page is written.")


if __name__ == "__main__":
    main()

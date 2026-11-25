"""Copy the /macro macros of one character to another, with their quickbar buttons.

    python copy_macros.py                         lists the characters and how many macros each has
    python copy_macros.py Jade Mycleric           Jade's macros and macro buttons go to Mycleric
    python copy_macros.py Jade Mycleric --layout  also the windows, chat colours, key binds and camera
    python copy_macros.py Jade Mycleric --folder <folder>    another settings folder

The game client keeps each character's macros on this computer, not on the server: one file a
character, <Name>-<number>.ini, under %APPDATA%\\Electronic Arts\\Dark Age of Camelot\\<game>.
Its [Macros] section holds the macros, and the [Quickbar] sections hold the buttons, a macro
button by the number of its macro.

What is copied: every macro (the target's own macros are replaced), and every quickbar button
that is a macro, to the same place on the same bar. A place where the target has a spell, a
style or an ability keeps it, and is named. Nothing else of the target is touched unless
--layout is given. A target that has no file yet (a character that has never left the game)
gets one made from the source, without the source's spells, styles and abilities.

The file that is changed is copied first to <Name>-<number>.ini.bak-<date>-<time> beside it.

Do it while the character you copy to is not in the game (at the character selection, or with
the game closed): the client writes the file again when a character leaves the game.
"""
import glob
import os
import re
import shutil
import subprocess
import sys
import time

MACRO = "52"                      # the kind of a quickbar button that is a macro
LAYOUT = ("Panels", "Chat", "NameOptions", "QuickBinds", "Camera", "ToolTips")


def folders():
    root = os.path.join(os.environ.get("APPDATA", ""), "Electronic Arts", "Dark Age of Camelot")
    return [path for path in glob.glob(os.path.join(root, "*")) if os.path.isdir(path)]


def find(folder, name):
    """The settings file of a character in a folder (the newest when it has several), or None."""
    found = [path for path in glob.glob(os.path.join(folder, "*-*.ini"))
             if os.path.basename(path).rsplit("-", 1)[0].lower() == name.lower()]
    return max(found, key=os.path.getmtime) if found else None


def read(path):
    """The file as (section name, lines) in order; what stands before the first section is under None."""
    with open(path, "rb") as handle:
        text = handle.read().decode("latin-1")
    lines = text.replace("\r\n", "\n").split("\n")
    if lines and lines[-1] == "":
        lines.pop()            # what follows the last line's end
    sections = [(None, [])]
    for line in lines:
        header = re.fullmatch(r"\[(.+)\]\s*", line)
        if header:
            sections.append((header.group(1), []))
        else:
            sections[-1][1].append(line)   # an empty line too: the file is written back as it was
    return sections


def split_tail(lines):
    """A section's lines, and the empty lines that end it."""
    body = list(lines)
    tail = []
    while body and not body[-1].strip():
        tail.append(body.pop())
    return body, tail


def write(path, sections):
    # As the client writes it: one line after the other, no empty line, each ended.
    out = []
    for name, lines in sections:
        if name is not None:
            out.append(f"[{name}]")
        out.extend(lines)
    with open(path, "wb") as handle:
        handle.write("".join(line + "\r\n" for line in out).encode("latin-1"))


def section(sections, name, create=False):
    for found, lines in sections:
        if found == name:
            return lines
    if not create:
        return None
    sections.append((name, []))
    return sections[-1][1]


def hotkeys(lines):
    """Quickbar buttons as {place: (kind, rest of the line)}."""
    keys = {}
    for line in lines or []:
        match = re.fullmatch(r"Hotkey_(\d+)=(\d+),(.*)", line)
        if match:
            keys[int(match.group(1))] = (match.group(2), match.group(3))
    return keys


def copy(source, target, layout):
    """Changes `target` (a list of sections) from `source`; what was done, as lines to print."""
    said = []
    macros = split_tail(section(source, "Macros") or [])[0]
    mine_macros = section(target, "Macros", create=True)
    mine_macros[:] = list(macros) + split_tail(mine_macros)[1]
    said.append(f"{sum(1 for line in macros if line.startswith('Macro_'))} macros copied.")
    bars = [name for name, _ in source if name and name.startswith("Quickbar")]
    for name in bars + [name for name, _ in target if name and name.startswith("Quickbar") and name not in bars]:
        lines = section(source, name) or []
        theirs = {place: key for place, key in hotkeys(lines).items() if key[0] == MACRO}
        if not theirs and not any(key[0] == MACRO for key in hotkeys(section(target, name)).values()):
            continue
        bar = section(target, name, create=True)
        # (The target's own macro buttons go: its macros are replaced, and they would point at other macros now.)
        mine = {place: key for place, key in hotkeys(bar).items() if key[0] != MACRO}
        kept = []
        for place, key in sorted(theirs.items()):
            if place in mine and mine[place][0] != MACRO:
                kept.append(f"{place + 1} ({mine[place][1].split(',')[1] if ',' in mine[place][1] else mine[place][1]})")
                continue
            mine[place] = key
        others, tail = split_tail([line for line in bar if not line.startswith("Hotkey_")])
        if not any(line.startswith("GroupSize=") for line in others):
            others = [line for line in lines if line.startswith("GroupSize=")] + others
        bar[:] = others + [f"Hotkey_{place}={kind},{rest}" for place, (kind, rest) in sorted(mine.items())] + tail
        said.append(f"{name}: {len(theirs) - len(kept)} macro buttons copied" +
                    (f"; kept the target's own at place {', '.join(kept)}" if kept else "") + ".")
    if layout:
        for name in LAYOUT:
            lines = section(source, name)
            if lines is not None:
                section(target, name, create=True)[:] = list(lines)
        said.append("Windows, chat, key binds and camera copied.")
    return said


def fresh(source):
    """A file for a character that has none: the source's, without its spells, styles and abilities."""
    made = []
    for name, lines in source:
        if name is not None and name.startswith("Quickbar"):
            lines = [line for line in lines if not line.startswith("Hotkey_") or hotkeys([line]).popitem()[1][0] == MACRO]
        made.append((name, list(lines)))
    return made


def game_is_open():
    out = subprocess.run(["tasklist"], capture_output=True, text=True).stdout.lower()
    return "game.dll" in out or "camelot.exe" in out


def main(argv):
    layout = "--layout" in argv
    folder = argv[argv.index("--folder") + 1] if "--folder" in argv else None
    names = [word for index, word in enumerate(argv[1:], 1) if not word.startswith("--") and argv[index - 1] != "--folder"]
    if len(names) == 0:
        for where in ([folder] if folder else folders()):
            files = sorted(glob.glob(os.path.join(where, "*-*.ini")), key=os.path.getmtime, reverse=True)
            if not files:
                continue
            print(where)
            for path in files:
                count = sum(1 for line in section(read(path), "Macros") or [] if line.startswith("Macro_"))
                print(f"    {os.path.basename(path).rsplit('-', 1)[0]:<16} {count:>2} macros   {time.strftime('%Y-%m-%d %H:%M', time.localtime(os.path.getmtime(path)))}")
        print("\npython copy_macros.py <from> <to>")
        return 0
    if len(names) != 2:
        print(__doc__)
        return 2
    source_name, target_name = names
    places = [folder] if folder else sorted(folders(), key=lambda where: os.path.getmtime(find(where, source_name) or where) if find(where, source_name) else 0, reverse=True)
    place = next((where for where in places if find(where, source_name)), None)
    if place is None:
        raise SystemExit(f"No settings file for {source_name} was found. Run this with no names to list the characters.")
    source_path = find(place, source_name)
    source = read(source_path)
    if not section(source, "Macros"):
        raise SystemExit(f"{source_name} has no macros ({source_path}).")
    target_path = find(place, target_name)
    if game_is_open():
        print(f"The game is open. This holds only if {target_name} is not in the game now: the client writes the file again when a character leaves.")
    if target_path is None:
        # Named as the source's file is: the number after the name is the game's, not the character's.
        target_path = os.path.join(place, f"{target_name[:1].upper()}{target_name[1:].lower()}-{os.path.basename(source_path).rsplit('-', 1)[1]}")
        target = fresh(source)
        write(target_path, target)
        print(f"{target_name} had no settings file: made {target_path} from {source_name}'s, with its macros, macro buttons, windows and key binds, and without its spells and styles.")
        return 0
    backup = f"{target_path}.bak-{time.strftime('%Y%m%d-%H%M%S')}"
    shutil.copy2(target_path, backup)
    target = read(target_path)
    for line in copy(source, target, layout):
        print(line)
    write(target_path, target)
    print(f"Changed {target_path}\nBackup: {backup}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

"""Regenerate the active mod list in README.md from ModsConfig.xml; commit and push when it changed.

Run: python Tools/modlist.py [--no-push]
Runs hourly from the Windows scheduled task "RimWorld modlist README" (pythonw, hidden).
"""
import os, re, subprocess, sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
GAME = Path(r"M:\Steam\steamapps\common\RimWorld")
WORKSHOP = Path(r"M:\Steam\steamapps\workshop\content\294100")
CONFIG = Path(os.environ["USERPROFILE"]) / r"AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModsConfig.xml"
GITHUB = {"sammael.harmony3strictcompat": "https://github.com/hugalafutro/Harmony3StrictCompat",
          "sammael.localtweaks": "https://github.com/hugalafutro/LocalTweaks"}
START, END = "<!-- MODLIST:START -->", "<!-- MODLIST:END -->"


def about(folder):
    """(packageId lowercased, name) from About/About.xml, or None."""
    f = folder / "About" / "About.xml"
    if not f.is_file():
        return None
    try:
        root = ET.parse(f).getroot()
    except ET.ParseError:
        # a few mods ship sloppy XML; fall back to the direct children by regex
        t = f.read_text(encoding="utf-8", errors="ignore")
        t = re.sub(r"<(modDependencies\w*|loadAfter|loadBefore|incompatibleWith\w*)>.*?</\1>", "", t, flags=re.S)
        pid, name = re.search(r"<packageId>\s*(.*?)\s*</packageId>", t), re.search(r"<name>\s*(.*?)\s*</name>", t)
        return (pid.group(1).lower(), name.group(1) if name else folder.name) if pid else None
    pid = root.findtext("packageId")
    return (pid.strip().lower(), (root.findtext("name") or folder.name).strip()) if pid else None


def index():
    mods = {}
    for kind, base in (("dlc", GAME / "Data"), ("local", GAME / "Mods"), ("workshop", WORKSHOP)):
        if not base.is_dir():
            continue
        for d in base.iterdir():
            a = about(d) if d.is_dir() else None
            if a and a[0] not in mods:  # first wins: DLC, then local, then workshop
                mods[a[0]] = (a[1], kind, d.name)
    return mods


def table(active, mods):
    rows = ["| # | Mod | Package ID |", "|---:|---|---|"]
    for i, pid in enumerate(active, 1):
        name, kind, folder = mods.get(pid, (pid, "missing", ""))
        name = name.replace("|", "\\|")
        if kind == "workshop":
            name = f"[{name}](https://steamcommunity.com/sharedfiles/filedetails/?id={folder})"
        elif pid in GITHUB:
            name = f"[{name}]({GITHUB[pid]})"
        elif kind == "dlc":
            name += " (official)"
        elif kind == "missing":
            name += " (not installed)"
        rows.append(f"| {i} | {name} | `{pid}` |")
    return "\n".join(rows)


def main():
    active = [li.text.strip().lower() for li in ET.parse(CONFIG).getroot().find("activeMods")]
    # ModsConfig's <version> is only rewritten when the game saves ModsConfig, so it lags game updates; the
    # installed build is in Version.txt.
    version = (GAME / "Version.txt").read_text(encoding="utf-8").strip()
    if all(p.startswith("ludeon.") for p in active):
        # the game resets ModsConfig to Core + DLC after a failed load; never publish that
        sys.exit("ModsConfig.xml holds only Core/DLC (reset by a failed load?), README left alone")
    block = (f"{START}\n## Active mod list\n\n{len(active)} mods in load order, RimWorld {version}. "
             f"Generated from ModsConfig.xml by `Tools/modlist.py`.\n\n{table(active, index())}\n{END}")
    readme = REPO / "README.md"
    text = readme.read_text(encoding="utf-8") if readme.exists() else ""
    new = re.sub(re.escape(START) + ".*?" + re.escape(END), lambda _: block, text, flags=re.S) if START in text \
        else text.rstrip() + ("\n\n" if text else "") + block + "\n"
    if new == text:
        return
    readme.write_text(new, encoding="utf-8", newline="\n")
    # The scheduled task runs under pythonw; without CREATE_NO_WINDOW each console git.exe flashes a window.
    git = lambda *a: subprocess.run(["git", "-C", str(REPO), *a], check=True, capture_output=True,
                                    creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    # pathspec commit: never sweeps up other staged work in progress
    git("commit", "-m", f"Update active mod list ({len(active)} mods)", "--", "README.md")
    if "--no-push" not in sys.argv:
        git("push", "-q", "origin", "main")


if __name__ == "__main__":
    main()

# Fix relevance check: lists README fix rows (compat or LocalTweaks) whose named mod, or the game, updated after
# both the row's last edit (git blame) and the last relevance audit (Tools/relevance_audit.txt, ISO local time).
# A flagged row needs a look at the mod's CURRENT code: does it still have the problem, or does it now do the fix
# itself (fingerprints only catch changes to the patched method, not a fix made elsewhere).
# usage: uv run --no-project python -X utf8 -I relevance.py <mod repo dir> [-a]   (-a also lists unflagged rows)
import re, os, sys, subprocess, datetime as dt
W = r"M:\Steam\steamapps\workshop\content\294100"
acf = open(r"M:\Steam\steamapps\workshop\appworkshop_294100.acf", encoding="utf-8").read().split('"WorkshopItemDetails"')[1]
upd = {m[0]: int(m[1]) for m in re.findall(r'"(\d+)"\s*\{[^}]*?"timeupdated"\s*"(\d+)"', acf, re.S)}
cfg = os.path.expandvars(r"%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModsConfig.xml")
active = {s.lower() for s in re.findall(r"<li>([^<]+)</li>", open(cfg, encoding="utf-8").read().split("<activeMods>")[1].split("</activeMods>")[0])}
mods = []  # (norm name, display, wid, t)
for wid, t in upd.items():
    try: about = open(os.path.join(W, wid, "About", "About.xml"), encoding="utf-8-sig", errors="replace").read()
    except OSError: continue
    about = re.sub(r"<(modDependencies\w*|loadAfter\w*|loadBefore\w*|incompatibleWith\w*)>.*?</\1>", "", about, flags=re.S | re.I)
    pid = re.search(r"<packageId>\s*([^<]+?)\s*</packageId>", about, re.I); name = re.search(r"<name>\s*([^<]+?)\s*</name>", about)
    if not pid or pid[1].lower() not in active or not name: continue
    mods.append((name[1], wid, t))
GAME_T = os.path.getmtime(r"M:\Steam\steamapps\common\RimWorld\Version.txt")
STOP = {"vanilla", "continued", "unknown", "vanilla unknown", "core", "seen with", "tooling", "this mod"}
def norm(s): return re.sub(r"[^a-z0-9]+", " ", re.sub(r"\[[^\]]*\]", "", s.lower())).strip()
nm = [(norm(n), n, w, t) for n, w, t in mods]
repo = sys.argv[1]
AUDIT = dt.datetime.fromisoformat(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "relevance_audit.txt")).read().strip()).timestamp()
ALIAS = {"vef": "vanilla expanded framework", "vef mvcf": "vanilla expanded framework", "vfe factory": "vanilla furniture expanded factory",
         "kanban stockpile continue": "kanbanstockpilecontinue", "vfe farming": "vanilla furniture expanded farming", "non uno pinata": "non uno pinata don t drop items", "regrowth plant patches": "regrowth 2"}
blame = subprocess.run(["git", "-C", repo, "blame", "--line-porcelain", "README.md"], capture_output=True, text=True, encoding="utf-8").stdout
times, lines, cur = [], [], None
for l in blame.splitlines():
    if l.startswith("author-time "): cur = int(l.split()[1])
    elif l.startswith("\t"): times.append(cur); lines.append(l[1:])
for i, (t, l) in enumerate(zip(times, lines)):
    t = max(t or 0, AUDIT)
    if "MODLIST:START" in l: break
    if not l.startswith("| ") or l.startswith("| Mod") or l.startswith("|---") or l.startswith("| ---"): continue
    col = l.split("|")[2 if "LocalTweaks" in repo else 1].strip()
    parts = [ALIAS.get(norm(p), norm(p)) for p in re.split(r"\+|,|\(|\)|/| and ", col) if norm(p)]
    hits = set()
    for p in parts:
        for n, disp, w, mt in nm:
            if p == n or (p not in STOP and len(p) > 5 and (p in n or n in p)): hits.add((disp, w, mt))
    newer = [(d, w, mt) for d, w, mt in hits if mt > t]
    game_row = any((p.startswith("vanilla") and p not in [x[0] for x in nm]) or p.split()[0] in ("rimworld", "core", "odyssey", "biotech", "ideology", "anomaly", "royalty") for p in parts)
    vanilla = game_row and GAME_T > t
    flag = "UPDATED" if newer or vanilla else ("nomatch" if not hits and not game_row else "")
    if flag or "-a" in sys.argv:
        print(f"{i+1:4} {flag:8} row {dt.datetime.fromtimestamp(t):%m-%d %H:%M} | {col[:70]}")
        for d, w, mt in sorted(newer): print(f"        {w} {d} updated {dt.datetime.fromtimestamp(mt):%m-%d %H:%M}")
        if vanilla: print(f"        game updated {dt.datetime.fromtimestamp(GAME_T):%m-%d %H:%M}")

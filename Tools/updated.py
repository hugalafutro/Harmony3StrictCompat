# List workshop mods updated since a local time: updated.py 2026-10-22T12:16
# Columns: A(ctive)/-, update time, workshop id, has 1.7 folder, ships a DLL, name | packageId
import re, os, sys, datetime as dt
W = r"M:\Steam\steamapps\workshop\content\294100"
acf = open(r"M:\Steam\steamapps\workshop\appworkshop_294100.acf", encoding="utf-8").read()
det = acf.split('"WorkshopItemDetails"')[1]
upd = {m[0]: int(m[1]) for m in re.findall(r'"(\d+)"\s*\{[^}]*?"timeupdated"\s*"(\d+)"', det, re.S)}
cfg = os.path.expandvars(r"%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModsConfig.xml")
active = {s.lower() for s in re.findall(r"<li>([^<]+)</li>", open(cfg, encoding="utf-8").read().split("<activeMods>")[1].split("</activeMods>")[0])}
cut = dt.datetime.fromisoformat(sys.argv[1]).timestamp()
for wid, t in sorted(upd.items(), key=lambda x: x[1]):
    if t < cut: continue
    try: about = open(os.path.join(W, wid, "About", "About.xml"), encoding="utf-8-sig", errors="replace").read()
    except OSError: continue
    about = re.sub(r"<(modDependencies\w*|loadAfter\w*|loadBefore\w*|incompatibleWith\w*)>.*?</\1>", "", about, flags=re.S | re.I)
    pid = re.search(r"<packageId>\s*([^<]+?)\s*</packageId>", about, re.I)
    name = re.search(r"<name>\s*([^<]+?)\s*</name>", about)
    pid = pid[1].lower() if pid else "?"
    has17 = os.path.isdir(os.path.join(W, wid, "1.7"))
    dll = any(f.endswith(".dll") for r, _, fs in os.walk(os.path.join(W, wid)) for f in fs)
    print(("A" if pid in active else "-"), dt.datetime.fromtimestamp(t).strftime("%m-%d %H:%M"), wid,
          "1.7dir" if has17 else "      ", "dll" if dll else "   ", name[1] if name else "?", "|", pid)

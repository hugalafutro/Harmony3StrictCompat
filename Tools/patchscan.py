# Which active mods touch the same game types as a mod DLL (conflict check for a new mod).
# usage: uv run --no-project --with dnfile python -I patchscan.py <dll> [max-share]
# Lists the RimWorld/Verse types the DLL references that fewer than max-share (default 0.08) of active mods
# reference, then every active mod referencing any of them. Scans all active mod DLLs in parallel.
import os, re, sys
from multiprocessing import Pool
import dnfile

W = r"M:\Steam\steamapps\workshop\content\294100"; LOCAL = r"M:\Steam\steamapps\common\RimWorld\Mods"
CFG = os.path.expandvars(r"%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModsConfig.xml")

def game_types(dll):
    try: t = dnfile.dnPE(dll).net.mdtables.TypeRef
    except Exception: return set()
    return {f"{r.TypeNamespace}.{r.TypeName}" for r in t or [] if str(r.TypeNamespace).startswith(("RimWorld", "Verse"))}

def mod_types(args):
    name, root = args
    found = set()
    for r, _, fs in os.walk(root):
        if "1.6" in r.replace(root, "").split(os.sep) and os.path.isdir(os.path.join(root, "1.7")): continue
        for f in fs:
            if f.endswith(".dll") and f != "0Harmony.dll": found |= game_types(os.path.join(r, f))
    return name, found

def active_mods():
    active = {s.lower() for s in re.findall(r"<li>([^<]+)</li>", open(CFG, encoding="utf-8").read().split("<activeMods>")[1].split("</activeMods>")[0])}
    out = {}
    for base in (W, LOCAL):
        for d in os.listdir(base):
            try: about = open(os.path.join(base, d, "About", "About.xml"), encoding="utf-8-sig", errors="replace").read()
            except OSError: continue
            about = re.sub(r"<(modDependencies\w*|loadAfter\w*|loadBefore\w*|incompatibleWith\w*)>.*?</\1>", "", about, flags=re.S | re.I)
            pid = re.search(r"<packageId>\s*([^<]+?)\s*</packageId>", about, re.I)
            if pid and pid[1].lower() in active:
                out[(re.search(r"<name>\s*([^<]+?)\s*</name>", about) or [0, d])[1]] = os.path.join(base, d)
    return out

if __name__ == "__main__":
    target = os.path.abspath(sys.argv[1]); share = float(sys.argv[2]) if len(sys.argv) > 2 else 0.08
    mine = game_types(target)
    mods = active_mods()
    with Pool() as pool: scanned = dict(pool.map(mod_types, sorted(mods.items()), chunksize=4))
    self_name = next((n for n, r in mods.items() if target.lower().startswith(r.lower())), None)
    scanned.pop(self_name, None)
    count = {t: sum(t in s for s in scanned.values()) for t in mine}
    rare = sorted(t for t in mine if count[t] < share * len(scanned))
    print(f"{os.path.basename(target)}: {len(mine)} game types, {len(rare)} rare (< {share:.0%} of {len(scanned)} active mods):")
    for t in rare: print(f"  {t} ({count[t]})")
    print("mods sharing a rare type:")
    for name, s in sorted(scanned.items()):
        c = [t for t in rare if t in s]
        if c: print(f"  {name}: {', '.join(c)}")

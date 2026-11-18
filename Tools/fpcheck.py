"""Offline version of Harmony3StrictCompat's startup fingerprint check: run before launching the game.
usage: uv run --no-project --with dnfile python -I fpcheck.py [-v]
Reads the Fingerprints dictionary from Source/Harmony3StrictCompat.cs, hashes the same methods in the game
and active-mod DLLs, and prints every method whose IL changed or vanished. Exit code 1 when anything changed.
-v also lists the unchanged ones and the keys whose mod is not active.
First run parses every candidate DLL (about a minute); later runs only re-parse DLLs that changed (seconds)."""
import glob, json, os, re, struct, sys
from functools import partial
from multiprocessing import Pool
import dnfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from patchscan import active_mods  # name -> folder of every active mod

GAME_DLL = r"M:\Steam\steamapps\common\RimWorld\Assembly-CSharp.dll"  # 1.7 keeps it in the game root
SOURCE = os.path.join(HERE, "..", "Source", "Harmony3StrictCompat.cs")
CACHE = os.path.join(HERE, ".fpcheck_cache.json")



def dll_dirs(mod):
    """DLLs the game loads for 1.7. Copy of apicheck.dll_dirs: importing apicheck parses the game DLL (10 s),
    and every Pool worker would repeat that."""
    lf = os.path.join(mod, "LoadFolders.xml")
    dirs = []
    if os.path.exists(lf):
        m = re.search(r"<v1\.7>(.*?)</v1\.7>", open(lf, encoding="utf-8-sig", errors="replace").read(), re.S | re.I)
        if m: dirs = [os.path.join(mod, d.strip().strip("/\\")) if d.strip() not in ("/", "") else mod for d in re.findall(r"<li[^>]*>([^<]*)</li>", m[1])]
    if not dirs:
        vers = sorted((d for d in os.listdir(mod) if re.fullmatch(r"\d+\.\d+", d) and float(d) <= 1.7), key=float)
        dirs = [mod, os.path.join(mod, "Common")] + ([os.path.join(mod, vers[-1])] if vers else [])
    return [p for d in dirs for p in glob.glob(os.path.join(d, "Assemblies", "*.dll"))]


def recorded():
    src = open(SOURCE, encoding="utf-8-sig").read()
    block = src.split("Dictionary<string, ulong> Fingerprints", 1)[1].split("};", 1)[0]
    return {k: int(v, 16) for k, v in re.findall(r'\["([^"]+)"\]\s*=\s*0x([0-9A-Fa-f]+)', block)}


def fnv(b):
    h = 0xcbf29ce484222325
    for x in b:
        h = ((h ^ x) * 0x100000001b3) & 0xffffffffffffffff
    return h


def il(pe, rva):
    d, off = pe.__data__, pe.get_offset_from_rva(rva)
    if d[off] & 3 == 2:  # tiny header
        return d[off + 1:off + 1 + (d[off] >> 2)]
    return d[off + 12:off + 12 + struct.unpack_from("<I", d, off + 4)[0]]


def scan(dll, wanted_types, iter_outer):
    """{full type name: {method name: [hashes]}} for the wanted types this DLL defines, plus the
    compiler-generated classes ("Outer+<Method>d__N") nested in iter_outer types."""
    data = open(dll, "rb").read()
    # cheap pre-filter: namespace and outer type name must both appear in the #Strings heap
    if not any(all(part.encode() in data for part in t.split("+", 1)[0].rsplit(".", 1)) for t in wanted_types):
        return {}
    pe = dnfile.dnPE(data=data)
    tables = pe.net.mdtables
    types = list(tables.TypeDef)
    parent = {r.NestedClass.row_index: r.EnclosingClass.row_index for r in (tables.NestedClass or [])}

    def full(i):  # 1-based TypeDef row index -> "Namespace.Outer+Inner"
        t = types[i - 1]
        if i in parent:
            return f"{full(parent[i])}+{t.TypeName}"
        return f"{t.TypeNamespace}.{t.TypeName}" if str(t.TypeNamespace) else str(t.TypeName)

    out = {}
    for i, t in enumerate(types, 1):
        name = full(i)
        if name in wanted_types or name.split("+<", 1)[0] in iter_outer:
            methods = out.setdefault(name, {})
            for m in t.MethodList:
                if m.row.Rva:
                    methods.setdefault(str(m.row.Name), []).append(fnv(il(pe, m.row.Rva)))
    return out


def safe_scan(dll, wanted_types, iter_outer):
    try:
        return scan(dll, wanted_types, iter_outer)
    except Exception as e:
        print(f"?? {dll}: {e}")
        return None  # not cached, retried next run


def lookup(found, key):
    """Hashes for a dictionary key, or None when no active DLL defines its type (mod absent)."""
    tname, method = key.split(":", 1)
    if tname not in found:
        return None
    if "/" in method:  # "Type:Iterator/MoveNext" = MoveNext of the compiler-generated iterator class
        outer, inner = method.split("/", 1)
        return [h for t, ms in found.items() if t.startswith(f"{tname}+<{outer}>") for h in ms.get(inner, [])]
    return found[tname].get(method, [])


def main():
    verbose = "-v" in sys.argv
    want = recorded()
    wanted_types = {k.split(":", 1)[0] for k in want}
    iter_outer = {k.split(":", 1)[0] for k in want if "/" in k}
    mod_of = {GAME_DLL: "RimWorld (game update)", os.path.join(os.path.dirname(GAME_DLL), "RimWorld.dll"): "RimWorld (game update)"}
    for name, folder in active_mods().items():
        for d in dll_dirs(folder):
            mod_of[d] = name
    dlls = list(mod_of)
    # dnfile parsing is slow (game DLL ~10 s), so results are cached per DLL until its size or mtime changes
    stamp = {d: f"{os.path.getsize(d)}:{os.path.getmtime(d)}" for d in dlls}
    try:
        cache = json.load(open(CACHE, encoding="utf-8"))
    except (OSError, ValueError):
        cache = {}
    if cache.get("types") != sorted(wanted_types):  # dictionary gained or lost a type: rescan everything
        cache = {"types": sorted(wanted_types), "dlls": {}}
    stale = [d for d in dlls if cache["dlls"].get(d, {}).get("stamp") != stamp[d]]
    if stale:
        with Pool() as pool:
            results = pool.map(partial(safe_scan, wanted_types=wanted_types, iter_outer=iter_outer), stale, chunksize=1)
        for d, scanned in zip(stale, results):
            if scanned is not None:
                cache["dlls"][d] = {"stamp": stamp[d], "types": scanned}
    cache["dlls"] = {d: cache["dlls"][d] for d in dlls if d in cache["dlls"]}  # drop inactive mods
    json.dump(cache, open(CACHE, "w", encoding="utf-8"))
    found, owner = {}, {}
    for d, entry in cache["dlls"].items():
        for t, ms in entry["types"].items():
            owner[t] = mod_of[d]
            for m, hs in ms.items():
                found.setdefault(t, {}).setdefault(m, []).extend(hs)

    bad, by_mod = 0, {}
    for key, expected in want.items():
        hashes = lookup(found, key)
        lines = by_mod.setdefault(owner.get(key.split(":", 1)[0], "(mod not active)"), [])
        if hashes is None:
            if verbose: lines.append(f"absent   {key}")
        elif not hashes:
            bad += 1
            lines.append(f"MISSING  {key}: method no longer exists")
        # overloads share one key; the game checks the overload the fix targets, so any match counts
        elif expected not in hashes:
            bad += 1
            lines.append(f"CHANGED  {key}: IL {', '.join(f'0x{h:016X}' for h in sorted(set(hashes)))}, recorded 0x{expected:016X}")
        elif verbose:
            lines.append(f"ok       {key}")
    for mod, lines in by_mod.items():
        if lines:
            print(mod)
            for line in lines: print("  " + line)
    print(f"{len(want)} fingerprints, {bad} changed" if bad else f"{len(want)} fingerprints, none changed")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())

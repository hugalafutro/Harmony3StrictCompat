# Check mod DLLs for references to game/Unity types and members missing in 1.7 (Assembly-CSharp + UnityStubs).
# usage: uv run --no-project --with dnfile python -I apicheck.py <mod folder>...
import os, re, sys, glob, dnfile

GAME = r"M:\Steam\steamapps\common\RimWorld"

def load_defs(path, types):
    pe = dnfile.dnPE(path)
    t = pe.net.mdtables
    tds = t.TypeDef.rows
    nested = {r.NestedClass.row_index: r.EnclosingClass.row_index for r in (t.NestedClass.rows if t.NestedClass else [])}
    def full(i):
        r = tds[i - 1]
        if i in nested: return full(nested[i]) + "/" + str(r.TypeName)
        return (str(r.TypeNamespace) + "." if str(r.TypeNamespace) else "") + str(r.TypeName)
    for i, r in enumerate(tds, 1):
        members = {str(m.row.Name) for m in r.MethodList if m.row} | {str(f.row.Name) for f in r.FieldList if f.row}
        ext = r.Extends
        base = None
        if ext and ext.row_index and ext.table:
            if ext.table.name == "TypeDef": base = full(ext.row_index)
            elif ext.table.name == "TypeRef":
                b = t.TypeRef.rows[ext.row_index - 1]; base = (str(b.TypeNamespace) + "." if str(b.TypeNamespace) else "") + str(b.TypeName)
        types[full(i)] = (members, base)

TYPES = {}
for a in ("Assembly-CSharp.dll", "UnityStubs.dll"):
    load_defs(os.path.join(GAME, a), TYPES)

def has_member(tn, name, depth=0):
    while tn in TYPES and depth < 30:
        members, base = TYPES[tn]
        if name in members: return True
        tn, depth = base, depth + 1
    return tn is not None and tn not in TYPES  # base outside our assemblies (System.Object etc.): can't tell, assume fine

def game_scope(name):
    return name == "Assembly-CSharp" or name.startswith("UnityEngine") or name == "UnityStubs"

def check(dll):
    pe = dnfile.dnPE(dll)
    t = pe.net.mdtables
    if not t.TypeRef: return []
    asm = {i: str(r.Name) for i, r in enumerate(t.AssemblyRef.rows, 1)} if t.AssemblyRef else {}
    refs = {}
    def tr_name(i):
        r = t.TypeRef.rows[i - 1]; sc = r.ResolutionScope
        n = (str(r.TypeNamespace) + "." if str(r.TypeNamespace) else "") + str(r.TypeName)
        if sc and sc.table and sc.table.name == "TypeRef":
            outer, s = tr_name(sc.row_index); return outer + "/" + str(r.TypeName), s
        return n, asm.get(sc.row_index, "?") if sc and sc.table and sc.table.name == "AssemblyRef" else "?"
    out = []
    for i in range(1, len(t.TypeRef.rows) + 1):
        n, s = tr_name(i); refs[i] = (n, s)
        if game_scope(s) and n not in TYPES: out.append(f"type  {n}  [{s}]")
    for r in (t.MemberRef.rows if t.MemberRef else []):
        c = r.Class
        if not (c and c.table and c.table.name == "TypeRef"): continue
        n, s = refs[c.row_index]
        if game_scope(s) and n in TYPES and not has_member(n, str(r.Name)):
            out.append(f"member {n}.{r.Name}")
    for r in (t.ImplMap.rows if t.ImplMap else []):
        mod = str(t.ModuleRef.rows[r.ImportScope.row_index - 1].Name) if r.ImportScope else "?"
        if "mono" in mod.lower(): out.append(f"pinvoke {mod}!{r.ImportName}")
    return sorted(set(out))

def dll_dirs(mod):
    lf = os.path.join(mod, "LoadFolders.xml")
    dirs = []
    if os.path.exists(lf):
        m = re.search(r"<v1\.7>(.*?)</v1\.7>", open(lf, encoding="utf-8-sig", errors="replace").read(), re.S | re.I)
        if m: dirs = [os.path.join(mod, d.strip().strip("/\\")) if d.strip() not in ("/", "") else mod for d in re.findall(r"<li[^>]*>([^<]*)</li>", m[1])]
    if not dirs:
        vers = sorted((d for d in os.listdir(mod) if re.fullmatch(r"\d+\.\d+", d) and float(d) <= 1.7), key=float)
        dirs = [mod, os.path.join(mod, "Common")] + ([os.path.join(mod, vers[-1])] if vers else [])
    return [p for d in dirs for p in glob.glob(os.path.join(d, "Assemblies", "*.dll"))]

# A mod shipping copies of game/engine assemblies is broken regardless of API use.
bundled = re.compile(r"^(Assembly-CSharp(-firstpass)?|UnityEngine(\..*)?|Unity\..*)\.dll$", re.I)
skip = re.compile(r"^(0Harmony|HarmonyMod|System\..*|Mono\..*|Microsoft\..*|netstandard)\.dll$", re.I)
if __name__ == "__main__":
    for mod in sys.argv[1:]:
        for dll in dll_dirs(mod):
            base = os.path.basename(dll)
            if bundled.match(base):
                print(f"!! {os.path.relpath(dll, os.path.dirname(mod))}: bundled game/engine assembly"); continue
            if skip.match(base): continue
            try: bad = check(dll)
            except Exception as e: bad = [f"parse error {e}"]
            if bad:
                print(f"== {os.path.relpath(dll, os.path.dirname(mod))}")
                for b in bad: print("   ", b)
    print("done")

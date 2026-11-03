"""FNV-1a 64 fingerprint of a method's IL body, matching Harmony3StrictCompat's startup check.
usage: uv run --no-project --with dnfile python -I ilhash.py <dll> [<Type:Method> ...]
With no method names it lists every type/method and its hash."""
import struct, sys
import dnfile

def body(pe, m):
    off = pe.get_offset_from_rva(m.Rva)
    b = pe.__data__[off:off + 65536]
    if b[0] & 3 == 2:
        return b[1:1 + (b[0] >> 2)]
    size = struct.unpack_from('<I', b, 4)[0]
    return b[12:12 + size]

def fnv(b):
    h = 0xcbf29ce484222325
    for x in b:
        h = ((h ^ x) * 0x100000001b3) & 0xffffffffffffffff
    return h

pe = dnfile.dnPE(sys.argv[1])
want = set(sys.argv[2:])
for t in pe.net.mdtables.TypeDef:
    ns = str(t.TypeNamespace); full = f"{ns}.{t.TypeName}" if ns else str(t.TypeName)
    for m in t.MethodList:
        if not m.row.Rva: continue
        name = f"{full}:{m.row.Name}"
        if want and name not in want: continue
        print(f"{name} 0x{fnv(body(pe, m.row)):016X} il={len(body(pe, m.row))}")

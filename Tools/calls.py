# List call targets inside a method: calls.py <dll> <TypeName> <MethodName>
import sys, struct, dnfile
pe = dnfile.dnPE(sys.argv[1]); t = pe.net.mdtables
def name(tok):
    tab, row = tok >> 24, tok & 0xFFFFFF
    if tab == 0x0A:
        r = t.MemberRef.rows[row-1]; c = r.Class
        owner = str(t.TypeRef.rows[c.row_index-1].TypeName) if c.table.name == 'TypeRef' else c.table.name
        return f"{owner}.{r.Name}"
    if tab == 0x06: return f"(def).{t.MethodDef.rows[row-1].Name}"
    if tab == 0x2B: return f"methodspec#{row}"
    return hex(tok)
for td in t.TypeDef.rows:
    if str(td.TypeName) != sys.argv[2]: continue
    for m in td.MethodList:
        if str(m.row.Name) != sys.argv[3]: continue
        off = pe.get_offset_from_rva(m.row.Rva); d = pe.__data__; h = d[off]
        if h & 3 == 2: size, code = h >> 2, off + 1
        else: size = struct.unpack_from('<I', d, off + 4)[0]; code = off + 4 * (struct.unpack_from('<H', d, off)[0] >> 12)
        body = d[code:code+size]
        for j in range(len(body) - 4):
            if body[j] in (0x28, 0x6F, 0x73):
                tok = struct.unpack_from('<I', body, j+1)[0]
                if tok >> 24 in (0x0A, 0x06, 0x2B) and (tok & 0xFFFFFF) > 0: print(hex(body[j]), name(tok))

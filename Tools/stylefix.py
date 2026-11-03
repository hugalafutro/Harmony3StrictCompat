import sys, re, zstandard
path=sys.argv[1]
raw=open(path,'rb').read()
frames=raw.count(b'\x28\xb5\x2f\xfd'); print('frames', frames, 'compressed', len(raw))
data=zstandard.ZstdDecompressor().decompressobj().decompress(raw) if frames==1 else None
if data is None:
    import io; data=zstandard.ZstdDecompressor().stream_reader(io.BytesIO(raw)).read()
text=data.decode('utf-8')
n=0
def fix(m):
    global n; n+=1
    kind='Dresser' if m.group(1).startswith('Dresser') else 'EndTable'
    return m.group(0)+f'<sourcePrecept>null</sourcePrecept><styleDef>AM_Mesoamerican{kind}</styleDef>'
# only things whose id block is NOT already followed by a styleDef inside the same thing: check next 3000 chars for styleDef before </thing>
def guard(m):
    tail=text[m.end():m.end()+4000]
    seg=tail.split('</thing>',1)[0]
    if '<styleDef>' in seg: return m.group(0)
    return fix(m)
new=re.sub(r'<id>((?:Dresser|EndTable)\d+)</id>', guard, text)
print('patched', n)
out=zstandard.ZstdCompressor(level=3).compress(new.encode('utf-8'))
open(path,'wb').write(out); print('written', len(out))

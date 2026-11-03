import sys, re, zstandard, xml.etree.ElementTree as ET
def load(path):
    raw=open(path,'rb').read()
    if raw[:4]==b'\x28\xb5\x2f\xfd':
        d=zstandard.ZstdDecompressor(); out=bytearray(); off=0
        while off<len(raw):
            # decompress consecutive frames
            with d.stream_reader(raw[off:]) as r: chunk=r.read()
            out+=chunk
            # find next frame start
            n=raw.find(b'\x28\xb5\x2f\xfd', off+4)
            if n<0: break
            off=n
        return bytes(out)
    return raw
for path in sys.argv[1:]:
    data=load(path); print('==',path.split('#')[-1], len(data),'bytes', data[:60])
    root=ET.fromstring(data)
    for th in root.iter('thing'):
        i=th.findtext('id') or ''
        if re.match(r'(Dresser|EndTable)\d+$', i):
            sp=[e.text for e in th.findall('sourcePrecept')]; sd=[e.text for e in th.findall('styleDef')]
            print(i, 'sourcePrecept',sp,'styleDef',sd, 'def',th.findtext('def'))

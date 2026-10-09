"""Technical RGBA -> PSX 4bpp packing of existing approved item art.

No characters/icons are redrawn. Only verified unused atlas bytes are filled;
all original glyphs, locale text, dimensions and original CLUTs stay unchanged.
"""
import argparse, hashlib, json, struct
from pathlib import Path
from PIL import Image

KEYS=['venom-flask','oil-flask','fire-flask','ice-flask','thunder-flask',
      'essence-of-silence','essence-of-blindness','essence-of-slowness',
      'essence-of-acceleration','essence-of-regeneration','essence-of-flight']
LOCALES=('en','de','fr','ja') # These are the four actual localized item_01 resources.
ROOT=Path(__file__).resolve().parents[1]
def sha(data): return hashlib.sha256(data).hexdigest().upper()
def decode(glyph,palette):
    words=struct.unpack('<16H',palette)
    colors=[((w&31)*255//31,((w>>5)&31)*255//31,((w>>10)&31)*255//31,0 if i==0 else 255) for i,w in enumerate(words)]
    image=Image.new('RGBA',(16,16));image.putdata([colors[n] for b in glyph for n in (b&15,b>>4)])
    return image
def convert(source):
    im=Image.open(source).convert('RGBA').resize((16,16),Image.Resampling.LANCZOS)
    pixels=list(im.getdata()); opaque=[p[:3] for p in pixels if p[3]>=96]
    if len(opaque)<12: raise ValueError('Empty bottle: '+str(source))
    strip=Image.new('RGB',(len(opaque),1));strip.putdata(opaque)
    q=strip.quantize(colors=15,method=Image.Quantize.MEDIANCUT)
    p=q.getpalette();colors=[tuple(p[n*3:n*3+3]) for n in range(15)]
    words=[0]+[(r*31//255)|((g*31//255)<<5)|((b*31//255)<<10) or 0x8000 for r,g,b in colors]
    actual=[((w&31)*255//31,((w>>5)&31)*255//31,((w>>10)&31)*255//31) for w in words[1:]]
    indexes=[0 if a<96 else 1+min(range(15),key=lambda n:sum((c-actual[n][j])**2 for j,c in enumerate((r,g,b)))) for r,g,b,a in pixels]
    glyph=bytes(indexes[n]|(indexes[n+1]<<4) for n in range(0,256,2))
    return glyph,struct.pack('<16H',*words)
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);ap.add_argument('--verify',action='store_true');args=ap.parse_args()
    out=args.output.resolve()
    if args.verify:
        proof=json.loads((out/'world-bottle-atlas.json').read_text())
        if [e['Key'] for e in proof['Entries']]!=KEYS or len(proof['Resources'])!=4:raise ValueError('Unexpected atlas catalog')
        allowed=set();entries=proof['Entries']
        for i,e in enumerate(entries):
            palette=bytes.fromhex(e['PaletteHex']);glyph=bytes.fromhex(e['GlyphHex'])
            if len(palette)!=32 or len(glyph)!=128 or e['ItemId']!=261+i or e['U']!=240 or e['V']!=21+16*i or e['Clut']!=((277+i//3)<<6)|(56+i%3):raise ValueError('Invalid native descriptor')
            allowed.update(range(i//3*128+i%3*32,i//3*128+i%3*32+32))
            for row in range(16):allowed.update(range((i*16+row)*128+120,(i*16+row)*128+128))
        for locale in LOCALES:
            relative='FFTIVC/data/enhanced/fftpack/tex/item/item_01.'+locale+'.tex';record=proof['Resources'][relative]
            original=(ROOT/'art/world-native-reference/fftpack/tex/item'/('item_01.'+locale+'.tex')).read_bytes();modified=(out/relative).read_bytes()
            if len(original)!=len(modified) or sha(original)!=record['OriginalSha256'] or sha(modified)!=record['Sha256'] or any(original[n] for n in allowed):raise ValueError('Unverified original or modified atlas')
            if any(a!=b and n not in allowed for n,(a,b) in enumerate(zip(original,modified))):raise ValueError('Existing original atlas pixel changed')
            for i,e in enumerate(entries):
                if modified[i//3*128+i%3*32:i//3*128+i%3*32+32]!=bytes.fromhex(e['PaletteHex']):raise ValueError('Palette not uploaded')
                glyph=bytes.fromhex(e['GlyphHex'])
                if any(modified[(i*16+y)*128+120:(i*16+y)*128+128]!=glyph[y*8:y*8+8] for y in range(16)):raise ValueError('Glyph not uploaded')
        print('PASS: all eleven actual 4bpp glyphs/CLUTs and four localized resource hashes verified; all original atlas bytes outside audited blank regions unchanged.');return
    if out.exists() or not out.is_relative_to(ROOT/'builds'): raise ValueError('Unique workspace build required')
    entries=[];patches={};sheet=Image.new('RGBA',(16*11,16))
    for i,key in enumerate(KEYS):
        source=ROOT/('art/venom-v2/normalized/venom-flask.item.png' if i==0 else 'art/full-v1/'+key+'/normalized/'+key+'.item.png')
        glyph,palette=convert(source)
        # Raw body starts at VRAM UV row 21, page X=896/Y=256.
        # Three palettes per blank row; unused rightmost column holds glyphs.
        offset=(i//3)*128+(i%3)*32
        for n,b in enumerate(palette):patches[offset+n]=b
        for row in range(16):
            offset=(i*16+row)*128+120
            for n,b in enumerate(glyph[row*8:row*8+8]):patches[offset+n]=b
        clut=((277+i//3)<<6)|(56+i%3)
        entries.append(dict(Key=key,ItemId=261+i,U=240,V=21+16*i,Clut=clut,
                            SourceSha256=sha(source.read_bytes()),PaletteHex=palette.hex(),GlyphHex=glyph.hex()))
        sheet.paste(decode(glyph,palette),(16*i,0))
    out.mkdir(parents=True)
    resources={}
    for locale in LOCALES:
        name='item_01.'+locale+'.tex'
        src=ROOT/'art/world-native-reference/fftpack/tex/item'/name;original=src.read_bytes()
        if len(original)!=0x7580 or any(original[n] for n in patches):raise ValueError('Atlas free-space assertion failed: '+name)
        new=bytearray(original)
        for n,b in patches.items():new[n]=b
        if any(a!=b and n not in patches for n,(a,b) in enumerate(zip(original,new))):raise ValueError('Original glyph changed')
        dest=out/'FFTIVC/data/enhanced/fftpack/tex/item'/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(new)
        resources[str(dest.relative_to(out)).replace('\\','/')]=dict(OriginalSha256=sha(original),Sha256=sha(new),Length=len(new),ChangedBytes=sum(a!=b for a,b in zip(original,new)))
    sheet.resize((176*4,16*4),Image.Resampling.NEAREST).save(out/'world-bottles-preview.png')
    (out/'world-bottle-atlas.json').write_text(json.dumps(dict(Version=1,Width=256,Height=235,VramOrigin=[896,256],BodyYOffset=21,OriginalGlyphsPreserved=True,OriginalClutsPreserved=True,Entries=entries,Resources=resources),indent=2)+'\n')
    print('PASS: eleven existing icons packed into separate native 16x16 bottles/palettes; all four original localized atlases preserved outside audited blank space.')
if __name__=='__main__':main()

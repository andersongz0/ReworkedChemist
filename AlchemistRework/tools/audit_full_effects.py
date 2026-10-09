"""Read-only native effect catalog; never changes executable or saves."""
from audit_native_layout import NativeImage, GAME
import sqlite3, struct
i=NativeImage((GAME/'FFT_enhanced.exe').read_bytes())
d=sqlite3.connect('FFTModLoader_Prototype_v0.10.30/analysis-ff16tools-game-01040/sqlite/original-0004.en.sqlite')
print('ABILITY SCHEMA',d.execute('PRAGMA table_info([Ability-en])').fetchall())
print('STATUS/NATIVE ACTIONS')
for key,name in d.execute('SELECT Key,Name FROM [Ability-en]'):
    if name and any(k in name.lower() for k in ('oil','slow','haste','regen','float','blind','silence','bomb','stone','poison','fire','blizzard','thunder')):
        print(key,name,i.read(0x789620+key*20,20).hex() if key<368 else '')
print('FORMULA POINTERS',[(n,hex(p-i.image_base)) for n,p in enumerate(struct.unpack('<107Q',i.read(0x682bc8,856)))])
print('STATUS ENTRIES',[(n,i.read(0x80fba0+n*6,6).hex()) for n in range(128)])

"""Build the fifteen-action complete Chemist playable test. Does not deploy or launch."""
import argparse
from contextlib import closing
import hashlib
import json
from pathlib import Path
import shutil
import sqlite3
import subprocess
from build_venom_content import ROOT, BASE, CLI, LOCALES, assert_same_tables
from build_jp_test import EXPECTED
from audit_native_layout import SUPPORTED_SHA256

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def main():
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    out=a.output.resolve();builds=(ROOT/'builds').resolve()
    if not out.is_relative_to(builds) or out==builds or out.exists():
        raise ValueError('Use a NEW build directory under AlchemistRework/builds.')
    stage=ROOT/'builds/chemist-full-content-001'
    proof=json.loads((stage/'content-integration.json').read_text(encoding='utf-8'))
    for name,digest in proof['PayloadHashes'].items():
        if sha(stage/name)!=digest:raise ValueError('Staged resource changed: '+name)
    plan=json.loads((stage/'native/playable-relocation-plan.json').read_text(encoding='utf-8'))
    if plan.get('Installable') is not True or plan.get('ReviewedFor')!='FullChemist15Actions':
        raise ValueError('Playable-test relocation review incomplete.')
    dll=ROOT/'PlayableRuntime/bin/Release/net8.0-windows7.0/FFTModLoader.ReworkedChemist.VenomTest.dll'
    if not dll.exists():raise FileNotFoundError('Build PlayableRuntime first.')
    mod=out/'Reworked Chemist - Venom Test';mod.mkdir(parents=True)
    shutil.copytree(stage/'resources/FFTIVC',mod/'FFTIVC')
    shutil.copytree(stage/'native',mod/'native')
    shutil.copy2(dll,mod/dll.name)
    shutil.copy2(dll.with_suffix('.deps.json'),mod/dll.with_suffix('.deps.json').name)
    bridge=ROOT/'builds/native-bridge-001/FFTModLoader.ContentExpansion.Native.dll'
    if sha(bridge)!='6BC8599CB05D528518FA4F6D83A20EA98B0746A6BBF839A031738900366B98E4':
        raise ValueError('Native bridge differs from the verified x64 bridge.')
    shutil.copy2(bridge,mod/bridge.name)
    # Keep full original tables: omitted originals would be interpreted as removals.
    databases=out/'databases';databases.mkdir()
    for locale in LOCALES:
        db=databases/f'playable.{locale}.sqlite';shutil.copy2(stage/f'databases/venom.{locale}.sqlite',db)
        table=f'Ability-{locale}'
        with closing(sqlite3.connect(db)) as conn:
            with conn:
                for identity,cost in EXPECTED.items():
                    if conn.execute(f'UPDATE "{table}" SET JpCost1=?,JpCost2=? WHERE Key=?',
                                    (cost&255,cost>>8,identity)).rowcount!=1:raise ValueError('Missing retained ability.')
        # Independently prove every original field except those eight JP bytes.
        with closing(sqlite3.connect(BASE/f'original-0004.{locale}.sqlite')) as original,closing(sqlite3.connect(db)) as merged:
            cols=[r[1] for r in merged.execute(f'PRAGMA table_info("{table}")')]
            lo,hi=cols.index('JpCost1'),cols.index('JpCost2');key=cols.index('Key')
            wanted=[]
            for row in original.execute(f'SELECT * FROM "{table}" ORDER BY Key'):
                row=list(row)
                if row[key] in EXPECTED:row[lo],row[hi]=EXPECTED[row[key]]&255,EXPECTED[row[key]]>>8
                wanted.append(tuple(row))
            if wanted!=merged.execute(f'SELECT * FROM "{table}" WHERE Key<512 ORDER BY Key').fetchall():
                raise ValueError('Unrelated original ability field changed.')
        converted=out/'roundtrip'/locale;converted.mkdir(parents=True)
        subprocess.run([str(CLI),'sqlite-to-nxd','-i',str(db),'-o',str(converted),'-g','fft'],check=True,stdout=subprocess.DEVNULL)
        checked=databases/f'playable.{locale}.roundtrip.sqlite'
        subprocess.run([str(CLI),'nxd-to-sqlite','-i',str(converted),'-o',str(checked),'-g','fft'],check=True,stdout=subprocess.DEVNULL)
        assert_same_tables(db,checked)
        for file in converted.iterdir():shutil.copy2(file,mod/'FFTIVC/data/enhanced/nxd'/file.name)
    config=dict(ModId='ffttic.tests.reworkedchemist.venom',ModName='Reworked Chemist - COMPLETE PLAYABLE TEST',
        ModAuthor='ZeroDS',ProjectUrl='https://github.com/andersongz0/ReworkedChemist',ModVersion='0.2.0-all-items-actions',
        ModDescription='Complete 15-action Chemist test; 11 new consumables, individual JP/stock/save, shops from chapter1, range4 single-target, elemental60 without Faith, statuses respect immunities. Previous Venom save keys retained. Enhanced supported version only.',
        ModDll=dll.name,CanUnload=False,HasExports=False,IsLibrary=False,IsUniversalMod=False,
        ModDependencies=['fftivc.utility.modloader','fftmodloader.jobexpansion','reloaded.sharedlib.hooks'],
        OptionalDependencies=[],SupportedAppId=['fft_enhanced.exe'])
    (mod/'ModConfig.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    hashes={str(f.relative_to(mod)).replace('\\','/'):sha(f) for f in mod.rglob('*') if f.is_file()}
    manifest=dict(Stage='FullChemistPlayableTest',ExecutableSha256=SUPPORTED_SHA256,FullReworkedChemistReady=True,
        NewItemId=261,NewAbilityId=513,NewAbilityIconId=55,ReservedAbilityId=512,Actions=15,NewItemCount=11,
        ItemIds=list(range(261,272)),AbilityIds=list(range(513,524)),AbilityIconIds=list(range(55,66)),
        JpCosts=EXPECTED,VenomJp=70,VenomPriceGil=140,Range=4,NativeAoeRadius=0,Consumes=1,
        Locales=LOCALES,NxdRoundTripVerified=True,NativeSaveBytesChanged=False,SeparateSaveState=True,
        GameplayTested=False,ModFileHashes=hashes,ModDirectory=str(mod))
    (out/'package-verification.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    print(f'PASS: COMPLETE 15-action/11-extra playable-test package, {len(hashes)} files, 7 complete localized tables: {mod}')

if __name__=='__main__':main()

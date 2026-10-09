"""Integrate Venom's staged native record, full localized tables and icon assets.

This is NOT a playable mod and deliberately contains no ModConfig or installer.
Native ability, shop/session/save and combat activation are separate release
gates. Never put these expanded tables into an unexpanded game's override path.
"""
import argparse
from contextlib import closing
import hashlib
import json
from pathlib import Path
import shutil
import sqlite3
import struct
import subprocess

from audit_native_layout import NativeImage, GAME, SUPPORTED_SHA256
from plan_content import read_definition, validate, DATABASE

ROOT = Path(__file__).resolve().parents[1]
WORKSPACE = ROOT.parent
BASE = WORKSPACE / 'FFTModLoader_Prototype_v0.10.30/analysis-ff16tools-game-01040/sqlite'
CLI = WORKSPACE / 'FF16 Tools/win-x64/FF16Tools.CLI.exe'
LOCALES = ('en', 'de', 'fr', 'ja', 'ko', 'cs', 'ct')


def common_record(image, price):
    if type(price) is not int or not 0 <= price <= 65535:
        raise ValueError('Native price must be uint16.')
    original = image.read(0x80EA90 + 240 * 12, 12)
    result = bytearray(original)
    struct.pack_into('<H', result, 8, price)
    # Preserve flags/type/legacy sprite. These bytes are NOT the new UI TEX ID
    # and are not an implementation of Poison. No inventing secondary fields.
    if bytes(result[:8] + result[10:]) != original[:8] + original[10:]:
        raise ValueError('Native price-only record builder changed other fields.')
    return bytes(result)


def medicine_record(image):
    # These offsets/fields are confirmed by the user's decomposed project and
    # the original code: itemChemist={formula,z,inflictStatus}, 3 bytes. Reuse
    # the existing Poison mask, NOT a newly invented status or item formula.
    poison = image.read(0x789620 + 28 * 20, 20)  # original, NOT uninitialized NEX copy
    if poison != bytes.fromhex('0401020000e242000aa0002703060000ffffffff'):
        raise ValueError('Supported native Poison action layout changed.')
    status_index = poison[11]
    status = image.read(0x80FBA0 + status_index * 6, 6)
    if status != bytes.fromhex('800000008000'):
        raise ValueError('Native Poison status mask/type differs from audited evidence.')
    pointer = struct.unpack('<Q', image.read(0x682BC8 + 56 * 8, 8))[0]
    if pointer != image.image_base + 0x30698C:
        raise ValueError('Native Inflict_Status dispatch differs from audited evidence.')
    # Native z is unused by Inflict_Status. The status type has no random/remove
    # bits and uses the positive infliction branch. This DOES NOT set the
    # battle hit-percent to 100, route a wide item ID, or consume an item.
    return bytes((56, 0, status_index))


def export_rows(source, target, locale, item_id, ability_id, icon_id, action):
    if type(item_id) is not int or not 261 <= item_id < 1024:
        raise ValueError('Extra item must avoid originals and fit audited stock width.')
    if type(ability_id) is not int or not 513 <= ability_id < 1024:
        raise ValueError('Original ability IDs and reserved action 512 cannot be reused.')
    if type(icon_id) is not int or not 55 <= icon_id <= 999:
        raise ValueError('Extra ability icon cannot replace original rows.')
    if target.exists():
        raise FileExistsError(target)
    staged = []
    with closing(sqlite3.connect(source.resolve().as_uri() + '?mode=ro', uri=True)) as native:
        unions = native.execute('SELECT * FROM _uniontypes ORDER BY id').fetchall()
        meta = native.execute("SELECT sql FROM sqlite_master WHERE name='_uniontypes'").fetchone()[0]
        for name, template_id, new_id, original_count in (
            (f'Item-{locale}', 240, item_id, 261),
            (f'Ability-{locale}', 368, ability_id, 512)):
            schema = native.execute('SELECT sql FROM sqlite_master WHERE name=?', (name,)).fetchone()[0]
            columns = [r[1] for r in native.execute(f'PRAGMA table_info("{name}")')]
            rows = native.execute(f'SELECT * FROM "{name}" ORDER BY Key').fetchall()
            key = columns.index('Key')
            if len(rows) != original_count or {r[key] for r in rows} != set(range(original_count)):
                raise ValueError(f'Incomplete original baseline: {name}')
            values = dict(zip(columns, next(r for r in rows if r[key] == template_id)))
            values.update(Key=new_id, Name=action['Name'], DLCFlags=0)
            if name.startswith('Item-'):
                values.update(NameSingular='venom flask', NamePlural='venom flasks', Name2=action['Name'],
                              Description='Alchemical flask. Inflicts Poison, respecting status immunities.',
                              SortOrder=6017, Comment='Additive Venom fixture; native effect activation pending.')
            else:
                values.update(IconId=icon_id, Description='Consumes one venom flask to inflict Poison. Range: 4.',
                              JpCost1=action['JpCost'] & 255, JpCost2=action['JpCost'] >> 8,
                              IsRandomDamage=0, IsRandomStatus=0,
                              Comment='Expanded ID fixture. Never install before native ability expansion.')
            staged.append((name, schema, columns, rows, tuple(values[c] for c in columns)))
    with closing(sqlite3.connect(target)) as built:
        with built:
            built.execute(meta); built.executemany('INSERT INTO _uniontypes VALUES (?,?)', unions)
            for name, schema, columns, rows, extra in staged:
                built.execute(schema)
                placeholders = ','.join('?' for _ in columns)
                built.executemany(f'INSERT INTO "{name}" VALUES ({placeholders})', [*rows, extra])
                if built.execute(f'SELECT * FROM "{name}" WHERE Key<? ORDER BY Key', (len(rows),)).fetchall() != rows:
                    raise ValueError('Original rows changed.')
    return {name: len(rows) for name, _, _, rows, _ in staged}


def assert_same_tables(expected, actual):
    with closing(sqlite3.connect(expected.resolve().as_uri() + '?mode=ro', uri=True)) as before, \
         closing(sqlite3.connect(actual.resolve().as_uri() + '?mode=ro', uri=True)) as after:
        names = [r[0] for r in before.execute("SELECT name FROM sqlite_master WHERE type='table' AND name != '_uniontypes'")]
        actual_names = {r[0] for r in after.execute("SELECT name FROM sqlite_master WHERE type='table' AND name != '_uniontypes'")}
        if set(names) != actual_names:
            raise ValueError('Unexpected tables after NXD round trip.')
        for name in names:
            if before.execute(f'SELECT * FROM "{name}" ORDER BY Key').fetchall() != after.execute(f'SELECT * FROM "{name}" ORDER BY Key').fetchall():
                raise ValueError(f'NXD round trip changed {name}.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--icons', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--exe', type=Path, default=GAME / 'FFT_enhanced.exe')
    parser.add_argument('--ability-id', type=int, default=513)
    args = parser.parse_args()
    out = args.output.resolve()
    # Keep non-installable development content OUTSIDE every game override tree.
    builds = (ROOT / 'builds').resolve()
    if not out.is_relative_to(builds) or out == builds or out.exists():
        raise ValueError('Use a NEW staging directory under AlchemistRework/builds.')
    icon_root = args.icons.resolve()
    icon_manifest = read_definition(icon_root / 'icon-assets.json')
    if icon_manifest.get('Stage') != 'NativeIconAssetsOnly' or icon_manifest.get('Installable') is not False:
        raise ValueError('Expected independently verified, staged icon assets.')
    definition = read_definition(ROOT / 'Alchemist.definition.json')
    with closing(sqlite3.connect(DATABASE.resolve().as_uri() + '?mode=ro', uri=True)) as db:
        validate(definition, db)
    action = next(a for a in definition['Actions'] if a['Key'] == 'venom-flask')
    if (action['JpCost'], action['PriceGil'], action['Status'], action['ConsumeCount']) != (70, 140, 'Poison', 1):
        raise ValueError('Venom fixture differs from approved balance.')
    item_id, icon_id = icon_manifest['CandidateItemId'], icon_manifest['CandidateAbilityIconId']
    image = NativeImage(args.exe.read_bytes())
    record = common_record(image, action['PriceGil'])
    secondary = medicine_record(image)
    if not 513 <= args.ability_id < 1024:
        raise ValueError('Invalid candidate ability; 512 is reserved.')
    # Verify icon proof instead of silently copying changed art/native containers.
    resources = []
    for resource in icon_manifest['Resources']:
        texture = icon_root / resource['Texture']
        parts = icon_root / resource['TextureParts']
        if not resource['ExactRgbaRoundTrip'] or not resource['NativePartGeometryPreserved'] or \
           hashlib.sha256(texture.read_bytes()).hexdigest() != resource['TextureSha256']:
            raise ValueError('Icon proof no longer matches resources.')
        resources.extend((texture, parts))
    resources.append(icon_root / 'resources/FFTIVC/data/enhanced/nxd/uiabilityicon.nxd')
    assert_same_tables(icon_root / 'ability-icons.sqlite', icon_root / 'roundtrip.sqlite')
    out.mkdir(parents=True)
    for file in resources:
        target = out / file.relative_to(icon_root)
        target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(file, target)
    native = out / 'native'; native.mkdir()
    (native / 'venom.item-common.bin').write_bytes(record)
    (native / 'venom.item-medicine.bin').write_bytes(secondary)
    databases = out / 'databases'; databases.mkdir()
    nxd = out / 'resources/FFTIVC/data/enhanced/nxd'
    preserved = {}
    for locale in LOCALES:
        database = databases / f'venom.{locale}.sqlite'
        preserved.update(export_rows(BASE / f'original-0004.{locale}.sqlite', database, locale,
                                     item_id, args.ability_id, icon_id, action))
        # Isolate each converter input for an independent, table-exact round trip.
        converted = out / 'roundtrip' / locale; converted.mkdir(parents=True)
        subprocess.run([str(CLI), 'sqlite-to-nxd', '-i', str(database), '-o', str(converted), '-g', 'fft'],
                       check=True, stdout=subprocess.DEVNULL)
        expected_files = {f'item.{locale}.nxd', f'ability.{locale}.nxd'}
        if {f.name for f in converted.iterdir()} != expected_files:
            raise ValueError('Unexpected converter payload.')
        check = databases / f'venom.{locale}.roundtrip.sqlite'
        subprocess.run([str(CLI), 'nxd-to-sqlite', '-i', str(converted), '-o', str(check), '-g', 'fft'],
                       check=True, stdout=subprocess.DEVNULL)
        assert_same_tables(database, check)
        for file in converted.iterdir(): shutil.copy2(file, nxd / file.name)
    payload_hashes = {str(p.relative_to(out)): hashlib.sha256(p.read_bytes()).hexdigest().upper()
                      for root in (out / 'resources', native) for p in root.rglob('*') if p.is_file()}
    manifest = dict(SchemaVersion=1, Stage='VenomContentIntegration', Installable=False,
                    FullReworkedChemistReady=False, ExecutableSha256=SUPPORTED_SHA256,
                    CandidateItemId=item_id, CandidateAbilityId=args.ability_id, CandidateAbilityIconId=icon_id,
                    ReservedLiveIds=False, NativeCommonRecordSize=len(record), NativePriceGil=140,
                    NativeMedicineRecordSize=len(secondary), NativeMedicineFormula=56,
                    NativeMedicineInflictStatus=39, NativePoisonStatusReused=True,
                    BattleHitPercentOverrideActive=False,
                    JpCost=70, DesiredEffect='Poison', DesiredConsumeCount=1,
                    PreservedOriginalRows=preserved, NxdRoundTripVerified=True, Locales=list(LOCALES),
                    LinkedUiIconVariants=['item-large', 'item-small', 'ability'],
                    NativeCommonRecordReady=True, NativeSecondaryItemRouteReady=False,
                    NativeSecondaryMedicineRecordReady=True,
                    LegacySpriteIconReady=False, AbilityTableExpansionActive=False,
                    ShopIntegrationActive=False, BattleIntegrationActive=False, SaveIntegrationActive=False,
                    PayloadHashes=payload_hashes,
                    Warnings=['No ModConfig, no live hook, no installation. Expanded NXD is unsafe without its native host.',
                              'Candidate IDs are not allocated against the final enabled mod set.',
                              'Native sprite byte is unchanged Potion fallback, not the purple UI TEX ID.',
                              'English names/descriptions of this new fixture are used in all seven locales.'])
    (out / 'content-integration.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f'PASS: Venom native record + 7 full localized item/ability tables + 3 icon variants: {out}')
    print('STAGING ONLY. No live ID allocation, game installation, shop purchase, native effect or save activation.')


if __name__ == '__main__': main()

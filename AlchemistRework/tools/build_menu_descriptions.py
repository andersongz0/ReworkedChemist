"""Change only the three group-action help texts, with a full NXD round trip."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import sqlite3
import subprocess

HELP = {
    'en': ('Select which potion to use.', 'Select which ether to use.', 'Select which remedy to use.'),
    'de': ('Wähle den Trank aus, der verwendet werden soll.', 'Wähle den Äther aus, der verwendet werden soll.', 'Wähle das Heilmittel aus, das verwendet werden soll.'),
    'fr': ('Sélectionnez la potion à utiliser.', "Sélectionnez l’éther à utiliser.", 'Sélectionnez le remède à utiliser.'),
    'ja': ('使用するポーションを選択します。', '使用するエーテルを選択します。', '使用する治療薬を選択します。'),
    'ko': ('사용할 포션을 선택합니다.', '사용할 에테르를 선택합니다.', '사용할 치료제를 선택합니다.'),
    'cs': ('选择要使用的药水。', '选择要使用的以太。', '选择要使用的治疗药。'),
    'ct': ('選擇要使用的藥水。', '選擇要使用的乙太。', '選擇要使用的治療藥。'),
}
KEYS = (368, 371, 380)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def build(mod, output, cli):
    mod, output = Path(mod).resolve(), Path(output).resolve()
    if output.exists() or output == mod or output in mod.parents or mod in output.parents:
        raise ValueError('Use a new separate verification directory.')
    output.mkdir(parents=True)
    nxd = mod / 'FFTIVC/data/enhanced/nxd'
    records = {}
    def run(*args):
        subprocess.run([str(cli), *map(str, args)], check=True, stdout=subprocess.DEVNULL)
    for locale, descriptions in HELP.items():
        source = nxd / f'ability.{locale}.nxd'
        before_hash = sha(source)
        work = output / locale
        inputs, converted = work / 'input', work / 'converted'
        inputs.mkdir(parents=True)
        converted.mkdir()
        shutil.copy2(source, inputs / source.name)
        database = work / 'before.sqlite'
        run('nxd-to-sqlite', '-i', inputs, '-o', database, '-g', 'fft')
        table = f'Ability-{locale}'
        with sqlite3.connect(database) as db:
            columns = [r[1] for r in db.execute(f'PRAGMA table_info("{table}")')]
            before = db.execute(f'SELECT * FROM "{table}" ORDER BY Key').fetchall()
        edited = work / 'edited.sqlite'
        shutil.copy2(database, edited)
        with sqlite3.connect(edited) as db:
            for key, description in zip(KEYS, descriptions):
                if db.execute(f'UPDATE "{table}" SET Description=? WHERE Key=?', (description, key)).rowcount != 1:
                    raise ValueError('Missing group ability row.')
        run('sqlite-to-nxd', '-i', edited, '-o', converted, '-g', 'fft')
        checked = work / 'roundtrip.sqlite'
        run('nxd-to-sqlite', '-i', converted, '-o', checked, '-g', 'fft')
        with sqlite3.connect(checked) as db:
            after = db.execute(f'SELECT * FROM "{table}" ORDER BY Key').fetchall()
        if len(before) != len(after):
            raise ValueError('Row count changed.')
        expected = dict(zip(KEYS, descriptions))
        changed = []
        for old, new in zip(before, after):
            key = old[columns.index('Key')]
            wanted = list(old)
            if key in expected:
                wanted[columns.index('Description')] = expected[key]
            if tuple(wanted) != new:
                raise ValueError(f'Unexpected NXD field change: {locale}, ability {key}')
            if old != new:
                changed.append(key)
        if changed != list(KEYS):
            raise ValueError(f'Expected exactly three changed descriptions: {changed}')
        generated = converted / source.name
        shutil.copy2(generated, source)
        records[locale] = dict(BeforeSha256=before_hash, AfterSha256=sha(source),
                               ChangedKeys=changed, ChangedColumns=['Description'],
                               Rows=len(after), OtherFieldsAndItemDescriptionsPreserved=True)
    proof = dict(DescriptionOnly=True, NxdRoundTripVerified=True, Locales=records)
    (output / 'description-verification.json').write_text(json.dumps(proof, indent=2) + '\n', encoding='utf-8')
    return proof


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--mod', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--ff16tools', type=Path, default=os.environ.get('FF16TOOLS_CLI'))
    args = parser.parse_args()
    if not args.ff16tools:
        parser.error('Supply --ff16tools or FF16TOOLS_CLI.')
    build(args.mod, args.output, args.ff16tools)
    print('PASS: only Potion/Ether/Remedy group-action descriptions changed in all seven game locales; individual items and all other fields preserved.')

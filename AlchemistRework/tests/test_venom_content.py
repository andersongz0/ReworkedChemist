import importlib.util
from contextlib import closing
from pathlib import Path
import sqlite3
import struct
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
spec = importlib.util.spec_from_file_location('build_venom_content', ROOT / 'tools/build_venom_content.py')
content = importlib.util.module_from_spec(spec); spec.loader.exec_module(content)
ACTION = dict(Name='Venom Flask', JpCost=70)


class VenomContentTests(unittest.TestCase):
    def test_common_record_changes_only_price(self):
        image = content.NativeImage((content.GAME / 'FFT_enhanced.exe').read_bytes())
        result = content.common_record(image, 140)
        original = image.read(0x80EA90 + 240 * 12, 12)
        self.assertEqual(len(result), 12)
        self.assertEqual(struct.unpack_from('<H', result, 8)[0], 140)
        self.assertEqual(result[:8] + result[10:], original[:8] + original[10:])
        for value in (-1, 65536, True):
            with self.assertRaises(ValueError): content.common_record(image, value)

    def test_all_locales_are_additive_and_link_to_extra_icon(self):
        with tempfile.TemporaryDirectory(prefix='fft-venom-test-') as tmp:
            for locale in content.LOCALES:
                target = Path(tmp) / f'{locale}.sqlite'
                source = content.BASE / f'original-0004.{locale}.sqlite'
                preserved = content.export_rows(source, target, locale, 261, 513, 55, ACTION)
                self.assertEqual(preserved, {f'Item-{locale}': 261, f'Ability-{locale}': 512})
                with closing(sqlite3.connect(target)) as db, \
                     closing(sqlite3.connect(source.resolve().as_uri() + '?mode=ro', uri=True)) as native:
                    for kind, count in (('Item', 261), ('Ability', 512)):
                        table = f'{kind}-{locale}'
                        self.assertEqual(db.execute(f'SELECT * FROM "{table}" WHERE Key<? ORDER BY Key', (count,)).fetchall(),
                                         native.execute(f'SELECT * FROM "{table}" ORDER BY Key').fetchall())
                    self.assertEqual(db.execute(f'SELECT IconId,JpCost1,JpCost2,Name FROM "Ability-{locale}" WHERE Key=513').fetchone(),
                                     (55, 70, 0, 'Venom Flask'))
                    self.assertEqual(db.execute(f'SELECT Name,UiItemCategoryId FROM "Item-{locale}" WHERE Key=261').fetchone(),
                                     ('Venom Flask', 34))
                    self.assertIsNone(db.execute(f'SELECT Key FROM "Ability-{locale}" WHERE Key=512').fetchone())

    def test_medicine_record_uses_original_poison_and_inflict_status(self):
        image = content.NativeImage((content.GAME / 'FFT_enhanced.exe').read_bytes())
        self.assertEqual(content.medicine_record(image), bytes((56, 0, 39)))
        class AlteredEvidence:
            image_base = image.image_base
            def read(self, rva, size):
                original = image.read(rva, size)
                if rva == 0x80FBA0 + 39 * 6: return bytes(size)
                return original
        with self.assertRaises(ValueError): content.medicine_record(AlteredEvidence())

    def test_native_ids_reserved_512_and_bad_icons_cannot_be_overwritten(self):
        source = content.BASE / 'original-0004.en.sqlite'
        with tempfile.TemporaryDirectory(prefix='fft-venom-test-') as tmp:
            for index, ids in enumerate(((240, 513, 55), (261, 368, 55), (261, 512, 55), (261, 513, 24),
                                         (1024, 513, 55), (261, 1024, 55), (261, 4095, 55), (True, 513, 55))):
                target = Path(tmp) / f'{index}.sqlite'
                with self.assertRaises(ValueError): content.export_rows(source, target, 'en', *ids, ACTION)
                self.assertFalse(target.exists())

    def test_unknown_database_is_never_overwritten(self):
        with tempfile.TemporaryDirectory(prefix='fft-venom-test-') as tmp:
            target = Path(tmp) / 'existing.sqlite'; target.write_bytes(b'user data')
            with self.assertRaises(FileExistsError):
                content.export_rows(content.BASE / 'original-0004.en.sqlite', target, 'en', 261, 513, 55, ACTION)
            self.assertEqual(target.read_bytes(), b'user data')


if __name__ == '__main__': unittest.main()

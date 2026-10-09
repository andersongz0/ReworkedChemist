import importlib.util
from contextlib import closing
from pathlib import Path
import sqlite3
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('build_venom_icons', ROOT/'tools/build_venom_icons.py')
icons = importlib.util.module_from_spec(spec)
spec.loader.exec_module(icons)


class IconAssetsTests(unittest.TestCase):
    def test_all_three_native_parts_preserve_geometry(self):
        for family, old, new in (('equip_item','ei_240_uitx','ei_261_uitx'),
                                ('equip_item_s','ei_s_240_uitx','ei_s_261_uitx'),
                                ('ability','a_024_uitx','a_055_uitx')):
            template=icons.REF/family/'textureparts'/(old+'.utexpt')
            actual=icons.clone_parts(template,old,new)
            self.assertEqual(actual[:32],template.read_bytes()[:32])
            self.assertEqual(actual.replace(new.encode(),old.encode()),template.read_bytes())

    def test_bad_part_stems_rejected(self):
        template=icons.REF/'ability/textureparts/a_024_uitx.utexpt'
        for old,new in (('missing','a_055_uitx'),('a_024_uitx','a_1000_uitx')):
            with self.assertRaises(ValueError): icons.clone_parts(template,old,new)

    def test_metadata_addition_preserves_all_original_rows(self):
        with tempfile.TemporaryDirectory(prefix='fft-icon-test-') as tmp:
            target=Path(tmp)/'icons.sqlite'
            icons.stage_table(icons.BASE,target,55)
            with closing(sqlite3.connect(target)) as db:
                rows=db.execute('SELECT * FROM UIAbilityIcon ORDER BY Key').fetchall()
            self.assertEqual(len(rows),56)
            self.assertEqual(rows[-1],(55,*rows[24][1:]))
            with closing(sqlite3.connect(icons.BASE.resolve().as_uri()+'?mode=ro',uri=True)) as base:
                self.assertEqual(rows[:55],base.execute('SELECT * FROM UIAbilityIcon ORDER BY Key').fetchall())

    def test_existing_native_icon_cannot_be_replaced(self):
        with tempfile.TemporaryDirectory(prefix='fft-icon-test-') as tmp:
            target=Path(tmp)/'icons.sqlite'
            with self.assertRaises(ValueError): icons.stage_table(icons.BASE,target,24)
            self.assertFalse(target.exists())

    def test_normalized_assets_are_native_size_and_purple(self):
        from PIL import Image
        for name,size in (('item',100),('item-small',48),('ability',96)):
            with Image.open(ROOT/f'art/venom-v1/normalized/venom-flask.{name}.png') as im:
                self.assertEqual(im.size,(size,size))
                self.assertEqual(im.mode,'RGBA')
                self.assertEqual(im.getpixel((0,0))[3],0)
                self.assertGreater(sum(1 for r,g,b,a in im.get_flattened_data() if a>128 and r>g*1.2 and b>g*1.2),20)

if __name__=='__main__': unittest.main()

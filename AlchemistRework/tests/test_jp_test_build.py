import importlib.util
from contextlib import closing
from pathlib import Path
import sqlite3
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("build_jp_test", ROOT / "tools/build_jp_test.py")
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)


class JpOnlyTest(unittest.TestCase):
    def test_all_512_rows_preserved_and_only_four_jp_costs_change(self):
        for locale in build.LOCALES:
            source = build.PROTOTYPE / f"analysis-ff16tools-game-01040/sqlite/original-0004.{locale}.sqlite"
            with tempfile.TemporaryDirectory() as temp:
                output = Path(temp) / "jp.sqlite"
                build.export_cost_rows(source, output, locale, build.EXPECTED)
                with closing(sqlite3.connect(source.as_uri() + "?mode=ro", uri=True)) as original, closing(sqlite3.connect(output)) as result:
                    table = f"Ability-{locale}"
                    columns = [r[1] for r in original.execute(f'PRAGMA table_info("{table}")')]
                    key = columns.index("Key")
                    rows = result.execute(f'SELECT * FROM "{table}"').fetchall()
                    self.assertEqual(len(rows), 512)
                    for row in rows:
                        native_id = row[key]
                        baseline = original.execute(f'SELECT * FROM "{table}" WHERE Key=?', (native_id,)).fetchone()
                        cost = build.EXPECTED.get(native_id)
                        for i, column in enumerate(columns):
                            expected = baseline[i]
                            if cost is not None and column == "JpCost1":
                                expected = cost & 255
                            if cost is not None and column == "JpCost2":
                                expected = cost >> 8
                            self.assertEqual(row[i], expected, (locale, native_id, column))
                    self.assertEqual(set(result.execute("SELECT name FROM sqlite_master WHERE type='table'").fetchall()), {(table,), ("_uniontypes",)})
                    self.assertEqual(result.execute("SELECT * FROM _uniontypes ORDER BY id").fetchall(), original.execute("SELECT * FROM _uniontypes ORDER BY id").fetchall())
                with self.assertRaises(FileExistsError):
                    build.export_cost_rows(source, output, locale, build.EXPECTED)

    def test_expanded_ids_are_not_accepted_in_this_partial_test(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaises(ValueError):
                build.export_cost_rows(Path("does-not-exist"), Path(temp)/"bad.sqlite", "en", {513:70})


if __name__ == "__main__":
    unittest.main()

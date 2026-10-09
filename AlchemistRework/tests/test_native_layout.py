import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("audit_native_layout", ROOT / "tools/audit_native_layout.py")
auditor = importlib.util.module_from_spec(spec)
spec.loader.exec_module(auditor)


class NativeLayoutTests(unittest.TestCase):
    def test_supported_native_evidence(self):
        exe = auditor.GAME / "FFT_enhanced.exe"
        if not exe.exists():
            self.skipTest("Read-only installed executable audit requires the local supported game")
        report = auditor.audit(exe.read_bytes())
        self.assertTrue(report["ReadOnly"])
        self.assertFalse(report["NativeReady"])
        self.assertEqual(report["Command6"]["ActionSlots"], list(range(368, 382))+[0, 0])
        self.assertEqual(report["Command6"]["PassiveSlots"], [441, 474, 475, 480, 509, 0])
        self.assertEqual([entry["Index"] for entry in report["CombatFormulaTable"]["KnownEntries"]], [56, 72, 73, 75])

    def test_wrong_executable_fails_closed(self):
        for data in (b"", b"MZ", b"unsupported executable"):
            with self.assertRaises(ValueError):
                auditor.audit(data)


if __name__ == "__main__":
    unittest.main()

import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("relocation_plan", ROOT / "tools/build_native_relocation_plan.py")
planner = importlib.util.module_from_spec(spec)
try:
    spec.loader.exec_module(planner)
    AVAILABLE = True
except ModuleNotFoundError:
    AVAILABLE = False


@unittest.skipUnless(AVAILABLE, "Relocation audit requires local Capstone dependency")
class RelocationPlanTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.image = planner.native_layout.NativeImage((planner.native_layout.GAME / "FFT_enhanced.exe").read_bytes())
        cls.report = (ROOT / "analysis/table-references.txt").read_text(encoding="utf-8")

    def test_incomplete_audit_never_claims_installable(self):
        plan = planner.build_plan(self.image, self.report)
        self.assertTrue(plan["ReadOnly"])
        self.assertFalse(plan["Installable"])
        ability = plan["Tables"][0]
        self.assertEqual(len(ability["VerifiedOperands"]), 29)
        self.assertEqual(len(ability["UnreviewedReferences"]), 6)
        self.assertIn("battle item byte transport", plan["IncompleteRoutes"])

    def test_wrong_report_and_table_identity_rejected(self):
        wrong = self.report.replace(planner.native_layout.SUPPORTED_SHA256.lower(), "0"*64)
        with self.assertRaises(ValueError):
            planner.build_plan(self.image, wrong)
        wrong = self.report.replace("AbilityData @ 140787f80", "AbilityData @ 140787f81")
        with self.assertRaises(ValueError):
            planner.build_plan(self.image, wrong)


if __name__ == "__main__":
    unittest.main()

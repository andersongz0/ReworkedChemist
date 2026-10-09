import copy
import importlib.util
from pathlib import Path
import sqlite3
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("plan_content", ROOT / "tools/plan_content.py")
planner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(planner)


class DefinitionTests(unittest.TestCase):
    def setUp(self):
        self.definition = planner.read_definition(ROOT / "Alchemist.definition.json")
        self.connection = sqlite3.connect(planner.DATABASE.resolve().as_uri() + "?mode=ro", uri=True)

    def tearDown(self):
        self.connection.close()

    def test_exact_user_actions_and_costs(self):
        expected = [("Potion",50),("Ether",150),("Remedy",300),("Phoenix Down",90),
                    ("Venom Flask",70),("Oil Flask",150),("Fire Flask",250),("Ice Flask",250),
                    ("Thunder Flask",250),("Essence of Silence",700),("Essence of Blindness",700),
                    ("Essence of Slowness",700),("Essence of Acceleration",1000),
                    ("Essence of Regeneration",1000),("Essence of Flight",1000)]
        self.assertEqual([(a["Name"],a["JpCost"]) for a in self.definition["Actions"]],expected)
        plan = planner.validate(self.definition,self.connection)
        self.assertEqual((plan["ActionCount"],plan["NewItemCount"],plan["NewAbilityCount"],plan["NewIconCount"]),(15,11,11,11))
        self.assertEqual(plan["TotalJp"],6660)
        self.assertFalse(plan["Installable"])
        self.assertEqual(plan["NativeTables"],{"Item-en":(261,0,260),"Ability-en":(512,0,511)})
        self.assertFalse(plan["PendingBalance"])
        for entry in plan["Retained"]+plan["New"]:
            self.assertEqual(entry["JpCost"],entry["EncodedJp"]["LowByte"]+(entry["EncodedJp"]["HighByte"]<<8))

    def test_confirmed_status_and_element_semantics(self):
        new = [a for a in self.definition["Actions"] if a.get("NewItem")]
        self.assertEqual([a["Status"] for a in new if a["Effect"]=="Status"],
                         ["Poison","Oil","Silence","Blind","Slow","Haste","Regen","Float"])
        elements = [a for a in new if a["Effect"]=="FixedElementalDamage"]
        self.assertEqual([a["Element"] for a in elements],["Fire","Ice","Lightning"])
        self.assertTrue(all(a["UsesFaith"] is False for a in elements))
        self.assertTrue(all(a["ConsumeCount"]==1 for a in self.definition["Actions"]))
        self.assertTrue(self.definition["TargetJob"]["PreserveReactionSupportMovement"])
        balance = self.definition["Balance"]
        self.assertEqual((balance["FixedElementalDamage"],balance["NewItemRange"],balance["StatusSuccessPercent"]),(60,4,100))
        self.assertEqual(balance["NewItemArea"],"SingleTarget")
        self.assertTrue(balance["Immediate"])
        self.assertFalse(balance["StatusUsesFaith"])
        self.assertTrue(balance["RespectStatusImmunities"])
        self.assertEqual(balance["ShopUnlockRule"],"Chapter1")
        self.assertTrue(all(a["PriceGil"]==2*a["JpCost"] for a in new))

    def test_invalid_definitions_fail_closed(self):
        for field,value in [("JpCost",-1),("JpCost",65536),("JpCost",True),("ConsumeCount",0),
                            ("NativeAbilityId",512),("NativeItemId",261)]:
            invalid = copy.deepcopy(self.definition)
            invalid["Actions"][0][field] = value
            with self.assertRaises(ValueError): planner.validate(invalid,self.connection)
        invalid = copy.deepcopy(self.definition)
        invalid["Actions"].append(copy.deepcopy(invalid["Actions"][0]))
        with self.assertRaises(ValueError): planner.validate(invalid,self.connection)

    def test_invalid_balance_and_capabilities_fail_closed(self):
        cases = [("FixedElementalDamage", 0), ("FixedElementalDamage", 65536),
                 ("FixedElementalDamage", True), ("NewItemRange", -1),
                 ("NewItemRange", 256), ("NewItemRange", None),
                 ("StatusSuccessPercent", 101), ("StatusUsesFaith", True),
                 ("Immediate", 1), ("RespectStatusImmunities", False),
                 ("ShopUnlockRule", "Unconfirmed")]
        for field, value in cases:
            with self.subTest(field=field, value=value):
                invalid = copy.deepcopy(self.definition)
                invalid["Balance"][field] = value
                with self.assertRaises(ValueError): planner.validate(invalid, self.connection)
        for value in (-1, 65536, None, True):
            invalid = copy.deepcopy(self.definition)
            invalid["Actions"][4]["PriceGil"] = value
            with self.assertRaises(ValueError): planner.validate(invalid, self.connection)
        for capabilities in ([], ["Unknown"], self.definition["RequiredCapabilities"] * 2):
            invalid = copy.deepcopy(self.definition)
            invalid["RequiredCapabilities"] = capabilities
            with self.assertRaises(ValueError): planner.validate(invalid, self.connection)
        invalid = copy.deepcopy(self.definition)
        invalid["Actions"][6]["UsesFaith"] = True
        with self.assertRaises(ValueError): planner.validate(invalid,self.connection)
        invalid = copy.deepcopy(self.definition)
        invalid["Actions"][4]["NativeItemId"] = 254
        with self.assertRaises(ValueError): planner.validate(invalid,self.connection)

    def test_both_icon_routes_are_required(self):
        for capability in ('ExpandedItemIcons','ExpandedAbilityIcons','LegacyItemIconRouting'):
            invalid = copy.deepcopy(self.definition)
            invalid['RequiredCapabilities'].remove(capability)
            with self.assertRaises(ValueError): planner.validate(invalid,self.connection)


if __name__ == "__main__": unittest.main()

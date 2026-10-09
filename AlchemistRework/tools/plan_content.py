"""Validate an Alchemist definition against pristine TIC tables; never install it.

Numeric IDs are deliberately not assigned yet. The native command decoder stores
9-bit IDs and action 0x200 already has a special branch in actionExecuteProcs.
Choosing 'the next NXD row' would neither expand those routes nor be safe.
"""
import argparse
import json
from pathlib import Path
import sqlite3

ROOT = Path(__file__).resolve().parents[1]
WORKSPACE = ROOT.parent
DATABASE = WORKSPACE / "FFTModLoader_Prototype_v0.10.30/analysis-ff16tools-game-01040/sqlite/original-0004.en.sqlite"
CAPABILITIES = {"ExpandedItemData", "ExpandedAbilityData", "ExpandedCommandIds",
                "ExpandedItemIcons", "ExpandedAbilityIcons", "LegacyItemIconRouting",
                "FormulaRegistry", "ExpandedBattleItemList",
                "InventoryPersistence", "ShopIntegration", "LearnedAbilityMigration"}


def validate_balance(balance):
    for key, low, high in (("FixedElementalDamage", 1, 65535),
                           ("TargetMaxHpPercent", 0, 100),
                           ("NewItemRange", 1, 255), ("StatusSuccessPercent", 0, 100)):
        value = balance.get(key)
        if type(value) is not int or not low <= value <= high:
            raise ValueError(f"Invalid balance value: {key}")
    for key, expected in (("NewItemArea", "SingleTarget"), ("Immediate", True),
                          ("StatusUsesFaith", False), ("RespectStatusImmunities", True),
                          ("Availability", "Shops"), ("ShopUnlockRule", "ByChapter")):
        if type(balance.get(key)) is not type(expected) or balance.get(key) != expected:
            raise ValueError(f"Unsupported first-test rule: {key}")
    unlocks=balance.get('ShopUnlockChapters')
    if not isinstance(unlocks,dict) or len(unlocks)!=11 or any(type(v) is not int or not 1<=v<=4 for v in unlocks.values()):
        raise ValueError('Every new item needs a valid shop chapter.')


def read_definition(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"Duplicate JSON property: {key}")
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding="utf-8"), object_pairs_hook=unique)


def validate(definition, connection):
    if type(definition.get("SchemaVersion")) is not int or definition.get("SchemaVersion") != 1 or definition.get("Stage") != "DefinitionOnly":
        raise ValueError("This tool accepts only schema 1, non-installable definitions")
    capabilities = definition["RequiredCapabilities"]
    if not isinstance(capabilities, list) or any(not isinstance(c, str) for c in capabilities) or len(set(capabilities)) != len(capabilities) or set(capabilities) != CAPABILITIES:
        raise ValueError("Incomplete, duplicate or unknown required capabilities")
    validate_balance(definition["Balance"])
    job = definition["TargetJob"]
    if any(type(job[field]) is not int for field in ("NativeId", "CommandId")):
        raise ValueError("Native job/command IDs must be integers")
    if any(job.get(field) is not True for field in ("ReplaceActions", "PreserveReactionSupportMovement", "PreserveCharacterSprites")):
        raise ValueError("This rework preserves passive abilities and character sprites")
    native = connection.execute('SELECT Name, "jobcommand+Id" FROM "Job-en" WHERE Key=?',
                                (job["NativeId"],)).fetchone()
    if native != (job["NativeName"], job["CommandId"]):
        raise ValueError("Target job/command does not match pristine data")
    actions = definition["Actions"]
    if not actions or len(actions) > 16:
        raise ValueError("More than 16 action slots requires a separate learned-slot expansion")
    keys, names, icons = set(), set(), set()
    retained, added, jp_total = [], [], 0
    pending = []
    for action in actions:
        key, name, jp = action["Key"], action["Name"], action["JpCost"]
        if not isinstance(key, str) or not isinstance(name, str) or not key.strip() or not name.strip() or key in keys or name in names:
            raise ValueError("Action keys/names must be nonempty and unique")
        keys.add(key)
        names.add(name)
        if type(jp) is not int or not 0 <= jp <= 65535:
            raise ValueError(f"Invalid JP cost: {key}")
        if type(action["ConsumeCount"]) is not int or action["ConsumeCount"] != 1:
            raise ValueError("Confirmed consumption is exactly one item per use")
        jp_total += jp
        encoded = {"LowByte": jp & 255, "HighByte": jp >> 8}
        if action["Effect"] == "Native":
            if action.get("NewItem") or action.get("NewAbility"):
                raise ValueError("Native actions cannot claim new item/ability IDs")
            for table, field in (("Item-en", "NativeItemId"), ("Ability-en", "NativeAbilityId")):
                if type(action[field]) is not int:
                    raise ValueError(f"Native ID must be an integer: {key}, {field}")
                row = connection.execute(f'SELECT Name FROM "{table}" WHERE Key=?', (action[field],)).fetchone()
                if row != (name,):
                    raise ValueError(f"Native identity mismatch: {key}, {table}")
            retained.append({"Name": name, "JpCost": jp, "EncodedJp": encoded,
                             "ItemId": action["NativeItemId"], "AbilityId": action["NativeAbilityId"]})
            continue
        if action.get("NewItem") is not True or action.get("NewAbility") is not True:
            raise ValueError("New actions require both a new item and a new ability")
        if "NativeItemId" in action or "NativeAbilityId" in action:
            raise ValueError("Never replace a native ID to simulate content expansion")
        if action["Effect"] == "Status":
            if action.get("Status") not in {"Poison", "Oil", "Silence", "Blind", "Slow", "Haste", "Regen", "Float"}:
                raise ValueError("Unknown requested status")
        elif action["Effect"] == "FixedElementalDamage":
            if action.get("Element") not in {"Fire", "Ice", "Lightning"} or action.get("UsesFaith") is not False:
                raise ValueError("Flasks require the selected element and no Faith scaling")
        else:
            raise ValueError("Unknown effect kind")
        icon = action["IconKey"]
        if not isinstance(icon, str) or not icon.strip() or icon in icons:
            raise ValueError("Each new item needs a distinct icon registration")
        icons.add(icon)
        price = action.get("PriceGil")
        if type(price) is not int or not 0 <= price <= 65535:
            raise ValueError("Shop prices must fit the native 16-bit price field")
        added.append({"Key": key, "Name": name, "JpCost": jp, "EncodedJp": encoded,
                      "Effect": action["Effect"], "IconKey": icon, "PriceGil": price,
                      **({"Status": action["Status"]} if action["Effect"] == "Status"
                         else {"Element": action["Element"], "UsesFaith": False})})
    counts = {}
    for table in ("Item-en", "Ability-en"):
        counts[table] = connection.execute(f'SELECT count(*),min(Key),max(Key) FROM "{table}"').fetchone()
    return {"Stage": "DefinitionOnly", "Installable": False, "NativeTables": counts,
            "TargetJob": job, "ActionCount": len(actions), "Retained": retained, "New": added,
            "NewItemCount": len(added), "NewAbilityCount": len(added), "NewIconCount": len(icons),
            "TotalJp": jp_total, "PendingBalance": pending,
            "Balance": definition["Balance"],
            "RequiredCapabilities": definition["RequiredCapabilities"],
            "NativeIdAllocation": "Pending command/table/sentinel audit; no IDs allocated"}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--definition", type=Path, default=ROOT / "Alchemist.definition.json")
    parser.add_argument("--database", type=Path, default=DATABASE)
    args = parser.parse_args()
    with sqlite3.connect(args.database.resolve().as_uri() + "?mode=ro", uri=True) as connection:
        result = validate(read_definition(args.definition), connection)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

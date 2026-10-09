"""Prepare guarded table-relocation operands; never write game files or memory.

Ghidra's reference listing is not sufficient to patch memory. Each source site
is decoded again from the hash-checked executable, the referenced field must
match, and unsupported encodings are explicitly left unreviewed.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
PROTOTYPE = ROOT.parent / "FFTModLoader_Prototype_v0.10.30"
sys.path.insert(0, str(PROTOTYPE / "analysis-ff16tools-game-01040/pydeps"))
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_MEM, X86_REG_RIP

spec = importlib.util.spec_from_file_location("native_layout", ROOT / "tools/audit_native_layout.py")
native_layout = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native_layout)


def references(report, table):
    header = re.search(rf"^=== {re.escape(table)} @ ([0-9a-f]+) ===$", report, re.MULTILINE)
    if not header:
        raise ValueError(f"Missing native table audit: {table}")
    body = report[header.end():].split("\n=== ", 1)[0]
    entries = re.findall(r"^Ref ([0-9a-f]+) -> \+([0-9a-f]+) (\w+) in (.+)$", body, re.MULTILINE)
    if not entries:
        raise ValueError(f"No references audited for {table}")
    return int(header[1], 16), entries


def build_plan(image, report):
    audited_hash = re.search(r"^SHA256: ([0-9a-fA-F]+)$", report, re.MULTILINE)
    if not audited_hash or audited_hash[1].upper() != native_layout.SUPPORTED_SHA256:
        raise ValueError("Reference report does not belong to the supported executable")
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    decoder.detail = True
    tables = []
    for table in ("AbilityData", "PartyItem"):
        source_va, entries = references(report, table)
        source_rva = source_va-image.image_base
        if source_rva != {"AbilityData": 0x787F80, "PartyItem": 0x11A7C00}[table]:
            raise ValueError(f"Unexpected audited source for {table}")
        operands, unreviewed = [], []
        seen = set()
        for site_hex, field_hex, reference_type, function in entries:
            site_rva = int(site_hex, 16)-image.image_base
            field = int(field_hex, 16)
            # Several propagated references can belong to one operand. Never
            # count them as independently patchable code locations.
            instruction = next(decoder.disasm(image.read(site_rva, 15), site_rva, count=1), None)
            resolved = None
            if instruction is not None:
                for operand in instruction.operands:
                    if operand.type != X86_OP_MEM:
                        continue
                    if operand.mem.base == X86_REG_RIP:
                        target = instruction.address+instruction.size+operand.mem.disp
                        if target == source_rva+field:
                            resolved = "RipRelative"
                    elif operand.mem.disp == source_rva+field:
                        resolved = "ImageRelativeIndexed"
            if resolved is None or instruction.disp_size != 4:
                unreviewed.append({"SiteRva": f"{site_rva:X}", "FieldOffset": field,
                    "Function": function, "Instruction": None if instruction is None else
                    f"{instruction.mnemonic} {instruction.op_str}",
                    "Reason": "No independently verified four-byte table operand"})
                continue
            key = (site_rva, instruction.disp_offset)
            if key in seen:
                continue
            seen.add(key)
            operands.append({"InstructionRva": f"{site_rva:X}",
                "OperandRva": f"{site_rva+instruction.disp_offset:X}", "FieldOffset": field,
                "Encoding": resolved, "InstructionLength": instruction.size,
                "ExpectedInstruction": bytes(instruction.bytes).hex().upper(),
                "ExpectedOperand": bytes(instruction.bytes)[instruction.disp_offset:instruction.disp_offset+4].hex().upper(),
                "Function": function})
        tables.append({"Name": table, "SourceRva": f"{source_rva:X}",
            "VerifiedOperands": operands, "UnreviewedReferences": unreviewed})
    return {"ExecutableSha256": native_layout.SUPPORTED_SHA256,
            "ReadOnly": True, "Installable": False, "Tables": tables,
            "IncompleteRoutes": ["command ID decoding", "secondary item/ability classification",
                "battle item byte transport", "shop and inventory bounds",
                "native save association and expanded persistence", "formula/UI dispatch", "new icons"],
            "Warning": "Verified operands alone are not a complete expansion. Never apply a partial plan."}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--exe", type=Path, default=native_layout.GAME / "FFT_enhanced.exe")
    parser.add_argument("--report", type=Path, default=ROOT / "analysis/table-references.txt")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    plan = build_plan(native_layout.NativeImage(args.exe.read_bytes()), args.report.read_text(encoding="utf-8"))
    if args.output:
        # Derived audit artifact, not source code or a game installation.
        args.output.write_text(json.dumps(plan, indent=2)+"\n", encoding="utf-8")
    print(json.dumps({"Installable": False, "Tables": [{"Name": t["Name"],
        "VerifiedOperands": len(t["VerifiedOperands"]), "UnreviewedReferences": len(t["UnreviewedReferences"])}
        for t in plan["Tables"]]}, indent=2))


if __name__ == "__main__":
    main()

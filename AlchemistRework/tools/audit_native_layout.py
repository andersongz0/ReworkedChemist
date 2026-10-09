"""Repeatable read-only evidence; does not patch, launch or inject the game."""
import argparse
import hashlib
import json
from pathlib import Path
import struct

SUPPORTED_SHA256 = "937233F7FE76182A665C487C8802F5CEC6662DDD09967E87CD09FB146FC6B5D5"
GAME = Path(r"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles")


class NativeImage:
    def __init__(self, data):
        self.data = data
        if hashlib.sha256(data).hexdigest().upper() != SUPPORTED_SHA256:
            raise ValueError("Unsupported executable; no native layout may be trusted")
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        if data[pe:pe+4] != b"PE\0\0":
            raise ValueError("Not a PE image")
        count = struct.unpack_from("<H", data, pe+6)[0]
        optional_size = struct.unpack_from("<H", data, pe+20)[0]
        if struct.unpack_from("<H", data, pe+24)[0] != 0x20B:
            raise ValueError("Not a PE32+ image")
        self.image_base = struct.unpack_from("<Q", data, pe+48)[0]
        section_start = pe+24+optional_size
        self.sections = []
        for i in range(count):
            offset = section_start+i*40
            virtual_size, rva, raw_size, raw_offset = struct.unpack_from("<4I", data, offset+8)
            self.sections.append((rva, virtual_size, raw_offset, raw_size))

    def read(self, rva, size):
        if rva < 0 or size < 0:
            raise ValueError("Invalid image read")
        for start, _, raw_offset, raw_size in self.sections:
            if start <= rva and rva+size <= start+raw_size:
                offset = raw_offset+rva-start
                if offset+size > len(self.data):
                    break
                return self.data[offset:offset+size]
        raise ValueError(f"RVA is not fully file-backed: {rva:X}+{size:X}")


def audit(data):
    image = NativeImage(data)
    command = image.read(0x67E210+6*25, 25)
    # Three high-bit bytes followed by 22 low-byte IDs, MSB-first.
    entries = [command[3+i] | (((command[i//8] >> (7-i%8)) & 1) << 8) for i in range(22)]
    expected = list(range(368, 382))+[0, 0, 441, 474, 475, 480, 509, 0]
    if entries != expected:
        raise ValueError("Native command decoder layout changed")
    pointers = struct.unpack("<107Q", image.read(0x682BC8, 107*8))
    known = {56: ("InflictStatus", 0x30698C), 72: ("Potion", 0x308C30),
             73: ("Ether", 0x306BD0), 75: ("PhoenixDown", 0x308C98)}
    for index, (_, rva) in known.items():
        if pointers[index] != image.image_base+rva:
            raise ValueError(f"Unexpected combat formula pointer at index {index}")
    items = []
    for item_id in (240, 243, 252, 253):
        common = image.read(0x80EA90+item_id*12, 12)
        items.append({"Id": item_id, "NativeSprite": common[1], "Palette": common[0],
                      "PriceGil": struct.unpack_from("<H", common, 8)[0],
                      "ShopAvailabilityByte": common[10]})
    return {"ExecutableSha256": SUPPORTED_SHA256, "ReadOnly": True, "NativeReady": False,
            "Command6": {"Rva": "67E210+6*25", "EncodedIdBits": 9,
                         "ActionSlots": entries[:16], "PassiveSlots": entries[16:]},
            "CombatFormulaTable": {"Rva": "682BC8", "AuditedDispatchIndices": [1, 106],
                "KnownEntries": [{"Index": index, "Name": name, "FunctionRva": f"{rva:X}"}
                                 for index, (name, rva) in known.items()]},
            "RetainedNativeItems": items,
            "Warnings": ["No new IDs assigned; 512 already has a special action route",
                         "The item command uses byte-sized IDs; new IDs cannot be inserted unchanged",
                         "Formula execution and interface paths both require expansion",
                         "Fixed native inventory/save layout is not expanded by this audit"]}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--exe", type=Path, default=GAME / "FFT_enhanced.exe")
    args = parser.parse_args()
    print(json.dumps(audit(args.exe.read_bytes()), indent=2))


if __name__ == "__main__":
    main()

"""Independently decode the release's three owned-table relocations (read only).

Never relocates PartyItem or the unrelated state adjoining the shared list.
Propagated references are included as review evidence, not patch locations.
"""
import json
import re
import struct
from pathlib import Path
from build_native_relocation_plan import native_layout, Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_MEM, X86_REG_RIP

ROOT = Path(__file__).resolve().parents[1]

# Audited against the named enhanced executable's ASM/control-flow listings.
# These are pointer provenance, NEVER additional displacement patch sites.
# Branch joins list every possible table-base definition. Native calls preserve
# the callee-saved bases; GetTotalAbilityList also retains its private R11 cursor.
PROVENANCE = {
    'AbilityData': {
        0x30CE34:[0x30CE28],0x2780E4:[0x2780DD],0x30CD4A:[0x30CD40],
        0x392780:[0x392776],0x30E5E3:[0x30E5D9],0x30DF81:[0x30DF77],
    },
    'SharedUiList': {
        0x286B91:[0x286B4C],0x286BAF:[0x286B4C],
        0x36720C:[0x367205],0x367264:[0x367205,0x367236],
        0x367287:[0x367205,0x367236],
        0x367589:[0x367205,0x367236,0x3672B3,0x36735A],
        0x3672E1:[0x367151],0x36732C:[0x367325],
        0x367382:[0x36735A],0x36748B:[0x36735A],0x3674A6:[0x36735A],
        0x36EB47:[0x36EB2A],0x36EB64:[0x36EB2A],
        0x335625:[0x33561B],0x334FFA:[0x334FF3],0x334BF1:[0x334BEA],
        0x334D32:[0x334D27],0x334D3C:[0x334D27],
        0x334E3F:[0x334E1D],0x334E68:[0x334E1D],0x334ED0:[0x334E1D],
        0x36C485:[0x36C47E],
        0x333AE8:[0x333AD2],0x333B32:[0x333AD2],0x333B4A:[0x333AD2],
        0x333E4A:[0x333AD2,0x333BFB],
        0x333C0F:[0x333BFB],0x333C27:[0x333BFB],0x333C5D:[0x333BFB],
        0x3332DC:[0x3332C3],0x3332E2:[0x3332C3],
        0x28B67B:[0x28B66E],0x28B686:[0x28B66E],0x28B692:[0x28B66E],
        0x36A942:[0x36A93B],0x36A97A:[0x36A973],0x36A9A2:[0x36A99B],
        0x36ACDD:[0x36ACCF],0x332C64:[0x332C5D],0x332C9C:[0x332C95],
        0x332D56:[0x332D4F],
    },
    'AbilityVisual': {
        0x206F1F:[0x206F18],0x2062D2:[0x2062CB],
        0x20CE40:[0x20CE39],0x205C87:[0x205C80],
    },
}

def build(image, ability_report, list_report, visual_report=None):
    decoder = Cs(CS_ARCH_X86, CS_MODE_64)
    decoder.detail = True
    tables = []
    descriptions=[
        ('AbilityData', 0x787F80, 4096, 8192, ability_report),
        ('SharedUiList', 0x1811470, 512, 2048, list_report),
    ]
    if visual_report is not None:
        # 6732F4 starts unrelated stat-growth data, NOT another effect entry.
        descriptions.append(('AbilityVisual',0x672DA0,0x554,3072,visual_report))
    for name, start, length, expanded, report in descriptions:
        sha = re.search(r'^SHA256: (\w+)$', report, re.M)
        if not sha or sha[1].upper() != native_layout.SUPPORTED_SHA256:
            raise ValueError('Evidence executable hash mismatch')
        refs = re.findall(r'^REF (\w+) <- (\w+) .* function=(.+)$', report, re.M)
        operands = {}
        propagated = {}
        for target, site, function in refs:
            target_rva = int(target, 16) - image.image_base
            if not start <= target_rva < start + length:
                continue
            rva = int(site, 16) - image.image_base
            ins = next(decoder.disasm(image.read(rva, 15), rva, count=1))
            resolved = None
            field = None
            for op in ins.operands:
                if op.type != X86_OP_MEM:
                    continue
                base = ins.address + ins.size + op.mem.disp if op.mem.base == X86_REG_RIP else op.mem.disp
                # A propagated Ghidra target can be +2 or +4 while the actual
                # independently decoded displacement points at list element 0.
                # Capstone 5 reports disp_size=2 for 66 TEST r/m16,r16 even
                # when the encoded addressing displacement is four bytes.
                # Independently check the actual four bytes against the signed
                # displacement. Never derive patch width from operand width.
                encoded_disp32 = (ins.disp_offset > 0 and ins.disp_offset+4 <= ins.size and
                    struct.unpack_from('<i',bytes(ins.bytes),ins.disp_offset)[0] == op.mem.disp)
                if start <= base < start + length and encoded_disp32:
                    resolved = 'RipRelative' if op.mem.base == X86_REG_RIP else 'ImageRelativeIndexed'
                    field = base-start
            item = dict(InstructionRva=f'{rva:X}', ExpectedInstruction=bytes(ins.bytes).hex().upper(),
                        Function=function, Instruction=f'{ins.mnemonic} {ins.op_str}')
            if resolved:
                item.update(DisplacementOffset=ins.disp_offset, FieldOffset=field, Encoding=resolved)
                operands[rva] = item
                propagated.pop(rva, None)
            elif rva not in operands:
                propagated[rva] = item
        if name == 'AbilityData':
            # These two native bases point beyond AbilityData, but their only
            # reads subtract 0x32C to access the original movement byte table.
            # Relocate the bias base, retaining the exact indexed expression.
            for rva in (0x30DF77, 0x30E5D9):
                ins = next(decoder.disasm(image.read(rva,15),rva,count=1))
                target = ins.address+ins.size+ins.operands[1].mem.disp
                if ins.mnemonic != 'lea' or target != 0x789280 or ins.disp_size != 4:
                    raise ValueError('Movement table bias instruction changed')
                operands[rva] = dict(InstructionRva=f'{rva:X}', ExpectedInstruction=bytes(ins.bytes).hex().upper(),
                    DisplacementOffset=ins.disp_offset, FieldOffset=target-start, Encoding='RipRelative',
                    Function='set_abilitycost_list' if rva == 0x30DF77 else 'set_normability2',
                    Instruction=f'{ins.mnemonic} {ins.op_str}', Review='Bias-only pointer; indexed read subtracts 0x32C')
        if set(propagated)!=set(PROVENANCE[name]):
            raise ValueError(f'{name}: propagated-reference audit no longer matches')
        for rva,entry in propagated.items():
            origins=PROVENANCE[name][rva]
            for origin in origins:
                if origin not in operands or operands[origin]['Function']!=entry['Function']:
                    raise ValueError('Provenance origin is not a relocated base in the same function')
                if not operands[origin]['Instruction'].startswith('lea '):
                    raise ValueError('Expected an independently decoded table-base LEA')
            entry['OriginRvas']=[f'{x:X}' for x in origins]
            entry['Review']='ASM/control-flow provenance: relocated base retained through indexed access, register copy, or native branch join; not a patch site.'
        extra=[]
        if name=='SharedUiList':
            ins=next(decoder.disasm(image.read(0x286B63,15),0x286B63,count=1))
            if ins.mnemonic!='mov' or ins.op_str!='r11, rdi':
                raise ValueError('GetTotalAbilityList private cursor copy changed')
            extra=[dict(InstructionRva='286B63',ExpectedInstruction=bytes(ins.bytes).hex().upper(),
                        Review='R11 receives relocated RDI, then advances by two per output element.')]
        tables.append(dict(Name=name, SourceRva=f'{start:X}', OriginalBytes=length, ExpandedBytes=expanded,
                           ProvenanceInstructions=extra,
                           VerifiedOperands=list(operands.values()), PropagatedReferences=list(propagated.values())))
    return dict(ExecutableSha256=native_layout.SUPPORTED_SHA256, Tables=tables,
                ReviewedFor='FiveActionVenomTest',
                InstallationRequires='Hash-locked host, native-context bridge, all menu/shop/combat/visual/save bindings; NOT a general arbitrary-content expansion.',
                Installable=visual_report is not None)

if __name__ == '__main__':
    image = native_layout.NativeImage((native_layout.GAME/'FFT_enhanced.exe').read_bytes())
    plan = build(image, (ROOT/'analysis/venom-live-binding-consumers.txt').read_text(encoding='utf-8'),
                 (ROOT/'analysis/venom-release-native.txt').read_text(encoding='utf-8'),
                 (ROOT/'analysis/venom-visual-table.txt').read_text(encoding='utf-8'))
    (ROOT/'analysis/playable-relocation-plan.json').write_text(json.dumps(plan,indent=2)+'\n',encoding='utf-8')
    print(json.dumps([{'Name':t['Name'], 'Operands':len(t['VerifiedOperands']),
                       'Propagated':len(t['PropagatedReferences'])} for t in plan['Tables']]))

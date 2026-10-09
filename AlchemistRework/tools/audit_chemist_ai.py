"""Independently decode the complete native AI candidate bank and its bounds."""
import json, re, struct
from pathlib import Path
from audit_native_layout import NativeImage, GAME, SUPPORTED_SHA256
from build_native_relocation_plan import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_MEM, X86_OP_IMM, X86_REG_RIP

ROOT=Path(__file__).resolve().parents[1]
report=(ROOT/'analysis/chemist-ai-native-tables-036.txt').read_text()
image=NativeImage((GAME/'FFT_enhanced.exe').read_bytes())
decoder=Cs(CS_ARCH_X86,CS_MODE_64);decoder.detail=True
operands={};propagated={};functions=set()
for field,site,name in re.findall(r'^TABREF (\w+) <- (\w+) (.+)$',report,re.M):
    rva=int(site,16)-image.image_base;functions.add(name)
    ins=next(decoder.disasm(image.read(rva,15),rva,count=1))
    found=False
    for op in ins.operands:
        if op.type!=X86_OP_MEM:continue
        address=ins.address+ins.size+op.mem.disp if op.mem.base==X86_REG_RIP else op.mem.disp
        if 0x1872598<=address<0x1872e18 and ins.disp_offset and struct.unpack_from('<i',ins.bytes,ins.disp_offset)[0]==op.mem.disp:
            operands[rva]=dict(InstructionRva=rva,ExpectedInstruction=bytes(ins.bytes).hex().upper(),DisplacementOffset=ins.disp_offset,FieldOffset=address-0x1872598,Encoding='RipRelative' if op.mem.base==X86_REG_RIP else 'ImageRelativeIndexed',Function=name)
            found=True
    if not found:propagated[rva]=dict(Function=name,Instruction=f'{ins.mnemonic} {ins.op_str}')
patches=[];biased={};current=''
for line in report.splitlines():
    match=re.match(r'^=== (.+) @',line)
    if match:current=match[1]
    if current not in functions or current=='CheckUnitActionEffective':continue
    match=re.match(r'^(140[0-9a-f]+) ',line)
    if not match:continue
    rva=int(match[1],16)-image.image_base;ins=next(decoder.disasm(image.read(rva,15),rva,count=1))
    for op in ins.operands:
        if op.type!=X86_OP_MEM:continue
        address=ins.address+ins.size+op.mem.disp if op.mem.base==X86_REG_RIP else op.mem.disp
        if 0x1872598<=address<0x1872e18 and ins.disp_offset and struct.unpack_from('<i',ins.bytes,ins.disp_offset)[0]==op.mem.disp:
            operands[rva]=dict(InstructionRva=rva,ExpectedInstruction=bytes(ins.bytes).hex().upper(),DisplacementOffset=ins.disp_offset,FieldOffset=address-0x1872598,Encoding='RipRelative' if op.mem.base==X86_REG_RIP else 'ImageRelativeIndexed',Function=current)
        # SetUnitParameter intentionally keeps Tmp_UA as R8/R10 across calls;
        # only its +EF8 candidate references move, NOT the selected-action base.
        if current=='SetUnitParameter' and op.mem.disp in (0xef8,0xefa,0xefb):
            assert ins.disp_size==4
            biased[rva]=dict(InstructionRva=rva,ExpectedInstruction=bytes(ins.bytes).hex().upper(),DisplacementOffset=ins.disp_offset,FieldOffset=op.mem.disp-0xef8,SourceBaseRva=0x18716a0,Function=current)
    # The only immediates34 and136 in these reviewed eight functions are
    # candidate row bounds and strides; status IDs33/34 elsewhere are excluded.
    for op in ins.operands:
        if op.type==X86_OP_IMM and op.imm in (34,136) and ins.mnemonic in ('imul','cmp'):
            value=64 if op.imm==34 else 256
            if ins.imm_size==1 and value>=128:raise ValueError('Widened native immediate required')
            before=bytes(ins.bytes);after=bytearray(before)
            after[ins.imm_offset:ins.imm_offset+ins.imm_size]=value.to_bytes(ins.imm_size,'little')
            patches.append(dict(InstructionRva=rva,Before=before.hex().upper(),After=after.hex().upper(),Function=current,Instruction=f'{ins.mnemonic} {ins.op_str}'))
assert functions=={'SetUACommand','SetupUnitAbilityFlags','SetUnitParameter','BattleRoutine_inner','DattoNoGotokuNigeyoRoutine','EvaluateAllUnitAction','SetMoveAreaForDattoNoGotokuNigeyo','RecoverCriticalStatus'},functions
for name in functions:
    assert any(p['Function']==name for p in patches),name
proof=dict(ExecutableSha256=SUPPORTED_SHA256,SourceRva=0x1872598,OriginalRows=16,OriginalCapacity=34,Capacity=64,Operands=list(operands.values()),BiasedOperands=list(biased.values()),Bounds=patches,PropagatedReferences=propagated,Functions=sorted(functions))
(ROOT/'analysis/chemist-ai-expansion-036.json').write_text(json.dumps(proof,indent=2)+'\n')
print('OPERANDS',len(operands),'BIASED',len(biased),'BOUNDS',len(patches),'PROPAGATED',len(propagated))
for rva,p in sorted(operands.items()):print(f'new(0x{rva:X},Convert.FromHexString("{p["ExpectedInstruction"]}"),{p["DisplacementOffset"]},{p["FieldOffset"]},NativeOperandEncoding.{p["Encoding"]}),')
for p in patches:print(f'(0x{p["InstructionRva"]:X},"{p["Before"]}","{p["After"]}"), // {p["Function"]}: {p["Instruction"]}')
print('PROPAGATED',json.dumps(propagated))

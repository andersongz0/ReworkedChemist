using System.Reflection.PortableExecutable;
using Iced.Intel;
byte[] code;ulong address;
if(args[0]=="--snapshot") {
 byte[] data=File.ReadAllBytes(args[1]);
 if(data.Length!=18864 || !data.AsSpan(0,8).SequenceEqual("CEXFAULT"u8))throw new InvalidDataException("Unsupported first-fault snapshot");
 int count=checked((int)BitConverter.ToUInt32(data,20));
 if(count>512)throw new InvalidDataException("Invalid captured code size");
 address=BitConverter.ToUInt64(data,32);code=data.AsSpan(1960,count).ToArray();
 ulong fault=BitConverter.ToUInt64(data,64);
 Console.WriteLine($"Exception IP: {fault:X}");
 if(fault>=address && fault<address+(ulong)code.Length) {
  int offset=checked((int)(fault-address));code=code.AsSpan(offset,Math.Min(96,code.Length-offset)).ToArray();address=fault;
 } else throw new InvalidDataException("Initial fault instruction was not captured");
} else {
 byte[] image=File.ReadAllBytes(args[0]);
 using var pe=new PEReader(new MemoryStream(image,false));
 int rva=Convert.ToInt32(args[1],16),length=Convert.ToInt32(args[2],16);
 var section=pe.PEHeaders.SectionHeaders.Single(s=>s.VirtualAddress<=rva&&rva+length<=s.VirtualAddress+s.SizeOfRawData);
 code=image.AsSpan(section.PointerToRawData+rva-section.VirtualAddress,length).ToArray();address=(ulong)rva;
}
var decoder=Decoder.Create(64,new ByteArrayCodeReader(code));decoder.IP=address;
while(decoder.IP<address+(ulong)code.Length){decoder.Decode(out var instruction);Console.WriteLine($"{instruction.IP:X8} {instruction}");}

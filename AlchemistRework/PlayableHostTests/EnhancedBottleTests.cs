using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using FFTModLoader.ContentExpansion.Runtime;

unsafe partial class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate long TextureCrop(long bank,int slot,long palette,int hash,int x,int y,int width,int height,long output,int stride,int bpp);
    static void CheckNativeCropEdges(OwnedNativeMemory native)
    {
        // Execute actual 41AC00, including native CRT math, rather than
        // supplying a hard-coded crop which concealed byte-corner overflow.
        nint crt=NativeLibrary.Load("ucrtbase.dll");
        foreach(var (rva,name) in new[]{(0x5DBDDC,"log2f"),(0x60CD08,"floorf"),(0x5DD7E0,"powf")})
            NativeLifetimeMemory.WriteProtected((long)native.Address+rva,
                new byte[]{0xFF,0x25,0,0,0,0}.Concat(BitConverter.GetBytes((long)NativeLibrary.GetExport(crt,name))).ToArray());
        using var uv=new OwnedNativeMemory(4096);using var rectangle=new OwnedNativeMemory(4096);
        var derive=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x41AC00);
        int[] Derive(byte u,byte v,int width)
        {
            byte[] vertices=new byte[64];
            // 260CF0 stores U+width back to an eight-bit PSX corner.
            for(int corner=0;corner<4;corner++)
            {
                ushort x=(byte)(u+((corner&1)!=0?width:0));
                ushort y=(byte)(v+((corner&2)!=0?16:0));
                BinaryPrimitives.WriteUInt16LittleEndian(vertices.AsSpan(corner*16),x);
                BinaryPrimitives.WriteUInt16LittleEndian(vertices.AsSpan(corner*16+2),y);
            }
            uv.Write(0,vertices);derive((long)uv.Address,(long)rectangle.Address);
            byte[] bytes=rectangle.Read(0,16);
            return Enumerable.Range(0,4).Select(n=>BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(n*4))).ToArray();
        }
        Check(Derive(240,21,16).SequenceEqual(new[]{0,42,512,32}),"Original U240+16 wrap failure not reproduced");
        for(int i=0;i<11;i++)
            Check(Derive(240,(byte)(21+16*i),15).SequenceEqual(new[]{480,42+32*i,32,32}),
                "Real renderer crop differs from private bottle source: "+i);
        Console.WriteLine("Fixture: real 41AC00 with native CRT reproduces U240+16 wrap -> x0/width512; width15 fixes all eleven crops to x480/width32 without losing any opaque column.");
    }
    [DllImport("kernel32.dll")] static extern nint RtlLookupFunctionEntry(ulong pc,out ulong imageBase,nint history);
    [DllImport("kernel32.dll")] static extern nint RtlVirtualUnwind(uint type,ulong imageBase,ulong pc,nint function,nint context,out nint handler,out ulong establisher,nint pointers);
    static void CheckDecoderUnwind(OwnedNativeMemory native)
    {
        byte[] call=native.Read(0x4165F5,5);
        long target=(long)native.Address+0x4165FA+BinaryPrimitives.ReadInt32LittleEndian(call.AsSpan(1));
        byte[] code=CheckedNativeRead.Read(target,1024);
        int begin=code.AsSpan().IndexOf(new byte[]{0x53,0x56,0x57,0x48,0x83,0xEC,0x20});
        int afterCall=code.AsSpan().IndexOf(new byte[]{0xFF,0xD0,0x48,0x89,0xC7})+2;
        int epilogue=code.AsSpan().IndexOf(new byte[]{0x48,0x83,0xC4,0x20,0x5F,0x5E,0x5B,0xC3});
        Check(begin>=0 && afterCall>begin && epilogue>afterCall,"Decoder frame instruction fixture missing");
        using var stack=new OwnedNativeMemory(4096);using var context=new OwnedNativeMemory(4096);
        long sp=(long)stack.Address+256;
        foreach(int pc in new[]{begin+3,afterCall,epilogue})
        {
            int allocation=pc==begin+3?0:32;
            stack.Write(256+allocation,BitConverter.GetBytes(0x11111111L));
            stack.Write(256+allocation+8,BitConverter.GetBytes(0x22222222L));
            stack.Write(256+allocation+16,BitConverter.GetBytes(0x33333333L));
            stack.Write(256+allocation+24,BitConverter.GetBytes(0x1234567812345678L));
            context.Write(48,BitConverter.GetBytes(0x10000Bu));context.Write(152,BitConverter.GetBytes(sp));
            context.Write(248,BitConverter.GetBytes(target+pc));
            nint f=RtlLookupFunctionEntry((ulong)(target+pc),out ulong imageBase,0);
            Check(f!=0,"Decoder wrapper lacks OS unwind metadata");
            RtlVirtualUnwind(0,imageBase,(ulong)(target+pc),f,context.Address,out _,out _,0);
            Check(Marshal.ReadInt64(context.Address,248)==0x1234567812345678L && Marshal.ReadInt64(context.Address,152)==sp+allocation+32,
                "OS cannot unwind decoder wrapper return/stack");
            Check(Marshal.ReadInt64(context.Address,176)==0x11111111L && Marshal.ReadInt64(context.Address,168)==0x22222222L &&
                Marshal.ReadInt64(context.Address,144)==0x33333333L,"OS cannot restore decoder wrapper RDI/RSI/RBX");
        }
        Console.WriteLine("Fixture: shared decoder caller has real OS unwind metadata; partial prologue, suspended call and epilogue restore return/RSP/RBX/RSI/RDI.");
    }
    static void SetDecoderFixture(OwnedNativeMemory decoder,int result)
    {
        byte[] code=new byte[]{0x48,0xB8}.Concat(BitConverter.GetBytes((long)decoder.Address+4096))
            .Concat(new byte[]{0xFF,0x00,0xB8}).Concat(BitConverter.GetBytes(result)).Concat(new byte[]{0xC3}).ToArray();
        NativeLifetimeMemory.WriteProtected((long)decoder.Address,code);
    }
    static void CheckEnhancedBottles(OwnedNativeMemory native,OwnedNativeMemory decoder,string mod)
    {
        CheckNativeCropEdges(native);
        using var source=new OwnedNativeMemory(0x80000);
        using var palette=new OwnedNativeMemory(4096);
        using var output=new OwnedNativeMemory(4096);
        using var descriptor=new OwnedNativeMemory(4096);
        using var alternate=new OwnedNativeMemory(0x80000);
        long bank=(long)native.Address+0x2EEE830;
        source.Write(0,Enumerable.Repeat((byte)0xAA,0x80000).ToArray());
        native.Write(0x2EEE830+0xA88,BitConverter.GetBytes((long)source.Address));
        byte[] decoderEntry=native.Read(0x3D25D8,14);
        CheckDecoderUnwind(native);
        // Execute the actual patched CALL with a correctly aligned fixture
        // frame. Outer scene is not run; shared decoder detour stays active.
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x4165F1,[0x48,0x83,0xEC,0x28]);
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x4165FA,[0x48,0x83,0xC4,0x28,0xC3]);
        var load=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x4165F1);
        byte[] before=source.Read(0,0x80000);
        load(0,0x9D,(long)source.Address);
        Check(source.Read(0,0x80000).SequenceEqual(before),"Other Enhanced resource modified");
        alternate.Write(0,before);load(0,0x9E,(long)alternate.Address);
        Check(alternate.Read(0,0x80000).SequenceEqual(before),"Other destination modified by bottle loader");
        SetDecoderFixture(decoder,-1);
        Check((int)load(0,0x9E,(long)source.Address)==-1 && source.Read(0,0x80000).SequenceEqual(before),"Failed decode modified texture");
        SetDecoderFixture(decoder,0);
        Check(load(0,0x9E,(long)source.Address)==0,"Enhanced decoder result lost");
        // Real native crop registration and 4bpp->RGBA conversion run;
        // only the outer GPU upload is stubbed. Actual 41AC00 above proves
        // the 15x16 primitive's padded crop is 32x32, x480/y42+32*i.
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x3D0A80,[0x31,0xC0,0xC3]);
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x3D0B10,[0xC3]);
        var crop=Marshal.GetDelegateForFunctionPointer<TextureCrop>(native.Address+0x3D0F8C);
        var register=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x3D0EF0);
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(mod,"native","world-bottle-atlas.json")));
        byte[] expected=(byte[])before.Clone();int i=0;
        foreach(var entry in doc.RootElement.GetProperty("Entries").EnumerateArray())
        {
            byte[] glyph=Convert.FromHexString(entry.GetProperty("GlyphHex").GetString()!);
            Check(Enumerable.Range(0,16).All(y=>(glyph[y*8+7]&0xF0)==0),"Width15 cuts an opaque pixel");
            byte[] colors=Convert.FromHexString(entry.GetProperty("PaletteHex").GetString()!);
            byte[] rgba=new byte[64];
            for(int n=1;n<16;n++)
            {
                int w=BinaryPrimitives.ReadUInt16LittleEndian(colors.AsSpan(n*2));
                rgba[n*4]=(byte)((w&31)*255/31);rgba[n*4+1]=(byte)(((w>>5)&31)*255/31);
                rgba[n*4+2]=(byte)(((w>>10)&31)*255/31);rgba[n*4+3]=255;
            }
            palette.Write(0,rgba);
            byte[] doubled=NativeConsumableVisuals.EnhancedGlyph(glyph);
            for(int y=0;y<32;y++)doubled.AsSpan(y*16,16).CopyTo(expected.AsSpan((42+32*i+y)*256+240));
            int[] rectangle=[480,42+32*i,32,32];
            descriptor.Write(0,rectangle.SelectMany(BitConverter.GetBytes).ToArray());
            int slot=(int)register(bank,(long)descriptor.Address);
            Check(native.Read(0x2EEE830+4+slot*20,16).SequenceEqual(descriptor.Read(0,16)),"Native Enhanced crop registration lost UV");
            crop(bank,0,(long)palette.Address,1,480,42+32*i,32,32,(long)output.Address,512,4);
            byte[] pixels=output.Read(0,4096);
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                int p=glyph[(y/2)*8+x/4];int index=(p>>(((x/2)&1)*4))&15;
                Check(pixels.AsSpan((y*32+x)*4,4).SequenceEqual(rgba.AsSpan(index*4,4)),"Real native converter produced wrong bottle pixel: "+i);
            }
            i++;
        }
        Check(i==11 && source.Read(0,0x80000).SequenceEqual(expected),"Enhanced source changed outside eleven private crops");
        source.Write(0,before);load(0,0x9E,(long)source.Address);
        Check(source.Read(0,0x80000).SequenceEqual(expected),"Enhanced cache reload lost bottle pixels");
        Check(native.Read(0x3D25D8,14).SequenceEqual(decoderEntry),"Existing decoder hook overwritten");
        Check(BinaryPrimitives.ReadInt32LittleEndian(decoder.Read(4096,4))==5,"Shared decoder chain was bypassed or invoked twice");
        native.Write(0x2EEE830+0xA88,new byte[8]);
        Console.WriteLine("Fixture: live decoder-conflict regression reproduced with existing JobExpansion detour; scoped Enhanced CALL executes existing chain exactly once, entry hook untouched.");
        Console.WriteLine("Fixture: eleven Enhanced double-UV crops through real native registration/4bpp conversion; cache reload, failed decode, other resources/destinations and all outside pixels preserved. GPU upload and source-file decode remain explicit fixtures.");
    }
}

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Reflection;
using FFTModLoader.ContentExpansion;
using FFTModLoader.ContentExpansion.Runtime;

unsafe partial class Program
{
    static void CheckConsumableCompletion(OwnedNativeMemory native,ChemistPlayableHost host,List<string> logs)
    {
        Console.WriteLine("Fixture: full native consumable completion");
        long image=(long)native.Address;
        NativeLifetimeMemory.WriteProtected(image+0x312ED0,ReceiverBefore);
        long Call(int rva,long a=0,long b=0,long c=0,long d=0)=>Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+rva)(a,b,c,d);
        void Stub(int rva,byte[] code)=>NativeLifetimeMemory.WriteProtected(image+rva,code);
        void Int(int rva,int value)=>native.Write(rva,BitConverter.GetBytes(value));
        using var actor=new OwnedNativeMemory(4096);using var target=new OwnedNativeMemory(4096);
        using var actorWorker=new OwnedNativeMemory(4096);using var targetWorker=new OwnedNativeMemory(4096);
        actor.Write(0,BitConverter.GetBytes((long)target.Address));actor.Write(8,[1]);target.Write(8,[2]);
        actor.Write(0x148,BitConverter.GetBytes((long)actorWorker.Address));target.Write(0x148,BitConverter.GetBytes((long)targetWorker.Address));
        targetWorker.Write(0x1BC,[1]);actor.Write(0x1A9,[1,1]);
        actorWorker.Write(0x1BC,[0]);actorWorker.Write(0x4F,[8,8]);targetWorker.Write(0x4F,[9,8]);
        native.Write(0xD3A410,BitConverter.GetBytes((long)actor.Address));Int(0xC6AD8C,1);
        for(int i=0;i<21;i++)native.Write(0x1853CE1+i*512,[255]);
        native.Write(0x1853CE1,[0]);native.Write(0x1853CE1+512,[1]);
        // Actual native vector, height, fixed-point length and duration setup.
        // CRT sqrt uses a real SSE square root; only terrain collision and
        // raster/camera geometry are isolated from the scene fixture.
        Stub(0x5E47D8,[0xF3,0x0F,0x51,0xC0,0xC3]);Stub(0x5E4130,[0x0F,0x57,0xC0,0xC3]);
        Stub(0x3125D0,[0x31,0xC0,0xC3]);
        // Real CheckGunResult/PrepareHitCheck/line sampling, with only the
        // scene geometry contact supplied deterministically at its own leaf.
        // Thus target-map ownership (not merely projectile vectors) is tested.
        using(var medicinePacket=new OwnedNativeMemory(4096))
        {
            byte[] originalMap=native.Read(0xD8DCB0,4096);
            byte[] map=new byte[4096];for(int i=0;i<512;i++){map[i*8]=63;map[i*8+6]=(byte)(i<256?0:1);}
            map[(8*16+8)*8]=0;map[(8*16+12)*8]=1;map[(8*16+10)*8]=2;
            native.Write(0xC6AD6A,[16,16]);native.Write(0xD8DCB0,map);
            native.Write(0x1853CE0+0x4F,[8,8,0]);native.Write(0x1853CE0+512+0x4F,[12,8,0]);
            native.Write(0x1853CE0+1024+1,[2]);native.Write(0x1853CE0+1024+0x4F,[10,8,0]);
            targetWorker.Write(0x4F,[12,8]);
            byte[] contact=new byte[]{0x48,0xB8}.Concat(BitConverter.GetBytes(image+0x37A5C10)).Concat(new byte[]{0xC7,0,2,0,0,0,0xB8,1,0,0,0,0xC3}).ToArray();
            Stub(0x3125D0,contact);
            foreach(ushort ability in ChemistMedicineFamilies.All.Concat(new ushort[]{381}).Concat(ChemistActionBindings.ExtraSlots.Select(s=>ChemistActionBindings.TestActions[s].AbilityId)))
            {
                byte[] packet=new byte[20];packet[1]=6;packet[10]=6;packet[11]=1;
                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),ability);medicinePacket.Write(0,packet);
                Check(Call(0x27FFE8,(long)medicinePacket.Address)==0,"Intercepted item target map rejected");
                Check((native.Read(0xD8DCB0+(8*16+10)*8+5,1)[0]&0x80)!=0 &&
                      (native.Read(0xD8DCB0+(8*16+12)*8+5,1)[0]&0x80)==0,"Item still targets selected unit through interceptor: "+ability);
                Check(medicinePacket.Read(0,20).SequenceEqual(packet),"Receiver resolution aliases action or packet");
                Stub(0x3125D0,[0x31,0xC0,0xC3]);
                Check(Call(0x27FFE8,(long)medicinePacket.Address)==0 &&
                      (native.Read(0xD8DCB0+(8*16+12)*8+5,1)[0]&0x80)!=0 &&
                      (native.Read(0xD8DCB0+(8*16+10)*8+5,1)[0]&0x80)==0,"Clear path did not restore selected receiver");
                Stub(0x3125D0,contact);
            }
            // Terrain blocks a flight without replacing it by the selected victim.
            contact[12]=255;contact[13]=255;contact[14]=255;contact[15]=255;Stub(0x3125D0,contact);
            Call(0x27FFE8,(long)medicinePacket.Address);
            Check(Enumerable.Range(0,512).All(i=>(native.Read(0xD8DCB0+i*8+5,1)[0]&0x80)==0),"Blocked trajectory still affected an arbitrary unit");
            Stub(0x3125D0,[0x31,0xC0,0xC3]);native.Write(0xD8DCB0,originalMap);
            native.Write(0x1853CE0+1024+1,[255]);targetWorker.Write(0x4F,[9,8]);
            Console.WriteLine("Fixture: all25 medicine/flask actions use actual CheckGunResult first receiver in effect map; intercept/clear/terrain paths preserve full packet, native range/height policy and single affected tile. Geometry contact is a scene fixture, not gameplay verification.");
        }
        // Real requestParmanentEffect and RequestBowEffect, including native
        // payload copying. Only the outer permanent-task allocator returns a
        // deterministic owned slot. No action or item identity is aliased.
        Stub(0x3109E8,[0xB8,1,0,0,0,0xC3]);
        const int task=0x37A5C40+0x60;
        byte[] expandedBranch=native.Read(0x1FADFF,5);
        actor.Write(0x142,BitConverter.GetBytes((ushort)513));actor.Write(0x152,[0]);
        try
        {
            Stub(0x1FADFF,[0xB9,0x7E,1,0,0]);Call(0x1FAD94,(long)actor.Address);
            Check(W(native.Read(task+4,2))!=16,"Fixture did not reproduce new actions using the weapon projectile");
        }
        finally{Stub(0x1FADFF,expandedBranch);}
        foreach(int s in ChemistActionBindings.ExtraSlots)
        {
            ushort id=ChemistActionBindings.TestActions[s].AbilityId,item=ChemistActionBindings.Item(s);
            actor.Write(0x142,BitConverter.GetBytes(id));actor.Write(0x150,BitConverter.GetBytes(item));actor.Write(0x1C8,BitConverter.GetBytes(item));
            foreach(byte weapon in new byte[]{0,1,7,15})
            {
                actor.Write(0x152,[weapon]);Call(0x1FAD94,(long)actor.Address);
                Check(W(native.Read(task+4,2))==16&&native.Read(task+3,1)[0]==20,"New flask did not select native item projectile handler: "+s);
                Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(task+0x28,4))==item,"Native projectile payload lost full-width item: "+s);
                Check(W(actor.Read(0x142,2))==id&&W(actor.Read(0x150,2))==item,"Projectile routing temporarily aliased action/item identity");
                Check(W(native.Read(0x186D778,2))==238&&W(native.Read(0x186D780,2))==266,
                    "Committed native trajectory lost current start/end coordinates: "+s);
                Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x186D770,4))>0,"Native launch duration is zero: "+s);
            }
        }
        // Current dump regression: an unprepared launch has six zero vector
        // words; the fixed selector must refresh them on every commitment.
        foreach(int distance in new[]{1,2,4})
        {
            targetWorker.Write(0x4F,[(byte)(8+distance),8]);
            native.Write(0x186D778,new byte[16]);actor.Write(0x142,BitConverter.GetBytes((ushort)513));
            Call(0x1FAD94,(long)actor.Address);
            Check(W(native.Read(0x186D780,2))-W(native.Read(0x186D778,2))==28*distance,"Stale trajectory vectors survived commitment");
        }
        targetWorker.Write(0x4F,[9,8]);
        actor.Write(0x1AA,[0]);Int(0x18732C8,1);Int(task+8,0);
        Call(0x1FAD94,(long)actor.Address);
        Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(task+8,4))==0&&BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x18732C8,4))==2,
            "Self-use queued a zero-length flight or blocked impact");actor.Write(0x1AA,[1]);
        foreach(ushort id in new ushort[]{28,146,148,368,393,406,512,524})
        {
            actor.Write(0x142,BitConverter.GetBytes(id));actor.Write(0x152,[0]);Call(0x1FAD94,(long)actor.Address);
            int expected=id==148?6:id is >=368 and <=393?16:native.Read(0x673678,1)[0];
            Check(W(native.Read(task+4,2))==expected,"Non-owned projectile routing changed: "+id);
        }
        // Execute each relocated inline battle-table read with its native
        // indexed instruction. Changing unrelated source bytes must no longer
        // alter the collision-wait flag for registered new actions.
        using var lookup=new OwnedNativeMemory(4096);
        foreach(int site in new[]{0x212CE9,0x212D18})
        {
            List<byte> code=[0x57,0x48,0xBF];code.AddRange(BitConverter.GetBytes(image));
            code.AddRange([0x49,0x89,0xC9]);code.AddRange(native.Read(site,9)); // rdi=image;r9=rcx;native MOVSX
            code.AddRange([0x89,0xC8,0x5F,0xC3]);NativeLifetimeMemory.WriteProtected((long)lookup.Address,code.ToArray());lookup.ExecutablePage(0);
            var readWord=Marshal.GetDelegateForFunctionPointer<Eight>(lookup.Address);
            foreach(int s in ChemistActionBindings.ExtraSlots)
            {
                int id=ChemistActionBindings.TestActions[s].AbilityId;
                Check(readWord(id)==(0x800|(W(native.Read(0x683500+ChemistExtraEffects.ImpactTemplate(s)*2,2))&511)),"Battle reads wrong collision-wait effect word: "+s);
            }
            foreach(int id in new[]{0,28,146,368,393,406,453})
                Check((int)readWord(id)==BinaryPrimitives.ReadInt16LittleEndian(native.Read(0x683500+id*2,2)),"Original battle effect entry changed: "+id);
        }
        // Native collision handler stage2. Raster rendering and trigonometry
        // are explicit no-op fixtures; its actual medicine classification,
        // item getter, collision handshake and task completion remain real.
        Console.WriteLine("Fixture: native projectile classification and both effect lookups passed");
        Stub(0x311CB0,[0x31,0xC0,0xC3]);
        native.Write(0x186D768,BitConverter.GetBytes(image+task));Int(0x37A5C10,-1);
        using var flags=new OwnedNativeMemory(4096);using var scene=new OwnedNativeMemory(0x25000);
        using var controller=new OwnedNativeMemory(0x1B1000);using var ui=new OwnedNativeMemory(4096);
        using var script=new OwnedNativeMemory(4096);
        native.Write(0x3CD9E60,BitConverter.GetBytes((long)flags.Address));native.Write(0x3CC96B8,BitConverter.GetBytes((long)scene.Address));
        native.Write(0xDE2BC8,BitConverter.GetBytes((long)controller.Address));native.Write(0x3CDA758,BitConverter.GetBytes((long)ui.Address));
        native.Write(0x11A3840,BitConverter.GetBytes(image+0x1980000));
        for(int i=0;i<9;i++)script.Write(i*4,BitConverter.GetBytes(i==8?0:64));
        // Resource loading, camera, shape renderer and effect-task bytecode
        // are outside this fixture: phase3 creates an empty completed task.
        foreach(int rva in new[]{0x24F5A0,0x2741E0,0x38DD00,0x381A54,0xD258C,0x31A994})Stub(rva,[0x31,0xC0,0xC3]);
        foreach(int rva in new[]{0x26D9C8,0x26E194,0x2F4730,0x26DEF8,0x2F47F0,0x20781C,0x1FE94C,0x1FAF54,0xEF3DC,0x20CB0C})Stub(rva,[0x31,0xC0,0xC3]);
        Int(0x3CD9DC0,0);native.Write(0x3CD9DC0,new byte[8]);Int(0x1856AA8,0);
        // End-mode UI/scene is an explicit fixture, now at its UNHOOKED entry.
        // The real actionExecuteProcs wait gates still run. This does not
        // validate rendering or the full native scene transition.
        long endingOriginal=image+0x2059AC;
        NativeLifetimeMemory.WriteProtected(endingOriginal,new byte[]{0x48,0xB8}.Concat(BitConverter.GetBytes(image+0xC6B1CC)).Concat(new byte[]{0xC7,0,0x2E,0,0,0,0x31,0xC0,0xC3}).ToArray());
        // Reproduce the dump's missing preparation in the original native
        // stage1, without executing its known divide-by-zero stage2. A legacy
        // Potion has the same item projectile and relies on external setup.
        native.Write(0x186D778,new byte[16]);Int(0x186D770,0);actor.Write(0x142,BitConverter.GetBytes((ushort)368));
        actor.Write(0x1C8,BitConverter.GetBytes((ushort)240));
        try
        {
            Stub(0x1FADFF,[0xB9,0x7E,1,0,0]);Call(0x1FAD94,(long)actor.Address);
            native.Write(task+0x58,new byte[8]);Call(0x315B10);
        }
        finally{Stub(0x1FADFF,expandedBranch);}
        Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x37A2758,4))==0&&W(native.Read(0x37A5BF8,2))==0,
            "Unprepared native stage1 did not reproduce the dump's zero distance/duration");
        foreach(ushort id in ChemistMedicineFamilies.All)
        {
            ushort item=ChemistMedicineFamilies.Item(id);
            actor.Write(0x142,BitConverter.GetBytes(id));actor.Write(0x150,BitConverter.GetBytes(item));actor.Write(0x1C8,BitConverter.GetBytes(item));
            foreach(byte weapon in new byte[]{0,1,7,15})
            foreach(int distance in new[]{0,1,2,4})
            {
                actor.Write(0x152,[weapon]);targetWorker.Write(0x4F,[(byte)(8+distance),8]);
                native.Write(0x186D778,new byte[16]);Int(0x186D770,0);Int(0x18732C8,1);
                byte stock=native.Read(0x11A7C00+item,1)[0];
                Call(0x1FAD94,(long)actor.Address);
                Check(W(native.Read(task+4,2))==16&&BinaryPrimitives.ReadInt32LittleEndian(native.Read(task+0x28,4))==item,"Potion projectile lost selected medicine identity");
                native.Write(task+0x1C,[0,0]);native.Write(task+0x21,[0]);native.Write(task+0x58,new byte[8]);Call(0x315B10);
                Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x37A2758,4))>0&&W(native.Read(0x37A5BF8,2))>0,"Potion reproduced zero-divisor crash after corrected preparation");
                for(int frame=0;frame<128&&BinaryPrimitives.ReadInt32LittleEndian(native.Read(task+8,4))==2;frame++)Call(0x315B10);
                Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x18732C8,4))==2&&BinaryPrimitives.ReadInt32LittleEndian(native.Read(task+8,4))==3,"Potion did not finish its actual native flight");
                Check(W(actor.Read(0x142,2))==id&&native.Read(0x11A7C00+item,1)[0]==stock,"Projectile changed action or consumed stock a second time");
            }
            foreach(bool self in new[]{true,false})
            {
                actor.Write(0x1AA,[(byte)(self?0:1)]);targetWorker.Write(0x4F,[8,8]);
                Int(0x18732C8,1);Int(task+8,0);
                // Native same-tile targeting still has a 12-unit height bias,
                // covered by distance0 above. Exact zero-length vectors need
                // a separate explicit geometry fixture, not a false premise.
                byte[] prepare=native.Read(0x312ED0,14);
                try
                {
                    if(!self){native.Write(0x186D778,new byte[16]);Stub(0x312ED0,[0xC3]);}
                    Call(0x1FAD94,(long)actor.Address);
                }
                finally{Stub(0x312ED0,prepare);}
                Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(task+8,4))==0&&BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x18732C8,4))==2,$"Self/coincident potion created a zero-length flight: id={id}; self={self}; vectors={Convert.ToHexString(native.Read(0x186D778,16))}; phase={BinaryPrimitives.ReadInt32LittleEndian(native.Read(task+8,4))}; impact={BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x18732C8,4))}");
            }
            actor.Write(0x1AA,[1]);
        }
        targetWorker.Write(0x4F,[9,8]);
        Console.WriteLine("Fixture: Potion family actual native trajectory preparation plus Ether/Remedy families, stage1 and full stage2 collision for all13 medicines, four weapon types, distances0/1/2/4 and self-use; exact coincident vectors are an explicit geometry fixture. Prior dump zero-divisor reproduced without executing its fatal stage2. Rendering/terrain are explicit fixtures.");
        foreach(int s in ChemistActionBindings.ExtraSlots)
        {
            Console.WriteLine($"Fixture: collision/completion {s}");
            int id=ChemistActionBindings.TestActions[s].AbilityId,item=ChemistActionBindings.Item(s);
            actor.Write(0x142,BitConverter.GetBytes((ushort)id));actor.Write(0x1C8,BitConverter.GetBytes((ushort)item));actor.Write(0x1A9,[1,1]);
            Call(0x1FAD94,(long)actor.Address);
            native.Write(task+0x1C,[0,0]);native.Write(task+0x21,[0]);native.Write(task+0x58,new byte[8]);
            Call(0x315B10); // real stage1: UV object, distance and duration
            Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x37A2758,4))>=16&&W(native.Read(0x37A5BF8,2))>0,
                "Real stage1 produced a zero divisor/duration: "+s);
            Int(0x18732C8,1);Int(0xC6B1CC,0x2D);
            actor.Write(0x340,[1]);Int(0xCDB824,1);
            Call(0x20CC50);Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0xC6B1CC,4))==0x2D,"Action ended before item impact");
            for(int frame=0;frame<128&&BinaryPrimitives.ReadInt32LittleEndian(native.Read(task+8,4))==2;frame++)Call(0x315B10);
            Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x18732C8,4))==2&&BinaryPrimitives.ReadInt32LittleEndian(native.Read(task+8,4))==3,"Collision did not release the pending native impact: "+s);
            int template=ChemistExtraEffects.ImpactTemplate(s),scriptId=W(native.Read(0x683500+template*2,2))&511;
            native.Write(0x1D330BC,BitConverter.GetBytes((ushort)scriptId));native.Write(0x3740220+scriptId*8,BitConverter.GetBytes((long)script.Address));
            Int(0x7B0758,1);Int(0xD40902,0);native.Write(0x18732D0,[0,0]);native.Write(0x1D330D4,[0,0]);
            Int(0x2E801F4,template);
            Call(0x319DA4);Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x18732C8,4))==3,"Impact failed phase2 ->3");
            Call(0x319DA4);Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x18732C8,4))==4,"Impact failed phase3 ->4");
            Call(0x319DA4);Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x18732C8,4))==0&&BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x2E801F4,4))==-1,"Completed impact retained its native lock");
            Int(0xCDB824,0);Call(0x20CC50);
            Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0xC6B1CC,4))==0x2D,"Action ignored a still-active throw sequence");
            actor.Write(0x340,[0]);Call(0x20CC50);
            Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0xC6B1CC,4))==0x2E,"Native action failed to proceed after collision/effect/throw completion: "+s);
        }
        Check(!logs.Any(l=>l.Contains("[Visual/Concluído]")),"Removed completion observer unexpectedly executed");
        Check(!logs.Any(l=>l.Contains("[Erro]")),string.Join("\n",logs));
        Console.WriteLine("PASS: new-ID weapon-projectile bug reproduced; all11 real native vector/height/duration preparation, stage1 and full stage2 flight (four weapon types; zeroed launch vectors, distances1/2/4 and self-use), both real inline effect lookups, collision1->2, effect controller2->3->4->0, action wait gates and unhooked native completion. Terrain/camera/resource/bytecode/raster/UI fixtures explicit: not rendered gameplay or glass-shatter verification.");
    }
}

using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using FFTModLoader.ContentExpansion;
using FFTModLoader.ContentExpansion.Runtime;

unsafe partial class Program
{
    static void ChemistAiTests(OwnedNativeMemory native,ChemistPlayableHost host,List<string> logs)
    {
        long img=(long)native.Address;
        T Field<T>(string name)=>(T)typeof(ChemistPlayableHost).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
        var bank=Field<NativeLifetimeMemory>("_aiCandidates");var common=Field<NativeLifetimeMemory>("_common");
        var session=Field<ChemistLiveSession>("_session");
        var saved=new Dictionary<long,byte[]>();
        void Save(int rva,int n)=>saved[img+rva]=native.Read(rva,n);
        foreach(var (rva,n) in new (int,int)[]{(0x1853CE0,21*512),(0x1873038,21),(0x1872598,16*34*4+16),
            (0x1872E20,128),(0x1872ED8,21*16),(0x18716A0,32),(0x18724CE,2),(0x187235D,1),(0x1873063,1),(0x186AF80,4),
            (0x11A7C00+240,14),(0x275980,14),(0x3170CC,14),(0x3883C0,14),(0x67E01D,1),(0x320504,14)})Save(rva,n);
        uint learnedBefore=session.ExtraLearning(2);
        int[] stockBefore=ChemistActionBindings.ExtraSlots.Select(session.Stock).ToArray();
        byte[] bankBefore=CheckedNativeRead.Read(bank.Address,16*64*4);
        long Call(int rva,long a=0,long b=0,long c=0,long d=0)=>Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+rva)(a,b,c,d);
        void Stock(ushort ability,int count)
        {
            int slot=ChemistMedicineFamilies.CombatSlot(ability);
            if(slot<4)native.Write(0x11A7C00+ChemistAiActions.Item(ability),[(byte)count]);
            else Call(0x2847F8,ChemistAiActions.Item(ability),count-session.Stock(slot));
        }
        void Worker(int index,int row,byte faction=0,byte bypass=0x20)
        {
            byte[] unit=new byte[512];unit[1]=1;unit[2]=2;unit[5]=faction;unit[6]=bypass;
            unit[0x12]=6;unit[0x29]=99;native.Write(0x1853CE0+index*512,unit);
            native.Write(0x1873038+index,[(byte)row]);
        }
        ushort[] Candidates(int row,int start,int count)=>Enumerable.Range(start,count)
            .Select(i=>(ushort)(W(CheckedNativeRead.Read(bank.Address+(row*64+i)*4,2))&1023)).ToArray();
        using var buffer=new OwnedNativeMemory(4096);using var secondary=new OwnedNativeMemory(4096);
        using var aiCode=new OwnedNativeMemory(4096);
        try
        {
            Console.WriteLine("Fixture: AI candidate rows");
            Check(ChemistAiActions.All.Length==25&&ChemistAiActions.All.Distinct().Count()==25,"AI actual-item catalog aliases variants");
            session.RestoreExtraLearning(2,ChemistActionBindings.ExtraMask);
            foreach(ushort ability in ChemistAiActions.All)Stock(ability,3);
            byte[] adjacent=native.Read(0x1872E18,8);
            for(int row=0;row<16;row++)
            {
                Worker(row,row);bank.Write(row*256,Enumerable.Repeat((byte)0xDA,256).ToArray());
                Check(Call(0x316D24,row,6,1)==26,"AI command candidate count differs");
                Check(Candidates(row,1,25).SequenceEqual(ChemistAiActions.All),"AI command omitted actual medicine/flask");
                Check(CheckedNativeRead.Read(bank.Address+row*256,4).All(b=>b==0xDA)&&
                    CheckedNativeRead.Read(bank.Address+row*256+26*4,152).All(b=>b==0xDA),"AI row overwrote neighboring command storage");
                for(int i=1;i<=25;i++)
                {
                    byte[] entry=CheckedNativeRead.Read(bank.Address+row*256+i*4,4);
                    Check(W(entry)>>10==row&&entry[2]==6&&entry[3]==0,"AI full-width worker/action encoding differs");
                }
            }
            Check(native.Read(0x1872E18,8).SequenceEqual(adjacent),"Widened AI candidates corrupted following native table");
            Worker(0,0);
            Console.WriteLine("Fixture: AI stock and paid learning");
            foreach(ushort ability in ChemistAiActions.All)
            {
                Stock(ability,0);Check(Call(0x316D24,0,6,1)==25,"Zero-stock AI candidate not excluded");
                Check(!Candidates(0,1,24).Contains(ability),"AI retained depleted item");Stock(ability,3);
            }
            // Paid group learning is independent of inventory. Actual native
            // retained bits + expanded sidecar bits are used, no bypass flag.
            Worker(0,0,0,0);byte[] bits=new byte[3];ChemistActionBindings.LearnRetained(bits,0);
            native.Write(0x1853CE0+0xA5,bits);session.RestoreExtraLearning(2,0);
            Check(Call(0x316D24,0,6,1)==5&&Candidates(0,1,4).SequenceEqual(ChemistPotionFamily.Abilities),"AI grouped learning did not unlock four real potions only");
            native.Write(0x1853CE0+0xA5,new byte[3]);Check(Call(0x316D24,0,6,1)==1,"Unlearned AI command gained stocked actions");
            session.RestoreExtraLearning(2,ChemistActionBindings.ExtraMask);
            Worker(0,0);
            Worker(1,1);native.Write(0x18724CE,[0,0]);native.Write(0x1872ED8,new byte[21*16]);
            native.Write(0x1853CE0+512+0x30,[10,0,150,0,1,0,100,0]);
            // The target prefilter is called repeatedly across actions/tiles.
            // Benchmark the real hooked body, not a managed substitute.
            buffer.Write(256,[..BitConverter.GetBytes(ChemistAiActions.All[0]),6,0]);
            Call(0x320504,(long)buffer.Address+256);
            var prefilter=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x38B34C);
            for(int i=0;i<100;i++)prefilter(1,0);
            var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<10000;i++)prefilter(1,0);
            timer.Stop();
            Console.WriteLine($"PERFORMANCE: 10000 native AI target prefilters = {timer.Elapsed.TotalMilliseconds:F2} ms");
            Check(timer.Elapsed.TotalMilliseconds<250,"Native AI hot path unexpectedly slow");
            foreach(ushort ability in ChemistAiActions.All)
            {
                buffer.Write(256,[..BitConverter.GetBytes(ability),6,0]);Call(0x320504,(long)buffer.Address+256);
                Check(W(native.Read(0x18716A2,2))==ability&&W(native.Read(0x18716A6,2))==ChemistAiActions.Item(ability),"Native AI selection lost full ability/real item");
                byte[] data=CheckedNativeRead.Read(Call(0x2BB060,ability),20);
                Check(data[8]==107&&data[0]==4,"AI did not receive modified formula/range");
                byte[] ai=CheckedNativeRead.Read(common.Address+ability*8+4,3);ai[2]|=0x40;
                Check(native.Read(0x18716AD,3).SequenceEqual(ai),"AI target/status policy differs from selected common record: "+ability);
                int slot=ChemistMedicineFamilies.CombatSlot(ability);
                Check(native.Read(0x18716B1,1)[0]==(slot>=4?ChemistExtraEffects.Element(slot):0),"AI element differs from actual selected flask");
                long[] policy=new long[2];
                for(int side=0;side<2;side++)
                {native.Write(0x1872EE0+16,[(byte)side]);policy[side]=Call(0x38B34C,1,0);}
                Check(policy.All(p=>p is >=0 and <=2)&&policy.Any(p=>p>0),"Native AI rejected all targets for modified action: "+ability);
                if(slot<4)Check(policy[0]==0&&policy[1]==2,"Native medicine policy no longer favors allies");
                if(slot>=4)
                {
                    int side=slot>=12?1:0;native.Write(0x1872EE0+16,[(byte)side]);
                    if(slot is >=6 and <=8)
                    {
                        native.Write(0x1853CE0+512+0x77,[(byte)ChemistExtraEffects.Element(slot)]);
                        Check(Call(0x38B34C,1,0)==0,"Native AI ignored elemental immunity");native.Write(0x1853CE0+512+0x77,[0]);
                    }
                    // Result2 is native "simulate effect", NOT a promise that
                    // a status will succeed. Original formula107/status result
                    // simulation performs immunity/redundancy checks later.
                }
                Stock(ability,0);Check(Call(0x38B34C,1,0)==0,"Native AI availability let depleted item reach target scoring");Stock(ability,3);
            }
            // Execute original SetupUnitAbilityFlags and original secondary
            Console.WriteLine("Fixture: AI native secondary builder");
            // SetUACommand for a full16-row secondary. Only its row lookup and
            // paid-learning helper are controlled fixtures; writer is real.
            byte[] secondaryBefore=CheckedNativeRead.Read(common.Address+368*8,16*8);
            saved[common.Address+368*8]=secondaryBefore;
            for(int i=0;i<16;i++)
            {
                secondary.Write(i*2,BitConverter.GetBytes((ushort)(368+i)));
                NativeLifetimeMemory.WriteProtected(common.Address+(368+i)*8+7,[(byte)(secondaryBefore[i*8+7]|0x80)]);
            }
            NativeLifetimeMemory.WriteProtected(img+0x275980,[0x48,0xB8,..BitConverter.GetBytes((long)secondary.Address),0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x3170CC,[0xB8,1,0,0,0,0xC3]);native.Write(0x67E01D,[0]);
            native.Write(0x1853CE0+0x13,[13]);Call(0x316B04,0);
            Check(Candidates(0,1,25).SequenceEqual(ChemistAiActions.All)&&
                Candidates(0,26,16).SequenceEqual(Enumerable.Range(368,16).Select(i=>(ushort)i)),"Native secondary command lost actions after capacity34");
            Check(W(CheckedNativeRead.Read(bank.Address+42*4,2))==367&&
                CheckedNativeRead.Read(bank.Address+42*4+2,2).SequenceEqual(new byte[]{169,132}),
                "Native terminal action missing");
            Check(CheckedNativeRead.Read(bank.Address+43*4+2,1)[0]==255,"Native widened row sentinel missing");
            // Resumable original controller visits all43 actions and resumes at
            Console.WriteLine("Fixture: AI native controller traversal");
            // saved cursor40. FundamentalRoutine_0 alone is a native counter/
            // yield fixture. Selection is an outer no-op for the synthetic
            // secondary/attack records; all25 real Chemist selections above
            // execute the native builder/hook with their actual metadata.
            buffer.Write(0,new byte[4096]);aiCode.Write(0,[0x48,0xB8,..BitConverter.GetBytes((long)buffer.Address+128),0xFF,0,0x31,0xC0,0xC3]);aiCode.ExecutablePage(0);
            NativeLifetimeMemory.WriteProtected(img+0x3883C0,[0xFF,0x25,0,0,0,0,..BitConverter.GetBytes((long)aiCode.Address)]);
            NativeLifetimeMemory.WriteProtected(img+0x320504,[0x31,0xC0,0xC3]);
            native.Write(0x18724CE,[0,0]);native.Write(0x1873063,[0]);
            Check(Call(0x38C8F8)==0&&BinaryPrimitives.ReadInt32LittleEndian(buffer.Read(128,4))==43&&native.Read(0x187235D,1)[0]==43,"Native AI controller stopped at old34 bound");
            buffer.Write(128,new byte[4]);native.Write(0x187235D,[40]);native.Write(0x1873063,[1]);
            Check(Call(0x38C8F8)==0&&BinaryPrimitives.ReadInt32LittleEndian(buffer.Read(128,4))==3,"Native AI resumed wrong widened action cursor");
            // Execute biased Tmp_UA+EF8/EFb accesses too, without relocating
            // Tmp_UA itself. Controlled selection has a simple native AI flag.
            native.Write(0x18716A0,new byte[32]);native.Write(0x18716AF,[0x80]);
            Call(0x3167E4,0);
            Check(Enumerable.Range(34,9).All(i=>(CheckedNativeRead.Read(bank.Address+i*4+3,1)[0]&7)==0),
                "Biased native SetUnitParameter flags still use old bank/34 bound");
            Check(native.Read(0x1872E18,8).SequenceEqual(adjacent),"Biased native AI writes corrupted following table");
            NativeLifetimeMemory.WriteProtected(img+0x320504,saved[img+0x320504]);
            // NPCs retain native level eligibility and NEVER touch PartyItem
            Console.WriteLine("Fixture: AI NPC supplies and commitment");
            // or expanded party supplies, even with actual commit flags1.
            foreach(byte faction in new byte[]{0x10,0x20})
            {
                Worker(0,0,faction);foreach(ushort ability in ChemistAiActions.All)Stock(ability,0);
                ushort[] eligible=ChemistAiActions.All.Where(a=>CheckedNativeRead.Read(Call(0x2B8C44,ChemistAiActions.Item(a))+11,1)[0]<=99).ToArray();
                Check(Call(0x316D24,0,6,1)==eligible.Length+1&&Candidates(0,1,eligible.Length).SequenceEqual(eligible),"NPC uses party inventory instead of native level eligibility");
                foreach(ushort ability in eligible)
                {
                    byte[] packet=new byte[20];packet[1]=6;BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),ability);
                    buffer.Write(256,packet);native.Write(0x186AF80,new byte[4]);
                    Check(Call(0x281488,(long)buffer.Address+256,(long)buffer.Address+512,1)==1,"NPC could not commit eligible action");
                    Check(W(buffer.Read(520,2))==ChemistAiActions.Item(ability),"NPC commit lost item identity");
                }
                Check(ChemistAiActions.All.All(a=>ChemistMedicineFamilies.CombatSlot(a)<4?
                    native.Read(0x11A7C00+ChemistAiActions.Item(a),1)[0]==0:session.Stock(ChemistMedicineFamilies.CombatSlot(a))==0),"NPC consumed party stock");
                native.Write(0x1853CE0+0x29,[0]);
                eligible=ChemistAiActions.All.Where(a=>CheckedNativeRead.Read(Call(0x2B8C44,ChemistAiActions.Item(a))+11,1)[0]==0).ToArray();
                Check(Call(0x316D24,0,6,1)==eligible.Length+1,"NPC native item level cap ignored");
            }
            Worker(0,0);foreach(ushort ability in ChemistAiActions.All)Stock(ability,2);
            foreach(ushort ability in ChemistAiActions.All)
            {
                byte[] packet=new byte[20];packet[1]=6;BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),ability);
                buffer.Write(256,packet);native.Write(0x186AF80,new byte[4]);
                Check(Call(0x281488,(long)buffer.Address+256,(long)buffer.Address+512,0)==1,"AI prediction rejected stocked action");
                Check(Call(0x281488,(long)buffer.Address+256,(long)buffer.Address+512,1)==1,"AI party commit rejected stocked action");
                int count=ChemistMedicineFamilies.CombatSlot(ability)<4?native.Read(0x11A7C00+ChemistAiActions.Item(ability),1)[0]:session.Stock(ChemistMedicineFamilies.CombatSlot(ability));
                Check(count==1,"AI party commitment consumed wrong stack or preview consumed");
                Stock(ability,0);Check(Call(0x281488,(long)buffer.Address+256,(long)buffer.Address+512,1)==-1,"Empty AI commitment accepted");
            }
            Check(!logs.Any(l=>l.Contains("[Erro]")),string.Join("\n",logs));
            NativeLifetimeMemory.WriteProtected(img+0x3170CC,saved[img+0x3170CC]);
            CheckAiEligibilityLeaf(native,host,session);
            Console.WriteLine("Fixture: AI all25 distinct learned/stocked medicine/flask candidates across16 widened64 rows; full16-action native secondary preserved past34; real SetupUnitAbilityFlags terminal/sentinel, biased SetUnitParameter flag writes and original EvaluateAllUnitAction43-action traversal/resume; unlearned/zero-stock exclusion, native NPC level supplies/no party consumption, all25 real selection formula107/range4, original target prefilter/elemental nullification and preview/own-stack commitment. Secondary learning/row and controller selection/scoring/scheduling are explicit outer fixtures; real battle AI decisions require gameplay test.");
        }
        finally
        {
            foreach(var p in saved)NativeLifetimeMemory.WriteProtected(p.Key,p.Value);
            bank.Write(0,bankBefore);session.RestoreExtraLearning(2,learnedBefore);
            for(int i=0;i<stockBefore.Length;i++){int slot=ChemistActionBindings.ExtraSlots.ElementAt(i);Call(0x2847F8,ChemistActionBindings.Item(slot),stockBefore[i]-session.Stock(slot));}
        }
    }

    static void CheckAiEligibilityLeaf(OwnedNativeMemory native,ChemistPlayableHost host,ChemistLiveSession session)
    {
        long img=(long)native.Address;
        var hooks=((System.Collections.IEnumerable)typeof(ChemistPlayableHost).GetField("_hooks",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Cast<object>().ToArray();
        long original=(nint)hooks[^1].GetType().GetProperty("OriginalFunctionAddress")!.GetValue(hooks[^1])!;
        long mirror=(long)typeof(ChemistLiveSession).GetField("_nativeLearning",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!;
        byte[] originalBytes=CheckedNativeRead.Read(original,16),party=native.Read(0x11A7D10+2*600,600);
        var eligible=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x38B34C);
        long Call(int rva,long a=0,long b=0)=>Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+rva)(a,b);
        void Stock(ushort a,int n)
        {
            int s=ChemistMedicineFamilies.CombatSlot(a);
            if(s<4)native.Write(0x11A7C00+ChemistAiActions.Item(a),[(byte)n]);
            else Call(0x2847F8,ChemistAiActions.Item(a),n-session.Stock(s));
        }
        try
        {
            // Sentinel original isolates eligibility from genuine target scoring,
            // which is tested separately above against the untouched real body.
            NativeLifetimeMemory.WriteProtected(original,[0xB8,0x34,0x12,0,0,0xC3]);
            byte[] record=party.ToArray();record[0]=4;record[1]=2;native.Write(0x11A7D10+2*600,record);
            native.Write(0x18716A0,[6]);native.Write(0x18724CE,[0]);
            byte[] worker=new byte[512];worker[1]=1;worker[2]=2;worker[0x29]=99;native.Write(0x1853CE0,worker);
            foreach(ushort a in ChemistAiActions.All)
            {
                native.Write(0x18716A2,BitConverter.GetBytes(a));Stock(a,2);
                int s=ChemistMedicineFamilies.CombatSlot(a);
                foreach(bool learned in new[]{false,true,false,true})
                {
                    byte[] bits=new byte[3];if(learned&&s<4)ChemistActionBindings.LearnRetained(bits,s);
                    native.Write(0x1853CE0+0xA5,bits);
                    session.RestoreExtraLearning(2,learned&&s>=4?1u<<(s-4):0);
                    Check(eligible(1,0)==(learned?0x1234:0),"Native hot-path learning mismatch: "+a);
                }
                Stock(a,0);Check(eligible(1,0)==0,"Native leaf accepted empty actual stack: "+a);Stock(a,2);
                NativeLifetimeMemory.WriteProtected(mirror,[0]);Check(eligible(1,0)==0,"Blocked session admitted AI action");NativeLifetimeMemory.WriteProtected(mirror,[1]);
                if(s>=4)
                {
                    foreach(int offset in new[]{0,1,4,5,6,0x11C,0x11D,0x124})
                    {
                        native.Write(0x11A7D10+2*600+offset,[(byte)(record[offset]^1)]);
                        Check(eligible(1,0)==0,"Stale serialized identity admitted extra learning");
                        native.Write(0x11A7D10+2*600+offset,[record[offset]]);
                    }
                }
                // Both enemy/guest faction encodings use their own level supply,
                // not party stock. Bypass is the original native NPC policy.
                Stock(a,0);native.Write(0x1853CE0+6,[0x20]);
                byte level=CheckedNativeRead.Read(Call(0x2B8C44,ChemistAiActions.Item(a))+11,1)[0];
                foreach(byte faction in new byte[]{0x10,0x20,0x30})
                {
                    native.Write(0x1853CE0+5,[faction]);native.Write(0x1853CE0+0x29,[level]);
                    Check(eligible(1,0)==0x1234,"Native NPC supplies/level boundary mismatch: "+a);
                    if(level>0){native.Write(0x1853CE0+0x29,[(byte)(level-1)]);Check(eligible(1,0)==0,"Native NPC below required level admitted");}
                }
                native.Write(0x1853CE0,worker);Stock(a,2);
            }
            foreach(ushort a in new ushort[]{0,367,384,512,524,1023,1024,65535})
            {native.Write(0x18716A2,BitConverter.GetBytes(a));Check(eligible(1,0)==0x1234,"Unregistered ability failed native pass-through");}
            native.Write(0x18716A2,BitConverter.GetBytes((ushort)513));
            native.Write(0x1853CE0+6,[0x20]);
            foreach(byte invalid in new byte[]{21,54,255})
            {native.Write(0x18724CE,[invalid]);Check(eligible(1,0)==0,"Invalid acting worker admitted");}
            native.Write(0x18724CE,[0]);native.Write(0x1853CE0+1,[255]);Check(eligible(1,0)==0,"Inactive worker admitted");native.Write(0x1853CE0+1,[1]);
            native.Write(0x1853CE0+6,[0]);
            foreach(byte invalid in new byte[]{54,255})
            {native.Write(0x1853CE0+2,[invalid]);Check(eligible(1,0)==0,"Invalid serialized slot admitted");}
            native.Write(0x1853CE0+2,[2]);
            session.RestoreExtraLearning(2,1);
            byte[] savedMirror=CheckedNativeRead.Read(mirror+16+2*16,16);
            foreach(byte inactive in new byte[]{0,255})
            {
                native.Write(0x11A7D10+2*600,[inactive]);NativeLifetimeMemory.WriteProtected(mirror+16+2*16+4,[inactive]);
                Check(eligible(1,0)==0,"Inactive party identity admitted");
            }
            native.Write(0x11A7D10+2*600,[record[0]]);NativeLifetimeMemory.WriteProtected(mirror+16+2*16,savedMirror);
            foreach(byte cmd in new byte[]{0,1,5,7,255}){native.Write(0x18716A0,[cmd]);Check(eligible(1,0)==0x1234,"Unrelated command failed native pass-through");}
            CheckAiEligibilityAbiAndUnwind(host);
            CheckAiEligibilityAbiAndUnwind(host,1,false);
            CheckAiLearnedLeaf(native,host,session);
            CheckAiSelectionGate(native,host);
            Console.WriteLine("Fixture: native AI eligibility hot path: all25 actual items, paid retained/extra learning, fresh stock, immutable identity replacement, blocked session, three NPC factions/level boundary, unrelated action pass-through; private GP/SIMD and OS unwind verified; no CLR handoff.");
        }
        finally
        {
            NativeLifetimeMemory.WriteProtected(original,originalBytes);native.Write(0x11A7D10+2*600,party);
        }
    }

    static void CheckAiEligibilityAbiAndUnwind(ChemistPlayableHost host,int which=2,bool registers=true)
    {
        var allocations=(List<NativeLifetimeMemory>)typeof(ChemistPlayableHost).GetField("_memory",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
        var bodies=allocations.Where(m=>CheckedNativeRead.Read(m.Address,11).SequenceEqual(Convert.FromHexString("5051524150415141524153"))).ToArray();
        Check(bodies.Length==3,"Expected two native AI queries and selection gate");long target=bodies[which].Address;
        byte[] body=CheckedNativeRead.Read(target,1024);
        int epilogue=body.AsSpan().IndexOf(Convert.FromHexString("415B415A415941585A5958"));
        Check(epilogue>11,"Native AI epilogue missing");
        using var stack=new OwnedNativeMemory(4096);using var context=new OwnedNativeMemory(4096);
        const long ret=0x1234567812345678;
        foreach(var (pc,allocation) in new[]{(1,8),(5,32),(11,56),(epilogue-1,56),(epilogue,56),(epilogue+2,48),(epilogue+10,8)})
        {
            long sp=(long)stack.Address+256;
            stack.Write(256+allocation,BitConverter.GetBytes(ret));
            context.Write(48,BitConverter.GetBytes(0x10000Bu));context.Write(152,BitConverter.GetBytes(sp));context.Write(248,BitConverter.GetBytes(target+pc));
            nint f=RtlLookupFunctionEntry((ulong)(target+pc),out ulong imageBase,0);Check(f!=0,"Native AI leaf lacks OS unwind");
            RtlVirtualUnwind(0,imageBase,(ulong)(target+pc),f,context.Address,out _,out _,0);
            Check(Marshal.ReadInt64(context.Address,248)==ret&&Marshal.ReadInt64(context.Address,152)==sp+allocation+8,"Native AI OS unwind mismatch");
        }
        if(!registers)return;
        // Standard caller frame; store post-call volatile GP and XMM0. All
        // SIMD opcodes in the leaf are absent, so XMM1..15 remain untouched too.
        using var caller=new OwnedNativeMemory(4096);using var output=new OwnedNativeMemory(4096);
        byte[] prefix=Convert.FromHexString("53564883EC284889CB4889D6B911000000BA2200000041B83300000041B94400000041BA5500000041BB66000000B877000000660F6EC0FFD3488906");
        byte[] suffix=Convert.FromHexString("48894E08488956104C8946184C894E204C8956284C895E30660F7E46384883C4285E5BC3");
        caller.Write(0,[..prefix,..suffix]);caller.ExecutablePage(0);
        Marshal.GetDelegateForFunctionPointer<Eight>(caller.Address)(target,(long)output.Address);
        foreach(var (offset,value) in new[]{(8,0x11),(16,0x22),(24,0x33),(32,0x44),(40,0x55),(48,0x66)})
            Check(Marshal.ReadInt64(output.Address,offset)==value,"Native AI private volatile register lost");
        Check(Marshal.ReadInt32(output.Address,56)==0x77,"Native AI SIMD register changed");
    }
    static void CheckAiLearnedLeaf(OwnedNativeMemory native,ChemistPlayableHost host,ChemistLiveSession session)
    {
        long img=(long)native.Address;
        var hooks=((System.Collections.IEnumerable)typeof(ChemistPlayableHost).GetField("_hooks",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Cast<object>().ToArray();
        long original=(nint)hooks[17].GetType().GetProperty("OriginalFunctionAddress")!.GetValue(hooks[17])!;
        byte[] before=CheckedNativeRead.Read(original,16),worker=native.Read(0x1853CE0,512);
        long mirror=(long)typeof(ChemistLiveSession).GetField("_nativeLearning",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!;
        var learned=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x3170CC);
        try
        {
            NativeLifetimeMemory.WriteProtected(original,[0xB8,0x34,0x12,0,0,0xC3]);
            CheckAiEligibilityAbiAndUnwind(host,0);
            native.Write(0x1853CE0+6,[0]);
            for(int s=0;s<15;s++)foreach(bool yes in new[]{false,true,false,true})
            {
                byte[] bits=new byte[3];if(s<4&&yes)ChemistActionBindings.LearnRetained(bits,s);
                native.Write(0x1853CE0+0xA5,bits);session.RestoreExtraLearning(2,s>=4&&yes?1u<<(s-4):0);
                for(int probe=0;probe<16;probe++){long value=learned(0,6,probe);Check(value==(yes&&probe==s?1:0),$"Native CheckLearned mask differs: learned={s}/{yes}, query={probe}, result={value}");}
            }
            native.Write(0x1853CE0+6,[0x20]);Check(learned(0,6,14)==1&&learned(0,6,15)==0,"Native NPC bypass/bounded catalog differs");
            NativeLifetimeMemory.WriteProtected(mirror,[0]);Check(learned(0,6,0)==0,"Blocked learning accepted");NativeLifetimeMemory.WriteProtected(mirror,[1]);
            foreach(int command in new[]{0,1,5,7,255})Check(learned(0,command,0)==0x1234,"Unrelated learned query did not pass through");
            foreach(int slot in new[]{-1,16,255})Check(learned(0,6,slot)==0x1234,"Out-of-contract learned query changed");
            foreach(int index in new[]{-1,21,255})Check(learned(index,6,0)==0,"Invalid learned worker admitted");
            for(int i=0;i<100;i++)learned(0,6,0);
            var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<10000;i++)learned(0,6,0);timer.Stop();
            Console.WriteLine($"PERFORMANCE: 10000 native AI learned queries = {timer.Elapsed.TotalMilliseconds:F2} ms");
            Check(timer.Elapsed.TotalMilliseconds<250,"Native learned hot path unexpectedly slow");
            Console.WriteLine("Fixture: native AI CheckLearned hot path all15 slots, retained masks, paid extra learning, NPC bypass, mutable gate and unrelated-command pass-through without CLR handoff.");
        }
        finally{NativeLifetimeMemory.WriteProtected(original,before);native.Write(0x1853CE0,worker);session.RestoreExtraLearning(2,ChemistActionBindings.ExtraMask);}
    }
    static void CheckAiSelectionGate(OwnedNativeMemory native,ChemistPlayableHost host)
    {
        var hooks=((System.Collections.IEnumerable)typeof(ChemistPlayableHost).GetField("_hooks",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Cast<object>().ToArray();
        long original=(nint)hooks[19].GetType().GetProperty("OriginalFunctionAddress")!.GetValue(hooks[19])!;
        byte[] before=CheckedNativeRead.Read(original,16);
        using var request=new OwnedNativeMemory(4096);
        var select=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x320504);
        try
        {
            NativeLifetimeMemory.WriteProtected(original,[0xB8,0x34,0x12,0,0,0xC3]);
            foreach(byte command in new byte[]{0,1,5,7,23,169,255}){request.Write(0,[0,0,command,0]);Check(select((long)request.Address)==0x1234,"Unrelated selection did not call original");}
            Check(select(0)==0x1234,"Null selection fallback changed");
            var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<10000;i++)select((long)request.Address);timer.Stop();
            Console.WriteLine($"PERFORMANCE: 10000 native non-item AI selections = {timer.Elapsed.TotalMilliseconds:F2} ms");
            Check(timer.Elapsed.TotalMilliseconds<250,"Unrelated native selection handoff unexpectedly slow");
            Console.WriteLine("Fixture: native selection command gate passes non-item monster/job actions to original without CLR handoff; all25 item selections still execute approved observer and original builder; gate OS unwind verified.");
        }
        finally{NativeLifetimeMemory.WriteProtected(original,before);}
    }
}

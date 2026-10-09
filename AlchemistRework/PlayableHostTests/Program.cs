using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using FFTModLoader.ContentExpansion;
using FFTModLoader.ContentExpansion.Runtime;
using Reloaded.Hooks;
using Reloaded.Hooks.ReloadedII.Interfaces;

unsafe partial class Program
{
    static byte[] ReceiverBefore=[];
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate long Eight(long a=0,long b=0,long c=0,long d=0,long e=0,long f=0,long g=0,long h=0);
    static void Main(string[] args)
    {
        if(args.Contains("--audit-backup"))
        {
            string backup=args[Array.IndexOf(args,"--audit-backup")+1];
            var registry=new ExpandedSaveRegistry(ChemistActionBindings.ExtraKeys,ChemistActionBindings.ExtraKeys);
            var store=new ExpandedSaveStore(Path.Combine(backup,"sidecars"),registry);
            var commit=new NativePartySaveCommit(registry,store);int restored=0;
            foreach(string file in Directory.GetFiles(Path.Combine(backup,"autoenhanced.png.decoded"),"*.sav"))
            {
                byte[] savedPacket=File.ReadAllBytes(file);
                if(savedPacket.Length!=40524)continue;
                byte[] savedWork=savedPacket.AsSpan(0x164,NativePartySaveCommit.WorkSize).ToArray();
                var state=commit.RestoreConsumedWork("CCB8BB735DD82E9FCC00FFC3B7C1EC2A2CB64FFC814B1AD0AD3B480ABF8F509C",savedWork);
                Check(state is not null&&state.Units.Length==5&&state.Units.All(u=>u.LearnedKeys.Length==11),"Existing autosave backup lost paid learning: "+Path.GetFileName(file));restored++;
            }
            using var audit=System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(backup,"verification.json")));
            int expected=audit.RootElement.GetProperty("Matches").EnumerateArray().Count(m=>m.GetProperty("File").GetString()=="autoenhanced.png");
            Check(expected>0 && restored==expected,"Unexpected autosave fixture set");
            Console.WriteLine($"PASS: all {restored} backed-up field autosaves restore five units with eleven learned skills each, without modifying native saves or sidecars.");return;
        }
        if(args.Contains("--hooks-api"))
        {
            var inspected=Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"Reloaded.Hooks.ReloadedII.dll"));
            foreach(var t in inspected.GetTypes().Where(t=>typeof(IReloadedHooks).IsAssignableFrom(t)))
            {Console.WriteLine("IMPLEMENTATION "+t);foreach(var c in t.GetConstructors())Console.WriteLine(c);}
            foreach(var t in new[]{typeof(IReloadedHooks),typeof(ReloadedHooks)}.Concat(typeof(IReloadedHooks).GetInterfaces()))
            {Console.WriteLine(t.FullName);foreach(var m in t.GetMethods())Console.WriteLine(m);}
            return;
        }
        string work=Path.GetFullPath(args.Length>0?args[0]:".");
        string executable=@"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\FFT_enhanced.exe";
        byte[] image=File.ReadAllBytes(executable);
        using var pe=new PEReader(new MemoryStream(image,false));
        var native=new OwnedNativeMemory(pe.PEHeaders.PEHeader!.SizeOfImage);
        foreach(var s in pe.PEHeaders.SectionHeaders)if(s.SizeOfRawData>0)native.Write(s.VirtualAddress,image.AsSpan(s.PointerToRawData,s.SizeOfRawData).ToArray());
        // No PE entry point, imported function or game scene is run. This
        // image is exclusively an owned fixture for the audited leaf/hooks.
        var code=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".text");
        for(int p=code.VirtualAddress&~4095;p<code.VirtualAddress+code.SizeOfRawData;p+=4096)native.ExecutablePage(p);
        string temp=Path.Combine(Path.GetTempPath(),"FFTModLoader-playable-host-"+Guid.NewGuid().ToString("N"));
        string mod=Path.Combine(temp,"Mod");Directory.CreateDirectory(Path.Combine(mod,"native"));
        foreach(string key in ChemistActionBindings.ExtraKeys)
            foreach(string suffix in new[]{".item-common.bin",".item-medicine.bin"})File.Copy(Path.Combine(work,"AlchemistRework","builds","chemist-full-content-001","native",key+suffix),Path.Combine(mod,"native",key+suffix));
        File.Copy(Path.Combine(work,"AlchemistRework","builds","chemist-full-content-001","native","playable-relocation-plan.json"),Path.Combine(mod,"native","playable-relocation-plan.json"));
        File.Copy(Path.Combine(work,"AlchemistRework","analysis","chemist-ai-expansion-036.json"),Path.Combine(mod,"native","chemist-ai-expansion.json"));
        string bottleBuild=Path.Combine(work,"AlchemistRework","builds","world-bottle-atlas-018");
        File.Copy(Path.Combine(bottleBuild,"world-bottle-atlas.json"),Path.Combine(mod,"native","world-bottle-atlas.json"));
        foreach(string source in Directory.GetFiles(Path.Combine(bottleBuild,"FFTIVC"),"*",SearchOption.AllDirectories))
        {
            string target=Path.Combine(mod,Path.GetRelativePath(bottleBuild,source));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);
        }
        var bridge=new NativeContextBridge(Path.Combine(work,"AlchemistRework","builds","native-bridge-007","FFTModLoader.ContentExpansion.Native.dll"));
        var logs=new List<string>();
        // Forward the legacy controller contract to the installed implementation;
        // retain only fixture target/trampoline pairs for stubbing outer UI calls.
        var implementation=Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"Reloaded.Hooks.ReloadedII.dll"));
        IReloadedHooks hooks=(IReloadedHooks)Activator.CreateInstance(implementation.GetTypes().Single(t=>!t.IsAbstract&&typeof(IReloadedHooks).IsAssignableFrom(t)))!;
        using var existingEffect=new OwnedNativeMemory(4096);
        // Emulate the already active, approved Dark Knight effect detour.
        existingEffect.Write(0,[0x81,0xFA,0xB8,0,0,0,0x75,5,0xBA,0xA4,0,0,0,0x89,0xD0,0xC3]);existingEffect.ExecutablePage(0);
        var legacyEffect=hooks.CreateHook<Eight>((void*)existingEffect.Address,(long)native.Address+0x319B50);legacyEffect.Activate();
        // Reproduce the live JobExpansion decoder detour before Chemist
        // installs. Native stub counts entry-chain calls without any CLR jump.
        using var existingDecoder=new OwnedNativeMemory(8192);
        existingDecoder.ExecutablePage(0);
        SetDecoderFixture(existingDecoder,0);
        var legacyDecoder=hooks.CreateHook<Eight>((void*)existingDecoder.Address,(long)native.Address+0x3D25D8);legacyDecoder.Activate();
        byte[] decoderEntry=native.Read(0x3D25D8,14);
        var host=new ChemistPlayableHost(hooks,(long)native.Address,bridge,logs.Add);
        Console.WriteLine("Fixture: installing native routes");
        host.Install(executable,mod,()=>"fixture-profile",Path.Combine(temp,"Sidecars"));
        Check(native.Read(0x3D25D8,14).SequenceEqual(decoderEntry),"Existing JobExpansion decoder detour changed");
        Check(logs.Count(l=>l.StartsWith("[Runtime] .NET="))==1 &&
            logs.Single(l=>l.StartsWith("[Runtime] .NET=")).Contains("métodos Chemist preparados="),
            "Runtime preparation/configuration evidence missing");
        var completionSection=pe.PEHeaders.SectionHeaders.Single(s=>s.VirtualAddress<=0x2059AC&&0x2059AC+14<=s.VirtualAddress+s.SizeOfRawData);
        Check(native.Read(0x2059AC,14).SequenceEqual(image.AsSpan(completionSection.PointerToRawData+0x2059AC-completionSection.VirtualAddress,14).ToArray()),
            "Logging-only completion detour still installed");
        Console.WriteLine("Fixture: native routes installed");
        if(args.Contains("--layout-smoke")){MedicinePositionTests(native);FourthMenuTests(native);return;}
        // The following unhooked helpers/original outer callbacks are explicit
        // fixtures. They do NOT establish NEX/UI/gameplay behavior. Actual
        // native leaf contracts and original command bodies have separate tests.
        foreach(int rva in new[]{0x2BD05C,0xF4BCC,0x30FFC4})NativeLifetimeMemory.WriteProtected((long)native.Address+rva,[0x31,0xC0,0xC3]);
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x30E2F0,[0xB8,0x07,0,0,0,0xC3]);
        StubOriginal(host,[10,11,13,20,21,22,30,31,32,34]);
        StubOriginal(typeof(ChemistPlayableHost).GetField("_save",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!,[0,1,2]);
        // Only the special-character policy helper is stubbed; the enhanced
        // command filter, pair-builder entry and both sorting bodies are real.
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x2CBED4,[0x31,0xC0,0xC3]);
        using var menu=new OwnedNativeMemory(4096);using var output=new OwnedNativeMemory(4096);
        native.Write(0x1800F50,BitConverter.GetBytes((long)menu.Address));
        menu.Write(0x2C,BitConverter.GetBytes((ushort)2));menu.Write(0x24,BitConverter.GetBytes((ushort)74));menu.Write(0xCC,BitConverter.GetBytes((ushort)300));
        menu.Write(0x70,[1]);menu.Write(0x175,[9]);
        byte[] record=new byte[600];record[0]=4;record[1]=2;record[4]=1;record[5]=7;record[6]=3;record[0x124]=9;
        native.Write(0x11A7D10+2*600,record);native.Write(0x1811428,[0,0]);
        var session=(ChemistLiveSession)typeof(ChemistPlayableHost).GetField("_session",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
        long Call(int rva,long a=0,long b=0,long c=0,long d=0,long e=0,long f=0,long g=0,long h=0)=>Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+rva)(a,b,c,d,e,f,g,h);
        if(args.Contains("--ai-smoke")){ChemistAiTests(native,host,logs);return;}
        // Execute the real enhanced shop builder and original six-argument
        // list body. Only the campaign chapter lookup is an explicit fixture.
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x2E9898,[0xB8,0x01,0,0,0,0xC3]);
        long shopShared=((NativeLifetimeMemory)typeof(ChemistPlayableHost).GetField("_shared",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Address;
        long itemStockAddress=((NativeLifetimeMemory)typeof(ChemistPlayableHost).GetField("_stock",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Address;
        byte[] itemStockBefore=CheckedNativeRead.Read(itemStockAddress,NativeItemStockLeaf.StateLength);
        foreach(int progress in new[]{-1,0,1,2,4,5,6,8,9,10,12,13,14,16,20,1,13,5})
        for(int shop=0;shop<15;shop++)
        {
            NativeLifetimeMemory.WriteProtected((long)native.Address+0x2E9898,[0xB8,..BitConverter.GetBytes(progress),0xC3]);
            native.Write(0x1811684,BitConverter.GetBytes((ushort)shop));
            int n=(int)Call(0x36C3EC,5,1);
            ushort[] stock=Enumerable.Range(0,n).Select(i=>W(CheckedNativeRead.Read(shopShared+i*2,2))).ToArray();
            Check(ChemistActionBindings.ExtraSlots.All(s=>stock.Contains(ChemistActionBindings.Item(s))==(progress>=ChemistExtraEffects.ShopProgress(s))),$"Real enhanced shop builder story unlock differs: progress={progress}, shop={shop}, count={n}");
            Check(W(CheckedNativeRead.Read(shopShared+2*n,2))==65535 && stock.Distinct().Count()==n,"Story shop terminator/duplicate regression");
            foreach(int s in ChemistActionBindings.ExtraSlots)
                Check(CheckedNativeRead.Read(Call(0x2B8C44,ChemistActionBindings.Item(s))+10,1)[0]==ChemistExtraEffects.ShopProgress(s),"Item common story availability disagrees with shop policy");
        }
        Check(CheckedNativeRead.Read(itemStockAddress,NativeItemStockLeaf.StateLength).SequenceEqual(itemStockBefore),"Story shop listing mutated existing inventory");
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x2E9898,[0xB8,0x01,0,0,0,0xC3]);
        Console.WriteLine("Fixture: story shop unlocks at native progress1/5/9/13, all15 shops and boundary/load-regression transitions, zero/negative fail closed; owned inventory and native item metadata preserved.");
        using var order=new OwnedNativeMemory(4096);
        Console.WriteLine("Fixture: original sorting");
        order.Write(0,[0xF0,0,0xF3,0,0xFF,0]);
        NativeLifetimeMemory.WriteProtected(shopShared,[0xF0,0,0xF3,0,5,1,0xFF,0xFF]);
        var hostHooks=((System.Collections.IEnumerable)typeof(ChemistPlayableHost).GetField("_hooks",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Cast<object>().ToArray();
        long Original(int i,long a=0,long b=0,long c=0,long d=0)=>Marshal.GetDelegateForFunctionPointer<Eight>(
            (nint)hostHooks[i].GetType().GetProperty("OriginalFunctionAddress")!.GetValue(hostHooks[i])!)(a,b,c,d);
        Check(Original(25,(long)order.Address,shopShared)==2 && W(CheckedNativeRead.Read(shopShared+4,2))==65535,
            "Regression setup did not reproduce original preset-sort loss");
        NativeLifetimeMemory.WriteProtected(shopShared,[0xF0,0,0xF3,0,5,1,0xFF,0xFF]);
        Check(Call(0x285DF0,(long)order.Address,shopShared)==3 && W(CheckedNativeRead.Read(shopShared+4,2))==261,
            "Real preset order discarded new shop item");
        native.Write(0x1874726,[0xF0,0,0xF3,0,0xFF,0]);
        Check(Call(0x286228,8,shopShared,-1)==3 && W(CheckedNativeRead.Read(shopShared+4,2))==261,
            "Real user-order validity/filter stage discarded Venom");
        Check(ChemistActionBindings.ExtraSlots.All(s=>Call(0x2B8EBC,ChemistActionBindings.Item(s))==1)&&Call(0x2B8EBC,272)==0,"Extra validity route broadened to unregistered items");
        NativeLifetimeMemory.WriteProtected(shopShared,[0xF0,0,0xFF,0xFF]);
        Check(Call(0x285DF0,(long)order.Address,shopShared)==1 && W(CheckedNativeRead.Read(shopShared+2,2))==65535,
            "Sort injected Venom into a list that did not contain it");
        long scratch=Original(23,6,0,0);
        Console.WriteLine("Fixture: action menus");
        Check(W(CheckedNativeRead.Read(scratch+8,2))==0,"Regression setup did not reproduce original enhanced 421-bound loss");
        // Action tab: execute real 28A6E8 -> 28A400 -> 28A370 -> 28A474.
        // Original 28A370 zeros 513 at its 421 bound. No outer-list stub here.
        Check(Call(0x28A6E8,0,75,0,(long)output.Address,2)==15 && W(output.Read(34,2))==513,
            "Real enhanced acquisition path omitted extra action");
        scratch=Call(0x28A370,6,1,0);
        Check(W(CheckedNativeRead.Read(scratch+8,2))==0,"Reaction-only tab received action 513");
        menu.Write(0xCC,BitConverter.GetBytes((ushort)60));
        Check(Call(0x28A6E8,0,75,0,(long)output.Address,2)==15 && W(output.Read(34,2))==0x4201,
            "Enhanced action ignored JP affordability");
        menu.Write(0x7E,[0x90,0,0]); // original Potion + Ether bits, not packed new positions
        menu.Write(0xCC,BitConverter.GetBytes((ushort)300));
        Check(Call(0x28A6E8,0,75,0,(long)output.Address,2)==15 && W(output.Read(2,2))==0x1170 &&
            W(output.Read(10,2))==0x1173 && W(output.Read(18,2))==380,"Enhanced retained learning bits remapped incorrectly");
        menu.Write(0x7E,[0,0,0]);
        // Execute the real UpdateUnitData serialization body from here. Scene
        // lookup and final stat reconstruction are explicit fixture helpers.
        // Install them only after the original sorting-body regressions above,
        // which share the campaign-context helper.
        foreach(int rva in new[]{0x2BB31C,0x275C70,0x284A08})
            NativeLifetimeMemory.WriteProtected((long)native.Address+rva,[0x31,0xC0,0xC3]);
        // Security-cookie checks preserve RAX; a zero-return stub would hide
        // native getskillrange/getskilleffect's rejection/result codes.
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x41FAE0,[0xC3]);
        // Microsoft x64 memset fixture: preserve RDI, fill RCX with DL for
        // R8 bytes and return the original destination.
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x5CA430,
            [0x57,0x48,0x8B,0xF9,0x4C,0x8B,0xC9,0x49,0x8B,0xC8,0x88,0xD0,0xF3,0xAA,0x49,0x8B,0xC1,0x5F,0xC3]);
        // Non-overlapping memcpy fixture for copy_struct_data's 20-byte
        // stack snapshots. Target/range/effect builders below remain real.
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x5C9D80,
            [0x57,0x56,0x48,0x8B,0xF9,0x48,0x8B,0xF2,0x4C,0x8B,0xC9,0x4C,0x89,0xC1,0xF3,0xA4,0x4C,0x89,0xC8,0x5E,0x5F,0xC3]);
        // Reproduce the live failure: a normal save-queue packet may concern
        // another position after our party snapshot has been frozen.
        byte[] liveWork=new byte[NativePartySaveCommit.WorkSize];record.CopyTo(liveWork,NativePartySaveCommit.RecordsOffset+1200);
        Console.WriteLine("Fixture: unrelated save packet");
        native.Write(0x2E80450,liveWork);Call(0x2CEEE8);
        byte[] otherPacket=new byte[16+liveWork.Length];otherPacket[16]=1;
        BinaryPrimitives.WriteUInt32LittleEndian(otherPacket,16);
        BinaryPrimitives.WriteUInt32LittleEndian(otherPacket.AsSpan(4),NativeSavePacket.Crc32(otherPacket.AsSpan(16)));
        BinaryPrimitives.WriteUInt32LittleEndian(otherPacket.AsSpan(8),0x11);
        using var otherWire=new OwnedNativeMemory(65536);otherWire.Write(0,otherPacket);otherWire.Write(50000,"other-position\0"u8.ToArray());
        Call(0x32B94,0,0x20000,(long)otherWire.Address+50000,(long)otherWire.Address,otherPacket.Length);
        Check(session.CanMutate && session.Frozen() is not null,"Unrelated native save packet disabled acquisition/battle");
        Check(!Directory.Exists(Path.Combine(temp,"Sidecars")),"Unrelated packet published our party state");
        // Use the exact enhanced confirmation pair (job word + masked ability),
        // not guessed action indices. 150 JP buys Potion + Venom, leaving 30.
        menu.Write(0xCC,BitConverter.GetBytes((ushort)150));
        void Confirm(int row)
        {
            Check(Call(0x28A6E8,0,75,0,(long)output.Address,4)==15,"Enhanced confirmation list missing");
            Call(0x2B9AD0,W(output.Read(row*8+2,2))&1023,W(output.Read(row*8,2)));
        }
        Confirm(0);Check(W(menu.Read(0xCC,2))==100 && menu.Read(0x7E,1)[0]==0x80,"Potion did not charge exactly 50 JP");
        Console.WriteLine("Fixture: learning and battle transport");
        Confirm(4);Check(W(menu.Read(0xCC,2))==30 && session.Learned(2),"Venom did not charge exactly 70 JP");
        byte[] paidLearning=menu.Read(0x7E,3);
        Confirm(1);Confirm(2);Confirm(3);Confirm(0);Confirm(4);
        Check(W(menu.Read(0xCC,2))==30 && menu.Read(0x7E,3).SequenceEqual(paidLearning),"Insufficient/repeated purchase changed JP or learning");
        Check(W(native.Read(0x11A7D10+2*600+0x82,2))==30 && native.Read(0x11A7D10+2*600+0x35,3).SequenceEqual(paidLearning),
            "Real native UpdateUnitData lost paid JP/retained learning in serialized unit");
        Check(Call(0x28A6E8,0,75,0,(long)output.Address,5)==2 && W(output.Read(2,2))==0x1170 && W(output.Read(10,2))==0x1201,
            "Enhanced learned list lost paid actions");
        native.Write(0x11A7C00+240,[2]);Call(0x2847F8,261,3);
        native.Write(0x1853CE0+1,[0]);native.Write(0x1853CE0+2,[2]);native.Write(0x1853CE0+0xA5,native.Read(0x11A7D10+2*600+0x35,3));
        using var paidParallel=new OwnedNativeMemory(4096);
        Check(Call(0x30E368,0,6,(long)output.Address,(long)paidParallel.Address,(long)paidParallel.Address+64,0,(long)paidParallel.Address+128,(long)paidParallel.Address+192)==2 &&
            W(output.Read(0,2))==368 && W(output.Read(2,2))==513,"Paid learning not available to battle worker");
        Check(Call(0x3170CC,0,6,0)==1 && Call(0x3170CC,0,6,4)==1 && Call(0x3170CC,0,6,1)==0,"Battle learned eligibility differs from acquisition");
        // Reset only owned fixture state for the independent 300-JP tests below.
        session.RestoreLearning(2,false);menu.Write(0x7E,[0,0,0]);menu.Write(0xCC,BitConverter.GetBytes((ushort)300));
        native.Write(0x1853CE0+0xA5,[0,0,0]);native.Write(0x11A7C00+240,[0]);Call(0x2847F8,261,-3);
        long visualAddress=(long)typeof(ChemistPlayableHost).GetField("_effectCallAddress",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
        Console.WriteLine("Fixture: effect routing");
        var effect=Marshal.GetDelegateForFunctionPointer<Eight>((nint)visualAddress);
        Check(effect(0,513)==28&&effect(0,28)==28&&effect(0,184)==164,"Visual caller bridge lost new mapping or the existing Dark Knight detour");
        Console.WriteLine("Fixture: effect bridge checked");
        Check(Call(0x319B50,0,184)==164&&Call(0x319B50,0,513)==513,"Existing global effect entry was replaced");
        // Real native classifier: a negative legacy out-of-bounds entry makes
        // a new action skip StartEffect entirely. The leaf must intercept only
        // our eleven IDs without changing the original argument/fallback.
        var classifierHook=hostHooks[35];
        var originalClassifier=Marshal.GetDelegateForFunctionPointer<Eight>((nint)classifierHook.GetType().GetProperty("OriginalFunctionAddress")!.GetValue(classifierHook)!);
        byte[] legacyOverflow=native.Read(0x683500+513*2,22);
        native.Write(0x683500+513*2,Enumerable.Repeat((byte)255,22).ToArray());
        foreach(int s in ChemistActionBindings.ExtraSlots)
        {
            int id=ChemistActionBindings.TestActions[s].AbilityId;
            Check(originalClassifier(id)==1 && Call(0x319D74,id)==0,"Legacy RSM classification still suppresses registered impact: "+s);
        }
        native.Write(0x683500+513*2,legacyOverflow);
        foreach(int id in new[]{0,28,146,368,393,406,512,524})
            Check(Call(0x319D74,id)==originalClassifier(id),"Non-mod effect classification changed: "+id);
        // Now execute the REAL StartEffect body through the pre-existing detour,
        // rather than proving only the template-return fixture above. Preserve
        // the Dark Knight remap and tail-jump to its original native function.
        byte[] legacyFixture=existingEffect.Read(0,32);
        byte[] nativeEffectTail=[0x81,0xFA,0xB8,0,0,0,0x75,5,0xBA,0xA4,0,0,0,0xFF,0x25,0,0,0,0];
        nativeEffectTail=nativeEffectTail.Concat(BitConverter.GetBytes(legacyEffect.OriginalFunctionAddress)).ToArray();
        NativeLifetimeMemory.WriteProtected((long)existingEffect.Address,nativeEffectTail);
        using var impactPayload=new OwnedNativeMemory(4096);
        Console.WriteLine("Fixture: real impact controller arming");
        impactPayload.Write(0,[1,0,0,0,0,0,0,0]);
        foreach(int s in ChemistActionBindings.ExtraSlots)
        {
            native.Write(0xD405AA,[0xA5]);
            effect(1,ChemistActionBindings.TestActions[s].AbilityId,(long)impactPayload.Address);
            int template=ChemistExtraEffects.ImpactTemplate(s);
            Check(native.Read(0xD405AA,1)[0]==0,"Extra item started a persistent casting effect: "+s);
            Check(W(native.Read(0x1D330BC,2))==(W(native.Read(0x683500+template*2,2))&511),"Real impact script selection differs: "+s);
            Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x2E801F4,4))==template,"Impact controller identity differs");
            Check(BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x18732C8,4))==1,"Impact did not arm the native collision/start handshake");
            if(s==5)Check(W(native.Read(0x1D330BC,2))==49 && ChemistExtraEffects.Status(s)==110,
                "Oil lost its actual Oil status or still selects monster script 350");
        }
        Check(ChemistExtraEffects.Template(5)==299 && ChemistExtraEffects.ImpactTemplate(5)==234 && W(native.Read(0x1D330BC,2))!=350,
            "Oil gameplay status and unsafe monster visual were not separated");
        NativeLifetimeMemory.WriteProtected((long)existingEffect.Address,legacyFixture);
        // Run original throw dispatcher, item initialization and UV/CLUT
        // calculation. Only scene animation switching is an owned fixture.
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x268E04,[0xFF,0xC1,0x66,0x41,0x89,0x48,0x10,0x31,0xC0,0xC3]);
        using var actorAnimation=new OwnedNativeMemory(4096);
        Console.WriteLine("Fixture: original throw and world bottle");
        using var targetAnimation=new OwnedNativeMemory(4096);
        using var bottleSprite=new OwnedNativeMemory(4096);
        Check(native.Read(0x6735A8+ChemistExtraEffects.NoCastingVisualSelector*2,2).SequenceEqual(new byte[]{0,0}),
            "No-cast selector is not a no-op in BOTH native casting stages");
        actorAnimation.Write(0x368,BitConverter.GetBytes((long)bottleSprite.Address));
        // Use the actual NewAnimation header (26F31C), not an all-zero
        // sprite. Its page is already 0x1E; page-only fixes cannot reproduce
        // or solve the reported Enhanced barcode rendering failure.
        bottleSprite.Write(4,Convert.FromHexString("1E008778"));
        actorAnimation.Write(0x88,[4,4]);targetAnimation.Write(0x88,[5,4]);
        foreach(int s in ChemistActionBindings.ExtraSlots)
        {
            actorAnimation.Write(0x142,BitConverter.GetBytes(ChemistActionBindings.TestActions[s].AbilityId));
            actorAnimation.Write(0x150,BitConverter.GetBytes((ushort)ChemistActionBindings.Item(s)));
            actorAnimation.Write(0x10,BitConverter.GetBytes((ushort)0x39));
            Call(0x269920,(long)actorAnimation.Address);
            Check(W(actorAnimation.Read(0x10,2))==0x39,"Extra item still requests a native casting pose: "+s);
            Call(0x269988,(long)actorAnimation.Address,(long)targetAnimation.Address);
            Check(W(actorAnimation.Read(0x10,2))==0x4D,"Adjacent extra flask omitted native trajectory: "+s);
            Check(bottleSprite.Read(0x10,2).SequenceEqual(new byte[]{15,16}),"World bottle dimensions differ");
            uint coords=NativeConsumableVisuals.BottleCoordinates(s-4);
            Check(bottleSprite.Read(0x12,2).SequenceEqual(BitConverter.GetBytes(coords).AsSpan(0,2).ToArray()) && W(bottleSprite.Read(6,2))==(coords>>16),
                "World bottle omitted its registered glyph/private palette: "+s);
            Call(0x2E1604,(long)output.Address,ChemistActionBindings.Item(s));
            Check(output.Read(0x14,4).SequenceEqual(BitConverter.GetBytes(coords)),"Alternate world atlas path omitted registered glyph: "+s);
            Check(W(actorAnimation.Read(0x150,2))==ChemistActionBindings.Item(s),"Visual bottle aliased consumable identity");
            targetAnimation.Write(0x88,[7,4]);Call(0x269988,(long)actorAnimation.Address,(long)targetAnimation.Address);
            Check(W(actorAnimation.Read(0x10,2))==0x4D,"Distant flask omitted native trajectory");
            targetAnimation.Write(0x88,[4,4]);Call(0x269988,(long)actorAnimation.Address,(long)targetAnimation.Address);
            Check(W(actorAnimation.Read(0x10,2))==0x3A,"Self-use created zero-length trajectory");
            targetAnimation.Write(0x88,[5,4]);
        }
        actorAnimation.Write(0x142,BitConverter.GetBytes((ushort)368));actorAnimation.Write(0x150,BitConverter.GetBytes((ushort)240));
        Call(0x269988,(long)actorAnimation.Address,(long)targetAnimation.Address);
        Check(W(actorAnimation.Read(0x10,2))==0x3A,"Ordinary adjacent Potion animation changed");
        foreach(ushort ability in ChemistMedicineFamilies.All.Where(a=>a!=368))
        {
            actorAnimation.Write(0x142,BitConverter.GetBytes(ability));
            actorAnimation.Write(0x150,BitConverter.GetBytes(ChemistMedicineFamilies.Item(ability)));
            foreach(byte distance in new byte[]{0,1,2,4})
            {
                targetAnimation.Write(0x88,[(byte)(4+distance),4]);
                Call(0x269988,(long)actorAnimation.Address,(long)targetAnimation.Address);
                Check(W(actorAnimation.Read(0x10,2))==(distance==0?0x3A:0x4D),"Potion variant native throw/self dispatch differs: "+ability);
                Check(W(actorAnimation.Read(0x150,2))==ChemistMedicineFamilies.Item(ability),"Medicine variant bottle lost its item identity");
            }
        }
        Console.WriteLine("Fixture: actual Hi-Potion/X-Potion/Elixir throw dispatcher plus Ether/Hi-Ether/seven Remedy variants, item initialization and adjacent/distant flight pose, with safe self-use; ordinary Potion unchanged. Scene animation switching is an explicit fixture.");
        using var parallelBottle=new OwnedNativeMemory(4096);
        foreach(int id in new[]{0,1,240,241,242,243,244,260,272,512,773})
        {
            Call(0x22A9E4,(long)output.Address,id);Original(36,(long)parallelBottle.Address,id);
            Check(output.Read(0x14,8).SequenceEqual(parallelBottle.Read(0x14,8)),"Original bottle changed or high item ID aliased: "+id);
            Call(0x2E1604,(long)output.Address,id);Original(37,(long)parallelBottle.Address,id);
            Check(output.Read(0x14,8).SequenceEqual(parallelBottle.Read(0x14,8)),"Alternate original bottle changed: "+id);
        }
        // Only the outer text setter is an explicit fixture; the scoped
        // native animation traversal/remapping executes unchanged emitted code.
        long captionOriginal=(nint)hostHooks[38].GetType().GetProperty("OriginalFunctionAddress")!.GetValue(hostHooks[38])!;
        NativeLifetimeMemory.WriteProtected(captionOriginal,Convert.FromHexString("488951004C8941084C8949104889D0C3"));
        using var captionActor=new OwnedNativeMemory(4096);using var captionWorker=new OwnedNativeMemory(4096);
        native.Write(0xD3A410,BitConverter.GetBytes((long)captionActor.Address));native.Write(0x7DCF9A,BitConverter.GetBytes((ushort)2));
        captionActor.Write(0x148,BitConverter.GetBytes((long)captionWorker.Address));captionWorker.Write(0x1BC,[2]);captionActor.Write(0x179,[6]);
        foreach(int s in ChemistActionBindings.ExtraSlots)
        {
            int id=ChemistActionBindings.Item(s),ability=ChemistActionBindings.TestActions[s].AbilityId;
            captionActor.Write(0x142,BitConverter.GetBytes((ushort)ability));captionActor.Write(0x150,BitConverter.GetBytes((ushort)id));
            captionActor.Write(0x17A,BitConverter.GetBytes((ushort)id));byte[] originalPacket=captionActor.Read(0x178,20);
            Check(Call(0x0E512C,(long)output.Address,0x7000+id,0x112233,0x445566)==0x7000+ability,"Target/impact balloon kept a monster name: "+s);
            Check(BinaryPrimitives.ReadInt64LittleEndian(output.Read(8,8))==0x112233&&BinaryPrimitives.ReadInt64LittleEndian(output.Read(16,8))==0x445566,
                "Caption remapper changed original UI arguments");
            Check(captionActor.Read(0x178,20).SequenceEqual(originalPacket),"Caption fix mutated native reaction data");
            Check(Call(0x0E512C,(long)output.Address,0x7000+ability)==0x7000+ability,"Correct ability name remapped twice");
            Check(Call(0x0E512C,(long)output.Address,0x3800+id)==0x3800+id,"Proper item name remapped");
            captionActor.Write(0x142,BitConverter.GetBytes((ushort)id));
            Check(Call(0x0E512C,(long)output.Address,0x7000+id)==0x7000+id,"Actual monster action was renamed");
            captionActor.Write(0x142,BitConverter.GetBytes((ushort)ability));captionActor.Write(0x179,[7]);
            Check(Call(0x0E512C,(long)output.Address,0x7000+id)==0x7000+id,"Non-Chemist command remapped");captionActor.Write(0x179,[6]);
            captionActor.Write(0x150,BitConverter.GetBytes((ushort)(id+1)));
            Check(Call(0x0E512C,(long)output.Address,0x7000+id)==0x7000+id,"Mismatched consumable remapped");
        }
        native.Write(0xD3A410,BitConverter.GetBytes(0L));
        foreach(int name in new[]{-1,0x7104,0x7110,0x7109,0x7201,0xB006})
            Check((int)Call(0x0E512C,(long)output.Address,name)==name,"Unowned/missing animation caption changed");
        // A malformed native list must not turn the caption fix into a hang.
        captionActor.Write(0,BitConverter.GetBytes((long)captionActor.Address));captionWorker.Write(0x1BC,[3]);
        native.Write(0xD3A410,BitConverter.GetBytes((long)captionActor.Address));
        Check(Call(0x0E512C,(long)output.Address,0x7109)==0x7109,"Bounded animation traversal did not stop");
        native.Write(0xD3A410,BitConverter.GetBytes(0L));
        Console.WriteLine("Fixture: all eleven custom world bottles/private CLUTs and scoped balloon labels verified; ordinary bottles and monster names unchanged");
        // Execute real FillUnitStatusWindow from ENTRY, preserving its real
        // RBP displayed animation and stack arg5. Scene animation lookup,
        // portrait, text copy and NEX row getter are explicit outer fixtures.
        // Exit after the label lookup through the original restoring epilogue.
        // Calling 2151E5 alone incorrectly inherits the CLR caller's RBP.
        long mapNameOriginal=(nint)hostHooks[39].GetType().GetProperty("OriginalFunctionAddress")!.GetValue(hostHooks[39])!;
        NativeLifetimeMemory.WriteProtected(mapNameOriginal,Convert.FromHexString("89C8C3"));
        int[] sceneSites=[0x260ABC,0x2607C0,0x22AA88,0x261CC,0x2151EA,0x215263,0x30FE60];
        var sceneBefore=sceneSites.ToDictionary(r=>r,r=>native.Read(r,14));
        using var displayedAnimation=new OwnedNativeMemory(4096);
        using var previewUi=new OwnedNativeMemory(16384);using var previewCursor=new OwnedNativeMemory(4096);
        using var previewMenu=new OwnedNativeMemory(4096);
        int[] previewGlobals=[0x3CD9DC0,0xD40950,0x184A9B0,0x7DCF9A,0xC6B1CC];
        var previewBefore=previewGlobals.ToDictionary(r=>r,r=>native.Read(r,8));
        byte[] previewPacketBefore=native.Read(0x2FD35A4,20);
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x260ABC,
            new byte[]{0x48,0xB8}.Concat(BitConverter.GetBytes((long)displayedAnimation.Address)).Concat(new byte[]{0xC3}).ToArray());
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x2607C0,
            new byte[]{0x48,0xB8}.Concat(BitConverter.GetBytes((long)displayedAnimation.Address)).Concat(new byte[]{0xC3}).ToArray());
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x22AA88,[0x31,0xC0,0xC3]);
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x261CC,[0xC3]);
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x30FE60,[0x31,0xC0,0xC3]);
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x2151EA,
            new byte[]{0xE9}.Concat(BitConverter.GetBytes(0x215033-0x2151EA-5)).ToArray());
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x215263,
            new byte[]{0xE9}.Concat(BitConverter.GetBytes(0x215033-0x215263-5)).ToArray());
        foreach(int s in ChemistActionBindings.ExtraSlots)
        {
            int ability=ChemistActionBindings.TestActions[s].AbilityId,item=ChemistActionBindings.Item(s),legacy=ability-256;
            displayedAnimation.Write(0x142,BitConverter.GetBytes((ushort)ability));
            displayedAnimation.Write(0x179,[6]);
            // Real Selection preserves +2. More importantly, Tmp_UA can
            // already describe another action when the display is refreshed.
            byte[] originalSelected=new byte[20];originalSelected[2]=0x34;originalSelected[3]=0x12;
            byte[] selected=ChemistBattleData.Selection(originalSelected,s,0,new byte[5],new byte[4]);
            Check(W(selected.AsSpan(2,2).ToArray())==0x1234,"Selection fixture incorrectly filled ability field");
            native.Write(0x18716A0,selected);
            long Name(int value)=>Call(0x214FB4,0,-1,0,0xC0,value-256);
            Check(Name(legacy)==ability,"Actual small map balloon still requests legacy row: "+legacy);
            var diagnostic=(NativeLifetimeMemory)typeof(ChemistPlayableHost).GetField("_mapNameDiagnostic",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            Check(BinaryPrimitives.ReadInt32LittleEndian(CheckedNativeRead.Read(diagnostic.Address+4,4))==ability,
                "Native displayed-unit name diagnostics omitted the resolved row");
            Check(Call(0x0F8474,legacy)==legacy,"Unrelated native/monster ability lookup renamed");
            Check(native.Read(0x18716A0,20).SequenceEqual(selected),"Map name correction changed the action packet");
            Check(Name(legacy+1)==legacy+1,"Unrelated bubble ID remapped");
            selected[0]=7;native.Write(0x18716A0,selected);
            Check(Name(legacy)==ability,"Reused non-item Tmp_UA broke displayed animation label");
            selected[0]=6;selected[6]++;native.Write(0x18716A0,selected);
            Check(Name(legacy)==ability,"Different selected item broke displayed animation label");
            native.Write(0x18716A0,new byte[20]);
            Check(Name(legacy)==ability,"Cleared Tmp_UA broke displayed animation label");
            displayedAnimation.Write(0x179,[7]);
            Check(Name(legacy)==legacy,"Non-item animation renamed");
            displayedAnimation.Write(0x179,[6]);displayedAnimation.Write(0x142,BitConverter.GetBytes((ushort)legacy));
            Check(Name(legacy)==legacy,"Original Cloud/monster animation renamed");
            // Real 2333E8 reads menu row 7000+ability and serializes a full
            // ushort before set_event_table_skill's lossy timeline byte.
            // The existing outer queue helper is stubbed, not the producer.
            previewUi.Write(0x346C,BitConverter.GetBytes(1));
            previewMenu.Write(0,BitConverter.GetBytes((ushort)(0x7000+ability)));
            native.Write(0x3CD9DC0,BitConverter.GetBytes((long)previewUi.Address));
            native.Write(0xD40950,BitConverter.GetBytes((long)previewCursor.Address));
            native.Write(0x184A9B0,BitConverter.GetBytes((long)previewMenu.Address));
            native.Write(0x7DCF9A,BitConverter.GetBytes((ushort)0));
            native.Write(0xC6B1CC,BitConverter.GetBytes(0x17));
            byte[] previewPacket=new byte[20];previewPacket[1]=6;native.Write(0x2FD35A4,previewPacket);
            Call(0x2333E8,(long)output.Address,(long)output.Address+128);
            Check(W(native.Read(0x2FD35A6,2))==ability,"Original full-width preview producer lost ability");
            byte[] frozenPreview=native.Read(0x2FD35A4,20);
            Check(Name(legacy)==ability,"Preview before animation update kept wrong Cloud/monster label");
            Check(BinaryPrimitives.ReadInt32LittleEndian(CheckedNativeRead.Read(diagnostic.Address+24,4))==2,"Preview identity was not used");
            Check(native.Read(0x2FD35A4,20).SequenceEqual(frozenPreview),"Name correction mutated preview packet");
            native.Write(0xC6B1CC,BitConverter.GetBytes(0x19));Check(Name(legacy)==ability,"Confirmation preview name lost");
            native.Write(0x2FD35A4,[1]);Check(Name(legacy)==legacy,"Different worker inherited preview name");
            native.Write(0x2FD35A4,[0,7]);Check(Name(legacy)==legacy,"Non-item preview renamed");
            native.Write(0x2FD35A4,[0,6]);native.Write(0xC6B1CC,BitConverter.GetBytes(0x27));
            Check(Name(legacy)==legacy,"Stale preview outside target modes renamed ordinary action");
            // Second native caption branch, CALL21525E, before text copy.
            // No timeline flags: name comes from animation+17A, not arg5.
            native.Write(0xC6B1CC,BitConverter.GetBytes(0x19));
            displayedAnimation.Write(0x17A,BitConverter.GetBytes((ushort)legacy));
            long ExecutionName()=>Call(0x214FB4,0,-1,0,0,0);
            Check(ExecutionName()==ability,"Second native caption call retained old preview name");
            native.Write(0x2FD35A4,new byte[20]);
            displayedAnimation.Write(0x142,BitConverter.GetBytes((ushort)ability));
            Check(ExecutionName()==ability,"Second native caption lost full animation identity");
            displayedAnimation.Write(0x179,[7]);Check(ExecutionName()==legacy,"Second non-item caption renamed");
            displayedAnimation.Write(0x179,[6]);displayedAnimation.Write(0x142,BitConverter.GetBytes((ushort)legacy));
            Check(ExecutionName()==legacy,"Second original Cloud/monster caption renamed");
            displayedAnimation.Write(0x17A,[0,0]);
            foreach(int r in previewGlobals)native.Write(r,previewBefore[r]);native.Write(0x2FD35A4,previewPacketBefore);
        }
        foreach(int r in sceneSites)NativeLifetimeMemory.WriteProtected((long)native.Address+r,sceneBefore[r]);
        native.Write(0x18716A0,new byte[20]);
        Console.WriteLine("Fixture: real small-balloon lookup 257..267 corrected to 513..523, scoped callsite and ordinary names preserved");
        Console.WriteLine("Fixture: actual Selection packet preserves unrelated +2; displayed animation resolves names even with reused or cleared Tmp_UA.");
        Console.WriteLine("Fixture: real FillUnitStatusWindow entry, native RBP/stack and original epilogue resolve all eleven map names from the displayed unit; mismatched, non-item and original Cloud/monster actions unchanged. Portrait/text/NEX scene helpers explicit.");
        Console.WriteLine("Fixture: real 2333E8 full-width preview producer before timeline byte resolves all eleven names in target/confirmation modes before animation update; other workers, commands and stale modes unchanged. Native preview packet never modified by lookup.");
        Console.WriteLine("Fixture: both original FillUnitStatusWindow balloon branches CALL2151E5/CALL21525E resolve all eleven full preview/animation names; ordinary action names and non-item commands preserved.");
        CheckEnhancedBottles(native,existingDecoder,mod);
        Check((long)native.Address+0x206317+5+BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x206318,4))==visualAddress,"Audited visual callsite did not target the owned bridge");
        Check(Call(0x2867BC,0,75,0,(long)output.Address,2)==15,"New learning list missing");
        Console.WriteLine("Fixture: learning regression");
        Check(W(output.Read(8,2))==513,"Venom identity lost");
        Call(0x2B9AD0,513,75);Check(W(menu.Read(0xCC,2))==230&&session.Learned(2),"JP/serialized-unit learning integration failed");
        Check(Call(0x28A6E8,0,75,0,(long)output.Address,2)==15&&W(output.Read(34,2))==0x1201,
            "Enhanced acquired Venom not marked learned");
        Check(Call(0x28A6E8,0,75,0,0,3)==14,"Enhanced mastery count omitted new learning state");
        Check(Call(0x28A6E8,0,75,0,(long)output.Address,5)==1 && W(output.Read(2,2))==0x1201,
            "Enhanced learned-only list ignored sidecar state");
        Check(Call(0x28A6E8,0,75,0,(long)output.Address,6)==14 && W(output.Read(114,2))==65535,
            "Enhanced unlearned-only list included acquired Venom");
        Call(0x2B9AD0,513,75);Check(W(menu.Read(0xCC,2))==230,"Repeated confirmation charged twice");
        Check(Call(0x2867BC,0,75,0,(long)output.Address,2)==15&&W(output.Read(8,2))==0x1201,"Learned flag lost");
        output.Write(0,[255,255]);Check(Call(0x2866E4,0,(long)output.Address)==1&&W(output.Read(0,2))==6,"Venom-only secondary command unavailable");
        Check(Call(0x2847F8,261,3)==3&&session.Stock(4)==3,"Expanded purchase stock identity failed");
        long shared=((NativeLifetimeMemory)typeof(ChemistPlayableHost).GetField("_shared",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Address;
        NativeLifetimeMemory.WriteProtected(shared,[255,255]);
        Check(Call(0x288B94,0,0,7,shared,0,0)==1&&W(CheckedNativeRead.Read(shared,2))==261,"Inventory omitted purchased extra item");
        native.Write(0x1853CE0+1,[0]);native.Write(0x1853CE0+2,[2]);
        Check(Call(0x3170CC,0,6,4)==1&&Call(0x3170CC,0,6,5)==0,"Battle learning used old alias bits");
        using var parallel=new OwnedNativeMemory(4096);
        Check(Call(0x30E368,0,6,(long)output.Address,(long)parallel.Address,(long)parallel.Address+64,0,(long)parallel.Address+128,(long)parallel.Address+192)==1,"Battle list filtering failed");
        Check(W(output.Read(0,2))==513&&parallel.Read(192,1)[0]==7,"Full-width battle/event transport failed");
        using var reaction=new OwnedNativeMemory(4096);byte[] packet=new byte[20];packet[1]=6;BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),513);reaction.Write(0,packet);
        // Reproduce the actual human-selection rejection, not SetTmp_UA's
        // unrelated AI packet. Both native wrappers reject 513 at 368.
        int originalRange=(int)Original(28,(long)reaction.Address),originalEffect=(int)Original(29,(long)reaction.Address);
        Console.WriteLine("Fixture: targeting regression");
        // Scene collision is isolated for these existing map-only fixtures.
        // The real receiver engine has its own complete native fixture below.
        ReceiverBefore=native.Read(0x312ED0,14);
        NativeLifetimeMemory.WriteProtected((long)native.Address+0x312ED0,[0x44,0x89,0xC0,0xC3]);
        Check(originalRange==-1 && originalEffect==-1,
            $"Original human range/effect 368-bound failure was not reproduced: {originalRange}/{originalEffect}");
        native.Write(0xC6AD6A,[16,16]);native.Write(0x1853CE0+0x4F,[8,8,0]);
        byte[] terrain=new byte[512*8];
        for(int tile=0;tile<512;tile++){terrain[tile*8]=63;terrain[tile*8+6]=(byte)(tile<256?0:1);}
        terrain[(8*16+8)*8]=0;terrain[(8*16+12)*8]=1;terrain[(8*16+13)*8]=2;
        native.Write(0xD8DCB0,terrain);
        int MapFlag(int x,int y,int flag)=>native.Read(0xD8DCB0+(y*16+x)*8+5,1)[0]&flag;
        foreach(var action in ChemistActionBindings.TestActions)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),action.AbilityId);reaction.Write(0,packet);
            Check(Call(0x27FE20,(long)reaction.Address)==0,"Registered Chemist action did not open human target selection");
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)
                Check((native.Read(0x18004E0+(y*16+x)*5,1)[0]!=0)==(Math.Abs(x-8)+Math.Abs(y-8)<=4),"Real native range mask is not Manhattan range4");
            Check(MapFlag(12,8,0x40)!=0 && MapFlag(13,8,0x40)==0,"Native occupied-target marking ignored range boundary");
            Check(W(reaction.Read(2,2))==action.AbilityId,"Target selection remapped full-width ability");
        }
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),513);
        native.Write(0x1853CE0+512+1,[1]);native.Write(0x1853CE0+512+0x4F,[12,8,0]);
        packet[10]=5;BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12),12);BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16),8);
        reaction.Write(0,packet);
        Check(Call(0x27FFE8,(long)reaction.Address)==0 && MapFlag(12,8,0x80)!=0,"Real native effect map omitted selected tile");
        Check(Enumerable.Range(0,256).Count(i=>(native.Read(0xD8DCB0+i*8+5,1)[0]&0x80)!=0)==1,
            "Native radius0 was not single-target");
        native.Write(0xD8DCB0+(8*16+12)*8+6,[1]);
        Check((int)Call(0x27FFE8,(long)reaction.Address)==-1,"Invalid native target tile was forced valid");
        native.Write(0xD8DCB0+(8*16+12)*8+6,[0]);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),524);reaction.Write(0,packet);
        Check((int)Call(0x27FE20,(long)reaction.Address)==-1 && (int)Call(0x27FFE8,(long)reaction.Address)==-1,
            "Human target integration widened to an unregistered ability");
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),513);reaction.Write(0,packet);
        Check(session.Stock(4)==3,"Native target-map generation consumed stock");
        native.Write(0x1853CE0+512+1,[1]);native.Write(0x1853CE0+512+0x4F,[12,8,0]);
        native.Write(0x186AFF4,[1,0,0,0]);native.Write(0x186AF80,[2,0,0,0]);
        Console.WriteLine("Fixture: real reaction prediction");
        Check(Call(0x283280,(long)native.Address+0x1853CE0,6,513,1)==0 &&
            W(native.Read(0x1853CE0+0x1A2,2))==513 && W(native.Read(0x1853CE0+0x1A8,2))==261,
            "Real reaction-caster range -> target -> commitment chain rejected registered action");
        Check(session.Stock(4)==3,"Real native prediction chain consumed stock");
        native.Write(0x1853CE0+512+0x4F,[13,8,0]);
        Check((int)Call(0x283280,(long)native.Address+0x1853CE0,6,513,1)==-2,
            "Real native reaction chain accepted an out-of-range target");
        native.Write(0x1853CE0+512+0x4F,[12,8,0]);native.Write(0x186AF80,[0,0,0,0]);
        Check(Call(0x281488,(long)reaction.Address,(long)output.Address,0)==1&&session.Stock(4)==3,"Preview consumed stock");
        Check(W(output.Read(2,2))==513&&W(output.Read(8,2))==261,"Commitment aliased item/ability ID");
        native.Write(0x186AF80,[2,0,0,0]);Call(0x281488,(long)reaction.Address,(long)output.Address,1);Check(session.Stock(4)==3,"Prediction consumed stock");
        native.Write(0x186AF80,[0,0,0,0]);Call(0x281488,(long)reaction.Address,(long)output.Address,1);Check(session.Stock(4)==2,"Commitment did not consume exactly once");
        byte[] request=[1,2,6,0];reaction.Write(0,request);Call(0x320504,(long)reaction.Address);
        MedicineCascadeTests(native);
        MedicinePositionTests(native);
        FourthMenuTests(native);
        PotionSubmenuTests(native,host);
        PotionHealingTests(native);
        if(args.Contains("--potion-smoke"))
        {
            CheckConsumableCompletion(native,host,logs);
            Console.WriteLine("Focused Potion checks only; not a complete release gate.");return;
        }
        Console.WriteLine("Fixture: save queue regressions");
        Check(W(native.Read(0x18716A2,2))==513&&W(native.Read(0x18716A6,2))==261&&native.Read(0x18716A4,2).SequenceEqual(new byte[]{4,0}),"Real native AI selection lost ability/range/extra item");
        Check(native.Read(0x18716AD,4).SequenceEqual(new byte[]{0x12,0x55,0x42,0}),"Medicine targeting/request flags confused with common learnability byte");
        reaction.Write(0,[1,6,6,0]);Call(0x320504,(long)reaction.Address);
        Check(W(native.Read(0x18716A2,2))==513 && W(native.Read(0x18716A6,2))==261,
            "Real AI selection confused worker1's packed ID with ability513");
        // Exact native packet async-save wire callbacks, then actual party
        // restore hook. All packets/units are synthetic and stored under TEMP.
        record=native.Read(0x11A7D10+2*600,600);
        byte[] serialized=new byte[NativePartySaveCommit.WorkSize];record.CopyTo(serialized,NativePartySaveCommit.RecordsOffset+1200);
        native.Write(0x2E80450,serialized);Call(0x2CEEE8);
        byte[] savePacket=new byte[16+0x154+serialized.Length];serialized.CopyTo(savePacket,0x164);
        BinaryPrimitives.WriteUInt32LittleEndian(savePacket,16);BinaryPrimitives.WriteUInt32LittleEndian(savePacket.AsSpan(4),NativeSavePacket.Crc32(savePacket.AsSpan(16)));BinaryPrimitives.WriteUInt32LittleEndian(savePacket.AsSpan(8),0x11);
        using var wire=new OwnedNativeMemory(65536);wire.Write(0,savePacket);wire.Write(50000,"fixture-slot\0"u8.ToArray());
        Call(0x32B94,0,0x10000,(long)wire.Address+50000,(long)wire.Address,savePacket.Length);
        byte[] entry=new byte[NativeSaveEvents.QueueEntrySize];BinaryPrimitives.WriteInt64LittleEndian(entry,savePacket.Length);BinaryPrimitives.WriteInt64LittleEndian(entry.AsSpan(8),(long)wire.Address);BinaryPrimitives.WriteInt64LittleEndian(entry.AsSpan(0x18),0x10000);"fixture-slot\0"u8.CopyTo(entry.AsSpan(0x20));wire.Write(51000,entry);
        Call(0x2847F8,261,5);Call(0x32E60,0,0,(long)wire.Address+51000);
        Check(Directory.GetFiles(Path.Combine(temp,"Sidecars"),"*.json",SearchOption.AllDirectories).Length==1,"Successful native save did not publish sidecar");
        Call(0x33DF0,0,(long)wire.Address+50000,0,0,(long)wire.Address,savePacket.Length);
        Check(session.Stock(4)==7,"Thumbnail read changed live stock");
        Call(0x284500,2);Check(session.Stock(4)==0&&!session.Learned(2),"Party initialization did not clear previous live state");
        Call(0x2CF768);
        Check(session.Stock(4)==2&&session.Learned(2)&&session.CanMutate,"Actual party-load did not restore frozen extra state");
        Call(0x284500,2);Call(0x2CF768);
        Check(session.Stock(4)==2&&session.Learned(2)&&session.CanMutate,"Cached repeated party-load lost learning/stock without a new file callback");
        // Regression from PID5588: using an extra flask leaves the field
        // SaveWork identical. Multiple subsequent autosaves must update extras
        // without blocking Items or leaving a stale pending slot object.
        for(int cycle=0;cycle<4;cycle++)
        {
            int expected=cycle%2+1;
            Call(0x2847F8,261,expected-session.Stock(4));
            Call(0x2CEEE8);
            Call(0x32B94,0,0x10000,(long)wire.Address+50000,(long)wire.Address,savePacket.Length);
            Call(0x32E60,0,0,(long)wire.Address+51000);
            Check(session.CanMutate&&session.Learned(2),"Same-native autosave blocked paid battle learning");
            Call(0x284500,2);Call(0x2CF768);
            Check(session.Stock(4)==expected&&session.Learned(2)&&session.CanMutate,"Same-native autosave did not restore the last confirmed extras");
            Check(Call(0x3170CC,0,6,4)==1,"Autosave replacement lost battle learned-bit eligibility");
            Check(Call(0x30E368,0,6,(long)output.Address,(long)parallel.Address,(long)parallel.Address+64,0,(long)parallel.Address+128,(long)parallel.Address+192)>0,"Autosave replacement emptied the stocked/learned battle list");
        }
        // Sidecar IO failure must leave paid learning and battle use intact.
        var ioRegistry=new ExpandedSaveRegistry(ChemistActionBindings.ExtraKeys,ChemistActionBindings.ExtraKeys);
        var ioStore=new ExpandedSaveStore(Path.Combine(temp,"FailedSidecars"),ioRegistry);
        var ioCommit=new NativePartySaveCommit(ioRegistry,ioStore);
        var ioSlot=new SaveSlotIdentity("fixture-profile","io-failure");
        Call(0x2CEEE8);var ioSnapshot=session.Frozen() ?? throw new Exception("Failed-write fixture lacks frozen state");
        Check(ioCommit.Enqueue(0x40000,ioSlot,savePacket,serialized,ioSnapshot.State),"Failed-write fixture not queued");
        Directory.CreateDirectory(ioStore.GetPath(ioSlot,ExpandedSaveRegistry.Hash(serialized)));
        var ioObserver=new BoundPartySaveObserver("fixture-profile",ioCommit,session.Frozen,session.Block,logs.Add);
        ioObserver.AfterWriteCompletion(new(0x40000,ioSlot,savePacket,0),0);
        Check(session.CanMutate&&session.Learned(2)&&session.Stock(4)==2&&Call(0x3170CC,0,6,4)==1,"Disk publication failure disabled valid live learned actions");
        Check(ioCommit.Complete(0x40000,ioSlot,savePacket,0)==SaveCommitResult.NoMatchingRequest&&logs.Any(l=>l.Contains("[Aviso] Extras deste save não foram gravados")),"Failed publication claimed success or retained its native request");
        Call(0x284500,0);Check(session.Stock(4)==0&&!session.Learned(2)&&session.Frozen() is null,"New party inherited extra state");
        // Full release: independent learning/stock for ALL eleven extras, not
        // eleven aliases of the previously validated Venom identity.
        menu.Write(0x7E,[0,0,0]);native.Write(0x1853CE0+0xA5,[0,0,0]);
        int total=ChemistActionBindings.TestActions.Sum(a=>a.JpCost);
        Check(total==6660,"Configured complete action JP total differs");
        menu.Write(0xCC,BitConverter.GetBytes((ushort)total));
        for(int s=0;s<15;s++)
        {
            int before=W(menu.Read(0xCC,2));Confirm(s);
            Check(W(menu.Read(0xCC,2))==before-ChemistActionBindings.TestActions[s].JpCost,"Full action JP deduction differs: "+s);
            Confirm(s);Check(W(menu.Read(0xCC,2))==before-ChemistActionBindings.TestActions[s].JpCost,"Full action charged twice: "+s);
        }
        Check(W(menu.Read(0xCC,2))==0&&session.ExtraLearning(2)==ChemistActionBindings.ExtraMask,"Full acquisition lost independent extra bits");
        Check(Call(0x28A6E8,0,75,0,(long)output.Address,5)==15,"Learned-only list lost full actions");
        for(int s=0;s<15;s++)Check((W(output.Read(s*8+2,2))&1023)==ChemistActionBindings.TestActions[s].AbilityId,"Learned action identity differs");
        Check(Call(0x28A6E8,0,75,0,0,3)==0,"Fully learned command not mastered");
        native.Write(0x1853CE0+0xA5,menu.Read(0x7E,3));
        foreach(int s in ChemistActionBindings.ExtraSlots)
        {
            Check(Call(0x2847F8,ChemistActionBindings.Item(s),s)==s&&session.Stock(s)==s,"Independent item purchase stock failed: "+s);
            long itemAddress=Call(0x2B8C44,ChemistActionBindings.Item(s));
            Check(W(CheckedNativeRead.Read(itemAddress+8,2))==ChemistExtraEffects.Price(s),"Native item price differs: "+s);
            Check(effect(0,ChemistActionBindings.TestActions[s].AbilityId)==ChemistExtraEffects.ImpactTemplate(s),"New visual effect differs from its impact mapping: "+s);
        }
        foreach(int s in Enumerable.Range(0,4))native.Write(0x11A7C00+ChemistActionBindings.Item(s),[3]);
        NativeLifetimeMemory.WriteProtected(shared,[255,255]);
        Check(Call(0x288B94,0,0,7,shared,0,0)==11,"Full inventory did not add eleven independent stacks");
        Check(Enumerable.Range(0,11).Select(i=>W(CheckedNativeRead.Read(shared+i*2,2))).Distinct().Count()==11,"Full inventory item aliases");
        Check(Call(0x30E368,0,6,(long)output.Address,(long)parallel.Address,(long)parallel.Address+64,0,(long)parallel.Address+128,(long)parallel.Address+192)==15,"Full battle list lost learned/stocked actions");
        for(int s=0;s<15;s++)Check(W(output.Read(s*2,2))==ChemistActionBindings.TestActions[s].AbilityId&&Call(0x3170CC,0,6,s)==1,"Full battle eligibility differs");
        foreach(int s in ChemistActionBindings.ExtraSlots)
        {
            ushort ability=ChemistActionBindings.TestActions[s].AbilityId;
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2),ability);reaction.Write(0,packet);
            Check(Call(0x27FE20,(long)reaction.Address)==0&&Call(0x27FFE8,(long)reaction.Address)==0,"Full human target pipeline rejects action: "+s);
            Check(Enumerable.Range(0,256).Count(i=>(native.Read(0xD8DCB0+i*8+5,1)[0]&0x80)!=0)==1,"Extra action is not single-target");
            int[] beforeStock=ChemistActionBindings.ExtraSlots.Select(session.Stock).ToArray();
            Call(0x281488,(long)reaction.Address,(long)output.Address,0);
            Check(beforeStock.SequenceEqual(ChemistActionBindings.ExtraSlots.Select(session.Stock)),"Extra preview consumed stock");
            native.Write(0x186AF80,[2,0,0,0]);Call(0x281488,(long)reaction.Address,(long)output.Address,1);
            Check(beforeStock.SequenceEqual(ChemistActionBindings.ExtraSlots.Select(session.Stock)),"Extra prediction consumed stock");
            native.Write(0x186AF80,[0,0,0,0]);Call(0x281488,(long)reaction.Address,(long)output.Address,1);
            Check(W(output.Read(2,2))==ability&&W(output.Read(8,2))==ChemistActionBindings.Item(s),"Extra commitment item/ability alias");
            foreach(int t in ChemistActionBindings.ExtraSlots)Check(session.Stock(t)==beforeStock[t-4]-(t==s?1:0),"Extra consumption touched another item");
            byte[] fullRequest=new byte[4];BinaryPrimitives.WriteUInt16LittleEndian(fullRequest,ability);fullRequest[2]=6;reaction.Write(0,fullRequest);
            Call(0x320504,(long)reaction.Address);
            Check(W(native.Read(0x18716A2,2))==ability&&W(native.Read(0x18716A6,2))==ChemistActionBindings.Item(s)&&native.Read(0x18716B1,1)[0]==ChemistExtraEffects.Element(s),"Extra AI selection lost element or item");
        }
        // Execute the dispatched formula stub + ORIGINAL native element/status
        // bodies. Only their scene scratch pointer and already-initialized
        // Preformula result are owned fixtures; no status helper is stubbed.
        using var result=new OwnedNativeMemory(4096);using var sceneScratch=new OwnedNativeMemory(0xB0000);
        Console.WriteLine("Fixture: actual formula bodies");
        native.Write(0x3CD9EF0,BitConverter.GetBytes((long)sceneScratch.Address));
        native.Write(0x186AF70,BitConverter.GetBytes((long)result.Address));
        native.Write(0x186AF68,BitConverter.GetBytes((long)native.Address+0x1853CE0+512));
        native.Write(0x186AF78,BitConverter.GetBytes((long)native.Address+0x1853CE0));
        long table=(long)native.Address+BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x309F57,4));
        long formulaAddress=BinaryPrimitives.ReadInt64LittleEndian(CheckedNativeRead.Read(table+107*8,8));
        var formula=Marshal.GetDelegateForFunctionPointer<Eight>((nint)formulaAddress);
        void InitFormula(int s,int faith=100)
        {
            result.Write(0,new byte[0x38]);result.Write(0,[1]);result.Write(0x2C,BitConverter.GetBytes((ushort)100));
            native.Write(0x1853CE0+512+0x57,new byte[0x23]);native.Write(0x1853CE0+512+0x1B4,[0]);
            native.Write(0x7B0760,new byte[0x60]);native.Write(0x7B0774,[(byte)faith,(byte)faith,6,0]);
            native.Write(0x7B0778,BitConverter.GetBytes(ChemistActionBindings.TestActions[s].AbilityId));
            byte[] mask=native.Read(0x80FBA0+ChemistExtraEffects.Status(s)*6,6);
            native.Write(0x7B07B0,mask);native.Write(0x7B0794,mask.Skip(1).ToArray());
        }
        foreach(int s in ChemistActionBindings.ExtraSlots.Where(s=>!ChemistExtraEffects.Elemental(s)))
        {
            byte[] mask=native.Read(0x80FBA0+ChemistExtraEffects.Status(s)*6+1,5);
            foreach(int faith in new[]{0,1,50,100})
            {
                InitFormula(s,faith);formula();
                Check(result.Read(0x1D,5).SequenceEqual(mask)&&result.Read(0,1)[0]==1&&W(result.Read(0x2C,2))==100,"Native fixed status/Faith failed: "+s);
            }
            InitFormula(s);native.Write(0x1853CE0+512+0x5C,mask);formula();
            Check(result.Read(0x1D,5).All(b=>b==0)&&result.Read(0,1)[0]==0,"Native status immunity bypassed: "+s);
        }
        foreach(int s in new[]{6,7,8})
        {
            byte element=ChemistExtraEffects.Element(s);
            native.Write(0x1853CE0+512+0x32,BitConverter.GetBytes((ushort)150));
            foreach(int faith in new[]{0,1,50,100})
            {InitFormula(s,faith);formula();Check(W(result.Read(6,2))==28&&result.Read(0,1)[0]==1,"MaxHP elemental damage used Faith");}
            foreach(ushort maxHp in new ushort[]{0,1,35,65,100,150,151,999,9999,65535})
            foreach(ushort remaining in new ushort[]{0,1,20})
            {
                InitFormula(s);native.Write(0x1853CE0+512+0x32,BitConverter.GetBytes(maxHp));
                native.Write(0x1853CE0+512+0x30,BitConverter.GetBytes(remaining));formula();
                Check(W(result.Read(6,2))==5+(maxHp*15+99)/100,"Elemental formula depends on currentHP or rounds/overflows incorrectly");
            }
            native.Write(0x1853CE0+512+0x32,BitConverter.GetBytes((ushort)150));
            InitFormula(s);native.Write(0x1853CE0+512+0x78,[element]);formula();Check(W(result.Read(6,2))==14,"Native elemental half resistance differs");
            InitFormula(s);native.Write(0x1853CE0+512+0x79,[element]);formula();Check(W(result.Read(6,2))==56,"Native elemental weakness differs");
            InitFormula(s);native.Write(0x1853CE0+512+0x77,[element]);formula();Check(W(result.Read(6,2))==0&&result.Read(0,1)[0]==0,"Native elemental null immunity bypassed");
            InitFormula(s);native.Write(0x1853CE0+512+0x76,[element]);formula();Check(W(result.Read(6,2))==0&&W(result.Read(8,2))==28&&(result.Read(0x27,1)[0]&0x40)!=0,"Native elemental absorption not healing");
            InitFormula(s);result.Write(0,[0]);formula();Check(W(result.Read(6,2))==0&&result.Read(0,1)[0]==0,"Element formula overrides preformula veto");
        }
        InitFormula(6);native.Write(0x1853CE0+512+0x63,[0x80]);native.Write(0x1853CE0+512+0x1F1,[0x80]);formula();
        Check(W(result.Read(6,2))==56&&(result.Read(0x24,1)[0]&0x80)!=0,"Native Oil+Fire effect/removal lost");
        Console.WriteLine("Fixture: three elemental flasks formula107 uses5 + ceiling15% target maxHP, currentHP/Faith independent, ushort bounds, native half/weak/null/absorb/Oil and preformula veto preserved.");
        // Freeze complete state; mutate all live counts/learning AFTER queueing;
        // successful async commit must restore the frozen snapshot, not live data.
        record=native.Read(0x11A7D10+2*600,600);record.CopyTo(serialized,NativePartySaveCommit.RecordsOffset+1200);
        native.Write(0x2E80450,serialized);Call(0x2CEEE8);
        Check(session.Frozen()!.Value.State.Items.Count==11&&session.Frozen()!.Value.State.Units.Single().LearnedKeys.Length==11,"Frozen full save omitted extra keys");
        serialized.CopyTo(savePacket,0x164);BinaryPrimitives.WriteUInt32LittleEndian(savePacket.AsSpan(4),NativeSavePacket.Crc32(savePacket.AsSpan(16)));wire.Write(0,savePacket);
        Call(0x32B94,0,0x10000,(long)wire.Address+50000,(long)wire.Address,savePacket.Length);
        foreach(int s in ChemistActionBindings.ExtraSlots)Call(0x2847F8,ChemistActionBindings.Item(s),50);
        session.RestoreExtraLearning(2,0);Call(0x32E60,0,0,(long)wire.Address+51000);
        Call(0x33DF0,0,(long)wire.Address+50000,0,0,(long)wire.Address,savePacket.Length);Call(0x284500,2);Call(0x2CF768);
        Check(session.ExtraLearning(2)==ChemistActionBindings.ExtraMask&&ChemistActionBindings.ExtraSlots.All(s=>session.Stock(s)==s-1),"Full frozen async save lost independent extra stock/learning");
        Call(0x284500,2);Call(0x2CF768);
        Check(session.ExtraLearning(2)==ChemistActionBindings.ExtraMask&&ChemistActionBindings.ExtraSlots.All(s=>session.Stock(s)==s-1),"Full cached reload lost extras");
        // In-battle snapshots have their own serialized records and packet
        // identity, not the stale world SaveWork. Persist current extra stock
        // and restore it after native party initialization during Continue.
        native.Write(0x11A7D10+2*600+0x30,[71]);
        byte[] battleInner=new byte[0x154+0xA31D0];
        BinaryPrimitives.WriteUInt32LittleEndian(battleInner,0x49544646);
        BinaryPrimitives.WriteUInt32LittleEndian(battleInner.AsSpan(4),0x7A);
        BinaryPrimitives.WriteUInt32LittleEndian(battleInner.AsSpan(12),1);
        native.Read(0x11A7D10,NativePartySaveCommit.RecordsSize).CopyTo(battleInner,NativePartySaveCommit.BattleRecordsOffset);
        byte[] battlePacket=new byte[battleInner.Length+16];battleInner.CopyTo(battlePacket,16);
        BinaryPrimitives.WriteUInt32LittleEndian(battlePacket,16);BinaryPrimitives.WriteUInt32LittleEndian(battlePacket.AsSpan(8),0x11);
        BinaryPrimitives.WriteUInt32LittleEndian(battlePacket.AsSpan(4),NativeSavePacket.Crc32(battleInner));
        using var battleWire=new OwnedNativeMemory(0x110000);battleWire.Write(0,battlePacket);battleWire.Write(0x100000,"fixture-battle\0"u8.ToArray());
        Call(0x32B94,0,0x20000,(long)battleWire.Address+0x100000,(long)battleWire.Address,battlePacket.Length);
        byte[] battleEntry=new byte[NativeSaveEvents.QueueEntrySize];BinaryPrimitives.WriteInt64LittleEndian(battleEntry,battlePacket.Length);
        BinaryPrimitives.WriteInt64LittleEndian(battleEntry.AsSpan(8),(long)battleWire.Address);BinaryPrimitives.WriteInt64LittleEndian(battleEntry.AsSpan(0x18),0x20000);
        "fixture-battle\0"u8.CopyTo(battleEntry.AsSpan(0x20));battleWire.Write(0x100100,battleEntry);
        foreach(int s in ChemistActionBindings.ExtraSlots)Call(0x2847F8,ChemistActionBindings.Item(s),50);
        session.RestoreExtraLearning(2,0);Call(0x32E60,0,0,(long)battleWire.Address+0x100100);
        using var autoController=new OwnedNativeMemory(0x400000);
        native.Write(0x3CD9EE0,BitConverter.GetBytes((long)autoController.Address));
        autoController.Write(0x3D5188,BitConverter.GetBytes((long)battleWire.Address+16));
        autoController.Write(0x3D5190,BitConverter.GetBytes((long)battleInner.Length));
        var fixtureHookList=((System.Collections.IEnumerable)typeof(ChemistPlayableHost).GetField("_hooks",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Cast<object>().ToArray();
        long autoOriginal=((nint)fixtureHookList[31].GetType().GetProperty("OriginalFunctionAddress")!.GetValue(fixtureHookList[31])!).ToInt64();
        NativeLifetimeMemory.WriteProtected(autoOriginal,[0xB8,1,0,0,0,0xC3]);
        Call(0x2181EC);Call(0x284500,2);Call(0x279BF4,(long)native.Address+0x11A7D10,1);
        Check(session.ExtraLearning(2)==ChemistActionBindings.ExtraMask&&ChemistActionBindings.ExtraSlots.All(s=>session.Stock(s)==s-1),"In-battle Continue lost frozen packet stock/learning across initialization");
        native.Write(0x11A7D10+2*600,record);native.Write(0x3CD9EE0,BitConverter.GetBytes(0L));
        NativeLifetimeMemory.WriteProtected(autoOriginal,[0x31,0xC0,0xC3]);
        Call(0x284500,0);Check(session.ExtraLearning(2)==0&&ChemistActionBindings.ExtraSlots.All(s=>session.Stock(s)==0),"New game inherited full extra state");
        // Enhanced load uses the actually selected pointer, not old global
        // scratch or a previous read callback. Native load mutates SaveWork;
        // the hook must preserve its pre-load fingerprint.
        using var selectedSave=new OwnedNativeMemory(65536);selectedSave.Write(0,serialized);
        native.Write(0xD407A0,BitConverter.GetBytes((long)selectedSave.Address));
        native.Write(0x2E80450,new byte[serialized.Length]);
        var hookList=((System.Collections.IEnumerable)typeof(ChemistPlayableHost).GetField("_hooks",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Cast<object>().ToArray();
        long enhancedOriginal=((nint)hookList[30].GetType().GetProperty("OriginalFunctionAddress")!.GetValue(hookList[30])!).ToInt64();
        var copyCode=new List<byte>{0x56,0x57,0x48,0xBE};copyCode.AddRange(BitConverter.GetBytes((long)selectedSave.Address+NativePartySaveCommit.RecordsOffset));
        copyCode.AddRange(new byte[]{0x48,0xBF});copyCode.AddRange(BitConverter.GetBytes((long)native.Address+0x11A7D10));
        copyCode.Add(0xB9);copyCode.AddRange(BitConverter.GetBytes(NativePartySaveCommit.RecordsSize));copyCode.AddRange(new byte[]{0xF3,0xA4,0x48,0xB8});
        copyCode.AddRange(BitConverter.GetBytes((long)selectedSave.Address));copyCode.AddRange(new byte[]{0x80,0x30,1,0x5F,0x5E,0xB8,1,0,0,0,0xC3});
        using var copyFixture=new OwnedNativeMemory(4096);copyFixture.Write(0,copyCode.ToArray());copyFixture.ExecutablePage(0);
        NativeLifetimeMemory.WriteProtected(enhancedOriginal,new byte[]{0xFF,0x25,0,0,0,0}.Concat(BitConverter.GetBytes((long)copyFixture.Address)).ToArray());
        Call(0x21B0E8,0);
        Check(session.ExtraLearning(2)==ChemistActionBindings.ExtraMask&&ChemistActionBindings.ExtraSlots.All(s=>session.Stock(s)==s-1),"Enhanced cached pointer load lost extra state or used post-load fingerprint");
        // Real manual preparation uses a DIFFERENT native buffer, 50 slots
        // and manual metadata. Reproduce the missing-autosave-hash failure;
        // only the explicitly prepared slot may supply the new sidecar.
        const int manualSlot=7;
        byte[] manualWork=(byte[])serialized.Clone();manualWork[0x11A]=1;manualWork[0x11C]^=3;
        Check(!manualWork.SequenceEqual(serialized),"Manual fixture must differ from autosave");
        native.Write(0x2C8B970+manualSlot*(NativePartySaveCommit.WorkSize+8),manualWork);
        native.Write(0x2E80450,new byte[serialized.Length]);
        Call(0x0C4420,0,manualSlot,1);
        Check(session.ManualFrozen() is { } mf && mf.Slot==manualSlot&&mf.State.Units.Single().LearnedKeys.Length==11,"Prepared manual slot not captured");
        byte[] manualPacket=new byte[16+50*(NativePartySaveCommit.WorkSize+8)];
        manualWork.CopyTo(manualPacket,16+manualSlot*(NativePartySaveCommit.WorkSize+8));
        BinaryPrimitives.WriteUInt32LittleEndian(manualPacket,16);BinaryPrimitives.WriteUInt32LittleEndian(manualPacket.AsSpan(8),0xE);
        BinaryPrimitives.WriteUInt32LittleEndian(manualPacket.AsSpan(4),NativeSavePacket.Crc32(manualPacket.AsSpan(16)));
        using var manualWire=new OwnedNativeMemory(0x210000);manualWire.Write(0,manualPacket);manualWire.Write(0x200000,"fixture-manual\0"u8.ToArray());
        Call(0x32B94,0,0x30000,(long)manualWire.Address+0x200000,(long)manualWire.Address,manualPacket.Length);
        byte[] manualEntry=new byte[NativeSaveEvents.QueueEntrySize];BinaryPrimitives.WriteInt64LittleEndian(manualEntry,manualPacket.Length);
        BinaryPrimitives.WriteInt64LittleEndian(manualEntry.AsSpan(8),(long)manualWire.Address);BinaryPrimitives.WriteInt64LittleEndian(manualEntry.AsSpan(0x18),0x30000);
        "fixture-manual\0"u8.CopyTo(manualEntry.AsSpan(0x20));manualWire.Write(0x200100,manualEntry);
        foreach(int s in ChemistActionBindings.ExtraSlots)Call(0x2847F8,ChemistActionBindings.Item(s),-99);
        session.RestoreExtraLearning(2,0);Call(0x32E60,0,0,(long)manualWire.Address+0x200100);
        Call(0x284500,0);selectedSave.Write(0,manualWork);Call(0x21B0E8,0);
        Check(session.ExtraLearning(2)==ChemistActionBindings.ExtraMask&&ChemistActionBindings.ExtraSlots.All(s=>session.Stock(s)==s-1),"Manual native preparation/queue/write/reload lost frozen eleven learned/stock identities");
        Check(session.Saves.RestoreConsumedWork("fixture-profile",new byte[serialized.Length]) is null,"Untouched manual slot inherited expanded state");
        // Churn the complete manual snapshot/queue/write/load pipeline, not
        // merely a Dictionary callback. Sources stay native-owned while the
        // CLR worker serializes/validates and a separate thread compacts GC.
        using(var stopGc=new CancellationTokenSource())
        {
            Console.WriteLine("Fixture: 250 manual saves with concurrent GC");
            var compact=Task.Run(()=>{while(!stopGc.IsCancellationRequested){GC.Collect(2,GCCollectionMode.Forced,true,true);Thread.Sleep(1);}});
            try
            {
                for(int round=0;round<250;round++)
                {
                    if(round%50==0)Console.WriteLine($"Fixture: manual stress {round}/250");
                    native.Write(0x11A7D10+2*600,record);
                    Call(0x0C4420,0,manualSlot,1);
                    Call(0x32B94,0,0x30000,(long)manualWire.Address+0x200000,(long)manualWire.Address,manualPacket.Length);
                    Call(0x32E60,0,0,(long)manualWire.Address+0x200100);
                    selectedSave.Write(0,manualWork);Call(0x21B0E8,0);
                    Check(session.ExtraLearning(2)==ChemistActionBindings.ExtraMask&&ChemistActionBindings.ExtraSlots.All(s=>session.Stock(s)==s-1),"Repeated GC/manual pipeline lost frozen learning or stock");
                }
            }
            finally{stopGc.Cancel();compact.GetAwaiter().GetResult();}
        }
        // Exact consumed autosave and in-battle Continue, without legacy read.
        Call(0x284500,0);native.Write(0x2E80450,serialized);Call(0x2181EC);
        Check(session.ExtraLearning(2)==ChemistActionBindings.ExtraMask&&session.Stock(5)==4,"Autosave load lost Oil/learning");
        Call(0x284500,0);Call(0x279BF4,(long)native.Address+0x11A7D10,1);
        Check(session.ExtraLearning(2)==ChemistActionBindings.ExtraMask&&session.Stock(5)==4,"Battle Continue lost Oil/learning");
        Call(0x284500,0);selectedSave.Write(0,new byte[serialized.Length]);Call(0x21B0E8,0);
        Check(session.ExtraLearning(2)==0&&ChemistActionBindings.ExtraSlots.All(s=>session.Stock(s)==0),"Unrelated native-only save inherited learned abilities");
        Check(!logs.Any(l=>l.Contains("[Erro]")),string.Join("\n",logs));
        // Read a genuine old schema document through the expanded registry,
        // without opening or changing any user's real save/sidecar.
        string legacyFolder=Path.Combine(temp,"LegacySidecars");
        var legacyRegistry=new ExpandedSaveRegistry(["venom-flask"],["venom-flask"]);
        var legacyStore=new ExpandedSaveStore(legacyFolder,legacyRegistry);
        var legacySlot=new SaveSlotIdentity("fixture-profile","old-venom-slot");
        string payloadHash=ExpandedSaveRegistry.Hash(serialized),recordHash=ExpandedSaveRegistry.Hash(record);
        legacyStore.Publish(legacySlot,payloadHash,new(new(){{"venom-flask",8}},[new(2,recordHash,["venom-flask"])]));
        var fullStore=new ExpandedSaveStore(legacyFolder,new(ChemistActionBindings.ExtraKeys,ChemistActionBindings.ExtraKeys));
        var migrated=fullStore.Load(legacySlot,payloadHash,new Dictionary<int,string>{{2,recordHash}})!;
        Check(migrated.Items["venom-flask"]==8&&migrated.Units[0].LearnedKeys.SequenceEqual(new[]{"venom-flask"})&&ChemistActionBindings.ExtraKeys.Skip(1).All(k=>migrated.Items.GetValueOrDefault(k)==0),"Prior Venom save not compatible with full release");
        CheckConsumableCompletion(native,host,logs);
Console.WriteLine("PASS: full host /94 guarded operands; 15 acquisition/confirmation pairs, exact paid JP and learning; 11 independent stock/save identities; real15-shop story unlocks at1/5/9/13 and original sorting; range4/radius0, target veto, one selected stack consumed at commit; all8 status immunity/Faith cases; elemental5+ceiling15% maxHP across ushort bounds, native half/weak/null/absorb/Oil; native async frozen save/load and250 manual cycles under concurrent compacting GC; original25-option AI regression suite. Campaign lookup, scene/UI/CRT helpers and scheduling remain explicit fixtures, NOT live gameplay verification. No live process or actual save touched.");
        // Retain fixture image while active hooks exist; process exit reclaims
        // code/allocations. Do not release hooked image pages before exit.
        ChemistAiTests(native,host,logs);
        GC.KeepAlive(host);GC.KeepAlive(bridge);
    }
    static ushort W(byte[] value)=>BinaryPrimitives.ReadUInt16LittleEndian(value);
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void StubOriginal(object host,int[] fixtureIndices)
    {
        var list=((System.Collections.IEnumerable)host.GetType().GetField("_hooks",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Cast<object>().ToArray();
        foreach(int i in fixtureIndices)
            NativeLifetimeMemory.WriteProtected(((nint)list[i].GetType().GetProperty("OriginalFunctionAddress")!.GetValue(list[i])!).ToInt64(),[0x31,0xC0,0xC3]);
    }
}

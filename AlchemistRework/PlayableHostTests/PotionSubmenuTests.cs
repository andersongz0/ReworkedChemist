using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using FFTModLoader.ContentExpansion;
using FFTModLoader.ContentExpansion.Runtime;

unsafe partial class Program
{
    static void PotionSubmenuTests(OwnedNativeMemory native,ChemistPlayableHost host)
    {
        long img=(long)native.Address;
        long Call(int rva,long a=0,long b=0,long c=0,long d=0,long e=0,long f=0,long g=0,long h=0)=>
            Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+rva)(a,b,c,d,e,f,g,h);
        var hooks=((System.Collections.IEnumerable)typeof(ChemistPlayableHost).GetField("_hooks",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!).Cast<object>().ToArray();
        // The two AI observers are appended after the human modal observer.
        var modalHook=hooks[^3];
        long original=(long)(nint)modalHook.GetType().GetProperty("OriginalFunctionAddress")!.GetValue(modalHook)!;
        var saved=new Dictionary<long,byte[]>();
        void Save(long p,int n)=>saved[p]=CheckedNativeRead.Read(p,n);
        foreach(int rva in new[]{0xD40950,0x184A9B0,0x7DCF9A,0xD407B8,0x783088,0xD40948,0x3CD9DD0,0xD407A8})Save(img+rva,8);
        Save(img+0x2FD35A4,20);Save(img+0x7831B0,72);Save(img+0xD492D0,160);
        Save(img+0x1853CE0,512);Save(img+0x11A7C00+240,14);
        Save(img+0x18716A0,20);Save(img+0x186AF80,4);
        Save(img+0x18004E0,1280);Save(img+0xD8DCB0,4096);
        Save(original,14);Save(img+0x23709D,14);Save(img+0x231C6A,14);
        Save(img+0x0FC598,14);Save(img+0x22B9F4,14);Save(img+0x2F7F4C,14);
        Save(img+0x7DCB48,8);Save(img+0x782FA8,12);Save(img+0xD408E8,2);
        Save(img+0xD40AA0,0x8800);Save(img+0x2FF9F28,4);
        Save(img+0x782CAC,4);Save(img+0xD407D4,2);
        Save(img+0x3791D70,0x120);Save(img+0x811624,4);
        Save(img+0x23970C,14);Save(img+0x2F3710,14);
        using var ui=new OwnedNativeMemory(4096);using var list=new OwnedNativeMemory(4096);
        using var script=new OwnedNativeMemory(16384);using var leaf=new OwnedNativeMemory(4096);
        using var start=new OwnedNativeMemory(4096);using var packet=new OwnedNativeMemory(4096);
        using var task=new OwnedNativeMemory(65536);
        byte[] Jump(long p)=>[0xFF,0x25,0,0,0,0,..BitConverter.GetBytes(p)];
        List<byte> code=[];
        void Emit(string s)=>code.AddRange(Convert.FromHexString(s.Replace(" ","")));
        void Addr(long p)=>code.AddRange(BitConverter.GetBytes(p));
        // Explicit native UI input/raster fixture: snapshot each displayed list,
        // then choose/cancel the scripted row. No CLR callback on a game stack.
        Emit("56 57 48 83 EC 28 49 BB");Addr((long)script.Address);
        // Scheduler regression: the native outer action task waits on +90.
        // Previously Show killed this task before its post-observer opened
        // the child. Capture any prematurely released target-input owner.
        Emit("48 B8");Addr((long)task.Address+0x90);
        Emit("48 83 38 00 74 04 41 FF 43 0C");
        Emit("48 B8");Addr((long)task.Address+0x810);
        Emit("8B 08 41 09 4B 08");
        Emit("41 8B 03 41 FF 03 48 C1 E0 0A 48 BF");Addr((long)script.Address+0x100);
        Emit("48 01 C7 48 BE");Addr((long)list.Address);
        Emit("B9 88 03 00 00 F3 A4 48 BE");Addr(img+0xD492D0);
        Emit("B9 10 00 00 00 F3 A4 48 BE");Addr((long)ui.Address+0x130);
        Emit("48 8B 36 B9 10 00 00 00 F3 A4 48 BE");Addr((long)ui.Address+0x158);
        Emit("B9 02 00 00 00 F3 A4 41 8B 0B FF C9 41 8B 44 8B 20 49 BA");Addr((long)ui.Address);
        Emit("66 41 89 82 58 01 00 00 C1 E8 10 49 BA");Addr(img+0xD407B8);
        Emit("66 41 89 02 66 83 F8 FF 0F 84");int cancelJump=code.Count;code.AddRange(new byte[4]);
        Emit("48 B9");Addr((long)ui.Address+0x108);
        Emit("0F BF 91 50 00 00 00 48 B8");Addr(img+0x22BA70);
        Emit("FF D0");
        // Error/ordinary windows are explicitly dismissed by the scene fixture.
        // The production adapter must never start either for a stocked child.
        Emit("48 B8");Addr((long)task.Address+0x90);
        Emit("48 C7 00 00 00 00 00 E9");int confirmedJump=code.Count;code.AddRange(new byte[4]);
        int cancelLabel=code.Count;
        Emit("48 B9");Addr((long)ui.Address+0x108);Emit("48 B8");Addr(img+0x22BE6C);Emit("FF D0");
        int listKill=code.Count;
        byte[] cancelDelta=BitConverter.GetBytes(cancelLabel-cancelJump-4);for(int i=0;i<4;i++)code[cancelJump+i]=cancelDelta[i];
        byte[] confirmedDelta=BitConverter.GetBytes(listKill-confirmedJump-4);for(int i=0;i<4;i++)code[confirmedJump+i]=confirmedDelta[i];
        Emit("48 B8");Addr(img+0x231C65);
        Emit("FF D0 48 83 C4 28 5F 5E 31 C0 C3");
        leaf.Write(0,code.ToArray());
        // Native validation wait is isolated from the rendered scene. Count
        // it and finish its actual task, so the outer menu cannot run ahead.
        leaf.Write(1024,[0x48,0xB8,..BitConverter.GetBytes((long)script.Address),0xFF,0x40,0x10,
            0x48,0xB8,..BitConverter.GetBytes((long)task.Address+0x90),0x48,0xC7,0,0,0,0,0,0xC3]);
        leaf.ExecutablePage(0);
        start.Write(0,[0x48,0x83,0xEC,0x28,0x48,0xB8,..BitConverter.GetBytes(img+0x237098),0xFF,0xE0]);start.ExecutablePage(0);
        try
        {
            NativeLifetimeMemory.WriteProtected(original,Jump((long)leaf.Address));
            NativeLifetimeMemory.WriteProtected(img+0x23709D,[0x48,0x83,0xC4,0x28,0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x231C6A,[0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x0FC598,[0xB8,1,0,0,0,0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x22B9F4,[0xB8,1,0,0,0,0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x2F7F4C,Jump((long)leaf.Address+1024));
            native.Write(0x7DCB48,BitConverter.GetBytes((long)packet.Address));native.Write(0xD408E8,[0,0]);
            // Execute real task_killmyself scheduler writes; tutorial/fiber
            // dispatch are explicit non-yielding fixtures in this OWN image.
            NativeLifetimeMemory.WriteProtected(img+0x23970C,[0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x2F3710,[0xC3]);
            native.Write(0xD40950,BitConverter.GetBytes((long)ui.Address));
            native.Write(0x184A9B0,BitConverter.GetBytes((long)list.Address));native.Write(0x7DCF9A,[0,0]);
            native.Write(0x783088,BitConverter.GetBytes((long)task.Address));native.Write(0xD40948,new byte[4]);
            native.Write(0x3CD9DD0,BitConverter.GetBytes((long)ui.Address));native.Write(0xD407A8,new byte[8]);
            task.Write(0x10,BitConverter.GetBytes(1L));
            Check(Call(0x22BF60,(long)packet.Address)==1,"Real multiwindow_break did not reproduce prior window completion");
            task.Write(0x10,new byte[8]);
            Check(Call(0x22BF60,(long)packet.Address)==0,"Real multiwindow_break still closed the reset child");
            var scopes=(NativeLifetimeMemory)typeof(ChemistPlayableHost).GetField("_potionScopes",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
            void KillFromList()
            {
                using var invoke=new OwnedNativeMemory(4096);
                invoke.Write(0,[0x48,0x83,0xEC,0x28,0x48,0xB8,..BitConverter.GetBytes(img+0x231C65),0xFF,0xD0,0x48,0x83,0xC4,0x28,0xC3]);invoke.ExecutablePage(0);
                Marshal.GetDelegateForFunctionPointer<Eight>(invoke.Address)();
            }
            task.Write(0x90,BitConverter.GetBytes(1L));KillFromList();
            Check(BitConverter.ToInt64(task.Read(0x90,8))==0,"Unscoped list no longer executes native task kill");
            scopes.Write(0,[1,0,0,0]);scopes.Write(8,BitConverter.GetBytes((long)task.Address));scopes.Write(16,BitConverter.GetBytes((long)ui.Address));
            task.Write(0x90,BitConverter.GetBytes(1L));KillFromList();
            Check(BitConverter.ToInt64(task.Read(0x90,8))==1,"Scoped native list killed itself before child choice");
            Call(0x2F7EC8);
            Check(BitConverter.ToInt64(task.Read(0x90,8))==0,"Original outer task kill incorrectly deferred");
            foreach(int mismatch in new[]{0,1,2,3})
            {
                native.Write(0xD40948,new byte[4]);
                scopes.Write(0,[1,0,0,0]);scopes.Write(8,BitConverter.GetBytes((long)task.Address));scopes.Write(16,BitConverter.GetBytes((long)ui.Address));
                if(mismatch==0)scopes.Write(0,new byte[4]);
                if(mismatch==1)scopes.Write(8,BitConverter.GetBytes((long)task.Address+8));
                if(mismatch==2)scopes.Write(16,BitConverter.GetBytes((long)ui.Address+8));
                if(mismatch==3)native.Write(0xD40948,BitConverter.GetBytes(1));
                task.Write(0x90+mismatch/3*0x800,BitConverter.GetBytes(1L));KillFromList();
                Check(BitConverter.ToInt64(task.Read(0x90+mismatch/3*0x800,8))==0,"Unrelated window/task kill intercepted");
            }
            scopes.Write(0,new byte[NativePotionWindowLifecycle.Slots*NativePotionWindowLifecycle.RecordSize]);native.Write(0xD40948,new byte[4]);
            native.Write(0x1853CE0+1,[0]);native.Write(0x1853CE0+6,[0]);native.Write(0x1853CE0+0xA5,[0x80,0,0]);
            void Prepare(params uint[] steps)
            {
                script.Write(0,new byte[16384]);
                task.Write(0x10,new byte[8]);
                task.Write(0x90,new byte[8]);task.Write(0x810,new byte[8]);task.Write(0x890,BitConverter.GetBytes(1L));
                task.Write(0x800,BitConverter.GetBytes((long)ui.Address+0x108));
                ui.Write(0x130,BitConverter.GetBytes(img+0x782FA8));
                ui.Write(0x128,[1,0]);
                ui.Write(0x150,BitConverter.GetBytes(img+0xD407B8));
                ui.Write(0xC90,BitConverter.GetBytes(img+0x237CD0));
                ui.Write(0x30,BitConverter.GetBytes(img+0x2366C0));
                ui.Write(0x190,BitConverter.GetBytes(img+0x237494));
                for(int i=0;i<6;i++)native.Write(0x782FA8+i*2,BitConverter.GetBytes((ushort)0x1024));
                native.Write(0xD40948,BitConverter.GetBytes(1));
                for(int i=0;i<steps.Length;i++)script.Write(0x20+i*4,BitConverter.GetBytes(steps[i]));
                list.Write(0,new byte[0x388]);list.Write(0,[0x70,0x71,0x7D,0x71,0xFF,0xFF]);
                ui.Write(0x158,[0,0]);native.Write(0xD407B8,[0,0]);native.Write(0x2FD35A4,[0,6]);
            }
            // The callsite adapter must preserve the native R11 window pointer
            // Hard-veto a zero-stock click before result/task writes, even if
            // the displayed/input flag was stale and incorrectly permitted it.
            foreach(ushort item in Enumerable.Range(240,13).Select(i=>(ushort)i))
            {
                Prepare(0);
                long childScope=scopes.Address+NativePotionWindowLifecycle.RecordSize;
                NativeLifetimeMemory.WriteProtected(childScope,new byte[NativePotionWindowLifecycle.RecordSize]);
                NativeLifetimeMemory.WriteProtected(childScope,BitConverter.GetBytes(2));
                NativeLifetimeMemory.WriteProtected(childScope+8,BitConverter.GetBytes((long)task.Address));
                NativeLifetimeMemory.WriteProtected(childScope+16,BitConverter.GetBytes((long)ui.Address));
                NativeLifetimeMemory.WriteProtected(childScope+48,BitConverter.GetBytes((long)ui.Address+0x108));
                list.Write(0,BitConverter.GetBytes((ushort)(0x3800+item)));
                native.Write(0x782FA8,BitConverter.GetBytes((ushort)65535)); // intentionally stale-enabled row
                native.Write(0x11A7C00+item,[0]);native.Write(0xD407B8,BitConverter.GetBytes((ushort)7));
                native.Write(0xD40AA0+0x810,new byte[8]);
                for(int click=0;click<32;click++)
                {
                    Call(0x22BA70,(long)ui.Address+0x108,0);
                    Check(BitConverter.ToUInt16(native.Read(0xD407B8,2))==7&&BitConverter.ToInt64(native.Read(0xD40AA0+0x810,8))==0,
                        "Zero-stock confirmation changed result or closed child");
                    Check(BitConverter.ToInt64(task.Read(0,8))==0&&native.Read(0x11A7C00+item,1)[0]==0,
                        "Zero-stock confirmation started a window or mutated stock");
                }
                native.Write(0x11A7C00+item,[1]);Call(0x22BA70,(long)ui.Address+0x108,0);
                Check(BitConverter.ToUInt16(native.Read(0xD407B8,2))==0&&BitConverter.ToInt64(native.Read(0xD40AA0+0x810,8))==1,
                    "Stocked child no longer uses native confirm");
                Prepare(0);native.Write(0x11A7C00+item,[0]);
                NativeLifetimeMemory.WriteProtected(childScope,BitConverter.GetBytes(1));
                native.Write(0xD40AA0+0x810,new byte[8]);
                native.Write(0x782FA8,BitConverter.GetBytes((ushort)65535));Call(0x22BA70,(long)ui.Address+0x108,0);
                Check(BitConverter.ToUInt16(native.Read(0xD407B8,2))==0&&BitConverter.ToInt64(native.Read(0xD40AA0+0x810,8))==1,
                    "Guard blocked parent/unrelated confirmation");
            }
            Console.WriteLine("Fixture: hard zero-stock confirm veto through actual multiwindow_keyright for all13 medicines and416 repeated clicks, stale-enabled flags, unchanged result/task/stock, stocked and parent fallback; no error/target/list-close startup. Hover/raster remain native gameplay checks.");
            // The callsite adapter must preserve the native R11 window pointer
            // and ignore every other window/owner. Execute its original native
            // task_create fallback, not a managed substitute.
            foreach(int mode in Enumerable.Range(0,8))
            {
                Prepare(0);
                long scope=scopes.Address+NativePotionWindowLifecycle.RecordSize;
                NativeLifetimeMemory.WriteProtected(scope,new byte[NativePotionWindowLifecycle.RecordSize]);
                NativeLifetimeMemory.WriteProtected(scope,BitConverter.GetBytes(mode==1?0:1));
                NativeLifetimeMemory.WriteProtected(scope+8,BitConverter.GetBytes((long)task.Address+(mode==2?8:0)));
                NativeLifetimeMemory.WriteProtected(scope+16,BitConverter.GetBytes((long)ui.Address+(mode==3?8:0)));
                NativeLifetimeMemory.WriteProtected(scope+48,BitConverter.GetBytes((long)ui.Address+(mode==5?0x100:0x108)));
                if(mode==6)list.Write(0,[0x7D,0x71]);
                if(mode==4)native.Write(0x782FA8,[4,0]);
                if(mode==7)native.Write(0x782FA8,[0,0]);
                long targetWindow=(long)ui.Address+(mode==4?0x58*4:mode==7?0:0xC60);
                Call(0x22BA70,(long)ui.Address+0x108,0);
                bool deferred=mode is 0 or 7;
                Check(BitConverter.ToInt64(task.Read(0,8))==targetWindow,"Potion adapter changed live R11 window pointer");
                Check(BitConverter.ToInt64(task.Read(0x90,8))==(deferred?0:1),"Potion target deferral intercepted unrelated owner/window/action: "+mode);
                Check(BitConverter.ToInt64(task.Read(0x810,8))==(deferred?1:0),"Potion deferral did not request native list completion or changed unrelated task");
                Check(W(CheckedNativeRead.Read(scope+4,2))==(deferred?1:0),"Potion deferred state differs from native callsite guard");
                native.Write(0x184A9B0,BitConverter.GetBytes((long)list.Address));
                NativeLifetimeMemory.WriteProtected(scope,new byte[NativePotionWindowLifecycle.RecordSize]);
            }
            void Run()
            {
                Marshal.GetDelegateForFunctionPointer<Eight>(start.Address)();
                Check(BitConverter.ToInt32(script.Read(12,4))==0,"Target navigation activated during potion menu");
                Check(BitConverter.ToInt64(task.Read(0x890,8))==1,"Native list task ended before final potion/cancel choice");
                Check(BitConverter.ToInt64(task.Read(0x90,8))==0,"Native validation still active after outer menu returned");
                Check(CheckedNativeRead.Read(scopes.Address,NativePotionWindowLifecycle.Slots*NativePotionWindowLifecycle.RecordSize).All(b=>b==0),"Potion scope leaked after confirm/cancel");
                KillFromList();
                Check(BitConverter.ToInt64(task.Read(0x890,8))==0,"Menu owner cannot end after final potion/cancel choice");
            }
            foreach(ushort ability in ChemistPotionFamily.Abilities)
            {
                foreach(ushort a in ChemistPotionFamily.Abilities)native.Write(0x11A7C00+ChemistPotionFamily.Item(a),[3]);
                int choice=Array.IndexOf(ChemistPotionFamily.Abilities,ability);Prepare(0,(uint)choice);Run();
                Check(BitConverter.ToInt32(script.Read(0,4))==2,"Potion did not open exactly one child window");
                Check(BitConverter.ToInt32(script.Read(16,4))==1,"Final potion did not await exactly one native validation window");
                Check(BitConverter.ToInt32(script.Read(8,4))==0,"Previous window completion request closed child immediately");
                Check(W(list.Read(0,2))==0x7000+ability&&W(list.Read(2,2))==0x717D,"Selected full ID did not return to original native controller");
                for(int i=0;i<4;i++)
                {
                    Check(W(script.Read(0x500+i*2,2))==0x3800+ChemistPotionFamily.Item(ChemistPotionFamily.Abilities[i]),"Wrong native inventory child identity");
                    Check(W(script.Read(0x500+0xA4+i*2,2))==3,"Child stock column differs");
                }
                Check(W(script.Read(0x508,2))==65535,"Child potion list lacks terminator");
                for(int i=0;i<4;i++)Check(W(script.Read(0x898+i*2,2))==65535,"Stocked child input started native target navigation");
                Check(ChemistPotionFamily.Abilities.All(a=>native.Read(0x11A7C00+ChemistPotionFamily.Item(a),1)[0]==3),"Opening/selecting child consumed stock");
                byte[] request=[..BitConverter.GetBytes(ability),6,0];packet.Write(0,request);Call(0x320504,(long)packet.Address);
                Check(W(native.Read(0x18716A6,2))==ChemistPotionFamily.Item(ability),"Selected native medicine identity lost in Tmp_UA");
                byte[] reaction=new byte[20];reaction[1]=6;BinaryPrimitives.WriteUInt16LittleEndian(reaction.AsSpan(2),ability);
                packet.Write(0,reaction);
                Check(Call(0x27FE20,(long)packet.Address)==0,"Potion variant range route rejected");
                native.Write(0x186AF80,new byte[4]);native.Write(0x1853CE0+0x1EE,[0]);
                Check(Call(0x281488,(long)packet.Address,(long)packet.Address+128,0)==1,"Variant preview refused");
                Check(native.Read(0x11A7C00+ChemistPotionFamily.Item(ability),1)[0]==3,"Variant preview consumed stock");
                Check(Call(0x281488,(long)packet.Address,(long)packet.Address+128,1)==1,"Variant commitment refused");
                Check(W(packet.Read(136,2))==ChemistPotionFamily.Item(ability),"Committed the wrong potion item");
                Check(native.Read(0x11A7C00+ChemistPotionFamily.Item(ability),1)[0]==2 &&
                    ChemistPotionFamily.Abilities.Where(a=>a!=ability).All(a=>native.Read(0x11A7C00+ChemistPotionFamily.Item(a),1)[0]==3),"Variant consumed other stocks");
                native.Write(0x11A7C00+ChemistPotionFamily.Item(ability),[0]);
                Check((int)Call(0x281488,(long)packet.Address,(long)packet.Address+128,1)==-1,"Empty variant committed");
            }
            foreach(ushort a in ChemistPotionFamily.Abilities)native.Write(0x11A7C00+ChemistPotionFamily.Item(a),[0]);
            native.Write(0x11A7C00+242,[4]);Prepare(0,2);Run();
            Check(W(script.Read(0x504,2))==0x38F2&&W(script.Read(0x508,2))==65535,"Sparse inventory changed the four visible potion rows");
            for(int i=0;i<4;i++)Check(W(script.Read(0x888+i*2,2))==(i==2?0:4),"Empty potion row not natively blocked");
            Check(W(list.Read(0,2))==0x7172,"Only X-Potion choice lost");
            Prepare(0,0,1,3,2);Run();
            Check(BitConverter.ToInt32(script.Read(0,4))==5&&W(list.Read(0,2))==0x7172,"An empty Potion/Hi-Potion/Elixir escaped the submenu");
            Check(native.Read(0x11A7C00+242,1)[0]==4,"Rejected choice consumed available potion");
            foreach(int mask in Enumerable.Range(0,16))
            {
                for(int i=0;i<4;i++)native.Write(0x11A7C00+ChemistPotionFamily.Item(ChemistPotionFamily.Abilities[i]),[(byte)((mask&(1<<i))!=0?i+1:0)]);
                if(mask==0)Prepare(0,0xFFFF0000,0xFFFF0000);
                else Prepare(0,(uint)Enumerable.Range(0,4).First(i=>(mask&(1<<i))!=0));
                Run();
                for(int i=0;i<4;i++)
                {
                    Check(W(script.Read(0x500+i*2,2))==0x3800+ChemistPotionFamily.Item(ChemistPotionFamily.Abilities[i]),"Inventory presentation identity moved with stock");
                    Check(W(script.Read(0x5A4+i*2,2))==((mask&(1<<i))!=0?i+1:0),"Visible stock differs");
                    Check(W(script.Read(0x888+i*2,2))==((mask&(1<<i))!=0?0:4),"Potion disabled flag differs from stock");
                    Check(W(script.Read(0x898+i*2,2))==((mask&(1<<i))!=0?65535:4),"Actual native input table differs from stock");
                }
            }
            foreach(ushort a in ChemistPotionFamily.Abilities)native.Write(0x11A7C00+ChemistPotionFamily.Item(a),[0]);
            native.Write(0x11A7C00+242,[4]);
            using var par=new OwnedNativeMemory(4096);
            Check(Call(0x30E368,0,6,(long)packet.Address,(long)par.Address,(long)par.Address+64,0,(long)par.Address+128,(long)par.Address+192)>0&&W(packet.Read(0,2))==368,"Potion group hidden when base Potion empty but X-Potion stocked");
            Prepare(0,0xFFFF0000,1);Run();
            Check(BitConverter.ToInt32(script.Read(0,4))==3&&W(ui.Read(0x158,2))==1&&W(list.Read(0,2))==0x7170,"Child cancel did not restore parent/other selection");
            Prepare(0,0xFFFF0000,0xFFFF0000);Run();
            Check(BitConverter.ToInt32(script.Read(0,4))==3&&W(native.Read(0xD407B8,2))==65535,"Parent cancel did not reach action controller");
            Check(BitConverter.ToInt32(script.Read(8,4))==0,"Cancel/back retained an old native task completion request");
            for(int i=0;i<32;i++){Prepare(0,0xFFFF0000,0xFFFF0000);Run();}
            Prepare(0xFFFF0000);Run();
            native.Write(0x1853CE0+0xA5,[0,0,0]);Prepare(0,0);Run();
            Check(BitConverter.ToInt32(script.Read(0,4))==1,"Unlearned Potion opened child");
            Check(native.Read(0x11A7C00+242,1)[0]==4,"Cancel/unlearned paths consumed stock");
            // Both added families use the SAME real native modal/target flow.
            // Seven Remedy rows use private input storage, never the parent's
            // six-window table. Inventory masks cover every blocked/stocked row.
            native.Write(0x1853CE0+0xA5,[0x90,0x08,0]);
            // Real parent row2 Remedy -> cancel child row3 -> parent row1
            // Ether -> choose Hi-Ether. Unlike old family tests, the group
            // opener is NOT always at parent row0. Parent cursor must be
            // restored before its native input is reopened and before target.
            Prepare(2,0xFFFF0003,1,1);
            list.Write(0,[0x70,0x71,0x73,0x71,0x7C,0x71,0x7D,0x71,0xFF,0xFF]);
            native.Write(0x11A7C00+244,[3]);native.Write(0x11A7C00+245,[3]);Run();
            Check(BitConverter.ToInt32(script.Read(0,4))==4&&W(script.Read(0x900+0x3A8,2))==2,
                "Back restored child cursor instead of original Items category");
            Check(W(ui.Read(0x158,2))==1&&W(list.Read(2,2))==0x7174&&W(list.Read(4,2))==0x717C,
                "Family switching lost restored parent selection or committed the wrong row");
            Console.WriteLine("Fixture: video047 parent cursor restoration: actual player modal/Back/target bridge preserves nonzero Remedy row2 after child cancel, switches to Ether row1, commits Hi-Ether into correct parent row and restores cursor before targeting; explicit native input scene fixture.");
            foreach(int group in new[]{1,2})
            {
                ushort[] family=ChemistMedicineFamilies.ForSlot(group);
                void PrepareFamily(params uint[] steps)
                {
                    Prepare(steps);list.Write(0,BitConverter.GetBytes((ushort)(0x7000+ChemistActionBindings.TestActions[group].AbilityId)));
                }
                foreach(ushort ability in family)
                {
                    foreach(ushort a in family)native.Write(0x11A7C00+ChemistMedicineFamilies.Item(a),[3]);
                    PrepareFamily(0,(uint)Array.IndexOf(family,ability));Run();
                    Check(BitConverter.ToInt32(script.Read(0,4))==2&&BitConverter.ToInt32(script.Read(16,4))==1,"Medicine family shares input with target or misses validation");
                    Check(W(list.Read(0,2))==0x7000+ability&&W(script.Read(0x500+family.Length*2,2))==65535,"Family choice/terminator identity lost");
                    for(int i=0;i<family.Length;i++)
                    {
                        Check(W(script.Read(0x500+i*2,2))==0x3800+ChemistMedicineFamilies.Item(family[i]),"Medicine child item ordering differs");
                        Check(W(script.Read(0x898+i*2,2))==65535,"Stocked family child did not close natively");
                    }
                    Check(BitConverter.ToInt64(ui.Read(0x130,8))==img+0x782FA8,"Private seven-row input pointer escaped into parent");
                    Check(native.Read(0x782FA8,12).SequenceEqual(Enumerable.Range(0,6).SelectMany(_=>BitConverter.GetBytes((ushort)0x1024)).ToArray()),"Medicine overwrote parent keyright storage");
                    packet.Write(0,[..BitConverter.GetBytes(ability),6,0]);Call(0x320504,(long)packet.Address);
                    ushort item=ChemistMedicineFamilies.Item(ability);
                    Check(W(native.Read(0x18716A6,2))==item,"Medicine selection aliases parent item");
                    byte status=native.Read((int)(Call(0x2B8E04,item)-img)+2,1)[0];
                    Check(native.Read(0x18716A8,5).SequenceEqual(native.Read(0x80FBA0+status*6+1,5)),"Remedy variant did not select its own native status mask");
                    byte[] reaction=new byte[20];reaction[1]=6;BinaryPrimitives.WriteUInt16LittleEndian(reaction.AsSpan(2),ability);packet.Write(0,reaction);
                    Check(Call(0x27FE20,(long)packet.Address)==0,"Medicine variant rejected by target route");
                    native.Write(0x186AF80,new byte[4]);native.Write(0x1853CE0+0x1EE,[0]);
                    Check(Call(0x281488,(long)packet.Address,(long)packet.Address+128,0)==1&&native.Read(0x11A7C00+item,1)[0]==3,"Medicine preview consumed");
                    Check(Call(0x281488,(long)packet.Address,(long)packet.Address+128,1)==1&&W(packet.Read(136,2))==item,"Medicine commitment lost selected item");
                    Check(native.Read(0x11A7C00+item,1)[0]==2&&family.Where(a=>a!=ability).All(a=>native.Read(0x11A7C00+ChemistMedicineFamilies.Item(a),1)[0]==3),"Medicine consumed the wrong stack");
                    native.Write(0x11A7C00+item,[0]);
                    Check((int)Call(0x281488,(long)packet.Address,(long)packet.Address+128,1)==-1,"Empty medicine committed");
                }
                for(int mask=0;mask<(1<<family.Length);mask++)
                {
                    for(int i=0;i<family.Length;i++)native.Write(0x11A7C00+ChemistMedicineFamilies.Item(family[i]),[(byte)((mask&(1<<i))!=0?i+1:0)]);
                    PrepareFamily(mask==0?[0,0xFFFF0000,0xFFFF0000]:[0,(uint)Enumerable.Range(0,family.Length).First(i=>(mask&(1<<i))!=0)]);Run();
                    for(int i=0;i<family.Length;i++)Check(W(script.Read(0x898+i*2,2))==((mask&(1<<i))!=0?65535:4),"Medicine stock disabled table mismatch");
                    using var args=new OwnedNativeMemory(4096);
                    int count=(int)Call(0x30E368,0,6,(long)packet.Address,(long)args.Address,(long)args.Address+64,0,(long)args.Address+128,(long)args.Address+192);
                    Check(Enumerable.Range(0,count).Any(i=>W(packet.Read(i*2,2))==ChemistActionBindings.TestActions[group].AbilityId)==(mask!=0),"Group availability ignores its family stocks");
                }
                foreach(ushort a in family)native.Write(0x11A7C00+ChemistMedicineFamilies.Item(a),[0]);
                native.Write(0x11A7C00+ChemistMedicineFamilies.Item(family[^1]),[4]);
                PrepareFamily([0,..Enumerable.Range(0,family.Length-1).Select(i=>(uint)i),(uint)(family.Length-1)]);Run();
                Check(W(list.Read(0,2))==0x7000+family[^1],"Empty confirmations escaped medicine child");
                for(int i=0;i<16;i++){PrepareFamily(0,0xFFFF0000,0xFFFF0000);Run();}
                native.Write(0x1853CE0+0xA5,[0,0,0]);PrepareFamily(0,0);Run();
                Check(BitConverter.ToInt32(script.Read(0,4))==1,"Unlearned medicine group opened child");
                native.Write(0x1853CE0+0xA5,[0x90,0x08,0]);
            }
            Console.WriteLine("Fixture: Ether and Remedy native modal submenu, nine item identities, MP/status selection, target/validation deferral, all132 stock masks, exact selected consumption, empty rejection, learned groups and32 cancel cycles; private seven-row input table restored without parent overwrite. Rendering/scene scheduling remain gameplay checks.");
            Console.WriteLine("Fixture: Potion native player CALL237098 opens stocked Potion/Hi-Potion/X-Potion/Elixir child; all four full IDs, sparse inventories, learned group, preview, exact selected consumption and cancel/back restore verified. Window drawing/input are explicit native fixtures; gameplay confirmation required.");
            Console.WriteLine("Fixture: real list CALL231C65/task_killmyself reproduced premature target-owner release; modal scope retains +90 through both menus, final cleanup resumes native termination, 32 cancel/back cycles and unrelated callers/tasks preserved. Tutorial/fiber dispatch and input/raster remain explicit fixtures.");
            Console.WriteLine("Fixture: actual CALL22BB5B/task_create exposes target startup before list termination; Potion defers startup until final stocked choice, cancel discards it, other actions create normally. All16 stock combinations retain four visible rows with native disabled flag4, empty confirmation stays in child and no stock changes. Input/raster/scheduling remain explicit fixtures.");
            Console.WriteLine("Fixture: real Enhanced 1024/window36 validation via multiwindow_keyright; live descriptor input flags, negative child completion, scoped/legacy/error/ordinary guards, R11 preservation and native validation wait verified. Scene input availability, rendering and validation completion are explicit fixtures.");
        }
        finally{foreach(var p in saved)NativeLifetimeMemory.WriteProtected(p.Key,p.Value);}
    }
}

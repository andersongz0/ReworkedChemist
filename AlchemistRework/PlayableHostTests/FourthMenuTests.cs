using System.Runtime.InteropServices;
using FFTModLoader.ContentExpansion.Runtime;

unsafe partial class Program
{
    static void FourthMenuTests(OwnedNativeMemory native)
    {
        long img=(long)native.Address;
        byte[] providerBefore=native.Read(0x3CD9DA8,8),coreBefore=native.Read(0x3CD9DC0,8);
        byte[] transformBefore=native.Read(0x3F0270,1),matrixBefore=native.Read(0x3D82F0,1);
        byte[] referenceDrawBefore=native.Read(0x3EDB20,26),componentTransformBefore=native.Read(0x40A914,1);
        int[] updateStubs=[0x3EF834,0x3EF860,0x3F9938,0x409774,0x409ECC,0x3DC22C,
            0x3D87BC,0x4091AC,0x0F5828,0x0F58A4,0x40C750,0x40CA50,0x40C7D8];
        byte[][] updateBefore=updateStubs.Select(t=>native.Read(t,16)).ToArray();
        using var scene=new OwnedNativeMemory(131072);
        using var names=new OwnedNativeMemory(4096);
        using var render=new OwnedNativeMemory(0xA0000);
        using var context=new OwnedNativeMemory(4096);
        long s=(long)scene.Address,provider=s,manager=s+0x200,battle=s+0x400,owner=s+0x600,
            resource=s+0x2200,root=s+0x2600,parent=s+0x3000,reference=s+0x4000,menu=s+0x4400,
            child=s+0x4800,component=s+0x5000,list=s+0x6000,board=s+0x7000,title=s+0x7800,
            vector=s+0x8000,vtable=s+0x8100,core=s+0x9000,character=s+0xA000,layer=s+0xB000;
        long n=(long)names.Address;
        void Put(long p,long v)=>NativeLifetimeMemory.WriteProtected(p,BitConverter.GetBytes(v));
        void I(long p,int v)=>NativeLifetimeMemory.WriteProtected(p,BitConverter.GetBytes(v));
        long Ptr(long p)=>BitConverter.ToInt64(CheckedNativeRead.Read(p,8));
        int Int(long p)=>BitConverter.ToInt32(CheckedNativeRead.Read(p,4));
        byte B(long p)=>CheckedNativeRead.Read(p,1)[0];
        ushort G(long p)=>BitConverter.ToUInt16(CheckedNativeRead.Read(p+0x228,2));
        void SetGroup(long p,ushort group)=>NativeLifetimeMemory.WriteProtected(p+0x228,BitConverter.GetBytes(group));
        void FinalBatchTests()
        {
            long r=(long)render.Address,ctx=(long)context.Address;
            // Real final3E75DC reordering: child/background cloned group/seq0
            // sorts before parent's text/seq1, despite being traversed last.
            var finalize=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x3E75DC);
            void Reset(bool isolated)
            {
                I(r+0x94178,3);
                for(int i=0;i<3;i++)
                {
                    I(r+0xA360+i*0x200,0);I(r+0xA368+i*0x200,0); // no bounds/effects
                    I(r+0xA370+i*0x200,((i==2&&isolated?G(component):27)<<16)+(i==1?1:0));
                    I(r+0x4A184+i*0x250,i);
                }
            }
            Reset(false);finalize(r,1);
            Check(Int(r+0x4A184+2*0x250)<Int(r+0x4A184+0x250),"Native final batch control did not reproduce child behind parent text");
            Reset(true);for(int i=0;i<3;i++)finalize(r,i);
            Check(Int(r+0x4A184+2*0x250)>Int(r+0x4A184+0x250),"Isolated child group still reordered behind Items");
            // Real3E9298 batch selection; only state comparator and downstream
            // allocation/material setup are native stubs, not the search.
            int[] targets=[0x3E7ADC,0x3E9218,0x3E6728];
            byte[][] saved=targets.Select(t=>native.Read(t,16)).ToArray();
            try
            {
                NativeLifetimeMemory.WriteProtected(img+targets[0],[0xB8,1,0,0,0,0xC3]);
                NativeLifetimeMemory.WriteProtected(img+targets[1],[0x89,0x91,0x7C,0x41,9,0,0x31,0xC0,0xC3]);
                NativeLifetimeMemory.WriteProtected(img+targets[2],[0xC3]);
                var select=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x3E9298);
                I(r+0x94178,1);I(r+0xA370,27<<16);
                I(ctx+0x224,27<<16);select(r,ctx);
                Check(Int(r+0x9417C)==0,"Native reuse control did not alias parent batch");
                I(ctx+0x224,G(component)<<16);select(r,ctx);
                Check(Int(r+0x9417C)==-1,"Fresh child render group reused original Items batch");
                I(r+0xA370,0);I(ctx+0x224,0);select(r,ctx);
                Check(Int(r+0x9417C)==-1,"Actual parent-zero batch incorrectly reused an existing batch");
            }
            finally{for(int i=0;i<targets.Length;i++)NativeLifetimeMemory.WriteProtected(img+targets[i],saved[i]);}
        }
        void NativeReentryFrames(NativeMedicineFourthMenu menus)
        {
            using var frameScene=new OwnedNativeMemory(4096);
            long f=(long)frameScene.Address,timeline=f+0x100,tracks=f+0x200,track=f+0x300,vt=f+0x400;
            int[] targets=[0x3F9938,0x3F2EF8,0x4095B8,0x409774,0x4000D8,0x4007FC,0x4015C0,0x41FAE0];
            byte[][] saved=targets.Select(t=>native.Read(t,32)).ToArray();
            long oldVtable=Ptr(parent),oldBegin=Ptr(parent+0x268),oldEnd=Ptr(parent+0x270);
            byte[] oldState=CheckedNativeRead.Read(parent+0xBC,8),oldCounter=CheckedNativeRead.Read(parent+0x300,4),oldGate=CheckedNativeRead.Read(reference+0x60,4);
            try
            {
                // Execute the real unforced reference update gate, native Show/
                // Hide/seek and property-timeline traversal. Only name resolution,
                // transforms, notifications, clock and one alpha property are
                // fixtures; no game scene, raster or GPU is executed.
                NativeLifetimeMemory.WriteProtected(img+0x3F9938,[0x48,0xB8,..BitConverter.GetBytes(parent),0xC3]);
                NativeLifetimeMemory.WriteProtected(img+0x409774,[0x48,0xB8,..BitConverter.GetBytes(timeline),0xC3]);
                foreach(int rva in new[]{0x3F2EF8,0x4095B8,0x4000D8,0x41FAE0})NativeLifetimeMemory.WriteProtected(img+rva,[0xC3]);
                NativeLifetimeMemory.WriteProtected(img+0x4007FC,[0xB8,1,0,0,0,0xC3]);
                // Representative component-alpha property dispatch: copy track
                // value, record application. The dispatch traversal is real.
                NativeLifetimeMemory.WriteProtected(img+0x4015C0,
                    [0x8B,0x41,0x40,0x41,0x89,0x80,0xBC,0,0,0,0x41,0xFF,0x80,0,3,0,0,0xC3]);
                Put(parent,vt);Put(vt+0x20,img+0x409C28);
                Put(parent+0x268,f);Put(parent+0x270,f+8);Put(f,timeline);
                Put(timeline+0x90,tracks);Put(timeline+0x98,tracks+8);Put(tracks,track);I(timeline+0x58,10);
                I(reference+0x60,1);I(parent+0x300,0);
                var frame=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x4092EC);
                var show=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x408EBC);
                var hide=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x408FF8);
                var seek=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x409824);
                void Queue(bool visible)
                {
                    I(track+0x40,visible?0x3F800000:0);
                    if(visible)show(parent,1);else hide(parent,1);
                    seek(parent,0,-1);
                    Check(B(timeline+0x68)==1&&Int(timeline+0x50)==BitConverter.SingleToInt32Bits(10f),"Native seek did not queue its end frame");
                }
                void Frame()=>frame(root,0,reference,0); // never force bypass
                // Negative control: the old persistent +90=0 gate survives
                // actual Show/seek, so an enabled timeline never gets applied.
                I(parent+0xBC,0);NativeLifetimeMemory.WriteProtected(reference+0x90,[0]);
                NativeLifetimeMemory.WriteProtected(parent+0x22A,[0]);Queue(true);Frame();
                Check(Int(parent+0xBC)==0&&Int(parent+0x300)==0&&B(parent+0x22A)==1,"Old hard-disable control did not reproduce invisible native reopen");
                NativeLifetimeMemory.WriteProtected(reference+0x90,[1]);
                for(int group=0;group<3;group++)for(int round=0;round<64;round++)
                {
                    var childScope=menus.Begin(group);Check(childScope!=null,"Native reentry production scope absent");childScope!.Dispose();
                    Check(B(reference+0x90)==1&&B(parent+0x1A0)==1,"Production closure permanently disabled native updates");
                    I(parent+0xBC,0x3F800000);Queue(false);
                    Check(Int(parent+0xBC)==0x3F800000,"Seek incorrectly applied Hide synchronously");
                    Frame();Check(Int(parent+0xBC)==0&&B(parent+0x22A)==0,"Deferred Hide did not close Items at target selection");
                    // Native menu reentry after target cancel, then a second
                    // normal opening representing exit/reentry or next turn.
                    for(int reopen=0;reopen<2;reopen++)
                    {
                        Queue(true);Check(Int(parent+0xBC)==0,"Seek incorrectly applied Show synchronously");
                        Frame();Check(Int(parent+0xBC)==0x3F800000&&B(parent+0x22A)==1,"Normal native Show reopened invisible Items");
                        Queue(false);Frame();Check(Int(parent+0xBC)==0,"Normal native Hide leaked Items into target/next turn");
                    }
                }
                Check(Int(parent+0x300)==960,"Unexpected native property application count");
                Console.WriteLine("Fixture: video048 native Items reentry: actual PE4092EC unforced reference gate reproduces old persistent-disable failure; actual PE408EBC/408FF8/409824 queue Show/Hide, actual PE409C28 applies deferred properties for192 production closures and384 normal reopenings across3 families. Property/name/transform/clock/subscriber fixtures; no live-game or GPU claim.");
            }
            finally
            {
                for(int i=0;i<targets.Length;i++)NativeLifetimeMemory.WriteProtected(img+targets[i],saved[i]);
                Put(parent,oldVtable);Put(parent+0x268,oldBegin);Put(parent+0x270,oldEnd);
                NativeLifetimeMemory.WriteProtected(parent+0xBC,oldState);NativeLifetimeMemory.WriteProtected(parent+0x300,oldCounter);
                NativeLifetimeMemory.WriteProtected(reference+0x60,oldGate);
            }
        }
        bool Attached(long sought)
        {
            long node=Ptr(layer+0xD0);
            for(int i=0;node!=0&&i<8;i++,node=Ptr(node+0x20))if(node==sought)return true;
            return false;
        }
        void NativeNestedCloseGate(NativeMedicineFourthMenu menus)
        {
            long nested=s+0xC000,refs=s+0xC800;
            byte[] resolver=native.Read(0x3F9938,16);
            long oldBegin=Ptr(resource+0xA0),oldEnd=Ptr(resource+0xA8),oldResource=Ptr(list+0x1F0);
            try
            {
                Node(nested,12,0,440);I(nested+0x30,Int(parent+8));Put(refs,nested);
                Put(resource+0xA0,refs);Put(resource+0xA8,refs+8);Put(list+0x1F0,resource);
                NativeLifetimeMemory.WriteProtected(img+0x3F9938,[0x48,0xB8,..BitConverter.GetBytes(list),0xC3]);
                var hide=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x408FF8);
                var reopen=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x156964);
                NativeLifetimeMemory.WriteProtected(list+0x22A,[1]);hide(parent,1);
                Check(B(list+0x22A)==0,"Old recursive close did not disable the native nested list");
                reopen(owner,-1);
                Check(B(list+0x22A)==0,"Native target-cancel root Show unexpectedly repaired the disabled ListAction gate");
                for(int group=0;group<3;group++)for(int round=0;round<16;round++)
                {
                    var scope=menus.Begin(group);Check(scope!=null&&B(list+0x22A)==1,"Child parent Show did not enable nested list");
                    scope!.Dispose();
                    Check(B(list+0x22A)==1,"Production close disabled nested native selection");
                    reopen(owner,-1); // same root-only Show used by native reentry
                    Check(B(list+0x22A)==1&&B(parent+0x1A0)==1,"Target cancel reopened an inert native list");
                    using(var returned=scope.ReopenItems())Check(returned!=null&&B(list+0x22A)==1,"Back parent input lost native nested shown-state");
                    Check(B(list+0x22A)==1&&Int(owner+0x15E8)==-1,"Second Back left list inactive or stale-selected");
                }
                Console.WriteLine("Fixture: video049 nested Items selection gate: actual PE408FF8 recursive close disables ListAction+22A; actual PE156964 root-only Show cannot repair it. Production root-only balance preserves nested shown-state across48 choose/target-cancel/Back cycles, with native PE1568EC cleanup. Resource/name/row/GPU leaves remain fixtures.");
            }
            finally
            {
                NativeLifetimeMemory.WriteProtected(img+0x3F9938,resolver);
                Put(resource+0xA0,oldBegin);Put(resource+0xA8,oldEnd);Put(list+0x1F0,oldResource);
            }
        }
        void SortUpdateReferences()
        {
            // Actual native4092EC recursively sorts only dirty sibling layers.
            // Alpha/parent-state helpers and component resolution are stubs;
            // the linked-list comparator, dirty gating and recursion are real.
            Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x4092EC)(root,0,root+0x110,1);
        }
        long[] RenderReferences()
        {
            // Actual PE3E9A6C traversal, only the primitive/GPU dispatcher is
            // replaced with a native recorder (R8 reference, derived key).
            I((long)render.Address,0);
            Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x3E9A6C)
                ((long)render.Address,(long)context.Address,root+0x110);
            int count=Int((long)render.Address);Check(count==3,"Unexpected native reference render count");
            return Enumerable.Range(0,count).Select(i=>Ptr((long)render.Address+8+i*8)).ToArray();
        }
        void Node(long p,int type,int x,int width)
        {
            I(p+0x34,type);I(p+0x38,x);I(p+0x3C,687);I(p+0x40,width);I(p+0x44,330);I(p+0x5C,8);
            I(p+0xC8,8);I(p+0x64,-1);I(p+0x68,0x646464);I(p+0x6C,0x3F800000);I(p+0xC4,-1);
            NativeLifetimeMemory.WriteProtected(p+0x90,[1]);NativeLifetimeMemory.WriteProtected(p+0x99,[1]);
            NativeLifetimeMemory.WriteProtected(p+0xCC,[1]);
        }
        foreach(var (offset,text) in new[]{(0,"ActionList"),(16,"ActionMenu"),(32,"ChemistMedicineChild"),(64,"ListAction"),(80,"TextTitle"),(112,"Potions"),(128,"Ethers"),(144,"Remedys"),(160,"Show"),(176,"Hide")})
            names.Write(offset,System.Text.Encoding.UTF8.GetBytes(text+"\0"));
        for(int i=0;i<6;i++)names.Write(192+i*16,System.Text.Encoding.UTF8.GetBytes($"BattleMenu0{i+1}\0"));
        Put(img+0x3CD9DA8,provider);Put(provider+0x10,manager);Put(manager+0x40,battle);Put(battle+0x48,owner);
        Put(owner+0x18,resource);Put(owner+0x48,parent);Put(owner+0x1590,s+0x8500);I(owner+8,77);
        Put(img+0x3CD9DC0,core);I(core+0xADDC,11);
        Node(root+0x110,1,0,1920);Node(reference,12,1412,440);Node(menu,12,1150,704);
        I(root+0x1D8,0); // root's accumulated priority at node(+110)+C8
        Node(layer,1,0,1920);I(layer+0x5C,0);I(layer+0xC8,0);
        I(reference+0x5C,0);I(reference+0xC8,0);I(menu+0x5C,0);I(menu+0xC8,0);
        Node(character,12,1020,440);
        Put(root+0x1E0,layer);Put(layer+0x18,root+0x110);Put(layer+0xD0,reference);
        Put(reference+0x18,layer);Put(menu+0x18,layer);Put(reference+0x20,menu);Put(menu+0x28,reference);
        Put(resource+0x68,vector);Put(resource+0x70,vector+8);Put(vector,parent);
        I(parent+8,11);I(parent+0x218,0);Put(parent+0x1F0,resource);NativeLifetimeMemory.WriteProtected(parent+0x1A0,[1]);
        SetGroup(parent,0); // captured real Items group, not old fixture27
        // Representative original rows/header/callbacks must never be edited.
        scene.Write(0x3300,Enumerable.Range(0,512).Select(i=>(byte)(i*17)).ToArray());
        byte[] parentData=CheckedNativeRead.Read(parent+0x300,512);
        int copies=0,initializations=0;string label="";bool alias=false,parentShow=false,hideCancelled=false,failShow=false;List<string> logs=[];
        long Invoke(int r,long a,long b,long c,long d)
        {
            switch(r)
            {
                case 0x3F9DA0:return root;
                case 0x3D87BC:Put(a,b);I(a+8,0);return a;
                case 0x3D87F4:Put(a,Ptr(b));I(a+8,0);return a;
                case 0x3D8D1C:return 0;
                case 0x3F0794:return Ptr(b)==n?reference:Ptr(b)==n+16?menu:Ptr(b)==n+192?character:Ptr(b)==n+32&&Attached(child)?child:0;
                case 0x3FAD88:
                    Check(a==resource&&b==reference&&c==0,"Incorrect native reference copy contract");
                    copies++;Node(child,12,1412,440);I(child+0x5C,0);I(child+0xC8,0);I(component+8,101);I(list+8,102);I(board+8,103);
                    I(component+0x218,0);I(list+0x218,9);I(board+0x218,13);Node(title,3,8,424);
                    Put(component+0x1F0,resource);Put(component,vtable);Put(list,vtable);Put(board,vtable);Put(vtable+0x18,img+0x123456);
                    Put(vector+8,component);Put(vector+16,list);Put(vector+24,board);Put(resource+0x70,vector+32);
                    // Actual408C4C copies nonzero render identities, not only GUI IDs.
                    SetGroup(component,0);SetGroup(list,27);SetGroup(board,0);
                    return child;
                case 0x3F9938:return alias?parent:component;
                case 0x123456:initializations++;return 0;
                case 0x4091AC:Check(a==component,"Rows resolved against parent Items");return Ptr(b)==n+64?list:Ptr(b)==n+80?board:0;
                case 0x40AAC0:Check(a==board,"Title board mismatch");return title;
                case 0x3F0F8C:Check(a==title,"Parent title overwritten");label=Marshal.PtrToStringUTF8((nint)b)!;return 0;
                case 0x409ECC:
                    Check(a==parent,"Timeline changed on unrelated component");
                    if(Ptr(b)==n+176)hideCancelled=true;
                    else {Check(Ptr(b)==n+160,"Invalid timeline CString");parentShow=false;}
                    return 0;
                case 0x408EBC:
                    Check(a==parent&&b==1&&hideCancelled,"Native enable ran before Hide cancellation");
                    return Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+r)(a,b,c,d);
                case 0x408FF8:
                    Check(a==parent&&b==0,"Native Hide recursively disabled original list/rows");
                    return Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+r)(a,b,c,d);
                case 0x1568EC:
                    Check(a==owner&&b==-1&&Ptr(owner+0x48)==parent,"Native list close did not own original Items");
                    return Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+r)(a,b,c,d);
                case 0x409824:
                    Check(a==parent&&c==-1,"Passive Items timeline not completed natively");
                    if(Ptr(b)==n+160){Check(hideCancelled,"Show did not cancel native Hide");parentShow=true;hideCancelled=false;if(failShow)throw new InvalidOperationException("Injected Show construction failure");}
                    else {Check(Ptr(b)==n+176,"Invalid Hide CString");parentShow=false;}
                    return 0;
                case 0x3F0654:
                case 0x3EF684:
                    return Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+r)(a,b,c,d);
                default:throw new InvalidOperationException("Unexpected fourth menu helper "+r.ToString("X"));
            }
        }
        try
        {
            // Execute actual PE 3EFF08 for dirty-gated accumulated priority.
            // Matrix composition is irrelevant to this regression and stubbed;
            // no imported APIs, PE entry point, game scene or GPU is executed.
            NativeLifetimeMemory.WriteProtected(img+0x3F0270,[0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x3D82F0,[0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x40A914,[0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x3EF834,[0xB8,1,0,0,0,0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x3EF860,[0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x3F9938,[0x31,0xC0,0xC3]);
            // Real408EBC/408FF8 transition+22A. Timeline lookup/reset and GUI
            // subscriber dispatch are explicit stubs (no scheduler or GPU).
            NativeLifetimeMemory.WriteProtected(img+0x409774,[0x31,0xC0,0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x409ECC,[0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x3DC22C,[0xC3]);
            // Real native addon closure1568EC ->156A84 ->1569C0. Hash and
            // ListAction resolution/validity use this owned scene. Cursor
            // rendering and row widgets are explicit leaf fixtures; actual
            // owner-selection reset and close dispatch remain native.
            NativeLifetimeMemory.WriteProtected(img+0x3D87BC,[0x48,0x89,0x11,0xC7,0x41,8,0,0,0,0,0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x4091AC,[0x48,0xB8,..BitConverter.GetBytes(list),0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x0F5828,[0xB8,1,0,0,0,0xC3]);
            foreach(int leaf in new[]{0x0F58A4,0x40C750,0x40CA50})NativeLifetimeMemory.WriteProtected(img+leaf,[0xC3]);
            NativeLifetimeMemory.WriteProtected(img+0x40C7D8,[0x89,0x91,0xA4,2,0,0,0xC3]);
            // mov eax,[rcx]; mov [rcx+rax*8+8],r8; mov edx,[rcx+94184];
            // mov [rcx+rax*4+48],edx; inc dword[rcx]; ret
            NativeLifetimeMemory.WriteProtected(img+0x3EDB20,
                [0x8B,0x01,0x4C,0x89,0x44,0xC1,0x08,0x8B,0x91,0x84,0x41,0x09,0x00,0x89,0x54,0x81,0x48,0xFF,0x01,0xC3]);
            var accumulate=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x3EFF08);
            var inherit=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x3EDCA4);
            var menus=new NativeMedicineFourthMenu(img,n,Invoke,logs.Add);
            string[] labels=["Potions","Ethers","Remedys"];
            // Negative control: the previous visual-only close leaves the
            // addon's last selected row/cursor alive across native Back.
            I(owner+0x15E8,3);I(list+0x2A4,3);
            Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x408FF8)(parent,1);
            Check(Int(owner+0x15E8)==3&&Int(list+0x2A4)==3,"Visual-only close control unexpectedly cleared native selection");
            for(int group=0;group<3;group++)for(int round=0;round<64;round++)
            {
                // Native Items is still visible while its Hide is in flight
                // when the child begins. A captured visible=1 is not an open
                // menu to restore after the child finishes (video046).
                NativeLifetimeMemory.WriteProtected(reference+0x90,[1]);
                NativeLifetimeMemory.WriteProtected(parent+0x1A0,[1]);
                NativeLifetimeMemory.WriteProtected(parent+0x22A,[0]);
                parentShow=false;
                var scope=menus.Begin(group);Check(scope!=null,"Fourth menu absent: "+string.Join(";",logs));
                Check(G(parent)==0&&G(component)!=0&&G(list)!=0&&G(list)!=27&&G(list)!=G(component)&&G(board)==0&&B(component+0x231)==1,"Copied render groups not isolated, internal inheritance or parent altered");
                if(group==0&&round==0)FinalBatchTests();
                Check(Ptr(owner+0x48)==component&&Ptr(owner+0x1590)==0&&Int(core+0xADDC)==101,"Child controller/focus/title not separate");
                Check(Int(reference+0x38)==1302&&Int(menu+0x38)==1040&&Int(child+0x38)==1412,"Four menu cascade positions differ");
                Check(Int(character+0x38)==910&&Int(character+0x3C)==687,"Character menu not cascaded with Abilities");
                Check(parentShow&&B(parent+0x22A)==1,"Items reference enabled without cancelling closing timeline/finishing Show");
                Check(B(reference+0x90)==1&&B(parent+0x1A0)==1&&B(child+0x90)==1&&B(component+0x1A0)==1,"Parent or child hidden");
                Check(Attached(child)&&Attached(reference)&&Attached(menu),"Native sibling insertion damaged");
                Check(Ptr(child+0x18)==layer&&Ptr(reference+0x18)==layer&&Int(child+0x5C)==1&&B(layer+0xD8)==1&&B(root+0x1E8)==0&&B(child+0x95)==1,"Fourth menu attached to wrong layer or wrong sibling sort invalidated");
                SortUpdateReferences();
                accumulate(child,0);
                Check(Int(child+0xC8)==1&&B(child+0x95)==0,$"Actual native transform did not refresh accumulated visual priority: cache={Int(child+0xC8)}, dirty={B(child+0x95)}, visible={B(child+0xCC)}, parent={Ptr(child+0x18):X}, rootCache={Int(root+0x1D8)}");
                var rendered=RenderReferences();
                Check(rendered.SequenceEqual(new[]{menu,reference,child}),"Actual reverse render traversal does not put child last");
                Check(Int((long)render.Address+0x50)==129,"Actual renderer did not use updated reference depth");
                foreach(string primitive in new[]{"background","text","cursor"})
                {
                    // Three representative parent contexts, native inheritance
                    // into child component context: no GPU/raster emulation.
                    I((long)context.Address+0x220,0);
                    inherit(0,(long)context.Address+0x400,(long)context.Address,child,component);
                    Check(Int((long)context.Address+0x620)==1,"Native nested "+primitive+" did not inherit the child render bias");
                    Check(Int((long)context.Address+0x624)==G(component)<<16,"Native nested context did not use independent batch identity");
                }
                Check(Ptr(child+8)==n+32&&label==labels[group]&&Int(title+0xF8)==0,"Unique name/family title wrong");
                Check(Int(list+0x248)==77&&Ptr(list+0x24C)==0&&Int(list+0x254)==0,"Child input subscriber not isolated");
                Check(CheckedNativeRead.Read(parent+0x300,512).SequenceEqual(parentData),"Original Items rows/header/callbacks changed");
                I(owner+0x15E8,group);I(list+0x2A4,group);
                scope!.Dispose();scope.Dispose();
                Check(Int(owner+0x15E8)==-1&&Int(list+0x2A4)==-1,"Child completion left original native list selection alive");
                Check(!parentShow&&B(parent+0x22A)==0&&B(reference+0x90)==1&&B(parent+0x1A0)==1,"Closed Items disabled native timeline updates and future normal reopening");
                Check(Ptr(owner+0x48)==parent&&Ptr(owner+0x1590)==s+0x8500&&Int(core+0xADDC)==11,"Back/confirm focus not restored");
                Check(Int(reference+0x38)==1412&&Int(menu+0x38)==1150&&B(child+0x90)==0&&B(component+0x1A0)==0,"Back/confirm child not hidden");
                Check(Int(character+0x38)==1020,"Back/confirm character position not restored");
                Check(copies==1&&initializations==3,"Fourth menu allocates or initializes on every reopen");
                // Video047: native parent input alone was reopened, but the
                // GUI remained hidden. Exercise the production visual scope,
                // not manual fixture writes that bypassed the missing step.
                I(core+0xADDC,31);
                var returned=scope.ReopenItems();
                Check(returned!=null&&parentShow&&B(reference+0x90)==1&&B(parent+0x1A0)==1&&B(parent+0x22A)==1,
                    "Back to Items reopened invisible native input");
                Check(Int(core+0xADDC)==11&&Ptr(owner+0x48)==parent&&Ptr(owner+0x1590)==s+0x8500&&B(child+0x90)==0,
                    "Back did not focus/show the original parent with child closed");
                Check(Int(reference+0x38)==1412&&Int(menu+0x38)==1150&&Int(character+0x38)==1020&&
                    CheckedNativeRead.Read(parent+0x300,512).SequenceEqual(parentData),"Back changed original parent rows/title/cascade");
                Check(scope.ReopenItems()==null,"Nested duplicate parent visual scope accepted");
                if(round%3==2)I(core+0xADDC,44); // native exit changed focus
                I(owner+0x15E8,round%7);I(list+0x2A4,round%7);
                returned!.Dispose();returned.Dispose();
                Check(Int(owner+0x15E8)==-1&&Int(list+0x2A4)==-1,"Returned-parent confirm/Back left stale native cursor bindings");
                Check(!parentShow&&B(reference+0x90)==1&&B(parent+0x1A0)==1&&B(parent+0x22A)==0&&
                    Int(core+0xADDC)==(round%3==2?44:31),"Returned Items leaked after confirm/Back or overwrote native exit focus");
                I(core+0xADDC,11);
            }
            NativeNestedCloseGate(menus);
            NativeReentryFrames(menus);
            // Reproduce the old bug: priority writes without transform-dirty
            // leave +C8 stale even though the sibling sort changes correctly.
            I(child+0x5C,2);accumulate(child,0);
            Check(Int(child+0xC8)==1,"Native control did not reproduce stale render depth");
            // Exact live failure: insert after nested Items but claim root as
            // parent, dirty root only. Actual update leaves Layer00 unsorted;
            // actual reverse renderer then draws Items over the visible child.
            using(var liveControl=menus.Begin(0))
            {
                Put(layer+0xD0,reference);Put(reference+0x28,0);Put(reference+0x20,menu);
                Put(menu+0x28,reference);Put(menu+0x20,0);Put(child+0x20,0);Put(child+0x28,0);
                Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x3F0654)(root+0x110,child,reference);
                NativeLifetimeMemory.WriteProtected(layer+0xD8,[0]);NativeLifetimeMemory.WriteProtected(root+0x1E8,[1]);
                SortUpdateReferences();accumulate(child,0);
                Check(Ptr(child+0x18)!=Ptr(reference+0x18)&&RenderReferences().SequenceEqual(new[]{menu,child,reference}),"Live wrong-parent control failed to reproduce child behind Items");
                Put(child+0x18,layer);NativeLifetimeMemory.WriteProtected(layer+0xD8,[1]);
                SortUpdateReferences();
                Check(RenderReferences().SequenceEqual(new[]{menu,reference,child}),"Correct intermediate-layer dirty sort did not fix live failure");
            }
            NativeLifetimeMemory.WriteProtected(reference+0x90,[0]);NativeLifetimeMemory.WriteProtected(parent+0x1A0,[0]);
            using(var hidden=menus.Begin(0)){Check(hidden!=null&&B(reference+0x90)==1&&B(parent+0x1A0)==1,"Closed Items did not reappear behind child");}
            Check(B(reference+0x90)==1&&B(parent+0x1A0)==1&&B(parent+0x22A)==0,"Closed Items disabled future native reopening after confirm");
            Check(!parentShow,"Items Show timeline leaked into target selection");
            // Leave a returned parent scope, then verify no stale GUI writes
            // after native scene ownership changes.
            var departureChild=menus.Begin(1);departureChild!.Dispose();
            var departureReturn=departureChild.ReopenItems();
            Put(battle+0x48,0);departureReturn!.Dispose();
            Check(B(reference+0x90)==1&&B(parent+0x1A0)==1,"Returned-parent disposal touched a departed scene");
            Put(battle+0x48,owner);
            var departed=menus.Begin(0);Put(battle+0x48,0);departed!.Dispose();
            Check(Ptr(owner+0x48)==component,"Departed scene pointer touched");
            Put(battle+0x48,owner);Put(owner+0x48,parent);Put(owner+0x1590,s+0x8500);
            I(reference+0x38,1412);I(menu+0x38,1150);I(core+0xADDC,11);
            I(character+0x38,1020);
            Put(layer+0xD0,reference);Put(reference+0x28,0);Put(reference+0x20,menu);Put(menu+0x28,reference);Put(menu+0x20,0);
            Put(resource+0x70,vector+8); // fresh native resource at reused addresses
            using(var reused=menus.Begin(2)){Check(reused!=null&&copies==2,"Reused scene addresses retained stale cached child");}
            var failedReturnChild=menus.Begin(1);failedReturnChild!.Dispose();failShow=true;
            bool returnFailed=false;
            try{failedReturnChild.ReopenItems();}
            catch(InvalidOperationException){returnFailed=true;}
            finally{failShow=false;}
            Check(returnFailed&&!parentShow&&B(reference+0x90)==1&&B(parent+0x1A0)==1&&B(parent+0x22A)==0&&
                Ptr(owner+0x48)==parent&&Int(core+0xADDC)==11,"Failed Back Show leaked parent visibility/controller/focus");
            using(var returnRetry=failedReturnChild.ReopenItems()){Check(returnRetry!=null&&parentShow,"Failed Back Show left return scope stuck");}
            NativeLifetimeMemory.WriteProtected(reference+0x90,[1]);NativeLifetimeMemory.WriteProtected(parent+0x1A0,[1]);
            NativeLifetimeMemory.WriteProtected(parent+0x22A,[0]);failShow=true;
            Check(menus.Begin(0)==null,"Injected Show failure did not roll back construction");failShow=false;
            Check(B(reference+0x90)==1&&B(parent+0x1A0)==1&&B(parent+0x22A)==0&&!parentShow&&
                Ptr(owner+0x48)==parent&&Int(reference+0x38)==1412&&Int(menu+0x38)==1150&&Int(core+0xADDC)==11,
                "Failed construction lost original visibility/closing state/controller/focus");
            // Partial construction fails once, leaves the original controller
            // and avoids repeated allocations or unsafe resource destruction.
            alias=true;var failed=new NativeMedicineFourthMenu(img,n,Invoke,logs.Add);int baseline=copies;
            Check(failed.Begin(0)==null&&Ptr(owner+0x48)==parent,"Aliased copy changed Items controller");
            for(int i=0;i<32;i++)Check(failed.Begin(1)==null,"Partial copy retried");
            Check(copies==baseline+1,"Failed native construction leaked repeated copies");
            Put(img+0x3CD9DA8,0);Check(menus.Begin(0)==null,"Missing scene changed GUI");
            Console.WriteLine("Fixture: independent fourth native medicine menu: original Items rows/title preserved, Potions/Ethers/Remedys headers, quarter-width cascade, separate controller/focus and192 confirm/back restores, one cached native tree, departed-scene and failed-copy guards. Native allocation/text/raster are explicit scene fixtures.");
            Console.WriteLine("Fixture: fourth-menu video regression: native parent Hide cancelled and Show completed, character/Abilities shifted together preserving their spacing, all family confirm/back restorations; timeline dispatch/raster remain explicit fixtures, not gameplay validation.");
            Console.WriteLine("Fixture: fourth-menu stacking regression: actual PE3EFF08 refreshes child accumulated+C8 depth only with transform-dirty+95; old unchanged-position stale-cache control reproduced, actual PE3E9A6C reverse render traversal and PE3EDCA4 nested render bias verified for192 reopen/confirm/back cycles. Matrix/primitive dispatch/raster/GPU remain fixtures, not gameplay validation.");
            Console.WriteLine("Fixture: fourth-menu final-batch regression: actual PE3E75DC reproduces cloned-group child behind Items text and preserves foreground order with isolated groups; actual PE3E9298 reproduces parent batch reuse and rejects it for isolated child, PE3EDCA4 propagates distinct identities for192 cycles. Material/allocation stubs and GPU pixels are not gameplay validation.");
            Console.WriteLine("Fixture: live Layer00 regression: actual PE3F0654 reproduces wrong-root ownership after nested Items, actual PE4092EC leaves clean Layer00 unsorted and actual PE3E9A6C draws Items over child; correct shared parent and dirty intermediate layer restore child-last order for all3 families/192 cycles. Actual Items group0 covered; no live-game/GPU claim.");
            Console.WriteLine("Fixture: video046 Items closure regression: initially visible/in-flight Hide parent is fully closed after192 family child completions; actual PE408EBC/408FF8 reset+22A for reopening after Back, Show cancelled and Hide completed before target/next turn. Timelines/subscriber/input are explicit fixtures, not gameplay validation.");
            Console.WriteLine("Fixture: video047 Back to Items regression: production ReopenItems restores visible parent/controller/title/focus/positions for192 returns across3 families; every parent confirm/Back closes the visual lifetime, double disposal and duplicate reopen guarded, native exit focus and departed scene preserved. Actual PE408EBC/408FF8 shown-state transitions; input/timelines/raster remain fixtures.");
            Console.WriteLine("Fixture: video049 selected-item Back cleanup: old visual-only PE408FF8 retains addon selection/cursor; production complete PE1568EC/156A84/1569C0 clears native owner+15E8 and list cursor for192 child completions and192 returned-parent exits across3 families. Name/list/validity/cursor-widget leaves are owned fixtures; no live-game or GPU claim.");
        }
        finally
        {
            NativeLifetimeMemory.WriteProtected(img+0x3CD9DA8,providerBefore);NativeLifetimeMemory.WriteProtected(img+0x3CD9DC0,coreBefore);
            NativeLifetimeMemory.WriteProtected(img+0x3F0270,transformBefore);NativeLifetimeMemory.WriteProtected(img+0x3D82F0,matrixBefore);
            NativeLifetimeMemory.WriteProtected(img+0x3EDB20,referenceDrawBefore);NativeLifetimeMemory.WriteProtected(img+0x40A914,componentTransformBefore);
            for(int i=0;i<updateStubs.Length;i++)NativeLifetimeMemory.WriteProtected(img+updateStubs[i],updateBefore[i]);
        }
    }
}

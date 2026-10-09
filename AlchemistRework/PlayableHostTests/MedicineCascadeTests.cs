using System.Runtime.InteropServices;
using FFTModLoader.ContentExpansion.Runtime;

unsafe partial class Program
{
    static void MedicineCascadeTests(OwnedNativeMemory native)
    {
        long img=(long)native.Address;
        var saved=CheckedNativeRead.Read(img+0x3CD9DA8,8);
        using var scene=new OwnedNativeMemory(131072);
        using var names=new OwnedNativeMemory(4096);
        long s=(long)scene.Address,provider=s,manager=s+0x200,battle=s+0x400,owner=s+0x600,
            resource=s+0x2200,root=s+0x2600,items=s+0x3000,menu=s+0x3200,
            action=s+0x3600,list=s+0x3C00,leaf=s+0x4000,collision=s+0x4200,rowText=s+0x4400;
        void Put(long p,long value)=>NativeLifetimeMemory.WriteProtected(p,BitConverter.GetBytes(value));
        void I(long p,int value)=>NativeLifetimeMemory.WriteProtected(p,BitConverter.GetBytes(value));
        int Int(long p)=>BitConverter.ToInt32(CheckedNativeRead.Read(p,4));
        long Ptr(long p)=>BitConverter.ToInt64(CheckedNativeRead.Read(p,8));
        void Node(long p,int type,int x,int width){I(p+0x34,type);I(p+0x38,x);I(p+0x40,width);I(p+0x44,330);I(p+0x5C,8);I(p+0x6C,0x3F800000);NativeLifetimeMemory.WriteProtected(p+0x90,[1]);}
        Put(img+0x3CD9DA8,provider);Put(provider+0x10,manager);Put(manager+0x40,battle);Put(battle+0x48,owner);
        Put(owner+0x18,resource);Put(owner+0x48,action);
        Node(root+0x110,1,0,1920);Node(items,12,1412,440);Node(menu,12,1150,704);
        Node(action+0x110,1,0,440);Node(list,12,0,440);Node(leaf,3,10,128);Node(collision,13,0,440);
        Node(rowText,3,80,320);Put(leaf+0x20,rowText);Put(rowText+0x20,collision);
        Put(action+0x1E0,leaf);Put(root+0x1E0,items);Put(items+0x20,menu);Put(menu+0x28,items);
        NativeLifetimeMemory.WriteProtected((long)names.Address+112,System.Text.Encoding.ASCII.GetBytes("ChemistCascadePassive\0"));
        NativeLifetimeMemory.WriteProtected((long)names.Address+144,System.Text.Encoding.ASCII.GetBytes("TextTitle\0"));
        I(leaf+0x60,0x67);I(leaf+0xF8,0xA92);Put(leaf+8,0x123456);Put(items+8,0x654321);
        long next=s+0x6000;int allocations=0;List<string> logs=[];
        long Invoke(int r,long a,long b,long c,long d)
        {
            switch(r)
            {
                case 0x3F9DA0:return root;
                case 0x3D87BC:Put(a,b);I(a+8,0);return a;
                case 0x3F0794:
                    long name=Ptr(b);
                    return name==(long)names.Address?items:name==(long)names.Address+32?menu:name==(long)names.Address+144?leaf:list;
                case 0x3D87F4:Put(a,Ptr(b));I(a+8,Int(b+8));return a;
                case 0x0FE810:
                case 0x3FAD88:
                    long p=next;next+=0x400;allocations++;
                    Node(p,r==0x0FE810?1:Int(b+0x34),0,440);
                    if(r==0x3FAD88){NativeLifetimeMemory.WriteProtected(p+0x38,CheckedNativeRead.Read(b+0x38,0x68));I(p+0xF8,Int(b+0xF8));}
                    return p;
                case 0x3EF54C:
                case 0x3F0E24:
                case 0x3F0A38:
                    NativeLifetimeMemory.WriteProtected(a+8,CheckedNativeRead.Read(b+8,16));
                    NativeLifetimeMemory.WriteProtected(a+0x38,CheckedNativeRead.Read(b+0x38,0x68));
                    if(r==0x3F0E24)I(a+0xF8,Int(b+0xF8));return 0;
                case 0x3F0654:
                case 0x3EF684:
                    // Actual native attach/position helpers; allocator, text
                    // copying and scene lookups above are explicit fixtures.
                    return Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+r)(a,b,c,d);
                default:throw new InvalidOperationException("Unexpected cascade helper "+r.ToString("X"));
            }
        }
        try
        {
            var cascade=new NativeMedicineMenuCascade(img,(long)names.Address,Invoke,logs.Add);
            long wrapper=0;int baseline=0;
            for(int i=0;i<64;i++)
            {
                I(leaf+0xF8,0xA92+i);
                var scope=cascade.Begin();Check(scope!=null,"Cascade did not open: "+string.Join(";",logs));
                wrapper=Ptr(items+0x20);
                Check(Int(wrapper+0x34)==1&&Int(wrapper+0x38)==1280,"Passive parent not one native inset behind child");
                Check(Int(items+0x38)==1412&&Int(menu+0x38)==1018,"Child right edge changed or preceding menus not shifted");
                Check(Ptr(menu+0x28)==wrapper&&Ptr(wrapper+0x28)==items,"Native sibling chain damaged");
                long drawing=Ptr(wrapper+0xD0),copied=Ptr(drawing+0xD0);
                long validName=(long)names.Address+112;
                Check(Ptr(copied+8)==validName&&Ptr(drawing+8)==validName&&Ptr(wrapper+8)==validName,"Passive lookup CString invalid");
                Check(Int(copied+0xF8)==0xA92+i&&Ptr(copied+0x20)==0&&Int(copied+0x40)<=132,"Row text leaked over child, title stale or collision/input copied");
                Check(CheckedNativeRead.Read(wrapper+0x90,1)[0]==1,"Parent drawing not visible");
                if(i==0)baseline=allocations;else Check(allocations==baseline,"Cascade allocates drawing nodes on each reopen");
                scope!.Dispose();scope.Dispose();
                // Actual native name search traverses hidden nodes too. The
                // v0.2.26 null passive name crashes this byte-comparison path.
                using var lookup=new OwnedNativeMemory(64);
                NativeLifetimeMemory.WriteProtected((long)lookup.Address,System.Text.Encoding.ASCII.GetBytes("NoSuchWidget\0"));
                Put((long)lookup.Address+32,(long)lookup.Address);
                Check(Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x3F0794)(wrapper,(long)lookup.Address+32,0,0)==0,"Native lookup found passive node as real widget");
                Check(Int(menu+0x38)==1150&&CheckedNativeRead.Read(wrapper+0x90,1)[0]==0,"Cancel/confirm did not restore/hide cascade");
            }
            Check(baseline==3,"Snapshot created GUI components instead of three drawing nodes");
            I(list+0x40,0);Check(cascade.Begin()==null&&Int(menu+0x38)==1150,"Malformed layout changes interactive menu");I(list+0x40,440);
            Put(leaf+0x20,leaf);Check(cascade.Begin()==null&&Int(menu+0x38)==1150,"Drawing cycle not rejected safely");Put(leaf+0x20,rowText);
            using(var recovered=cascade.Begin()){Check(recovered!=null,"Recovery after rejected snapshot failed");}
            Check(NativeMedicineMenuCascade.Inset(440)==132&&NativeMedicineMenuCascade.Inset(880)==264,"Cascade scaling differs");
            Console.WriteLine("Fixture: medicine cascade passive parent drawing, native 132/440 inset, native attach/position helpers, lookup-name isolation, no collision/input copy, 64 reopen/restore cycles, bounded allocation reuse and malformed/cyclic scene rejection. Resource allocation/text/raster remain explicit fixtures; visual gameplay check required.");
            Console.WriteLine("Fixture: cascade crash 3F0794 null-CString branch avoided with valid reserved names, actual native search after hide in64 cycles; parent row text omitted and header width bounded to exposed strip. Raster remains gameplay check.");
        }
        finally{NativeLifetimeMemory.WriteProtected(img+0x3CD9DA8,saved);}
    }
}

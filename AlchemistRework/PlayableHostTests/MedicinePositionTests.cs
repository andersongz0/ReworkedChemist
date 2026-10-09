using System.Runtime.InteropServices;
using FFTModLoader.ContentExpansion.Runtime;

unsafe partial class Program
{
    static void MedicinePositionTests(OwnedNativeMemory native)
    {
        long img=(long)native.Address;
        byte[] before=native.Read(0x3CD9DA8,8);
        using var scene=new OwnedNativeMemory(32768);
        long s=(long)scene.Address,provider=s,manager=s+0x100,battle=s+0x200,owner=s+0x300,
            resource=s+0x2000,root=s+0x3000,component=s+0x4000,node=s+0x5000,menu=s+0x5800;
        void Put(long p,long n)=>NativeLifetimeMemory.WriteProtected(p,BitConverter.GetBytes(n));
        void Int(long p,int n)=>NativeLifetimeMemory.WriteProtected(p,BitConverter.GetBytes(n));
        int Read(long p)=>BitConverter.ToInt32(CheckedNativeRead.Read(p,4));
        Put(img+0x3CD9DA8,provider);Put(provider+0x10,manager);Put(manager+0x40,battle);Put(battle+0x48,owner);
        Put(owner+0x18,resource);Put(owner+0x48,component);
        Int(node+0x34,12);Int(node+0x38,1280);Int(node+0x3C,687);Int(node+0x40,384);Int(node+0x44,330);
        byte[] tree=CheckedNativeRead.Read(node+0x18,32),dimensions=CheckedNativeRead.Read(node+0x40,8);
        long Call(int r,long a,long b,long c,long d)
        {
            if(r==0x3D87BC){Put(a,b);return a;}
            return r switch {0x3F9DA0=>root,0x3D8D1C=>0,
                0x3F0794=>BitConverter.ToInt64(CheckedNativeRead.Read(b,8))==s+0x6010?menu:node,
                0x3EF684=>Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+r)(a,b,c,d),
                _=>throw new InvalidOperationException("Unexpected position helper")};
        }
        var layout=new NativeMedicineMenuPosition(img,s+0x6000,Call,_=>{});
        try
        {
            for(int group=0;group<3;group++)for(int i=0;i<64;i++)
            {
                var scope=layout.Begin();Check(scope!=null&&Read(node+0x38)==1376&&Read(node+0x3C)==687,"Medicine submenu does not move1/4 right");
                Check(CheckedNativeRead.Read(node+0x18,32).SequenceEqual(tree)&&CheckedNativeRead.Read(node+0x40,8).SequenceEqual(dimensions),"Position changed tree or dimensions");
                Check(CheckedNativeRead.Read(node+0x95,1)[0]==1,"Native layout cache not invalidated");
                scope!.Dispose();scope.Dispose();Check(Read(node+0x38)==1280,"Medicine confirmation/back did not restore X");
            }
            Int(root+0x150,1920);Int(node+0x38,1412);Int(node+0x40,440);Int(menu+0x34,12);Int(menu+0x38,1150);Int(menu+0x3C,687);
            using(var edge=layout.Begin()){Check(edge!=null&&Read(node+0x38)==1480&&Read(menu+0x38)==1108,"Native cascade escaped right screen edge");}
            Check(Read(node+0x38)==1412&&Read(menu+0x38)==1150,"Edge cascade did not restore both positions");
            Int(root+0x150,0);Int(node+0x38,1280);Int(node+0x40,384);
            var departed=layout.Begin();Put(battle+0x48,0);departed!.Dispose();Check(Read(node+0x38)==1376,"Departed scene pointer was touched");
            Put(battle+0x48,owner);Int(node+0x38,1280);Put(img+0x3CD9DA8,0);Check(layout.Begin()==null,"Absent scene changed layout");
            Check(NativeMedicineMenuPosition.Inset(440)==110&&NativeMedicineMenuPosition.Inset(880)==220,"Quarter-width scaling differs");
            Console.WriteLine("Fixture: three medicine submenus X-only native reference offset1/4, 192 reopen/confirm/back restores, native position cache invalidation, no text/tree/input copies and departed-scene guard. Raster remains gameplay check.");
        }
        finally{NativeLifetimeMemory.WriteProtected(img+0x3CD9DA8,before);}
    }
}

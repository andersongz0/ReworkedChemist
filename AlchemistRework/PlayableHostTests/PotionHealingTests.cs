using System.Buffers.Binary;
using System.Runtime.InteropServices;
using FFTModLoader.ContentExpansion;
using FFTModLoader.ContentExpansion.Runtime;

unsafe partial class Program
{
    static void PotionHealingTests(OwnedNativeMemory native)
    {
        long img=(long)native.Address;
        var saved=new Dictionary<long,byte[]>();
        void Save(int rva,int n)=>saved[img+rva]=native.Read(rva,n);
        foreach(int rva in new[]{0x186AF70,0x186AF68,0x186AF78,0x3CD9EF0})Save(rva,8);
        Save(0x7B0760,0x80);Save(0x186B028,1);Save(0x11A7C00+240,14);Save(0x186AF80,4);
        Save(0x15416E,14);Save(0x15420F,14);
        using var result=new OwnedNativeMemory(4096);
        using var actor=new OwnedNativeMemory(4096);using var target=new OwnedNativeMemory(4096);
        using var scene=new OwnedNativeMemory(0xB0000);
        using var colors=new OwnedNativeMemory(4096);using var invoke=new OwnedNativeMemory(4096);
        try
        {
            native.Write(0x186AF70,BitConverter.GetBytes((long)result.Address));
            native.Write(0x186AF68,BitConverter.GetBytes((long)target.Address));
            native.Write(0x186AF78,BitConverter.GetBytes((long)actor.Address));
            long table=img+BinaryPrimitives.ReadInt32LittleEndian(native.Read(0x309F57,4));
            long entry=BinaryPrimitives.ReadInt64LittleEndian(CheckedNativeRead.Read(table+107*8,8));
            var formula=Marshal.GetDelegateForFunctionPointer<Eight>((nint)entry);
            foreach(ushort ability in ChemistPotionFamily.Abilities)
            foreach(int undead in new[]{0,1})
            {
                native.Write(0x7B0760,new byte[0x80]);native.Write(0x7B0776,[6]);
                native.Write(0x7B0778,BitConverter.GetBytes(ability));
                result.Write(0,new byte[0x38]);result.Write(0,[1]);result.Write(0x2C,[100,0]);
                target.Write(0,new byte[512]);target.Write(0x30,[1,0]);target.Write(0x32,[0xF4,1]);
                target.Write(0x34,[1,0]);target.Write(0x36,[200,0]);target.Write(0x61,[(byte)(undead*16)]);
                formula();
                int expected=ability switch {368=>30,369=>70,370=>150,373=>500,_=>throw new Exception()};
                Check(W(native.Read(0x7B077A,2))==ChemistPotionFamily.Item(ability),"Healing formula lost selected item");
                Check(native.Read(0x7B077E,1)[0]==ChemistPotionFamily.Item(ability)-240,"Healing catalog index aliases Potion");
                Check(native.Read(0x7B0788,1)[0]==(ability==373?74:72),"Elixir/Potion formula mismatch");
                Check(W(result.Read(undead==0?8:6,2))==expected,"Actual native medicine heal/damage differs: "+ability);
                Check(W(result.Read(undead==0?6:8,2))==0,"Native medicine reported both damage and HP healing");
                Check(W(result.Read(12,2))==(ability==373?200:0),"Elixir MP recovery missing or another potion restored MP");
                Check((result.Read(0x27,1)[0]&(undead==0?0x40:0x80))!=0,"Recovery result lost native attack type");
                Check(result.Read(0x1D,5).All(b=>b==0),"Potion variant still produces status instead of recovery");
            }
            native.Write(0x3CD9EF0,BitConverter.GetBytes((long)scene.Address));native.Write(0x186AF80,new byte[4]);
            var medicine=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x2B8E04);
            var actionData=Marshal.GetDelegateForFunctionPointer<Eight>(native.Address+0x2BB060);
            foreach(ushort ability in ChemistMedicineFamilies.All.Where(a=>ChemistMedicineFamilies.Group(a)>0))
            {
                ushort item=ChemistMedicineFamilies.Item(ability);int group=ChemistMedicineFamilies.Group(ability);
                long record=medicine(item);byte[] med=CheckedNativeRead.Read(record,3);
                byte[] action=CheckedNativeRead.Read(actionData(ability),20);
                Check(action[8]==107&&action[11]==med[2]&&action[0]==4,"Variant action lost its own medicine status/range");
                native.Write(0x7B0760,new byte[0x80]);native.Write(0x7B0776,[6]);native.Write(0x7B0778,BitConverter.GetBytes(ability));
                result.Write(0,new byte[0x38]);result.Write(0,[1]);result.Write(0x2C,[100,0]);
                target.Write(0,new byte[512]);actor.Write(0,new byte[512]);
                byte[] status=native.Read(0x80FBA0+med[2]*6,6);
                if(group==2)
                {
                    Check(status[0]==0x10,"Expected native removal medicine type");
                    native.Write(0x7B07B0,status);native.Write(0x7B078F,status.Skip(1).ToArray());
                    target.Write(0x61,status.Skip(1).ToArray());target.Write(0x1EF,status.Skip(1).ToArray());
                }
                formula();
                Check(W(native.Read(0x7B077A,2))==item&&native.Read(0x7B077E,1)[0]==item-240,"Medicine formula aliases base group index");
                if(group==1)
                {
                    Check(W(result.Read(12,2))==(ability==371?20:50)&&result.Read(0x27,1)[0]==0x10,"Native Ether/Hi-Ether recovery differs");
                    Check(W(result.Read(8,2))==0&&result.Read(0x22,5).All(b=>b==0),"Ether unexpectedly heals HP or removes status");
                }
                else
                {
                    Check(native.Read(0x7B0788,1)[0]==56&&result.Read(0x22,5).SequenceEqual(status.Skip(1)),"Actual native status-removal formula lost selected medicine mask: "+ability);
                    Check(result.Read(0x1D,5).All(b=>b==0)&&W(result.Read(8,2))==0,"Remedy family inflicted status or HP recovery");
                }
            }
            Console.WriteLine("Fixture: Ether/Hi-Ether native recovery20/50MP and all seven Remedy-family native removal masks/type16 via real formula107 dispatch, selected catalog indices and own range/action status records; no HP recovery or inflicted statuses. Native result application/scene remain gameplay checks.");
            // Execute ORIGINAL Enhanced inventory stock/color blocks. Widget
            // lookup, strings, raster and unrelated scene code are excluded;
            // stock lookup and writes to title/count color use the real body.
            byte[] Jump(long address)=>[0xFF,0x25,0,0,0,0,..BitConverter.GetBytes(address)];
            invoke.Write(0,[0x53,0x56,0x57,0x41,0x55,0x48,0x89,0xCE,0x49,0x89,0xD5,0x4C,0x89,0xC3,
                ..Jump(img+0x154159)]);
            invoke.Write(128,[0x41,0x5D,0x5F,0x5E,0x5B,0xC3]);invoke.ExecutablePage(0);
            NativeLifetimeMemory.WriteProtected(img+0x15416E,Jump(img+0x1541EC));
            NativeLifetimeMemory.WriteProtected(img+0x15420F,Jump((long)invoke.Address+128));
            var color=Marshal.GetDelegateForFunctionPointer<Eight>(invoke.Address);
            foreach(ushort ability in ChemistMedicineFamilies.All)
            foreach(byte stock in new byte[]{0,1,99})
            {
                native.Write(0x11A7C00+ChemistMedicineFamilies.Item(ability),[stock]);colors.Write(0,new byte[512]);
                color(ChemistMedicineFamilies.Item(ability),(long)colors.Address,(long)colors.Address+128);
                Check(BinaryPrimitives.ReadInt32LittleEndian(colors.Read(0x60,4))==(stock==0?0x69:0x67),"Enhanced title stock color mismatch");
                Check(BinaryPrimitives.ReadInt32LittleEndian(colors.Read(0xE0,4))==(stock==0?0x6A:0x68),"Enhanced missing stock count does not use native red style");
            }
            Console.WriteLine("Fixture: actual formula107 dispatch reaches native Potion/Hi-Potion/X-Potion recovery30/70/150 and Elixir maxHP/maxMP, retaining selected medicine, HP/MP result types and Undead rules; no status result. Result application/scene remain gameplay checks.");
            Console.WriteLine("Fixture: actual Enhanced inventory stock lookup and native title/count color blocks for all13 medicines at stock0/1/99; empty count uses style106, title105, stocked104/103. Widget lookup and raster excluded; child item presentation and full ability commitment verified separately.");
        }
        finally{foreach(var p in saved)NativeLifetimeMemory.WriteProtected(p.Key,p.Value);}
    }
}

using FF16Tools.Files.Nex;
using FF16Tools.Files.Nex.Entities;
using FF16Tools.Files.Nex.Managers;
using System.Collections;
using System.Text.Json;

if(args.Length==2 && args[0]=="--update-flask-descriptions")
{
    string root=Path.GetFullPath(args[1]);
    foreach(string locale in new[]{"en","de","fr","ja","ko","cs","ct"})
    foreach(string family in new[]{"ability","item"})
    {
        string table=family+"."+locale,path=Path.Combine(root,"FFTIVC/data/enhanced/nxd",table+".nxd");
        var input=NexDataFile.FromFile(path);
        var layout=TableMappingReader.ReadTableLayout(table,new Version(1,0,4),"ffto");
        int description=Array.IndexOf(layout.Columns.Keys.ToArray(),"Description");
        var outputBuilder=new NexDataFileBuilder(layout);
        uint first=family=="ability"?515u:263u;
        foreach(var row in input.RowManager!.GetAllRowInfos())
        {
            var cells=NexUtils.ReadRow(layout,input.Buffer!,row.RowDataOffset);
            if(row.Key>=first && row.Key<first+3)
            {
                string element=new[]{"fire","ice","lightning"}[row.Key-first];
                cells[description]=(family=="ability"?"Consumes one flask. ":"")+"Deals 5 + 15% of target maximum HP as "+element+" damage (rounded up), without Faith. Elemental modifiers apply."+(family=="ability"?" Range: 4.":"");
            }
            outputBuilder.AddRow(row.Key,row.Key2,row.Key3,cells);
        }
        using(var stream=File.Create(path))outputBuilder.Write(stream);
        var roundtrip=NexDataFile.FromFile(path);
        Check(roundtrip.RowManager!.GetAllRowInfos().Count()==input.RowManager.GetAllRowInfos().Count(),"Description rewrite changed row count");
        foreach(var row in input.RowManager.GetAllRowInfos())
        {
            Check(roundtrip.RowManager.TryGetRowInfo(out var afterRow,row.Key,row.Key2,row.Key3),"Description rewrite removed row");
            var before=NexUtils.ReadRow(layout,input.Buffer!,row.RowDataOffset);
            var after=NexUtils.ReadRow(layout,roundtrip.Buffer!,afterRow!.RowDataOffset);
            for(int i=0;i<before.Count;i++)
                if(!(row.Key>=first && row.Key<first+3 && i==description))
                    Check(Equal(before[i],after[i]),"Description rewrite changed unrelated field");
        }
    }
    Console.WriteLine("PASS: updated only three flask descriptions in fourteen localized NXD files.");return;
}
if(args.Length!=3)throw new ArgumentException("Pass pristine extraction, playable mod, NEW verification directory.");
string originalRoot=Path.GetFullPath(args[0]),modRoot=Path.GetFullPath(args[1]),output=Path.GetFullPath(args[2]);
if(Directory.Exists(output))throw new IOException("Verification output must be new.");
Directory.CreateDirectory(output);
var version=new Version(1,0,4);
string[] locales=["en","de","fr","ja","ko","cs","ct"];
var costs=new Dictionary<uint,int>{{368,50},{371,150},{380,300},{381,90}};
foreach(string locale in locales)
{
    Verify("ability."+locale,"0004."+locale,513,true);
    Verify("item."+locale,"0004."+locale,261,false);
}
Verify("uiabilityicon","0004",55,false);
// A preceding mod's unrelated change must survive the production diff merge.
string ability="ability.en";
byte[] baseBytes=File.ReadAllBytes(Path.Combine(originalRoot,"0004.en/nxd/ability.en.nxd"));
var baseline=new NexDataFile();baseline.Read(baseBytes);
var abilityLayout=TableMappingReader.ReadTableLayout(ability,version,"ffto");
int nameIndex=Array.IndexOf(abilityLayout.Columns.Keys.ToArray(),"Name");
var builder=new NexDataFileBuilder(abilityLayout);
foreach(var row in baseline.RowManager!.GetAllRowInfos())
{
    var cells=NexUtils.ReadRow(abilityLayout,baseline.Buffer!,row.RowDataOffset);
    if(row.Key==155)cells[nameIndex]="OtherMod Holy Sword Marker";
    builder.AddRow(row.Key,row.Key2,row.Key3,cells);
}
string marker=Path.Combine(output,"other-mod.nxd");using(var file=File.Create(marker))builder.Write(file);
string combinedPath=Path.Combine(output,"compatible.nxd");
NexMergeEngine.Merge(baseBytes,ability,version,[new("other-mod",marker),new("venom-test",Patch(ability))],combinedPath);
var combined=NexDataFile.FromFile(combinedPath);
Check(combined.RowManager!.TryGetRowInfo(out var marked,155),"Unrelated mod row removed.");
Check((string)NexUtils.ReadRow(abilityLayout,combined.Buffer!,marked!.RowDataOffset)[nameIndex]=="OtherMod Holy Sword Marker","Unrelated mod edit overwritten.");
Check(Enumerable.Range(513,11).All(id=>combined.RowManager.TryGetRowInfo(out _,(uint)id)),"Added ability removed by diff merge.");
Console.WriteLine("PASS: production diff merge preserves an unrelated Holy Sword edit and adds all eleven full-width abilities.");
File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new {
    Stage="FullChemistPlayableResources",ProductionNxdMergePassed=true,Locales=locales,NewItemCount=11,Actions=15,
    NewItemId=261,NewAbilityId=513,NewAbilityIconId=55,ReservedAbilityId512Absent=true,
    OriginalRowsPreserved=true,OtherModEditPreserved=true,GameplayTested=false
},new JsonSerializerOptions{WriteIndented=true}));

string Patch(string table)=>Path.Combine(modRoot,"FFTIVC/data/enhanced/nxd",table+".nxd");
void Verify(string name,string archive,uint extraId,bool abilityCosts)
{
    byte[] originalBytes=File.ReadAllBytes(Path.Combine(originalRoot,archive,"nxd",name+".nxd"));
    var original=new NexDataFile();original.Read(originalBytes);
    var patch=NexDataFile.FromFile(Patch(name));
    string mergedPath=Path.Combine(output,name+".nxd");
    NexMergeEngine.Merge(originalBytes,name,version,[new("venom-test",Patch(name))],mergedPath);
    var merged=NexDataFile.FromFile(mergedPath);
    var layout=TableMappingReader.ReadTableLayout(name,version,"ffto");
    string[] columns=layout.Columns.Keys.ToArray();
    var rows=original.RowManager!.GetAllRowInfos().ToArray();
    Check(merged.RowManager!.GetAllRowInfos().Count()==rows.Length+11,"Unexpected added/removed rows: "+name);
    Check(!original.RowManager.TryGetRowInfo(out _,extraId),"New resource identity already exists: "+name);
    foreach(var row in rows)
    {
        Check(merged.RowManager.TryGetRowInfo(out var changed,row.Key,row.Key2,row.Key3),"Original row removed: "+name);
        var before=NexUtils.ReadRow(layout,original.Buffer!,row.RowDataOffset);
        var after=NexUtils.ReadRow(layout,merged.Buffer!,changed!.RowDataOffset);
        for(int i=0;i<before.Count;i++)
        {
            if(abilityCosts&&costs.TryGetValue(row.Key,out int jp)&&columns[i] is "JpCost1" or "JpCost2")
                Check(Convert.ToInt32(after[i])==(columns[i]=="JpCost1"?jp&255:jp>>8),"Retained JP cost differs.");
            else if(!columns[i].StartsWith("Comment",StringComparison.OrdinalIgnoreCase))
                Check(Equal(before[i],after[i]),$"Unrelated original field changed: {name}/{row.Key}/{columns[i]}");
        }
    }
    for(uint id=extraId;id<extraId+11;id++)
    {
        Check(patch.RowManager!.TryGetRowInfo(out var expected,id)&&merged.RowManager.TryGetRowInfo(out _,id),"New row missing: "+name);
        merged.RowManager.TryGetRowInfo(out var addedRow,id);
        var want=NexUtils.ReadRow(layout,patch.Buffer!,expected!.RowDataOffset);
        var actual=NexUtils.ReadRow(layout,merged.Buffer!,addedRow!.RowDataOffset);
        Check(want.Zip(actual).All(p=>Equal(p.First,p.Second)),"New row fields changed: "+name);
        if(name.StartsWith("ability.") && id is >=515 and <=517 || name.StartsWith("item.") && id is >=263 and <=265)
            Check(((string)actual[Array.IndexOf(columns,"Description")]).Contains("5 + 15% of target maximum HP"),"Elemental description still advertises old damage.");
    }
    if(abilityCosts)Check(!merged.RowManager.TryGetRowInfo(out _,512),"Reserved ability 512 inserted.");
    Console.WriteLine($"PASS: production merge {name}, {rows.Length} original rows preserved, added IDs {extraId}–{extraId+10}.");
}
static bool Equal(object? left,object? right)
{
    if(left is NexUnionKey a&&right is NexUnionKey b)return a.Type==b.Type&&a.Value==b.Value;
    if(left is string||right is string)return Equals(left,right);
    if(left is IEnumerable x&&right is IEnumerable y)
    {
        var first=x.Cast<object>().ToArray();var second=y.Cast<object>().ToArray();
        return first.Length==second.Length&&first.Zip(second).All(p=>Equal(p.First,p.Second));
    }
    return Equals(left,right);
}
static void Check(bool okay,string message){if(!okay)throw new InvalidDataException(message);}

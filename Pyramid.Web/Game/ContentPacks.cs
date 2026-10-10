using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pyramid.Web.Game;
public class ContentPack
{
    public int SchemaVersion {get;set;}=1;
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string? Extends {get;set;}
    public int StartLevel {get;set;}=1;
    public List<Realm> Levels {get;set;}=[];
    public List<Portal> Portals {get;set;}=[];
}
public class Realm
{
    public int Number {get;set;}
    public string Name {get;set;}="";
    public string Objective {get;set;}="";
    public string Start {get;set;}="camp";
    public Dictionary<string,ModRoom> Rooms {get;set;}=[];
    public List<Puzzle> Puzzles {get;set;}=[];
    public string[] WinFlags {get;set;}=[];
    public Level AsLevel()=>new(Number,Name,Objective,"",[],Rooms.ToDictionary(x=>x.Key,x=>new Room(x.Key,x.Value.Name,x.Value.Description,x.Value.Exits,x.Value.Items,x.Value.Features)),Puzzles.ToArray());
}
public class ModRoom
{
    public string Name {get;set;}="";
    public string Description {get;set;}="";
    public bool Lit {get;set;}=true;
    public Dictionary<string,string> Exits {get;set;}=[];
    public Dictionary<string,string> Requires {get;set;}=[];
    public string[] Items {get;set;}=[];
    public Dictionary<string,string> Features {get;set;}=[];
    public string? Image {get;set;}
}
public class Portal
{
    public int FromLevel {get;set;}
    public string FromRoom {get;set;}="";
    public string Direction {get;set;}="east";
    public int ToLevel {get;set;}
    public string ToRoom {get;set;}="";
    public string? Requires {get;set;}
    public string Description {get;set;}="A portal shimmers here.";
}
public class RealmSave
{
    public Dictionary<string,List<string>> Items {get;set;}=[];
    public HashSet<string> Flags {get;set;}=[];
}
public static class ContentPacks
{
    private static Dictionary<string,ContentPack> packs=[];
    private static readonly JsonSerializerOptions options=new(){PropertyNameCaseInsensitive=true,UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};
    private static bool Id(string value)=>value!=null&&Regex.IsMatch(value,"\\A[a-z][a-z0-9-]{0,47}\\z");
    private static bool Text(string? value,int max=2000)=>value!=null&&value.Length>0&&value.Length<=max&&!value.Any(c=>char.IsControl(c)&&c!='\n');
    private static bool Item(string value)=>value!=null&&Regex.IsMatch(value,"\\A[a-z][a-z0-9 '-]{0,63}\\z");
    private static readonly string[] directions=["north","south","east","west","up","down"];
    public static ContentPack Get(string id)=>packs.GetValueOrDefault(id)??throw new InvalidOperationException("This content pack is unavailable. Ask the administrator to restore it; your save is retained.");
    public static IEnumerable<object> Catalog()=>new[]{new{id="pyramid",name="Chamber of the Gods"}}.Concat(packs.Values.Select(p=>new{id=p.Id,name=p.Name}));
    public static void Load(string directory)
    {
        var loaded=new Dictionary<string,ContentPack>();var addons=new List<ContentPack>();
        if(!Directory.Exists(directory)){packs=loaded;return;}
        foreach(var file in Directory.GetFiles(directory,"*.json").Order())
        {
            if(new FileInfo(file).Length>1024*1024)throw new InvalidOperationException($"Content file too large: {Path.GetFileName(file)}");
            ContentPack pack;
            try{pack=JsonSerializer.Deserialize<ContentPack>(File.ReadAllText(file),options)??throw new JsonException("Empty pack");}
            catch(JsonException e){throw new InvalidOperationException($"Invalid content file {Path.GetFileName(file)}: {e.Message}");}
            if(pack.SchemaVersion!=1||!Id(pack.Id)||pack.Id=="pyramid"||!Text(pack.Name,100)||pack.Levels==null||pack.Portals==null)throw new InvalidOperationException($"Invalid content header: {Path.GetFileName(file)}");
            if(pack.Extends!=null)addons.Add(pack);else if(!loaded.TryAdd(pack.Id,pack))throw new InvalidOperationException("Duplicate content pack ID: "+pack.Id);
        }
        foreach(var addon in addons)
        {
            if(!loaded.TryGetValue(addon.Extends!,out var parent))throw new InvalidOperationException("Missing base pack: "+addon.Extends);
            parent.Levels.AddRange(addon.Levels);parent.Portals.AddRange(addon.Portals);
        }
        foreach(var pack in loaded.Values)
        {
            try{Validate(pack);}catch(NullReferenceException){throw new InvalidOperationException("Invalid content pack: null entry in "+pack.Id);}
        }
        packs=loaded;
    }
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string why){if(!ok)throw new InvalidOperationException("Invalid content pack: "+why);}
    private static void Validate(ContentPack pack)
    {
        Require(pack.Levels.Count is >0 and <=100,pack.Id+" level count");
        Require(pack.Levels.Select(l=>l.Number).Distinct().Count()==pack.Levels.Count,pack.Id+" duplicate level number");
        Require(pack.Levels.Any(l=>l.Number==pack.StartLevel),pack.Id+" missing start level");
        foreach(var level in pack.Levels)
        {
            Require(level.Number>0&&Text(level.Name,100)&&Text(level.Objective)&&level.Rooms is {Count:>0 and <=100}&&level.Puzzles!=null&&level.WinFlags!=null,"level fields");
            Require(level.Rooms.ContainsKey(level.Start),"start room");
            Require(level.Rooms[level.Start]!=null&&level.Rooms[level.Start].Lit,"start room must provide a lit recovery point");
            foreach(var (id,room) in level.Rooms)
            {
                Require(Id(id)&&room!=null&&Text(room.Name,100)&&Text(room.Description)&&room.Exits!=null&&room.Requires!=null&&room.Items!=null&&room.Features!=null,"room fields");
                Require(room.Exits.All(e=>directions.Contains(e.Key)&&level.Rooms.ContainsKey(e.Value)),"exit destination");
                Require(room.Requires.All(e=>room.Exits.ContainsKey(e.Key)&&Id(e.Value)),"exit requirement");
                Require(room.Items.Length<=40&&room.Items.All(Item),"item names");
                Require(room.Features.Count<=40&&room.Features.All(f=>Item(f.Key)&&Text(f.Value)),"feature text");
                Require(room.Image==null||Regex.IsMatch(room.Image,"\\Aroom-art/room([1-9]|[1-5][0-9]|60)\\.png\\z")||Regex.IsMatch(room.Image,"\\Amod-art/"+Regex.Escape(pack.Id)+"/[a-z][a-z0-9-]{0,63}\\.png\\z"),"image must name a bundled room PNG or a PNG inside this pack's mod-art directory");
            }
            Require(level.Puzzles.Count<=100&&level.Puzzles.Select(p=>p.Flag).Distinct().Count()==level.Puzzles.Count,"puzzle count or duplicate flag");
            foreach(var p in level.Puzzles)Require(p!=null&&level.Rooms.ContainsKey(p.Room)&&Item(p.Command)&&Id(p.Flag)&&Text(p.Response)&&p.Needs!=null&&p.Needs.All(Item)&&p.Requires!=null&&p.Requires.All(Id)&&p.Hints is {Length:>0 and <=5}&&p.Hints.All(h=>Text(h)),"puzzle fields");
            var flags=level.Puzzles.Select(p=>p.Flag).ToHashSet();
            string[] reserved=["help","?","look","l","inventory","i","journal","note","save","retreat","go","n","s","e","w","u","d","north","south","east","west","up","down","get","take","drop","read","examine","x","hint","share","offers","map","clear"];
            Require(level.Puzzles.Select(p=>(p.Room,p.Command)).Distinct().Count()==level.Puzzles.Count,"duplicate puzzle command");
            Require(level.Puzzles.All(p=>p.Command==string.Join(' ',p.Command.Split(' ',StringSplitOptions.RemoveEmptyEntries))&&!reserved.Contains(p.Command.Split(' ')[0])&&p.Command is not "light lamp" and not "off lamp"&&p.Requires.All(flags.Contains)&&!p.Requires.Contains(p.Flag)),"reserved command, whitespace, or invalid puzzle prerequisite");
            Require(level.Rooms.Values.All(r=>r.Requires.Values.All(flags.Contains)),"unknown exit flag");
            Require(level.WinFlags.All(f=>level.Puzzles.Any(p=>p.Flag==f)),"winning flags must be puzzle flags");
        }
        Require(pack.Portals.Count<=200&&pack.Portals.Select(p=>$"{p.FromLevel}:{p.FromRoom}:{p.Direction}").Distinct().Count()==pack.Portals.Count,"duplicate portal or portal count");
        foreach(var p in pack.Portals)
        {
            var from=pack.Levels.SingleOrDefault(l=>l.Number==p.FromLevel);var to=pack.Levels.SingleOrDefault(l=>l.Number==p.ToLevel);
            Require(from!=null&&to!=null&&from.Rooms.ContainsKey(p.FromRoom)&&to.Rooms.ContainsKey(p.ToRoom)&&directions.Contains(p.Direction)&&Text(p.Description)&&!from.Rooms[p.FromRoom].Exits.ContainsKey(p.Direction)&&(p.Requires==null||Id(p.Requires)),"portal destination or conflicting exit");
            Require(p.Requires==null||from.Puzzles.Any(x=>x.Flag==p.Requires),"unknown portal flag");
        }
    }
}


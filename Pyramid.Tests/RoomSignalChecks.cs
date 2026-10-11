using System.Text.Json;
using Pyramid.Web.Game;

static class RoomSignalChecks
{
    public static void Run(Action<bool,string> check)
    {
        JsonElement Signal(Expedition s)=>JsonSerializer.SerializeToElement(Exploration.RoomChanges(s));
        var s=Engine.New();s.Room="east";
        var dark=Signal(s);check(!dark.GetProperty("visible").GetBoolean()&&dark.GetProperty("openRoutes").GetArrayLength()==0,"Dark room signals reveal no routes");
        s.Inventory=["lamp","grain"];s.LampOn=true;
        var before=Signal(s);check(!before.GetProperty("openRoutes").EnumerateArray().Any(x=>x.GetString()=="north"),"Closed ibis arch not signaled as open");
        Engine.Execute(s,"feed bird");var after=Signal(s);
        check(after.GetProperty("openRoutes").EnumerateArray().Any(x=>x.GetString()=="north")&&after.GetProperty("solved").EnumerateArray().Any(x=>x.GetString()=="east-open"),"Puzzle exposes new local route and completion for sound cue");
        s.Room="west";check(Signal(s).GetProperty("solved").GetArrayLength()==0,"Room signals exclude remote puzzle flags");
        var mod=Engine.New("oasis-of-stars");Engine.Execute(mod,"west");Engine.Execute(mod,"down");Engine.Execute(mod,"map");
        check(mod.MapSnapshot!.Nodes.All(n=>mod.Visited.Contains(n.Id)),"Vertical map only reveals visited rooms");
        check(mod.MapSnapshot.Edges.Any(e=>e.Direction=="down"&&e.Kind=="stairs"),"Map retains authored vertical stairs type");
        var old=JsonSerializer.Deserialize<MapEdge>("{\"From\":\"a\",\"To\":\"b\",\"Direction\":\"east\"}");
        check(old!.Kind=="passage","Older saved map edges remain compatible");
    }
}

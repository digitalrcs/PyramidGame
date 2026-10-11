namespace Pyramid.Web.Game;

public record MapNode(string Id,string Name);
public record MapEdge(string From,string To,string Direction,string Kind="passage");
public record MapSnapshot(int Serial,string Realm,string CurrentRoom,MapNode[] Nodes,MapEdge[] Edges);

public static class Exploration
{
    public static object RoomChanges(Expedition s)
    {
        if(!Engine.Lit(s))return new {visible=false,openRoutes=Array.Empty<string>(),solved=Array.Empty<string>()};
        var room=Engine.Current(s).Rooms[s.Room];
        var routes=room.Exits.Where(exit=>s.PackId=="pyramid"
            ? exit.Value switch {"west-shrine"=>s.Flags.Contains("west-open"),"east-shrine"=>s.Flags.Contains("east-open"),"north-shrine"=>s.Flags.Contains("north-open"),_=>true}
            : !ModEngine.Realm(s).Rooms[s.Room].Requires.TryGetValue(exit.Key,out var flag)||s.Flags.Contains(flag))
            .Select(exit=>exit.Key).ToList();
        if(s.PackId!="pyramid")routes.AddRange(ContentPacks.Get(s.PackId).Portals
            .Where(p=>p.FromLevel==s.Level&&p.FromRoom==s.Room&&(p.Requires==null||s.Flags.Contains(p.Requires))).Select(p=>p.Direction));
        var solved=Engine.Current(s).Puzzles.Where(p=>p.Room==s.Room&&s.Flags.Contains(p.Flag)).Select(p=>p.Flag).ToList();
        if(s.PackId=="pyramid"&&s.Room=="gate"&&s.Flags.Contains("word"))solved.Add("word");
        return new {visible=true,openRoutes=routes.Distinct().Order().ToArray(),solved=solved.Order().ToArray()};
    }
    public static void RewardDiscovery(Expedition s)
    {
        var start=s.PackId=="pyramid"?"camp":ModEngine.Realm(s).Start;
        if(s.Room!=start)Engine.Reward(s,$"discover:{s.PackId}:{s.Level}:{s.Room}",1,"A new chamber charted.");
        if(Engine.Current(s).Rooms.Keys.All(s.Visited.Contains))Engine.Reward(s,$"survey:{s.PackId}:{s.Level}",10,"Every chamber in this realm explored.");
    }
    public static bool Execute(Expedition s,string raw)
    {
        var command=string.Join(' ',raw.Trim().ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
        if(command is not ("clear" or "map"))return false;
        s.Actions++;s.Revision++;
        if(command=="clear"){s.Transcript.Clear();return true;}
        s.MapsUsed++;
        var rooms=Engine.Current(s).Rooms;
        var known=s.Visited.Where(rooms.ContainsKey).ToHashSet();
        var origin=s.PackId=="pyramid"?"camp":ModEngine.Realm(s).Start;
        var nodes=known.OrderBy(id=>id==origin?0:1).ThenBy(id=>id).Select(id=>new MapNode(id,rooms[id].Name)).ToArray();
        var edges=known.Order().SelectMany(id=>rooms[id].Exits.Where(e=>known.Contains(e.Value)).Select(e=>new MapEdge(id,e.Value,e.Key,
            s.PackId=="pyramid"?"passage":ModEngine.Realm(s).Rooms[id].ExitKinds.GetValueOrDefault(e.Key,"passage")))).ToList();
        if(s.PackId!="pyramid")edges.AddRange(ContentPacks.Get(s.PackId).Portals.Where(p=>p.FromLevel==s.Level&&p.ToLevel==s.Level&&known.Contains(p.FromRoom)&&known.Contains(p.ToRoom)).Select(p=>new MapEdge(p.FromRoom,p.ToRoom,p.Direction,"portal")));
        s.MapSnapshot=new(s.MapsUsed,$"{s.PackId}:{s.Level}",s.Room,nodes,edges.ToArray());
        Engine.Say(s,"map","command");Engine.Say(s,"Your explored rooms are charted. MAP costs 2 points each time; undiscovered chambers remain hidden.");
        if(s.Transcript.Count>180)s.Transcript.RemoveRange(0,s.Transcript.Count-180);
        return true;
    }
    public static bool RoomActivity(Expedition s)
    {
        if(!Engine.Lit(s))return false;
        return s.Items[s.Room].Count>0||Engine.Current(s).Puzzles.Any(p=>p.Room==s.Room&&!s.Flags.Contains(p.Flag))
            ||Engine.Current(s).Rooms[s.Room].Features.Count>0
            ||(s.PackId=="pyramid"&&s.Room=="study"&&s.Level<=5&&!s.Journal.Contains(Engine.Tablet(s,s.Level)))
            ||(s.PackId=="pyramid"&&s.Room=="gate"&&(!s.Flags.Contains("word")||(s.Level==6&&!s.Won)));
    }
}

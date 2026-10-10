namespace Pyramid.Web.Game;

public record MapNode(string Id,string Name);
public record MapEdge(string From,string To,string Direction);
public record MapSnapshot(int Serial,string Realm,string CurrentRoom,MapNode[] Nodes,MapEdge[] Edges);

public static class Exploration
{
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
        var nodes=known.Order().Select(id=>new MapNode(id,rooms[id].Name)).ToArray();
        var edges=known.Order().SelectMany(id=>rooms[id].Exits.Where(e=>known.Contains(e.Value)).Select(e=>new MapEdge(id,e.Value,e.Key))).ToList();
        if(s.PackId!="pyramid")edges.AddRange(ContentPacks.Get(s.PackId).Portals.Where(p=>p.FromLevel==s.Level&&p.ToLevel==s.Level&&known.Contains(p.FromRoom)&&known.Contains(p.ToRoom)).Select(p=>new MapEdge(p.FromRoom,p.ToRoom,p.Direction)));
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

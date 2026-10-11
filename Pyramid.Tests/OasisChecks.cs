using System.Text.Json;
using Pyramid.Web;
using Pyramid.Web.Game;

static class OasisChecks
{
    public static void Run(Action<bool,string> check)
    {
        JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value);
        var catalog=Json(ContentPacks.Catalog());
        check(catalog.GetArrayLength()==2&&catalog.EnumerateArray().Select(x=>x.GetProperty("id").GetString()).ToHashSet().SetEquals(new[]{"pyramid","oasis-of-stars"}),"Adventure catalog offers original campaign and standalone Oasis, not add-ons");

        var map=Engine.New("oasis-of-stars");Engine.Execute(map,"map");
        check(map.MapSnapshot!.Nodes.Length==1&&map.MapSnapshot.Nodes[0].Id=="camp"&&map.MapSnapshot.Edges.Length==0,"New Oasis map reveals only the starting camp");
        Engine.Execute(map,"west");Engine.Execute(map,"map");
        check(map.MapSnapshot!.Nodes.Select(n=>n.Id).ToHashSet().SetEquals(new[]{"camp","well"})&&map.MapSnapshot.Edges.All(e=>new[]{"camp","well"}.Contains(e.From)&&new[]{"camp","well"}.Contains(e.To)),"Oasis map hides unvisited expanded rooms and their connections");
        var locked=Engine.New("oasis-of-stars");Engine.Execute(locked,"east");Engine.Execute(locked,"turn key");Engine.Execute(locked,"east");
        check(locked.Level==1&&locked.Room=="arch"&&!locked.Flags.Contains("portal-open"),"Oasis first portal cannot be opened without its carried key");
        foreach(var realm in ContentPacks.Get("oasis-of-stars").Levels)
        foreach(var puzzle in realm.Puzzles)
        {
            Expedition AtPuzzle()
            {
                var state=Engine.New("oasis-of-stars");state.Level=realm.Number;state.Room=puzzle.Room;state.Inventory=puzzle.Needs.ToList();state.LampOn=true;
                ModEngine.Restore(state);return state;
            }
            if(puzzle.Needs.Length>0)
            {
                var state=AtPuzzle();state.Flags.UnionWith(puzzle.Requires);state.Inventory.Remove(puzzle.Needs[0]);
                Engine.Execute(state,puzzle.Command);
                check(!state.Flags.Contains(puzzle.Flag),"Oasis puzzle requires its carried object: "+puzzle.Command);
            }
            if(puzzle.Requires.Length>0)
            {
                var state=AtPuzzle();state.Flags.UnionWith(puzzle.Requires.Skip(1));Engine.Execute(state,puzzle.Command);
                check(!state.Flags.Contains(puzzle.Flag),"Oasis puzzle cannot skip prior mechanisms: "+puzzle.Command);
            }
        }

        var path=Path.Combine(Path.GetTempPath(),"pyramid-oasis-"+Guid.NewGuid().ToString("N"));
        var db=new Storage(path);db.Create("host","OasisExplorer","test-hash");db.Create("other","OtherExplorer","test-hash");
        db.Command("host",0,"get bottle");var original=JsonSerializer.Serialize(db.Read("host"));
        JsonElement View()=>Json(db.PlayView("host"));
        JsonElement State()=>View().GetProperty("game");
        void Command(string id,string command)
        {
            var revision=View().GetProperty("party").GetProperty("Revision").GetInt64();
            if(!db.PlayCommand("host",revision,command,id).accepted)throw new Exception("Adventure command rejected: "+command);
        }
        var oasis=db.NewParty("host","Private oasis","oasis-of-stars",false);
        Command(oasis,"west");Command(oasis,"get bronze key");Command(oasis,"note The key opens the arch");
        var pyramid=db.NewParty("host","Private pyramid","pyramid",false);Command(pyramid,"north");
        check(State().GetProperty("PackId").GetString()=="pyramid"&&State().GetProperty("roomId").GetString()=="hub","One explorer can start the original campaign without a companion");
        db.PartyAction("host",oasis,"resume");
        check(State().GetProperty("PackId").GetString()=="oasis-of-stars"&&State().GetProperty("roomId").GetString()=="well"&&State().GetProperty("inventory").EnumerateArray().Any(x=>x.GetString()=="bronze key")&&State().GetProperty("Journal").GetArrayLength()==1,"Switching adventures restores Oasis location, belongings, and notes");
        db=new Storage(path);db.PartyAction("host",pyramid,"resume");
        check(State().GetProperty("roomId").GetString()=="hub"&&!State().GetProperty("inventory").EnumerateArray().Any(x=>x.GetString()=="bronze key")&&State().GetProperty("Journal").GetArrayLength()==0,"Two adventures retain independent progress across storage restart");
        check(Json(db.Lobby("other")).GetProperty("games").GetArrayLength()==0,"Private single-player adventures remain hidden from other accounts");
        db.PartyAction("host",pyramid,"solo");
        check(JsonSerializer.Serialize(db.Read("host"))==original,"Selecting either adventure leaves the existing original solo save untouched");

        var old=new Expedition{PackId="oasis-of-stars",Level=2,Room="temple",Inventory=["bronze key"],Solved=2,
            Achievements=["The Moon Realm complete"],Visited=["landing","temple"],
            Realms=new(){[1]=new(){Items=new(){["camp"]=[],["well"]=[],["arch"]=[]},Flags=["portal-open"]},
                [2]=new(){Items=new(){["landing"]=[],["temple"]=[]},Flags=["moon-awake"]}},
            VisitedRealms=new(){[1]=["camp","well","arch"],[2]=["landing","temple"]}};
        old=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(old))!;ModEngine.Restore(old);
        Engine.Execute(old,"look");
        check(!old.Won&&old.Room=="temple"&&old.Flags.Contains("moon-awake")&&old.Inventory.Contains("bronze key"),"Legacy demo completion remains playable without incorrectly winning the expanded adventure");
        Engine.Execute(old,"north");Engine.Execute(old,"get star chart");
        check(old.Room=="archive"&&old.Inventory.Contains("star chart"),"Legacy demo save initializes and can collect objects in added rooms");
        old=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(old))!;ModEngine.Restore(old);
        check(!old.Items["archive"].Contains("star chart"),"Added-room collection persists through an upgraded save round trip");
        Engine.Execute(old,"south");Engine.Execute(old,"west");Engine.Execute(old,"west");Engine.Execute(old,"west");Engine.Execute(old,"west");
        check(old.Level==1&&old.Room=="well"&&!old.Items["well"].Contains("bronze key"),"Upgrade does not respawn old collected objects when returning through portals");

        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
        var guide=File.ReadAllText(Path.Combine(root,"docs","Oasis-Walkthrough.md"));
        var chapters=System.Text.RegularExpressions.Regex.Matches(guide,"```text\\s*([\\s\\S]*?)```");
        check(chapters.Count==3,"Developer walkthrough supplies a command route for each of three realms");
        var adventure=Engine.New("oasis-of-stars");adventure.Inventory=["lamp","bottle"];adventure.LampOn=true;
        for(int chapter=0;chapter<chapters.Count;chapter++)
        {
            foreach(var command in chapters[chapter].Groups[1].Value.Split('\n').Select(x=>x.Trim()).Where(x=>x.Length>0))Engine.Execute(adventure,command);
            if(chapter==0)check(adventure.Level==2&&adventure.Room=="landing"&&adventure.Solved==4&&!adventure.Won,"Published Oasis route solves the spring and reaches the moon without early victory");
            if(chapter==1)check(adventure.Level==3&&adventure.Room=="dawnlanding"&&adventure.Solved==8&&!adventure.Won,"Published Moon route solves four puzzles and reaches Dawn without early victory");
            adventure=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(adventure))!;ModEngine.Restore(adventure);
        }
        check(adventure.Won&&adventure.Finished!=null&&adventure.Solved==13&&adventure.Room=="sanctum","Published full walkthrough wins all three Oasis realms and thirteen puzzles across save reloads");
        var score=adventure.Score;var finished=adventure.Finished;Engine.Execute(adventure,"offer water to stranger");Engine.Execute(adventure,"look");
        check(adventure.Score==score&&adventure.Solved==13&&adventure.Finished==finished,"Repeating the Oasis finale cannot farm points or change completion time");
        var groupState=Engine.New("oasis-of-stars");groupState.Inventory=["lamp"];groupState.LampOn=true;
        var party=new Party{World=groupState};
        party.Members["leader"]=new(){Name="Leader",State=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(groupState))!};
        party.Members["sleeper"]=new(){Name="Sleeper",State=Engine.New("oasis-of-stars")};
        foreach(System.Text.RegularExpressions.Match chapter in chapters)
        {
            foreach(var command in chapter.Groups[1].Value.Split('\n').Select(x=>x.Trim()).Where(x=>x.Length>0))Cooperation.Execute(party,"leader",command);
            party=JsonSerializer.Deserialize<Party>(JsonSerializer.Serialize(party))!;
        }
        check(party.World.Won&&party.World.Solved==13&&party.Members["leader"].State.Won,"The complete Oasis route also wins through the shared expedition engine");
        Cooperation.Bind(party,party.Members["sleeper"].State);
        check(party.Members["sleeper"].State.Won&&party.Members["sleeper"].State.Level==1&&party.Members["sleeper"].State.Room=="camp","Returning Oasis companion receives shared victory while retaining their separate saved realm and room");
    }
}

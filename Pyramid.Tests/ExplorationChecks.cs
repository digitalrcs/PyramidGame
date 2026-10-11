using Pyramid.Web.Game;
using System.Text.Json;

static class ExplorationChecks
{
    public static void Run(Action<bool,string> check)
    {
        var s=Engine.New();s.Solved=2;s.Journal.Add("Keep my clue");
        Engine.Execute(s,"map");
        check(s.Score==48&&s.MapsUsed==1,"MAP charges two points");
        check(s.MapSnapshot!.Nodes.Length==1&&s.MapSnapshot.Nodes[0].Id=="camp"&&s.MapSnapshot.Edges.Length==0,"MAP excludes undiscovered rooms and routes");
        var before=JsonSerializer.Serialize(s.MapSnapshot);Engine.View(s);Engine.View(s);
        check(s.MapsUsed==1&&JsonSerializer.Serialize(s.MapSnapshot)==before,"Reading state does not charge or refresh map");
        Engine.Execute(s,"get lamp");Engine.Execute(s,"light lamp");Engine.Execute(s,"north");
        check(s.MapSnapshot.Nodes.Length==1,"Exploration does not reveal new map nodes without opening MAP");
        Engine.Execute(s,"map");
        check(s.Score==47&&s.MapSnapshot.Serial==2&&s.MapSnapshot.CurrentRoom=="hub"&&s.MapSnapshot.Nodes.Length==2,"Second map opening charges and refreshes explored rooms");
        check(s.MapSnapshot.Edges.Length==2&&s.MapSnapshot.Edges.All(e=>new[]{"camp","hub"}.Contains(e.From)&&new[]{"camp","hub"}.Contains(e.To)),"Map links contain visited destinations only");
        var progress=(s.Room,s.Score,s.Moves,s.MapsUsed,s.Inventory.Count,s.Visited.Count);Engine.Execute(s,"clear");
        check(s.Transcript.Count==0&&s.Journal.SequenceEqual(new[]{"Keep my clue"})&&progress==(s.Room,s.Score,s.Moves,s.MapsUsed,s.Inventory.Count,s.Visited.Count),"CLEAR empties transcript without erasing journal or progress");
        s=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(s))!;
        check(s.Transcript.Count==0&&s.MapsUsed==2&&s.MapSnapshot!.Serial==2,"CLEAR and paid map persist through save roundtrip");
        check(s.BonusPoints==1,"First new chamber earns a discovery bonus");Engine.Execute(s,"south");Engine.Execute(s,"north");
        check(s.BonusPoints==1&&s.Score==47,"Revisits and starting rooms cannot farm discovery bonuses after reload");
        var poor=Engine.New();Engine.Execute(poor,"map");poor.Solved=1;
        check(poor.Score==23,"Map cost remains recorded when opened at zero score");
        var activity=Engine.New();check(Exploration.RoomActivity(activity),"Loose objects mark visible room activity");
        activity.Items["camp"].Clear();check(!Exploration.RoomActivity(activity),"Empty landing does not mark activity");
        activity.Room="west";check(!Exploration.RoomActivity(activity),"Darkness hides room activity");
        activity.Inventory=["lamp"];activity.LampOn=true;activity.Items["west"].Clear();check(Exploration.RoomActivity(activity),"Unsolved local puzzle marks activity");
        foreach(var p in Engine.Current(activity).Puzzles.Where(p=>p.Room=="west"))activity.Flags.Add(p.Flag);
        check(!Exploration.RoomActivity(activity),"Solved empty room clears activity");
        activity.Room="gate";check(Exploration.RoomActivity(activity),"Unanswered gate marks activity");activity.Flags.Add("word");check(!Exploration.RoomActivity(activity),"Answered gate clears activity");
        activity.Level=6;activity.Items["gate"]=[];check(Exploration.RoomActivity(activity),"Final unfinished throne marks activity");activity.Won=true;check(!Exploration.RoomActivity(activity),"Completed empty throne clears activity");

        var mod=Engine.New("oasis-of-stars");Engine.Execute(mod,"west");Engine.Execute(mod,"get bronze key");Engine.Execute(mod,"east");Engine.Execute(mod,"east");Engine.Execute(mod,"turn key");Engine.Execute(mod,"east");
        check(mod.Level==2&&mod.Visited.SetEquals(new[]{"landing"}),"Portal map begins with only visited destination room");
        Engine.Execute(mod,"east");Engine.Execute(mod,"map");
        check(mod.MapSnapshot!.Realm=="oasis-of-stars:2"&&mod.MapSnapshot.Nodes.Length==2&&mod.MapsUsed==1,"Mod MAP uses current realm and charges once");
        Engine.Execute(mod,"west");Engine.Execute(mod,"west");
        check(mod.Level==1&&mod.Visited.SetEquals(new[]{"camp","well","arch"}),"Portal return restores personal visited room history");
        check(mod.BonusPoints==3,"Three discovered Oasis rooms earn bonuses without claiming an incomplete realm survey");
        mod=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(mod))!;Engine.Execute(mod,"east");
        check(mod.Level==2&&mod.Visited.Contains("temple"),"Visited realm history survives save roundtrip");
        Engine.Execute(mod,"east");check(mod.BonusPoints==3,"Portal revisits cannot repeat discovery or survey bonuses");
        Engine.Execute(mod,"clear");check(mod.Transcript.Count==0&&mod.Inventory.Contains("bronze key"),"Mod CLEAR preserves belongings");
        var party=new Party{World=Engine.New()};party.Members["a"]=new(){State=Engine.New()};party.Members["b"]=new(){State=Engine.New()};
        Cooperation.Execute(party,"a","map");Cooperation.Execute(party,"a","clear");
        check(party.Members["a"].State.Transcript.Count==0&&party.Members["b"].State.Transcript.Count>0&&party.Members["a"].State.MapsUsed==1&&party.Members["b"].State.MapsUsed==0&&party.Members["b"].State.MapSnapshot==null,"Co-op maps and transcript clearing remain personal");
        var giver=party.Members["a"];var recipient=party.Members["b"];
        giver.Name="Alpha";recipient.Name="Bravo";giver.Awake=recipient.Awake=true;giver.Seen=recipient.Seen=DateTimeOffset.UtcNow;
        giver.State.Inventory=["lamp","bottle","rope","grain","token"];
        Cooperation.Execute(party,"a","share item bottle with Bravo");check(giver.State.BonusPoints==0,"Unaccepted offer earns no bonus");
        Cooperation.Execute(party,"b","get bottle from Alpha");check(giver.State.BonusPoints==5&&recipient.State.BonusPoints==0,"Accepted transfer rewards only giver");
        Cooperation.Execute(party,"b","share item bottle with Alpha");Cooperation.Execute(party,"a","get bottle from Bravo");
        Cooperation.Execute(party,"a","share item bottle with Bravo");Cooperation.Execute(party,"b","get bottle from Alpha");
        check(giver.State.BonusPoints==5,"Passing same item back and forth cannot farm giver rewards");
        Cooperation.Execute(party,"a","share item lamp with Bravo");Cooperation.Execute(party,"b","get lamp from Alpha");check(giver.State.BonusPoints==5,"Personal lamps do not earn sharing bonuses");
        foreach(var item in new[]{"rope","grain","token"}){Cooperation.Execute(party,"a",$"share item {item} with Bravo");Cooperation.Execute(party,"b",$"get {item} from Alpha");}
        check(giver.State.BonusPoints==15,"Item-sharing reward capped at three distinct items per realm");
        Cooperation.Execute(party,"a","share journal with Bravo");check(giver.State.BonusPoints==15,"Empty journal cannot earn a sharing bonus");
        giver.State.Journal.Add("Observe the stars");Cooperation.Execute(party,"a","share journal with Bravo");Cooperation.Execute(party,"a","share journal with Bravo");
        check(giver.State.BonusPoints==18,"New journal knowledge earns bonus only once");
        giver.State.Journal.Add("Observe the moon");Cooperation.Execute(party,"a","share journal with Bravo");check(giver.State.BonusPoints==18,"Journal reward limited to once per expedition");
    }
}

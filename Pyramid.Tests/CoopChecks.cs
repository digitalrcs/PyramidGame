using System.Text.Json;
using System.Text.Json.Nodes;
using Pyramid.Web;
using Pyramid.Web.Game;

static class CoopChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        JsonElement Json(object o)=>JsonSerializer.SerializeToElement(o);
        bool Has(JsonElement a,string value)=>a.EnumerateArray().Any(x=>x.GetString()==value);
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}check(rejected,name);}
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
        var content=Path.Combine(root,"Pyramid.Web","ContentPacks");
        ContentPacks.Load(content);
        var path=Path.Combine(Path.GetTempPath(),"pyramid-coop-"+Guid.NewGuid().ToString("N"));
        var db=new Storage(path);
        foreach(var id in new[]{"a","b","c"})db.Create(id,id=="a"?"Wayfarer":id=="b"?"Mrs_Player":"Observer","test-hash");
        db.Command("a",0,"get bottle");
        var solo=JsonSerializer.Serialize(db.Read("a"));
        string game=db.NewParty("a","Test expedition","pyramid",false);
        JsonElement View(string id)=>Json(db.PlayView(id));
        JsonElement State(string id)=>View(id).GetProperty("game");
        long Rev(string id)=>View(id).GetProperty("party").GetProperty("Revision").GetInt64();
        JsonElement Cmd(string id,string command){var r=db.PlayCommand(id,Rev(id),command,game);check(r.accepted,"Co-op command accepted: "+command);return Json(r.view).GetProperty("game");}
        check(Json(db.Lobby("c")).GetProperty("games").GetArrayLength()==0,"Private games hidden from outsiders");
        Reject(()=>db.PartyAction("b",game,"accept"),"Uninvited player cannot enter private game");
        Reject(()=>db.PartyAction("b",game,"open"),"Only owner may open game");
        var privateRevision=Rev("a");
        Reject(()=>db.PartyAction("c",game,"decline"),"Outsider cannot decline nonexistent invitation");
        check(Rev("a")==privateRevision,"Outsider decline cannot invalidate shared revision");
        db.PartyAction("a",game,"invite","Mrs_Player");
        check(Json(db.Lobby("b")).GetProperty("games")[0].GetProperty("invited").GetBoolean(),"Invitation visible to recipient");
        db.PartyAction("b",game,"accept");
        db.PartyAction("a",game,"open");db.PartyAction("c",game,"request");
        var requestedRevision=Rev("a");db.PartyAction("c",game,"request");
        check(Rev("a")==requestedRevision,"Repeated join request does not invalidate shared revision");
        Reject(()=>db.PartyAction("c",game,"resume"),"Join request requires host approval");
        Reject(()=>db.PartyAction("b",game,"approve","c"),"Member cannot approve another member");
        db.PartyAction("a",game,"approve","c");db.PartyAction("c",game,"accept");
        check(View("a").GetProperty("party").GetProperty("members").GetArrayLength()==3,"Approved request joins saved expedition");
        var rev=Rev("a");
        var race=await Task.WhenAll(Task.Run(()=>db.PlayCommand("a",rev,"get bottle",game)),Task.Run(()=>db.PlayCommand("b",rev,"get bottle",game)));
        check(race.Count(r=>r.accepted)==1,"Concurrent party commands enforce a shared revision");
        check(new[]{"a","b"}.Count(id=>Has(State(id).GetProperty("inventory"),"bottle"))==1,"Concurrent pickup cannot duplicate an object");
        var owner=Has(State("a").GetProperty("inventory"),"bottle")?"a":"b";var receiver=owner=="a"?"b":"a";
        var ownerName=owner=="a"?"Wayfarer":"Mrs_Player";var receiverName=receiver=="a"?"Wayfarer":"Mrs_Player";
        Cmd(receiver,"get bottle from "+ownerName);check(!Has(State(receiver).GetProperty("inventory"),"bottle"),"Items cannot be taken without consent");
        Cmd(owner,"share item bottle with "+receiverName);check(Has(State(owner).GetProperty("inventory"),"bottle"),"Offering keeps item until accepted");
        Cmd(receiver,"get bottle from "+ownerName);
        check(!Has(State(owner).GetProperty("inventory"),"bottle")&&Has(State(receiver).GetProperty("inventory"),"bottle"),"Accepted item transfers once");
        Cmd(receiver,"get bottle from "+ownerName);check(State(receiver).GetProperty("inventory").EnumerateArray().Count(x=>x.GetString()=="bottle")==1,"Offer cannot be redeemed twice");
        Cmd("a","north");Cmd("a","west");Cmd("b","north");Cmd("b","east");
        check(State("a").GetProperty("roomId").GetString()=="west"&&State("b").GetProperty("roomId").GetString()=="east","Companions explore independently");
        Cmd(receiver,"share item bottle with "+ownerName);Cmd(owner,"get bottle from "+receiverName);
        check(Has(State(receiver).GetProperty("inventory"),"bottle"),"Items cannot transfer between rooms");
        Cmd("a","east");Cmd("b","west");
        check(State("a").GetProperty("Transcript").EnumerateArray().Any(x=>x.GetProperty("Text").GetString()!.Contains("Mrs_Player has entered")),"Arrivals announced to players already in room");
        Cmd("a","share information The river has a clue");
        check(State("b").GetProperty("Transcript").EnumerateArray().Any(x=>x.GetProperty("Text").GetString()!.Contains("The river has a clue")),"Information shared with companion in room");
        check(!State("c").GetProperty("Transcript").EnumerateArray().Any(x=>x.GetProperty("Text").GetString()!.Contains("The river has a clue")),"Room information is not broadcast to remote companions");
        Cmd("a","note Look for the river");Cmd("a","share journal with Mrs_Player");Cmd("a","share journal with Mrs_Player");
        check(State("b").GetProperty("Journal").EnumerateArray().Count(x=>x.GetString()!.Contains("river"))==1,"Journal sharing copies notes without duplicates");
        var inventoryBeforeSleep=State("a").GetProperty("inventory").GetRawText();
        db.Sleep("a");Cmd("b","look");
        check(State("b").GetProperty("Transcript").EnumerateArray().Any(x=>x.GetProperty("Text").GetString()!.Contains("Wayfarer is standing still, in a trance")),"Sleeping companion described in LOOK");
        db=new Storage(path);check(State("a").GetProperty("roomId").GetString()=="hub","Resume restores saved location after storage recreation");
        check(State("a").GetProperty("inventory").GetRawText()==inventoryBeforeSleep,"Resume preserves personal inventory");
        db.PartyAction("a",game,"solo");check(JsonSerializer.Serialize(db.Read("a"))==solo,"Original solo expedition preserved by co-op play");
        check(!db.PlayCommand("a",0,"look",game).accepted,"Stale party context cannot mutate solo game");
        db.PartyAction("a",game,"resume");check(State("a").GetProperty("roomId").GetString()=="hub","Returning from solo resumes shared expedition");

        game=db.NewParty("a","Portal expedition","oasis-of-stars",false);db.PartyAction("a",game,"invite","Mrs_Player");db.PartyAction("b",game,"accept");
        Cmd("a","west");Cmd("a","get bronze key");Cmd("a","east");Cmd("a","east");Cmd("a","turn key");Cmd("a","east");
        check(State("a").GetProperty("Level").GetInt32()==2&&State("b").GetProperty("Level").GetInt32()==1,"Portal travel leaves companion in their own realm");
        Cmd("a","east");Cmd("a","ring bell");Cmd("a","west");Cmd("a","west");Cmd("a","west");Cmd("a","west");
        check(!State("a").GetProperty("Transcript").EnumerateArray().Last().GetProperty("Text").GetString()!.Contains("You see: bronze key"),"Returning realm does not respawn collected item");
        Cmd("b","east");Cmd("b","east");Cmd("b","east");var solved=State("b").GetProperty("Solved").GetInt32();Cmd("b","ring bell");
        check(State("b").GetProperty("Solved").GetInt32()==solved,"Realm puzzle state shared across portal visits");
        db=new Storage(path);check(State("a").GetProperty("Level").GetInt32()==1&&State("b").GetProperty("Level").GetInt32()==2,"Independent realms persist across restart");

        var expanded=Path.Combine(path,"expanded-content");Directory.CreateDirectory(expanded);
        var extension=JsonNode.Parse(File.ReadAllText(Path.Combine(content,"oasis-of-stars.json")))!;
        var rooms=extension["levels"]![0]!["rooms"]!;
        rooms["well"]!["exits"]!["west"]="garden";
        rooms["garden"]=JsonNode.Parse("""{"name":"Hidden garden","description":"A newly uncovered garden lies beside the well.","exits":{"east":"well"},"items":["lotus"]}""");
        File.WriteAllText(Path.Combine(expanded,"oasis-of-stars.json"),extension.ToJsonString());
        File.Copy(Path.Combine(content,"oasis-moon-addon.json"),Path.Combine(expanded,"oasis-moon-addon.json"));
        ContentPacks.Load(expanded);
        check(!Has(State("a").GetProperty("items"),"bronze key"),"Expanded pack does not respawn objects in saved rooms");
        Cmd("a","west");check(State("a").GetProperty("roomId").GetString()=="garden"&&Has(State("a").GetProperty("items"),"lotus"),"New rooms initialize objects inside existing realm saves");
        Cmd("a","get lotus");db=new Storage(path);
        check(Has(State("a").GetProperty("inventory"),"lotus")&&!Has(State("a").GetProperty("items"),"lotus"),"New-room objects remain collected after restart");
        Cmd("a","east");
        var empty=Path.Combine(path,"empty-content");Directory.CreateDirectory(empty);ContentPacks.Load(empty);
        var roster=Json(db.Roster());
        check(roster.GetArrayLength()==3&&roster.EnumerateArray().Count(x=>x.GetProperty("room").GetString()=="Content pack unavailable")==2,"Removed content does not break roster for other players");
        ContentPacks.Load(content);

        var invalid=Path.Combine(path,"invalid-content");Directory.CreateDirectory(invalid);
        var original=JsonNode.Parse(File.ReadAllText(Path.Combine(content,"oasis-of-stars.json")))!;
        foreach(var image in new[]{"../secret.png","https://example.com/tracker.png","room-art/room61.png","room-art/room1.svg"})
        {
            var bad=original.DeepClone();bad["levels"]![0]!["rooms"]!["camp"]!["image"]=image;
            File.WriteAllText(Path.Combine(invalid,"bad.json"),bad.ToJsonString());Reject(()=>ContentPacks.Load(invalid),"Unsafe mod image rejected: "+image);
        }
        ContentPacks.Load(content);
        Campaign(check);
    }

    static void Campaign(Action<bool,string> check)
    {
        var s=Engine.New();s.Inventory=["lamp"];s.LampOn=true;s.Items["camp"].Remove("lamp");
        var party=new Party{World=s};party.Members["a"]=new(){Name="Leader",State=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(s))!,Awake=true,Seen=DateTimeOffset.UtcNow};
        party.Members["b"]=new(){Name="Sleeper",State=Engine.New()};
        void Cmd(string text)=>Cooperation.Execute(party,"a",text);
        Expedition State()=>party.Members["a"].State;
        void Travel(string target)
        {
            var q=new Queue<(string,List<string>)>();q.Enqueue((State().Room,[]));var seen=new HashSet<string>();
            while(q.Count>0){var(room,path)=q.Dequeue();if(room==target){foreach(var dir in path)Cmd(dir);check(State().Room==target,"Co-op travel "+target);return;}if(!seen.Add(room))continue;foreach(var exit in Engine.Current(State()).Rooms[room].Exits)q.Enqueue((exit.Value,[..path,exit.Key]));}
            throw new Exception("Unreachable room");
        }
        string[][] solutions=[ ["east|fill bottle","west|pour water","east|feed ibis","gallery|tie rope"], ["west|oil wheel","west|turn wheel","east|place weight","gallery|lay plank"], ["west|polish mirror","east|fit cog","east|set orion","gallery|turn east"], ["west|strike sun river moon","east|play flute","gallery|sound fork"], ["west|place feather","east|reflect name","east|say amen","gallery|show mirror"], ["west|place cube","east|fit rod","gallery|turn east"] ];
        string[] words=["dawn","memory","shadow","silence","truth","eternity"];
        Cmd("get bottle");
        for(int n=1;n<=6;n++)
        {
            foreach(var room in new[]{"west","east","study"}){Travel(room);foreach(var item in State().Items[room].ToArray())Cmd("take "+item);if(room=="study")Cmd("read tablet");}
            foreach(var action in solutions[n-1]){var parts=action.Split('|');Travel(parts[0]);Cmd(parts[1]);}
            foreach(var room in new[]{"west-shrine","east-shrine","north-shrine"}){Travel(room);foreach(var item in State().Items[room].ToArray())Cmd("get "+item);}
            Travel("camp");Cmd("deposit");check(State().Banked.Count==n*3,"Co-op relics banked on level "+n);
            Travel("gate");Cmd("say "+words[n-1]);
            party=JsonSerializer.Deserialize<Party>(JsonSerializer.Serialize(party))!;
            if(n<6)
            {
                if(n==1){var other=party.Members["b"];other.Awake=true;other.Seen=DateTimeOffset.UtcNow;Cmd("ascend");check(State().Level==1,"Awake companions must gather before ascent");Cmd("ascend now");check(State().Level==1,"ASCEND arguments cannot bypass gathering");other.Awake=false;}
                Cmd("ascend");check(State().Level==n+1&&party.Members["b"].State.Level==n+1&&party.Members["b"].State.Room=="camp","Offline companion follows ascent without blocking campaign");
            }
        }
        for(int i=0;i<5;i++)Cmd($"set {State().Gods[i]} {i+1}");Cmd("invoke");
        check(State().Won&&party.World.Won&&party.World.Banked.Count==18,"Full co-op campaign can win with shared relics and final puzzle");
        Cooperation.Bind(party,party.Members["b"].State);check(party.Members["b"].State.Won,"Returning companion receives shared victory");
    }
}


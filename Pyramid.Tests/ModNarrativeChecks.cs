using System.Text.Json;
using System.Text.Json.Nodes;
using Pyramid.Web.Game;

static class ModNarrativeChecks
{
    public static void Run(Action<bool,string> check)
    {
        var directory=Path.Combine(Path.GetTempPath(),"pyramid-narrative-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var original=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Pyramid.Web/ContentPacks"));
        var fixture="""
        {"schemaVersion":1,"id":"narrative-test","name":"Narrative Test","startLevel":1,"levels":[{"number":1,"name":"Test Realm","objective":"Read changing text.","start":"camp","rooms":{"camp":{"name":"Camp","description":"The door is shut.","items":["bronze key"],"features":{"door":"The lock is shut.","tablet":"Permanent inscription."},"descriptionStates":[{"presentItems":["bronze key"],"description":"A key is here."},{"absentItems":["bronze key"],"absentFlags":["opened"],"description":"The key is gone; the door is shut."},{"requires":["opened"],"description":"The door is open.","features":{"door":"The open lock is settled."}},{"requires":["opened"],"absentItems":["bronze key"],"description":"The door is open and the key is elsewhere."}]}},"puzzles":[{"room":"camp","command":"unlock door","flag":"opened","needs":["bronze key"],"requires":[],"response":"Open.","hints":["Use the key."]}],"winFlags":[]}],"portals":[]}
        """;
        var file=Path.Combine(directory,"test.json");
        try
        {
            File.WriteAllText(file,fixture);ContentPacks.Load(directory);var state=Engine.New("narrative-test");
            check(ModEngine.Description(state)=="A key is here.","Conditional room description sees present loose item");
            Engine.Execute(state,"get bronze key");
            check(ModEngine.Description(state)=="The key is gone; the door is shut.","Taking item updates absentItems and absentFlags conditions");
            Engine.Execute(state,"unlock door");
            check(ModEngine.Description(state)=="The door is open and the key is elsewhere.","Last matching description wins and all rule conditions apply");
            check(ModEngine.Feature(state,"door")=="The open lock is settled."&&ModEngine.Feature(state,"tablet")=="Permanent inscription.","Matching feature overrides merge without erasing other features");
            Engine.Execute(state,"drop bronze key");
            check(ModEngine.Description(state)=="The door is open.","Dropped item updates conditions without undoing solved mechanism");
            var restored=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(state))!;ModEngine.Restore(restored);
            check(ModEngine.Description(restored)=="The door is open."&&ModEngine.Feature(restored,"door")=="The open lock is settled.","Narrative conditions derive from saved room state after reload");
            bool Reject(Action<JsonObject> change)
            {
                var document=JsonNode.Parse(fixture)!.AsObject();var rule=document["levels"]![0]!["rooms"]!["camp"]!["descriptionStates"]![0]!.AsObject();change(rule);File.WriteAllText(file,document.ToJsonString());
                try{ContentPacks.Load(directory);return false;}catch(InvalidOperationException){return true;}
            }
            check(Reject(r=>r["requires"]=new JsonArray("missing-flag")),"Narrative validation rejects unknown realm flags");
            check(Reject(r=>{r["requires"]=new JsonArray("opened");r["absentFlags"]=new JsonArray("opened");}),"Narrative validation rejects contradictory flag conditions");
            check(Reject(r=>r["absentItems"]=new JsonArray("bronze key")),"Narrative validation rejects contradictory item conditions");
            check(Reject(r=>r["presentItems"]=new JsonArray("../bad")),"Narrative validation rejects invalid item names");
            check(Reject(r=>r["description"]=""),"Narrative validation rejects empty replacement text");
            check(Reject(r=>r["features"]=new JsonObject{["door"]=""}),"Narrative validation rejects empty feature override");
            check(Reject(r=>r["absentFlags"]=null),"Narrative validation rejects null condition arrays");
            check(Reject(r=>r["features"]=null),"Narrative validation rejects null feature overrides");
            bool RejectRoom(string property,JsonNode? value)
            {
                var document=JsonNode.Parse(fixture)!;document["levels"]![0]!["rooms"]!["camp"]![property]=value;File.WriteAllText(file,document.ToJsonString());
                try{ContentPacks.Load(directory);return false;}catch(InvalidOperationException){return true;}
            }
            check(RejectRoom("descriptionStates",null),"Narrative validation rejects null rule list");
            check(RejectRoom("descriptionStates",new JsonArray(Enumerable.Range(0,41).Select(_=>(JsonNode)new JsonObject{["description"]="Rule"}).ToArray())),"Narrative validation bounds rule count");
            check(RejectRoom("exitKinds",new JsonObject{["up"]="stairs"}),"Passage kind requires an existing exit");
            var mapFixture=JsonNode.Parse(fixture)!;var mapRoom=mapFixture["levels"]![0]!["rooms"]!["camp"]!;mapRoom["exits"]=new JsonObject{["up"]="camp"};mapRoom["exitKinds"]=new JsonObject{["up"]="ladder"};File.WriteAllText(file,mapFixture.ToJsonString());ContentPacks.Load(directory);
            check(ContentPacks.Get("narrative-test").Levels[0].Rooms["camp"].ExitKinds["up"]=="ladder","Valid vertical passage kind is retained");
            mapRoom["exitKinds"]!["up"]="teleporter";File.WriteAllText(file,mapFixture.ToJsonString());var invalidKind=false;try{ContentPacks.Load(directory);}catch(InvalidOperationException){invalidKind=true;}
            check(invalidKind,"Unknown passage kinds are rejected");
            var legacy=JsonNode.Parse(fixture)!;legacy["levels"]![0]!["rooms"]!["camp"]!.AsObject().Remove("descriptionStates");File.WriteAllText(file,legacy.ToJsonString());ContentPacks.Load(directory);
            check(ModEngine.Description(Engine.New("narrative-test"))=="The door is shut.","Legacy packs without descriptionStates retain base prose");
        }
        finally {ContentPacks.Load(original);Directory.Delete(directory,true);}
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Pyramid.Web.Game;

static class ReadableChecks
{
    public static void Run(Action<bool,string> check)
    {
        var directory=Path.Combine(Path.GetTempPath(),"pyramid-readable-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var original=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Pyramid.Web/ContentPacks"));
        var fixture="""
        {"schemaVersion":1,"id":"reading-test","name":"Reading Test","readableItems":{"ancient scroll":"The river answers the moon.","bronze scroll":"Turn the eastern wheel.","secret letter":"A remote secret."},"startLevel":1,"levels":[{"number":1,"name":"Library","objective":"Read a scroll.","start":"camp","rooms":{"camp":{"name":"Camp","description":"Scrolls rest on a table.","exits":{"east":"vault","north":"dark"},"items":["ancient scroll","bronze scroll","stone"],"features":{"sign":"A fixed sign."}},"vault":{"name":"Vault","description":"A distant letter.","exits":{"west":"camp"},"items":["secret letter"]},"dark":{"name":"Dark room","description":"No light.","lit":false,"exits":{"south":"camp"}}},"puzzles":[],"winFlags":[]}],"portals":[]}
        """;
        var file=Path.Combine(directory,"readable.json");
        try
        {
            File.WriteAllText(file,fixture);ContentPacks.Load(directory);
            var s=Engine.New("reading-test");
            void Cmd(string command)=>Engine.Execute(s,command);
            bool Said(string text)=>s.Transcript.Last().Text.Contains(text,StringComparison.OrdinalIgnoreCase);
            Cmd("read scroll");check(s.Journal.Count==0&&Said("Which do you mean"),"Ambiguous readable shorthand requests full item name");
            Cmd("read secret letter");check(s.Journal.Count==0&&!s.Transcript.Last().Text.Contains("remote secret"),"Remote readable content cannot be read from another room");
            Cmd("examine ancient scroll");check(Said("READ ANCIENT SCROLL")&&s.Journal.Count==0,"Examine suggests reading without silently copying writing");
            Cmd("read ancient scroll");check(s.Journal.Count==1&&s.Journal[0].Contains("river answers"),"Read visible loose item copies authored text");
            check(s.Items["camp"].Contains("ancient scroll"),"Reading does not consume or take the item");
            int score=s.Score;Cmd("read ancient");check(s.Journal.Count==1&&Said("already")&&s.Score==score,"Unique shorthand reread cannot duplicate notes or farm points");
            Cmd("get ancient scroll");Cmd("east");Cmd("read scroll");check(Said("already"),"Carried readable item works away from its original room");
            Cmd("drop ancient scroll");Cmd("west");Cmd("read ancient scroll");check(Said("cannot find")&&s.Journal.Count==1,"Dropped item elsewhere cannot be read remotely");
            Cmd("get bronze scroll");Cmd("north");Cmd("read bronze scroll");check(s.Journal.Count==1&&Said("light"),"Darkness prevents reading carried writing");
            Cmd("retreat");Cmd("read stone");check(Said("no readable writing"),"Ordinary object explains that it has no authored writing");
            Cmd("read sign");check(Said("fixed sign"),"Existing READ feature behavior remains available");
            s.Journal=Enumerable.Range(0,100).Select(i=>"NOTE: "+i).ToList();Cmd("read bronze scroll");
            check(s.Journal.Count==100&&Said("not copied")&&s.Transcript[^2].Text=="Turn the eastern wheel.","Full journal still displays writing without exceeding 100 entries");
            var saved=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(s))!;check(saved.Journal.SequenceEqual(s.Journal),"Readable journal survives save round trip");
            foreach(var bad in new[]{"",new string('x',2001)})
            {
                var json=JsonNode.Parse(fixture)!;json["readableItems"]!["ancient scroll"]=bad;File.WriteAllText(file,json.ToJsonString());
                bool rejected=false;try{ContentPacks.Load(directory);}catch(InvalidOperationException){rejected=true;}check(rejected,"Readable validation rejects empty or excessive writing");
            }
            var legacy=JsonNode.Parse(fixture)!.AsObject();legacy.Remove("readableItems");File.WriteAllText(file,legacy.ToJsonString());ContentPacks.Load(directory);
            check(ContentPacks.Get("reading-test").ReadableItems.Count==0,"Legacy packs omit readable items without breaking");
            var builtIn=Engine.New();builtIn.Inventory.Add("star chart");Engine.Execute(builtIn,"read chart");
            check(builtIn.Journal.Count==1&&builtIn.Journal[0].Contains("ORION"),"Built-in star chart copies existing Orion clue");
            Engine.Execute(builtIn,"read star chart");check(builtIn.Journal.Count==1,"Built-in chart reread is deduplicated");
            builtIn.Inventory.Add("lamp");builtIn.LampOn=true;builtIn.Room="study";Engine.Execute(builtIn,"read tablet");
            check(builtIn.Journal.Contains(Engine.Tablet(builtIn,1)),"Built-in study tablet behavior remains unchanged");
            var tablet=Engine.New();tablet.Level=2;tablet.Room="west-shrine";tablet.LampOn=true;tablet.Inventory=["lamp"];
            tablet.Items=Engine.Current(tablet).Rooms.ToDictionary(r=>r.Key,r=>r.Value.Items.ToList());
            Engine.Execute(tablet,"examine lapis tablet");
            check(tablet.Transcript.Last().Text.Contains("READ LAPIS TABLET"),"Lapis tablet examination advertises readable writing");
            Engine.Execute(tablet,"read lapis tablet");
            check(tablet.Journal.Single().Contains(ReadableItems.LapisTabletText)&&tablet.Items["west-shrine"].Contains("lapis tablet"),"Loose lapis tablet can be read and journaled without taking it");
            Engine.Execute(tablet,"get lapis tablet");tablet.Room="study";Engine.Execute(tablet,"read tablet");
            check(tablet.Journal.Contains(Engine.Tablet(tablet,2)),"Carried lapis relic cannot intercept study READ TABLET clue");
            Engine.Execute(tablet,"read lapis tablet");
            check(tablet.Journal.Count==2&&tablet.Transcript.Last().Text.Contains("already"),"Explicit relic reading still works in study and does not duplicate notes");
            tablet.Room="camp";Engine.Execute(tablet,"deposit");Engine.Execute(tablet,"read lapis tablet");
            check(tablet.Banked.Contains("lapis tablet")&&!tablet.Inventory.Contains("lapis tablet")&&tablet.Journal.Count==2,"Reading preserves lapis tablet deposit behavior and saved journal");
        }
        finally{ContentPacks.Load(original);Directory.Delete(directory,true);}
        var oasis=Engine.New("oasis-of-stars");oasis.Level=2;oasis.Room="archive";ModEngine.Restore(oasis);Engine.Execute(oasis,"read chart");
        check(oasis.Journal.Count==1&&oasis.Journal[0].Contains("SET STARS THREE ONE TWO"),"Installed Oasis chart copies its own constellation clue from merged addon");
    }
}

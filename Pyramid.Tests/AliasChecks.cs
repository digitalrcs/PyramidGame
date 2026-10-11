using Pyramid.Web.Game;

static class AliasChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(var command in new[]{"feed ibis","FEED BIRD","feed the bird","give grain to ibis"})
        {
            var s=Engine.New();s.Room="east";s.Inventory=["lamp"];s.LampOn=true;
            Engine.Execute(s,command);
            check(!s.Flags.Contains("east-open")&&s.Solved==0&&s.Transcript.Last().Text.Contains("grain"),command+" requires grain");
            s.Inventory.Add("grain");Engine.Execute(s,command);
            check(s.Flags.Contains("east-open")&&s.Solved==1&&s.Score==25,command+" feeds living ibis");
            Engine.Execute(s,"feed bird");check(s.Solved==1&&s.Score==25,"Alias cannot repeat puzzle reward");
        }
        var wrong=Engine.New();wrong.Inventory=["lamp","grain"];wrong.LampOn=true;
        Engine.Execute(wrong,"feed bird");check(wrong.Solved==0,"Bird command requires correct room");
        var items=Engine.New();Engine.Execute(items,"take lantern");Engine.Execute(items,"light lantern");
        check(items.Inventory.Contains("lamp")&&items.LampOn,"Lantern alias works for taking and lighting");
        Engine.Execute(items,"examine lantern");check(items.Transcript.Last().Text.Contains("brass lamp"),"Lantern alias works for examining");
        Engine.Execute(items,"drop lantern");check(!items.Inventory.Contains("lamp")&&items.Items["camp"].Contains("lamp"),"Lantern alias works for dropping");
        items.Items["camp"].AddRange(["brass cog","iron cog"]);Engine.Execute(items,"get cog");
        check(items.Transcript.Last().Text.StartsWith("Which do you mean:"),"Ambiguous item names still require clarification");
        Engine.Execute(items,"get gear");check(items.Inventory.Contains("brass cog"),"Gear resolves to brass cog");
        var mirror=Engine.New();mirror.Level=3;mirror.Room="west";mirror.Inventory=["lamp","polishing cloth"];mirror.LampOn=true;
        Engine.Execute(mirror,"clean mirror");check(mirror.Flags.Contains("west-open"),"Clean mirror alias solves original puzzle");
    }
}

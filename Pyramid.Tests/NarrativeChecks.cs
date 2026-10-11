using Pyramid.Web.Game;

static class NarrativeChecks
{
    private static Expedition At(int number,string room)
    {
        var s=Engine.New();s.Level=number;s.Room=room;s.Inventory=["lamp"];s.LampOn=true;
        s.Items=World.Levels[number-1].Rooms.ToDictionary(r=>r.Key,r=>r.Value.Items.ToList());
        s.Transcript.Clear();return s;
    }
    private static string Look(Expedition s)
    {
        s.Transcript.Clear();Engine.Look(s);return string.Join("\n",s.Transcript.Select(x=>x.Text));
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(var level in World.Levels)
        foreach(var room in level.Rooms.Values)
        {
            var s=At(level.Number,room.Id);
            foreach(var item in room.Items)
            {
                check(Look(s).Contains(item),$"Room {level.Number}/{room.Id} lists loose {item}");
                Engine.Execute(s,"get "+item);
                // Clear inventory between pickups to exercise every item without a capacity failure.
                s.Inventory=["lamp"];
            }
            var empty=Look(s);
            check(empty.Contains("There are no loose objects here."),$"Room {level.Number}/{room.Id} reflects collected objects");
            // A drop into any room, not just an object's spawn room, must be visible and takeable.
            s.Inventory.Add("test keepsake");Engine.Execute(s,"drop test keepsake");
            check(Look(s).Contains("You see: test keepsake."),$"Room {level.Number}/{room.Id} lists dropped object");
            Engine.Execute(s,"get test keepsake");
            check(!Look(s).Contains("test keepsake"),$"Room {level.Number}/{room.Id} removes collected drop");
        }
        foreach(var level in World.Levels)
        foreach(var puzzle in level.Puzzles)
        {
            var s=At(level.Number,puzzle.Room);
            s.Inventory.AddRange(puzzle.Needs);s.Flags.UnionWith(puzzle.Requires);
            var before=RoomNarrative.Describe(s);
            Engine.Execute(s,puzzle.Command);
            check(s.Flags.Contains(puzzle.Flag)&&RoomNarrative.Describe(s)!=before,$"Solved {level.Number}/{puzzle.Command} updates scenery");
            Engine.Execute(s,"examine room");
            check(s.Transcript.Last().Text==RoomNarrative.Describe(s),$"EXAMINE reflects {level.Number}/{puzzle.Command}");
        }
        var basin=At(1,"west");basin.Inventory.Add("bottle");basin.Flags.Add("water");Engine.Execute(basin,"pour water");
        check(!Look(basin).Contains("dry",StringComparison.OrdinalIgnoreCase)&&Look(basin).Contains("Water fills"),"Restored basin is wet rather than dry");
        var ibis=At(1,"east");Engine.Execute(ibis,"get rope");
        check(!Look(ibis).Contains("rope",StringComparison.OrdinalIgnoreCase),"Taken rope is no longer hanging in cistern prose");
        ibis.Inventory.Add("grain");Engine.Execute(ibis,"feed bird");
        check(Look(ibis).Contains("eats contentedly")&&!Look(ibis).Contains("empty feeding bowl"),"Fed living ibis has a meal beside its separate stone carving");
        var bridge=At(4,"gallery");bridge.Inventory.Add("tuning fork");Engine.Execute(bridge,"sound fork");
        check(!Look(bridge).Contains("invisible",StringComparison.OrdinalIgnoreCase)&&Look(bridge).Contains("visible in vibrating dust"),"Revealed bridge no longer described as invisible");
        var gate=At(1,"gate");gate.Banked.UnionWith(World.Levels[0].Relics);gate.Journal.Add(Engine.Tablet(gate,1));Engine.Execute(gate,"say dawn");
        check(Look(gate).Contains("All three offerings are secured")&&Look(gate).Contains("The way is ready. ASCEND."),"Completed gate reports deposited offerings and accepted answer");
        var throne=At(6,"gate");Engine.Execute(throne,"set ra 2");
        check(Look(throne).Contains("2: ra")&&!Look(throne).Contains("Five empty niches"),"Throne describes occupied niches");
    }
}

namespace Pyramid.Web.Game;
public static class ModEngine
{
    public static Realm Realm(Expedition s)=>ContentPacks.Get(s.PackId).Levels.Single(l=>l.Number==s.Level);
    public static void Store(Expedition s)
    {
        s.Realms[s.Level]=new(){Items=s.Items,Flags=s.Flags};
        s.VisitedRealms[s.Level]=s.Visited;
    }
    public static void Restore(Expedition s)
    {
        var realm=Realm(s);
        if(!realm.Rooms.ContainsKey(s.Room))throw new InvalidOperationException("This saved room is missing from the installed content pack. Restore the previous pack; your save is retained.");
        if(!s.Realms.TryGetValue(s.Level,out var save)){save=new(){Items=Realm(s).Rooms.ToDictionary(r=>r.Key,r=>r.Value.Items.ToList())};s.Realms[s.Level]=save;}
        foreach(var room in realm.Rooms)save.Items.TryAdd(room.Key,room.Value.Items.ToList());
        if(s.VisitedRealms.TryGetValue(s.Level,out var visited))s.Visited=visited;
        s.Visited.RemoveWhere(id=>!realm.Rooms.ContainsKey(id));
        s.Visited.Add(s.Room);s.VisitedRealms[s.Level]=s.Visited;
        s.Items=save.Items;s.Flags=save.Flags;
    }
    public static Expedition New(string pack)
    {
        var content=ContentPacks.Get(pack);var s=new Expedition{PackId=pack,Level=content.StartLevel};s.Room=Realm(s).Start;s.Visited=[s.Room];Restore(s);Look(s);return s;
    }
    public static void Look(Expedition s)
    {
        if(!Engine.Lit(s)){Engine.Say(s,"Darkness hides this room. LIGHT LAMP or RETREAT.");return;}
        var room=Realm(s).Rooms[s.Room];var portals=ContentPacks.Get(s.PackId).Portals.Where(p=>p.FromLevel==s.Level&&p.FromRoom==s.Room).ToArray();
        Engine.Say(s,room.Name+"\n"+room.Description+"\n"+string.Join("\n",portals.Select(p=>$"{p.Description} Direction: {p.Direction}."))+"\nExits: "+string.Join(", ",room.Exits.Keys.Concat(portals.Select(p=>p.Direction)))+".\n"+(s.Items[s.Room].Count>0?"You see: "+string.Join(", ",s.Items[s.Room])+".":"There are no loose objects here."));
    }
    public static void Execute(Expedition s,string raw)
    {
        string command=string.Join(' ',raw.Trim().ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
        var words=command.Split(' ',2);string verb=words[0],noun=words.Length>1?words[1]:"";
        var realm=Realm(s);var room=realm.Rooms[s.Room];
        Engine.Say(s,command,"command");s.Actions++;
        void Say(string text)=>Engine.Say(s,text);
        string direction=verb=="go"?noun:verb;direction=direction switch{"n"=>"north","s"=>"south","e"=>"east","w"=>"west","u"=>"up","d"=>"down",_=>direction};
        if(command is "help" or "?")Say("LOOK | N S E W UP DOWN | GET <item> | DROP <item> | EXAMINE <feature> | INVENTORY | NOTE <text> | JOURNAL | MAP | CLEAR | HINT | RETREAT | LIGHT LAMP. MAP costs 2 points per opening. CLEAR erases only the transcript. Follow descriptions for puzzle commands. Portals lead to other realms; each realm remembers its objects and mechanisms.");
        else if(command is "look" or "l")Look(s);
        else if(command is "inventory" or "i")Say("Carrying: "+string.Join(", ",s.Inventory));
        else if(command=="journal")Say(string.Join("\n",s.Journal));
        else if(verb=="note"){if(noun.Length>0&&s.Journal.Count<100){s.Journal.Add("NOTE: "+noun);Say("Observation recorded.");}else Say("Your journal holds up to 100 notes.");}
        else if(command=="save")Say("Every action is saved automatically.");
        else if(command=="retreat"){s.Room=realm.Start;s.Visited.Add(s.Room);s.Moves++;Look(s);}
        else if(command is "light lamp" or "off lamp"){if(s.Inventory.Contains("lamp")){s.LampOn=command=="light lamp";Say(s.LampOn?"The lamp is lit.":"The lamp is extinguished.");}else Say("You need a lamp.");}
        else if(!Engine.Lit(s))Say("You need light to act here.");
        else if(direction is "north" or "south" or "east" or "west" or "up" or "down")
        {
            var portal=ContentPacks.Get(s.PackId).Portals.FirstOrDefault(p=>p.FromLevel==s.Level&&p.FromRoom==s.Room&&p.Direction==direction);
            if(portal!=null)
            {
                if(portal.Requires!=null&&!s.Flags.Contains(portal.Requires))Say("The portal is sealed. Study this realm's mechanisms.");
                else{Store(s);s.Level=portal.ToLevel;s.Room=portal.ToRoom;s.Visited=[];Restore(s);s.Moves++;Look(s);Exploration.RewardDiscovery(s);}
            }
            else if(!room.Exits.TryGetValue(direction,out var next))Say("There is no passage that way.");
            else if(room.Requires.TryGetValue(direction,out var flag)&&!s.Flags.Contains(flag))Say("That passage is sealed.");
            else{s.Room=next;s.Visited.Add(next);s.Moves++;Look(s);Exploration.RewardDiscovery(s);}
        }
        else if(verb is "get" or "take")
        {
            if(!s.Items[s.Room].Contains(noun))Say("Use the exact name of an object in this room.");
            else if(s.Inventory.Count>=8)Say("Your satchel is full.");
            else{s.Items[s.Room].Remove(noun);s.Inventory.Add(noun);Say("Taken: "+noun);}
        }
        else if(verb=="drop") {if(s.Inventory.Remove(noun)){s.Items[s.Room].Add(noun);Say("Dropped: "+noun);}else Say("You do not carry that object.");}
        else if(verb is "read" or "examine" or "x")
        {
            if(room.Features.TryGetValue(noun,out var description))Say(description);
            else Say(room.Description);
        }
        else if(command=="hint")
        {
            var puzzle=realm.Puzzles.FirstOrDefault(p=>p.Room==s.Room&&!s.Flags.Contains(p.Flag));
            if(puzzle==null)Say("Explore the realm and examine its features.");
            else{string key=$"{s.Level}:{puzzle.Flag}";int n=s.Hints.GetValueOrDefault(key);s.Hints[key]=n+1;s.HintsUsed++;Say(puzzle.Hints[Math.Min(n,puzzle.Hints.Length-1)]);}
        }
        else
        {
            var puzzle=realm.Puzzles.FirstOrDefault(p=>p.Room==s.Room&&p.Command==command);
            if(puzzle==null)Say("That action finds no answer here. EXAMINE the surroundings or HELP.");
            else if(s.Flags.Contains(puzzle.Flag))Say("That mechanism is already settled.");
            else if(!puzzle.Needs.All(s.Inventory.Contains))Say("You must carry: "+string.Join(", ",puzzle.Needs));
            else if(!puzzle.Requires.All(s.Flags.Contains))Say("Something must be prepared first.");
            else{s.Flags.Add(puzzle.Flag);s.Solved++;Say(puzzle.Response);s.Achievements.Add($"{realm.Name}: {puzzle.Flag}");}
        }
        if(realm.WinFlags.Length>0&&realm.WinFlags.All(s.Flags.Contains)&&s.Level==realm.Number&&!s.Achievements.Contains(realm.Name+" complete")){s.Achievements.Add(realm.Name+" complete");Say("REALM COMPLETE: "+realm.Name+". You may continue exploring.");}
        Store(s);s.Revision++;if(s.Transcript.Count>180)s.Transcript.RemoveRange(0,s.Transcript.Count-180);
    }
}


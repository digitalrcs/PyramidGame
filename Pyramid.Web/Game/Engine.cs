using System.Security.Cryptography;

namespace Pyramid.Web.Game;

public record Entry(string Kind,string Text);
public class Expedition
{
    public string PackId {get;set;}="pyramid";
    public Dictionary<int,RealmSave> Realms {get;set;}=[];
    public int Version {get;set;}=1;
    public int Level {get;set;}=1;
    public string Room {get;set;}="camp";
    public List<string> Inventory {get;set;}=[];
    public Dictionary<string,List<string>> Items {get;set;}=[];
    public HashSet<string> Flags {get;set;}=[];
    public HashSet<string> Visited {get;set;}=[];
    public Dictionary<int,HashSet<string>> VisitedRealms {get;set;}=[];
    public MapSnapshot? MapSnapshot {get;set;}
    public HashSet<string> Banked {get;set;}=[];
    public HashSet<string> Achievements {get;set;}=[];
    public List<string> Journal {get;set;}=[];
    public List<Entry> Transcript {get;set;}=[];
    public Dictionary<string,int> Hints {get;set;}=[];
    public string[] Gods {get;set;}=[];
    public Dictionary<string,int> Arrangement {get;set;}=[];
    public int Moves {get;set;}
    public int Actions {get;set;}
    public int Solved {get;set;}
    public int HintsUsed {get;set;}
    public int MapsUsed {get;set;}
    public int BonusPoints {get;set;}
    public HashSet<string> RewardKeys {get;set;}=[];
    public long Revision {get;set;}
    public bool LampOn {get;set;}
    public bool Won {get;set;}
    public DateTimeOffset Started {get;set;}=DateTimeOffset.UtcNow;
    public DateTimeOffset? Finished {get;set;}
    public int Score => Math.Max(0,Banked.Count*20+Solved*25+(PackId=="pyramid"?(Level-1)*100:0)+(Won?500:0)+BonusPoints-HintsUsed*5-MapsUsed*2);
}

public static class Engine
{
    public static Expedition New(string pack="pyramid")
    {
        if(pack!="pyramid")return ModEngine.New(pack);
        var s=new Expedition {Gods=["ra","isis","thoth","anubis","osiris"]};
        RandomNumberGenerator.Shuffle(s.Gods.AsSpan());
        EnterLevel(s);
        Say(s,"Your expedition is private; other explorers can see only your location and accomplishments. Type HELP for commands. Every action is saved.");
        Look(s);
        return s;
    }
    public static Level Current(Expedition s)=>s.PackId=="pyramid"?World.Levels[s.Level-1]:ModEngine.Realm(s).AsLevel();
    public static bool Lit(Expedition s)=>(s.PackId=="pyramid"?s.Room=="camp":ModEngine.Realm(s).Rooms[s.Room].Lit)||(s.LampOn&&(s.Inventory.Contains("lamp")||s.Items[s.Room].Contains("lamp")));
    public static void Say(Expedition s,string message,string kind="response")=>s.Transcript.Add(new(kind,message));
    public static void Reward(Expedition s,string key,int points,string message)
    {
        if(points>0&&s.RewardKeys.Add(key)){s.BonusPoints+=points;Say(s,$"{message} +{points} bonus points.","milestone");}
    }
    private static void EnterLevel(Expedition s)
    {
        s.Room="camp";s.Flags=[];s.Visited=["camp"];
        s.Items=Current(s).Rooms.ToDictionary(r=>r.Key,r=>r.Value.Items.ToList());
        if(s.Level>1){s.Inventory=["lamp"];s.LampOn=true;}
    }
    public static string Tablet(Expedition s,int number)=> number switch {
        1=>$"TABLET I: {s.Gods[2]} stands at neither edge of the five niches.",
        2=>$"TABLET II: {s.Gods[3]} stands immediately to the right of {s.Gods[2]}.",
        3=>$"TABLET III: {s.Gods[4]} stands exactly two places to the right of {s.Gods[2]}.",
        4=>$"TABLET IV: {s.Gods[0]} stands somewhere to the left of {s.Gods[4]}.",
        _=>$"TABLET V: {s.Gods[1]} stands somewhere between {s.Gods[0]} and {s.Gods[2]}."
    };
    public static void Look(Expedition s)
    {
        if(s.PackId!="pyramid"){ModEngine.Look(s);return;}
        if(!Lit(s)){Say(s,"DARKNESS HIDES THE PASSAGE. Light your lamp, or RETREAT to the landing; there is no need to lose your expedition.");return;}
        var room=Current(s).Rooms[s.Room];
        Say(s,room.Name+"\n"+room.Description+"\nExits: "+string.Join(", ",room.Exits.Keys)+".\n"+(s.Items[s.Room].Count>0?"You see: "+string.Join(", ",s.Items[s.Room])+".":"There are no loose objects here."));
        if(s.Flags.Count>0) Say(s,"Opened routes: "+string.Join(", ",new[]{("west-open","western shrine"),("east-open","eastern shrine"),("north-open","gallery crossing")}.Where(x=>s.Flags.Contains(x.Item1)).Select(x=>x.Item2))+".");
    }
    private static string? Resolve(Expedition s,string noun,IEnumerable<string> items)
    {
        var choices=items.Distinct().ToArray();
        if(choices.Contains(noun))return noun;
        var matches=choices.Where(x=>noun.Length>0&&x.Split(' ').Contains(noun)).ToArray();
        if(matches.Length==1)return matches[0];
        Say(s,matches.Length>1?"Which do you mean: "+string.Join(", ",matches)+"?":"You cannot find that object here.");
        return null;
    }
    public static void Execute(Expedition s,string raw)
    {
        if(Exploration.Execute(s,raw))return;
        if(s.PackId!="pyramid"){ModEngine.Execute(s,raw);return;}
        var command=string.Join(' ',raw.Trim().ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
        if(command.Length==0)return;
        Say(s,command,"command");
        s.Actions++;
        var parts=command.Split(' ',2);var verb=parts[0];var noun=parts.Length>1?parts[1]:"";
        var level=Current(s);
        var aliases=new Dictionary<string,string>{{"n","north"},{"s","south"},{"e","east"},{"w","west"}};
        string direction=verb=="go"?noun:verb;
        direction=aliases.GetValueOrDefault(direction,direction);
        if(verb is "help" or "?") Say(s,"LOOK / L | N S E W / GO NORTH | GET <OBJECT> | DROP <OBJECT> | EXAMINE / READ <THING> | LIGHT LAMP / OFF LAMP | INVENTORY / I | DEPOSIT (at the landing) | JOURNAL | MAP | CLEAR | HINT | RETREAT | SAY <ANSWER> | ASCEND. Puzzle verbs are suggested by descriptions: FILL, POUR, TIE, OIL, TURN, PLACE, LAY, FIT, SET, STRIKE, PLAY, SOUND, REFLECT, SHOW. In the final throne: SET <GOD> <1-5>, then INVOKE. Every accepted command is saved. Hints cost 5 points; MAP costs 2 points each time. CLEAR erases only the transcript. There is no timer.");
        else if(verb is "journal") Say(s,s.Journal.Count>0?string.Join("\n\n",s.Journal):"Your journal is empty. READ TABLET in each study. NOTE <TEXT> records your own observations.");
        else if(verb=="note") {if(noun.Length>0&&s.Journal.Count<100){s.Journal.Add("NOTE: "+noun);Say(s,"Observation recorded.");}else Say(s,"Write NOTE followed by your observation (up to 100 journal entries).");}
        else if(verb is "inventory" or "i") Say(s,s.Inventory.Count>0?"Carrying: "+string.Join(", ",s.Inventory):"You carry nothing.");
        else if(verb=="score") Say(s,$"Score {s.Score}. Relics {s.Banked.Count}/18. Moves {s.Moves}. Puzzles solved {s.Solved}. Hints {s.HintsUsed}.");
        else if(verb=="save") Say(s,"Your expedition is saved automatically after each command. Sign in with the same account to resume.");
        else if(verb=="retreat") {s.Room="camp";s.Moves++;Say(s,"You follow the safety cord back to this level's landing. Your belongings remain where you left them.");Look(s);}
        else if(command is "light lamp" or "on lamp" or "off lamp")
        {if(s.Inventory.Contains("lamp")||s.Items[s.Room].Contains("lamp")){s.LampOn=command!="off lamp";Say(s,s.LampOn?"The lamp is lit.":"The lamp is extinguished.");if(s.LampOn)s.Achievements.Add("Light the way");}else Say(s,"The lamp is not here.");}
        else if(verb is "look" or "l" && noun.Length==0) Look(s);
        else if(s.Won) Say(s,"Your expedition is complete. You may explore your journal, map, and score. The gods remember your name.");
        else if(!Lit(s)) Say(s,"You need light to act safely. LIGHT LAMP if it is here, or RETREAT to the landing.");
        else if(direction is "north" or "south" or "east" or "west")
        {
            if(!level.Rooms[s.Room].Exits.TryGetValue(direction,out var next)) Say(s,"There is no passage that way.");
            else if((next=="west-shrine"&&!s.Flags.Contains("west-open"))||(next=="east-shrine"&&!s.Flags.Contains("east-open"))||(next=="north-shrine"&&!s.Flags.Contains("north-open"))) Say(s,"The way is sealed. Examine the mechanism and the objects around you.");
            else {s.Room=next;s.Moves++;s.Visited.Add(next);s.Achievements.Add("First steps");if(s.Visited.Count==10)s.Achievements.Add($"Cartographer {s.Level}");Look(s);Exploration.RewardDiscovery(s);}
        }
        else if(verb is "get" or "take")
        {
            var item=Resolve(s,noun,s.Items[s.Room]);
            if(item!=null){if(s.Inventory.Count>=8)Say(s,"Your satchel holds eight objects. Drop a tool or deposit your relics.");else{s.Items[s.Room].Remove(item);s.Inventory.Add(item);Say(s,"Taken: "+item+".");}}
        }
        else if(verb=="drop") {var item=Resolve(s,noun,s.Inventory);if(item!=null){s.Inventory.Remove(item);s.Items[s.Room].Add(item);Say(s,"Dropped: "+item+".");}}
        else if(verb=="deposit")
        {
            if(s.Room!="camp")Say(s,"Relics must be deposited at this level's landing.");
            else {var relics=s.Inventory.Where(x=>level.Relics.Contains(x)).ToArray();foreach(var item in relics){s.Inventory.Remove(item);s.Banked.Add(item);}Say(s,relics.Length>0?"Secured: "+string.Join(", ",relics)+". +20 points each.":"You have no relics to deposit.");if(relics.Length>0)s.Achievements.Add("Relic keeper");}
        }
        else if(verb is "read" or "examine" or "x" or "look") Examine(s,noun);
        else if(verb=="hint")
        {
            string key=$"{s.Level}:{s.Room}";int index=s.Hints.GetValueOrDefault(key);
            s.Hints[key]=index+1;s.HintsUsed++;
            var puzzle=level.Puzzles.FirstOrDefault(p=>p.Room==s.Room&&!s.Flags.Contains(p.Flag));
            Say(s,puzzle!=null?puzzle.Hints[Math.Min(index,2)]:s.Room=="gate"?(s.Level==6?"The five journal tablets imply one ordering. The god named in Tablet I belongs in niche 3. Complete the three offerings, SAY ETERNITY, then SET each god and INVOKE.":$"Deposit all three relics at the landing. READ TABLET in the study. SAY {level.Word.ToUpperInvariant()} here, then ASCEND."):"Explore every branch. Read room descriptions and EXAMINE objects. The study contains a tablet and a stair riddle. Bring tools between branches.");
        }
        else if(s.Room=="gate"&&verb=="say"&&noun==level.Word){if(s.Flags.Add("word"))s.Solved++;Say(s,"The ancient word is accepted. The gate awaits the recovered relics.");}
        else if(verb=="ascend")
        {
            if(s.Level==6)Say(s,"There is no higher chamber. Solve the throne and INVOKE.");
            else if(s.Room!="gate")Say(s,"The ascent is at the northern gate.");
            else if(!level.Relics.All(s.Banked.Contains)||!s.Flags.Contains("word")||!s.Journal.Contains(Tablet(s,s.Level))) Say(s,"The gate requires three deposited relics, the spoken riddle answer, and this level's tablet in your journal.");
            else {s.Achievements.Add($"Level {s.Level} complete");Say(s,$"LEVEL {s.Level} COMPLETE. Your tools remain in the old chambers; your lamp and journal travel with you. +100 points.","milestone");s.Level++;EnterLevel(s);Look(s);}
        }
        else if(s.Level==6&&s.Room=="gate"&&verb=="set")
        {
            var args=noun.Split(' ');
            if(args.Length!=2||!s.Gods.Contains(args[0])||!int.TryParse(args[1],out int slot)||slot<1||slot>5) Say(s,"Use SET <GOD> <1-5>. Names: RA, ISIS, THOTH, ANUBIS, OSIRIS.");
            else {foreach(var key in s.Arrangement.Where(x=>x.Value==slot).Select(x=>x.Key).ToArray())s.Arrangement.Remove(key);s.Arrangement[args[0]]=slot;Say(s,$"{args[0]} now occupies niche {slot}.\n"+string.Join(" | ",Enumerable.Range(1,5).Select(n=>$"{n}: {s.Arrangement.FirstOrDefault(x=>x.Value==n).Key??"empty"}")));}
        }
        else if(verb=="invoke")
        {
            if(s.Level!=6||s.Room!="gate")Say(s,"Only the final throne can hear the invocation.");
            else if(!level.Relics.All(s.Banked.Contains)||!s.Flags.Contains("word"))Say(s,"The offerings are incomplete, or the final riddle remains unanswered.");
            else if(!s.Gods.Select((god,i)=>s.Arrangement.GetValueOrDefault(god)==i+1).All(x=>x))Say(s,"The gods remain silent. At least one tablet contradicts this order. You may rearrange the niches without losing your offerings.");
            else {s.Won=true;s.Finished=DateTimeOffset.UtcNow;s.Achievements.Add("Chamber of the Gods");s.Achievements.Add("Level 6 complete");if(s.HintsUsed==0)s.Achievements.Add("Unaided ascension");Say(s,"THE FIVE NAMES BECOME ONE. Stone gives way to morning. You have returned every offering, restored the divine order, and won the pyramid. Your expedition will remain in the explorers' record.","milestone");}
        }
        else
        {
            var puzzle=level.Puzzles.FirstOrDefault(p=>p.Room==s.Room&&p.Command==command);
            if(puzzle==null)Say(s,"That action finds no answer here. Try EXAMINE, READ, or HELP. Short commands work best.");
            else if(s.Flags.Contains(puzzle.Flag))Say(s,"That mechanism is already settled.");
            else if(!puzzle.Needs.All(s.Inventory.Contains))Say(s,"You need to carry: "+string.Join(", ",puzzle.Needs.Where(x=>!s.Inventory.Contains(x)))+".");
            else if(!puzzle.Requires.All(s.Flags.Contains))Say(s,"Something must be prepared first. Examine the mechanism.");
            else{s.Flags.Add(puzzle.Flag);s.Solved++;Say(s,puzzle.Response+" +25 points.");s.Achievements.Add("Problem solver");}
        }
        if(s.Transcript.Count>180)s.Transcript.RemoveRange(0,s.Transcript.Count-180);
        s.Revision++;
    }
    private static void Examine(Expedition s,string noun)
    {
        var level=Current(s);
        if(s.Room=="study"&&noun is "tablet" or "inscription")
        {
            if(s.Level<=5){string clue=Tablet(s,s.Level);if(!s.Journal.Contains(clue))s.Journal.Add(clue);Say(s,clue+" Copied into your journal.");s.Achievements.Add("Keeper of notes");}
            else Say(s,level.Rooms["study"].Description);
            return;
        }
        if(noun is "room" or "wall" or "mechanism" or "inscription" or "riddle"){Say(s,level.Rooms[s.Room].Description);return;}
        var present=s.Inventory.Concat(s.Items[s.Room]).ToArray();
        if(noun=="chart"||noun=="star chart") {Say(s,present.Contains("star chart")?"The chart marks ORION as the constellation that opens the dome.":"You do not have the chart here.");return;}
        if(noun=="lamp"){Say(s,present.Contains("lamp")?"A steady brass lamp. LIGHT LAMP illuminates your current room. It needs no refilling.":"The lamp is elsewhere.");return;}
        if(present.Any(x=>x==noun||x.Split(' ').Contains(noun))){Say(s,"The "+Resolve(s,noun,present)+" may be useful elsewhere. Read the mechanisms for signs of its purpose.");return;}
        // Scenery is described without revealing remote objects or solutions.
        if(noun.Length>1&&level.Rooms[s.Room].Description.Contains(noun,StringComparison.OrdinalIgnoreCase))Say(s,level.Rooms[s.Room].Description);
        else Say(s,"You see no such thing here. EXAMINE ROOM describes the visible mechanisms.");
    }
    public static object View(Expedition s)=>new {
        s.PackId,totalRelics=s.PackId=="pyramid"?18:0,roomCount=Current(s).Rooms.Count,offeringCount=Current(s).Relics.Length,
        s.Level,levelName=Current(s).Name,room=Current(s).Rooms[s.Room].Name,roomId=s.Room,roomLit=Lit(s),
        roomNumber=s.PackId=="pyramid"?World.RoomNumber(s.Level,s.Room):Array.IndexOf(Current(s).Rooms.Keys.ToArray(),s.Room)+1,
        roomImage=Lit(s)?(s.PackId=="pyramid"?$"room-art/room{World.RoomNumber(s.Level,s.Room)}.png":ModEngine.Realm(s).Rooms[s.Room].Image):null,
        objective=Current(s).Objective,inventory=s.Inventory,score=s.Score,s.Moves,s.Actions,
        relics=s.Banked.Count,levelRelics=Current(s).Relics.Count(s.Banked.Contains),s.Solved,s.HintsUsed,s.MapsUsed,s.BonusPoints,
        mapSnapshot=s.MapSnapshot,roomActivity=Exploration.RoomActivity(s),
        s.Revision,s.Won,s.Started,s.Finished,s.Transcript,s.Journal,s.Achievements,
        visited=s.Visited.Select(id=>new{id,name=Current(s).Rooms[id].Name}),
        exits=Lit(s)?Current(s).Rooms[s.Room].Exits.Keys.ToArray():[],
        items=Lit(s)?s.Items[s.Room]:[],arrangement=s.Arrangement,
        levels=(s.PackId=="pyramid"?World.Levels:ContentPacks.Get(s.PackId).Levels.Select(l=>l.AsLevel())).Select(l=>new{l.Number,l.Name}),
        progress=s.PackId!="pyramid"?ContentPacks.Get(s.PackId).Levels.Count(l=>s.Achievements.Contains(l.Name+" complete"))*100/ContentPacks.Get(s.PackId).Levels.Count:s.Won?100:Math.Min(99,((s.Level-1)*3+Current(s).Relics.Count(s.Banked.Contains))*100/18)
    };
}


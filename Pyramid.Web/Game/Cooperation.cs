namespace Pyramid.Web.Game;

public class Companion
{
    public string Name {get;set;}="";
    public Expedition State {get;set;}=new();
    public DateTimeOffset Seen {get;set;}=DateTimeOffset.MinValue;
    public bool Awake {get;set;}
    public Dictionary<string,string> Offers {get;set;}=[];
    public bool Online => Awake && Seen>DateTimeOffset.UtcNow.AddSeconds(-45);
}
public class Party
{
    public string Id {get;set;}="";
    public string Owner {get;set;}="";
    public string Name {get;set;}="";
    public bool Open {get;set;}
    public long Revision {get;set;}
    public Expedition World {get;set;}=new();
    public Dictionary<string,Companion> Members {get;set;}=[];
    public Dictionary<string,string> Invitations {get;set;}=[];
    public Dictionary<string,string> Requests {get;set;}=[];
}

public static class Cooperation
{
    public static void Bind(Party party,Expedition player)
    {
        var world=party.World;
        player.Items=world.Items;player.Flags=world.Flags;player.Banked=world.Banked;
        player.Gods=world.Gods;player.Arrangement=world.Arrangement;
        player.Solved=world.Solved;player.Won=world.Won;player.Finished=world.Finished;
        player.Realms=world.Realms;
        if(player.PackId!="pyramid")ModEngine.Restore(player);
        player.Revision=party.Revision;
    }
    public static void Publish(Party party,Expedition player)
    {
        if(player.PackId!="pyramid")ModEngine.Store(player);
        var world=party.World;
        world.Items=player.Items;world.Flags=player.Flags;world.Banked=player.Banked;
        world.Arrangement=player.Arrangement;world.Solved=player.Solved;world.Won=player.Won;
        world.Finished=player.Finished;world.Realms=player.Realms;world.Level=player.Level;
    }
    public static void Tell(Companion p,string text)
    {
        Engine.Say(p.State,text,"social");
        if(p.State.Transcript.Count>180)p.State.Transcript.RemoveRange(0,p.State.Transcript.Count-180);
    }
    public static bool Together(Companion a,Companion b)=>a.State.Level==b.State.Level&&a.State.Room==b.State.Room;
    public static void Look(Party party,Companion player)
    {
        var others=party.Members.Values.Where(p=>p!=player&&Together(player,p)).Select(p=>p.Name+(p.Online?" is here too.":" is standing still, in a trance.")).ToArray();
        if(others.Length>0)Tell(player,string.Join("\n",others));
    }
    public static void Execute(Party party,string id,string raw)
    {
        var player=party.Members[id];var s=player.State;
        Bind(party,s);var oldLevel=s.Level;var oldRoom=s.Room;
        string command=string.Join(' ',raw.Trim().ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
        if(command.Split(' ')[0]=="ascend"&&s.PackId=="pyramid"&&party.Members.Values.Any(p=>p!=player&&p.Online&&(p.State.Level!=s.Level||p.State.Room!="gate")))
            Tell(player,"Gather every awake companion at the gate before ascending. Companions in a trance travel with the party.");
        else if(!Social(party,player,raw,command))Engine.Execute(s,raw);
        Publish(party,s);
        if(s.PackId=="pyramid"&&s.Level!=oldLevel)
        {
            foreach(var other in party.Members.Values.Where(p=>p!=player))
            {
                other.State.Level=s.Level;other.State.Room=s.Room;other.State.Inventory=["lamp"];other.State.LampOn=true;other.State.Visited=[s.Room];other.Offers.Clear();
                Bind(party,other.State);Tell(other,$"The expedition ascends together to {Engine.Current(s).Name}.");Engine.Look(other.State);
            }
        }
        if(oldRoom!=s.Room||oldLevel!=s.Level)
        {
            player.Offers.Clear();
            foreach(var other in party.Members.Values.Where(p=>p!=player))
            {
                if(other.State.Level==oldLevel&&other.State.Room==oldRoom)Tell(other,$"{player.Name} has left the room.");
                if(Together(player,other))Tell(other,$"{player.Name} has entered the room.");
            }
            Look(party,player);
        }
        else if(command is "look" or "l")Look(party,player);
        if(command is "help" or "?")Tell(player,"Together: SHARE ITEM <item> WITH <name> offers an item; GET <item> FROM <name> accepts it. SHARE INFORMATION <text> speaks to awake companions in this room. SHARE JOURNAL WITH <name> copies your notes. OFFERS lists items offered to you. Shared doors, relics and puzzles are saved for everyone. Use the EXPEDITIONS panel to invite, join or resume.");
        party.Revision++;
    }
    private static bool Social(Party party,Companion player,string raw,string command)
    {
        var s=player.State;
        Companion? Target(string name)=>party.Members.Values.FirstOrDefault(p=>p!=player&&p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
        bool Ready(Companion? p){if(p==null||!Together(player,p)||!p.Online){Tell(player,"That explorer must be awake in the same room.");return false;}return true;}
        if(command.StartsWith("share information ")||command.StartsWith("say to room "))
        {
            string normalized=System.Text.RegularExpressions.Regex.Replace(raw.Trim(),"\\s+"," ");
            string message=normalized[(command.StartsWith("share information ")?18:12)..].Trim();
            var others=party.Members.Values.Where(p=>p!=player&&p.Online&&Together(player,p)).ToArray();
            if(others.Length==0)Tell(player,"No awake companion is here to hear you.");
            else{foreach(var p in others)Tell(p,$"{player.Name} shares: {message}");Tell(player,$"You share: {message}");}
            return true;
        }
        if(command.StartsWith("share journal with "))
        {
            var other=Target(command[19..]);if(!Ready(other))return true;
            int oldNotes=other!.State.Journal.Count;
            foreach(var note in s.Journal.Where(n=>!other!.State.Journal.Contains(n)).Take(Math.Max(0,100-other!.State.Journal.Count)))other.State.Journal.Add(note);
            if(other.State.Journal.Count>oldNotes)Engine.Reward(s,"share-journal",3,"New knowledge shared with a companion.");
            Tell(other!,$"{player.Name} has shared their journal with you.");Tell(player,"Your notes have been copied into your companion's journal.");return true;
        }
        if(command=="offers")
        {
            var offers=party.Members.Values.Where(p=>p!=player&&p.Online&&Together(player,p)).SelectMany(p=>p.Offers.Where(o=>o.Value==player.Name&&p.State.Inventory.Contains(o.Key)).Select(o=>$"{p.Name} offers {o.Key}. GET {o.Key} FROM {p.Name}"));
            Tell(player,string.Join("\n",offers.DefaultIfEmpty("No items are offered to you here.")));return true;
        }
        if(command.StartsWith("share item "))
        {
            var split=command[11..].Split(" with ",2);if(split.Length!=2){Tell(player,"Use SHARE ITEM <exact item name> WITH <explorer>.");return true;}
            var other=Target(split[1]);if(!Ready(other))return true;
            if(!s.Inventory.Contains(split[0])){Tell(player,"You do not carry that item.");return true;}
            player.Offers[split[0]]=other!.Name;Tell(player,$"Offered {split[0]} to {other.Name}. It remains yours until accepted.");Tell(other,$"{player.Name} offers {split[0]}. GET {split[0]} FROM {player.Name} to accept.");return true;
        }
        if((command.StartsWith("get ")||command.StartsWith("take "))&&command.Contains(" from "))
        {
            var split=command[(command.StartsWith("get ")?4:5)..].Split(" from ",2);var other=Target(split[1]);if(!Ready(other))return true;
            if(other!.Offers.GetValueOrDefault(split[0])!=player.Name||!other.State.Inventory.Contains(split[0]))Tell(player,"That explorer has not offered you this item. Ask them to SHARE ITEM first.");
            else if(s.Inventory.Count>=8)Tell(player,"Your satchel is full.");
            else
            {
                other.State.Inventory.Remove(split[0]);s.Inventory.Add(split[0]);other.Offers.Remove(split[0]);
                Tell(other,$"{player.Name} accepts your {split[0]}.");Tell(player,$"Received {split[0]} from {other.Name}.");
                string prefix=$"share-item:{s.PackId}:{s.Level}:";
                if(split[0]!="lamp"&&other.State.RewardKeys.Count(key=>key.StartsWith(prefix,StringComparison.Ordinal))<3)
                    Engine.Reward(other.State,prefix+split[0],5,"An item delivered to a companion.");
            }return true;
        }
        return false;
    }
}

namespace Pyramid.Web.Game;

public static class ReadableItems
{
    public const string StarChartText="The chart marks ORION as the constellation that opens the dome.";
    public const string LapisTabletText="The archivist's record reads: 'Water may carry away ink, but what is remembered survives. Preserve the words, restore the crossings, and return the offerings to the landing.'";
    private static readonly Dictionary<string,string> BuiltIn=new(){["star chart"]=StarChartText,["lapis tablet"]=LapisTabletText};
    private static IReadOnlyDictionary<string,string> Texts(Expedition s)=>s.PackId=="pyramid"?BuiltIn:ContentPacks.Get(s.PackId).ReadableItems;
    private static string[] Present(Expedition s)=>s.Inventory.Concat(Engine.Lit(s)?s.Items.GetValueOrDefault(s.Room)??[]:[]).Distinct().ToArray();
    private static string[] Matches(Expedition s,string noun)
    {
        var choices=Present(s);
        if(choices.Contains(noun))return [noun];
        return choices.Where(item=>noun.Length>0&&(item.Split(' ').Contains(noun)||item.StartsWith(noun+" ",StringComparison.Ordinal)||item.EndsWith(" "+noun,StringComparison.Ordinal))).ToArray();
    }
    // False leaves current-room scenery handling to the caller. Neither inventory
    // in another expedition nor remotely placed items can provide their text.
    public static bool TryRead(Expedition s,string noun)
    {
        if(!Engine.Lit(s)){Engine.Say(s,"You need light to read.");return true;}
        var matches=Matches(s,noun);
        if(matches.Length>1){Engine.Say(s,"Which do you mean: "+string.Join(", ",matches)+"? Use the full item name.");return true;}
        if(matches.Length==0)return false;
        var item=matches[0];
        if(!Texts(s).TryGetValue(item,out var text)){Engine.Say(s,"There is no readable writing on the "+item+".");return true;}
        string entry=$"{item.ToUpperInvariant()}: {text}";
        Engine.Say(s,text);
        if(s.Journal.Contains(entry))Engine.Say(s,"This writing is already in your journal.");
        else if(s.Journal.Count>=100)Engine.Say(s,"Your journal is full (100 entries). The writing is shown above, but was not copied.");
        else{s.Journal.Add(entry);s.Achievements.Add("Keeper of notes");Engine.Say(s,"Copied into your journal. Use JOURNAL to read it again.");}
        return true;
    }
    public static bool TryExamine(Expedition s,string noun)
    {
        if(!Engine.Lit(s))return false;
        var matches=Matches(s,noun);
        if(matches.Length!=1||!Texts(s).ContainsKey(matches[0]))return false;
        Engine.Say(s,$"The {matches[0]} bears readable writing. READ {matches[0].ToUpperInvariant()} to copy it into your journal.");return true;
    }
}

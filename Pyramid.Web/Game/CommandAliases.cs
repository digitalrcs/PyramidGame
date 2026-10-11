namespace Pyramid.Web.Game;

// Curated vocabulary for the built-in campaign; custom adventures keep their own commands.
public static class CommandAliases
{
    private static readonly Dictionary<string,string> Commands = new()
    {
        ["feed bird"]="feed ibis", ["feed the bird"]="feed ibis", ["feed the ibis"]="feed ibis",
        ["give grain to bird"]="feed ibis", ["give grain to ibis"]="feed ibis",
        ["fill the bottle"]="fill bottle", ["pour bottle"]="pour water",
        ["tie the rope"]="tie rope", ["attach rope"]="tie rope",
        ["lubricate wheel"]="oil wheel", ["turn the wheel"]="turn wheel",
        ["place iron weight"]="place weight", ["put weight on scales"]="place weight",
        ["lay board"]="lay plank", ["place plank"]="lay plank", ["lay cedar plank"]="lay plank",
        ["clean mirror"]="polish mirror", ["polish the mirror"]="polish mirror",
        ["fit gear"]="fit cog", ["insert cog"]="fit cog", ["insert gear"]="fit cog", ["fit brass cog"]="fit cog",
        ["play reed flute"]="play flute", ["blow flute"]="play flute",
        ["sound tuning fork"]="sound fork", ["strike tuning fork"]="sound fork",
        ["place the feather"]="place feather", ["put feather on scales"]="place feather",
        ["show bronze mirror"]="show mirror", ["place stone cube"]="place cube",
        ["fit copper rod"]="fit rod", ["insert rod"]="fit rod",
        ["light lantern"]="light lamp", ["off lantern"]="off lamp"
    };
    private static readonly Dictionary<string,string[]> Items = new()
    {
        ["lamp"]=["lantern", "brass lamp"], ["bottle"]=["small bottle"],
        ["grain"]=["birdseed", "bird seed", "bundle of grain"],
        ["cedar plank"]=["board", "wooden plank"], ["brass cog"]=["gear", "brass gear"],
        ["polishing cloth"]=["rag"], ["wooden mallet"]=["hammer"]
    };
    public static string Command(string command)=>Commands.GetValueOrDefault(command,command);
    public static bool ItemMatches(string item,string noun)=>
        item.Split(' ').Contains(noun)||(Items.TryGetValue(item,out var aliases)&&aliases.Contains(noun));
}

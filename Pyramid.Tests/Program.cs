using Pyramid.Web.Game;
using Pyramid.Web;
using System.Text.Json;

int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAILED: "+name);checks++;Console.WriteLine("PASS "+name);}
var s=Engine.New();
void Cmd(string text)=>Engine.Execute(s,text);
void Travel(string target)
{
    var queue=new Queue<(string room,List<string> path)>();queue.Enqueue((s.Room,[]));var seen=new HashSet<string>();
    while(queue.Count>0){var (room,path)=queue.Dequeue();if(room==target){foreach(var dir in path)Cmd(dir);Check(s.Room==target,"Travel to "+target);return;}if(!seen.Add(room))continue;foreach(var exit in Engine.Current(s).Rooms[room].Exits)queue.Enqueue((exit.Value,[..path,exit.Key]));}
    throw new Exception("Unreachable "+target);
}
Check(World.Levels.Length==6&&World.Levels.Sum(x=>x.Rooms.Count)==60,"Six levels and 60 rooms");
var artNumbers=new HashSet<int>();
foreach(var level in World.Levels) foreach(var room in level.Rooms.Keys) artNumbers.Add(World.RoomNumber(Array.IndexOf(World.Levels,level)+1,room));
Check(artNumbers.SetEquals(Enumerable.Range(1,60)),"Every room has a distinct stable image number");
Check(JsonSerializer.SerializeToElement(Engine.View(s)).GetProperty("roomImage").GetString()=="room-art/room1.png","Landing artwork uses a subfolder-relative URL");
var unlit=Engine.New();Engine.Execute(unlit,"north");
Check(JsonSerializer.SerializeToElement(Engine.View(unlit)).GetProperty("roomImage").ValueKind==JsonValueKind.Null,"Unlit rooms hide artwork");
Cmd("get lamp");Cmd("light lamp");Cmd("get bottle");Cmd("north");Cmd("west");Cmd("north");Check(s.Room=="west","Unsolved shrine blocks travel");
// Independent, literal solutions exercise every puzzle and every level transition.
string[][] solutions=[
 ["east|fill bottle","west|pour water","east|feed ibis","gallery|tie rope"],
 ["west|oil wheel","west|turn wheel","east|place weight","gallery|lay plank"],
 ["west|polish mirror","east|fit cog","east|set orion","gallery|turn east"],
 ["west|strike sun river moon","east|play flute","gallery|sound fork"],
 ["west|place feather","east|reflect name","east|say amen","gallery|show mirror"],
 ["west|place cube","east|fit rod","gallery|turn east"]];
string[] words=["dawn","memory","shadow","silence","truth","eternity"];
for(int n=1;n<=6;n++)
{
    Check(s.Level==n,"Entered level "+n);
    foreach(var room in new[]{"west","east","study"}){Travel(room);foreach(var item in s.Items[room].ToArray())Cmd("take "+item);if(room=="study")Cmd("read tablet");}
    foreach(var action in solutions[n-1]){var pair=action.Split('|');Travel(pair[0]);var solved=s.Solved;Cmd(pair[1]);Check(s.Solved==solved+1,"Solve "+pair[1]);Cmd(pair[1]);Check(s.Solved==solved+1,"Cannot farm puzzle score");}
    foreach(var room in new[]{"west-shrine","east-shrine","north-shrine"}){Travel(room);foreach(var item in s.Items[room].ToArray())Cmd("get "+item);}
    Travel("camp");Cmd("deposit");Check(s.Banked.Count==n*3,"Deposited level "+n+" relics");int score=s.Score;Cmd("deposit");Check(s.Score==score,"Cannot farm deposit score");
    Travel("gate");Cmd("say "+words[n-1]);
    s=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(s))!;
    Check(s.Banked.Count==n*3,"Save round trip level "+n);
    if(n<6)Cmd("ascend");
}
Cmd("invoke");Check(!s.Won,"Final puzzle cannot be skipped");
for(int i=0;i<5;i++)Cmd($"set {s.Gods[i]} {i+1}");Cmd("invoke");Check(s.Won&&s.Banked.Count==18,"Entire campaign can be won");
Check(s.Journal.Count==5,"Five persistent clues");Check(s.Achievements.Contains("Unaided ascension"),"Unaided achievement");
int finalScore=s.Score;Cmd("invoke");Check(s.Score==finalScore,"Win cannot be farmed");
// Exhaustively prove the five clue relations admit exactly one order.
IEnumerable<int[]> Permute(int[] remaining,int[] prefix){if(remaining.Length==0){yield return prefix;yield break;}foreach(int x in remaining)foreach(var p in Permute(remaining.Where(v=>v!=x).ToArray(),[..prefix,x]))yield return p;}
var valid=Permute([0,1,2,3,4],[]).Where(order=>{int a=Array.IndexOf(order,0),b=Array.IndexOf(order,1),c=Array.IndexOf(order,2),d=Array.IndexOf(order,3),e=Array.IndexOf(order,4);return c!=0&&c!=4&&d==c+1&&e==c+2&&a<e&&b>Math.Min(a,c)&&b<Math.Max(a,c);}).ToArray();
Check(valid.Length==1&&valid[0].SequenceEqual([0,1,2,3,4]),"Final clues have exactly one solution among 120 orders");
var dark=Engine.New();Engine.Execute(dark,"north");Engine.Execute(dark,"get grain");Check(!dark.Inventory.Contains("grain"),"Cannot take objects through darkness");Engine.Execute(dark,"retreat");Check(dark.Room=="camp","Darkness has a recovery route");
var path=Path.Combine(Path.GetTempPath(),"pyramid-tests-"+Guid.NewGuid().ToString("N"));
var db=new Storage(path);db.Create("a","Alpha","test-hash");db.Create("b","Bravo","test-hash");
var tasks=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>db.Command("a",0,"get lamp"))));
Check(tasks.Count(x=>x.accepted)==1,"Concurrent commands enforce revision");Check(db.Read("b").Inventory.Count==0,"Player inventory isolation");
var restored=new Storage(path);Check(restored.Read("a").Inventory.Contains("lamp"),"Database survives service recreation");
var token=db.NewSession("a");Check(db.ValidSession(token,"a")&&!db.ValidSession(token,"b"),"Session bound to account");db.EndSession(token);Check(!db.ValidSession(token,"a"),"Logout revokes session");
await CoopChecks.Run(Check);
ExplorationChecks.Run(Check);
AccountChecks.Run(Check);
await MailChecks.Run(Check);
Console.WriteLine($"{checks} checks passed. Full campaign: {s.Actions} commands, {s.Moves} moves, {s.Score} points.");

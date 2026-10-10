using Microsoft.Data.Sqlite;
using System.Text.Json;
using Pyramid.Web.Game;

namespace Pyramid.Web;

public record Account(string Id,string Name,string Hash,int Failures,DateTimeOffset? LockedUntil,string? Email=null,bool EmailVerified=false,bool IsAdmin=false,bool Suspended=false,string? PendingEmail=null,bool EmailNotifications=true);
public partial class Storage
{
    private readonly string connectionString;
    public Storage(string directory)
    {
        Directory.CreateDirectory(directory);
        connectionString=new SqliteConnectionStringBuilder{DataSource=Path.Combine(directory,"pyramid.db"),Mode=SqliteOpenMode.ReadWriteCreate,DefaultTimeout=15}.ToString();
        using var db=Open();using var cmd=db.CreateCommand();
        cmd.CommandText="""
          PRAGMA journal_mode=WAL;
          CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY,name TEXT NOT NULL UNIQUE COLLATE NOCASE,hash TEXT NOT NULL,state TEXT NOT NULL,lastSeen TEXT NOT NULL,failures INTEGER NOT NULL DEFAULT 0,lockedUntil TEXT);
          CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,userId TEXT NOT NULL,expires TEXT NOT NULL);
          CREATE INDEX IF NOT EXISTS session_user ON sessions(userId);
          CREATE TABLE IF NOT EXISTS parties(id TEXT PRIMARY KEY,ownerId TEXT NOT NULL,state TEXT NOT NULL);
          CREATE TABLE IF NOT EXISTS active_games(userId TEXT PRIMARY KEY,partyId TEXT NOT NULL);
          """;cmd.ExecuteNonQuery();
        MigrateAccounts(db);
    }
    private SqliteConnection Open(){var db=new SqliteConnection(connectionString);db.Open();return db;}
    public Account? Find(string name)=>FindAccount("name=$value",name);
    public bool Create(string id,string name,string hash,string? email=null)
    {
        using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="INSERT OR IGNORE INTO users(id,name,hash,state,lastSeen,pendingEmail) VALUES($id,$name,$hash,$state,$seen,$email)";
        cmd.Parameters.AddWithValue("$id",id);cmd.Parameters.AddWithValue("$name",name);cmd.Parameters.AddWithValue("$email",(object?)NormalizeEmail(email)??DBNull.Value);cmd.Parameters.AddWithValue("$hash",hash);cmd.Parameters.AddWithValue("$state",JsonSerializer.Serialize(Engine.New()));cmd.Parameters.AddWithValue("$seen",DateTimeOffset.UtcNow.ToString("O"));return cmd.ExecuteNonQuery()==1;
    }
    public void LoginResult(string id,bool success)
    {
        using var db=Open();using var cmd=db.CreateCommand();
        cmd.CommandText=success?"UPDATE users SET failures=0,lockedUntil=NULL WHERE id=$id":"UPDATE users SET failures=CASE WHEN lockedUntil IS NOT NULL AND lockedUntil < $now THEN 1 ELSE failures+1 END,lockedUntil=CASE WHEN failures>=4 THEN $until ELSE lockedUntil END WHERE id=$id";
        cmd.Parameters.AddWithValue("$id",id);cmd.Parameters.AddWithValue("$until",DateTimeOffset.UtcNow.AddMinutes(15).ToString("O"));cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();
        RecordLogin(id,success,id);
    }
    public string NewSession(string id)
    {
        var token=Guid.NewGuid().ToString("N");using var db=Open();using var cmd=db.CreateCommand();
        cmd.CommandText="DELETE FROM sessions WHERE expires < $now; INSERT INTO sessions(id,userId,expires) VALUES($session,$id,$expires)";
        cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));cmd.Parameters.AddWithValue("$session",token);cmd.Parameters.AddWithValue("$id",id);cmd.Parameters.AddWithValue("$expires",DateTimeOffset.UtcNow.AddHours(8).ToString("O"));cmd.ExecuteNonQuery();return token;
    }
    public bool ValidSession(string token,string id)
    {
        using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM sessions s JOIN users u ON u.id=s.userId WHERE s.id=$session AND s.userId=$id AND expires>$now AND u.suspended=0";
        cmd.Parameters.AddWithValue("$session",token);cmd.Parameters.AddWithValue("$id",id);cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));return (long)cmd.ExecuteScalar()!>0;
    }
    public void EndSession(string token){using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="DELETE FROM sessions WHERE id=$session";cmd.Parameters.AddWithValue("$session",token);cmd.ExecuteNonQuery();}
    public Expedition Read(string id)
    {
        using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="SELECT state FROM users WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);
        return JsonSerializer.Deserialize<Expedition>((string)(cmd.ExecuteScalar()??throw new InvalidOperationException("Account missing")))!;
    }
    public (bool accepted,Expedition state) Command(string id,long revision,string text)
    {
        using var db=Open();using var tx=db.BeginTransaction();using var cmd=db.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="SELECT state FROM users WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);
        var state=JsonSerializer.Deserialize<Expedition>((string)cmd.ExecuteScalar()!)!;
        if(state.Revision!=revision)return(false,state);
        Engine.Execute(state,text);
        cmd.CommandText="UPDATE users SET state=$state,lastSeen=$now WHERE id=$id";cmd.Parameters.AddWithValue("$state",JsonSerializer.Serialize(state));cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();tx.Commit();return(true,state);
    }
    public void Touch(string id)
    {
        using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="UPDATE users SET lastSeen=$now WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();
    }
    public object Roster()
    {
        using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="SELECT u.id,u.name,u.state,u.lastSeen,p.state FROM users u LEFT JOIN active_games a ON a.userId=u.id LEFT JOIN parties p ON p.id=a.partyId ORDER BY u.lastSeen DESC LIMIT 100";
        using var r=cmd.ExecuteReader();var result=new List<object>();
        while(r.Read())
        {
            var s=JsonSerializer.Deserialize<Expedition>(r.GetString(2))!;
            bool available=true;string roomName;
            try{if(!r.IsDBNull(4)){var party=JsonSerializer.Deserialize<Party>(r.GetString(4))!;s=party.Members[r.GetString(0)].State;Cooperation.Bind(party,s);}roomName=Engine.Current(s).Rooms[s.Room].Name;}
            catch(InvalidOperationException){available=false;roomName="Content pack unavailable";}
            result.Add(new{id=r.GetString(0),name=r.GetString(1),s.Level,room=roomName,s.Score,s.Moves,relics=s.Banked.Count,s.Solved,s.Achievements,s.Won,online=DateTimeOffset.Parse(r.GetString(3))>DateTimeOffset.UtcNow.AddSeconds(-45),progress=s.Won?100:available&&s.PackId=="pyramid"?Math.Min(99,((s.Level-1)*3+Engine.Current(s).Relics.Count(s.Banked.Contains))*100/18):0});
        }
        return result;
    }
}

using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pyramid.Web.Game;

namespace Pyramid.Web;

public record DatabaseBackup(string Format,int Version,DateTimeOffset CreatedAt,Dictionary<string,List<Dictionary<string,JsonElement>>> Tables);

public partial class Storage
{
    static readonly string[] BackupTables=["users","parties","active_games","account_audit","login_history"];
    public DatabaseBackup ExportBackup(string actor)
    {
        using var db=Open();using var tx=db.BeginTransaction();RequireAdmin(db,tx,actor);
        var backup=ReadBackup(db,tx);Audit(db,tx,actor,actor,"backup-export","Private accounts and gameplay export");tx.Commit();return backup;
    }
    static DatabaseBackup ReadBackup(SqliteConnection db,SqliteTransaction tx)
    {
        var tables=new Dictionary<string,List<Dictionary<string,JsonElement>>>();
        foreach(var table in BackupTables)
        {
            using var cmd=Query(db,tx,$"SELECT * FROM {table}");using var r=cmd.ExecuteReader();var rows=new List<Dictionary<string,JsonElement>>();
            while(r.Read())
            {
                var row=new Dictionary<string,JsonElement>();
                for(int i=0;i<r.FieldCount;i++)row[r.GetName(i)]=JsonSerializer.SerializeToElement(r.IsDBNull(i)?null:r.GetValue(i));
                rows.Add(row);
            }
            tables.Add(table,rows);
        }
        return new("pyramid-portable-backup",1,DateTimeOffset.UtcNow,tables);
    }
    static string[] Columns(SqliteConnection db,SqliteTransaction? tx,string table)
    {
        using var cmd=Query(db,tx,$"PRAGMA table_info({table})");using var r=cmd.ExecuteReader();var cols=new List<string>();while(r.Read())cols.Add(r.GetString(1));return cols.ToArray();
    }
    static void InsertRows(SqliteConnection db,SqliteTransaction? tx,string table,string[] columns,List<Dictionary<string,JsonElement>> rows)
    {
        if(rows is null||rows.Count>100000)throw new InvalidOperationException("Invalid row count.");
        foreach(var row in rows)
        {
            if(row is null||row.Count!=columns.Length||columns.Any(c=>!row.ContainsKey(c)))throw new InvalidOperationException("Incompatible columns.");
            using var cmd=Query(db,tx,$"INSERT INTO {table} ({string.Join(',',columns)}) VALUES ({string.Join(',',columns.Select((c,i)=>"$p"+i))})");
            for(int i=0;i<columns.Length;i++)
            {
                var value=row[columns[i]];object typed=value.ValueKind switch{JsonValueKind.Null=>DBNull.Value,JsonValueKind.String=>value.GetString()!,JsonValueKind.Number=>value.GetInt64(),_=>throw new InvalidOperationException("Invalid field type.")};
                cmd.Parameters.AddWithValue("$p"+i,typed);
            }
            cmd.ExecuteNonQuery();
        }
    }
    public string ImportBackup(string actor,DatabaseBackup backup)
    {
        using var db=Open();using var tx=db.BeginTransaction();RequireAdmin(db,tx,actor);RequireOwnerRestore(db,tx,actor);
        if(backup.Format!="pyramid-portable-backup"||backup.Version!=1||backup.Tables is null||!BackupTables.ToHashSet().SetEquals(backup.Tables.Keys))throw new InvalidOperationException("Incompatible backup.");
        // Stage into an isolated, server-defined schema: the import cannot supply SQL.
        using var stage=new SqliteConnection("Data Source=:memory:");stage.Open();
        var columns=new Dictionary<string,string[]>();
        foreach(var table in BackupTables)
        {
            using var schema=Query(db,tx,"SELECT sql FROM sqlite_master WHERE type='table' AND name=$table",("$table",table));
            using var create=Query(stage,null,(string)schema.ExecuteScalar()!);create.ExecuteNonQuery();
            columns[table]=Columns(db,tx,table);InsertRows(stage,null,table,columns[table],backup.Tables[table]);
        }
        using(var emailIndex=Query(stage,null,"CREATE UNIQUE INDEX user_email ON users(email COLLATE NOCASE) WHERE email IS NOT NULL"))emailIndex.ExecuteNonQuery();
        ValidateBackup(stage);
        ValidateOwnerRestore(db,tx,stage);
        // A consistent recovery export is durably written before the replacement transaction.
        var recovery=ReadBackup(db,tx);
        var root=Path.Combine(Path.GetDirectoryName(new SqliteConnectionStringBuilder(connectionString).DataSource)!,"ImportRecovery");
        Directory.CreateDirectory(root);
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(root,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var file=Path.Combine(root,$"before-import-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        var options=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None};
        if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
        using(var output=new FileStream(file,options)){JsonSerializer.Serialize(output,recovery,new JsonSerializerOptions(JsonSerializerDefaults.Web));output.Flush(true);}
        using(var clear=Query(db,tx,"DELETE FROM sessions;DELETE FROM account_tokens;DELETE FROM active_games;DELETE FROM parties;DELETE FROM users;DELETE FROM account_audit;DELETE FROM login_history"))clear.ExecuteNonQuery();
        using(var exists=Query(db,tx,"SELECT COUNT(*) FROM sqlite_master WHERE name='mail_outbox'"))
            if((long)exists.ExecuteScalar()!>0){using var mail=Query(db,tx,"DELETE FROM mail_outbox");mail.ExecuteNonQuery();}
        foreach(var table in BackupTables)InsertRows(db,tx,table,columns[table],backup.Tables[table]);
        // INSERT user trigger supplies a timestamp; restore the original registration date.
        foreach(var row in backup.Tables["users"])
        {
            using var update=Query(db,tx,"UPDATE users SET createdAt=$created,lastSeen=$past WHERE id=$id",("$created",row["createdAt"].ValueKind==JsonValueKind.Null?DBNull.Value:row["createdAt"].GetString()!),("$past",DateTimeOffset.MinValue.ToString("O")),("$id",row["id"].GetString()!));update.ExecuteNonQuery();
        }
        foreach(var row in backup.Tables["parties"])
        {
            var party=JsonSerializer.Deserialize<Party>(row["state"].GetString()!)!;
            foreach(var member in party.Members.Values){member.Awake=false;member.Seen=DateTimeOffset.MinValue;member.Offers.Clear();}
            party.Revision=checked(party.Revision+1);SaveParty(db,tx,party);
        }
        Audit(db,tx,actor,actor,"backup-import","Accounts and adventures replaced; sessions and email tokens revoked");tx.Commit();
        return Path.GetFileName(file);
    }
    static void ValidateBackup(SqliteConnection db)
    {
        var users=new Dictionary<string,string>();int admins=0;
        using(var cmd=Query(db,null,"SELECT id,name,hash,state,isAdmin,suspended,email,emailVerified,pendingEmail,lastSeen,createdAt,emailNotifications,failures,lockedUntil FROM users"))using(var r=cmd.ExecuteReader())while(r.Read())
        {
            string id=r.GetString(0),name=r.GetString(1);
            if(id.Length is <1 or >64||!Regex.IsMatch(name,@"\A[a-zA-Z0-9_-]{3,24}\z")||Convert.FromBase64String(r.GetString(2)).Length<20)throw new InvalidOperationException("Invalid account.");
            foreach(int i in new[]{4,5,7,11})if(r.GetInt64(i) is not (0 or 1))throw new InvalidOperationException("Invalid account flag.");
            if(r.GetInt64(12)<0)throw new InvalidOperationException("Invalid login failure count.");
            if(!r.IsDBNull(13))_=DateTimeOffset.Parse(r.GetString(13));
            if(!r.IsDBNull(6)&&!AccountEndpoints.ValidEmail(r.GetString(6))||!r.IsDBNull(8)&&!AccountEndpoints.ValidEmail(r.GetString(8))||r.GetBoolean(7)&&r.IsDBNull(6))throw new InvalidOperationException("Invalid email.");
            _=DateTimeOffset.Parse(r.GetString(9));if(!r.IsDBNull(10))_=DateTimeOffset.Parse(r.GetString(10));
            ValidateAdventure(JsonSerializer.Deserialize<Expedition>(r.GetString(3))!);users.Add(id,name);
            if(r.GetBoolean(4)&&!r.GetBoolean(5))admins++;
        }
        if(admins==0)throw new InvalidOperationException("Keep an active administrator.");
        var parties=new Dictionary<string,Party>();
        using(var cmd=Query(db,null,"SELECT id,ownerId,state FROM parties"))using(var r=cmd.ExecuteReader())while(r.Read())
        {
            var p=JsonSerializer.Deserialize<Party>(r.GetString(2))!;
            if(p is null||p.Id!=r.GetString(0)||p.Owner!=r.GetString(1)||!users.ContainsKey(p.Owner)||p.Members is null||!p.Members.ContainsKey(p.Owner)||p.Members.Count>8||string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>60||p.Name.Any(char.IsControl)||p.Revision<0||p.Revision==long.MaxValue||p.Invitations is null||p.Requests is null)throw new InvalidOperationException("Invalid party.");
            ValidateAdventure(p.World);
            foreach(var (id,m) in p.Members){if(m is null||!users.TryGetValue(id,out var name)||m.Name!=name||m.State is null||m.State.PackId!=p.World.PackId||m.Offers is null)throw new InvalidOperationException("Invalid companion.");ValidateAdventure(m.State);}
            if(p.Invitations.Keys.Concat(p.Requests.Keys).Any(id=>!users.ContainsKey(id)))throw new InvalidOperationException("Invalid invitation.");
            parties.Add(p.Id,p);
        }
        using(var cmd=Query(db,null,"SELECT userId,partyId FROM active_games"))using(var r=cmd.ExecuteReader())while(r.Read())
            if(!users.ContainsKey(r.GetString(0))||!parties.TryGetValue(r.GetString(1),out var party)||!party.Members.ContainsKey(r.GetString(0)))throw new InvalidOperationException("Invalid active expedition.");
    }
    static void ValidateAdventure(Expedition s)
    {
        if(s is null||s.Version!=1||s.Level<1||s.Revision<0||s.Revision==long.MaxValue||s.Inventory is null||s.Items is null||s.Visited is null||s.Transcript is null||s.Journal is null||s.Realms is null||s.Flags is null||s.Banked is null||s.Achievements is null||s.Hints is null||s.RewardKeys is null||s.VisitedRealms is null||s.Arrangement is null)throw new InvalidOperationException("Invalid adventure.");
        if(string.IsNullOrEmpty(s.PackId)||s.Moves<0||s.Actions<0||s.Solved<0||s.HintsUsed<0||s.MapsUsed<0||s.BonusPoints<0)throw new InvalidOperationException("Invalid adventure statistics.");
        if(s.PackId=="pyramid"&&(s.Level>6||s.Gods is not {Length:5}||!s.Gods.ToHashSet().SetEquals(new[]{"ra","isis","thoth","anubis","osiris"})))throw new InvalidOperationException("Invalid campaign.");
        var level=Engine.Current(s);
        ValidateRooms(s.Items,level.Rooms.Keys);
        if(string.IsNullOrEmpty(s.Room)||!level.Rooms.ContainsKey(s.Room)||s.Visited.Any(room=>room is null||!level.Rooms.ContainsKey(room)))throw new InvalidOperationException("Unknown room.");
        foreach(var strings in new IEnumerable<string>[] {s.Inventory,s.Flags,s.Banked,s.Achievements,s.Journal,s.RewardKeys})
            if(strings.Any(value=>value is null))throw new InvalidOperationException("Invalid adventure text.");
        if(s.Transcript.Any(entry=>entry is null||entry.Kind is null||entry.Text is null))throw new InvalidOperationException("Invalid transcript.");
        IEnumerable<string> RoomsAt(int number)
        {
            if(s.PackId=="pyramid")return number is >=1 and <=6?World.Levels[number-1].Rooms.Keys:throw new InvalidOperationException("Unknown saved level.");
            return ContentPacks.Get(s.PackId).Levels.SingleOrDefault(realm=>realm.Number==number)?.Rooms.Keys??throw new InvalidOperationException("Unknown saved realm.");
        }
        foreach(var (number,realm) in s.Realms)
        {
            if(realm is null||realm.Flags is null||realm.Flags.Any(flag=>flag is null))throw new InvalidOperationException("Invalid saved realm.");
            ValidateRooms(realm.Items,RoomsAt(number));
        }
        foreach(var (number,visited) in s.VisitedRealms)
        {
            var rooms=RoomsAt(number).ToHashSet();
            if(visited is null||visited.Any(room=>room is null||!rooms.Contains(room)))throw new InvalidOperationException("Unknown visited realm.");
        }
        _=JsonSerializer.Serialize(Engine.View(s));
    }
    static void ValidateRooms(Dictionary<string,List<string>> items,IEnumerable<string> roomIds)
    {
        if(items is null||roomIds.Any(room=>!items.ContainsKey(room))||items.Values.Any(list=>list is null||list.Any(item=>item is null)))throw new InvalidOperationException("Invalid saved room objects.");
    }
}

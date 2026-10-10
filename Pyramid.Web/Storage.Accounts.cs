using Microsoft.Data.Sqlite;
using System.Text.Json;
using Pyramid.Web.Game;

namespace Pyramid.Web;
public partial class Storage
{
    private static string? NormalizeEmail(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim().ToLowerInvariant();
    private static void MigrateAccounts(SqliteConnection db)
    {
        var columns=new HashSet<string>();
        using(var read=Query(db,null,"PRAGMA table_info(users)"))using(var r=read.ExecuteReader())while(r.Read())columns.Add(r.GetString(1));
        foreach(var (name,type) in new[]{("email","TEXT"),("pendingEmail","TEXT"),("emailVerified","INTEGER NOT NULL DEFAULT 0"),("isAdmin","INTEGER NOT NULL DEFAULT 0"),("suspended","INTEGER NOT NULL DEFAULT 0"),("emailNotifications","INTEGER NOT NULL DEFAULT 1"),("createdAt","TEXT")})
            if(!columns.Contains(name)){using var alter=Query(db,null,$"ALTER TABLE users ADD COLUMN {name} {type}");alter.ExecuteNonQuery();}
        using var cmd=Query(db,null,"""
            CREATE UNIQUE INDEX IF NOT EXISTS user_email ON users(email COLLATE NOCASE) WHERE email IS NOT NULL;
            CREATE TABLE IF NOT EXISTS account_tokens(hash TEXT PRIMARY KEY,userId TEXT NOT NULL,purpose TEXT NOT NULL,email TEXT,expires TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS account_token_user ON account_tokens(userId);
            CREATE TABLE IF NOT EXISTS account_audit(id INTEGER PRIMARY KEY AUTOINCREMENT,at TEXT NOT NULL,actorId TEXT,targetId TEXT,action TEXT NOT NULL,detail TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS login_history(id INTEGER PRIMARY KEY AUTOINCREMENT,at TEXT NOT NULL,userId TEXT,identifier TEXT NOT NULL,success INTEGER NOT NULL);
            CREATE TRIGGER IF NOT EXISTS user_created AFTER INSERT ON users BEGIN UPDATE users SET createdAt=strftime('%Y-%m-%dT%H:%M:%fZ','now') WHERE id=NEW.id; END;
            """);cmd.ExecuteNonQuery();
    }
    private const string AccountColumns="id,name,hash,failures,lockedUntil,email,emailVerified,isAdmin,suspended,pendingEmail,emailNotifications";
    private static Account ReadAccount(SqliteDataReader r)=>new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetInt32(3),r.IsDBNull(4)?null:DateTimeOffset.Parse(r.GetString(4)),r.IsDBNull(5)?null:r.GetString(5),r.GetBoolean(6),r.GetBoolean(7),r.GetBoolean(8),r.IsDBNull(9)?null:r.GetString(9),r.GetBoolean(10));
    private Account? FindAccount(string predicate,string value)
    {
        using var db=Open();using var cmd=Query(db,null,$"SELECT {AccountColumns} FROM users WHERE {predicate}",("$value",value));using var r=cmd.ExecuteReader();return r.Read()?ReadAccount(r):null;
    }
    public Account? FindLogin(string value)=>FindAccount("name=$value COLLATE NOCASE OR (email=$value COLLATE NOCASE AND emailVerified=1)",value.Trim());
    public Account? FindById(string id)=>FindAccount("id=$value",id);
    public bool SetPendingEmail(string id,string email)
    {
        using var db=Open();using var tx=db.BeginTransaction();
        using(var current=Query(db,tx,"SELECT pendingEmail FROM users WHERE id=$id",("$id",id)))
            if(string.Equals(current.ExecuteScalar() as string,NormalizeEmail(email),StringComparison.Ordinal)){tx.Commit();return true;}
        using var cmd=Query(db,tx,"UPDATE users SET pendingEmail=$email WHERE id=$id AND NOT EXISTS(SELECT 1 FROM users WHERE email=$email COLLATE NOCASE AND id<>$id)",("$id",id),("$email",NormalizeEmail(email)??""));
        if(cmd.ExecuteNonQuery()!=1)return false;
        using var invalidate=Query(db,tx,"DELETE FROM account_tokens WHERE userId=$id AND purpose='verify'",("$id",id));invalidate.ExecuteNonQuery();tx.Commit();return true;
    }
    public void SetEmailNotifications(string id,bool enabled){using var db=Open();using var cmd=Query(db,null,"UPDATE users SET emailNotifications=$enabled WHERE id=$id",("$id",id),("$enabled",enabled?1:0));cmd.ExecuteNonQuery();}
    public bool IssueAccountToken(string userId,string purpose,string tokenHash,DateTimeOffset expires,string? expectedEmail=null)
    {
        if(purpose is not ("verify" or "reset"))throw new ArgumentException("Invalid token purpose.");
        using var db=Open();using var tx=db.BeginTransaction();
        using var clear=Query(db,tx,"DELETE FROM account_tokens WHERE (userId=$id AND purpose=$purpose) OR expires<$now",("$id",userId),("$purpose",purpose),("$now",DateTimeOffset.UtcNow.ToString("O")));clear.ExecuteNonQuery();
        using var insert=Query(db,tx,"""
            INSERT INTO account_tokens(hash,userId,purpose,email,expires)
            SELECT $hash,id,$purpose,CASE WHEN $purpose='verify' THEN pendingEmail ELSE email END,$expires
            FROM users WHERE id=$id AND suspended=0
            AND ($expected IS NULL OR (CASE WHEN $purpose='verify' THEN pendingEmail ELSE email END)=$expected COLLATE NOCASE)
            """,("$hash",tokenHash),("$id",userId),("$purpose",purpose),("$expires",expires.ToString("O")),("$expected",(object?)NormalizeEmail(expectedEmail)??DBNull.Value));
        // A stale recipient snapshot must not issue a link for a different address, or invalidate an existing link.
        if(insert.ExecuteNonQuery()!=1)return false;
        tx.Commit();return true;
    }
    public Account? FindTokenAccount(string hash,string purpose)
    {
        if(purpose is not ("verify" or "reset"))return null;
        using var db=Open();using var cmd=Query(db,null,$"SELECT {AccountColumns} FROM users WHERE suspended=0 AND id IN(SELECT userId FROM account_tokens WHERE hash=$hash AND purpose=$purpose AND expires>$now)",("$hash",hash),("$purpose",purpose),("$now",DateTimeOffset.UtcNow.ToString("O")));using var r=cmd.ExecuteReader();return r.Read()?ReadAccount(r):null;
    }
    public bool ConsumeVerificationToken(string hash)=>ConsumeToken(hash,"verify",null);
    public bool ConsumeResetToken(string hash,string newPasswordHash)=>ConsumeToken(hash,"reset",newPasswordHash);
    private bool ConsumeToken(string hash,string purpose,string? password)
    {
        using var db=Open();using var tx=db.BeginTransaction();string id;string email;
        using(var cmd=Query(db,tx,"SELECT t.userId,t.email FROM account_tokens t JOIN users u ON u.id=t.userId WHERE t.hash=$hash AND t.purpose=$purpose AND t.expires>$now AND u.suspended=0",("$hash",hash),("$purpose",purpose),("$now",DateTimeOffset.UtcNow.ToString("O"))))
        using(var r=cmd.ExecuteReader()){if(!r.Read()||r.IsDBNull(1))return false;id=r.GetString(0);email=r.GetString(1);}
        if(purpose=="verify")
        {
            using var update=Query(db,tx,"UPDATE users SET email=$email,pendingEmail=NULL,emailVerified=1 WHERE id=$id AND pendingEmail=$email AND NOT EXISTS(SELECT 1 FROM users WHERE email=$email COLLATE NOCASE AND id<>$id)",("$id",id),("$email",email));if(update.ExecuteNonQuery()!=1)return false;
            using var invalidate=Query(db,tx,"DELETE FROM account_tokens WHERE userId=$id AND purpose='reset'",("$id",id));invalidate.ExecuteNonQuery();
        }
        else
        {
            using var update=Query(db,tx,"UPDATE users SET hash=$hash,failures=0,lockedUntil=NULL WHERE id=$id AND email=$email AND emailVerified=1",("$id",id),("$email",email),("$hash",password!));if(update.ExecuteNonQuery()!=1)return false;
            Revoke(db,tx,id);
        }
        using var remove=Query(db,tx,"DELETE FROM account_tokens WHERE userId=$id AND purpose=$purpose",("$id",id),("$purpose",purpose));remove.ExecuteNonQuery();Audit(db,tx,id,id,purpose=="reset"?"password-reset":"email-verified","");tx.Commit();return true;
    }
    public void RecordLogin(string identifier,bool success,string? userId=null)
    {
        using var db=Open();using var cmd=Query(db,null,"INSERT INTO login_history(at,userId,identifier,success) VALUES($at,$user,$identifier,$success); DELETE FROM login_history WHERE id<(SELECT COALESCE(MAX(id),0)-10000 FROM login_history)",("$at",DateTimeOffset.UtcNow.ToString("O")),("$user",(object?)userId??DBNull.Value),("$identifier",identifier.Length>254?identifier[..254]:identifier),("$success",success?1:0));cmd.ExecuteNonQuery();
    }
    private static void Audit(SqliteConnection db,SqliteTransaction tx,string actor,string target,string action,string detail)
    {using var cmd=Query(db,tx,"INSERT INTO account_audit(at,actorId,targetId,action,detail) VALUES($at,$actor,$target,$action,$detail)",("$at",DateTimeOffset.UtcNow.ToString("O")),("$actor",actor),("$target",target),("$action",action),("$detail",detail));cmd.ExecuteNonQuery();}
    private static void RequireAdmin(SqliteConnection db,SqliteTransaction? tx,string actor)
    {using var cmd=Query(db,tx,"SELECT COUNT(*) FROM users WHERE id=$id AND isAdmin=1 AND suspended=0",("$id",actor));if((long)cmd.ExecuteScalar()!!=1)throw new InvalidOperationException("Administrator access required.");}
    private static void Revoke(SqliteConnection db,SqliteTransaction tx,string id)
    {using var cmd=Query(db,tx,"DELETE FROM sessions WHERE userId=$id; UPDATE users SET lastSeen=$past WHERE id=$id",("$id",id),("$past",DateTimeOffset.MinValue.ToString("O")));cmd.ExecuteNonQuery();SleepCurrent(db,tx,id);}
    public bool BootstrapAdmin(string username)
    {
        using var db=Open();using var tx=db.BeginTransaction();using var cmd=Query(db,tx,"UPDATE users SET isAdmin=1 WHERE name=$name COLLATE NOCASE AND suspended=0 AND isAdmin=0",("$name",username));bool changed=cmd.ExecuteNonQuery()==1;if(changed)Audit(db,tx,"configuration",username,"bootstrap-admin","");tx.Commit();return Find(username)?.IsAdmin==true;
    }
    public object AdminUsers(string actor)
    {
        using var db=Open();RequireAdmin(db,null,actor);using var cmd=Query(db,null,"SELECT u.id,u.name,email,emailVerified,isAdmin,suspended,pendingEmail,emailNotifications,lastSeen,createdAt,u.state,(SELECT COUNT(*) FROM login_history h WHERE h.userId=u.id AND success=1),(SELECT MAX(at) FROM login_history h WHERE h.userId=u.id AND success=1),p.state FROM users u LEFT JOIN active_games a ON a.userId=u.id LEFT JOIN parties p ON p.id=a.partyId ORDER BY u.name");using var r=cmd.ExecuteReader();var users=new List<object>();
        while(r.Read())
        {
            var s=JsonSerializer.Deserialize<Expedition>(r.GetString(10))!;
            if(!r.IsDBNull(13))
            {
                var party=JsonSerializer.Deserialize<Party>(r.GetString(13))!;
                if(party.Members.TryGetValue(r.GetString(0),out var member)){s=member.State;s.Solved=party.World.Solved;s.Banked=party.World.Banked;s.Won=party.World.Won;}
            }
            users.Add(new{id=r.GetString(0),name=r.GetString(1),email=r.IsDBNull(2)?null:r.GetString(2),emailVerified=r.GetBoolean(3),isAdmin=r.GetBoolean(4),suspended=r.GetBoolean(5),pendingEmail=r.IsDBNull(6)?null:r.GetString(6),emailNotifications=r.GetBoolean(7),lastSeen=r.GetString(8),createdAt=r.IsDBNull(9)?null:r.GetString(9),score=s.Score,level=s.Level,loginCount=r.GetInt64(11),lastLogin=r.IsDBNull(12)?null:r.GetString(12)});
        }
        return users;
    }
    public object AdminLogins(string actor)
    {
        using var db=Open();RequireAdmin(db,null,actor);using var cmd=Query(db,null,"SELECT at,userId,identifier,success FROM login_history ORDER BY id DESC LIMIT 100");using var r=cmd.ExecuteReader();var result=new List<object>();while(r.Read())result.Add(new{at=r.GetString(0),userId=r.IsDBNull(1)?null:r.GetString(1),identifier=r.GetString(2),success=r.GetBoolean(3)});return result;
    }
    public object AdminAudit(string actor)
    {
        using var db=Open();RequireAdmin(db,null,actor);using var cmd=Query(db,null,"SELECT at,actorId,targetId,action,detail FROM account_audit ORDER BY id DESC LIMIT 200");using var r=cmd.ExecuteReader();var result=new List<object>();while(r.Read())result.Add(new{at=r.GetString(0),actorId=r.GetString(1),targetId=r.GetString(2),action=r.GetString(3),detail=r.GetString(4)});return result;
    }
    public void AdminRecordEmail(string actor,string target,string detail)
    {using var db=Open();using var tx=db.BeginTransaction();RequireAdmin(db,tx,actor);Audit(db,tx,actor,target,"email",detail);tx.Commit();}
    public void AdminAction(string actor,string target,string action)
    {
        if(action is not ("kick" or "suspend" or "restore" or "delete"))throw new InvalidOperationException("Unknown administrator action.");
        using var db=Open();using var tx=db.BeginTransaction();RequireAdmin(db,tx,actor);
        using(var cmd=Query(db,tx,"SELECT COUNT(*) FROM users WHERE id=$id",("$id",target)))if((long)cmd.ExecuteScalar()!!=1)throw new InvalidOperationException("Account unavailable.");
        if(target==actor)throw new InvalidOperationException("You cannot apply this action to your own account.");
        if(action is "suspend" or "delete")
        {
            using var guard=Query(db,tx,"SELECT COUNT(*) FROM users WHERE id<>$id AND isAdmin=1 AND suspended=0",("$id",target));if((long)guard.ExecuteScalar()!<1)throw new InvalidOperationException("Keep at least one active administrator.");
        }
        if(action!="restore")Revoke(db,tx,target);
        if(action=="delete")DeleteAccount(db,tx,target);
        else if(action is "suspend" or "restore"){using var cmd=Query(db,tx,"UPDATE users SET suspended=$value WHERE id=$id",("$id",target),("$value",action=="suspend"?1:0));cmd.ExecuteNonQuery();}
        Audit(db,tx,actor,target,action,"");tx.Commit();
    }
    private static void DeleteAccount(SqliteConnection db,SqliteTransaction tx,string target)
    {
        var parties=new List<Party>();using(var cmd=Query(db,tx,"SELECT state FROM parties"))using(var r=cmd.ExecuteReader())while(r.Read())parties.Add(JsonSerializer.Deserialize<Party>(r.GetString(0))!);
        foreach(var party in parties)
        {
            bool changed=party.Invitations.Remove(target)|party.Requests.Remove(target);
            if(party.Members.Remove(target,out var member))
            {
                changed=true;
                var items=party.World.PackId!="pyramid"&&party.World.Realms.TryGetValue(member.State.Level,out var realm)?realm.Items:party.World.Items;
                if(items.TryGetValue(member.State.Room,out var floor))floor.AddRange(member.State.Inventory.Where(i=>i!="lamp"));
                foreach(var other in party.Members.Values){foreach(var key in other.Offers.Where(o=>o.Value.Equals(member.Name,StringComparison.OrdinalIgnoreCase)).Select(o=>o.Key).ToArray())other.Offers.Remove(key);Cooperation.Tell(other,$"{member.Name} has left this expedition.");}
            }
            if(!changed)continue;
            if(party.Members.Count==0){using var remove=Query(db,tx,"DELETE FROM active_games WHERE partyId=$id; DELETE FROM parties WHERE id=$id",("$id",party.Id));remove.ExecuteNonQuery();continue;}
            if(party.Owner==target)party.Owner=party.Members.Keys.OrderBy(x=>x,StringComparer.Ordinal).First();
            party.Revision++;SaveParty(db,tx,party);using var owner=Query(db,tx,"UPDATE parties SET ownerId=$owner WHERE id=$id",("$owner",party.Owner),("$id",party.Id));owner.ExecuteNonQuery();
        }
        DeleteAccountMail(db,tx,target);
        using var delete=Query(db,tx,"DELETE FROM active_games WHERE userId=$id; DELETE FROM account_tokens WHERE userId=$id; DELETE FROM login_history WHERE userId=$id; DELETE FROM users WHERE id=$id",("$id",target));delete.ExecuteNonQuery();
    }
}

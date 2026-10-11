using Pyramid.Web;
using Microsoft.Data.Sqlite;
using System.Text.Json;

public static class AccountChecks
{
    public static void Run(Action<bool,string> check)
    {
        check(AccountEndpoints.ValidEmail("Explorer@Example.tv"),"Mixed-case email with .tv domain is valid");
        check(AccountEndpoints.RegistrationError("Explorer","Explorer@Example.tv","example-passphrase") is null,"Registration accepts mixed-case .tv address");
        check(AccountEndpoints.RegistrationError(" Explorer "," Explorer@Example.tv ","example-passphrase") is null,"Registration tolerates surrounding pasted spaces");
        check(AccountEndpoints.RegistrationError("Explorer","Explorer@Example.tv","short")!.StartsWith("Password"),"Short password error does not blame a valid email");
        check(AccountEndpoints.RegistrationError("Explorer@Example.tv","Explorer@Example.tv","example-passphrase")!.StartsWith("Explorer name"),"Email entered as explorer name gets a specific name error");
        check(AccountEndpoints.RegistrationError("Explorer","not-an-email","example-passphrase")!.StartsWith("Enter a valid email"),"Malformed email gets a specific email error");
        check(AccountEndpoints.RegistrationError("Explorer","explorer @example.tv","example-passphrase")!=null,"Internal email whitespace remains rejected");
        check(AccountEndpoints.RegistrationError("Explorer","explorer@example.tv\r\nBcc:other@example.tv","example-passphrase")!=null,"Embedded email header injection remains rejected");
        string path=Path.Combine(Path.GetTempPath(),"pyramid-accounts-"+Guid.NewGuid().ToString("N"));var db=new Storage(path);
        db.Create("admin","Keeper","old");db.Create("a","Alpha","old","alpha@example.test");db.Create("b","Bravo","old");
        check(db.FindById("admin")?.IsAdmin==false,"Registration never grants administrator rights");
        check(!db.BootstrapAdmin("missing")&&db.BootstrapAdmin("Keeper"),"Bootstrap promotes only configured existing user");
        check(db.FindLogin("alpha@example.test")==null&&db.Find("ALPHA")?.Id=="a","Pending email cannot sign in; username stays available");
        db.IssueAccountToken("a","verify","verify-a",DateTimeOffset.UtcNow.AddMinutes(10));
        db.SetPendingEmail("a","ALPHA@example.test");check(db.FindTokenAccount("verify-a","verify")!=null,"Resending unchanged address preserves queued verification token");
        check(db.ConsumeVerificationToken("verify-a")&&!db.ConsumeVerificationToken("verify-a"),"Verification tokens are single use");
        check(db.FindLogin("ALPHA@EXAMPLE.TEST")?.EmailVerified==true,"Verified email login is case insensitive");
        check(!db.SetPendingEmail("b","alpha@example.test"),"Verified email cannot be claimed by another account");
        db.SetPendingEmail("a","new@example.test");db.IssueAccountToken("a","verify","verify-old",DateTimeOffset.UtcNow.AddMinutes(10));db.SetPendingEmail("a","newer@example.test");
        check(!db.ConsumeVerificationToken("verify-old")&&db.FindLogin("alpha@example.test")?.Id=="a","Changing pending address invalidates link and preserves verified identity");
        check(db.IssueAccountToken("a","verify","verify-current",DateTimeOffset.UtcNow.AddMinutes(10),expectedEmail:"newer@example.test"),"Verification token binds to expected current email");
        check(!db.IssueAccountToken("a","verify","verify-stale-snapshot",DateTimeOffset.UtcNow.AddMinutes(10),expectedEmail:"new@example.test")&&db.FindTokenAccount("verify-stale-snapshot","verify")==null,"Stale verification recipient snapshot cannot issue a token for changed address");
        check(db.FindTokenAccount("verify-current","verify")!=null,"Stale verification attempt rolls back invalidation of previous valid token");
        db.IssueAccountToken("a","reset","expired",DateTimeOffset.UtcNow.AddSeconds(-1));check(!db.ConsumeResetToken("expired","changed"),"Expired reset rejected");
        string session=db.NewSession("a");string party=db.NewParty("a","Shared","pyramid",false);db.PartyAction("a",party,"invite","Bravo");db.PartyAction("b",party,"accept");
        using(var view=JsonDocument.Parse(JsonSerializer.Serialize(db.PlayView("a"))))db.PlayCommand("a",view.RootElement.GetProperty("party").GetProperty("Revision").GetInt64(),"get bottle",party);
        db.IssueAccountToken("a","reset","reset",DateTimeOffset.UtcNow.AddMinutes(10));
        check(!db.IssueAccountToken("a","reset","reset-stale-snapshot",DateTimeOffset.UtcNow.AddMinutes(10),expectedEmail:"wrong@example.test")&&db.FindTokenAccount("reset-stale-snapshot","reset")==null&&db.FindTokenAccount("reset","reset")!=null,"Stale reset recipient snapshot cannot issue a token or invalidate prior reset link");
        check(db.FindTokenAccount("reset","reset")?.Id=="a"&&db.FindTokenAccount("reset","verify")==null,"Reset lookup requires correct purpose");
        check(db.ConsumeResetToken("reset","new-hash")&&!db.ConsumeResetToken("reset","third"),"Reset changes password once atomically");
        check(db.FindById("a")?.Hash=="new-hash"&&!db.ValidSession(session,"a"),"Password reset revokes existing sessions");
        using(var raw=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(path,"pyramid.db")}.ToString()))
        {raw.Open();using var cmd=raw.CreateCommand();cmd.CommandText="SELECT state FROM parties";using var json=JsonDocument.Parse((string)cmd.ExecuteScalar()!);check(!json.RootElement.GetProperty("Members").GetProperty("a").GetProperty("Awake").GetBoolean(),"Password reset leaves player in trance");}
        bool denied=false;try{db.AdminAction("a","b","delete");}catch(InvalidOperationException){denied=true;}check(denied,"Non administrator cannot delete users");
        denied=false;try{db.AdminAction("admin","admin","delete");}catch(InvalidOperationException){denied=true;}check(denied,"Administrator cannot delete own account");
        session=db.NewSession("b");db.AdminAction("admin","b","kick");check(!db.ValidSession(session,"b")&&db.FindById("b")!=null,"Kick revokes session without deleting progress");
        db.AdminAction("admin","b","suspend");session=db.NewSession("b");check(!db.ValidSession(session,"b")&&db.FindById("b")?.Suspended==true,"Suspended accounts cannot use sessions");db.AdminAction("admin","b","restore");
        db.LoginResult("a",true);check(JsonSerializer.Serialize(db.AdminUsers("admin")).Contains("\"loginCount\":1"),"Successful login history visible to administrator");
        db.AdminAction("admin","a","delete");check(db.FindById("a")==null&&JsonSerializer.Serialize(db.Lobby("b")).Contains("Bravo"),"Deleting expedition owner preserves companions and transfers ownership");
        check(JsonSerializer.Serialize(db.PlayView("b")).Contains("bottle"),"Deleted explorer's carried objects return to shared room");
        check(JsonSerializer.Serialize(db.AdminAudit("admin")).Contains("delete"),"Deletion leaves administrative audit record");
        db.AdminAction("admin","b","delete");check(!JsonSerializer.Serialize(db.Lobby("admin")).Contains("Shared"),"Deleting last member removes empty expedition");
        var reopened=new Storage(path);check(reopened.FindById("admin")?.IsAdmin==true,"Account schema migration is idempotent");
        var oldPath=Path.Combine(path,"legacy");Directory.CreateDirectory(oldPath);
        using(var raw=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(oldPath,"pyramid.db")}.ToString()))
        {raw.Open();using var cmd=raw.CreateCommand();cmd.CommandText="CREATE TABLE users(id TEXT PRIMARY KEY,name TEXT NOT NULL UNIQUE COLLATE NOCASE,hash TEXT NOT NULL,state TEXT NOT NULL,lastSeen TEXT NOT NULL,failures INTEGER NOT NULL DEFAULT 0,lockedUntil TEXT); INSERT INTO users VALUES('legacy','OldExplorer','hash',$state,$now,0,NULL)";cmd.Parameters.AddWithValue("$state",JsonSerializer.Serialize(Pyramid.Web.Game.Engine.New()));cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();}
        var migrated=new Storage(oldPath);check(migrated.Find("OldExplorer")?.Email==null&&migrated.Read("legacy").Room=="camp","Legacy SQLite migration preserves account and expedition state");
    }
}

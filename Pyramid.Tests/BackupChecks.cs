using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Pyramid.Web;
using Pyramid.Web.Game;
using System.Text.Json;

public static class BackupChecks
{
    public static void Run(Action<bool,string> check)
    {
        var path=Path.Combine(Path.GetTempPath(),"pyramid-backup-"+Guid.NewGuid().ToString("N"));
        var db=new Storage(path);var hash=new PasswordHasher<string>().HashPassword("fixture","Backup fixture password 2026!");
        db.Create("admin","Keeper",hash);db.BootstrapAdmin("Keeper");db.Create("a","Alpha",hash,"alpha@example.test");db.Create("b","Bravo",hash);
        object? Sql(string query){using var raw=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(path,"pyramid.db")}.ToString());raw.Open();using var cmd=raw.CreateCommand();cmd.CommandText=query;return cmd.ExecuteScalar();}
        Party Party()=>JsonSerializer.Deserialize<Party>((string)Sql("SELECT state FROM parties")!)!;
        db.Command("a",0,"get lamp");db.Command("a",db.Read("a").Revision,"note Remember the gate");
        var solo=JsonSerializer.Serialize(db.Read("a"));
        var partyId=db.NewParty("a","Backup expedition","pyramid",false);db.PartyAction("a",partyId,"invite","Bravo");db.PartyAction("b",partyId,"accept");
        db.PlayCommand("a",Party().Revision,"get bottle",partyId);db.PlayCommand("a",Party().Revision,"share item bottle with Bravo",partyId);db.PlayCommand("b",Party().Revision,"north",partyId);
        var partyBefore=Party();
        var session=db.NewSession("a");db.IssueAccountToken("a","verify","backup-old-link",DateTimeOffset.UtcNow.AddHours(1));db.EnsureMailStorage();
        Sql("INSERT INTO mail_outbox(id,userId,payload,created,nextAttempt) VALUES('fixture-mail','a','encrypted fixture','2026-01-01','2026-01-01')");
        db.LoginResult("a",true);
        var backup=db.ExportBackup("admin");
        check(backup.Tables.Keys.ToHashSet().SetEquals(new[]{"users","parties","active_games","account_audit","login_history"}),"Backup exports accounts and adventures without sessions, email tokens or mail payloads");
        check(backup.Tables["login_history"].Count>0&&backup.Tables["users"].Count==3&&backup.Tables["parties"].Count==1,"Backup includes login history, all accounts and shared expeditions");
        bool Denied(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
        check(Denied(()=>db.ExportBackup("a"))&&Denied(()=>db.ImportBackup("a",backup)),"Non administrators cannot export or import backups");
        DatabaseBackup Clone()=>JsonSerializer.Deserialize<DatabaseBackup>(JsonSerializer.Serialize(backup))!;
        void Reject(DatabaseBackup changed,string label)
        {
            var before=JsonSerializer.Serialize(db.Read("a"));var beforeParty=JsonSerializer.Serialize(Party());var count=(long)Sql("SELECT COUNT(*) FROM users")!;
            bool rejected=false;try{db.ImportBackup("admin",changed);}catch(Exception ex) when(ex is InvalidOperationException or JsonException or SqliteException or FormatException or KeyNotFoundException or ArgumentException){rejected=true;}
            check(rejected&&before==JsonSerializer.Serialize(db.Read("a"))&&beforeParty==JsonSerializer.Serialize(Party())&&count==(long)Sql("SELECT COUNT(*) FROM users")!&&db.ValidSession(session,"a")&&db.FindTokenAccount("backup-old-link","verify")!=null,"Invalid backup preserves accounts, adventures and credentials: "+label);
        }
        Dictionary<string,JsonElement> Alpha(DatabaseBackup b)=>b.Tables["users"].Single(r=>r["id"].GetString()=="a");
        void ChangeAdventure(DatabaseBackup b,Action<Expedition> mutate){var row=Alpha(b);var state=JsonSerializer.Deserialize<Expedition>(row["state"].GetString()!)!;mutate(state);row["state"]=JsonSerializer.SerializeToElement(JsonSerializer.Serialize(state));}
        var bad=Clone();foreach(var user in bad.Tables["users"])user["isAdmin"]=JsonSerializer.SerializeToElement(0);Reject(bad,"no active administrator");
        bad=Clone();bad.Tables["users"][0].Remove("name");Reject(bad,"missing schema column");
        bad=Clone();bad.Tables["sessions"]=[];Reject(bad,"unapproved table");
        Reject(Clone() with{Version=999},"unsupported backup version");
        bad=Clone();ChangeAdventure(bad,s=>s.Room="unmapped-room");Reject(bad,"unknown room");
        bad=Clone();ChangeAdventure(bad,s=>s.PackId="not-installed");Reject(bad,"unknown content pack");
        bad=Clone();ChangeAdventure(bad,s=>s.Items.Remove(s.Items.Keys.First(k=>k!=s.Room)));Reject(bad,"missing noncurrent room items");
        bad=Clone();ChangeAdventure(bad,s=>s.Gods=[..s.Gods,s.Gods[0]]);Reject(bad,"extra duplicate divine name");
        bad=Clone();var mod=Engine.New("oasis-of-stars");mod.Realms[mod.Level]=null!;Alpha(bad)["state"]=JsonSerializer.SerializeToElement(JsonSerializer.Serialize(mod));Reject(bad,"null saved mod realm");
        bad=Clone();mod=Engine.New("oasis-of-stars");mod.Realms[mod.Level].Items.Remove(mod.Items.Keys.First(k=>k!=mod.Room));Alpha(bad)["state"]=JsonSerializer.SerializeToElement(JsonSerializer.Serialize(mod));Reject(bad,"missing mod realm room");
        bad=Clone();bad.Tables["active_games"][0]["partyId"]=JsonSerializer.SerializeToElement("nonexistent");Reject(bad,"dangling active expedition");
        bad=Clone();var orphan=JsonSerializer.Deserialize<Party>(bad.Tables["parties"][0]["state"].GetString()!)!;orphan.Members["missing"]=new Companion{Name="Missing",State=Engine.New()};bad.Tables["parties"][0]["state"]=JsonSerializer.SerializeToElement(JsonSerializer.Serialize(orphan));Reject(bad,"missing companion account");
        check(!Directory.Exists(Path.Combine(path,"ImportRecovery")),"Rejected backups create no recovery files or replacement writes");
        db.Command("a",db.Read("a").Revision,"get bottle");db.Create("later","LaterExplorer",hash);
        string recoveryFile=db.ImportBackup("admin",backup);
        var recovery=JsonSerializer.Deserialize<DatabaseBackup>(File.ReadAllText(Path.Combine(path,"ImportRecovery",recoveryFile)),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        check(recovery.Tables["users"].Any(r=>r["id"].GetString()=="later")&&!backup.Tables["users"].Any(r=>r["id"].GetString()=="later"),"Restore first writes a recovery export containing the replaced database");
        check(db.FindById("later")==null&&JsonSerializer.Serialize(db.Read("a"))==solo,"Restore replaces accounts and restores exact solo progress and journal");
        check(db.FindById("admin")!.IsAdmin&&db.FindById("a")!.Hash==hash&&new PasswordHasher<string>().VerifyHashedPassword("fixture",db.FindById("a")!.Hash,"Backup fixture password 2026!")!=PasswordVerificationResult.Failed,"Restore retains administrator and functioning password hashes");
        var restored=Party();
        check(restored.Owner==partyBefore.Owner&&restored.Members.Keys.ToHashSet().SetEquals(partyBefore.Members.Keys)&&restored.Members["a"].State.Inventory.Contains("bottle")&&restored.Members["b"].State.Room==partyBefore.Members["b"].State.Room&&JsonSerializer.Serialize(restored.World)==JsonSerializer.Serialize(partyBefore.World),"Restore retains shared floor items, membership, independent satchels and locations");
        check(restored.Revision>partyBefore.Revision&&restored.Members.Values.All(m=>!m.Awake&&m.Offers.Count==0)&&!db.PlayCommand("a",partyBefore.Revision,"look",partyId).accepted,"Restore invalidates old shared revisions, pending offers and online presence");
        check(!db.ValidSession(session,"a")&&db.FindTokenAccount("backup-old-link","verify")==null&&(long)Sql("SELECT COUNT(*) FROM mail_outbox")! == 0,"Restore revokes sessions and old email links and clears queued mail");
        check((long)Sql("SELECT COUNT(*) FROM active_games")! == 2&&JsonSerializer.Serialize(db.AdminAudit("admin")).Contains("backup-import"),"Restore preserves active world selection and records the import audit");
        check(JsonSerializer.Serialize(new Storage(path).Read("a"))==solo,"Restored database reopens with saved progress intact");
    }
}

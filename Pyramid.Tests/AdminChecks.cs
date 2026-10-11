using Pyramid.Web;
using Pyramid.Web.Game;
using Microsoft.Data.Sqlite;
using System.Text.Json;

public static class AdminChecks
{
    public static void Run(Action<bool,string> check)
    {
        var path=Path.Combine(Path.GetTempPath(),"pyramid-admin-"+Guid.NewGuid().ToString("N"));var db=new Storage(path);
        db.Create("admin","Keeper","hash");db.BootstrapAdmin("Keeper");db.Create("a","Alpha","hash","alpha@example.test");db.Create("b","Bravo","hash","bravo@example.test");db.Create("c","Charlie","hash");
        bool Denied(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
        check(Denied(()=>db.AdminOverview("a"))&&Denied(()=>db.AdminExpeditions("a")),"Admin statistics and expedition list reject non administrators");
        check(Denied(()=>db.AdminApproveOrReset("a","b","approve","Bravo","bravo@example.test")),"Manual approval rejects non administrators");
        check(Denied(()=>db.AdminApproveOrReset("admin","a","approve","wrong","alpha@example.test")),"Manual approval requires exact explorer confirmation");
        db.IssueAccountToken("a","verify","old-verify",DateTimeOffset.UtcNow.AddHours(1));
        check(Denied(()=>db.AdminApproveOrReset("admin","a","approve","Alpha","stale@example.test"))&&!db.FindById("a")!.EmailVerified,"Manual approval refuses stale email snapshots");
        db.AdminApproveOrReset("admin","a","approve","Alpha","alpha@example.test");
        check(db.FindLogin("alpha@example.test")?.Id=="a"&&!db.ConsumeVerificationToken("old-verify"),"Manual approval enables email login and invalidates outstanding tokens");
        check(JsonSerializer.Serialize(db.AdminAudit("admin")).Contains("admin-email-approved"),"Manual approval leaves explicit administrator audit record");
        // Pending addresses are not reserved: a verified owner must still prevent a second manual claim.
        db.Create("d","Delta","hash","same@example.test");db.SetPendingEmail("b","same@example.test");db.AdminApproveOrReset("admin","d","approve","Delta","same@example.test");
        check(Denied(()=>db.AdminApproveOrReset("admin","b","approve","Bravo","same@example.test"))&&!db.FindById("b")!.EmailVerified,"Manual approval cannot take another account's verified email");
        db.Command("a",0,"get lamp");var soloRevision=db.Read("a").Revision;var session=db.NewSession("a");
        var partyId=db.NewParty("a","Shared reset test","pyramid",true);db.PartyAction("a",partyId,"invite","Bravo");db.PartyAction("b",partyId,"accept");db.PartyAction("a",partyId,"invite","Charlie");db.PartyAction("d",partyId,"request");
        Party Party(){using var raw=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(path,"pyramid.db")}.ToString());raw.Open();using var cmd=raw.CreateCommand();cmd.CommandText="SELECT state FROM parties WHERE id=$id";cmd.Parameters.AddWithValue("$id",partyId);return JsonSerializer.Deserialize<Party>((string)cmd.ExecuteScalar()!)!;}
        db.PlayCommand("a",Party().Revision,"get bottle",partyId);var partyBefore=JsonSerializer.Serialize(Party());
        check(Denied(()=>db.AdminApproveOrReset("b","a","reset-solo","Alpha"))&&Denied(()=>db.AdminApproveOrReset("admin","a","reset-solo","wrong")),"Solo reset requires administrator and exact name");
        db.AdminApproveOrReset("admin","a","reset-solo","Alpha");
        check(db.Read("a").Inventory.Count==0&&db.Read("a").Revision>soloRevision&&!db.Command("a",soloRevision,"get bottle").accepted,"Solo reset clears progress and rejects stale commands");
        check(partyBefore==JsonSerializer.Serialize(Party())&&db.FindById("a")!.Hash=="hash"&&db.ValidSession(session,"a"),"Solo reset preserves shared world, account and session");
        check(Denied(()=>db.AdminResetExpedition("b",partyId,"Shared reset test"))&&Denied(()=>db.AdminResetExpedition("admin",partyId,"wrong")),"Shared reset requires administrator and exact expedition name");
        var before=Party();db.AdminResetExpedition("admin",partyId,"Shared reset test");var after=Party();
        check(after.Revision>before.Revision&&!db.PlayCommand("a",before.Revision,"get bottle",partyId).accepted,"Shared reset increments revision and rejects commands from before reset");
        check(after.Owner==before.Owner&&after.Open&&after.Members.Keys.Order().SequenceEqual(before.Members.Keys.Order())&&after.Invitations.ContainsKey("c")&&after.Requests.ContainsKey("d"),"Shared reset retains host, membership, openness, invitations and requests");
        check(after.World.Items["camp"].Contains("bottle")&&after.Members.Values.All(x=>x.State.Room=="camp"&&x.State.Inventory.SequenceEqual(new[]{"lamp"})&&x.Offers.Count==0&&x.State.Revision==after.Revision),"Shared reset restores floor items and restarts every companion");
        check(db.Read("a").Revision==soloRevision+1,"Shared reset leaves independent solo save unchanged");
        var users=JsonSerializer.SerializeToElement(db.AdminUsers("admin"));var alpha=users.EnumerateArray().Single(x=>x.GetProperty("id").GetString()=="a");
        check(alpha.GetProperty("partyId").GetString()==partyId&&alpha.GetProperty("inventory").GetArrayLength()==1&&alpha.TryGetProperty("online",out _)&&alpha.TryGetProperty("room",out _),"Administrator user details reflect active shared adventure");
        var overview=JsonSerializer.SerializeToElement(db.AdminOverview("admin"));check(overview.GetProperty("totalUsers").GetInt32()==5&&overview.GetProperty("totalExpeditions").GetInt32()==1,"Administrator overview counts users and expeditions");
        check(overview.GetProperty("onlineUsers").GetInt32()==1&&alpha.GetProperty("online").GetBoolean(),"Online statistics require both recent activity and an authenticated session");
        db.AdminAction("admin","b","suspend");check(Denied(()=>db.AdminApproveOrReset("admin","b","approve","Bravo","same@example.test")),"Manual approval cannot restore a suspended account");
        check(new Storage(path).Read("a").Inventory.Count==0,"Administrator reset survives database reopen");
    }
}

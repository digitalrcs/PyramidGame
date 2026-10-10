using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Pyramid.Web;
using System.Text.Json;

static class MailChecks
{
    private sealed class TestEnvironment(string root,string name="Development"):IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="Pyramid.Tests";
        public string EnvironmentName{get;set;}=name;
        public string ContentRootPath{get;set;}=root;
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
        public string WebRootPath{get;set;}=Path.Combine(root,"wwwroot");
        public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
    public static async Task Run(Action<bool,string> check)
    {
        var root=Path.Combine(Path.GetTempPath(),"pyramid-mail-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var storage=new Storage(root);var environment=new TestEnvironment(root);
        var protector=DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(root,"keys")));
        var values=new Dictionary<string,string?>{{"Pyramid:DataDirectory",root},{"Pyramid:PublicBaseUrl","http://127.0.0.1:5199/pyramid/"},{"Pyramid:Mail:Mode","Capture"},{"Pyramid:Mail:FromAddress","pyramid@example.test"}};
        MailDelivery Make()=>new(storage,new ConfigurationBuilder().AddInMemoryCollection(values).Build(),environment,protector,NullLogger<MailDelivery>.Instance);
        void Sql(string sql){using var db=new SqliteConnection("Data Source="+Path.Combine(root,"pyramid.db"));db.Open();using var cmd=db.CreateCommand();cmd.CommandText=sql;cmd.ExecuteNonQuery();}
        string Scalar(string sql){using var db=new SqliteConnection("Data Source="+Path.Combine(root,"pyramid.db"));db.Open();using var cmd=db.CreateCommand();cmd.CommandText=sql;return cmd.ExecuteScalar()?.ToString()??"";}
        using var mail=Make();check(mail.Enabled&&mail.Mode=="Capture","Development capture mode enabled without SMTP secrets");
        check(mail.BuildLink("reset","TOKEN/+?")=="http://127.0.0.1:5199/pyramid/#reset=TOKEN%2F%2B%3F","Mail links preserve app subfolder and escape fragment tokens");
        storage.Create("verified","Verified","hash");storage.Create("pending","Pending","hash");storage.Create("changed","Changed","hash");storage.Create("deleted","Deleted","hash");
        Sql("UPDATE users SET email='verified@example.test',emailVerified=1 WHERE id='verified'; UPDATE users SET pendingEmail='pending@example.test' WHERE id='pending'; UPDATE users SET email='old@example.test',emailVerified=1 WHERE id='changed'; UPDATE users SET email='deleted@example.test',emailVerified=1 WHERE id='deleted';");
        var secret="TOKEN_SHOULD_NEVER_APPEAR_IN_DATABASE";
        check(mail.Queue("verified","verified@example.test","Reset password",mail.BuildLink("reset",secret),"reset:verified:1"),"Mail enqueues durably");
        check(mail.WasQueued("reset:verified:1")&&!mail.Queue("verified","verified@example.test","Duplicate","No duplicate","reset:verified:1"),"Durable dedupe blocks repeated queue submissions");
        var payload=Scalar("SELECT payload FROM mail_outbox LIMIT 1");check(!payload.Contains(secret)&&!payload.Contains("verified@example.test")&&!payload.Contains("Reset password"),"Outbox encrypts recipient, subject, and secret link at rest");
        mail.Queue("pending","pending@example.test","Verify account","Verification test",allowPendingEmail:true);
        mail.Queue("pending","pending@example.test","Invitation blocked","Unverified recipient");
        mail.Queue("changed","old@example.test","Old address blocked","Changed recipient");
        mail.Queue("deleted","deleted@example.test","Deleted blocked","Deleted recipient");
        mail.Queue("verified","verified@example.test","Invitation opted out","Opted out before delivery",requireNotifications:true);
        storage.SetEmailNotifications("verified",false);
        Sql("UPDATE users SET email='new@example.test' WHERE id='changed'; DELETE FROM users WHERE id='deleted';");
        check(Scalar("SELECT COUNT(*) FROM mail_outbox WHERE userId='deleted'")=="0","Account deletion removes queued mail atomically");
        await mail.StartAsync(CancellationToken.None);
        var deadline=DateTimeOffset.UtcNow.AddSeconds(12);
        while(DateTimeOffset.UtcNow<deadline&&Scalar("SELECT COUNT(*) FROM mail_outbox WHERE status IN ('pending','sending')")!="0")await Task.Delay(50);
        await mail.StopAsync(CancellationToken.None);
        var files=Directory.GetFiles(Path.Combine(root,"MailCapture"),"*.eml");
        check(files.Length==2&&Scalar("SELECT COUNT(*) FROM mail_outbox WHERE status='captured'")=="2","Capture delivers verified mail and explicitly allowed verification mail");
        check(Scalar("SELECT COUNT(*) FROM mail_outbox WHERE status='cancelled'")=="3","Sender cancels changed, unverified, or newly opted-out notification recipients");
        check(!files.Select(File.ReadAllText).Any(x=>x.Contains("Invitation opted out")),"Notification preference is rechecked before delivery while password recovery still works");
        check(Scalar("SELECT COUNT(*) FROM mail_outbox WHERE payload<>''")=="0","Delivered and cancelled messages discard encrypted payloads");
        var status=JsonSerializer.Serialize(mail.AdminStatus());check(!status.Contains(secret)&&!status.Contains("example.test")&&!status.Contains("Reset password"),"Admin delivery status exposes no addresses, content, or tokens");
        check(files.Select(File.ReadAllText).Any(x=>x.Contains(secret)),"Private development capture contains usable reset link");
        values["Pyramid:Mail:Mode"]="Disabled";using(var disabled=Make())check(!disabled.Enabled&&!disabled.Queue(null,"nobody@example.test","Test","No email"),"Disabled mode never queues email");
        values["Pyramid:Mail:Mode"]="Capture";environment.EnvironmentName="Production";
        bool rejected=false;try{using var invalid=Make();}catch(InvalidOperationException){rejected=true;}check(rejected,"Production rejects plaintext capture mode");
        environment.EnvironmentName="Development";values["Pyramid:PublicBaseUrl"]="http://evil.example.test/pyramid/";
        rejected=false;try{using var invalid=Make();}catch(InvalidOperationException){rejected=true;}check(rejected,"Nonloopback plaintext public links are rejected");
        values["Pyramid:PublicBaseUrl"]="https://digitalrcs.com/pyramid/?redirect=evil";
        rejected=false;try{using var invalid=Make();}catch(InvalidOperationException){rejected=true;}check(rejected,"Canonical mail URL cannot contain query parameters");
        values["Pyramid:PublicBaseUrl"]="https://digitalrcs.com/pyramid/";values["Pyramid:Mail:Mode"]="Smtp";values["Pyramid:Mail:Port"]="25";values["Pyramid:Mail:Username"]="test";values["Pyramid:Mail:Password"]="dummy";
        rejected=false;try{using var invalid=Make();}catch(InvalidOperationException){rejected=true;}check(rejected,"SMTP refuses insecure port configuration");
    }
}

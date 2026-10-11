using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Pyramid.Web;
using System.Text.Json;

static class SettingsChecks
{
    sealed class Environment(string root):IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="Pyramid.Tests";public string EnvironmentName{get;set;}="Development";
        public string ContentRootPath{get;set;}=root;public string WebRootPath{get;set;}=Path.Combine(root,"wwwroot");
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
    public static async Task Run(Action<bool,string> check)
    {
        var root=Path.Combine(Path.GetTempPath(),"pyramid-settings-"+Guid.NewGuid().ToString("N"));var db=new Storage(root);db.Create("admin","Keeper","hash");db.BootstrapAdmin("Keeper");db.Create("other","Explorer","hash");
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Pyramid:DataDirectory",root}}).Build();var env=new Environment(root);var protection=DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(root,"keys")));
        var settings=new SiteSettings(db,config,env,protection);using var mail=new MailDelivery(db,config,env,protection,NullLogger<MailDelivery>.Instance,settings);
        long Revision()=>JsonSerializer.SerializeToElement(settings.AdminView("admin")).GetProperty("revision").GetInt64();
        var secret="FICTIONAL_SMTP_PASSWORD_FOR_TEST_ONLY";
        var input=new SiteSettingsInput(false,"https://example.test/pyramid/","Scheduled maintenance tonight.","https://example.test/support",false,new("Smtp","smtp.example.test",465,"sender@example.test","sender@example.test","Pyramid",secret),Revision());
        bool Denied(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
        check(Denied(()=>settings.AdminView("other"))&&Denied(()=>settings.Update("other",input)),"Settings read and write require administrator rights");
        check(Denied(()=>settings.Update("admin",input with{PublicBaseUrl="https://user:pass@example.test/"}))&&Denied(()=>settings.Update("admin",input with{PublicBaseUrl="http://outside.example/"})),"Mail canonical URL rejects embedded credentials and nonloopback plaintext");
        check(Denied(()=>settings.Update("admin",input with{Mail=input.Mail with{Port=25}}))&&Denied(()=>settings.Update("admin",input with{DonationUrl="javascript:alert(1)"})),"Settings reject insecure SMTP port and unsafe donation URL");
        await mail.StartAsync(CancellationToken.None);check(!mail.ExecuteTask!.IsCompleted,"Disabled mail worker stays alive for runtime configuration");
        settings.Update("admin",input);check(mail.Enabled&&mail.Mode=="Smtp"&&!settings.AllowRegistration,"Saved settings immediately update mail mode and registration without restart");
        check(mail.BuildLink("verify","token")=="https://example.test/pyramid/#verify=token","Mail links immediately use configured public application URL");
        var view=JsonSerializer.Serialize(settings.AdminView("admin"));check(!view.Contains(secret)&&view.Contains("passwordConfigured"),"Administrator settings response never includes SMTP password");
        using(var raw=new SqliteConnection("Data Source="+Path.Combine(root,"pyramid.db"))){raw.Open();using var cmd=raw.CreateCommand();cmd.CommandText="SELECT payload FROM site_settings";var saved=(string)cmd.ExecuteScalar()!;check(!saved.Contains(secret)&&saved.Contains("ProtectedPassword"),"SMTP password is protected at rest in SQLite");}
        check(Denied(()=>settings.Update("admin",input)),"Stale settings form cannot overwrite newer administrator edits");
        input=input with{Revision=Revision(),Mail=input.Mail with{Password=""}};settings.Update("admin",input);
        check(JsonSerializer.SerializeToElement(settings.AdminView("admin")).GetProperty("mail").GetProperty("passwordConfigured").GetBoolean(),"Blank password preserves existing encrypted credential");
        var reopened=new SiteSettings(new Storage(root),config,env,protection);check(!reopened.AllowRegistration&&reopened.MaintenanceMessage==input.MaintenanceMessage,"Runtime settings survive service recreation");
        check(!JsonSerializer.Serialize(db.ExportBackup("admin")).Contains("site_settings"),"Portable gameplay backup excludes server settings and SMTP credentials");
        check(Denied(()=>settings.Update("admin",input with{Revision=Revision(),Mail=input.Mail with{ClearPassword=true}})),"Cannot erase required password while SMTP remains enabled");
        settings.Update("admin",input with{Revision=Revision(),Mail=input.Mail with{Mode="Disabled",ClearPassword=true}});check(!mail.Enabled&&!JsonSerializer.SerializeToElement(settings.AdminView("admin")).GetProperty("mail").GetProperty("passwordConfigured").GetBoolean(),"Disabling delivery can explicitly clear stored credential");
        await mail.StopAsync(CancellationToken.None);
        using(var raw=new SqliteConnection("Data Source="+Path.Combine(root,"pyramid.db"))){raw.Open();using var cmd=raw.CreateCommand();cmd.CommandText="INSERT INTO mail_outbox(id,payload,status,created,nextAttempt) VALUES('pending','','pending','now','now'),('sending','','sending','now','now'),('sent','','sent','now','now');";cmd.ExecuteNonQuery();}
        check(Denied(()=>db.ClearAdminLogs("other","audit","CLEAR LOGS"))&&Denied(()=>db.ClearAdminLogs("admin","audit","wrong")),"Log clearing requires administrator and exact confirmation");
        db.ClearAdminLogs("admin","mail","CLEAR LOGS");var statuses=JsonSerializer.Serialize(db.MailStatus());check(statuses.Contains("pending")&&statuses.Contains("sending")&&!statuses.Contains("\"sent\""),"Mail log clearing preserves pending and in-flight deliveries");
        db.RecordLogin("Explorer",true,"other");db.ClearAdminLogs("admin","logins","CLEAR LOGS");check(JsonSerializer.Serialize(db.AdminLogins("admin"))=="[]","Administrator can clear login history");
        db.ClearAdminLogs("admin","audit","CLEAR LOGS");var audit=JsonSerializer.SerializeToElement(db.AdminAudit("admin"));check(audit.GetArrayLength()==1&&audit[0].GetProperty("action").GetString()=="logs-cleared","Clearing audit retains an audit entry recording the clear");
    }
}

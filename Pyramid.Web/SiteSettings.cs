using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using System.Security.Claims;

namespace Pyramid.Web;

public record MailSettingsInput(string Mode,string Host,int Port,string Username,string FromAddress,string SenderName,string? Password=null,bool ClearPassword=false);
public record SiteSettingsInput(bool AllowRegistration,string PublicBaseUrl,string MaintenanceMessage,string DonationUrl,bool ChimesDefault,MailSettingsInput Mail,long? Revision=null,string SupportUrl="");
internal record RuntimeMail(string Mode,string Host,int Port,string Username,string FromAddress,string SenderName,string Password,Uri? PublicBase);
internal record StoredSettings(SiteSettingsInput Values,string? ProtectedPassword,long Revision=0);
internal record RuntimeSettings(SiteSettingsInput Values,RuntimeMail Mail,long Revision=0);

public sealed class SiteSettings
{
    readonly Storage storage;readonly IDataProtector protector;readonly bool development;readonly object sync=new();
    RuntimeSettings current;
    internal RuntimeMail Mail=>Volatile.Read(ref current).Mail;
    public bool AllowRegistration=>Volatile.Read(ref current).Values.AllowRegistration;
    public object PublicView(){var s=Volatile.Read(ref current).Values;return new{maintenanceMessage=s.MaintenanceMessage,donationUrl=s.DonationUrl,supportUrl=s.SupportUrl,chimesDefault=s.ChimesDefault};}
    public string MaintenanceMessage=>Volatile.Read(ref current).Values.MaintenanceMessage;
    public string DonationUrl=>Volatile.Read(ref current).Values.DonationUrl;
    public bool ChimesDefault=>Volatile.Read(ref current).Values.ChimesDefault;
    public SiteSettings(Storage storage,IConfiguration config,IWebHostEnvironment environment,IDataProtectionProvider protection)
    {
        this.storage=storage;development=environment.IsDevelopment();protector=protection.CreateProtector("Pyramid.SiteSettings.v1");
        storage.EnsureSettingsStorage();var mail=config.GetSection("Pyramid:Mail");var user=mail["Username"]??"";
        var values=new SiteSettingsInput(config.GetValue("Pyramid:AllowRegistration",true),config["Pyramid:PublicBaseUrl"]??"","","https://github.com/sponsors/digitalrcs",true,new(mail["Mode"]??"Disabled",mail["Host"]??"smtp.gmail.com",mail.GetValue("Port",465),user,mail["FromAddress"]??user,mail["SenderName"]??"Pyramid"));
        var password=mail["Password"]??"";long revision=0;var saved=storage.ReadSettings();
        if(saved!=null){var stored=JsonSerializer.Deserialize<StoredSettings>(saved)??throw new InvalidOperationException("Invalid stored settings.");values=stored.Values;revision=stored.Revision;password=stored.ProtectedPassword==null?"":protector.Unprotect(stored.ProtectedPassword);}
        current=Validate(values,password,allowCapture:development) with{Revision=revision};
    }
    RuntimeSettings Validate(SiteSettingsInput values,string password,bool allowCapture)
    {
        if(values is null||values.Mail is null)throw new InvalidOperationException("Settings are required.");var m=values.Mail;
        var mode=m.Mode?.Trim().ToLowerInvariant() switch{"disabled"=>"Disabled","smtp"=>"Smtp","capture" when allowCapture=>"Capture",_=>throw new InvalidOperationException("Choose Disabled or Smtp delivery.")};
        Uri? publicBase=null;
        if(values.PublicBaseUrl is null||values.PublicBaseUrl.Length>2048||values.PublicBaseUrl.Any(char.IsControl))throw new InvalidOperationException("Invalid public application URL.");
        if(!string.IsNullOrWhiteSpace(values.PublicBaseUrl)&&(!Uri.TryCreate(values.PublicBaseUrl,UriKind.Absolute,out publicBase)||publicBase.UserInfo!=""||publicBase.Query!=""||publicBase.Fragment!=""||!(publicBase.Scheme=="https"||(development&&publicBase.Scheme=="http"&&publicBase.IsLoopback))))throw new InvalidOperationException("Public URL must be HTTPS without credentials, query or fragment. Development permits HTTP loopback.");
        if(mode!="Disabled"&&publicBase==null)throw new InvalidOperationException("Email requires the trusted public application URL.");
        if(m.Port is not (465 or 587))throw new InvalidOperationException("Use SMTP port 465 (SSL) or 587 (STARTTLS).");
        if(m.Host is null||m.Host.Length>253||m.Host.Any(char.IsWhiteSpace)||m.Host.Any(char.IsControl)||m.Host.Contains('/')||m.Host.Contains(':')||m.Host!=""&&Uri.CheckHostName(m.Host)==UriHostNameType.Unknown)throw new InvalidOperationException("Enter a valid SMTP host name.");
        if(m.Username is null||m.Username.Length>254||m.Username.Any(char.IsControl)||m.FromAddress is null||m.SenderName is null||m.SenderName.Length is <1 or >100||m.SenderName.Any(char.IsControl)||password.Length>1024||password.Any(char.IsControl))throw new InvalidOperationException("Invalid SMTP account or sender settings.");
        if(mode!="Disabled"&&!AccountEndpoints.ValidEmail(m.FromAddress))throw new InvalidOperationException("Email requires a valid sender address.");
        if(m.FromAddress!=""&&!AccountEndpoints.ValidEmail(m.FromAddress))throw new InvalidOperationException("Invalid sender email address.");
        if(mode=="Smtp"&&(string.IsNullOrWhiteSpace(m.Host)||string.IsNullOrWhiteSpace(m.Username)||string.IsNullOrEmpty(password)))throw new InvalidOperationException("SMTP requires a host, username and password.");
        if(values.MaintenanceMessage is null||values.MaintenanceMessage.Length>500||values.MaintenanceMessage.Any(c=>char.IsControl(c)&&c!='\n'))throw new InvalidOperationException("The site message must be 500 characters or fewer.");
        if(values.DonationUrl is null||values.DonationUrl.Length>500||values.DonationUrl!=""&&(!Uri.TryCreate(values.DonationUrl,UriKind.Absolute,out var donation)||donation.Scheme!="https"||donation.UserInfo!=""))throw new InvalidOperationException("Donation URL must be HTTPS, or empty to hide the link.");
        if(values.SupportUrl is null||values.SupportUrl.Length>500||values.SupportUrl!=""&&(!Uri.TryCreate(values.SupportUrl,UriKind.Absolute,out var support)||support.Scheme!="https"||support.UserInfo!=""))throw new InvalidOperationException("Support URL must be HTTPS, or empty to hide the link.");
        var clean=values with{PublicBaseUrl=publicBase?.AbsoluteUri.TrimEnd('/')+ (publicBase==null?"":"/"),Mail=m with{Mode=mode,Password=null,ClearPassword=false}};
        return new(clean,new(mode,m.Host,m.Port,m.Username,m.FromAddress,m.SenderName,password,publicBase));
    }
    public object AdminView(string actor)
    {
        storage.RequireSettingsAdmin(actor);var s=Volatile.Read(ref current);var m=s.Mail;
        return new{revision=s.Revision,allowRegistration=s.Values.AllowRegistration,publicBaseUrl=s.Values.PublicBaseUrl,maintenanceMessage=s.Values.MaintenanceMessage,donationUrl=s.Values.DonationUrl,supportUrl=s.Values.SupportUrl,chimesDefault=s.Values.ChimesDefault,mail=new{mode=m.Mode,host=m.Host,port=m.Port,username=m.Username,fromAddress=m.FromAddress,senderName=m.SenderName,passwordConfigured=m.Password.Length>0}};
    }
    public void Update(string actor,SiteSettingsInput input)
    {
        lock(sync)
        {
            storage.RequireSettingsAdmin(actor);
            if(input?.Mail is null)throw new InvalidOperationException("Mail settings are required.");
            if(input.Mail.ClearPassword&&!string.IsNullOrEmpty(input.Mail.Password))throw new InvalidOperationException("Choose either a replacement password or clear password.");
            if(input.Revision!=current.Revision)throw new SettingsConflictException();
            var password=input.Mail.ClearPassword?"":string.IsNullOrEmpty(input.Mail.Password)?current.Mail.Password:input.Mail.Password;
            var next=Validate(input,password,allowCapture:development&&current.Mail.Mode=="Capture") with{Revision=checked(current.Revision+1)};
            var persisted=new StoredSettings(next.Values,password.Length==0?null:protector.Protect(password),next.Revision);
            storage.SaveSettings(actor,JsonSerializer.Serialize(persisted),current.Revision);Volatile.Write(ref current,next);
        }
    }
}

public partial class Storage
{
    internal void EnsureSettingsStorage(){using var db=Open();using var cmd=Query(db,null,"CREATE TABLE IF NOT EXISTS site_settings(id INTEGER PRIMARY KEY CHECK(id=1),payload TEXT NOT NULL)");cmd.ExecuteNonQuery();}
    internal string? ReadSettings(){using var db=Open();using var cmd=Query(db,null,"SELECT payload FROM site_settings WHERE id=1");return cmd.ExecuteScalar() as string;}
    internal void RequireSettingsAdmin(string actor){using var db=Open();RequireAdmin(db,null,actor);}
    internal void SaveSettings(string actor,string payload,long expectedRevision)
    {using var db=Open();using var tx=db.BeginTransaction();RequireAdmin(db,tx,actor);using var version=Query(db,tx,"SELECT payload FROM site_settings WHERE id=1");var previous=version.ExecuteScalar() as string;if((previous==null?0:JsonSerializer.Deserialize<StoredSettings>(previous)!.Revision)!=expectedRevision)throw new SettingsConflictException();using var cmd=Query(db,tx,"INSERT INTO site_settings(id,payload) VALUES(1,$payload) ON CONFLICT(id) DO UPDATE SET payload=$payload",("$payload",payload));cmd.ExecuteNonQuery();Audit(db,tx,actor,actor,"settings-updated","Site and mail settings updated; secret values omitted.");tx.Commit();}
    public void ClearAdminLogs(string actor,string kind,string confirmation)
    {
        using var db=Open();using var tx=db.BeginTransaction();RequireAdmin(db,tx,actor);
        if(confirmation!="CLEAR LOGS")throw new InvalidOperationException("Type CLEAR LOGS to confirm.");
        string sql=kind switch{"audit"=>"DELETE FROM account_audit","logins"=>"DELETE FROM login_history","mail"=>"DELETE FROM mail_outbox WHERE status IN ('sent','captured','cancelled','failed','expired')",_=>throw new InvalidOperationException("Choose audit, logins or mail.")};
        using var clear=Query(db,tx,sql);var count=clear.ExecuteNonQuery();Audit(db,tx,actor,actor,"logs-cleared",$"{kind}: {count} records cleared.");tx.Commit();
    }
}

public static class SettingsEndpoints
{
    public static void MapSettings(this WebApplication app)
    {
        var api=app.MapGroup("/api/admin").RequireAuthorization().RequireRateLimiting("play");
        api.AddEndpointFilter(async(context,next)=>context.HttpContext.RequestServices.GetRequiredService<Storage>().FindById(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!) is {IsAdmin:true,Suspended:false}?await next(context):Results.StatusCode(403));
        api.MapGet("/settings",(HttpContext c,SiteSettings settings)=>Results.Ok(settings.AdminView(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
        api.MapPost("/settings",(HttpContext c,SiteSettings settings,SiteSettingsInput input)=>{try{settings.Update(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!,input);return Results.Ok(new{message="Settings saved. Changes apply immediately."});}catch(SettingsConflictException e){return Results.Conflict(new{error=e.Message});}catch(InvalidOperationException e){return Results.BadRequest(new{error=e.Message});}});
        api.MapPost("/settings/test-email",(HttpContext c,Storage db,MailDelivery mail)=>{
            var actor=c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;var account=db.FindById(actor)!;
            if(!mail.Enabled||account is not {EmailVerified:true,Email:not null})return Results.BadRequest(new{error="Enable email delivery and approve your administrator email before sending a test."});
            var key=$"settings-test:{actor}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()/120}";
            if(mail.WasQueued(key))return Results.BadRequest(new{error="Wait two minutes before sending another test."});
            mail.Queue(actor,account.Email,"Pyramid email delivery test","This is the test message requested from Pyramid administration. Your email settings are working.",key);db.AdminRecordEmail(actor,actor,"Administrator explicitly requested a settings test email.");return Results.Ok(new{message="Test email queued for your approved administrator address."});
        });
        api.MapPost("/logs/clear",(HttpContext c,Storage db,ClearLogsInput input)=>{try{db.ClearAdminLogs(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!,input.Kind,input.Confirmation);return Results.Ok(new{message="Selected logs cleared. Pending mail was preserved."});}catch(InvalidOperationException e){return Results.BadRequest(new{error=e.Message});}});
    }
    public record ClearLogsInput(string Kind,string Confirmation);
}

public sealed class SettingsConflictException():InvalidOperationException("Another administrator changed these settings. Refresh and review the current values before saving.");

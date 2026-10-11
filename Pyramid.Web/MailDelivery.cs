using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using MimeKit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pyramid.Web;

public sealed class MailDelivery : BackgroundService
{
    private record Envelope(string To,string Subject,string Body,bool AllowPendingEmail=false,bool RequireNotifications=false);
    private readonly Storage storage;
    private readonly IDataProtector protector;
    private readonly ILogger<MailDelivery> logger;
    private readonly string captureDirectory;
    private readonly SiteSettings settings;
    public string Mode=>settings.Mail.Mode;
    public bool Enabled=>Mode!="Disabled";

    public MailDelivery(Storage storage,IConfiguration configuration,IWebHostEnvironment environment,IDataProtectionProvider protection,ILogger<MailDelivery> logger,SiteSettings? settings=null)
    {
        this.storage=storage;this.logger=logger;protector=protection.CreateProtector("Pyramid.MailOutbox.v1");
        this.settings=settings??new SiteSettings(storage,configuration,environment,protection);
        captureDirectory=Path.Combine(Path.GetFullPath(configuration["Pyramid:DataDirectory"]??"App_Data",environment.ContentRootPath),"MailCapture");
        var webRoot=Path.GetFullPath(environment.WebRootPath??Path.Combine(environment.ContentRootPath,"wwwroot"));
        if(captureDirectory.Equals(webRoot,StringComparison.OrdinalIgnoreCase)||captureDirectory.StartsWith(webRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Mail capture must remain outside the public web root.");
        if(Mode=="Capture")Directory.CreateDirectory(captureDirectory);
        storage.EnsureMailStorage();
    }
    public string BuildLink(string kind,string value)
    {
        if(kind is not ("reset" or "verify" or "expedition"))throw new ArgumentException("Unknown email link type.",nameof(kind));
        var publicBase=settings.Mail.PublicBase;
        if(publicBase==null)throw new InvalidOperationException("PublicBaseUrl is not configured.");
        return publicBase.AbsoluteUri.TrimEnd('/')+"/#"+kind+"="+Uri.EscapeDataString(value);
    }
    public bool Queue(string? userId,string to,string subject,string body,string? dedupeKey=null,bool allowPendingEmail=false,bool requireNotifications=false)
    {
        if(!Enabled)return false;
        if(to.Length>320||!MailboxAddress.TryParse(to,out var recipient)||recipient.Address!=to||subject.Length is <1 or >200||subject.Contains('\r')||subject.Contains('\n')||body.Length>32000)throw new ArgumentException("Invalid mail message.");
        var envelope=protector.Protect(JsonSerializer.Serialize(new Envelope(to,subject,body,allowPendingEmail,requireNotifications)));
        var dedupe=dedupeKey==null?null:Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dedupeKey)));
        return storage.EnqueueMail(userId,envelope,dedupe);
    }
    public bool WasQueued(string dedupeKey)=>storage.MailWasQueued(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dedupeKey))));
    public object AdminStatus()=>new{mode=Mode,enabled=Enabled,counts=storage.MailStatus()};

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var lease=await MaintenanceGate.Enter(false,stoppingToken);
                var config=settings.Mail;
                if(config.Mode=="Disabled"){await Task.Delay(TimeSpan.FromSeconds(3),stoppingToken);continue;}
                var mail=storage.ClaimMail();
                if(mail==null){await Task.Delay(TimeSpan.FromSeconds(3),stoppingToken);continue;}
                try
                {
                    var data=JsonSerializer.Deserialize<Envelope>(protector.Unprotect(mail.Payload))??throw new InvalidOperationException("Invalid envelope.");
                    if(!storage.MailRecipientCurrent(mail.UserId,data.To,data.AllowPendingEmail,data.RequireNotifications)){storage.FinishMail(mail.Id,"cancelled");continue;}
                    var message=new MimeMessage();message.From.Add(new MailboxAddress(config.SenderName,config.FromAddress));message.To.Add(MailboxAddress.Parse(data.To));message.Subject=data.Subject;
                    message.MessageId=mail.Id+"@pyramid.local";message.Body=new TextPart("plain"){Text=data.Body};
                    if(config.Mode=="Capture")
                    {
                        // Deliberately plaintext only in a private development directory for integration tests.
                        await message.WriteToAsync(Path.Combine(captureDirectory,mail.Id+".eml"),stoppingToken);
                    }
                    else
                    {
                        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);timeout.CancelAfter(TimeSpan.FromSeconds(60));
                        using var smtp=new SmtpClient(){Timeout=30000};
                        await smtp.ConnectAsync(config.Host,config.Port,config.Port==465?SecureSocketOptions.SslOnConnect:SecureSocketOptions.StartTls,timeout.Token);
                        await smtp.AuthenticateAsync(config.Username,config.Password,timeout.Token);
                        if(!storage.MailRecipientCurrent(mail.UserId,data.To,data.AllowPendingEmail,data.RequireNotifications)){storage.FinishMail(mail.Id,"cancelled");await smtp.DisconnectAsync(true,timeout.Token);continue;}
                        await smtp.SendAsync(message,timeout.Token);await smtp.DisconnectAsync(true,timeout.Token);
                    }
                    storage.FinishMail(mail.Id,config.Mode=="Capture"?"captured":"sent");
                }
                catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
                catch(Exception error)
                {
                    // Never log exception messages: SMTP responses may contain recipients or message fragments.
                    var category=error.GetType().Name;
                    storage.FinishMail(mail.Id,mail.Attempts>=6?"failed":"pending",category,mail.Attempts);
                    logger.LogWarning("Mail delivery attempt failed ({Category}).",category);
                }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception error)
            {
                logger.LogWarning("Mail queue temporarily unavailable ({Category}).",error.GetType().Name);
                try{await Task.Delay(TimeSpan.FromSeconds(15),stoppingToken);}catch(OperationCanceledException){break;}
            }
        }
    }
}

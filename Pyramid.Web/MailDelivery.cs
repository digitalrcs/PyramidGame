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
    private readonly string host,username,password,from,senderName,captureDirectory;
    private readonly int port;
    private readonly Uri? publicBase;
    public string Mode {get;}
    public bool Enabled=>Mode!="Disabled";

    public MailDelivery(Storage storage,IConfiguration configuration,IWebHostEnvironment environment,IDataProtectionProvider protection,ILogger<MailDelivery> logger)
    {
        this.storage=storage;this.logger=logger;protector=protection.CreateProtector("Pyramid.MailOutbox.v1");
        var config=configuration.GetSection("Pyramid:Mail");
        Mode=(config["Mode"]??"Disabled").Trim().ToLowerInvariant() switch{"disabled"=>"Disabled","capture"=>"Capture","smtp"=>"Smtp",_=>throw new InvalidOperationException("Unknown mail mode.")};
        if(Mode=="Capture"&&!environment.IsDevelopment())throw new InvalidOperationException("Mail capture is allowed only in Development.");
        host=config["Host"]??"smtp.gmail.com";port=config.GetValue("Port",465);
        username=config["Username"]??"";password=config["Password"]??"";
        from=config["FromAddress"]??username;senderName=config["SenderName"]??"Pyramid";
        captureDirectory=Path.Combine(Path.GetFullPath(configuration["Pyramid:DataDirectory"]??"App_Data",environment.ContentRootPath),"MailCapture");
        var webRoot=Path.GetFullPath(environment.WebRootPath??Path.Combine(environment.ContentRootPath,"wwwroot"));
        if(captureDirectory.Equals(webRoot,StringComparison.OrdinalIgnoreCase)||captureDirectory.StartsWith(webRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Mail capture must remain outside the public web root.");
        var baseSetting=configuration["Pyramid:PublicBaseUrl"];
        if(!string.IsNullOrWhiteSpace(baseSetting))
        {
            if(!Uri.TryCreate(baseSetting,UriKind.Absolute,out publicBase)||!string.IsNullOrEmpty(publicBase.UserInfo)||!string.IsNullOrEmpty(publicBase.Query)||!string.IsNullOrEmpty(publicBase.Fragment)||
               !(publicBase.Scheme=="https"||(environment.IsDevelopment()&&publicBase.Scheme=="http"&&publicBase.IsLoopback)))
                throw new InvalidOperationException("PublicBaseUrl must be a trusted HTTPS application URL (HTTP loopback is allowed in Development).");
        }
        if(Enabled&&publicBase==null)throw new InvalidOperationException("Email requires Pyramid:PublicBaseUrl.");
        if(Mode=="Smtp"&&(string.IsNullOrWhiteSpace(host)||string.IsNullOrWhiteSpace(username)||string.IsNullOrWhiteSpace(password)||port is not (465 or 587)))throw new InvalidOperationException("SMTP requires host, credentials, and TLS port 465 or 587.");
        if(Enabled&&!MailboxAddress.TryParse(from,out _))throw new InvalidOperationException("Email requires a valid FromAddress.");
        if(Mode=="Capture")Directory.CreateDirectory(captureDirectory);
        storage.EnsureMailStorage();
    }
    public string BuildLink(string kind,string value)
    {
        if(kind is not ("reset" or "verify" or "expedition"))throw new ArgumentException("Unknown email link type.",nameof(kind));
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
        if(!Enabled)return;
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var mail=storage.ClaimMail();
                if(mail==null){await Task.Delay(TimeSpan.FromSeconds(3),stoppingToken);continue;}
                try
                {
                    var data=JsonSerializer.Deserialize<Envelope>(protector.Unprotect(mail.Payload))??throw new InvalidOperationException("Invalid envelope.");
                    if(!storage.MailRecipientCurrent(mail.UserId,data.To,data.AllowPendingEmail,data.RequireNotifications)){storage.FinishMail(mail.Id,"cancelled");continue;}
                    var message=new MimeMessage();message.From.Add(new MailboxAddress(senderName,from));message.To.Add(MailboxAddress.Parse(data.To));message.Subject=data.Subject;
                    message.MessageId=mail.Id+"@pyramid.local";message.Body=new TextPart("plain"){Text=data.Body};
                    if(Mode=="Capture")
                    {
                        // Deliberately plaintext only in a private development directory for integration tests.
                        await message.WriteToAsync(Path.Combine(captureDirectory,mail.Id+".eml"),stoppingToken);
                    }
                    else
                    {
                        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);timeout.CancelAfter(TimeSpan.FromSeconds(60));
                        using var smtp=new SmtpClient(){Timeout=30000};
                        await smtp.ConnectAsync(host,port,port==465?SecureSocketOptions.SslOnConnect:SecureSocketOptions.StartTls,timeout.Token);
                        await smtp.AuthenticateAsync(username,password,timeout.Token);
                        if(!storage.MailRecipientCurrent(mail.UserId,data.To,data.AllowPendingEmail,data.RequireNotifications)){storage.FinishMail(mail.Id,"cancelled");await smtp.DisconnectAsync(true,timeout.Token);continue;}
                        await smtp.SendAsync(message,timeout.Token);await smtp.DisconnectAsync(true,timeout.Token);
                    }
                    storage.FinishMail(mail.Id,Mode=="Capture"?"captured":"sent");
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

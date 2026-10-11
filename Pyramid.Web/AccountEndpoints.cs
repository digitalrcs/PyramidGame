using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Pyramid.Web;

public static class AccountEndpoints
{
    static readonly object MailIssueLock=new();
    public static bool ValidEmail(string? email)
    {
        if(email is null||email.Length>254||email.Any(char.IsWhiteSpace)||email.Any(char.IsControl))return false;
        return System.Net.Mail.MailAddress.TryCreate(email,out var parsed)&&parsed.Address==email&&email.Contains('@')&&email.Split('@')[1].Contains('.');
    }
    static bool Password(string? value)=>value is {Length:>=12 and <=128};
    static string HashToken(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    static bool TokenValid(string? token)=>token is {Length:64}&&token.All(Uri.IsHexDigit);
    static string User(HttpContext c)=>c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    static object Message(string text)=>new{message=text};
    static async Task SignIn(HttpContext c,Storage db,Account account)
    {
        var sid=db.NewSession(account.Id);
        var identity=new ClaimsIdentity([new(ClaimTypes.NameIdentifier,account.Id),new(ClaimTypes.Name,account.Name),new("sid",sid)],"Pyramid");
        await c.SignInAsync("Pyramid",new ClaimsPrincipal(identity),new AuthenticationProperties{IsPersistent=false,ExpiresUtc=DateTimeOffset.UtcNow.AddHours(8)});
    }
    public static string QueueVerification(Storage db,MailDelivery mail,Account account)
    {
        var address=account.PendingEmail??(!account.EmailVerified?account.Email:null);
        if(!mail.Enabled)return "disabled";
        if(address is null)return "unavailable";
        lock(MailIssueLock){
        var key=$"verify:{account.Id}:{HashToken(address)}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()/120}";
        if(mail.WasQueued(key))return "already-queued";
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        if(!db.IssueAccountToken(account.Id,"verify",HashToken(token),DateTimeOffset.UtcNow.AddHours(24),expectedEmail:address))return "unavailable";
        return mail.Queue(account.Id,address,"Verify your Pyramid email",$"Hello {account.Name},\n\nConfirm this email address for your Pyramid explorer account:\n{mail.BuildLink("verify",token)}\n\nThis link expires in 24 hours. Open it and choose Verify email. If you did not request this, ignore this message.\n\nPyramid",key,allowPendingEmail:true)?"queued":"disabled";
        }
    }
    public static void MapAccounts(this WebApplication app)
    {
        var settings=app.Services.GetRequiredService<SiteSettings>();
        var dummy=app.Services.GetRequiredService<IPasswordHasher<string>>().HashPassword("dummy",Guid.NewGuid().ToString());
        app.MapGet("/api/session",(HttpContext c,Storage db,IAntiforgery csrf,MailDelivery mail)=>{
            var account=c.User.Identity?.IsAuthenticated==true?db.FindById(User(c)):null;
            return Results.Ok(new{token=csrf.GetAndStoreTokens(c).RequestToken,user=account is null?null:new{id=account.Id,name=account.Name,isAdmin=account.IsAdmin,isGod=account.IsGod,role=AccountRoles.For(account.IsAdmin,account.IsGod),emailVerified=account.EmailVerified,email=account.Email,pendingEmail=account.PendingEmail,verificationRequired=!account.EmailVerified,canRestoreBackup=db.CanImportBackup(account.Id)},verificationDelivery=account?.EmailVerified==true?"not-required":mail.Enabled?"required":"disabled",registration=settings.AllowRegistration,emailEnabled=mail.Enabled,site=settings.PublicView()});
        });
        app.MapPost("/api/register",async(Registration input,HttpContext c,Storage db,IPasswordHasher<string> hasher,MailDelivery mail)=>{
            if(!settings.AllowRegistration)return Results.Json(new{error="Registration is closed."},statusCode:403);
            if(input.Name is null||!Regex.IsMatch(input.Name,"\\A[a-zA-Z0-9_-]{3,24}\\z")||!Password(input.Password)||!ValidEmail(input.Email))return Results.BadRequest(new{error="Enter a valid email, a 3–24 character explorer name (letters, numbers, underscore or dash), and a 12–128 character password."});
            var id=Guid.NewGuid().ToString("N");
            if(!db.Create(id,input.Name,hasher.HashPassword(id,input.Password),input.Email))return Results.BadRequest(new{error="That explorer name or email is unavailable."});
            var account=db.FindById(id)!;var delivery=QueueVerification(db,mail,account);db.LoginResult(id,true);await SignIn(c,db,account);
            return Results.Ok(new{id,name=account.Name,verificationRequired=true,verificationDelivery=delivery,message=delivery is "queued" or "already-queued"?"Account created. A verification email is queued. Verify your email before entering the pyramid.":"Account created. Email delivery is unavailable. Ask an administrator to approve your email before entering the pyramid."});
        }).RequireRateLimiting("auth");
        app.MapPost("/api/login",async(Login input,HttpContext c,Storage db,IPasswordHasher<string> hasher)=>{
            if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>254||!Password(input.Password))return Results.Json(new{error="Sign-in failed. Check your credentials or try again later."},statusCode:401);
            var account=db.FindSignIn(input.Name.Trim());
            var correct=hasher.VerifyHashedPassword(account?.Id??"dummy",account?.Hash??dummy,input.Password)!=PasswordVerificationResult.Failed;
            if(account is null||account.Suspended||account.LockedUntil>DateTimeOffset.UtcNow||!correct){if(account is not null&&(account.LockedUntil is null||account.LockedUntil<=DateTimeOffset.UtcNow))db.LoginResult(account.Id,false);else db.RecordLogin(input.Name,false,account?.Id);return Results.Json(new{error="Sign-in failed. Check your credentials or try again later."},statusCode:401);}
            db.LoginResult(account.Id,true);await SignIn(c,db,account);return Results.Ok(new{id=account.Id,name=account.Name,verificationRequired=!account.EmailVerified});
        }).RequireRateLimiting("auth");
        app.MapPost("/api/forgot-password",(EmailOnly input,Storage db,MailDelivery mail)=>{
            if(ValidEmail(input.Email)&&mail.Enabled)
            {
              lock(MailIssueLock){
                var account=db.FindLogin(input.Email!);
                if(account is {EmailVerified:true,Suspended:false,Email:not null})
                {
                    var key=$"reset:{account.Id}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()/120}";
                    if(!mail.WasQueued(key))
                    {
                        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                        if(db.IssueAccountToken(account.Id,"reset",HashToken(token),DateTimeOffset.UtcNow.AddMinutes(30),expectedEmail:account.Email))
                        mail.Queue(account.Id,account.Email,"Reset your Pyramid password",$"Hello {account.Name},\n\nReset your password using this single-use link within 30 minutes:\n{mail.BuildLink("reset",token)}\n\nIf you did not request this, ignore this message. Your password has not changed.\n\nPyramid",key);
                    }
                }
            }
            }
            return Results.Ok(Message("If a verified account matches that email, a reset link will be sent. Check your inbox and spam folder."));
        }).RequireRateLimiting("auth");
        app.MapPost("/api/reset-password",(ResetPassword input,Storage db,IPasswordHasher<string> hasher)=>{
            if(!TokenValid(input.Token)||!Password(input.Password))return Results.BadRequest(new{error="Use a valid reset link and a 12–128 character password."});
            var digest=HashToken(input.Token);var account=db.FindTokenAccount(digest,"reset");
            if(account is null||!db.ConsumeResetToken(digest,hasher.HashPassword(account.Id,input.Password)))return Results.BadRequest(new{error="This reset link has expired or already been used. Request a new one."});
            return Results.Ok(Message("Password changed. All previous sessions have ended. Sign in with your new password."));
        }).RequireRateLimiting("auth");
        app.MapPost("/api/verify-email",(TokenInput input,Storage db)=>{
            if(!TokenValid(input.Token)||!db.ConsumeVerificationToken(HashToken(input.Token)))return Results.BadRequest(new{error="This verification link has expired or already been used. Request a new one in Account."});
            return Results.Ok(Message("Email verified. You can now sign in with this email and request password resets."));
        }).RequireRateLimiting("auth");
        var accountApi=app.MapGroup("/api/account").RequireAuthorization().RequireRateLimiting("play");
        accountApi.MapGet("",(HttpContext c,Storage db)=>{var a=db.FindById(User(c))!;return Results.Ok(new{id=a.Id,name=a.Name,email=a.Email,emailVerified=a.EmailVerified,pendingEmail=a.PendingEmail,isAdmin=a.IsAdmin,isGod=a.IsGod,role=AccountRoles.For(a.IsAdmin,a.IsGod),suspended=a.Suspended,emailNotifications=a.EmailNotifications});});
        accountApi.MapPost("/email",(EmailChange input,HttpContext c,Storage db,IPasswordHasher<string> hasher,MailDelivery mail)=>{
            var a=db.FindById(User(c))!;
            if(!mail.Enabled)return Results.BadRequest(new{error="Email delivery is not configured. Contact the administrator."});
            if(!ValidEmail(input.Email)||!Password(input.Password)||hasher.VerifyHashedPassword(a.Id,a.Hash,input.Password)==PasswordVerificationResult.Failed)return Results.BadRequest(new{error="Check your email and current password."});
            if(!db.SetPendingEmail(a.Id,input.Email))return Results.BadRequest(new{error="This email cannot be used."});
            QueueVerification(db,mail,db.FindById(a.Id)!);return Results.Ok(Message("Verification email queued. Your existing email remains active until you confirm the new address."));
        }).RequireRateLimiting("auth");
        accountApi.MapPost("/resend-verification",(HttpContext c,Storage db,MailDelivery mail)=>{
            if(!mail.Enabled)return Results.BadRequest(new{error="Email delivery is not configured. Contact the administrator."});
            var delivery=QueueVerification(db,mail,db.FindById(User(c))!);return Results.Ok(new{verificationDelivery=delivery,message=delivery is "queued" or "already-queued"?"A verification email is queued. Please allow two minutes between requests.":"No address is awaiting verification. Refresh your account details."});
        }).RequireRateLimiting("auth");
        accountApi.MapPost("/notifications",(NotificationPreference input,HttpContext c,Storage db)=>{db.SetEmailNotifications(User(c),input.Enabled);return Results.Ok();});
        var admin=app.MapGroup("/api/admin").RequireAuthorization().RequireRateLimiting("play");
        admin.AddEndpointFilter(async(context,next)=>{
            var db=context.HttpContext.RequestServices.GetRequiredService<Storage>();
            var a=db.FindById(User(context.HttpContext));
            return a is {IsAdmin:true,Suspended:false}?await next(context):Results.Json(new{error="Administrator access required."},statusCode:403);
        });
        admin.MapGet("/users",(HttpContext c,Storage db,MailDelivery mail)=>Results.Ok(new{users=db.AdminUsers(User(c)),overview=db.AdminOverview(User(c)),expeditions=db.AdminExpeditions(User(c)),audit=db.AdminAudit(User(c)),logins=db.AdminLogins(User(c)),mail=mail.AdminStatus()}));
        admin.MapPost("/action",(AdminActionInput input,HttpContext c,Storage db)=>{
            try{
                if(input.Action=="reset-expedition"){db.AdminResetExpedition(User(c),input.Target,input.Confirmation);return Results.Ok(Message("Shared expedition restarted for every member."));}
                if(input.Action is "approve" or "reset-solo"){db.AdminApproveOrReset(User(c),input.Target,input.Action,input.Confirmation,input.ExpectedEmail);return Results.Ok(Message(input.Action=="approve"?"Email approved by administrator.":"Solo adventure restarted. Shared expeditions are unchanged."));}
                var target=db.FindById(input.Target);
                if(target is null)return Results.BadRequest(new{error="Explorer no longer exists."});
                if(input.Action=="delete"&&input.Confirmation!=target.Name)return Results.BadRequest(new{error="Type the exact explorer name to confirm permanent deletion."});
                db.AdminAction(User(c),input.Target,input.Action);return Results.Ok(Message("Administrator action completed."));
            }catch(InvalidOperationException e){return Results.BadRequest(new{error=e.Message});}
        });
        admin.MapPost("/message",(AdminMessage input,HttpContext c,Storage db,MailDelivery mail)=>{
            if(!mail.Enabled)return Results.BadRequest(new{error="Email delivery is not configured."});
            if(string.IsNullOrWhiteSpace(input.Subject)||input.Subject.Length>120||input.Subject.Any(char.IsControl)||string.IsNullOrWhiteSpace(input.Message)||input.Message.Length>2000)return Results.BadRequest(new{error="Enter a subject (up to 120 characters) and message (up to 2000 characters)."});
            var target=db.FindById(input.Target);
            if(target is not {EmailVerified:true,Suspended:false,Email:not null}||!target.EmailNotifications)return Results.BadRequest(new{error="This explorer must have verified email notifications enabled."});
            var key=$"message:{target.Id}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()/120}";
            if(mail.WasQueued(key))return Results.BadRequest(new{error="Please wait two minutes before emailing this explorer again."});
            mail.Queue(target.Id,target.Email,input.Subject,$"Hello {target.Name},\n\n{input.Message}\n\nPyramid\nYou can disable expedition and information emails in Account.",key,requireNotifications:true);
            db.AdminRecordEmail(User(c),target.Id,"Information email queued");
            return Results.Ok(Message("Email queued for delivery."));
        });
    }
    public record Registration(string Name,string Email,string Password);
    public record Login(string Name,string Password);
    public record EmailOnly(string? Email);
    public record ResetPassword(string Token,string Password);
    public record TokenInput(string Token);
    public record EmailChange(string Email,string Password);
    public record NotificationPreference(bool Enabled);
    public record AdminActionInput(string Target,string Action,string? Confirmation,string? ExpectedEmail=null);
    public record AdminMessage(string Target,string Subject,string Message);
}

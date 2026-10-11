using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using System.Text.Encodings.Web;
using Pyramid.Web;
using Pyramid.Web.Game;

var builder=WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders().AddConsole();
builder.WebHost.ConfigureKestrel(o=>{o.AddServerHeader=false;o.Limits.MaxRequestBodySize=4096;});
var configuredData=builder.Configuration["Pyramid:DataDirectory"]??"App_Data";
var dataDir=Path.GetFullPath(configuredData,builder.Environment.ContentRootPath);
var publicDir=Path.GetFullPath(builder.Environment.WebRootPath??Path.Combine(builder.Environment.ContentRootPath,"wwwroot"));
if(dataDir.Equals(publicDir,StringComparison.OrdinalIgnoreCase)||dataDir.StartsWith(publicDir+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Private data cannot be stored in wwwroot.");
Directory.CreateDirectory(dataDir);
var contentDir=Path.GetFullPath(builder.Configuration["Pyramid:ContentDirectory"]??"ContentPacks",builder.Environment.ContentRootPath);
if(contentDir.Equals(publicDir,StringComparison.OrdinalIgnoreCase)||contentDir.StartsWith(publicDir+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Content packs contain puzzle answers and must remain outside wwwroot.");
ContentPacks.Load(contentDir);
var keys=builder.Services.AddDataProtection().SetApplicationName("DigitalRCS.Pyramid.v1").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir,"keys")));
if(OperatingSystem.IsWindows()&&!builder.Environment.IsDevelopment())keys.ProtectKeysWithDpapi(protectToLocalMachine:true);
builder.Services.AddSingleton(new Storage(dataDir));
builder.Services.AddSingleton<SiteSettings>();
builder.Services.AddSingleton<MailDelivery>();
builder.Services.AddHostedService(sp=>sp.GetRequiredService<MailDelivery>());
builder.Services.Configure<PasswordHasherOptions>(o=>o.IterationCount=310000);
builder.Services.AddSingleton<IPasswordHasher<string>,PasswordHasher<string>>();
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-Pyramid-CSRF";o.Cookie.Name="Pyramid.Csrf";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;});
builder.Services.AddAuthentication("Pyramid").AddCookie("Pyramid",o=>{
    o.Cookie.Name="Pyramid.Session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;
    o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
    o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=false;
    o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
    o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
    o.Events.OnValidatePrincipal=c=>{var id=c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);var sid=c.Principal?.FindFirstValue("sid");if(id==null||sid==null||!c.HttpContext.RequestServices.GetRequiredService<Storage>().ValidSession(sid,id))c.RejectPrincipal();return Task.CompletedTask;};
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>{
    o.RejectionStatusCode=429;
    o.AddPolicy("auth",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new(){PermitLimit=12,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
    o.AddPolicy("play",c=>RateLimitPartition.GetFixedWindowLimiter(c.User.FindFirstValue(ClaimTypes.NameIdentifier)??c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new(){PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
var app=builder.Build();
// Explicit opt-in for a known nginx/reverse-proxy address; never trust arbitrary forwarded headers.
var proxy=builder.Configuration["Pyramid:TrustedProxy"];
if(!string.IsNullOrWhiteSpace(proxy))
{
    var forwarding=new ForwardedHeadersOptions{ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto,ForwardLimit=1};
    forwarding.KnownProxies.Clear();forwarding.KnownIPNetworks.Clear();forwarding.KnownProxies.Add(IPAddress.Parse(proxy));
    app.UseForwardedHeaders(forwarding);
}
if(!app.Environment.IsDevelopment())app.UseExceptionHandler(error=>error.Run(async c=>{c.Response.StatusCode=500;await c.Response.WriteAsJsonAsync(new{error="The expedition could not be saved. Please retry shortly."});}));
// IIS integration supplies PathBase automatically. This setting also permits local subfolder testing.
var basePath=builder.Configuration["Pyramid:PathBase"];
if(!string.IsNullOrWhiteSpace(basePath))app.UsePathBase(basePath);
app.Use(async(c,next)=>{
    if(c.Request.Path=="/index.html"){c.Response.Redirect(c.Request.PathBase+"/");return;}
    c.Response.Headers.ContentSecurityPolicy="default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    c.Response.Headers.XContentTypeOptions="nosniff";c.Response.Headers.XFrameOptions="DENY";
    c.Response.Headers["Referrer-Policy"]="no-referrer";c.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
    if(!app.Environment.IsDevelopment()&&!c.Request.IsHttps){c.Response.StatusCode=400;await c.Response.WriteAsJsonAsync(new{error="HTTPS is required."});return;}
    if(c.Request.Path.StartsWithSegments("/api"))c.Response.Headers.CacheControl="no-store";
    await next();
});
app.Use(async(c,next)=>{
    if(!c.Request.Path.StartsWithSegments("/api")){await next();return;}
    bool importing=c.Request.Method=="POST"&&c.Request.Path=="/api/admin/backup/import";
    using var lease=await MaintenanceGate.Enter(importing,c.RequestAborted);
    if(importing){var limit=c.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();if(limit is {IsReadOnly:false})limit.MaxRequestBodySize=BackupEndpoints.UploadLimit;}
    else if(c.Request.Method=="POST"&&c.Request.Path=="/api/admin/settings"){var limit=c.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();if(limit is {IsReadOnly:false})limit.MaxRequestBodySize=8192;}
    await next();
});
app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();
app.Use(async(c,next)=>{
    if(c.Request.Method=="POST"&&c.Request.Path.StartsWithSegments("/api"))
    {
        try{await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);}
        catch(AntiforgeryValidationException){c.Response.StatusCode=400;await c.Response.WriteAsJsonAsync(new{error="Session check failed. Reload the page and try again."});return;}
    }
    await next();
});
// Serve only bundled assets. index.html always receives a server-derived subfolder base.
app.UseStaticFiles(new StaticFileOptions{OnPrepareResponse=c=>{if(c.Context.Request.Path=="/index.html")c.Context.Response.Headers.CacheControl="no-store";}});
app.MapAccounts();
app.MapSettings();
app.MapRoles();
app.MapBackups();
var api=app.MapGroup("/api").RequireAuthorization().RequireRateLimiting("play");
api.AddEndpointFilter(async(context,next)=>{
    // Administrators retain access to the separate administration endpoints, but playing
    // requires a verified address for every account. Recovery and account routes are separate.
    if(context.HttpContext.Request.Path=="/api/logout")return await next(context);
    var account=context.HttpContext.RequestServices.GetRequiredService<Storage>().FindById(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    return account?.EmailVerified==true?await next(context):Results.Json(new{code="verification_required",error="Verify your email before starting or resuming an adventure."},statusCode:403);
});
api.MapPost("/logout",async(HttpContext c,Storage db)=>{db.Sleep(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);db.EndSession(c.User.FindFirstValue("sid")!);await c.SignOutAsync("Pyramid");return Results.Ok();});
api.MapGet("/play",(HttpContext c,Storage db)=>{try{return Results.Ok(db.PlayView(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!));}catch(InvalidOperationException e){return Results.Json(new{error=e.Message},statusCode:409);}});
api.MapGet("/expeditions",(HttpContext c,Storage db)=>Results.Ok(db.Lobby(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
api.MapPost("/expeditions/create",(CreateExpedition input,HttpContext c,Storage db)=>{
    if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>60||input.Name.Any(char.IsControl)||string.IsNullOrEmpty(input.Pack))return Results.BadRequest(new{error="Choose an expedition name (1–60 characters) and a content pack."});
    try{return Results.Ok(new{id=db.NewParty(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!,input.Name.Trim(),input.Pack,input.Open)});}catch(InvalidOperationException e){return Results.BadRequest(new{error=e.Message});}
});
api.MapPost("/expeditions/action",(ExpeditionAction input,HttpContext c,Storage db,MailDelivery mail)=>{
    if(input.Id==null||input.Id.Length>32||input.Action==null||input.Action.Length>16||input.Target==null||input.Target.Length>32)return Results.BadRequest(new{error="Invalid expedition request."});
    try{db.PartyAction(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!,input.Id,input.Action,input.Target);
        if(input.Action is "invite" or "approve" && mail.Enabled){
            var recipient=input.Action=="invite"?db.Find(input.Target):db.FindById(input.Target);
            if(recipient is {EmailVerified:true,Suspended:false,Email:not null,EmailNotifications:true}&&db.HasInvitation(input.Id,recipient.Id))
                mail.Queue(recipient.Id,recipient.Email,"Pyramid expedition invitation",$"Hello {recipient.Name},\n\n{c.User.Identity!.Name} has invited you to a Pyramid expedition. Sign in with your own account and accept the invitation when ready:\n{mail.BuildLink("expedition",input.Id)}\n\nOpening this link does not join automatically. Manage email notifications in Account.\n\nPyramid",$"invite:{input.Id}:{recipient.Id}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()/3600}",requireNotifications:true);
        }
        return Results.Ok();}catch(InvalidOperationException e){return Results.BadRequest(new{error=e.Message});}
});
api.MapPost("/play/command",(CommandInput input,HttpContext c,Storage db)=>{
    if(string.IsNullOrWhiteSpace(input.Text)||input.Text.Length>160||input.Text.Any(char.IsControl))return Results.BadRequest(new{error="Enter a command of 1–160 printable characters."});
    var result=db.PlayCommand(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!,input.Revision,input.Text,input.ExpeditionId);
    return result.accepted?Results.Ok(result.view):Results.Json(new{error="A companion or another tab changed this expedition. Review the updated room and retry.",play=result.view},statusCode:409);
});
api.MapGet("/game",(HttpContext c,Storage db)=>{string id=c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;db.Touch(id);return Results.Ok(Engine.View(db.Read(id)));});
api.MapGet("/explorers",(HttpContext c,Storage db)=>{db.Touch(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);return Results.Ok(db.Roster());});
api.MapPost("/command",(CommandInput input,HttpContext c,Storage db)=>{
    if(string.IsNullOrWhiteSpace(input.Text)||input.Text.Length>160||input.Text.Any(char.IsControl))return Results.BadRequest(new{error="Enter a command of 1–160 printable characters."});
    var result=db.Command(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!,input.Revision,input.Text);
    return result.accepted?Results.Ok(Engine.View(result.state)):Results.Json(new{error="Another tab changed this expedition. Your view has been refreshed; retry your command.",state=Engine.View(result.state)},statusCode:409);
});
app.MapGet("/health",()=>Results.Ok(new{status="ok"}));
async Task ServeClient(HttpContext c){
    if(c.Request.Path==""&&c.Request.PathBase.HasValue){c.Response.Redirect(c.Request.PathBase+"/");return;}
    c.Response.Headers.CacheControl="no-store";
    var html=await File.ReadAllTextAsync(Path.Combine(app.Environment.WebRootPath,"index.html"));
    c.Response.ContentType="text/html; charset=utf-8";
    await c.Response.WriteAsync(html.Replace("__APP_BASE__",HtmlEncoder.Default.Encode(c.Request.PathBase+"/")));
}
app.MapGet("/",ServeClient);
app.MapGet("/admin",ServeClient);
// Operator preflight applies additive schema changes and validates configuration
// without opening a listener or starting the mail worker.
if(builder.Configuration.GetValue("Pyramid:ValidateOnly",false))
{
    _=app.Services.GetRequiredService<Storage>();
    _=app.Services.GetRequiredService<SiteSettings>();
    _=app.Services.GetRequiredService<MailDelivery>();
    Console.WriteLine("Database schema and configuration validated; background services were not started.");
    return;
}
// Explicit local operator bootstrap; never promote the first public registrant.
var bootstrapGod=builder.Configuration["Pyramid:BootstrapGod"];
if(!string.IsNullOrWhiteSpace(bootstrapGod)){app.Services.GetRequiredService<Storage>().BootstrapGod(bootstrapGod);if(builder.Configuration.GetValue("Pyramid:BootstrapOnly",false))return;}
var bootstrapName=builder.Configuration["Pyramid:BootstrapAdmin"];
if(!string.IsNullOrWhiteSpace(bootstrapName))
{
    var accounts=app.Services.GetRequiredService<Storage>();
    if(!accounts.BootstrapAdmin(bootstrapName))throw new InvalidOperationException("Bootstrap requires an existing, active explorer account.");
    var bootstrapEmail=builder.Configuration["Pyramid:BootstrapEmail"];
    if(!string.IsNullOrEmpty(bootstrapEmail))
    {
        if(!AccountEndpoints.ValidEmail(bootstrapEmail))throw new InvalidOperationException("Invalid bootstrap email.");
        var account=accounts.Find(bootstrapName)!;
        if(account.Email!=bootstrapEmail.ToLowerInvariant()&&!accounts.SetPendingEmail(account.Id,bootstrapEmail))throw new InvalidOperationException("Bootstrap email is unavailable.");
        AccountEndpoints.QueueVerification(accounts,app.Services.GetRequiredService<MailDelivery>(),accounts.FindById(account.Id)!);
    }
    if(builder.Configuration.GetValue("Pyramid:BootstrapOnly",false))return;
}
app.Run();

public record CommandInput(string Text,long Revision,string? ExpeditionId=null);
public record CreateExpedition(string Name,string Pack,bool Open);
public record ExpeditionAction(string Id,string Action,string Target);

using System.Security.Claims;

namespace Pyramid.Web;

// Only implemented roles are assignable. Add future roles with explicit server
// permissions and a storage migration rather than treating them as administrators.
public static class AccountRoles
{
    public static string For(bool isAdmin,bool isGod=false)=>isGod?"god":isAdmin?"admin":"explorer";
    public static object[] Catalog => [
        new {id="explorer",name="Explorer",description="Play and manage your own account and expeditions.",assignable=true},
        new {id="admin",name="Administrator",description="Manage ordinary accounts, expeditions, settings and email.",assignable=true},
        new {id="god",name="GOD",description="Installation owner. Provisioned only by the local operator.",assignable=false}
    ];
}

public partial class Storage
{
    public void ChangeRole(string actor,string target,string role,string expectedRole,string confirmation)
    {
        using var db=Open();using var tx=db.BeginTransaction();RequireAdmin(db,tx,actor);
        if(InstallationOwner(db,tx)==target)throw new InvalidOperationException("GOD is an installation owner role and cannot be reassigned.");
        if(role is not ("explorer" or "admin")||expectedRole is not ("explorer" or "admin"))throw new InvalidOperationException("Choose an available role.");
        if(target==actor)throw new InvalidOperationException("Another administrator must change your role.");
        string name;bool wasAdmin;
        using(var cmd=Query(db,tx,"SELECT name,isAdmin FROM users WHERE id=$id",("$id",target)))
        using(var reader=cmd.ExecuteReader())
        {if(!reader.Read())throw new InvalidOperationException("Explorer unavailable.");name=reader.GetString(0);wasAdmin=reader.GetBoolean(1);}
        if(confirmation!=name)throw new InvalidOperationException("Type the explorer name exactly to confirm.");
        if(AccountRoles.For(wasAdmin)!=expectedRole)throw new RoleConflictException();
        if(role==expectedRole){tx.Commit();return;}
        if(role=="explorer")
        {
            using var guard=Query(db,tx,"SELECT COUNT(*) FROM users WHERE isAdmin=1 AND suspended=0 AND id<>$id",("$id",target));
            if((long)guard.ExecuteScalar()!<1)throw new InvalidOperationException("Keep at least one active administrator.");
        }
        using(var update=Query(db,tx,"UPDATE users SET isAdmin=$admin WHERE id=$id",("$admin",role=="admin"?1:0),("$id",target)))update.ExecuteNonQuery();
        Revoke(db,tx,target);
        Audit(db,tx,actor,target,"role-changed",$"{expectedRole} -> {role}; sessions ended.");tx.Commit();
    }
}
public sealed class RoleConflictException:InvalidOperationException
{public RoleConflictException():base("This explorer's role changed. Refresh the explorer list and review it again.") {}}
public record RoleChangeInput(string Target,string Role,string ExpectedRole,string Confirmation);
public static class RoleEndpoints
{
    public static void MapRoles(this WebApplication app)
    {
        var api=app.MapGroup("/api/admin").RequireAuthorization().RequireRateLimiting("play");
        api.AddEndpointFilter(async(context,next)=>context.HttpContext.RequestServices.GetRequiredService<Storage>().FindById(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!) is {IsAdmin:true,Suspended:false}?await next(context):Results.StatusCode(403));
        api.MapGet("/roles",()=>Results.Ok(AccountRoles.Catalog));
        api.MapPost("/role",(HttpContext c,Storage db,RoleChangeInput input)=>{
            try{db.ChangeRole(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!,input.Target,input.Role,input.ExpectedRole,input.Confirmation);return Results.Ok(new{message="Role updated. The explorer must sign in again. Saved adventures were preserved."});}
            catch(RoleConflictException e){return Results.Conflict(new{error=e.Message});}
            catch(InvalidOperationException e){return Results.BadRequest(new{error=e.Message});}
        });
    }
}

using Pyramid.Web;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;

static class RoleChecks
{
    public static void Run(Action<bool,string> check)
    {
        var path=Path.Combine(Path.GetTempPath(),"pyramid-roles-"+Guid.NewGuid().ToString("N"));var db=new Storage(path);
        var hasher=new PasswordHasher<string>();
        foreach(var (id,name) in new[]{("owner","Owner_QA"),("admin","Admin_QA"),("user","Explorer_QA")})
            db.Create(id,name,hasher.HashPassword(id,"Fixture-password-only-123!"),name+"@example.test");
        db.BootstrapAdmin("Admin_QA");
        bool Denied(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
        check(Denied(()=>db.BootstrapGod("Missing_QA")),"Owner bootstrap requires an existing account");
        db.BootstrapGod("Owner_QA");
        check(db.FindById("owner") is {IsGod:true,IsAdmin:true}&&db.HasGod(),"Explicit owner bootstrap grants GOD and administration");
        check(Denied(()=>db.BootstrapGod("Admin_QA")),"A second installation owner cannot be assigned");
        check(Denied(()=>db.ChangeRole("user","admin","explorer","admin","Admin_QA")),"Explorer cannot change permissions");
        check(Denied(()=>db.ChangeRole("admin","user","god","explorer","Explorer_QA")),"Administrator cannot assign GOD");
        check(Denied(()=>db.ChangeRole("admin","owner","explorer","admin","Owner_QA")),"Administrator cannot demote GOD");
        check(Denied(()=>db.ChangeRole("owner","owner","explorer","admin","Owner_QA")),"GOD cannot remove own owner role through role API");
        foreach(var action in new[]{"kick","suspend","delete"})check(Denied(()=>db.AdminAction("admin","owner",action)),"Administrator cannot "+action+" GOD");
        check(Denied(()=>db.AdminApproveOrReset("admin","owner","reset-solo","Owner_QA")),"Administrator cannot reset GOD's adventure");
        check(Denied(()=>db.AdminApproveOrReset("admin","owner","approve","Owner_QA","Owner_QA@example.test")),"Administrator cannot approve a different owner's email");
        var saved=JsonSerializer.Serialize(db.Read("user"));var session=db.NewSession("user");
        check(Denied(()=>db.ChangeRole("owner","user","admin","explorer","wrong")),"Role change requires exact explorer confirmation");
        db.ChangeRole("owner","user","admin","explorer","Explorer_QA");
        check(db.FindById("user")!.IsAdmin&&!db.ValidSession(session,"user"),"Promotion updates permission and ends previous sessions");
        check(JsonSerializer.Serialize(db.Read("user"))==saved,"Promotion preserves saved adventures");
        check(Denied(()=>db.ChangeRole("owner","user","admin","explorer","Explorer_QA")),"Stale role changes are rejected");
        db.ChangeRole("owner","user","explorer","admin","Explorer_QA");
        check(!db.FindById("user")!.IsAdmin&&JsonSerializer.Serialize(db.Read("user"))==saved,"Demotion preserves gameplay");
        var backup=db.ExportBackup("owner");
        check(!backup.Tables.ContainsKey("installation_owner"),"Portable backup cannot assign installation ownership");
        check(Denied(()=>db.ImportBackup("admin",backup)),"Ordinary administrator cannot bypass owner protection through import");
        backup.Tables["users"].RemoveAll(row=>row["id"].GetString()=="owner");
        check(Denied(()=>db.ImportBackup("owner",backup))&&db.FindById("owner")!.IsGod,"Import must retain the active bound owner");
        var reopened=new Storage(path);check(reopened.FindById("owner")!.IsGod,"Owner binding survives storage restart");
    }
}

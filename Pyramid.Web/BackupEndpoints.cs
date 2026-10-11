using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Pyramid.Web;

public static class BackupEndpoints
{
    public const long UploadLimit=21*1024*1024;
    public static void MapBackups(this WebApplication app)
    {
        var api=app.MapGroup("/api/admin/backup").RequireAuthorization().RequireRateLimiting("play");
        api.MapGet("",(HttpContext c,Storage db)=>{
            var id=c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if(db.FindById(id) is not {IsAdmin:true,Suspended:false})return Results.Forbid();
            var bytes=JsonSerializer.SerializeToUtf8Bytes(db.ExportBackup(id),new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return Results.File(bytes,"application/json",$"pyramid-backup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
        });
        api.MapPost("/import",async(HttpContext c,Storage db)=>{
            var id=c.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if(db.FindById(id) is not {IsAdmin:true,Suspended:false})return Results.Forbid();
            if(!db.CanImportBackup(id))return Results.Json(new{error="Only GOD may restore a backup on this installation."},statusCode:403);
            try
            {
                var input=await c.Request.ReadFromJsonAsync<BackupImport>();
                if(input?.Confirmation!="REPLACE ALL DATA"||input.Backup is null)return Results.BadRequest(new{error="Type REPLACE ALL DATA to confirm the full restore."});
                var saved=db.ImportBackup(id,input.Backup);
                await c.SignOutAsync("Pyramid");
                return Results.Ok(new{message="Backup restored. All sessions ended. Sign in with an administrator account from the imported backup.",recoveryBackup=saved});
            }
            catch(Exception e) when(e is JsonException or InvalidOperationException or ArgumentException or Microsoft.Data.Sqlite.SqliteException or FormatException or KeyNotFoundException or NullReferenceException or IndexOutOfRangeException)
            {return Results.BadRequest(new{error="Import rejected. The backup must be a compatible Pyramid export with valid accounts, adventures and installed content packs. Existing data was not replaced."});}
        });
    }
}
public record BackupImport(string Confirmation,DatabaseBackup Backup);

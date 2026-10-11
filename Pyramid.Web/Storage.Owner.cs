using Microsoft.Data.Sqlite;

namespace Pyramid.Web;

public partial class Storage
{
    private static string? InstallationOwner(SqliteConnection db,SqliteTransaction? tx)
    {using var cmd=Query(db,tx,"SELECT userId FROM installation_owner WHERE id=1");return cmd.ExecuteScalar() as string;}
    public bool IsGod(string id){using var db=Open();return InstallationOwner(db,null)==id;}
    public bool HasGod(){using var db=Open();return InstallationOwner(db,null)!=null;}
    public bool CanImportBackup(string actor){using var db=Open();var owner=InstallationOwner(db,null);return owner==null||owner==actor;}
    private static void ProtectOwner(SqliteConnection db,SqliteTransaction tx,string actor,string target)
    {if(InstallationOwner(db,tx)==target&&actor!=target)throw new InvalidOperationException("Only GOD may change the installation owner's account or adventure.");}
    public void BootstrapGod(string name)
    {
        if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException("Specify the existing installation owner's explorer name.");
        using var db=Open();using var tx=db.BeginTransaction();string id;
        using(var cmd=Query(db,tx,"SELECT id FROM users WHERE name=$name COLLATE BINARY AND suspended=0",("$name",name)))id=cmd.ExecuteScalar() as string??throw new InvalidOperationException("GOD bootstrap requires the exact existing active explorer name.");
        var owner=InstallationOwner(db,tx);if(owner!=null&&owner!=id)throw new InvalidOperationException("This installation already has an owner.");
        using(var insert=Query(db,tx,"INSERT OR IGNORE INTO installation_owner(id,userId,name) VALUES(1,$id,$name)",("$id",id),("$name",name)))insert.ExecuteNonQuery();
        using(var promote=Query(db,tx,"UPDATE users SET isAdmin=1 WHERE id=$id",("$id",id)))promote.ExecuteNonQuery();
        if(owner==null)Audit(db,tx,"operator",id,"god-provisioned","Installation owner bound by explicit local operator command.");tx.Commit();
    }
    private static void RequireOwnerRestore(SqliteConnection db,SqliteTransaction tx,string actor)
    {var owner=InstallationOwner(db,tx);if(owner!=null&&owner!=actor)throw new InvalidOperationException("Only GOD may restore a backup on this installation.");}
    private static void ValidateOwnerRestore(SqliteConnection db,SqliteTransaction tx,SqliteConnection stage)
    {
        var owner=InstallationOwner(db,tx);if(owner==null)return;
        using var readName=Query(db,tx,"SELECT name FROM installation_owner WHERE id=1");var ownerName=(string)readName.ExecuteScalar()!;
        using var cmd=Query(stage,null,"SELECT COUNT(*) FROM users WHERE id=$id AND name=$name COLLATE BINARY AND isAdmin=1 AND suspended=0",("$id",owner),("$name",ownerName));
        if((long)cmd.ExecuteScalar()!!=1)throw new InvalidOperationException("The backup must preserve the active installation owner and administrator role.");
    }
}

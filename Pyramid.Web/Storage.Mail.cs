using Microsoft.Data.Sqlite;

namespace Pyramid.Web;

internal record QueuedMail(string Id,string? UserId,string Payload,int Attempts);

public partial class Storage
{
    public void EnsureMailStorage()
    {
        using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="""
        CREATE TABLE IF NOT EXISTS mail_outbox(
          id TEXT PRIMARY KEY,userId TEXT,payload TEXT NOT NULL,dedupe TEXT UNIQUE,
          status TEXT NOT NULL DEFAULT 'pending',attempts INTEGER NOT NULL DEFAULT 0,
          created TEXT NOT NULL,nextAttempt TEXT NOT NULL,lastError TEXT);
        CREATE INDEX IF NOT EXISTS mail_pending ON mail_outbox(status,nextAttempt);
        CREATE TRIGGER IF NOT EXISTS mail_account_deleted AFTER DELETE ON users
          BEGIN DELETE FROM mail_outbox WHERE userId=OLD.id; END;
        """;cmd.ExecuteNonQuery();
    }
    internal bool EnqueueMail(string? userId,string payload,string? dedupe)
    {
        using var db=Open();using var cmd=Query(db,null,"""
        INSERT OR IGNORE INTO mail_outbox(id,userId,payload,dedupe,created,nextAttempt)
        VALUES($id,$user,$payload,$dedupe,$now,$now)
        """,("$id",Guid.NewGuid().ToString("N")),("$user",(object?)userId??DBNull.Value),
        ("$payload",payload),("$dedupe",(object?)dedupe??DBNull.Value),("$now",DateTimeOffset.UtcNow.ToString("O")));
        return cmd.ExecuteNonQuery()==1;
    }
    internal QueuedMail? ClaimMail()
    {
        using var db=Open();using var tx=db.BeginTransaction();var now=DateTimeOffset.UtcNow;
        using(var cleanup=Query(db,tx,"""
          DELETE FROM mail_outbox WHERE created<$old;
          UPDATE mail_outbox SET status='expired',payload='' WHERE status IN ('pending','sending') AND created<$expired;
          """,("$old",now.AddDays(-30).ToString("O")),("$expired",now.AddDays(-1).ToString("O"))))cleanup.ExecuteNonQuery();
        QueuedMail? message=null;
        using(var cmd=Query(db,tx,"SELECT id,userId,payload,attempts FROM mail_outbox WHERE status IN ('pending','sending') AND nextAttempt<=$now ORDER BY created LIMIT 1",("$now",now.ToString("O"))))
        using(var r=cmd.ExecuteReader())if(r.Read())message=new(r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.GetString(2),r.GetInt32(3)+1);
        if(message!=null)
        {
            using var claim=Query(db,tx,"UPDATE mail_outbox SET status='sending',attempts=attempts+1,nextAttempt=$lease WHERE id=$id",("$lease",now.AddMinutes(5).ToString("O")),("$id",message.Id));claim.ExecuteNonQuery();
        }
        tx.Commit();return message;
    }
    internal bool MailRecipientCurrent(string? userId,string address,bool allowPendingEmail,bool requireNotifications)
    {
        if(userId==null)return true;
        using var db=Open();using var cmd=Query(db,null,"SELECT COUNT(*) FROM users WHERE id=$id AND ((email=$email COLLATE NOCASE AND emailVerified=1) OR ($pending=1 AND pendingEmail=$email COLLATE NOCASE)) AND suspended=0 AND ($notifications=0 OR emailNotifications=1)",("$id",userId),("$email",address),("$pending",allowPendingEmail?1:0),("$notifications",requireNotifications?1:0));
        return (long)cmd.ExecuteScalar()!>0;
    }
    internal void FinishMail(string id,string status,string? error=null,int attempts=0)
    {
        using var db=Open();using var cmd=Query(db,null,"""
        UPDATE mail_outbox SET status=$status,lastError=$error,nextAttempt=$next,
          payload=CASE WHEN $status='pending' THEN payload ELSE '' END WHERE id=$id
        """,("$status",status),("$error",(object?)error??DBNull.Value),("$id",id),
        ("$next",DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600,30*Math.Pow(2,Math.Clamp(attempts,0,7)))).ToString("O")));
        cmd.ExecuteNonQuery();
    }
    public object MailStatus()
    {
        using var db=Open();using var cmd=Query(db,null,"SELECT status,COUNT(*) FROM mail_outbox GROUP BY status");using var r=cmd.ExecuteReader();
        var counts=new Dictionary<string,long>();while(r.Read())counts[r.GetString(0)]=r.GetInt64(1);return counts;
    }
    internal bool MailWasQueued(string dedupe)
    {
        using var db=Open();using var cmd=Query(db,null,"SELECT COUNT(*) FROM mail_outbox WHERE dedupe=$dedupe",("$dedupe",dedupe));return (long)cmd.ExecuteScalar()!>0;
    }
    // May be called inside account deletion's transaction; the trigger also handles all deletion paths.
    private static void DeleteAccountMail(SqliteConnection db,SqliteTransaction tx,string id)
    {
        using var exists=Query(db,tx,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='mail_outbox'");
        if((long)exists.ExecuteScalar()!>0){using var cmd=Query(db,tx,"DELETE FROM mail_outbox WHERE userId=$id",("$id",id));cmd.ExecuteNonQuery();}
    }
}

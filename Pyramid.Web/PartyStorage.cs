using Microsoft.Data.Sqlite;
using System.Text.Json;
using Pyramid.Web.Game;

namespace Pyramid.Web;
public partial class Storage
{
    private static SqliteCommand Query(SqliteConnection db,SqliteTransaction? tx,string sql,params (string,object)[] args)
    {
        var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;
        foreach(var (key,value) in args)cmd.Parameters.AddWithValue(key,value);return cmd;
    }
    private static Party LoadParty(SqliteConnection db,SqliteTransaction? tx,string id)
    {
        using var cmd=Query(db,tx,"SELECT state FROM parties WHERE id=$id",("$id",id));
        return JsonSerializer.Deserialize<Party>((string)(cmd.ExecuteScalar()??throw new InvalidOperationException("Expedition unavailable.")))!;
    }
    private static void SaveParty(SqliteConnection db,SqliteTransaction tx,Party party)
    {
        using var cmd=Query(db,tx,"UPDATE parties SET state=$state WHERE id=$id",("$id",party.Id),("$state",JsonSerializer.Serialize(party)));cmd.ExecuteNonQuery();
    }
    private static string? Active(SqliteConnection db,SqliteTransaction? tx,string user)
    {
        using var cmd=Query(db,tx,"SELECT partyId FROM active_games WHERE userId=$id",("$id",user));return cmd.ExecuteScalar() as string;
    }
    private static void SleepCurrent(SqliteConnection db,SqliteTransaction tx,string user)
    {
        var active=Active(db,tx,user);if(active==null)return;var party=LoadParty(db,tx,active);
        if(party.Members.TryGetValue(user,out var member)){member.Awake=false;member.Offers.Clear();party.Revision++;SaveParty(db,tx,party);}
    }
    public void Sleep(string user){using var db=Open();using var tx=db.BeginTransaction();SleepCurrent(db,tx,user);tx.Commit();}
    private static void Wake(Party party,string user)
    {
        var member=party.Members[user];bool wasAsleep=!member.Online;member.Awake=true;member.Seen=DateTimeOffset.UtcNow;
        if(wasAsleep){foreach(var other in party.Members.Values.Where(p=>p!=member&&Cooperation.Together(p,member)))Cooperation.Tell(other,$"{member.Name} awakens from a trance.");party.Revision++;}
    }
    private static object PartyView(Party party,string user)
    {
        var member=party.Members[user];Cooperation.Bind(party,member.State);
        return new{game=Engine.View(member.State),party=new{party.Id,party.Name,owner=party.Owner,party.Open,party.Revision,members=party.Members.Select(p=>new{id=p.Key,name=p.Value.Name,level=p.Value.State.Level,room=Engine.Current(p.Value.State).Rooms[p.Value.State.Room].Name,here=Cooperation.Together(member,p.Value),online=p.Value.Online,score=p.Value.State.Score,moves=p.Value.State.Moves,achievements=p.Value.State.Achievements}),requests=user==party.Owner?party.Requests:null,invitations=user==party.Owner?party.Invitations:null}};
    }
    public object PlayView(string user)
    {
        using var db=Open();using var tx=db.BeginTransaction();var id=Active(db,tx,user);
        if(id==null){tx.Commit();return new{game=Engine.View(Read(user)),party=(object?)null};}
        var party=LoadParty(db,tx,id);Wake(party,user);SaveParty(db,tx,party);tx.Commit();return PartyView(party,user);
    }
    public (bool accepted,object view) PlayCommand(string user,long revision,string text,string? expectedParty=null)
    {
        using var db=Open();using var tx=db.BeginTransaction();var id=Active(db,tx,user);
        if(id!=expectedParty){tx.Commit();return(false,PlayView(user));}
        if(id==null)
        {
            using var read=Query(db,tx,"SELECT state FROM users WHERE id=$id",("$id",user));
            var state=JsonSerializer.Deserialize<Expedition>((string)read.ExecuteScalar()!)!;
            if(state.Revision!=revision)return(false,new{game=Engine.View(state),party=(object?)null});
            Engine.Execute(state,text);
            using var save=Query(db,tx,"UPDATE users SET state=$state,lastSeen=$now WHERE id=$id",("$state",JsonSerializer.Serialize(state)),("$now",DateTimeOffset.UtcNow.ToString("O")),("$id",user));save.ExecuteNonQuery();tx.Commit();
            return(true,new{game=Engine.View(state),party=(object?)null});
        }
        var party=LoadParty(db,tx,id);
        if(party.Revision!=revision)return(false,PartyView(party,user));
        Wake(party,user);Cooperation.Execute(party,user,text);SaveParty(db,tx,party);tx.Commit();return(true,PartyView(party,user));
    }
    public object Lobby(string user)
    {
        using var db=Open();using var cmd=Query(db,null,"SELECT state FROM parties");using var reader=cmd.ExecuteReader();var list=new List<object>();
        while(reader.Read())
        {
            var p=JsonSerializer.Deserialize<Party>(reader.GetString(0))!;
            if(p.Open||p.Members.ContainsKey(user)||p.Invitations.ContainsKey(user))list.Add(new{p.Id,p.Name,p.Open,owner=p.Members[p.Owner].Name,member=p.Members.ContainsKey(user),invited=p.Invitations.ContainsKey(user),requested=p.Requests.ContainsKey(user),count=p.Members.Count,pack=p.World.PackId});
        }
        return new{active=Active(db,null,user),games=list,packs=ContentPacks.Catalog()};
    }
    public bool HasInvitation(string partyId,string user)
    {
        using var db=Open();return LoadParty(db,null,partyId).Invitations.ContainsKey(user);
    }
    public string NewParty(string user,string name,string pack,bool open)
    {
        using var db=Open();using var tx=db.BeginTransaction();
        using(var count=Query(db,tx,"SELECT COUNT(*) FROM parties WHERE ownerId=$id",("$id",user)))if((long)count.ExecuteScalar()!>=10)throw new InvalidOperationException("You may host up to ten saved expeditions.");
        using var account=Query(db,tx,"SELECT name FROM users WHERE id=$id",("$id",user));string username=(string)account.ExecuteScalar()!;
        var state=Engine.New(pack);state.Inventory=["lamp"];state.LampOn=true;state.Items[state.Room].Remove("lamp");
        state.Transcript=[];Engine.Say(state,"This is a shared expedition. Your satchel and notes are personal; floor objects, doors and puzzles belong to the world. Invite companions through EXPEDITIONS. Type HELP for commands.");Engine.Look(state);
        var party=new Party{Id=Guid.NewGuid().ToString("N"),Owner=user,Name=name,Open=open,World=state};
        party.Members[user]=new Companion{Name=username,State=JsonSerializer.Deserialize<Expedition>(JsonSerializer.Serialize(state))!,Awake=true,Seen=DateTimeOffset.UtcNow};
        SleepCurrent(db,tx,user);
        using(var insert=Query(db,tx,"INSERT INTO parties(id,ownerId,state) VALUES($id,$owner,$state)",("$id",party.Id),("$owner",user),("$state",JsonSerializer.Serialize(party))))insert.ExecuteNonQuery();
        using(var active=Query(db,tx,"INSERT INTO active_games(userId,partyId) VALUES($user,$id) ON CONFLICT(userId) DO UPDATE SET partyId=$id",("$user",user),("$id",party.Id)))active.ExecuteNonQuery();
        tx.Commit();return party.Id;
    }
    public void PartyAction(string user,string id,string action,string target="")
    {
        using var db=Open();using var tx=db.BeginTransaction();
        if(action=="solo")
        {
            SleepCurrent(db,tx,user);using var clear=Query(db,tx,"DELETE FROM active_games WHERE userId=$id",("$id",user));clear.ExecuteNonQuery();tx.Commit();return;
        }
        var party=LoadParty(db,tx,id);
        if(action=="resume"||action=="accept")
        {
            if(!party.Members.ContainsKey(user))
            {
                if(!party.Invitations.ContainsKey(user))throw new InvalidOperationException("An invitation or approved request is required.");
                if(party.Members.Count>=8)throw new InvalidOperationException("This expedition has eight explorers already.");
                using var account=Query(db,tx,"SELECT name FROM users WHERE id=$id",("$id",user));
                var state=Engine.New(party.World.PackId);state.Level=party.World.Level;state.Room=state.PackId=="pyramid"?"camp":ContentPacks.Get(state.PackId).Levels.Single(l=>l.Number==state.Level).Start;
                state.Visited=[state.Room];state.Inventory=["lamp"];state.LampOn=true;state.Transcript=[];
                Cooperation.Bind(party,state);Engine.Look(state);
                party.Members[user]=new(){Name=(string)account.ExecuteScalar()!,State=state};
                party.Invitations.Remove(user);party.Requests.Remove(user);
            }
            // Reload only when switching from another expedition, avoiding overwriting this party.
            if(Active(db,tx,user)!=id)SleepCurrent(db,tx,user);
            using var set=Query(db,tx,"INSERT INTO active_games(userId,partyId) VALUES($user,$id) ON CONFLICT(userId) DO UPDATE SET partyId=$id",("$user",user),("$id",id));set.ExecuteNonQuery();Wake(party,user);
        }
        else if(action=="request")
        {
            if(!party.Open||party.Members.Count>=8)throw new InvalidOperationException("This expedition is not accepting requests.");
            if(party.Members.ContainsKey(user))throw new InvalidOperationException("You already belong to this expedition.");
            if(party.Requests.ContainsKey(user)||party.Invitations.ContainsKey(user)){tx.Commit();return;}
            if(party.Requests.Count>=50)throw new InvalidOperationException("The host must review pending requests first.");
            using var account=Query(db,tx,"SELECT name FROM users WHERE id=$id",("$id",user));party.Requests[user]=(string)account.ExecuteScalar()!;
        }
        else if(action=="decline")
        {
            if(!party.Invitations.Remove(user))throw new InvalidOperationException("No invitation is pending for you.");
        }
        else
        {
            if(party.Owner!=user)throw new InvalidOperationException("Only the host may manage this expedition.");
            if(action is "open" or "close")party.Open=action=="open";
            else if(action=="invite")
            {
                using var account=Query(db,tx,"SELECT id,name FROM users WHERE name=$name COLLATE NOCASE",("$name",target));using var reader=account.ExecuteReader();
                if(!reader.Read())throw new InvalidOperationException("That explorer was not found.");
                if(party.Invitations.Count>=50)throw new InvalidOperationException("Invitation limit reached.");
                if(!party.Members.ContainsKey(reader.GetString(0)))party.Invitations[reader.GetString(0)]=reader.GetString(1);
            }
            else if(action is "approve" or "deny")
            {
                if(!party.Requests.Remove(target,out var name))throw new InvalidOperationException("Request no longer pending.");
                if(action=="approve")party.Invitations[target]=name;
            }
            else if(action=="revoke")party.Invitations.Remove(target);
            else throw new InvalidOperationException("Unknown expedition action.");
        }
        party.Revision++;SaveParty(db,tx,party);tx.Commit();
    }
}

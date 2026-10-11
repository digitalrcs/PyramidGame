namespace Pyramid.Web;

// Imports are exclusive; normal requests and mail delivery may run concurrently.
public static class MaintenanceGate
{
    static readonly SemaphoreSlim Turn = new(1,1), Room = new(1,1), Count = new(1,1);
    static int readers;
    public static async Task<IDisposable> Enter(bool exclusive, CancellationToken token)
    {
        await Turn.WaitAsync(token);
        if(exclusive)
        {
            try { await Room.WaitAsync(token); }
            catch { Turn.Release(); throw; }
            return new Lease(()=>{Room.Release();Turn.Release();});
        }
        Turn.Release();
        await Count.WaitAsync(token);
        try { if(readers==0)await Room.WaitAsync(token);readers++; }
        finally { Count.Release(); }
        return new Lease(()=>{Count.Wait();try{if(--readers==0)Room.Release();}finally{Count.Release();}});
    }
    sealed class Lease(Action release):IDisposable
    {
        Action? action=release;
        public void Dispose()=>Interlocked.Exchange(ref action,null)?.Invoke();
    }
}

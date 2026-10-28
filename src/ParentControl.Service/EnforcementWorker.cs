using Microsoft.Extensions.Options;

namespace ParentControl.Service;

public sealed class EnforcementWorker(IStateStore store,IAccountController accounts,IOptions<ParentOptions> options,ILogger<EnforcementWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await store.InitializeAsync(stop); bool? applied=null;
        while(!stop.IsCancellationRequested)
        {
            try
            {
                var state=await store.GetStateAsync(stop);var desired=state.Locked;
                if(desired&&state.LockedUntil is { } until&&until<=DateTimeOffset.UtcNow){state=await store.SetStateAsync(false,null,"Zeit abgelaufen",stop);desired=false;}
                var scheduled=await ScheduledState(stop);if(scheduled.HasValue&&state.Reason.StartsWith("Zeitplan",StringComparison.Ordinal)&&desired!=scheduled.Value){state=await store.SetStateAsync(scheduled.Value,null,"Zeitplan",stop);desired=scheduled.Value;}
                if(applied!=desired){await accounts.SetDisabledAsync(options.Value.ChildUser,desired,stop);applied=desired;}
            }catch(Exception ex){log.LogError(ex,"Sperrzustand konnte nicht erzwungen werden; erneuter Versuch folgt.");applied=null;}
            await Task.Delay(TimeSpan.FromSeconds(10),stop);
        }
    }
    private async Task<bool?> ScheduledState(CancellationToken ct)
    {
        var now=DateTime.Now;foreach(var r in await store.GetScheduleAsync(ct)){if(r.DayOfWeek!=(int)now.DayOfWeek)continue;if(TimeOnly.TryParse(r.Start,out var a)&&TimeOnly.TryParse(r.End,out var b)){var t=TimeOnly.FromDateTime(now);if(a<=t&&t<b)return r.Locked;}}return null;
    }
}

public sealed class ControlService(IStateStore store,IAccountController accounts,IOptions<ParentOptions> options)
{
    private readonly SemaphoreSlim _gate=new(1,1);
    public async Task<ControlState> LockAsync(DateTimeOffset? until,string reason,CancellationToken ct)
    {
        await _gate.WaitAsync(ct);try{var state=await store.SetStateAsync(true,until,reason,ct);await accounts.SetDisabledAsync(options.Value.ChildUser,true,ct);_ = Task.Run(()=>accounts.WarnAndLogoffAsync(options.Value.ChildUser,TimeSpan.FromSeconds(Math.Clamp(options.Value.WarningSeconds,5,300)),CancellationToken.None));return state;}catch{await store.SetStateAsync(false,null,"Sperrversuch fehlgeschlagen",ct);throw;}finally{_gate.Release();}
    }
    public async Task<ControlState> UnlockAsync(CancellationToken ct){await _gate.WaitAsync(ct);try{await accounts.SetDisabledAsync(options.Value.ChildUser,false,ct);return await store.SetStateAsync(false,null,"Manuell freigegeben",ct);}finally{_gate.Release();}}
}

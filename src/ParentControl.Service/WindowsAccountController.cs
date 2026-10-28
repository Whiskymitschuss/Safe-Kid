using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ParentControl.Service;

public interface IAccountController { Task SetDisabledAsync(string localUser, bool disabled, CancellationToken ct); Task WarnAndLogoffAsync(string localUser, TimeSpan delay, CancellationToken ct); }

public sealed class WindowsAccountController(ILogger<WindowsAccountController> log) : IAccountController
{
    private const uint FILTER_NORMAL_ACCOUNT=2, UF_ACCOUNTDISABLE=2;
    public Task SetDisabledAsync(string localUser,bool disabled,CancellationToken ct)
    {
        EnsureSimpleLocalName(localUser); var rc=NetUserGetInfo(null,localUser,1,out var ptr);if(rc!=0)throw new Win32Exception((int)rc,"Lokales Kinderkonto wurde nicht gefunden.");
        try{var info=Marshal.PtrToStructure<USER_INFO_1>(ptr);if((info.usri1_flags&FILTER_NORMAL_ACCOUNT)==0)throw new InvalidOperationException("Ausgewähltes Konto ist kein lokales Standardkonto.");var flags=disabled?info.usri1_flags|UF_ACCOUNTDISABLE:info.usri1_flags&~UF_ACCOUNTDISABLE;var set=new USER_INFO_1008{usri1008_flags=flags};rc=NetUserSetInfo(null,localUser,1008,ref set,out _);if(rc!=0)throw new Win32Exception((int)rc,"Kontostatus konnte nicht gesetzt werden.");log.LogInformation("Kontostatus für {User}: disabled={Disabled}",localUser,disabled);}finally{NetApiBufferFree(ptr);}return Task.CompletedTask;
    }
    public async Task WarnAndLogoffAsync(string localUser,TimeSpan delay,CancellationToken ct)
    {
        foreach(var s in Sessions(localUser)) WTSMessage(s,"ParentControl Remote",$"Dieser PC wird in {(int)delay.TotalSeconds} Sekunden gesperrt. Bitte Arbeit speichern.",Math.Max(5,(int)delay.TotalSeconds));
        await Task.Delay(delay,ct); foreach(var s in Sessions(localUser)) if(!WTSLogoffSession(IntPtr.Zero,s,true))log.LogWarning("Sitzung {Session} konnte nicht abgemeldet werden: {Error}",s,Marshal.GetLastWin32Error());
    }
    private static IEnumerable<int> Sessions(string user){if(!WTSEnumerateSessions(IntPtr.Zero,0,1,out var p,out var n))yield break;try{var size=Marshal.SizeOf<WTS_SESSION_INFO>();for(var i=0;i<n;i++){var x=Marshal.PtrToStructure<WTS_SESSION_INFO>(p+i*size);if(WTSQuerySessionInformation(IntPtr.Zero,x.SessionId,5,out var up,out var bytes)){try{var name=Marshal.PtrToStringUni(up);if(string.Equals(name,user,StringComparison.OrdinalIgnoreCase))yield return x.SessionId;}finally{WTSFreeMemory(up);}}}}finally{WTSFreeMemory(p);}}
    private static void WTSMessage(int session,string title,string message,int seconds){var response=0;WTSSendMessage(IntPtr.Zero,session,title,title.Length*2,message,message.Length*2,0,seconds,out response,false);}
    private static void EnsureSimpleLocalName(string user){if(string.IsNullOrWhiteSpace(user)||user.Contains('\\')||user.Contains('@'))throw new InvalidOperationException("Nur lokale Windows-Konten werden unterstützt.");}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct USER_INFO_1{public string usri1_name;public string usri1_password;public uint usri1_password_age;public uint usri1_priv;public string usri1_home_dir;public string usri1_comment;public uint usri1_flags;public string usri1_script_path;}
    [StructLayout(LayoutKind.Sequential)]private struct USER_INFO_1008{public uint usri1008_flags;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct WTS_SESSION_INFO{public int SessionId;public string pWinStationName;public int State;}
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]private static extern uint NetUserGetInfo(string? server,string user,uint level,out IntPtr buf);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]private static extern uint NetUserSetInfo(string? server,string user,uint level,ref USER_INFO_1008 buf,out uint error);
    [DllImport("Netapi32.dll")]private static extern uint NetApiBufferFree(IntPtr p);
    [DllImport("Wtsapi32.dll",SetLastError=true)]private static extern bool WTSEnumerateSessions(IntPtr server,int reserved,int version,out IntPtr info,out int count);
    [DllImport("Wtsapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool WTSQuerySessionInformation(IntPtr server,int session,int cls,out IntPtr buffer,out int bytes);
    [DllImport("Wtsapi32.dll")]private static extern void WTSFreeMemory(IntPtr p);
    [DllImport("Wtsapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool WTSSendMessage(IntPtr server,int session,string title,int titleBytes,string msg,int msgBytes,int style,int timeout,out int response,bool wait);
    [DllImport("Wtsapi32.dll",SetLastError=true)]private static extern bool WTSLogoffSession(IntPtr server,int session,bool wait);
}

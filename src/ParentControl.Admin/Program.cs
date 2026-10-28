using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ParentControl.Admin;
internal static class Program
{
    static int Main(string[] args)
    {
        if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)){Console.Error.WriteLine("Dieses Werkzeug muss als Administrator ausgeführt werden.");return 5;}
        if(args.Length!=2||args[0]!="emergency-unlock"||args[1].Contains('\\')||args[1].Contains('@')){Console.WriteLine("Verwendung: ParentControl.Admin.exe emergency-unlock <lokales-kinderkonto>");return 2;}
        var rc=NetUserGetInfo(null,args[1],1,out var ptr);if(rc!=0)throw new Win32Exception((int)rc);
        try{var u=Marshal.PtrToStructure<USER_INFO_1>(ptr);var info=new USER_INFO_1008{flags=u.flags&~2u};rc=NetUserSetInfo(null,args[1],1008,ref info,out _);if(rc!=0)throw new Win32Exception((int)rc);Console.WriteLine("Konto wurde lokal freigegeben. Danach den Dienststatus prüfen.");return 0;}finally{NetApiBufferFree(ptr);}
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct USER_INFO_1{public string name,password;public uint age,priv;public string home,comment;public uint flags;public string script;}
    [StructLayout(LayoutKind.Sequential)]private struct USER_INFO_1008{public uint flags;}
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]private static extern uint NetUserGetInfo(string? server,string user,uint level,out IntPtr ptr);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]private static extern uint NetUserSetInfo(string? server,string user,uint level,ref USER_INFO_1008 info,out uint error);
    [DllImport("Netapi32.dll")]private static extern uint NetApiBufferFree(IntPtr ptr);
}

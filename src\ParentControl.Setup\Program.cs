using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;

namespace ParentControl.Setup;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Contains("/uninstall",StringComparer.OrdinalIgnoreCase)){Uninstall();return;}
        Application.Run(new SetupForm());
    }

    internal static readonly string InstallDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"ParentControl Remote");
    internal static readonly string DataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"ParentControlRemote");

    internal static ProcessResult Run(string file,string arguments)
    {
        using var p=Process.Start(new ProcessStartInfo(file,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})??throw new InvalidOperationException($"{file} konnte nicht gestartet werden.");
        var stdout=p.StandardOutput.ReadToEnd();var stderr=p.StandardError.ReadToEnd();p.WaitForExit();return new(p.ExitCode,stdout,stderr);
    }

    internal static void ExtractPayload(string target)
    {
        Directory.CreateDirectory(target);var assembly=Assembly.GetExecutingAssembly();var resources=assembly.GetManifestResourceNames().Where(x=>x.StartsWith("Payload/",StringComparison.Ordinal));
        if(!resources.Any())throw new InvalidOperationException("Die Setup-Nutzlast fehlt. Bitte den Installer mit scripts\\build-oneclick.ps1 erzeugen.");
        foreach(var resource in resources){var relative=resource["Payload/".Length..].Replace('/',Path.DirectorySeparatorChar).Replace('\\',Path.DirectorySeparatorChar);var output=Path.GetFullPath(Path.Combine(target,relative));if(!output.StartsWith(Path.GetFullPath(target)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Ungültiger Nutzlastpfad.");Directory.CreateDirectory(Path.GetDirectoryName(output)!);using var source=assembly.GetManifestResourceStream(resource)!;using var destination=File.Create(output);source.CopyTo(destination);}
    }

    internal static void Uninstall()
    {
        if(MessageBox.Show("ParentControl Remote entfernen und das Kinderkonto vorsorglich freigeben?","Deinstallation",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        try
        {
            Run("sc.exe","stop ParentControlRemote");
            var config=Path.Combine(InstallDir,"appsettings.json");if(File.Exists(config)){using var doc=JsonDocument.Parse(File.ReadAllText(config));var child=doc.RootElement.GetProperty("ParentControl").GetProperty("ChildUser").GetString();var admin=Path.Combine(InstallDir,"ParentControl.Admin.exe");if(!string.IsNullOrWhiteSpace(child)&&File.Exists(admin))Run(admin,$"emergency-unlock \"{child}\"");}
            Run("sc.exe","delete ParentControlRemote");Run("powershell.exe","-NoProfile -NonInteractive -Command \"Remove-NetFirewallRule -DisplayName 'ParentControl Remote (Tailscale)' -ErrorAction SilentlyContinue\"");
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ParentControlRemote",false);
            MessageBox.Show("Dienst und Firewallregel wurden entfernt. Die Programmdateien können nach dem nächsten Neustart gelöscht werden. Die lokale Datenbank bleibt zur Sicherheit erhalten.","Deinstallation abgeschlossen");
        }catch(Exception ex){MessageBox.Show(ex.Message,"Deinstallation fehlgeschlagen",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
}

internal sealed record ProcessResult(int ExitCode,string Output,string Error);

internal sealed class SetupForm:Form
{
    private readonly ComboBox _users=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=390};
    private readonly TextBox _adminName=new(){Width=390,Text="ParentAdmin"};
    private readonly TextBox _adminPassword=new(){Width=390,UseSystemPasswordChar=true};
    private readonly TextBox _adminPassword2=new(){Width=390,UseSystemPasswordChar=true};
    private readonly NumericUpDown _warning=new(){Minimum=5,Maximum=300,Value=30,Width=90};
    private readonly CheckBox _tailscale=new(){Text="Tailscale HTTPS nach Installation automatisch einrichten",AutoSize=true,Checked=true};
    private readonly Button _install=new(){Text="JETZT INSTALLIEREN",Height=48,Width=420,BackColor=Color.FromArgb(38,108,229),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
    private readonly ProgressBar _progress=new(){Width=420,Style=ProgressBarStyle.Marquee,Visible=false};
    private readonly Label _status=new(){AutoSize=true,MaximumSize=new Size(420,0),ForeColor=Color.DimGray};

    public SetupForm()
    {
        Text="ParentControl Remote – Installation";ClientSize=new Size(500,710);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);BackColor=Color.White;
        var title=new Label{Text="ParentControl Remote",Font=new Font("Segoe UI Semibold",20),AutoSize=true};var intro=new Label{Text="Sichere Fernsteuerung des Kinder-PCs über Tailscale",AutoSize=true,ForeColor=Color.DimGray};
        var accountLabel=new Label{Text="Konto des Kindes",AutoSize=true};var help=new Label{Text="Dieses Konto wird nach erfolgreicher Anlage des Elternkontos zum Standardnutzer herabgestuft.",MaximumSize=new Size(420,0),AutoSize=true,ForeColor=Color.DimGray};
        var adminLabel=new Label{Text="Neues lokales Eltern-Administratorkonto",AutoSize=true};var passwordLabel=new Label{Text="Passwort (mindestens 12 Zeichen)",AutoSize=true};var password2Label=new Label{Text="Passwort wiederholen",AutoSize=true};
        var warningLabel=new Label{Text="Warnfrist vor Abmeldung (Sekunden)",AutoSize=true};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(36,28,36,28),AutoScroll=true};panel.Controls.AddRange([title,intro,Spacer(10),accountLabel,_users,help,Spacer(8),adminLabel,_adminName,passwordLabel,_adminPassword,password2Label,_adminPassword2,Spacer(8),warningLabel,_warning,Spacer(8),_tailscale,Spacer(12),_install,_progress,_status]);Controls.Add(panel);
        Shown+=(_,_)=>LoadUsers();_install.Click+=async(_,_)=>await InstallAsync();
    }
    private static Control Spacer(int h)=>new Panel{Height=h,Width=1};
    private void LoadUsers()
    {
        try
        {
            var ps="Get-LocalUser | Where-Object {$_.Enabled -eq $true -and $_.PrincipalSource -eq 'Local'} | Select-Object -ExpandProperty Name";
            var r=Program.Run("powershell.exe",$"-NoProfile -NonInteractive -Command \"{ps.Replace("\"","\\\"")}\"");if(r.ExitCode!=0)throw new InvalidOperationException(r.Error);foreach(var line in r.Output.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries))_users.Items.Add(line.Trim());if(_users.Items.Count>0)_users.SelectedIndex=0;else{_install.Enabled=false;_status.Text="Kein aktiviertes lokales Standardkonto gefunden.";}
        }catch(Exception ex){_install.Enabled=false;_status.Text="Konten konnten nicht gelesen werden: "+ex.Message;}
    }
    private async Task InstallAsync()
    {
        if(_users.SelectedItem is not string child)return;var admin=_adminName.Text.Trim();var password=_adminPassword.Text;
        if(admin.Length is <3 or >20||admin.IndexOfAny(['\\','/','[',']',':',';','|','=','+',',','*','?','<','>','@'])>=0){MessageBox.Show("Bitte einen gültigen lokalen Kontonamen mit 3–20 Zeichen eingeben.");return;}if(string.Equals(admin,child,StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Eltern- und Kinderkonto müssen verschieden sein.");return;}if(password.Length<12||password!=_adminPassword2.Text){MessageBox.Show("Die Passwörter müssen übereinstimmen und mindestens 12 Zeichen lang sein.");return;}
        if(MessageBox.Show($"Neues Eltern-Administratorkonto: {admin}\nKinderkonto: {child}\n\nZuerst wird das Elternkonto angelegt und geprüft. Erst danach verliert das Kinderkonto seine Administratorrechte. Fortfahren?","Sicherheitsprüfung",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        _install.Enabled=false;_users.Enabled=false;_adminName.Enabled=false;_adminPassword.Enabled=false;_adminPassword2.Enabled=false;_progress.Visible=true;
        try{await Task.Run(()=>Install(child,admin,password,(int)_warning.Value,_tailscale.Checked));_adminPassword.Clear();_adminPassword2.Clear();_progress.Visible=false;_status.ForeColor=Color.DarkGreen;_status.Text="Installation abgeschlossen. Die Elternanmeldung wird jetzt geöffnet.";MessageBox.Show($"ParentControl Remote wurde installiert.\n\nEltern-Administratorkonto: {admin}\nKinderkonto: {child} (Standardnutzer)\n\nBewahren Sie das Elternpasswort sicher auf.","Fertig",MessageBoxButtons.OK,MessageBoxIcon.Information);Process.Start(new ProcessStartInfo("http://127.0.0.1:8765"){UseShellExecute=true});}
        catch(Exception ex){_adminPassword.Clear();_adminPassword2.Clear();_progress.Visible=false;_status.ForeColor=Color.DarkRed;_status.Text=ex.Message;_install.Enabled=true;_users.Enabled=true;_adminName.Enabled=true;_adminPassword.Enabled=true;_adminPassword2.Enabled=true;}
    }
    private static void Install(string child,string admin,string password,int warning,bool tailscale)
    {
        NativeAccounts.CreateVerifiedAdministrator(admin,password);
        try{NativeAccounts.RemoveFromAdministrators(child);}catch{NativeAccounts.DeleteUser(admin);throw;}
        Directory.CreateDirectory(Program.InstallDir);Directory.CreateDirectory(Program.DataDir);Program.Run("sc.exe","stop ParentControlRemote");
        Program.ExtractPayload(Program.InstallDir);
        var configPath=Path.Combine(Program.InstallDir,"appsettings.json");using(var doc=JsonDocument.Parse(File.ReadAllText(configPath))){var root=JsonSerializer.Deserialize<Dictionary<string,object>>(doc.RootElement.GetRawText())!;var pc=JsonSerializer.Deserialize<Dictionary<string,object>>(doc.RootElement.GetProperty("ParentControl").GetRawText())!;pc["ChildUser"]=child;pc["WarningSeconds"]=warning;root["ParentControl"]=pc;File.WriteAllText(configPath,JsonSerializer.Serialize(root,new JsonSerializerOptions{WriteIndented=true}));}
        Program.Run("sc.exe","delete ParentControlRemote");Thread.Sleep(800);var service=Path.Combine(Program.InstallDir,"ParentControl.Service.exe");Check(Program.Run("sc.exe",$"create ParentControlRemote binPath= \"\\\"{service}\\\"\" start= auto DisplayName= \"ParentControl Remote\""),"Dienst konnte nicht angelegt werden");Program.Run("sc.exe","description ParentControlRemote \"Sichere lokale Elternsteuerung über Tailscale\"");Program.Run("sc.exe","failure ParentControlRemote reset= 86400 actions= restart/5000/restart/15000/restart/60000");
        var firewall="Remove-NetFirewallRule -DisplayName 'ParentControl Remote (Tailscale)' -ErrorAction SilentlyContinue; New-NetFirewallRule -DisplayName 'ParentControl Remote (Tailscale)' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8765 -RemoteAddress 100.64.0.0/10 -Profile Any | Out-Null";Check(Program.Run("powershell.exe",$"-NoProfile -NonInteractive -Command \"{firewall}\""),"Firewallregel konnte nicht angelegt werden");Check(Program.Run("sc.exe","start ParentControlRemote"),"Dienst konnte nicht gestartet werden");
        File.Copy(Application.ExecutablePath,Path.Combine(Program.InstallDir,"Uninstall.exe"),true);using var key=Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ParentControlRemote");key.SetValue("DisplayName","ParentControl Remote");key.SetValue("DisplayVersion","1.0.0");key.SetValue("Publisher","ParentControl Remote");key.SetValue("InstallLocation",Program.InstallDir);key.SetValue("UninstallString",$"\"{Path.Combine(Program.InstallDir,"Uninstall.exe")}\" /uninstall");key.SetValue("NoModify",1);
        if(tailscale&&File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"Tailscale\tailscale.exe"))){var ts=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"Tailscale\tailscale.exe");Program.Run(ts,"serve --bg --https=443 http://127.0.0.1:8765");}
    }
    private static void Check(ProcessResult r,string context){if(r.ExitCode!=0)throw new InvalidOperationException($"{context}: {r.Error}{r.Output}");}
}

internal static class NativeAccounts
{
    private const uint UserPrivUser=1,UfScript=1,UfNormalAccount=0x200,NerrSuccess=0,NerrUserExists=2224,ErrorMemberNotInAlias=1377;
    public static void CreateVerifiedAdministrator(string user,string password)
    {
        var info=new USER_INFO_1{name=user,password=password,priv=UserPrivUser,flags=UfScript|UfNormalAccount};var rc=NetUserAdd(null,1,ref info,out _);if(rc==NerrUserExists)throw new InvalidOperationException("Das gewünschte Elternkonto existiert bereits.");if(rc!=NerrSuccess)throw new Win32Exception((int)rc,"Elternkonto konnte nicht angelegt werden.");
        try{var group=AdministratorGroupName();var member=new LOCALGROUP_MEMBERS_INFO_3{name=user};rc=NetLocalGroupAddMembers(null,group,3,ref member,1);if(rc!=NerrSuccess)throw new Win32Exception((int)rc,"Elternkonto konnte nicht zum Administrator gemacht werden.");if(!LogonUser(user,".",password,2,0,out var token))throw new Win32Exception(Marshal.GetLastWin32Error(),"Die Anmeldung des neuen Elternkontos konnte nicht verifiziert werden.");CloseHandle(token);}catch{DeleteUser(user);throw;}
    }
    public static void RemoveFromAdministrators(string user){var member=new LOCALGROUP_MEMBERS_INFO_3{name=user};var rc=NetLocalGroupDelMembers(null,AdministratorGroupName(),3,ref member,1);if(rc!=NerrSuccess&&rc!=ErrorMemberNotInAlias)throw new Win32Exception((int)rc,"Das Kinderkonto konnte nicht zum Standardnutzer herabgestuft werden.");}
    public static void DeleteUser(string user)=>NetUserDel(null,user);
    private static string AdministratorGroupName(){var account=(NTAccount)new SecurityIdentifier("S-1-5-32-544").Translate(typeof(NTAccount));var value=account.Value;return value[(value.IndexOf('\\')+1)..];}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct USER_INFO_1{public string name,password;public uint passwordAge,priv;public string? homeDir,comment;public uint flags;public string? scriptPath;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct LOCALGROUP_MEMBERS_INFO_3{public string name;}
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]private static extern uint NetUserAdd(string? server,uint level,ref USER_INFO_1 info,out uint error);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]private static extern uint NetUserDel(string? server,string user);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]private static extern uint NetLocalGroupAddMembers(string? server,string group,uint level,ref LOCALGROUP_MEMBERS_INFO_3 member,uint count);
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]private static extern uint NetLocalGroupDelMembers(string? server,string group,uint level,ref LOCALGROUP_MEMBERS_INFO_3 member,uint count);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool LogonUser(string user,string domain,string password,int type,int provider,out IntPtr token);
    [DllImport("kernel32.dll")]private static extern bool CloseHandle(IntPtr handle);
}

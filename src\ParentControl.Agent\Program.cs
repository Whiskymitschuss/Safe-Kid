using System.IO.Pipes;
using System.Text;
using System.Windows.Forms;

namespace ParentControl.Agent;
internal static class Program
{
    [STAThread] static async Task Main()
    {
        ApplicationConfiguration.Initialize();
        while(true)
        {
            try{await using var pipe=new NamedPipeClientStream(".","ParentControlRemote.Agent",PipeDirection.In,PipeOptions.Asynchronous);await pipe.ConnectAsync(5000);using var reader=new StreamReader(pipe,Encoding.UTF8);var message=await reader.ReadToEndAsync();if(!string.IsNullOrWhiteSpace(message))MessageBox.Show(message,"ParentControl Remote",MessageBoxButtons.OK,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button1,MessageBoxOptions.ServiceNotification);}catch(TimeoutException){}catch(IOException){}await Task.Delay(3000);
        }
    }
}

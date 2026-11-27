using System;
using Tekken5Launcher;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

static class Setup {
    [STAThread] static int Main(string[] args) {
        if (args.Length == 2 && args[0] == "--verify") {
            try { Extract(Path.GetFullPath(args[1])); Shortcut(args[1],Path.Combine(args[1],"Tekken 5 Recomp.lnk")); return 0; }
            catch (Exception ex) { File.WriteAllText(args[1]+".error.txt",ex.ToString()); return 1; }
        }
        Application.EnableVisualStyles();
        var form = new Form { Text="Instalar Tekken 5 Vulkan", Width=490, Height=190, StartPosition=FormStartPosition.CenterScreen, FormBorderStyle=FormBorderStyle.FixedDialog, MaximizeBox=false };
        var label = new Label { Left=16, Top=16, Width=445, Height=51, Text="Instala o launcher, as ferramentas e um atalho. Na primeira abertura, selecione sua ISO americana SLUS-21059 v1.00 e clique em Preparar jogo." };
        var button = new Button { Left=16, Top=82, Width=150, Height=29, Text="Instalar e abrir" };
        form.Controls.Add(label); form.Controls.Add(button);
        button.Click += async (sender,e) => {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Tekken5Vulkan");
            button.Enabled=false;
            bool working=true;
            FormClosingEventHandler prevent = (s,ev) => { ev.Cancel=working; };
            form.FormClosing += prevent;
            label.Text="Extraindo os arquivos do instalador...";
            try {
                await Task.Run(() => { SaveStorage.EnsureDirectory(); Extract(root); });
                Shortcut(root, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Tekken 5 Recomp.lnk"));
                Process.Start(new ProcessStartInfo(Path.Combine(root,"Tekken5Launcher.exe")) { WorkingDirectory=root });
                working=false; form.Close();
            } catch (Exception ex) {
                label.Text=ex.Message;
                button.Enabled=true;
            } finally { working=false; form.FormClosing -= prevent; }
        };
        Application.Run(form);
        return 0;
    }
    static void Extract(string target) {
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Ja existe uma instalacao em "+target+".");
        string stage=Path.Combine(Path.GetDirectoryName(target), "tk5-" + Guid.NewGuid().ToString("N").Substring(0, 12));
        try {
            using (var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            using (var archive=new ZipArchive(payload,ZipArchiveMode.Read))
                archive.ExtractToDirectory(stage);
            Directory.Move(stage,target);
        } finally {
            if (Directory.Exists(stage)) Directory.Delete(stage,true);
        }
    }
    static void Shortcut(string root,string path) {
        dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        dynamic link=shell.CreateShortcut(path);
        try {
            link.TargetPath=Path.Combine(root,"Tekken5Launcher.exe");
            link.WorkingDirectory=root;
            link.IconLocation=Path.Combine(root,"Tekken5Launcher.exe")+",0";
            link.Save();
        } finally {
            Marshal.FinalReleaseComObject(link);
            Marshal.FinalReleaseComObject(shell);
        }
    }
}

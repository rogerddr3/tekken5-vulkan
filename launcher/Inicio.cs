using System;
using System.IO;
using System.Windows;
using System.Threading;
namespace Tekken5Launcher { public sealed partial class Launcher {
        [STAThread]
        public static void Main(string[] args) {
            string folder=AppDomain.CurrentDomain.BaseDirectory;
            int parent;
            if (args.Length==2 && (args[0]=="--aplicar" || args[0]=="--recuperar") && int.TryParse(args[1],out parent) && parent>0) {
                try { InstallUpdate.Helper(parent,args[0]=="--recuperar"); }
                catch (Exception ex) { MessageBox.Show(ex.Message,"Tekken 5 Vulkan"); }
                return;
            }
            using (var mutex=new Mutex(false, MutexName(folder))) {
                bool acquired;
                try { acquired=mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired=true; }
                if (!acquired) { MessageBox.Show("O launcher já está aberto."); return; }
                try {
                    if (InstallUpdate.Pending(folder)) {
                        InstallUpdate.Handoff(InstallUpdate.Stage(folder),true);
                        return;
                    }
                    InstallUpdate.Collect(folder);
                    var app=new Application();
                    app.Run(new Launcher(folder).window);
                } catch (Exception ex) { MessageBox.Show(ex.Message,"Tekken 5 Vulkan"); }
                finally { mutex.ReleaseMutex(); }
            }
        }
        internal static string MutexName(string folder) {
            folder=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            using (var sha=System.Security.Cryptography.SHA256.Create())
                return "Local\\Tekken5VulkanLauncher-"+BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(folder.ToUpperInvariant()))).Replace("-","");
        }
    }
}

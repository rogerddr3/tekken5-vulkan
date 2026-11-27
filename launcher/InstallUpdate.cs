using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Xml.Linq;

namespace Tekken5Launcher {
    internal static class InstallUpdate {
        const string Area=".tekken-update";
        internal const string LauncherFile="Tekken5Launcher.exe";
        static readonly string[] Files={LauncherFile,"Compilar-Tekken5.ps1","preparar-ferramentas.py","entradas-tekken5.txt","Preparar-Projeto.ps1"};
        internal static string Stage(string root) {
            string path=Path.Combine(root,Area,Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
        internal static void Clean(string work) {
            try { Directory.Delete(work,true); }
            catch (IOException) {} catch (UnauthorizedAccessException) {}
        }
        internal static void Prepare(string work,string archive,Version version) {
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string next=Path.Combine(work,"new");
            Directory.CreateDirectory(next);
            using (var input=File.OpenRead(archive))
            using (var zip=new ZipArchive(input,ZipArchiveMode.Read)) {
                long total=0;
                foreach (var entry in zip.Entries) {
                    string name=entry.FullName;
                    if (!Files.Contains(name) || !seen.Add(name) || entry.Length<=0 || entry.Length>Releases.Limit-total)
                        throw new InvalidDataException("Arquivo inesperado, duplicado ou grande demais no pacote: "+name);
                    total+=entry.Length;
                    string dest=Path.Combine(next,Path.GetFileName(name));
                    using (var src=entry.Open())
                    using (var file=new FileStream(dest,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                        byte[] buf=new byte[81920]; int read;
                        while ((read=src.Read(buf,0,buf.Length))>0) {
                            if (file.Length+read>entry.Length) throw new InvalidDataException("Tamanho inválido: "+name);
                            file.Write(buf,0,read);
                        }
                        if (file.Length!=entry.Length) throw new InvalidDataException("Pacote incompleto: "+name);
                    }
                }
            }
            if (!Files.All(seen.Contains)) throw new InvalidDataException("Faltam os executáveis da edição Windows.");
            var assembly=AssemblyName.GetAssemblyName(Path.Combine(next,LauncherFile));
            if (assembly.Name!="Tekken5Launcher" || assembly.Version.ToString(3)!=version.ToString(3))
                throw new InvalidDataException("A versão do launcher não corresponde à release.");
            Directory.CreateDirectory(Path.Combine(work,"old"));
        }
        static bool Id(string name) { return Regex.IsMatch(name??"", @"^[a-f0-9]{32}\z"); }
        internal static void Apply(string root,string work) {
            string area=Path.Combine(root,Area), marker=Path.Combine(area,"pending.xml");
            string id=Path.GetFileName(work);
            if (!Id(id) || !String.Equals(Path.Combine(area,id),work,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Pasta de atualização inválida.");
            var files=Files.Where(n=>File.Exists(Path.Combine(work,"new",n))).ToArray();
            if (files.Length != Files.Length) throw new InvalidDataException("Atualização incompleta.");
            var plan=new XElement("update",new XAttribute("id",id));
            foreach (string name in files) plan.Add(new XElement("file",new XAttribute("name",name),
                new XAttribute("existed",File.Exists(Path.Combine(root,name)))));
            string staging=Path.Combine(work,"journal.tmp");
            using (var file=File.Create(staging)) { plan.Save(file); file.Flush(true); }
            File.Move(staging,marker);
            try {
                foreach (var item in plan.Elements("file")) {
                    string name=(string)item.Attribute("name");
                    string src=Path.Combine(work,"new",name), dest=Path.Combine(root,name);
                    if ((bool)item.Attribute("existed")) File.Replace(src,dest,Path.Combine(work,"old",name));
                    else File.Move(src,dest);
                }
                File.Delete(marker); // Commit only after every file is in place.
            } catch { Recover(root); throw; }
        }
        internal static void Recover(string root) {
            string area=Path.Combine(root,Area),marker=Path.Combine(area,"pending.xml");
            if (!File.Exists(marker)) return;
            var plan=XElement.Load(marker);
            string id=(string)plan.Attribute("id");
            if (!Id(id)) throw new InvalidDataException("Registro de atualização inválido.");
            string work=Path.Combine(area,id);
            foreach (var item in plan.Elements("file").Reverse()) {
                string name=(string)item.Attribute("name");
                if (!Files.Contains(name)) throw new InvalidDataException("Arquivo inválido no registro de atualização.");
                string backup=Path.Combine(work,"old",name),dest=Path.Combine(root,name);
                if (File.Exists(backup)) {
                    if (File.Exists(dest)) File.Replace(backup,dest,null); else File.Move(backup,dest);
                } else if (!(bool)item.Attribute("existed") && !File.Exists(Path.Combine(work,"new",name))) File.Delete(dest);
            }
            File.Delete(marker);
        }
        internal static void Collect(string root) {
            string area=Path.Combine(root,Area);
            if (!Directory.Exists(area) || File.Exists(Path.Combine(area,"pending.xml"))) return;
            foreach (string dir in Directory.GetDirectories(area)) {
                if (Id(Path.GetFileName(dir)) && (File.Exists(Path.Combine(dir,"finished")) ||
                    Directory.GetLastWriteTimeUtc(dir)<DateTime.UtcNow.AddDays(-1))) Clean(dir);
            }
        }
        internal static bool Pending(string root) { return File.Exists(Path.Combine(root,Area,"pending.xml")); }
        internal static void Handoff(string work,bool recoverOnly=false) {
            string helper=Path.Combine(work,"Atualizador.exe");
            File.Copy(Assembly.GetExecutingAssembly().Location,helper);
            using (Process.Start(new ProcessStartInfo(helper,(recoverOnly?"--recuperar ":"--aplicar ")+Process.GetCurrentProcess().Id) {UseShellExecute=false,WorkingDirectory=work})) {}
        }
        internal static void Helper(int parentId,bool recoverOnly) {
            var work=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            if (!Id(work.Name) || work.Parent==null || work.Parent.Name!=Area) throw new InvalidDataException("Local do atualizador inválido.");
            string root=work.Parent.Parent.FullName;
            try {
                Process parent=null;
                try { parent=Process.GetProcessById(parentId); } catch (ArgumentException) {}
                using (parent) { if (parent!=null && !parent.WaitForExit(30000)) throw new IOException("Feche o launcher e tente novamente."); }
                using (var mutex=new Mutex(false,Launcher.MutexName(root))) {
                    bool acquired;
                    try { acquired=mutex.WaitOne(15000); } catch (AbandonedMutexException) { acquired=true; }
                    if (!acquired) throw new IOException("Outra instância do launcher está em execução.");
                    try { Recover(root); if (!recoverOnly) Apply(root,work.FullName); }
                    finally { mutex.ReleaseMutex(); }
                }
            } catch (Exception ex) {
                MessageBox.Show("A atualização não foi concluída. "+ex.Message,"Tekken 5 Vulkan",MessageBoxButton.OK,MessageBoxImage.Warning);
                if (recoverOnly || Pending(root)) return;
            }
            if (!File.Exists(Path.Combine(root,Area,"pending.xml"))) File.WriteAllText(Path.Combine(work.FullName,"finished"),"");
            Process.Start(new ProcessStartInfo(Path.Combine(root,LauncherFile)) {UseShellExecute=false,WorkingDirectory=root});
        }
    }
}

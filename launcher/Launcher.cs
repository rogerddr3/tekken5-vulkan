using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Tekken5Launcher {
    public sealed partial class Launcher {
        readonly Window window;
        readonly string root, engine, gameFolder, runner, boot, profile;
        string iso, patch, verifiedPath;
        DateTime verifiedDate;
        bool busy;
        Process game;
        static readonly int[] scales = {1,2,3,4,6};
        const string DiscHash = "AEC7958A90D603DAEC0BF850F2F0EB19029CF11F";

        T Find<T>(string name) where T : FrameworkElement { return (T)window.FindName(name); }
        void Bind(string name, Action action) {
            Find<Button>(name).Click += (s,e) => { try { action(); } catch (Exception ex) { Error(ex); } };
        }
        void Error(Exception ex) { MessageBox.Show(window,ex.Message,"Tekken 5 Vulkan",MessageBoxButton.OK,MessageBoxImage.Warning); }
        void Status(string message) { Find<TextBlock>("Status").Text = message; }
        static string Quote(string path) { return "\"" + path + "\""; }
        bool Built { get { return File.Exists(runner) && File.Exists(boot); } }
        string PreparationMarker { get { return Path.Combine(Path.GetDirectoryName(runner),"preparo-v3.ok"); } }

        public Launcher(string directory) {
            root = directory;
            engine = Path.Combine(root,"ps2-native-recomp");
            gameFolder = Path.Combine(engine,"games","tekken5");
            runner = Path.Combine(engine,"build","native-tekken5","ps2run.exe");
            boot = Path.Combine(root,"dados-locais","files","SLUS_210.59");
            profile = Path.Combine(root,"tekken5-perfil.xml");
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Janela.xaml"))
                window = (Window)XamlReader.Load(s);
            XElement config = new XElement("perfil");
            if (File.Exists(profile)) config = XElement.Load(profile);
            iso = (string)config.Element("iso") ?? Path.GetFullPath(Path.Combine(root,"..","SLUS-21059 (1.00).iso"));
            patch = (string)config.Element("patch") ?? "";
            var display = Find<ComboBox>("Display");
            display.ItemsSource = new[]{"Janela","Tela cheia"};
            display.SelectedIndex = (string)config.Element("tela")=="1" ? 1 : 0;
            var aspect = Find<ComboBox>("Aspect");
            aspect.ItemsSource = new[]{"16:9 · Panorâmico","4:3 · Original"};
            aspect.SelectedIndex = (string)config.Element("proporcao")=="4:3" ? 1 : 0;
            var scale = Find<ComboBox>("Scale");
            scale.ItemsSource = new[]{"1× · 640 × 448 (original)", "2× · 1280 × 896",
                "3× · 1920 × 1344 (largura Full HD)", "4× · 2560 × 1792 (largura Quad HD)",
                "6× · 3840 × 2688 (largura 4K UHD, experimental)"};
            int n;
            scale.SelectedIndex = int.TryParse((string)config.Element("escala"),out n) ? Math.Max(0,Array.IndexOf(scales,n)) : 0;
            var camera = Find<CheckBox>("WideCamera");
            camera.IsChecked = (string)config.Element("cameraAmpla")!="0";
            camera.Checked += (s,e)=>Persist();
            camera.Unchecked += (s,e)=>Persist();
            var vsync = Find<CheckBox>("VSync");
            vsync.IsChecked = (string)config.Element("vsync")!="0";
            vsync.Checked += (s,e)=>Persist();
            vsync.Unchecked += (s,e)=>Persist();
            display.SelectionChanged += (s,e)=>Persist();
            aspect.SelectionChanged += (s,e)=>{ Persist(); Controls(); };
            scale.SelectionChanged += (s,e)=>Persist();
            foreach (string name in new[]{"Inicio","Graficos","Mods","Opcoes"}) Bind(name,()=>Page(name));
            Bind("Choose",()=>ChooseDisc());
            Bind("ChoosePatch",ChoosePatch);
            Bind("DisablePatch",()=>{ patch=""; Persist(); Controls(); });
            Find<Button>("Play").Click += async (s,e)=>await Play();
            window.Closing += (s,e)=>{ if (busy) { e.Cancel=true; Status("Aguarde a operação em andamento."); } };
            Page("Inicio");
            Controls();
            Status(Built ? "Pronto para jogar" : "Selecione o disco para preparar a primeira compilação.");
            InitUpdates();
        }
        void Page(string name) {
            var tabs=new[]{"Inicio","Graficos","Mods","Opcoes"};
            var pages=new[]{"Home","VideoPage","Library","Settings"};
            for(int i=0;i<tabs.Length;i++) {
                bool active=tabs[i]==name;
                Find<FrameworkElement>(pages[i]).Visibility=active?Visibility.Visible:Visibility.Collapsed;
                Find<Button>(tabs[i]).Background=(Brush)new BrushConverter().ConvertFrom(active?"#51634C":"#304339");
            }
        }
        void Persist() {
            var temp=profile+".tmp";
            try {
                new XElement("perfil",new XElement("iso",iso),new XElement("patch",patch),
                    new XElement("tela",Find<ComboBox>("Display").SelectedIndex),
                    new XElement("vsync",Find<CheckBox>("VSync").IsChecked==true?"1":"0"),
                    new XElement("cameraAmpla",Find<CheckBox>("WideCamera").IsChecked==true?"1":"0"),
                    new XElement("proporcao",Find<ComboBox>("Aspect").SelectedIndex==0?"16:9":"4:3"),
                    new XElement("escala",scales[Find<ComboBox>("Scale").SelectedIndex])).Save(temp);
                if(File.Exists(profile)) File.Replace(temp,profile,null); else File.Move(temp,profile);
            } catch(Exception ex) { Error(ex); }
        }
        void Controls() {
            foreach(string id in new[]{"Play","Choose","ChoosePatch","Display","Aspect","Scale","VSync","ReleaseButton"})
                Find<FrameworkElement>(id).IsEnabled=!busy && game==null;
            Find<CheckBox>("WideCamera").IsEnabled=!busy && game==null && Find<ComboBox>("Aspect").SelectedIndex==0;
            Find<Button>("DisablePatch").IsEnabled=!busy && game==null && patch.Length>0;
            Find<Button>("Play").Content=Built?"JOGAR":"PREPARAR JOGO";
            Find<TextBlock>("HomePack").Text="Windows x64   ·   Vulkan";
            Find<TextBlock>("Disc").Text=File.Exists(iso)?iso:"Escolha sua imagem ISO.";
            Find<TextBlock>("PatchName").Text=patch.Length==0?"Nenhum patch ativo":patch;
        }
        bool ChooseDisc() {
            var dialog=new OpenFileDialog {Filter="Imagem de disco (*.iso)|*.iso",Title="Selecionar Tekken 5 USA"};
            if(dialog.ShowDialog(window)!=true) return false;
            iso=dialog.FileName; verifiedPath=null; Persist(); Controls(); return true;
        }
        static void ValidatePatch(string path) {
            if(!File.Exists(path) || new FileInfo(path).Length>1024*1024)
                throw new IOException("Escolha um patch de dados de até 1 MB.");
            int count=0;
            foreach(string line in File.ReadLines(path)) {
                if(String.IsNullOrWhiteSpace(line)) continue;
                var m=Regex.Match(line.Trim(),@"^([01])\s+([0-9a-fA-F]{1,8})\s+([1248])\s+([0-9a-fA-F]{1,16})$");
                if(!m.Success || Convert.ToUInt32(m.Groups[2].Value,16)>(0x02000000-int.Parse(m.Groups[3].Value))
                    || m.Groups[4].Value.TrimStart('0').Length>2*int.Parse(m.Groups[3].Value))
                    throw new InvalidDataException("O arquivo precisa usar o formato patches.txt do runtime.");
                count++;
            }
            if(count==0) throw new InvalidDataException("O patch está vazio.");
        }
        void ChoosePatch() {
            var d=new OpenFileDialog {Filter="Patch de dados (*.txt)|*.txt"};
            if(d.ShowDialog(window)!=true) return;
            ValidatePatch(d.FileName); patch=d.FileName; Persist(); Controls();
        }
        async Task VerifyDisc() {
            var source=new FileInfo(iso);
            if(verifiedPath==iso && verifiedDate==source.LastWriteTimeUtc) return;
            if(source.Length!=4483547136L) throw new InvalidDataException("Esta versão exige o disco americano SLUS-21059 v1.00.");
            Status("Verificando o disco…");
            var hash=await Task.Run(()=>{ using(var f=File.OpenRead(source.FullName)) using(var sha=SHA1.Create())
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-",""); });
            if(hash!=DiscHash) throw new InvalidDataException("O conteúdo da ISO difere do disco americano SLUS-21059 v1.00.");
            verifiedPath=iso; verifiedDate=source.LastWriteTimeUtc;
        }
        bool NeedsPreparation() {
            string marker=PreparationMarker;
            if (!File.Exists(marker)) return true;
            var known=new System.Collections.Generic.HashSet<string>(File.ReadAllLines(marker),StringComparer.OrdinalIgnoreCase);
            using(var sha=SHA256.Create()) {
                foreach(string unit in new[]{"vu0","vu1"}) {
                    string dir=Path.Combine(gameFolder,unit);
                    if (!Directory.Exists(dir)) continue;
                    foreach(string file in Directory.GetFiles(dir,"*.bin")) {
                        using(var input=File.OpenRead(file)) {
                            string entry=unit+"/"+Path.GetFileName(file)+" "+BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");
                            if (!known.Remove(entry)) return true;
                        }
                    }
                }
            }
            return known.Count!=0;
        }
        void Build(bool optimize) {
            var script=Path.Combine(root,optimize?"Compilar-Tekken5.ps1":"Preparar-Projeto.ps1");
            if(!File.Exists(script)) throw new FileNotFoundException("Extraia todos os arquivos do pacote antes de preparar o jogo.",script);
            var info=new ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(script)+" -Iso "+Quote(iso)+(optimize?" -Otimizar":""))
                {WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var log=new StreamWriter(Path.Combine(root,"compilacao.log"),false))
            using(var process=new Process {StartInfo=info}) {
                DataReceivedEventHandler output=(s,e)=>{ if(e.Data!=null) lock(log) {log.WriteLine(e.Data); log.Flush();} };
                process.OutputDataReceived+=output; process.ErrorDataReceived+=output;
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); process.WaitForExit();
                if(process.ExitCode!=0 || !Built) throw new IOException("Falha ao preparar o jogo. Consulte compilacao.log na pasta do launcher.");
            }
        }
        async Task Play() {
            if(busy || game!=null) return;
            if(!File.Exists(iso) && !ChooseDisc()) return;
            busy=true; Controls();
            try {
                await VerifyDisc();
                int scale=scales[Find<ComboBox>("Scale").SelectedIndex];
                if(!Built) {
                    Status("Compilando o jogo para Windows. Acompanhe em compilacao.log.");
                    await Task.Run(()=>Build(false));
                } else if(await Task.Run((Func<bool>)NeedsPreparation)) {
                    Status("Preparando o renderer e os programas de luta antes de jogar. Detalhes em compilacao.log.");
                    try { await Task.Run(()=>Build(true)); }
                    catch(Exception ex) {
                        if(!Built || !File.Exists(PreparationMarker)) throw;
                        if(MessageBox.Show(window,ex.Message+" Abrir o jogo com a compilação existente?",
                            "Tekken 5 Vulkan",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) throw;
                    }
                }
                if(patch.Length>0) ValidatePatch(patch);
                foreach(var folder in new[]{"vu0","vu1"}) Directory.CreateDirectory(Path.Combine(gameFolder,folder));
                bool wide=Find<ComboBox>("Aspect").SelectedIndex==0;
                // The SLUS-21059 screen setting is the high byte of the options word.
                // Keep the other three option bytes and the user's separate mod intact.
                string aspectPatch=Path.Combine(gameFolder,wide?"launcher-16x9.txt":"launcher-4x3.txt");
                File.WriteAllText(aspectPatch,wide?"1 003E453B 1 01\n":"1 003E453B 1 00\n",System.Text.Encoding.ASCII);
                string args=Quote(boot)+" --disc "+Quote(iso)+" --game-dir "+Quote(gameFolder)+" --memcard "+Quote(Path.Combine(gameFolder,"tekken5.ps2"))
                    +(Find<ComboBox>("Display").SelectedIndex==1?" --fullscreen":" --window")
                    +(wide?" --widescreen":"")+(patch.Length==0?"":" --patches "+Quote(patch))+" --patches "+Quote(aspectPatch);
                var info=new ProcessStartInfo(runner,args){UseShellExecute=false,WorkingDirectory=root};
                info.EnvironmentVariables["TK5_WIDE_CAMERA"]=wide && Find<CheckBox>("WideCamera").IsChecked==true?"1":"0";
                info.EnvironmentVariables["PS2_GPU_BACKEND"]="vulkan";
                info.EnvironmentVariables["PS2_PRESENT_MODE"]=Find<CheckBox>("VSync").IsChecked==true?"fifo":"immediate";
                info.EnvironmentVariables["PS2_GPU_HW"]=scale==1?"1":"0";
                info.EnvironmentVariables["PS2_SCALE"]=scale.ToString();
                info.EnvironmentVariables["PS2_WINDOW_TITLE"]="Tekken 5 - Vulkan";
                info.EnvironmentVariables["PS2_VU_CXX"]=Path.Combine(engine,"tools","toolchain","llvm-mingw-20260908-ucrt-x86_64","bin","clang++.exe");
                game=Process.Start(info);
                game.Exited+=(s,e)=>window.Dispatcher.BeginInvoke(new Action(()=>{
                    int code=game.ExitCode; game.Dispose(); game=null; Controls(); window.WindowState=WindowState.Normal;
                    Status(code==0?"Pronto para jogar":"O jogo encerrou com erro. Consulte games/tekken5/logs.");
                }));
                game.EnableRaisingEvents=true;
                window.WindowState=WindowState.Minimized; Status("Jogo em execução");
            } catch(Exception ex) { Status("Não foi possível iniciar o jogo."); Error(ex); }
            finally { busy=false; Controls(); }
        }
    }
}

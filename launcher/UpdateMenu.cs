using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Tekken5Launcher {
    public sealed partial class Launcher {
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        List<Release> releases=new List<Release>();
        CancellationTokenSource download;
        bool checking, closed;
        void InitUpdates() {
            Bind("ReleaseButton",()=>Find<Popup>("ReleasePopup").IsOpen=true);
            Bind("CancelUpdate",()=>{ if (download!=null) download.Cancel(); });
            Find<Button>("ReleaseReload").Click+=async (s,e)=>await CheckReleases();
            Find<Popup>("ReleasePopup").PreviewKeyDown+=(s,e)=>{
                if (e.Key==System.Windows.Input.Key.Escape) Find<Popup>("ReleasePopup").IsOpen=false;
            };
            window.ContentRendered+=async (s,e)=>await CheckReleases();
            window.Closed+=(s,e)=>{ closed=true; lifetime.Cancel(); };
            ShowReleases();
        }
        void ShowReleases() {
            var versions=releases.Select(r=>r.Version).Concat(new[]{Releases.Installed}).Distinct().OrderByDescending(v=>v);
            var list=Find<StackPanel>("ReleaseList");
            list.Children.Clear();
            foreach (var v in versions) {
                bool installed=v.Equals(Releases.Installed);
                var item=new Button { Content=installed?"✓  "+v+"   ·   Instalada":v.ToString(),
                    HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(10,7,10,7),
                    Margin=new Thickness(0,0,0,3),IsEnabled=!installed };
                var release=releases.FirstOrDefault(r=>r.Version==v);
                if (!installed && release!=null) item.Click+=async (s,e)=>await InstallRelease(release);
                list.Children.Add(item);
            }
            var newer=releases.FirstOrDefault(r=>r.Version>Releases.Installed);
            var button=Find<Button>("ReleaseButton");
            button.Content="Lançamento · "+Releases.Installed+"  ▾";
            if (newer!=null) {
                button.Background=(Brush)new BrushConverter().ConvertFrom("#916E39");
                button.Content="Atualizar · "+newer.Version+"  ▾";
            } else button.Background=(Brush)new BrushConverter().ConvertFrom("#304339");
            button.ToolTip="Versão instalada: "+Releases.Installed;
        }
        async Task CheckReleases() {
            if (checking || busy || closed) return;
            var note=Find<TextBlock>("ReleaseNote");
            checking=true;
            Find<Button>("ReleaseReload").IsEnabled=false;
            note.Text="Consultando o GitHub…";
            try {
                var result=await Releases.List(lifetime.Token);
                if (closed) return;
                releases=result;
                ShowReleases();
                note.Text=releases.Count==0?"Nenhuma release compatível publicada.":releases[0].Version>Releases.Installed?
                    "Selecione uma versão para instalar.":"Você está na versão mais recente disponível.";
            } catch (Exception) {
                if (!closed) note.Text="Não foi possível consultar as releases. Tente novamente.";
            } finally { checking=false; if (!closed) Find<Button>("ReleaseReload").IsEnabled=true; }
        }
        async Task InstallRelease(Release release) {
            if (busy || game!=null || closed) return;
            Find<Popup>("ReleasePopup").IsOpen=false;
            string action=release.Version<Releases.Installed?"Voltar para a versão ":"Instalar a versão ";
            if (MessageBox.Show(window,action+release.Version+"? O launcher será reiniciado após o download.",
                    "Tekken 5 Vulkan",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
            var status=Find<TextBlock>("Status");
            var cancel=Find<Button>("CancelUpdate");
            string work=null;
            busy=true; Controls();
            download=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            try {
                work=InstallUpdate.Stage(root);
                string archive=Path.Combine(work,Releases.AssetName);
                cancel.Visibility=Visibility.Visible;
                status.Text="Baixando versão "+release.Version+"…";
                var progress=new Progress<int>(percent=>{if (download!=null && cancel.IsVisible) status.Text="Baixando versão "+release.Version+" · "+percent+"%";});
                await Releases.Download(release,archive,progress,download.Token);
                download.Token.ThrowIfCancellationRequested();
                cancel.Visibility=Visibility.Collapsed;
                status.Text="Preparando a instalação…";
                await Task.Run(()=>InstallUpdate.Prepare(work,archive,release.Version));
                InstallUpdate.Handoff(work);
                work=null;
                busy=false;
                window.Close();
            } catch (Exception ex) {
                if (download.IsCancellationRequested) status.Text="Download cancelado.";
                else {
                    status.Text="Falha na atualização. Você pode tentar novamente.";
                    Error(ex);
                }
            } finally {
                if (work!=null) InstallUpdate.Clean(work);
                download.Dispose(); download=null;
                if (!closed) { cancel.Visibility=Visibility.Collapsed; busy=false; Controls(); }
            }
        }
    }
}

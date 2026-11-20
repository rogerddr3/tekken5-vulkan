using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Tekken5Launcher {
    public sealed class Release {
        public Version Version;
        public string Tag, Digest;
        public long Size;
    }

    public static class Releases {
        public const string Repository = "rogerddr3/tekken5-vulkan";
        public const string AssetName = "Tekken5Vulkan-Windows-x64.zip";
        internal const long Limit = 1024L * 1024 * 1024;
        public static Version Installed {
            get { return new Version(Assembly.GetExecutingAssembly().GetName().Version.ToString(3)); }
        }
        public sealed class AssetData {
            public string name, digest, state;
            public long size;
        }
        public sealed class ReleaseData {
            public string tag_name;
            public bool draft, prerelease;
            public AssetData[] assets;
        }
        internal static List<Release> Parse(string json, out int count) {
            var data = new JavaScriptSerializer { MaxJsonLength = 8*1024*1024 }.Deserialize<ReleaseData[]>(json);
            if (data == null) throw new InvalidDataException("O GitHub retornou uma lista inválida.");
            count = data.Length;
            var found = new List<Release>();
            foreach (var item in data) {
                if (item == null || item.draft || item.prerelease || item.tag_name == null ||
                    !Regex.IsMatch(item.tag_name, @"^v(?:0|[1-9][0-9]{0,4})(?:\.(?:0|[1-9][0-9]{0,4})){2}\z")) continue;
                Version version;
                if (!Version.TryParse(item.tag_name.Substring(1),out version)) continue;
                var asset = (item.assets ?? new AssetData[0]).FirstOrDefault(a => a != null && a.name == AssetName && a.state == "uploaded");
                if (asset == null || asset.size <= 0 || asset.size > Limit ||
                    !Regex.IsMatch(asset.digest ?? "", @"^sha256:[a-fA-F0-9]{64}\z")) continue;
                found.Add(new Release {Version=version, Tag=item.tag_name, Size=asset.size, Digest=asset.digest.Substring(7)});
            }
            return found;
        }
        static HttpClient Client() {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient(new HttpClientHandler {AllowAutoRedirect=false}) {Timeout=Timeout.InfiniteTimeSpan,MaxResponseContentBufferSize=8*1024*1024};
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Tekken5Vulkan-Launcher/"+Installed);
            return client;
        }
        public static async Task<List<Release>> List(CancellationToken cancel) {
            var result = new List<Release>();
            using (var client = Client())
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel)) {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                for (int page=1;;page++) {
                    string url = "https://api.github.com/repos/"+Repository+"/releases?per_page=100&page="+page;
                    using (var response = await client.GetAsync(url,timeout.Token).ConfigureAwait(false)) {
                        response.EnsureSuccessStatusCode();
                        int count;
                        result.AddRange(Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false),out count));
                        if (count<100) break;
                    }
                }
            }
            return result.OrderByDescending(r=>r.Version).ToList();
        }
        static bool AssetHost(Uri uri) {
            return uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo == "" &&
                (uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com" ||
                 uri.Host == "objects.githubusercontent.com" || uri.Host == "github-releases.githubusercontent.com");
        }
        public static async Task Download(Release release,string path,IProgress<int> progress,CancellationToken cancel) {
            using (var client=Client())
                await Download(client,release,path,progress,cancel).ConfigureAwait(false);
        }
        internal static async Task Download(HttpClient client,Release release,string path,IProgress<int> progress,CancellationToken cancel) {
            var uri = new Uri("https://github.com/"+Repository+"/releases/download/"+release.Tag+"/"+AssetName);
            using (var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel)) {
                timeout.CancelAfter(TimeSpan.FromMinutes(15));
                for (int redirects=0; redirects<=5 && AssetHost(uri); redirects++) {
                    using (var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false)) {
                        if ((int)response.StatusCode>=300 && (int)response.StatusCode<400 && response.Headers.Location!=null) {
                            uri=new Uri(uri,response.Headers.Location); continue;
                        }
                        response.EnsureSuccessStatusCode();
                        using (var src=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var stop=timeout.Token.Register(src.Dispose))
                        using (var dest=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))
                        using (var hash=SHA256.Create()) {
                            byte[] buffer=new byte[81920];
                            long total=0;
                            int read,last=-1;
                            while ((read=await src.ReadAsync(buffer,0,buffer.Length,timeout.Token).ConfigureAwait(false))>0) {
                                total+=read;
                                if (total>release.Size) throw new InvalidDataException("O download excedeu o tamanho publicado.");
                                hash.TransformBlock(buffer,0,read,buffer,0);
                                await dest.WriteAsync(buffer,0,read,timeout.Token).ConfigureAwait(false);
                                int percent=(int)(total*100/release.Size);
                                if (percent!=last) { progress.Report(percent); last=percent; }
                            }
                            hash.TransformFinalBlock(new byte[0],0,0);
                            if (total!=release.Size || !String.Equals(BitConverter.ToString(hash.Hash).Replace("-",""),release.Digest,StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("O pacote recebido não corresponde ao SHA-256 publicado no GitHub.");
                        }
                        return;
                    }
                }
                throw new InvalidDataException("O endereço de download retornado pelo GitHub é inválido.");
            }
        }
    }
}

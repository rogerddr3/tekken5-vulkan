using System; using System.IO;
namespace Tekken5Launcher {
 public static class SaveStorage {
  public static string EnsureDirectory() {
   var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tekken5Vulkan-Saves");
   Directory.CreateDirectory(path);
   return path;
  }
  public static string CardPath(string gameFolder) {
   var card = Path.Combine(EnsureDirectory(), "tekken5.ps2");
   var legacy = Path.Combine(gameFolder, "tekken5.ps2");
   if (File.Exists(card) || !File.Exists(legacy)) return card;
   var temporary = Path.Combine(Path.GetDirectoryName(card), Guid.NewGuid().ToString("N") + ".tmp");
   try {
    using (var input = new FileStream(legacy, FileMode.Open, FileAccess.Read, FileShare.Read))
    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
     input.CopyTo(output);
     output.Flush(true);
    }
    try { File.Move(temporary,card); }
    catch (IOException) { if (!File.Exists(card)) throw; }
   } finally { File.Delete(temporary); }
   return card;
  }
 }
}

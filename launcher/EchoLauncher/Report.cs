// Echo Launcher -- "Report a problem": a zip on the desktop with the launcher's log and settings, the game's
// eg_*.txt mod logs, mods\mode.txt and the backend lines of config.json. Nothing in those files is a credential:
// the only key on this path is nakama's public default socket key.
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace EchoLauncher
{
    public static class Report
    {
        public static string Make(Game game, Status status)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var path = Path.Combine(desktop, "EchoLauncher-report-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip");
            if (File.Exists(path)) File.Delete(path);
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                AddIf(zip, Log.Path, "launcher.log");
                AddIf(zip, Settings.FilePath, "settings.json");
                var about = new StringBuilder();
                about.Append("Echo Launcher ").Append(Program.Version).Append('\n');
                about.Append("Windows ").Append(Environment.OSVersion.VersionString).Append(", 64-bit OS: ").Append(Environment.Is64BitOperatingSystem).Append('\n');
                about.Append("time ").Append(DateTime.Now.ToString("o")).Append('\n');
                if (game != null)
                {
                    about.Append("game ").Append(game.Exe).Append('\n');
                    about.Append("config points at ").Append(game.CurrentTarget()).Append('\n');
                    about.Append("mod loader installed ").Append(game.ForwarderInstalled).Append('\n');
                    foreach (var f in new[] { "eg_loader.txt", "eg_debug.txt", "es_sync.txt", "eg_gate.txt", "eg_script.txt", "eg_control_log.txt" })
                        AddIf(zip, Path.Combine(game.BinDir, f), "game/" + f);
                    AddIf(zip, game.ModeFile, "game/mods/mode.txt");
                    AddIf(zip, game.ConfigPath, "game/_local/config.json");
                    try
                    {
                        if (Directory.Exists(game.ModsDir))
                        {
                            var list = new StringBuilder();
                            foreach (var p in Directory.GetFiles(game.ModsDir))
                                list.Append(Path.GetFileName(p)).Append('\t').Append(new FileInfo(p).Length).Append('\t').Append(Net.Sha256File(p)).Append('\n');
                            AddText(zip, "game/mods/listing.txt", list.ToString());
                        }
                    }
                    catch { }
                }
                if (status != null)
                {
                    about.Append("servers seen: ");
                    foreach (var s in status.Servers) about.Append(s.Name).Append(s.Up ? " up " : " down ").Append(s.Players).Append("p; ");
                    about.Append('\n');
                }
                AddText(zip, "about.txt", about.ToString());
            }
            Log.Write("problem report written: " + path);
            return path;
        }

        static void AddIf(ZipArchive zip, string file, string name)
        {
            try { if (!string.IsNullOrEmpty(file) && File.Exists(file)) zip.CreateEntryFromFile(file, name, CompressionLevel.Optimal); } catch { }
        }

        static void AddText(ZipArchive zip, string name, string text)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(text);
        }
    }
}

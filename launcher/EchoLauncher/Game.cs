// Echo Launcher -- everything that touches the game folder: finding echovr.exe, the backend lines in
// _local\config.json (backed up once, restored by "Back to official"), the client forwarder in bin\win10
// (the original kept as amd_ags_x64_orig.dll), the mods in <game>\mods\ and mods\mode.txt, and starting the game.
// The launcher writes to exactly two places inside the game folder: bin\win10\amd_ags_x64.dll (+ _orig) and
// mods\ (plus the one config file under _local). Manifest.SafeDest enforces that for downloaded files.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EchoLauncher
{
    public class Game
    {
        public const string ServerKey = "defaultkey";  // nakama's stock socket key; public, every client carries it
        public const string BackupName = "config.original-backup.json";
        public const string ForwarderName = "amd_ags_x64.dll";
        public const string OriginalName = "amd_ags_x64_orig.dll";

        public readonly string Exe;
        public Game(string exe) { Exe = exe; }

        public string BinDir { get { return Path.GetDirectoryName(Exe); } }
        public string Root { get { return Path.GetFullPath(Path.Combine(BinDir, "..", "..")); } }
        public string ModsDir { get { return Path.Combine(Root, "mods"); } }
        public string ModeFile { get { return Path.Combine(ModsDir, "mode.txt"); } }
        public string ForwarderPath { get { return Path.Combine(BinDir, ForwarderName); } }
        public string OriginalPath { get { return Path.Combine(BinDir, OriginalName); } }
        public bool ForwarderInstalled { get { return File.Exists(OriginalPath); } }

        public string ConfigPath
        {
            get
            {
                var a = Path.Combine(Root, "_local", "config.json");
                var b = Path.Combine(BinDir, "_local", "config.json");
                if (File.Exists(a)) return a;
                if (File.Exists(b)) return b;
                return a;
            }
        }
        public string BackupPath { get { return Path.Combine(Path.GetDirectoryName(ConfigPath), BackupName); } }

        // ---- finding the game ----

        public static readonly string[] KnownExes =
        {
            @"C:\echo\EchoBinarys\bin\win10\echovr.exe",
            @"C:\EchoVR\ready-at-dawn-echo-arena\bin\win10\echovr.exe",
        };

        public static string FindExe(Settings s)
        {
            var cands = new List<string>();
            if (!string.IsNullOrEmpty(s.GameExe)) cands.Add(s.GameExe);
            cands.Add(Migration.OldSwitcherExe());
            cands.AddRange(KnownExes);
            const string sub = @"Software\Software\ready-at-dawn-echo-arena\bin\win10\echovr.exe";
            foreach (var pf in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") })
            {
                if (string.IsNullOrEmpty(pf)) continue;
                cands.Add(Path.Combine(pf, "Meta Horizon", sub));
                cands.Add(Path.Combine(pf, "Oculus", sub));
            }
            foreach (var c in cands)
                if (!string.IsNullOrEmpty(c) && File.Exists(c) && Path.GetFileName(c).Equals("echovr.exe", StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        public static bool IsRunning() { return Process.GetProcessesByName("echovr").Length > 0; }

        // ---- the backend lines ----

        // What the game points at today: "EchoVRCE (official)", a host, or why unknown.
        public string CurrentTarget()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return "no config yet";
                var o = Json.Parse(File.ReadAllText(ConfigPath, Encoding.UTF8));
                var h = Json.Str(o, "serverdb_host");
                if (h.IndexOf("echovrce.com", StringComparison.OrdinalIgnoreCase) >= 0) return "EchoVRCE (official)";
                var i = h.IndexOf("://", StringComparison.Ordinal);
                if (i < 0) return "unknown";
                var rest = h.Substring(i + 3);
                var end = rest.IndexOfAny(new[] { '/', '?' });
                return end < 0 ? rest : rest.Substring(0, end);
            }
            catch { return "unreadable config"; }
        }

        public void WriteServerConfig(string hostPort)
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            Directory.CreateDirectory(dir);
            if (File.Exists(ConfigPath) && !File.Exists(BackupPath))
            {
                File.Copy(ConfigPath, BackupPath);
                Log.Write("saved the original config as " + BackupName);
            }
            var ws = "ws://" + hostPort + "/ws?format=evr&token=" + ServerKey;
            var json = new Dictionary<string, object>
            {
                { "apiservice_host", "http://" + hostPort },
                { "configservice_host", ws },
                { "loginservice_host", ws },
                { "matchingservice_host", ws },
                { "serverdb_host", ws },
                { "transactionservice_host", ws },
                { "publisher_lock", "echovrce" },
            };
            var tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, Json.Stringify(json), new UTF8Encoding(false));
            if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
            File.Move(tmp, ConfigPath);
            Log.Write("config.json now points at " + hostPort);
        }

        public bool RestoreOriginalConfig()
        {
            if (!File.Exists(BackupPath)) return false;
            File.Copy(BackupPath, ConfigPath, true);
            Log.Write("config.json restored from " + BackupName);
            return true;
        }

        // ---- the client forwarder ----

        public bool ForwarderCurrent(FileEntry f)
        {
            try { return File.Exists(ForwarderPath) && ForwarderInstalled && Net.Sha256File(ForwarderPath) == f.Sha256; }
            catch { return false; }
        }

        public async Task EnsureForwarder(FileEntry f, Action<string> say, Action<double> progress)
        {
            if (ForwarderCurrent(f)) return;
            if (!File.Exists(ForwarderPath)) throw new FileNotFoundException("bin\\win10\\" + ForwarderName + " is missing from the game folder; the game install looks broken");
            say("Getting the mod loader (" + f.Name + " " + f.Version + ")...");
            var tmp = ForwarderPath + ".download";
            await Net.Download(f.Url, tmp, f.Sha256, f.Size, progress).ConfigureAwait(false);
            if (!ForwarderInstalled)
            {
                File.Move(ForwarderPath, OriginalPath);  // the game's own file, kept under the name the loader forwards to
                Log.Write("kept the game's " + ForwarderName + " as " + OriginalName);
            }
            else
            {
                File.Delete(ForwarderPath);  // an older loader of ours
            }
            File.Move(tmp, ForwarderPath);
            Log.Write("mod loader installed: " + f.Name + " " + f.Version + " (" + f.Sha256.Substring(0, 12) + ")");
        }

        public void RemoveForwarder()
        {
            if (!ForwarderInstalled) return;
            if (File.Exists(ForwarderPath)) File.Delete(ForwarderPath);
            File.Move(OriginalPath, ForwarderPath);
            Log.Write("mod loader removed; the game's own " + ForwarderName + " is back");
        }

        // ---- the mods ----

        public string DestPath(FileEntry f)
        {
            if (!Manifest.SafeDest(f.Dest)) throw new InvalidOperationException("refusing destination " + f.Dest);
            return Path.Combine(Root, f.Dest.Replace('/', Path.DirectorySeparatorChar));
        }

        public async Task EnsureMod(FileEntry f, Action<string> say, Action<double> progress)
        {
            var dest = DestPath(f);
            try { if (File.Exists(dest) && Net.Sha256File(dest) == f.Sha256) return; } catch { }
            say("Getting " + f.Name + " " + f.Version + "...");
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            var tmp = dest + ".download";
            await Net.Download(f.Url, tmp, f.Sha256, f.Size, progress).ConfigureAwait(false);
            if (File.Exists(dest))
            {
                var bak = dest + ".bak";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(dest, bak);
            }
            File.Move(tmp, dest);
            Log.Write("installed " + f.Dest + " " + f.Version + " (" + f.Sha256.Substring(0, 12) + ")");
        }

        public void WriteModeFile(IEnumerable<string> dlls, string modeName)
        {
            Directory.CreateDirectory(ModsDir);
            var sb = new StringBuilder();
            sb.Append("# written by Echo Launcher ").Append(Program.Version).Append(" on ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\n");
            sb.Append("# mode: ").Append(modeName).Append("\n");
            var any = false;
            foreach (var d in dlls) { sb.Append(d).Append("\n"); any = true; }
            if (!any) sb.Append("none\n");
            File.WriteAllText(ModeFile, sb.ToString(), new UTF8Encoding(false));
        }

        public Process Launch()
        {
            var psi = new ProcessStartInfo(Exe) { WorkingDirectory = BinDir, UseShellExecute = true };
            var p = Process.Start(psi);
            Log.Write("started " + Exe + (p != null ? " (pid " + p.Id + ")" : ""));
            return p;
        }
    }
}

// Echo Launcher -- retiring the old "EchoVRCE Switcher" (PowerShell + inject.exe + a hidden watcher re-armed at
// logon). Done once: stop the watcher, remove the Startup entry and inject.exe, keep the folder (its
// config.original-backup.json is the same backup this launcher uses), and take over the remembered game path.
using System;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace EchoLauncher
{
    public static class Migration
    {
        public static string OldDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchoVRCE-Switcher"); }
        }

        public static string OldSwitcherExe()
        {
            try
            {
                var memo = Path.Combine(OldDir, "echo-exe.txt");
                if (!File.Exists(memo)) return null;
                var p = File.ReadAllText(memo).Trim();
                return File.Exists(p) ? p : null;
            }
            catch { return null; }
        }

        public static void Run(Settings s)
        {
            if (s.MigratedOldSwitcher || !Directory.Exists(OldDir)) return;
            Log.Write("old EchoVRCE Switcher found at " + OldDir + ": retiring its watcher");
            try
            {
                // The watcher: powershell.exe running mode-watch.ps1 (any copy of it).
                using (var q = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'powershell.exe'"))
                {
                    foreach (ManagementObject mo in q.Get())
                    {
                        var cl = mo["CommandLine"] as string ?? "";
                        if (cl.IndexOf("mode-watch.ps1", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var pid = Convert.ToInt32(mo["ProcessId"]);
                        try { Process.GetProcessById(pid).Kill(); Log.Write("stopped the old mode watcher (pid " + pid + ")"); } catch { }
                    }
                }
            }
            catch (Exception e) { Log.Write("could not look for the old watcher: " + e.Message); }

            foreach (var f in new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "EchoVRCE-Mode.cmd"),
                Path.Combine(OldDir, "inject.exe"),
                Path.Combine(OldDir, "mode-watch.pid"),
                Path.Combine(OldDir, "mode-watch.stop"),
            })
            {
                try { if (File.Exists(f)) { File.Delete(f); Log.Write("removed " + f); } }
                catch (Exception e) { Log.Write("could not remove " + f + ": " + e.Message); }
            }
            if (string.IsNullOrEmpty(s.GameExe))
            {
                var exe = OldSwitcherExe();
                if (exe != null) { s.GameExe = exe; Log.Write("took the game path from the old switcher: " + exe); }
            }
            s.MigratedOldSwitcher = true;
            s.Save();
        }
    }
}

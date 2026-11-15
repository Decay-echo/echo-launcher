// Echo Launcher -- self-update. latest.json names the newest version; a newer one is downloaded to
// %LOCALAPPDATA%\EchoLauncher\update\EchoLauncher.exe and verified. "Restart to update" starts that copy with
// --apply-update, which waits for this process to exit, copies itself over the installed exe and starts it.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EchoLauncher
{
    public static class Updater
    {
        public static string UpdateDir { get { return Path.Combine(Settings.Dir, "update"); } }
        public static string UpdateExe { get { return Path.Combine(UpdateDir, "EchoLauncher.exe"); } }

        public static bool IsNewer(string latest, string current)
        {
            Version a, b;
            return Version.TryParse(latest, out a) && Version.TryParse(current, out b) && a > b;
        }

        // Fetches a newer launcher if latest.json says there is one; the path of the verified file, or null.
        public static async Task<string> Fetch(Latest latest, Action<string> say, Action<double> progress)
        {
            if (latest == null || !IsNewer(latest.Version, Program.Version)) return null;
            if (!Manifest.SafeUrl(latest.Url) || latest.Sha256.Length != 64) { Log.Write("latest.json refused (url/sha256)"); return null; }
            try
            {
                if (File.Exists(UpdateExe) && Net.Sha256File(UpdateExe) == latest.Sha256) return UpdateExe;
            }
            catch { }
            say("Downloading Echo Launcher " + latest.Version + "...");
            Directory.CreateDirectory(UpdateDir);
            var tmp = UpdateExe + ".download";
            await Net.Download(latest.Url, tmp, latest.Sha256, latest.Size, progress).ConfigureAwait(false);
            if (File.Exists(UpdateExe)) File.Delete(UpdateExe);
            File.Move(tmp, UpdateExe);
            Log.Write("update " + latest.Version + " downloaded and verified: " + UpdateExe);
            return UpdateExe;
        }

        public static void ApplyAndExit(string updateExe)
        {
            var target = Program.ExePath;
            var args = "--apply-update " + Process.GetCurrentProcess().Id + " \"" + target + "\"";
            Log.Write("restarting into the update: " + updateExe + " " + args);
            Process.Start(new ProcessStartInfo(updateExe, args) { UseShellExecute = false });
            Environment.Exit(0);
        }

        // Runs in the NEW exe: args = --apply-update <pid> <target path>.
        public static int RunApply(string[] args)
        {
            try
            {
                var pid = int.Parse(args[1]);
                var target = args[2];
                try { Process.GetProcessById(pid).WaitForExit(30000); } catch { }
                var self = Program.ExePath;
                Exception last = null;
                for (var i = 0; i < 20; i++)
                {
                    try { File.Copy(self, target, true); last = null; break; }
                    catch (Exception e) { last = e; Thread.Sleep(500); }
                }
                if (last != null) throw last;
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) });
                return 0;
            }
            catch (Exception e)
            {
                Log.Write("update could not be applied: " + e.Message);
                System.Windows.Forms.MessageBox.Show("The update could not be applied: " + e.Message + "\n\nThe current version still works; download the new one from the release page.", "Echo Launcher");
                return 1;
            }
        }
    }
}

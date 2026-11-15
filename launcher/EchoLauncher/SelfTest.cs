// Echo Launcher -- headless self-test of everything but the window and the game start:
//   EchoLauncher.exe --selftest <base url> <scratch folder>
// Builds a fake game folder under the scratch folder (a stand-in amd_ags_x64.dll and config.json), then runs
// the real code paths against the manifest and files served at the base url: fetch + parse manifest and status,
// write the backend config (backup), install the loader and a mode's mods (download, sha256), write mode.txt,
// then undo it all and check the originals are back. Results go to <scratch>\selftest-result.txt and the log;
// the exit code is the number of failures. test\make-test-site.ps1 serves a matching site.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EchoLauncher
{
    public static class SelfTest
    {
        public static int Run(string baseUrl, string scratch)
        {
            var lines = new List<string>();
            var fails = 0;
            Action<string, bool> check = (name, ok) => { lines.Add((ok ? "PASS  " : "FAIL  ") + name); if (!ok) fails++; };
            try
            {
                RunAsync(baseUrl.TrimEnd('/'), scratch, check).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                lines.Add("FAIL  exception: " + e);
                fails++;
            }
            lines.Add("FAILS: " + fails);
            Directory.CreateDirectory(scratch);
            File.WriteAllText(Path.Combine(scratch, "selftest-result.txt"), string.Join(Environment.NewLine, lines) + Environment.NewLine, Encoding.UTF8);
            foreach (var l in lines) Log.Write("selftest " + l);
            return fails;
        }

        static async Task RunAsync(string baseUrl, string scratch, Action<string, bool> check)
        {
            // a fake game: <scratch>\game\bin\win10\{echovr.exe, amd_ags_x64.dll}, <scratch>\game\_local\config.json
            var root = Path.Combine(scratch, "game");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            var bin = Path.Combine(root, "bin", "win10");
            Directory.CreateDirectory(bin);
            Directory.CreateDirectory(Path.Combine(root, "_local"));
            var exe = Path.Combine(bin, "echovr.exe");
            File.WriteAllText(exe, "not a real game");
            File.WriteAllText(Path.Combine(bin, Game.ForwarderName), "ORIGINAL AMD AGS");
            const string originalConfig = "{\"serverdb_host\":\"ws://g.echovrce.com:80/serverdb\",\"publisher_lock\":\"echovrce\"}";
            File.WriteAllText(Path.Combine(root, "_local", "config.json"), originalConfig);
            var game = new Game(exe);
            check("fake game root resolves", game.Root.TrimEnd('\\').Equals(root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
            check("target before: official", game.CurrentTarget() == "EchoVRCE (official)");

            // the three documents
            var manifest = Manifest.Parse(await Net.GetText(baseUrl + "/mods/manifest.json"));
            check("manifest parsed with files", manifest.Files.Count > 0);
            check("manifest has a loader entry", manifest.Forwarder != null);
            check("manifest has modes", manifest.Modes.Count >= 2);
            var status = Status.Parse(await Net.GetText(baseUrl + "/status.json"));
            check("status parsed with servers", status.Servers.Count > 0);
            var latest = Latest.Parse(await Net.GetText(baseUrl + "/launcher/latest.json"));
            check("latest parsed", latest.Version.Length > 0 && latest.Sha256.Length == 64);
            check("SafeDest refuses escapes", !Manifest.SafeDest("mods/../x.dll") && !Manifest.SafeDest("bin/win10/sub/x.dll") && !Manifest.SafeDest("other/x.dll") && Manifest.SafeDest("mods/a.dll") && Manifest.SafeDest("bin/win10/amd_ags_x64.dll"));

            // backend config
            game.WriteServerConfig(manifest.Backend);
            check("backup written once", File.Exists(game.BackupPath) && File.ReadAllText(game.BackupPath) == originalConfig);
            check("target after: our backend", game.CurrentTarget() == manifest.Backend);
            game.WriteServerConfig(manifest.Backend);
            check("second write keeps the first backup", File.ReadAllText(game.BackupPath) == originalConfig);
            var cfg = Json.Parse(File.ReadAllText(game.ConfigPath));
            check("config has six hosts and the lock", Json.Str(cfg, "loginservice_host").Contains("token=" + Game.ServerKey) && Json.Str(cfg, "publisher_lock") == "echovrce" && Json.Str(cfg, "apiservice_host") == "http://" + manifest.Backend);

            // loader + mods
            await game.EnsureForwarder(manifest.Forwarder, s => { }, p => { });
            check("original kept as _orig", File.Exists(game.OriginalPath) && File.ReadAllText(game.OriginalPath) == "ORIGINAL AMD AGS");
            check("loader installed with the manifest hash", game.ForwarderCurrent(manifest.Forwarder));
            await game.EnsureForwarder(manifest.Forwarder, s => { }, p => { });
            check("loader re-check is a no-op", File.ReadAllText(game.OriginalPath) == "ORIGINAL AMD AGS");

            var installed = new List<string>();
            foreach (var mode in manifest.Modes)
            {
                foreach (var dll in mode.Dlls)
                {
                    var f = manifest.FileByName(dll);
                    if (f == null) continue;
                    await game.EnsureMod(f, s => { }, p => { });
                    var dest = game.DestPath(f);
                    check("mod " + f.Name + " installed and verified", File.Exists(dest) && Net.Sha256File(dest) == f.Sha256);
                    installed.Add(Path.GetFileName(dest));
                }
            }
            // a corrupted mod is replaced (and the bad copy kept as .bak)
            if (installed.Count > 0)
            {
                var f = manifest.FileByName(installed[0]);
                var dest = game.DestPath(f);
                File.WriteAllText(dest, "corrupted");
                await game.EnsureMod(f, s => { }, p => { });
                check("corrupted mod replaced", Net.Sha256File(dest) == f.Sha256 && File.Exists(dest + ".bak"));
            }
            // a bad manifest hash is refused and leaves nothing behind
            var bad = new FileEntry { Name = "bad.dll", Kind = "mod", Dest = "mods/bad.dll", Url = manifest.Forwarder.Url, Sha256 = new string('0', 64), Size = 0 };
            var refused = false;
            try { await game.EnsureMod(bad, s => { }, p => { }); } catch (IOException) { refused = true; }
            check("wrong sha256 refused", refused && !File.Exists(Path.Combine(game.ModsDir, "bad.dll")) && !File.Exists(Path.Combine(game.ModsDir, "bad.dll.download")));

            var normal = manifest.ModeById("normal") ?? manifest.Modes[0];
            game.WriteModeFile(normal.Dlls, normal.Name);
            var modeText = File.ReadAllText(game.ModeFile);
            check("mode.txt lists the mode's dlls", normal.Dlls.Count == 0 ? modeText.Contains("none") : modeText.Contains(normal.Dlls[0]));

            // update check against latest.json (the served exe has a newer version than this one, or not)
            var newer = Updater.IsNewer(latest.Version, Program.Version);
            check("version compare works", Updater.IsNewer("1.0.1", "1.0.0") && !Updater.IsNewer("1.0.0", "1.0.0") && !Updater.IsNewer("0.9.9", "1.0.0"));
            Log.Write("selftest: latest.json says " + latest.Version + ", this is " + Program.Version + (newer ? " (update would be offered)" : " (no update)"));

            // undo
            check("restore config", game.RestoreOriginalConfig() && File.ReadAllText(game.ConfigPath) == originalConfig && game.CurrentTarget() == "EchoVRCE (official)");
            game.RemoveForwarder();
            check("original loader back", !File.Exists(game.OriginalPath) && File.ReadAllText(game.ForwarderPath) == "ORIGINAL AMD AGS");
            game.WriteModeFile(new string[0], "official");
            check("mode.txt says none", File.ReadAllText(game.ModeFile).Contains("none"));

            // a problem report can be written (to the desktop; removed again)
            var report = Report.Make(game, status);
            check("report zip written", File.Exists(report) && new FileInfo(report).Length > 0);
            try { File.Delete(report); } catch { }
        }
    }
}

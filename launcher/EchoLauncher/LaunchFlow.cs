// Echo Launcher -- the launch sequence and its undo, shared by the window and the command-line modes
// (--launch <mode>, --official), so a desktop shortcut or a test script does exactly what the button does.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EchoLauncher
{
    public static class LaunchFlow
    {
        // The manifest: the server's (signature verified, version never lower than one accepted before), else
        // the cached copy (verified again), else the built-in fallback (modes only, nothing installable).
        public static async Task<Manifest> LoadManifest(Settings settings)
        {
            byte[] bytes = null;
            var source = "";
            try
            {
                bytes = await Net.GetSigned(settings.BaseUrl + "/mods/manifest.json", "manifest.json").ConfigureAwait(false);
                source = "server";
            }
            catch (Exception e) { Log.Write("manifest from the server refused or unreachable (" + e.Message + "); trying the cached copy"); }
            if (bytes == null && File.Exists(Settings.CachedManifestPath) && File.Exists(Settings.CachedManifestPath + ".sig"))
            {
                try
                {
                    var cached = File.ReadAllBytes(Settings.CachedManifestPath);
                    string why;
                    if (Signing.Verify(cached, File.ReadAllText(Settings.CachedManifestPath + ".sig"), out why)) { bytes = cached; source = "cache"; }
                    else Log.Write("cached manifest signature rejected (" + why + ")");
                }
                catch { }
            }
            if (bytes != null)
            {
                try
                {
                    var manifest = Manifest.Parse(Encoding.UTF8.GetString(bytes));
                    if (manifest.Version < settings.ManifestVersionSeen)
                        throw new System.Security.SecurityException("manifest version " + manifest.Version + " is older than the " + settings.ManifestVersionSeen + " accepted before");
                    if (source == "server")
                    {
                        try
                        {
                            Directory.CreateDirectory(Settings.Dir);
                            File.WriteAllBytes(Settings.CachedManifestPath, bytes);
                            File.WriteAllText(Settings.CachedManifestPath + ".sig", await Net.GetText(settings.BaseUrl + "/mods/manifest.json.sig").ConfigureAwait(false));
                        }
                        catch { }
                        if (manifest.Version > settings.ManifestVersionSeen) { settings.ManifestVersionSeen = manifest.Version; settings.Save(); }
                    }
                    Log.Write("manifest v" + manifest.Version + " from " + source + " (signature ok): " + manifest.Files.Count + " files, " + manifest.Modes.Count + " modes");
                    return manifest;
                }
                catch (Exception e) { Log.Write("manifest rejected: " + e.Message); }
            }
            var fallback = Manifest.Fallback();
            Log.Write("no trusted manifest available: modes shown, but nothing can be installed");
            return fallback;
        }

        // Config -> loader -> the mode's mods -> mode.txt -> (start). Throws with a plain message on failure.
        public static async Task Launch(Game game, Manifest manifest, ModeInfo mode, Settings settings, Action<string> say, Action<double> progress, bool start)
        {
            if (game == null) throw new InvalidOperationException("Echo VR was not found; pick echovr.exe first");
            if (mode == null) throw new InvalidOperationException("no such mode");
            if (Game.IsRunning()) throw new InvalidOperationException("Echo VR is already running; close it first so the mods load");
            if (game.LooksLikeServerCopy())
                throw new InvalidOperationException(game.Root + " is a dedicated-server copy of the game (its config carries server parameters); pick your own Echo VR install with Find game...");
            if (manifest.IsFallback || manifest.Files.Count == 0)
                throw new InvalidOperationException("the mod list could not be fetched from " + settings.BaseUrl + "; check your internet connection");
            var fwd = manifest.Forwarder;
            if (fwd == null) throw new InvalidOperationException("the manifest has no mod loader entry");

            game.WriteServerConfig(manifest.Backend);
            await game.EnsureForwarder(fwd, say, progress).ConfigureAwait(false);
            var names = new List<string>();
            foreach (var dll in mode.Dlls)
            {
                var f = manifest.FileByName(dll);
                if (f == null) { Log.Write("mode " + mode.Id + " names " + dll + " but the manifest has no such file; skipped"); continue; }
                await game.EnsureMod(f, say, progress).ConfigureAwait(false);
                names.Add(Path.GetFileName(f.Dest));
            }
            game.WriteModeFile(names, mode.Name);
            settings.LastMode = mode.Id;
            settings.Save();
            if (start) game.Launch();
        }

        // Back to official: restore config, the game's own loader back, mode.txt "none". True if a config was restored.
        public static bool Official(Game game)
        {
            if (game == null) throw new InvalidOperationException("Echo VR was not found");
            if (Game.IsRunning()) throw new InvalidOperationException("Echo VR is running; close it first");
            var restored = game.RestoreOriginalConfig();
            game.RemoveForwarder();
            if (Directory.Exists(game.ModsDir)) game.WriteModeFile(new string[0], "official");
            return restored;
        }
    }
}

// Echo Launcher -- settings.json in %LOCALAPPDATA%\EchoLauncher\. Only what the player chose and where
// things are; no credentials exist on this path.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EchoLauncher
{
    public class Settings
    {
        public const string DefaultBaseUrl = "https://get.bootnet.app";

        public string BaseUrl = DefaultBaseUrl;   // where status.json, launcher/latest.json, mods/manifest.json live
        public string GameExe = "";               // ...\bin\win10\echovr.exe
        public string LastMode = "normal";        // a mode id from the manifest
        public string DiscordUrl = "";            // the manifest's "discord" wins when present
        public bool MigratedOldSwitcher = false;
        public string UpdateReady = "";           // path of a downloaded, verified new EchoLauncher.exe

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchoLauncher"); }
        }
        public static string FilePath { get { return Path.Combine(Dir, "settings.json"); } }
        public static string CachedManifestPath { get { return Path.Combine(Dir, "manifest.cache.json"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var o = Json.Parse(File.ReadAllText(FilePath, Encoding.UTF8));
                s.BaseUrl = Json.Str(o, "base_url", DefaultBaseUrl).TrimEnd('/');
                s.GameExe = Json.Str(o, "game_exe");
                s.LastMode = Json.Str(o, "last_mode", "normal");
                s.DiscordUrl = Json.Str(o, "discord_url");
                s.MigratedOldSwitcher = Json.Bool(o, "migrated_old_switcher");
                s.UpdateReady = Json.Str(o, "update_ready");
                if (string.IsNullOrWhiteSpace(s.BaseUrl)) s.BaseUrl = DefaultBaseUrl;
            }
            catch (Exception e)
            {
                Log.Write("settings.json unreadable, using defaults: " + e.Message);
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var o = new Dictionary<string, object>
                {
                    { "base_url", BaseUrl },
                    { "game_exe", GameExe },
                    { "last_mode", LastMode },
                    { "discord_url", DiscordUrl },
                    { "migrated_old_switcher", MigratedOldSwitcher },
                    { "update_ready", UpdateReady },
                };
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, Json.Stringify(o), Encoding.UTF8);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Log.Write("could not save settings: " + e.Message);
            }
        }
    }
}

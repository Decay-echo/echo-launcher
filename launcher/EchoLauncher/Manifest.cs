// Echo Launcher -- the three JSON documents get.bootnet.app serves (shapes agreed in echo-stage report 29):
//   /status.json            what is up, for the status panel
//   /launcher/latest.json   the newest launcher, for self-update
//   /mods/manifest.json     the files to install and the launch modes to offer
using System;
using System.Collections.Generic;

namespace EchoLauncher
{
    public class FileEntry
    {
        public string Name, Kind, Dest, Url, Sha256, Version;
        public long Size;
    }

    public class ModeInfo
    {
        public string Id, Name, Note;
        public List<string> Dlls = new List<string>();
    }

    public class Manifest
    {
        public long Version;
        public string Discord = "", Backend = "echo.bootnet.app:7350", Modsync = "echo.bootnet.app:7400";
        public List<FileEntry> Files = new List<FileEntry>();
        public List<ModeInfo> Modes = new List<ModeInfo>();
        public string RawJson = "";
        public bool IsFallback;

        public FileEntry Forwarder
        {
            get { foreach (var f in Files) if (f.Kind == "forwarder") return f; return null; }
        }

        public FileEntry FileByName(string name)
        {
            foreach (var f in Files) if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        public ModeInfo ModeById(string id)
        {
            foreach (var m in Modes) if (m.Id == id) return m;
            return null;
        }

        // Downloads come over https, or from this machine during a test.
        public static bool SafeUrl(string url)
        {
            return !string.IsNullOrEmpty(url) && (url.StartsWith("https://") || url.StartsWith("http://127.0.0.1") || url.StartsWith("http://localhost"));
        }

        // Only the two folders the launcher may write to, with forward slashes and no way up.
        public static bool SafeDest(string dest)
        {
            if (string.IsNullOrEmpty(dest)) return false;
            var d = dest.Replace('\\', '/');
            if (d.Contains("..") || d.Contains("//") || d.StartsWith("/") || d.Contains(":")) return false;
            if (!(d.StartsWith("bin/win10/") || d.StartsWith("mods/"))) return false;
            var leaf = d.Substring(d.LastIndexOf('/') + 1);
            if (leaf.Length == 0 || !leaf.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return false;
            return d.Split('/').Length == (d.StartsWith("bin/") ? 3 : 2);
        }

        public static Manifest Parse(string json)
        {
            var o = Json.Parse(json);
            if (Json.AsObject(o) == null) throw new FormatException("manifest is not a JSON object");
            var m = new Manifest { RawJson = json };
            m.Version = Json.Int(o, "version");
            m.Discord = Json.Str(o, "discord");
            m.Backend = Json.Str(o, "backend", m.Backend);
            m.Modsync = Json.Str(o, "modsync", m.Modsync);
            foreach (var fo in Json.Arr(o, "files"))
            {
                var f = new FileEntry
                {
                    Name = Json.Str(fo, "name"), Kind = Json.Str(fo, "kind"), Dest = Json.Str(fo, "dest").Replace('\\', '/'),
                    Url = Json.Str(fo, "url"), Sha256 = Json.Str(fo, "sha256").ToLowerInvariant(), Version = Json.Str(fo, "version"),
                    Size = Json.Int(fo, "size"),
                };
                if (f.Name.Length == 0 || !SafeDest(f.Dest) || f.Sha256.Length != 64 || !SafeUrl(f.Url))
                {
                    Log.Write("manifest: file entry refused (name/dest/sha256/url): " + f.Name + " -> " + f.Dest);
                    continue;
                }
                m.Files.Add(f);
            }
            foreach (var mo in Json.Arr(o, "modes"))
            {
                var mode = new ModeInfo { Id = Json.Str(mo, "id"), Name = Json.Str(mo, "name"), Note = Json.Str(mo, "note"), Dlls = Json.StrList(mo, "dlls") };
                if (mode.Id.Length == 0 || mode.Name.Length == 0) continue;
                m.Modes.Add(mode);
            }
            if (m.Modes.Count == 0) m.Modes.Add(new ModeInfo { Id = "normal", Name = "Normal (stock physics)", Note = "no mods" });
            return m;
        }

        // When neither the server nor the cache can be read: the known modes, no files (install needs the real one).
        public static Manifest Fallback()
        {
            var m = new Manifest { IsFallback = true };
            m.Modes.Add(new ModeInfo { Id = "normal", Name = "Normal (stock physics)", Note = "you still see other players' mods", Dlls = new List<string> { "echo_viewer_client.dll" } });
            m.Modes.Add(new ModeInfo { Id = "gravity", Name = "Gravity", Dlls = new List<string> { "echo_gravity_client.dll" } });
            return m;
        }
    }

    public class ServerStatus
    {
        public string Name = "", Address = "", Mode = "", MatchState = "";
        public bool Up;
        public long Players, Max;
    }

    public class Status
    {
        public string Ts = "", Backend = "", Modsync = "", Motd = "";
        public List<ServerStatus> Servers = new List<ServerStatus>();

        public static Status Parse(string json)
        {
            var o = Json.Parse(json);
            if (Json.AsObject(o) == null) throw new FormatException("status is not a JSON object");
            var s = new Status { Ts = Json.Str(o, "ts"), Backend = Json.Str(o, "backend"), Modsync = Json.Str(o, "modsync"), Motd = Json.Str(o, "motd") };
            foreach (var so in Json.Arr(o, "servers"))
            {
                var sv = new ServerStatus
                {
                    Name = Json.Str(so, "name"), Up = Json.Bool(so, "up"), Address = Json.Str(so, "address"),
                    Players = Json.Int(so, "players"), Max = Json.Int(so, "max", 16), Mode = Json.Str(so, "mode"),
                };
                var match = Json.Get(so, "match");
                if (match != null) sv.MatchState = Json.Str(match, "state");
                s.Servers.Add(sv);
            }
            return s;
        }
    }

    public class Latest
    {
        public string Version = "", Url = "", Sha256 = "", Notes = "", MinSupported = "";
        public long Size;

        public static Latest Parse(string json)
        {
            var o = Json.Parse(json);
            if (Json.AsObject(o) == null) throw new FormatException("latest.json is not a JSON object");
            return new Latest
            {
                Version = Json.Str(o, "version"), Url = Json.Str(o, "url"), Sha256 = Json.Str(o, "sha256").ToLowerInvariant(),
                Notes = Json.Str(o, "notes"), MinSupported = Json.Str(o, "min_supported"), Size = Json.Int(o, "size"),
            };
        }
    }
}

// Echo Launcher -- launcher.log: one line per event, in %LOCALAPPDATA%\EchoLauncher\. Nothing secret is ever
// logged (the only credential-like value on this path, the backend's socket key, is public).
using System;
using System.IO;
using System.Text;

namespace EchoLauncher
{
    public static class Log
    {
        static readonly object Gate = new object();
        public static string Path { get; private set; }
        const long MaxBytes = 2 * 1024 * 1024;

        public static void Init(string dir)
        {
            Directory.CreateDirectory(dir);
            Path = System.IO.Path.Combine(dir, "launcher.log");
            try
            {
                var fi = new FileInfo(Path);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    var old = Path + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(Path, old);
                }
            }
            catch { }
        }

        public static void Write(string message)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message;
            lock (Gate)
            {
                try { if (Path != null) File.AppendAllText(Path, line + Environment.NewLine, Encoding.UTF8); } catch { }
            }
            try { LineWritten?.Invoke(line); } catch { }
        }

        public static void Write(string format, params object[] args)
        {
            try { Write(string.Format(format, args)); } catch { Write(format); }
        }

        // The main window shows the log as it grows.
        public static event Action<string> LineWritten;
    }
}

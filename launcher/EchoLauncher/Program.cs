// Echo Launcher -- entry point. One window, one instance. "--apply-update" is the self-update's second half.
using System;
using System.Threading;
using System.Windows.Forms;

namespace EchoLauncher
{
    public static class Program
    {
        public const string Version = "1.0.0";
        public static string ExePath { get { return Application.ExecutablePath; } }

        [STAThread]
        public static int Main(string[] args)
        {
            Log.Init(Settings.Dir);
            if (args.Length == 3 && args[0] == "--apply-update") return Updater.RunApply(args);
            if (args.Length == 3 && args[0] == "--selftest") return SelfTest.Run(args[1], args[2]);

            bool created;
            using (var mutex = new Mutex(true, "Local\\EchoLauncher.single", out created))
            {
                if (!created)
                {
                    MessageBox.Show("Echo Launcher is already open.", "Echo Launcher");
                    return 0;
                }
                Log.Write("Echo Launcher " + Version + " starting from " + ExePath);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => { Log.Write("unexpected error: " + e.Exception); MessageBox.Show("Something went wrong: " + e.Exception.Message, "Echo Launcher"); };
                Application.Run(new MainForm());
                GC.KeepAlive(mutex);
            }
            return 0;
        }
    }
}

// Echo Launcher -- command-line modes, for shortcuts and tests (no window, no dialogs; exit code 0 = done):
//   EchoLauncher.exe --launch <mode id> [--game <path to echovr.exe>] [--no-start]
//   EchoLauncher.exe --official [--game <path to echovr.exe>]
// What happened is in launcher.log and in %LOCALAPPDATA%\EchoLauncher\last-command.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EchoLauncher
{
    public static class Headless
    {
        public static int Run(string[] args)
        {
            var lines = new List<string>();
            Action<string> say = s => { lines.Add(s); Log.Write(s); };
            var code = 1;
            try
            {
                var settings = Settings.Load();
                string gameExe = null;
                var start = true;
                for (var i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--game" && i + 1 < args.Length) gameExe = args[++i];
                    else if (args[i] == "--no-start") start = false;
                }
                if (gameExe == null) gameExe = Game.FindExe(settings);
                if (gameExe == null || !File.Exists(gameExe)) throw new InvalidOperationException("echovr.exe not found; pass --game <path>");
                var game = new Game(gameExe);
                say("game: " + game.Root);
                if (args[0] == "--official")
                {
                    var restored = LaunchFlow.Official(game);
                    say(restored ? "back to official: config restored, loader removed" : "loader removed; no saved original config to restore");
                    code = 0;
                }
                else
                {
                    var manifest = LaunchFlow.LoadManifest(settings).GetAwaiter().GetResult();
                    var mode = manifest.ModeById(args.Length > 1 ? args[1] : settings.LastMode);
                    if (mode == null) throw new InvalidOperationException("no such mode; one of: " + string.Join(", ", manifest.Modes.ConvertAll(m => m.Id)));
                    LaunchFlow.Launch(game, manifest, mode, settings, say, p => { }, start).GetAwaiter().GetResult();
                    say((start ? "started Echo VR" : "prepared Echo VR") + " in mode '" + mode.Name + "'; config points at " + game.CurrentTarget() + "; loader " + (game.ForwarderInstalled ? "installed" : "NOT installed"));
                    code = 0;
                }
            }
            catch (Exception e)
            {
                say("failed: " + e.Message);
                Log.Write("headless command failed: " + e);
            }
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                File.WriteAllText(Path.Combine(Settings.Dir, "last-command.txt"), string.Join(Environment.NewLine, lines) + Environment.NewLine + "exit " + code + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
            return code;
        }
    }
}

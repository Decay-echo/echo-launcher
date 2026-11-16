// Echo Launcher -- the window. Top: what is up (status.json). Middle: the mode and the two big buttons.
// Bottom: the log. Everything slow runs off the UI thread and reports back through Say()/Progress().
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EchoLauncher
{
    public class MainForm : Form
    {
        readonly Settings settings = Settings.Load();
        Manifest manifest = Manifest.Fallback();
        Status status;
        Game game;
        bool busy;

        // controls
        readonly Label lblTitle = new Label(), lblServers = new Label(), lblMotd = new Label(), lblGame = new Label(), lblModeNote = new Label(), lblStatusLine = new Label();
        readonly ListView lvServers = new ListView();
        readonly ComboBox cboMode = new ComboBox();
        readonly Button btnLaunch = new Button(), btnOfficial = new Button(), btnReport = new Button(), btnGame = new Button(), btnRefresh = new Button(), btnUpdate = new Button(), btnSettings = new Button();
        readonly ProgressBar progress = new ProgressBar();
        readonly TextBox log = new TextBox();
        readonly Timer statusTimer = new Timer { Interval = 10000 };

        static readonly Color Bg = Color.FromArgb(32, 34, 37), Panel = Color.FromArgb(24, 25, 28), Fg = Color.White, Dim = Color.Gainsboro;
        static readonly Color Blue = Color.FromArgb(88, 101, 242), Green = Color.FromArgb(59, 165, 93), Grey = Color.FromArgb(64, 68, 75), Orange = Color.FromArgb(230, 126, 34);

        public MainForm()
        {
            Text = "Echo Launcher " + Program.Version;
            Size = new Size(640, 760);
            MinimumSize = new Size(560, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Segoe UI", 10);
            BuildLayout();
            Log.LineWritten += line => { try { BeginInvoke(new Action(() => AppendLog(line))); } catch { } };
            Shown += async (s, e) => await Startup();
            statusTimer.Tick += async (s, e) => await RefreshStatus(false);
        }

        void BuildLayout()
        {
            var y = 14;
            lblTitle.Text = "Echo Launcher";
            lblTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(20, y);
            Controls.Add(lblTitle);

            btnUpdate.Text = "Update ready: restart";
            btnUpdate.Size = new Size(190, 30);
            btnUpdate.Location = new Point(410, y + 2);
            btnUpdate.BackColor = Orange; btnUpdate.FlatStyle = FlatStyle.Flat; btnUpdate.Visible = false;
            btnUpdate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUpdate.Click += (s, e) => { if (!string.IsNullOrEmpty(settings.UpdateReady) && File.Exists(settings.UpdateReady)) Updater.ApplyAndExit(settings.UpdateReady); };
            Controls.Add(btnUpdate);
            y += 44;

            lblServers.Text = "Servers";
            lblServers.AutoSize = true; lblServers.Location = new Point(22, y); lblServers.ForeColor = Dim;
            Controls.Add(lblServers);
            btnRefresh.Text = "Refresh"; btnRefresh.Size = new Size(90, 26); btnRefresh.Location = new Point(510, y - 4);
            btnRefresh.FlatStyle = FlatStyle.Flat; btnRefresh.BackColor = Grey; btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Click += async (s, e) => await RefreshStatus(true);
            Controls.Add(btnRefresh);
            y += 26;

            lvServers.View = View.Details; lvServers.FullRowSelect = true; lvServers.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lvServers.BackColor = Panel; lvServers.ForeColor = Fg; lvServers.BorderStyle = BorderStyle.FixedSingle;
            lvServers.Columns.Add("Server", 90); lvServers.Columns.Add("State", 90); lvServers.Columns.Add("Players", 80); lvServers.Columns.Add("Mode", 90);
            lvServers.Size = new Size(580, 96); lvServers.Location = new Point(22, y);
            lvServers.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(lvServers);
            y += 102;

            lblMotd.AutoSize = false; lblMotd.Size = new Size(580, 40); lblMotd.Location = new Point(22, y); lblMotd.ForeColor = Dim;
            lblMotd.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(lblMotd);
            y += 44;

            lblGame.AutoSize = false; lblGame.Size = new Size(470, 40); lblGame.Location = new Point(22, y); lblGame.ForeColor = Dim;
            lblGame.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(lblGame);
            btnGame.Text = "Find game..."; btnGame.Size = new Size(110, 30); btnGame.Location = new Point(492, y);
            btnGame.FlatStyle = FlatStyle.Flat; btnGame.BackColor = Grey; btnGame.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnGame.Click += (s, e) => PickGame();
            Controls.Add(btnGame);
            y += 48;

            var lblMode = new Label { Text = "Game mode", AutoSize = true, Location = new Point(22, y), ForeColor = Dim };
            Controls.Add(lblMode);
            y += 24;
            cboMode.DropDownStyle = ComboBoxStyle.DropDownList; cboMode.Size = new Size(580, 30); cboMode.Location = new Point(22, y);
            cboMode.BackColor = Panel; cboMode.ForeColor = Fg; cboMode.FlatStyle = FlatStyle.Flat;
            cboMode.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cboMode.SelectedIndexChanged += (s, e) => ShowModeNote();
            Controls.Add(cboMode);
            y += 34;
            lblModeNote.AutoSize = false; lblModeNote.Size = new Size(580, 36); lblModeNote.Location = new Point(22, y); lblModeNote.ForeColor = Dim;
            lblModeNote.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(lblModeNote);
            y += 42;

            btnLaunch.Text = "Launch Echo on our servers"; btnLaunch.Size = new Size(580, 48); btnLaunch.Location = new Point(22, y);
            btnLaunch.BackColor = Blue; btnLaunch.FlatStyle = FlatStyle.Flat; btnLaunch.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            btnLaunch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnLaunch.Click += async (s, e) => await LaunchClicked();
            Controls.Add(btnLaunch);
            y += 56;

            btnOfficial.Text = "Back to official EchoVRCE"; btnOfficial.Size = new Size(580, 40); btnOfficial.Location = new Point(22, y);
            btnOfficial.BackColor = Green; btnOfficial.FlatStyle = FlatStyle.Flat; btnOfficial.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btnOfficial.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnOfficial.Click += (s, e) => OfficialClicked();
            Controls.Add(btnOfficial);
            y += 48;

            btnReport.Text = "Report a problem"; btnReport.Size = new Size(160, 30); btnReport.Location = new Point(22, y);
            btnReport.FlatStyle = FlatStyle.Flat; btnReport.BackColor = Grey;
            btnReport.Click += (s, e) => ReportClicked();
            Controls.Add(btnReport);
            btnSettings.Text = "Settings..."; btnSettings.Size = new Size(120, 30); btnSettings.Location = new Point(482, y);
            btnSettings.FlatStyle = FlatStyle.Flat; btnSettings.BackColor = Grey; btnSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSettings.Click += (s, e) => SettingsClicked();
            Controls.Add(btnSettings);
            y += 40;

            progress.Size = new Size(580, 8); progress.Location = new Point(22, y); progress.Visible = false;
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(progress);
            y += 12;
            lblStatusLine.AutoSize = false; lblStatusLine.Size = new Size(580, 22); lblStatusLine.Location = new Point(22, y); lblStatusLine.ForeColor = Dim;
            lblStatusLine.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(lblStatusLine);
            y += 26;

            log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.BackColor = Panel; log.ForeColor = Dim;
            log.Font = new Font("Consolas", 9); log.Location = new Point(22, y);
            log.Size = new Size(580, ClientSize.Height - y - 16);
            log.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(log);
        }

        // ---- startup and status ----

        async Task Startup()
        {
            Say("Starting...");
            try { Migration.Run(settings); } catch (Exception e) { Log.Write("migration: " + e.Message); }
            FindGame();
            await LoadManifest();
            await RefreshStatus(false);
            statusTimer.Start();
            if (!string.IsNullOrEmpty(settings.UpdateReady) && File.Exists(settings.UpdateReady)) ShowUpdateButton();
            else await CheckUpdate();
            Say(game != null ? "Ready. Pick a mode and launch." : "Ready. Find your Echo VR folder first (Find game...).");
        }

        void FindGame()
        {
            var exe = Game.FindExe(settings);
            game = exe != null ? new Game(exe) : null;
            if (game != null && settings.GameExe != exe) { settings.GameExe = exe; settings.Save(); }
            ShowGame();
        }

        void ShowGame()
        {
            if (game == null) { lblGame.Text = "Echo VR not found. Click Find game... and pick echovr.exe (inside your Echo folder, under bin\\win10)."; lblGame.ForeColor = Orange; return; }
            lblGame.ForeColor = Dim;
            lblGame.Text = "Game: " + game.Root + "\nPlays on: " + TargetName() + (game.ForwarderInstalled ? "   |   mod loader: installed" : "   |   mod loader: not installed");
        }

        void PickGame()
        {
            using (var dlg = new OpenFileDialog { Title = "Find echovr.exe (inside your Echo VR folder, under bin\\win10)", Filter = "echovr.exe|echovr.exe|All programs (*.exe)|*.exe" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (!Path.GetFileName(dlg.FileName).Equals("echovr.exe", StringComparison.OrdinalIgnoreCase)) { Say("Please pick the file named echovr.exe."); return; }
                settings.GameExe = dlg.FileName; settings.Save();
                game = new Game(dlg.FileName);
                Log.Write("using game " + dlg.FileName);
                ShowGame();
            }
        }

        async Task LoadManifest()
        {
            manifest = await LaunchFlow.LoadManifest(settings);
            cboMode.Items.Clear();
            var select = 0;
            for (var i = 0; i < manifest.Modes.Count; i++)
            {
                cboMode.Items.Add(manifest.Modes[i].Name);
                if (manifest.Modes[i].Id == settings.LastMode) select = i;
            }
            if (cboMode.Items.Count > 0) cboMode.SelectedIndex = select;
            ShowModeNote();
        }

        ModeInfo SelectedMode()
        {
            var i = cboMode.SelectedIndex;
            return i >= 0 && i < manifest.Modes.Count ? manifest.Modes[i] : null;
        }

        void ShowModeNote()
        {
            var m = SelectedMode();
            lblModeNote.Text = m == null ? "" : (string.IsNullOrEmpty(m.Note) ? (m.Dlls.Count == 0 ? "No mod." : string.Join(", ", m.Dlls)) : m.Note);
        }

        async Task RefreshStatus(bool manual)
        {
            try
            {
                var json = await Net.GetText(settings.BaseUrl + "/status.json", 10);
                status = Status.Parse(json);
                var host = manifest.Backend.Split(':')[0];
                int port;
                if (!int.TryParse(manifest.Backend.Contains(":") ? manifest.Backend.Split(':')[1] : "7350", out port)) port = 7350;
                var ping = await Net.PingMs(host, port);
                lvServers.BeginUpdate();
                lvServers.Items.Clear();
                foreach (var s in status.Servers)
                {
                    var it = new ListViewItem(new[] { s.Name, s.Up ? (string.IsNullOrEmpty(s.MatchState) ? "up" : s.MatchState) : "down", s.Up ? s.Players + " / " + s.Max : "-", s.Mode });
                    it.ForeColor = s.Up ? Fg : Color.Gray;
                    lvServers.Items.Add(it);
                }
                lvServers.EndUpdate();
                lblServers.Text = "Servers   (backend " + status.Backend + ", mod sync " + status.Modsync + ", ping " + (ping < 0 ? "-" : ping + " ms") + ")";
                lblMotd.Text = status.Motd;
                if (manual) Say("Status refreshed.");
            }
            catch (Exception e)
            {
                // The reason can carry the status address, which players are not shown; it goes to the log.
                Log.Write("status not reachable: " + e.Message);
                lblServers.Text = "Servers   (status not reachable)";
                if (manual) Say("Could not read the server status.");
            }
        }

        bool updateRequired;

        async Task CheckUpdate()
        {
            try
            {
                var bytes = await Net.GetSigned(settings.BaseUrl + "/launcher/latest.json", "latest.json", 10);
                var latest = Latest.Parse(Encoding.UTF8.GetString(bytes));
                if (Updater.IsNewer(Program.Version, latest.Version))
                {
                    Log.Write("latest.json offers " + latest.Version + ", older than this " + Program.Version + ": ignored");
                    return;
                }
                if (!string.IsNullOrEmpty(latest.MinSupported) && Updater.IsNewer(latest.MinSupported, Program.Version))
                {
                    updateRequired = true;
                    btnLaunch.Enabled = false;
                    Say("This Echo Launcher (" + Program.Version + ") is no longer supported; the update is required before launching.");
                }
                if (!Updater.IsNewer(latest.Version, Program.Version)) return;
                Log.Write("launcher " + latest.Version + " is available (this is " + Program.Version + "): " + latest.Notes);
                var path = await Updater.Fetch(latest, Say, Progress);
                if (path != null) { settings.UpdateReady = path; settings.Save(); ShowUpdateButton(); }
            }
            catch (Exception e) { Log.Write("update check: " + e.Message); }
            finally { Progress(-1); }
        }

        void ShowUpdateButton()
        {
            btnUpdate.Visible = true;
            btnUpdate.Text = updateRequired ? "Update required: restart" : "Update ready: restart";
            Say(updateRequired ? "The required update is downloaded: click 'Update required: restart'."
                               : "A newer Echo Launcher is downloaded: click 'Update ready: restart' when convenient.");
        }

        // ---- the two big buttons ----

        async Task LaunchClicked()
        {
            if (busy) return;
            if (updateRequired) { Say("Update first: click 'Update required: restart'."); return; }
            if (game == null) { Say("Find your Echo VR folder first (Find game...)."); PickGame(); if (game == null) return; }
            if (Game.IsRunning()) { Say("Echo VR is already running. Close it, then launch from here so the mods load."); return; }
            var mode = SelectedMode();
            if (mode == null) { Say("Pick a mode first."); return; }
            busy = true; SetButtons(false);
            try
            {
                if (manifest.IsFallback || manifest.Files.Count == 0) await LoadManifest();
                mode = SelectedMode() ?? mode;
                await LaunchFlow.Launch(game, manifest, mode, settings, Say, Progress, true);
                Progress(-1);
                Say("Echo VR started in mode '" + mode.Name + "'. First time here: the game shows a four-character code; give it to the Discord bot with /link-headset.");
                ShowGame();
            }
            catch (Exception e)
            {
                Log.Write("launch failed: " + e);
                Say("Could not launch: " + e.Message);
            }
            finally { busy = false; SetButtons(true); Progress(-1); }
        }

        void OfficialClicked()
        {
            if (busy) return;
            if (game == null) { PickGame(); if (game == null) return; }
            if (Game.IsRunning()) { Say("Close Echo VR first, then click again."); return; }
            try
            {
                var restored = LaunchFlow.Official(game);
                Say(restored ? "Back on official EchoVRCE: original config restored, mod loader removed. Start Echo as usual."
                             : "Mod loader removed. No saved original config to restore (you may never have connected from here).");
                ShowGame();
            }
            catch (Exception e) { Log.Write("back to official failed: " + e); Say("Could not switch back: " + e.Message); }
        }

        void ReportClicked()
        {
            try
            {
                var path = Report.Make(game, status);
                Say("Report saved to your desktop: " + Path.GetFileName(path) + ". Post it in Discord with what went wrong.");
                try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true }); } catch { }
            }
            catch (Exception e) { Say("Could not write the report: " + e.Message); }
        }

        void SettingsClicked()
        {
            // Where the launcher fetches status and updates is not shown to players. A test can still set base_url in the
            // launcher's settings file.
            using (var dlg = new Form { Text = "Echo Launcher settings", Size = new Size(520, 150), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Bg, ForeColor = Fg, Font = Font })
            {
                var l2 = new Label { Text = "Game: " + (game != null ? game.Exe : "(not found)"), AutoSize = false, Size = new Size(476, 22), Location = new Point(14, 20), ForeColor = Dim };
                var ok = new Button { Text = "Close", Location = new Point(394, 64), Size = new Size(96, 30), FlatStyle = FlatStyle.Flat, BackColor = Blue, DialogResult = DialogResult.OK };
                dlg.Controls.AddRange(new Control[] { l2, ok });
                dlg.AcceptButton = ok;
                dlg.ShowDialog(this);
            }
        }

        // Where the game plays, in words: our servers, the official service, or another one. No address is shown.
        string TargetName()
        {
            var t = game.CurrentTarget();
            if (t == "no config yet" || t == "unknown" || t == "unreadable config" || t.StartsWith("EchoVRCE")) return t;
            var ours = manifest.Backend.Split(':')[0];
            return t.Split(':')[0].Equals(ours, StringComparison.OrdinalIgnoreCase) ? "our servers" : "another server";
        }

        // ---- helpers ----

        void SetButtons(bool on) { btnLaunch.Enabled = on; btnOfficial.Enabled = on; btnGame.Enabled = on; btnSettings.Enabled = on; }

        void Say(string text)
        {
            if (InvokeRequired) { try { BeginInvoke(new Action(() => Say(text))); } catch { } return; }
            lblStatusLine.Text = text;
            Log.Write(text);
        }

        void Progress(double fraction)
        {
            if (InvokeRequired) { try { BeginInvoke(new Action(() => Progress(fraction))); } catch { } return; }
            if (fraction < 0) { progress.Visible = false; return; }
            progress.Visible = true;
            progress.Value = Math.Max(0, Math.Min(100, (int)(fraction * 100)));
        }

        void AppendLog(string line)
        {
            if (log.TextLength > 200000) log.Clear();
            log.AppendText(line + Environment.NewLine);
        }
    }
}

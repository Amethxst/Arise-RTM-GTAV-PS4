using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using libdebug;

namespace Arise_RTM
{
    public sealed class MainForm : Form, IToolHost
    {
        private const int ConnectionTimeoutMs = 10000;
        private const string TargetGameVersion = "1.57";
        private const string GameProcessName = "eboot.bin";
        private const string XUrl = "https://x.com/yAmethxst";
        private const string DiscordUrl = "https://amethxst.dev/discord";

        private const int PanelHeight = OutfitsPanel.DesiredHeight;
        private const int TabHeaderHeight = 28;
        private const int WindowHeightWithLog = 938;
        private const int WindowHeightWithoutLog = 870;

        private sealed class TimedResult<T>
        {
            public bool TimedOut;
            public T Value;
        }

        private sealed class PreflightResult
        {
            public bool Passed;
            public string Failure;
        }

        private readonly PS4Connection connection = new PS4Connection();
        private readonly string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        private GameProfile profile;
        private StatEngine engine;
        private int gamePid = -1;
        private bool gameReady;
        private bool busy;
        private string operationTitle = string.Empty;
        private readonly bool previewMode;
        private CancellationTokenSource cancellation;
        private int lastProgressTick;

        private TextBox ipTextBox, payloadPortTextBox, ftpPortTextBox, logTextBox;
        private Button connectPs4Button, connectGameButton, cancelButton;
        private Label firmwareLabel, titleIdLabel, patchLabel, preflightLabel, progressLabel, lockLabel;
        private Panel connectionGroup, characterGroup;
        private Panel lockOverlay;
        private RadioButton character1Button, character2Button;
        private CheckBox saveCheckBox, verifyCheckBox, confirmCheckBox, showLogCheckBox;
        private ThemedTabs tabs;
        private OutfitsPanel outfitsPanel;
        private DebugPanel debugPanel;
        private ProgressStrip progressBar;
        private ToolStripStatusLabel statusLabel;

        /// <param name="preview">Unlocks the whole window without a console, to look at it; nothing can be written.</param>
        public MainForm(bool preview = false)
        {
            previewMode = preview;
            BuildUi();
            LoadConnectionInfo();
            InitializeEngine();
            Theme.Apply(this);
            UpdateGate();

            if (previewMode)
            {
                Text += " - preview";
                Log("Preview mode: the window is unlocked without a console. Nothing can be sent to the game.");
                SetStatus("Preview mode - no console connected", Color.DarkOrange);
            }
        }

        // ---- IToolHost ------------------------------------------------------------------

        StatEngine IToolHost.Engine => engine;

        PS4Connection IToolHost.Connection => connection;

        int IToolHost.Slot => character1Button.Checked ? 0 : (character2Button.Checked ? 1 : -1);

        void IToolHost.ChooseCharacter(int slot)
        {
            if (slot == 0)
                character1Button.Checked = true;
            else if (slot == 1)
                character2Button.Checked = true;
        }

        void IToolHost.Log(string message)
        {
            Log(message);
        }

        void IToolHost.SetStatus(string text, Color color)
        {
            SetStatus(text, color);
        }

        bool IToolHost.Confirm(string message, string caption)
        {
            return MessageBox.Show(message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        public Task RunOptionAsync(IRecoveryOption option)
        {
            return RunAsync(option.Name, option.RequiredHandlers, option.OptionalHandlers, option.Run);
        }

        public async Task RunAsync(string title, IEnumerable<string> requiredHandlers, IEnumerable<string> optionalHandlers,
            Func<OptionContext, OptionResult> work, string confirmDetails = null, bool changesStats = true, bool needsCharacter = true)
        {
            IToolHost host = this;
            if (busy || !gameReady)
            {
                string reason = busy ? "another operation is running"
                    : previewMode ? "preview mode: no console is connected"
                    : "the game is not connected (connect to the PS4 and to GTAV first)";
                Log($"'{title}' not started: {reason}.");
                SetStatus($"'{title}' not started", Color.DarkOrange);
                return;
            }

            if (needsCharacter && host.Slot < 0)
            {
                MessageBox.Show("Choose Character 1 or Character 2 in the toolbar first.", "Character", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> required = requiredHandlers.ToList();
            string missing = required.FirstOrDefault(handler => !engine.IsAvailable(handler));
            if (missing != null)
            {
                Log($"'{title}' needs the handler '{missing}', which is unavailable on this build.");
                SetStatus($"'{title}' unavailable on this build", Color.DarkOrange);
                return;
            }

            string who = character1Button.Checked ? "Character 1" : "Character 2";
            bool askToApply = confirmDetails != null || (changesStats && confirmCheckBox.Checked);
            List<string> unverified = required.Concat(optionalHandlers)
                .Distinct()
                .Where(handler => engine.IsAvailable(handler) && !engine.IsVerified(handler))
                .ToList();

            if (askToApply || unverified.Count > 0)
            {
                string where = host.Slot >= 0 ? " on " + who : string.Empty;
                StringBuilder message = new StringBuilder(askToApply
                    ? $"{(changesStats ? "Apply" : "Run")} '{title}'{where}?\n\n{confirmDetails ?? "This changes your stats."}"
                    : $"Run '{title}'{where}?");

                if (unverified.Count > 0)
                    message.Append("\n\nFirst run on this build: the game has not been checked with " + string.Join(", ", unverified) +
                                   " yet. If something is wrong, the game may crash.");

                if (!host.Confirm(message.ToString(), "Confirm"))
                    return;
            }

            BeginOperation(title);
            try
            {
                int currentPid = await Task.Run(() => connection.FindProcessID(GameProcessName));
                if (currentPid != gamePid)
                {
                    Log("The GTA V process changed or closed. Connect to the game again.");
                    ResetGameState();
                    SetStatus("Game restarted - reconnect to GTAV", Color.DarkOrange);
                    return;
                }

                OptionContext context = new OptionContext
                {
                    Engine = engine,
                    Slot = host.Slot,
                    Verify = verifyCheckBox.Checked,
                    Save = saveCheckBox.Checked,
                    Log = Log,
                    Progress = ReportProgress,
                    Cancel = cancellation.Token
                };

                Log(context.Slot >= 0 ? $"Running '{title}' on MP{context.Slot}, verify={context.Verify}, save={context.Save}." : $"Running '{title}'.");
                OptionResult result = await Task.Run(() => work(context));

                Log($"'{title}' done: {result.Summary}.");
                if (result.IsClean)
                    await Task.Run(() => engine.CommitSignatures());

                SetStatus(result.IsClean ? $"'{title}' applied" : $"'{title}' finished: {result.Summary}",
                    result.IsClean ? Color.Green : Color.DarkOrange);

                if (result.IsClean && changesStats)
                    try { connection.Notify($"Arise - RTM: {title}"); } catch { }
            }
            catch (OperationCanceledException)
            {
                Log($"'{title}' cancelled. What is already written stays written.");
                SetStatus($"'{title}' cancelled", Color.DarkOrange);
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                SetStatus($"'{title}' failed", Color.Red);
            }
            finally
            {
                EndOperation();
            }
        }

        // ---- operation lock -------------------------------------------------------------

        private void BeginOperation(string title)
        {
            busy = true;
            operationTitle = title;
            cancellation = new CancellationTokenSource();
            progressLabel.Text = title;
            progressBar.Indeterminate = true;
            cancelButton.Enabled = true;
            UpdateGate();
        }

        private void EndOperation()
        {
            busy = false;
            progressBar.Indeterminate = false;
            progressBar.Value = 0;
            progressLabel.Text = "Idle";
            cancelButton.Enabled = false;
            cancellation?.Dispose();
            cancellation = null;
            UpdateGate();
        }

        private void ReportProgress(int done, int total)
        {
            int now = Environment.TickCount;
            if (done < total && now - lastProgressTick < 80)
                return;

            lastProgressTick = now;
            if (IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                if (!busy)
                    return;

                progressBar.Indeterminate = false;
                progressBar.Maximum = Math.Max(1, total);
                progressBar.Value = Math.Min(done, progressBar.Maximum);
                progressBar.Invalidate();
                progressLabel.Text = $"{operationTitle}  {done:N0}/{total:N0}";
            }));
        }

        private void UpdateGate()
        {
            connectionGroup.Enabled = !busy;
            bool usable = gameReady || previewMode;
            characterGroup.Enabled = usable && !busy;
            lockOverlay.Visible = !usable || busy;

            if (busy)
                lockLabel.Text = "Working: " + operationTitle;
            else
                lockLabel.Text = "Connect to the PS4 and to GTAV first.";

            Theme.StyleToggle(character1Button);
            Theme.StyleToggle(character2Button);
            outfitsPanel?.SetCharacterChosen(((IToolHost)this).Slot >= 0);
            debugPanel?.SetCharacter(((IToolHost)this).Slot);
        }

        // ---- UI -------------------------------------------------------------------------

        private void BuildUi()
        {
            Text = "Arise - RTM (GTA V 1.57)";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, WindowHeightWithLog);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;

            Panel banner = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(8, 8, 8), Tag = "keep" };
            banner.Paint += (s, e) =>
            {
                using (Brush accent = new SolidBrush(Theme.Yellow))
                    e.Graphics.FillRectangle(accent, 0, banner.Height - 2, banner.Width, 2);
            };
            banner.Controls.Add(new Label
            {
                Text = "Arise - RTM", AutoSize = true, Location = new Point(10, 8),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Theme.Text
            });
            firmwareLabel = BannerInfo("FW: N/D", 200);
            titleIdLabel = BannerInfo("Title ID: N/D", 280);
            patchLabel = BannerInfo("Patch: N/D", 480);
            preflightLabel = BannerInfo("Preflight: not run", 570);
            banner.Controls.AddRange(new Control[] { firmwareLabel, titleIdLabel, patchLabel, preflightLabel });
            AddCredits(banner);
            Controls.Add(banner);

            Panel toolbar = new Panel { Location = new Point(0, 36), Size = new Size(1000, 74), BackColor = Theme.Bg };
            toolbar.Paint += (s, e) =>
            {
                using (Pen line = new Pen(Theme.Line))
                    e.Graphics.DrawLine(line, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };

            connectionGroup = new Panel { Location = new Point(8, 4), Size = new Size(984, 32), BackColor = Theme.Bg };
            connectionGroup.Controls.Add(NewLabel("PS4 IP", 0, 9));
            ipTextBox = new TextBox { Location = new Point(46, 6), Size = new Size(120, 21) };
            connectionGroup.Controls.Add(ipTextBox);
            connectionGroup.Controls.Add(NewLabel("Payload", 178, 9));
            payloadPortTextBox = new TextBox { Location = new Point(226, 6), Size = new Size(46, 21), Text = "9090" };
            connectionGroup.Controls.Add(payloadPortTextBox);
            connectionGroup.Controls.Add(NewLabel("FTP", 284, 9));
            ftpPortTextBox = new TextBox { Location = new Point(308, 6), Size = new Size(46, 21), Text = "2121" };
            connectionGroup.Controls.Add(ftpPortTextBox);

            connectPs4Button = new TileButton { Text = "1. CONNECT PS4", Location = new Point(372, 4), Size = new Size(180, 24), Primary = true, Centered = true };
            connectPs4Button.Click += ConnectPs4Button_Click;
            connectGameButton = new TileButton { Text = "2. CONNECT GTAV", Location = new Point(558, 4), Size = new Size(180, 24), Primary = true, Centered = true, Enabled = false };
            connectGameButton.Click += ConnectGameButton_Click;
            connectionGroup.Controls.AddRange(new Control[] { connectPs4Button, connectGameButton });

            showLogCheckBox = new CheckBox { Text = "Show log", Location = new Point(880, 7), Size = new Size(100, 20), Checked = LoadLogPreference() };
            showLogCheckBox.CheckedChanged += (s, e) => ApplyLogVisibility();
            connectionGroup.Controls.Add(showLogCheckBox);
            toolbar.Controls.Add(connectionGroup);

            characterGroup = new Panel { Location = new Point(8, 38), Size = new Size(984, 32), BackColor = Theme.Bg, Enabled = false };
            characterGroup.Controls.Add(NewLabel("Stats character", 0, 9));
            Panel characterPanel = new Panel { Location = new Point(96, 4), Size = new Size(244, 26), BackColor = Theme.Bg };
            character1Button = Toggle("Character 1", 0, 0, 120);
            character2Button = Toggle("Character 2", 124, 0, 120);
            characterPanel.Controls.AddRange(new Control[] { character1Button, character2Button });
            characterGroup.Controls.Add(characterPanel);

            saveCheckBox = new CheckBox { Text = "Save after write", Location = new Point(480, 7), Size = new Size(130, 20), Checked = true };
            verifyCheckBox = new CheckBox { Text = "Verify read-back (slow)", Location = new Point(616, 7), Size = new Size(150, 20) };
            confirmCheckBox = new CheckBox { Text = "Ask before applying", Location = new Point(772, 7), Size = new Size(150, 20), Checked = true };
            characterGroup.Controls.AddRange(new Control[] { saveCheckBox, verifyCheckBox, confirmCheckBox });
            toolbar.Controls.Add(characterGroup);
            Controls.Add(toolbar);

            tabs = new ThemedTabs { Location = new Point(8, 114), Size = new Size(984, PanelHeight + TabHeaderHeight) };
            outfitsPanel = new OutfitsPanel(this);
            debugPanel = new DebugPanel(this);
            tabs.AddPage(outfitsPanel);
            tabs.AddPage(debugPanel);
            Controls.Add(tabs);

            lockLabel = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold), BackColor = Theme.Bg
            };
            lockOverlay = new Panel { Location = new Point(8, 114 + TabHeaderHeight), Size = new Size(984, PanelHeight), BackColor = Theme.Bg, Tag = "keep" };
            lockOverlay.Controls.Add(lockLabel);
            Controls.Add(lockOverlay);
            lockOverlay.BringToFront();

            progressLabel = new Label { Text = "Idle", Location = new Point(8, 814), Size = new Size(984, 14), ForeColor = Theme.Muted };
            progressBar = new ProgressStrip { Location = new Point(8, 832), Size = new Size(862, 6) };
            cancelButton = new TileButton { Text = "Cancel", Location = new Point(878, 823), Size = new Size(114, 22), Centered = true, Enabled = false };
            cancelButton.Click += (s, e) => cancellation?.Cancel();
            Controls.AddRange(new Control[] { progressLabel, progressBar, cancelButton });

            logTextBox = new TextBox
            {
                Location = new Point(8, 852), Size = new Size(984, 62),
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = Theme.Mono, BackColor = Color.FromArgb(11, 11, 11), ForeColor = Color.FromArgb(190, 190, 190),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(logTextBox);
            ApplyLogVisibility(false);

            StatusStrip strip = new StatusStrip { BackColor = Theme.Bg };
            statusLabel = new ToolStripStatusLabel("STATUS: Idle") { ForeColor = Theme.Text };
            strip.Items.Add(statusLabel);
            Controls.Add(strip);

            FormClosing += (sender, args) =>
            {
                cancellation?.Cancel();
                try { connection.Disconnect(); } catch { }
            };
        }

        /// <summary>"by yAmethxst" with links to X, Discord and the credits file, right-aligned in the banner.</summary>
        private void AddCredits(Panel banner)
        {
            const int right = 990;
            const int gap = 10;
            (string Text, Action Open)[] items =
            {
                ("Credits", OpenCreditsFile),
                ("Discord", () => LinkOpener.Open(DiscordUrl, Log)),
                ("X", () => LinkOpener.Open(XUrl, Log))
            };

            int x = right;
            foreach ((string text, Action open) in items)
            {
                LinkLabel link = new LinkLabel
                {
                    Text = text, AutoSize = true, LinkColor = Theme.Yellow, ActiveLinkColor = Theme.YellowHover,
                    VisitedLinkColor = Theme.Yellow, LinkBehavior = LinkBehavior.HoverUnderline, BackColor = banner.BackColor,
                    Cursor = Cursors.Hand
                };
                link.Location = new Point(0, 11);
                banner.Controls.Add(link);
                x -= link.PreferredWidth + gap;
                link.Left = x;
                link.LinkClicked += (s, e) => open();
            }

            Label author = new Label
            {
                Text = "by yAmethxst", AutoSize = true, ForeColor = Theme.Text, BackColor = banner.BackColor,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
            };
            author.Location = new Point(0, 11);
            banner.Controls.Add(author);
            author.Left = x - author.PreferredWidth - gap;
        }

        private void OpenCreditsFile()
        {
            string path = Path.Combine(baseDirectory, "CREDITS.txt");
            if (!File.Exists(path))
            {
                MessageBox.Show("CREDITS.txt was not found next to the tool.", "Credits", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LinkOpener.Open(path, Log);
        }

        private RadioButton Toggle(string text, int x, int y, int width)
        {
            RadioButton button = new RadioButton { Text = text, Location = new Point(x, y), Size = new Size(width, 24) };
            button.CheckedChanged += (s, e) => UpdateGate();
            return button;
        }

        private static Label NewLabel(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Theme.Muted };
        }

        private static Label BannerInfo(string text, int x)
        {
            return new Label { Text = text, Location = new Point(x, 11), AutoSize = true, ForeColor = Theme.Muted };
        }

        private static Color StatusColor(Color requested)
        {
            if (requested == Color.Green) return Theme.Ok;
            if (requested == Color.Red) return Theme.Bad;
            if (requested == Color.DarkOrange) return Theme.Yellow;
            return Theme.Text;
        }

        private void InitializeEngine()
        {
            try
            {
                profile = GameProfile.Load(Path.Combine(baseDirectory, "profile.ini"), TargetGameVersion);
                engine = new StatEngine(connection, profile, new SignatureStore(Path.Combine(baseDirectory, "signatures.ini")), Log);

                bool hashOk = JenkinsHash.Joaat("adder") == 0xB779A091;
                Log(hashOk ? "Hash self-test OK." : "Hash self-test FAILED.");
                if (!hashOk)
                    throw new InvalidOperationException("Hash self-test failed.");

                Log($"Profile {profile.GameVersion} loaded (address base 0x{profile.AddressBase:X}, {profile.Handlers.Count} handlers).");

                try
                {
                    OutfitOffsets.Load(TargetGameVersion);
                }
                catch (Exception ex)
                {
                    outfitsPanel.Enabled = false;
                    Log("Outfits disabled: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                connectPs4Button.Enabled = false;
                SetStatus("Configuration error", Color.Red);
                MessageBox.Show($"Configuration error:\n{ex.Message}", "Arise - RTM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---- connection -----------------------------------------------------------------

        private async void ConnectPs4Button_Click(object sender, EventArgs e)
        {
            if (connection.IsConnected)
            {
                ResetSession("Disconnected.");
                return;
            }

            if (TryReadPorts(out int payloadPort, out int ftpPort))
                await ConnectToPs4Async(payloadPort);
        }

        private async void ConnectGameButton_Click(object sender, EventArgs e)
        {
            if (TryReadPorts(out int payloadPort, out int ftpPort))
                await ConnectToGameAsync(payloadPort, ftpPort);
        }

        private async Task ConnectToPs4Async(int payloadPort)
        {
            string ip = ipTextBox.Text.Trim();
            if (ip.Length == 0)
            {
                MessageBox.Show("Enter the PS4 IP address.", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetConnectionBusy(true, "CHECKING...");
            try
            {
                TimedResult<bool> debugCheck = await RunWithTimeoutAsync(() => connection.IsDebugActive(ip), ConnectionTimeoutMs);
                if (debugCheck.TimedOut)
                {
                    FailPs4("Debug check timeout");
                    return;
                }

                if (!debugCheck.Value)
                {
                    connectPs4Button.Text = "INJECTING...";
                    string payloadPath = Path.Combine(baseDirectory, "ps4debug.bin");
                    TimedResult<bool> payload = await RunWithTimeoutAsync(
                        () => connection.InjectPayload(ip, payloadPath, payloadPort), ConnectionTimeoutMs);

                    if (payload.TimedOut || !payload.Value)
                    {
                        MessageBox.Show("Enable \"PayLoader Server\" in the GoldHEN settings (port 9090).",
                            "Payload Not Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        FailPs4("Payload unreachable");
                        return;
                    }

                    await Task.Delay(1200);
                }

                connectPs4Button.Text = "CONNECTING...";
                TimedResult<bool> connect = await RunWithTimeoutAsync(() =>
                {
                    connection.Connect(ip);
                    return connection.IsConnected;
                }, ConnectionTimeoutMs);

                if (connect.TimedOut || !connect.Value)
                {
                    FailPs4("PS4 connection timeout");
                    return;
                }

                connection.Notify("Arise - RTM: Connected to PS4!");
                firmwareLabel.Text = $"FW: {connection.GetConsoleFirmwareText()}";
                SetStatus("Connected to PS4 - now connect to GTAV", Color.Green);
                Log($"Connected to {ip}.");
            }
            catch (Exception ex)
            {
                FailPs4("PS4 connection error");
                Log("ERROR: " + ex.Message);
            }
            finally
            {
                SetConnectionBusy(false);
            }
        }

        private async Task ConnectToGameAsync(int payloadPort, int ftpPort)
        {
            string ip = ipTextBox.Text.Trim();
            SetConnectionBusy(true, null, "CONNECTING GTAV...");
            try
            {
                int pid = await Task.Run(() => connection.FindProcessID(GameProcessName));
                if (pid <= 0)
                {
                    SetStatus("GTAV not found - start the game", Color.DarkOrange);
                    MessageBox.Show("eboot.bin not found. Start the game.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string titleId = "N/D";
                if (connection.TryGetProcessInfo(pid, out ProcessInfo processInfo))
                    titleId = CleanValue(processInfo.titleid);

                titleIdLabel.Text = $"Title ID: {titleId} ({RegionOf(titleId)})";
                if (!profile.AllowedTitleIds.Contains(titleId))
                {
                    FailGame($"Title ID {titleId} is not supported by this tool.");
                    return;
                }

                string patch = await DetectPatchAsync(ip, titleId, ftpPort);
                if (patch.Length == 0)
                {
                    FailGame("Game version not confirmed.");
                    return;
                }

                patchLabel.Text = $"Patch: {patch}";
                if (patch != profile.GameVersion)
                {
                    FailGame($"Game version {patch} is not supported (expected {profile.GameVersion}).");
                    return;
                }

                preflightLabel.Text = "Preflight: running...";
                PreflightResult preflight = await Task.Run(() => RunPreflight(pid, titleId));
                if (!preflight.Passed)
                {
                    preflightLabel.Text = "Preflight: FAILED";
                    Log("PREFLIGHT FAILED: " + preflight.Failure);
                    MessageBox.Show(preflight.Failure, "Preflight failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    FailGame("Preflight failed");
                    return;
                }

                gamePid = pid;
                gameReady = true;
                preflightLabel.Text = "Preflight: OK";
                SaveConnectionInfo(ip, payloadPort, ftpPort);
                outfitsPanel?.Attach(pid);

                try { connection.Notify("Arise - RTM: GTAV connected"); } catch { }

                bool coreVerified = HandlerKeys.Core.All(engine.IsVerified);
                string verified = coreVerified ? "core verified" : "core UNVERIFIED (first run on this build)";
                SetStatus($"GTA V found (PID {pid}) - {verified}.", coreVerified ? Color.Green : Color.DarkOrange);
                Log($"GTA V {patch} {titleId} PID {pid}: preflight passed, {verified}.");
            }
            catch (Exception ex)
            {
                FailGame("GTAV lookup error");
                Log("ERROR: " + ex.Message);
            }
            finally
            {
                SetConnectionBusy(false);
                UpdateGate();
            }
        }

        private async Task<string> DetectPatchAsync(string ip, string titleId, int ftpPort)
        {
            TimedResult<string> ftp = await RunWithTimeoutAsync(() =>
                connection.TryGetGamePatchVersionFromFtp(ip, titleId, out string appVersion, out _, out _, out _, ftpPort)
                    ? appVersion
                    : string.Empty,
                ConnectionTimeoutMs);

            string patch = ftp.TimedOut ? string.Empty : NormalizeVersion(ftp.Value);
            if (patch.Length > 0)
            {
                Log($"Game version read from param.sfo: {patch}.");
                return patch;
            }

            DialogResult answer = MessageBox.Show(
                $"The game version could not be read via FTP.\nIs this GTA V {profile.GameVersion}?",
                "Game version", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
                return string.Empty;

            Log($"Game version confirmed manually: {profile.GameVersion}.");
            return profile.GameVersion;
        }

        private PreflightResult RunPreflight(int pid, string titleId)
        {
            try
            {
                bool passed = engine.Preflight(pid, titleId, out string failure);
                return new PreflightResult { Passed = passed, Failure = failure };
            }
            catch (Exception ex)
            {
                return new PreflightResult { Passed = false, Failure = ex.Message };
            }
        }

        private void FailPs4(string status)
        {
            ResetSession(status, Color.Red);
        }

        private void FailGame(string status)
        {
            ResetGameState();
            SetStatus(status, Color.Red);
            Log(status);
        }

        private void ResetSession(string status, Color? color = null)
        {
            try { connection.Disconnect(); } catch { }
            ResetGameState();
            firmwareLabel.Text = "FW: N/D";
            SetStatus(status, color ?? Color.Black);
            Log(status);
            SetConnectionBusy(false);
        }

        private void ResetGameState()
        {
            engine?.Reset();
            gameReady = false;
            gamePid = -1;
            outfitsPanel?.Detach();
            titleIdLabel.Text = "Title ID: N/D";
            patchLabel.Text = "Patch: N/D";
            preflightLabel.Text = "Preflight: not run";
            UpdateGate();
        }

        private void SetConnectionBusy(bool working, string ps4Text = null, string gameText = null)
        {
            connectPs4Button.Enabled = !working;
            connectGameButton.Enabled = !working && connection.IsConnected;

            if (ps4Text != null)
                connectPs4Button.Text = ps4Text;
            if (gameText != null)
                connectGameButton.Text = gameText;

            if (!working)
            {
                connectPs4Button.Text = connection.IsConnected ? "DISCONNECT PS4" : "1. CONNECT PS4";
                connectGameButton.Text = gameReady ? "GTAV CONNECTED (reconnect)" : "2. CONNECT GTAV";
                connectGameButton.Enabled = connection.IsConnected;
            }
        }

        // ---- small helpers --------------------------------------------------------------

        private void SetStatus(string text, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetStatus(text, color)));
                return;
            }

            statusLabel.Text = "STATUS: " + text;
            statusLabel.ForeColor = StatusColor(color);
        }

        private void Log(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }

            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            logTextBox.AppendText(line + Environment.NewLine);

            try { File.AppendAllText(Path.Combine(baseDirectory, "rtm.log"), line + Environment.NewLine); }
            catch { }
        }

        private bool TryReadPorts(out int payloadPort, out int ftpPort)
        {
            ftpPort = 2121;
            if (!int.TryParse(payloadPortTextBox.Text.Trim(), out payloadPort) || payloadPort < 1 || payloadPort > 65535)
            {
                MessageBox.Show("Invalid payload port.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!int.TryParse(ftpPortTextBox.Text.Trim(), out ftpPort) || ftpPort < 1 || ftpPort > 65535)
            {
                MessageBox.Show("Invalid FTP port.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private string UiPreferencePath => Path.Combine(baseDirectory, "ui.txt");

        private bool LoadLogPreference()
        {
            try
            {
                return !File.Exists(UiPreferencePath) || File.ReadAllText(UiPreferencePath).Trim() != "log=0";
            }
            catch
            {
                return true;
            }
        }

        /// <summary>Hides or shows the log pane (the window shrinks or grows with it). The log is still written to rtm.log.</summary>
        private void ApplyLogVisibility(bool save = true)
        {
            bool show = showLogCheckBox.Checked;
            logTextBox.Visible = show;
            ClientSize = new Size(ClientSize.Width, show ? WindowHeightWithLog : WindowHeightWithoutLog);

            if (!save)
                return;

            try { File.WriteAllText(UiPreferencePath, show ? "log=1" : "log=0"); }
            catch { }
        }

        private void LoadConnectionInfo()
        {
            try
            {
                string path = Path.Combine(baseDirectory, "connection.txt");
                if (!File.Exists(path))
                    return;

                string[] parts = File.ReadAllText(path).Trim().Split(':');
                if (parts.Length >= 1) ipTextBox.Text = parts[0];
                if (parts.Length >= 2) payloadPortTextBox.Text = parts[1];
                if (parts.Length >= 3) ftpPortTextBox.Text = parts[2];
            }
            catch
            {
            }
        }

        private void SaveConnectionInfo(string ip, int payloadPort, int ftpPort)
        {
            try
            {
                File.WriteAllText(Path.Combine(baseDirectory, "connection.txt"), $"{ip}:{payloadPort}:{ftpPort}", Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static async Task<TimedResult<T>> RunWithTimeoutAsync<T>(Func<T> operation, int timeoutMs)
        {
            Task<T> worker = Task.Run(operation);
            Task completed = await Task.WhenAny(worker, Task.Delay(timeoutMs));

            if (completed == worker)
                return new TimedResult<T> { TimedOut = false, Value = await worker };

            return new TimedResult<T> { TimedOut = true, Value = default(T) };
        }

        private static string CleanValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "N/D" : value.Trim().TrimEnd('\0');
        }

        private static string NormalizeVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string[] parts = raw.Trim().Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                return $"{major}.{minor:00}";

            return raw.Trim();
        }

        private static string RegionOf(string titleId)
        {
            switch (titleId.ToUpperInvariant())
            {
                case "CUSA00419": return "US";
                case "CUSA00411": return "EU";
                case "CUSA00880": return "JP";
                default: return "UNKNOWN";
            }
        }
    }
}

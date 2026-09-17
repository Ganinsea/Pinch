using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ImageCompressorFloat
{
    internal sealed class WorkItem : IDisposable
    {
        public string FilePath;
        public Bitmap Bitmap;
        public string DesiredPath;
        public string OutputDirectory;
        public long TargetBytes;
        public void Dispose() { if (Bitmap != null) { Bitmap.Dispose(); Bitmap = null; } }
    }

    internal sealed class FloatForm : Form
    {
        private Color accent;
        private Color pink;
        private Color ink;
        private PinchSettings settings;
        private readonly Panel titleBar;
        private readonly Label title;
        private readonly CloseButton close;
        private readonly ToolStripMenuItem top;
        private SettingsForm settingsForm;
        private bool showingResult;
        private readonly Label statusLabel;
        private readonly NotifyIcon tray;
        private readonly Icon appIcon;
        private readonly ContextMenuStrip menu;
        private readonly ToolTip toolTip;
        private readonly System.Windows.Forms.Timer resetTimer;
        private readonly BackgroundWorker worker;
        private readonly Queue<WorkItem> queue = new Queue<WorkItem>();
        private readonly List<string> failures = new List<string>();
        private readonly string dataDirectory;
        private WorkItem current;
        private CompressionResult lastResult;
        private int completed;
        private int firstFrames;
        private bool exitRequested;
        private bool dragging;
        private Point dragScreenStart;
        private Point dragWindowStart;
        internal bool IsBusy { get { return current != null || worker.IsBusy || queue.Count > 0; } }
        internal string StatusText { get { return statusLabel.Text; } }
        internal bool ResetPending { get { return resetTimer.Enabled; } }
        internal int SuccessCount { get { return completed; } }
        internal int FailureCount { get { return failures.Count; } }

        public FloatForm(string dataDirectory, bool showTray)
        {
            this.dataDirectory = dataDirectory;
            settings = PinchSettings.Load(dataDirectory);
            accent = settings.TitleColor; pink = settings.Background;
            ink = PinchSettings.Readable(Color.FromArgb(18,32,51), pink);
            Text = "Pinch";
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(176, 150);
            BackColor = pink;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            Point? saved = settings.WindowPosition;
            TopMost = settings.AlwaysOnTop;
            Opacity = settings.OpacityPercent / 100d;
            StartPosition = saved.HasValue ? FormStartPosition.Manual : FormStartPosition.CenterScreen;
            if (saved.HasValue) Location = ClampToScreen(saved.Value);
            appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Icon = appIcon ?? SystemIcons.Application;

            titleBar = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = accent };
            title = new Label { Text = "Pinch", ForeColor = PinchSettings.Readable(Color.White, accent), AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold), Location = new Point(10, 5) };
            close = new CloseButton { Dock = DockStyle.Right, Width = 30, BackColor = accent,
                ForeColor = PinchSettings.Readable(Color.White, accent), Text = "\u00d7", Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
                AccessibleName = "\u9690\u85cf\u5230\u6258\u76d8" };
            close.Click += delegate { HideToTray(); };
            titleBar.Controls.Add(title);
            titleBar.Controls.Add(close);
            statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                BackColor = pink, ForeColor = ink, Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                Padding = new Padding(4), AutoEllipsis = true };
            Controls.Add(statusLabel);
            Controls.Add(titleBar);
            toolTip = new ToolTip();
            toolTip.SetToolTip(close, "\u9690\u85cf\u5230\u6258\u76d8");
            toolTip.SetToolTip(title, "Pinch " + AppInfo.Version);

            menu = new ContextMenuStrip();
            menu.Items.Add("\u663e\u793a Pinch", null, delegate { RestoreFromTray(); });
            menu.Items.Add("\u8bbe\u7f6e...", null, delegate { OpenSettings(); });
            top = new ToolStripMenuItem("\u603b\u5728\u6700\u524d") { Checked = TopMost, CheckOnClick = true };
            top.Click += delegate { TopMost = top.Checked; SaveSettings(); };
            menu.Items.Add(top);
            menu.Items.Add("\u6253\u5f00\u6700\u8fd1\u8f93\u51fa", null, delegate {
                if (lastResult != null && File.Exists(lastResult.OutputPath)) OpenPath(Path.GetDirectoryName(lastResult.OutputPath));
            });
            menu.Items.Add("\u67e5\u770b\u5931\u8d25\u8bb0\u5f55", null, delegate {
                MessageBox.Show(failures.Count == 0 ? "\u6682\u65e0\u5931\u8d25\u8bb0\u5f55" : string.Join(Environment.NewLine, failures.Take(20)),
                    "Pinch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Items.Add("\u6253\u5f00\u65e5\u5fd7\u76ee\u5f55", null, delegate {
                try { string path = Path.Combine(dataDirectory, "logs"); Directory.CreateDirectory(path); OpenPath(path); }
                catch (Exception error) { Notify(error.Message, ToolTipIcon.Error); }
            });
            menu.Items.Add("\u5173\u4e8e Pinch", null, delegate {
                MessageBox.Show("Pinch " + AppInfo.Version + "\r\n\u5f00\u53d1\u8005\uff1aHan", "Pinch");
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("\u5f7b\u5e95\u9000\u51fa\u7a0b\u5e8f", null, delegate { RequestExit(); });
            ContextMenuStrip = menu;
            tray = new NotifyIcon { Icon = Icon, Text = "Pinch " + AppInfo.Version, ContextMenuStrip = menu, Visible = showTray };
            tray.DoubleClick += delegate { RestoreFromTray(); };

            resetTimer = new System.Windows.Forms.Timer { Interval = settings.ResetSeconds * 1000 };
            resetTimer.Tick += delegate { resetTimer.Stop(); if (!IsBusy) ResetHint(); };
            worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) {
                using (WorkItem item = (WorkItem)e.Argument)
                    e.Result = item.Bitmap == null ? ImageCompressor.CompressFile(item.FilePath, item.TargetBytes, item.OutputDirectory, CancellationToken.None)
                        : ImageCompressor.Compress(item.Bitmap, item.DesiredPath, item.TargetBytes, CancellationToken.None);
            };
            worker.RunWorkerCompleted += WorkCompleted;
            foreach (Control control in new Control[] { this, titleBar, title, close, statusLabel })
            {
                AttachDrop(control);
                if (control != close) AttachMove(control);
            }
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing)
                { exitRequested = true; ClearQueue(); SaveSettings(); return; }
                if (!exitRequested) { e.Cancel = true; HideToTray(); }
                else if (worker.IsBusy || current != null) { e.Cancel = true; }
            };
            ResetHint();
        }

        private void AttachDrop(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += delegate(object sender, DragEventArgs e) {
                e.Effect = !exitRequested && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            };
            control.DragDrop += delegate(object sender, DragEventArgs e) {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null) EnqueueFiles(files);
            };
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keys)
        {
            if (keys == (Keys.Control | Keys.V))
            {
                if (!exitRequested)
                {
                    try { AcceptClipboard(Clipboard.GetDataObject()); }
                    catch (System.Runtime.InteropServices.ExternalException) { Notify("\u526a\u8d34\u677f\u6b63\u5fd9\uff0c\u8bf7\u91cd\u8bd5", ToolTipIcon.Info); }
                    catch (Exception error) { RuntimeState.Log(dataDirectory, "clipboard: " + error); Notify(error.Message, ToolTipIcon.Error); }
                }
                return true;
            }
            return base.ProcessCmdKey(ref message, keys);
        }

        internal void AcceptClipboard(IDataObject data)
        {
            if (data == null || exitRequested) return;
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0 && File.Exists(files[0]) && ImageCompressor.Supports(files[0]))
                    EnqueueFiles(new[] { files[0] });
                return;
            }
            if (data.GetDataPresent(DataFormats.Bitmap, true))
            {
                using (Image image = data.GetData(DataFormats.Bitmap, true) as Image)
                {
                    if (image != null)
                    {
                        string output = Path.Combine(settings.ResolveOutputDirectory(null),
                            "Pinch_clipboard_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "ys.jpg");
                        EnqueueBitmap(new Bitmap(image), output);
                    }
                }
            }
        }

        internal void EnqueueFiles(IEnumerable<string> files)
        {
            if (exitRequested) return;
            BeginBatch();
            foreach (string file in files) queue.Enqueue(new WorkItem { FilePath = file, TargetBytes = settings.MaxOutputBytes,
                OutputDirectory = settings.Location == SaveLocation.Source ? null : settings.ResolveOutputDirectory(file) });
            StartNext();
        }

        internal void EnqueueBitmap(Bitmap ownedBitmap, string desiredPath)
        {
            if (exitRequested) { ownedBitmap.Dispose(); return; }
            BeginBatch();
            queue.Enqueue(new WorkItem { Bitmap = ownedBitmap, DesiredPath = desiredPath, TargetBytes = settings.MaxOutputBytes });
            StartNext();
        }

        private void BeginBatch()
        {
            resetTimer.Stop();
            showingResult = false;
            if (!IsBusy) { completed = 0; firstFrames = 0; failures.Clear(); }
        }

        private void StartNext()
        {
            if (worker.IsBusy || current != null || exitRequested || queue.Count == 0) return;
            current = queue.Dequeue();
            SetStatus("\u538b\u7f29\u4e2d..." + (queue.Count > 0 ? "\r\n\u7b49\u5f85 " + queue.Count + " \u5f20" : ""), accent);
            toolTip.SetToolTip(statusLabel, current.FilePath ?? "\u526a\u8d34\u677f\u56fe\u7247");
            worker.RunWorkerAsync(current);
        }

        private void WorkCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => WorkCompleted(sender, e)));
                return;
            }
            if (e.Error != null)
            {
                string detail = (current.FilePath ?? "Clipboard") + ": " + e.Error.Message;
                failures.Add(detail);
                RuntimeState.Log(dataDirectory, "FAILED " + detail);
            }
            else
            {
                lastResult = (CompressionResult)e.Result;
                completed++;
                if (lastResult.FirstFrameOnly) firstFrames++;
                RuntimeState.Log(dataDirectory, "OK " + lastResult.OutputPath + " bytes=" + lastResult.Bytes);
            }
            if (exitRequested) { current = null; Close(); return; }
            if (queue.Count > 0) { current = null; StartNext(); return; }
            string summary;
            if (completed == 1 && failures.Count == 0)
                summary = "\u5b8c\u6210\uff1a" + (lastResult.Bytes / 1000000d).ToString("0.00") + " MB";
            else summary = "\u5b8c\u6210\uff1a" + completed + " \u5f20" + (failures.Count > 0 ? "\r\n\u5931\u8d25\uff1a" + failures.Count + " \u5f20" : "");
            SetStatus(summary, failures.Count > 0 ? Color.FromArgb(164, 39, 66) : Color.FromArgb(21, 128, 61));
            toolTip.SetToolTip(statusLabel, lastResult == null ? summary : lastResult.OutputPath);
            Notify(summary + (firstFrames > 0 ? "\r\n\u52a8\u56fe/\u591a\u9875\u56fe\u7247\u5df2\u4fdd\u5b58\u7b2c\u4e00\u5e27" : ""), failures.Count > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info);
            resetTimer.Start();
            showingResult = true;
            current = null;
        }

        internal void RequestExit()
        {
            if (exitRequested) return;
            exitRequested = true;
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Close();
            resetTimer.Stop();
            ClearQueue();
            SaveSettings();
            if (worker.IsBusy || current != null) SetStatus("\u5b8c\u6210\u5f53\u524d\u56fe\u7247\u540e\u9000\u51fa", accent);
            else Close();
        }

        private void ClearQueue() { while (queue.Count > 0) queue.Dequeue().Dispose(); }
        private void SetStatus(string text, Color color) { resetTimer.Stop(); statusLabel.Text = text; statusLabel.ForeColor = PinchSettings.Readable(color, pink); }
        private void ResetHint() { showingResult = false; SetStatus("\u62d6\u5165\u56fe\u7247\r\n\u81ea\u52a8\u538b\u7f29\u5230 " + (settings.MaxOutputBytes / 1000000d).ToString("0.##") + "MB \u5185", ink); toolTip.SetToolTip(statusLabel, ""); }
        private void Notify(string text, ToolTipIcon icon) { if (tray.Visible && settings.Notifications) tray.ShowBalloonTip(2600, "Pinch", text, icon); }
        private void OpenPath(string path) { try { Process.Start(path); } catch (Exception error) { Notify(error.Message, ToolTipIcon.Error); } }
        private void HideToTray() { SaveSettings(); Hide(); }
        internal void RestoreFromTray() { if (!exitRequested) { Location = ClampToScreen(Location); Show(); WindowState = FormWindowState.Normal; Activate(); } }
        private void SaveSettings()
        {
            try { settings.WindowPosition = Location; settings.AlwaysOnTop = TopMost; settings.Save(dataDirectory); }
            catch (Exception error) { RuntimeState.Log(dataDirectory, "settings-save: " + error.Message); }
        }
        internal PinchSettings CurrentSettings { get { return settings.Copy(); } }
        internal void ApplySettings(PinchSettings value)
        {
            PinchSettings updated = value.Copy(); updated.Validate();
            if (updated.Location == SaveLocation.Custom)
            {
                Directory.CreateDirectory(updated.CustomDirectory);
                string probe = Path.Combine(updated.CustomDirectory, ".pinch-access-" + Guid.NewGuid().ToString("N") + ".tmp");
                try { using (FileStream file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { } }
                finally { if (File.Exists(probe)) File.Delete(probe); }
            }
            updated.WindowPosition = Location; updated.Save(dataDirectory); settings = updated;
            accent = settings.TitleColor; pink = settings.Background; ink = PinchSettings.Readable(Color.FromArgb(18,32,51),pink);
            BackColor = statusLabel.BackColor = pink; titleBar.BackColor = close.BackColor = accent;
            title.ForeColor = close.ForeColor = PinchSettings.Readable(Color.White,accent);
            Opacity = settings.OpacityPercent / 100d; TopMost = settings.AlwaysOnTop; top.Checked = TopMost;
            resetTimer.Interval = settings.ResetSeconds * 1000;
            if (!IsBusy && !showingResult) ResetHint();
            else statusLabel.ForeColor = PinchSettings.Readable(statusLabel.ForeColor,pink);
            if (!IsBusy && showingResult) { resetTimer.Stop(); resetTimer.Start(); }
            Invalidate(true);
        }
        internal void OpenSettings()
        {
            if (exitRequested) return;
            if (settingsForm != null && !settingsForm.IsDisposed) { settingsForm.Activate(); return; }
            settingsForm = new SettingsForm(settings, ApplySettings) { Icon = Icon, TopMost = TopMost };
            settingsForm.FormClosed += delegate { settingsForm = null; };
            settingsForm.Show(this);
        }
        private Point ClampToScreen(Point point)
        {
            Rectangle area = Screen.FromPoint(point).WorkingArea;
            return new Point(Math.Max(area.Left, Math.Min(point.X, area.Right - Width)), Math.Max(area.Top, Math.Min(point.Y, area.Bottom - Height)));
        }
        private void AttachMove(Control control)
        {
            control.MouseDown += delegate(object sender, MouseEventArgs e) {
                if (e.Button != MouseButtons.Left) return;
                Activate(); Focus();
                dragScreenStart = control.PointToScreen(e.Location); dragWindowStart = Location;
                dragging = true; control.Capture = true;
            };
            control.MouseMove += delegate(object sender, MouseEventArgs e) {
                if (!dragging) return;
                Point now = control.PointToScreen(e.Location);
                Location = new Point(dragWindowStart.X + now.X - dragScreenStart.X, dragWindowStart.Y + now.Y - dragScreenStart.Y);
            };
            control.MouseUp += delegate { dragging = false; control.Capture = false; SaveSettings(); };
            control.MouseCaptureChanged += delegate { dragging = false; };
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearQueue();
                if (settingsForm != null) settingsForm.Dispose();
                if (resetTimer != null) resetTimer.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                if (worker != null) worker.Dispose();
                if (menu != null) menu.Dispose();
                if (toolTip != null) toolTip.Dispose();
                if (appIcon != null) appIcon.Dispose();
            }
            base.Dispose(disposing);
        }
        private sealed class CloseButton : Button
        {
            public CloseButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; TabStop = false; Cursor = Cursors.Hand; }
            protected override bool ShowFocusCues { get { return false; } }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }
}

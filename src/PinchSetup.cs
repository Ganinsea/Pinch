using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ImageCompressorFloat;

namespace PinchSetup
{
    internal static class SetupProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Contains("--silent"))
            {
                try { Install(); return 0; }
                catch (Exception error) { Console.Error.WriteLine(error.Message); RuntimeState.Log(RuntimeState.DataDirectory, "setup: " + error); return 1; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (SetupForm form = new SetupForm()) Application.Run(form);
            return 0;
        }
        private static void Install()
        {
            using (Mutex guard = new Mutex(false, RuntimeState.InstancePrefix + ".installation"))
            {
                bool held;
                try { held = guard.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
                if (!held) throw new InvalidOperationException("Another Pinch installation is already running.");
                try { InstallerCore.Install(InstallerCore.DefaultLayout(), InstallerCore.Resource("Pinch.exe"),
                    InstallerCore.Resource("PinchUninstall.exe"), InstallerCore.Resource("PinchGuide.md"), null); }
                finally { guard.ReleaseMutex(); }
            }
        }
        private sealed class SetupForm : Form
        {
            private readonly BackgroundWorker worker = new BackgroundWorker();
            private readonly Label status;
            private readonly Button install;
            private readonly Button launch;
            private readonly Icon appIcon;
            public SetupForm()
            {
                Text = "Pinch " + AppInfo.Version + " \u5b89\u88c5";
                AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
                ClientSize = new Size(380, 262); FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
                BackColor = Color.FromArgb(255, 229, 235);
                Font = new Font("Microsoft YaHei UI", 9f);
                appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Icon = appIcon;
                TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18,12,18,12) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                layout.Controls.Add(new Label { Text = "Pinch", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Microsoft YaHei UI", 22, FontStyle.Bold), ForeColor = Color.FromArgb(216,82,116) }, 0, 0);
                layout.Controls.Add(new Label { Text = "V" + AppInfo.Version + "\r\n\u5f00\u53d1\u8005\uff1aHan", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
                status = new Label { Text = "\u51c6\u5907\u5b89\u88c5", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
                layout.Controls.Add(status, 0, 2);
                FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0,6,0,0) };
                install = new Button { Text = "\u5b89\u88c5", Size = new Size(90,30), FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(216,82,116), ForeColor = Color.White };
                install.FlatAppearance.BorderSize = 0;
                launch = new Button { Text = "\u542f\u52a8 Pinch", Size = new Size(100,30), Enabled = false };
                Button close = new Button { Text = "\u5173\u95ed", Size = new Size(74,30) };
                buttons.Controls.Add(install); buttons.Controls.Add(launch); buttons.Controls.Add(close);
                layout.Controls.Add(buttons,0,3); Controls.Add(layout);
                install.Click += delegate { install.Enabled = false; launch.Enabled = false; status.Text = "\u6b63\u5728\u5b89\u88c5..."; worker.RunWorkerAsync(); };
                worker.DoWork += delegate { Install(); };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                    install.Enabled = true;
                    if (e.Error != null) { status.Text = "\u5b89\u88c5\u5931\u8d25"; RuntimeState.Log(RuntimeState.DataDirectory, e.Error.ToString()); MessageBox.Show(this, e.Error.Message, "Pinch", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    else { status.Text = "\u5b89\u88c5\u5b8c\u6210"; install.Text = "\u91cd\u65b0\u5b89\u88c5"; launch.Enabled = true; }
                };
                launch.Click += delegate {
                    try { Process.Start(System.IO.Path.Combine(RuntimeState.InstallDirectory, "Pinch.exe")); Close(); }
                    catch (Exception error) { MessageBox.Show(this, error.Message, "Pinch"); }
                };
                close.Click += delegate { Close(); };
                FormClosing += delegate(object sender, FormClosingEventArgs e) { if (worker.IsBusy) { e.Cancel = true; status.Text = "\u6b63\u5728\u5b89\u88c5\uff0c\u8bf7\u7a0d\u5019"; } };
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) { worker.Dispose(); if (appIcon != null) appIcon.Dispose(); }
                base.Dispose(disposing);
            }
        }
    }
}

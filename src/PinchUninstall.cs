using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ImageCompressorFloat;

namespace PinchSetup
{
    internal static class UninstallProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool quiet = args.Contains("--quiet");
            try
            {
                InstallLayout layout = InstallerCore.DefaultLayout();
                if (args.Length > 1 && args[0] == "--remove")
                {
                    int pid;
                    if (!int.TryParse(args[1], out pid)) return 1;
                    try
                    {
                        using (Process parent = Process.GetProcessById(pid))
                        {
                            if (!string.Equals(parent.MainModule.FileName, Path.Combine(layout.Root, "PinchUninstall.exe"), StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("Uninstall parent identity mismatch.");
                            if (!parent.WaitForExit(15000)) throw new IOException("Uninstaller is still running.");
                        }
                    }
                    catch (ArgumentException) { }
                    using (Mutex guard = new Mutex(false, RuntimeState.InstancePrefix + ".installation"))
                    {
                        bool held;
                        try { held = guard.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
                        if (!held) throw new IOException("Pinch setup is running.");
                        try { InstallerCore.Uninstall(layout); } finally { guard.ReleaseMutex(); }
                    }
                    if (!quiet) MessageBox.Show("Pinch \u5df2\u5378\u8f7d\uff0c\u56fe\u7247\u548c\u7528\u6237\u6570\u636e\u5df2\u4fdd\u7559\u3002", "Pinch");
                    return 0;
                }
                if (!quiet && MessageBox.Show("\u786e\u5b9a\u5378\u8f7d Pinch\uff1f\r\n\u4fdd\u7559\u5df2\u751f\u6210\u7684\u56fe\u7247\u548c\u7528\u6237\u6570\u636e\u3002", "Pinch", MessageBoxButtons.OKCancel) != DialogResult.OK) return 0;
                if (!string.Equals(Application.ExecutablePath, Path.Combine(layout.Root, "PinchUninstall.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Run the installed Pinch uninstaller.");
                string copy = Path.Combine(Path.GetTempPath(), "PinchUninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(Application.ExecutablePath, copy);
                Process.Start(new ProcessStartInfo(copy, "--remove " + Process.GetCurrentProcess().Id + (quiet ? " --quiet" : "")) { UseShellExecute = false, CreateNoWindow = true });
                return 0;
            }
            catch (Exception error)
            {
                RuntimeState.Log(RuntimeState.DataDirectory, "uninstall: " + error);
                if (!quiet) MessageBox.Show(error.Message, "Pinch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else Console.Error.WriteLine(error.Message);
                return 1;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Win32;
using ImageCompressorFloat;

namespace PinchSetup
{
    internal sealed class InstallLayout
    {
        public readonly string Root;
        public readonly bool Integrate;
        public readonly string DesktopShortcut;
        public readonly string StartShortcut;
        public InstallLayout(string root, bool integrate)
        {
            Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(Path.GetFileName(Root), "Pinch", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Install directory must be named Pinch.");
            Integrate = integrate;
            DesktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Pinch.lnk");
            StartShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Pinch", "Pinch.lnk");
        }
    }
    internal static class InstallerCore
    {
        private const string RegistryPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Han.Pinch";
        private static readonly string[] OwnedFiles = { "Pinch.exe", "PinchUninstall.exe", "Pinch-User-Guide.txt", "Pinch.install.xml" };
        public static InstallLayout DefaultLayout() { return new InstallLayout(RuntimeState.InstallDirectory, true); }

        public static byte[] Resource(string name)
        {
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (input == null) throw new InvalidDataException("Missing installer resource: " + name);
                using (MemoryStream output = new MemoryStream()) { input.CopyTo(output); return output.ToArray(); }
            }
        }
        public static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
        private static void CheckPath(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked install paths are not supported: " + current);
                current = Path.GetDirectoryName(current);
            }
        }
        private static void ValidatePayload(byte[] bytes, string assemblyName)
        {
            string probe = Path.Combine(Path.GetTempPath(), "pinch-payload-" + Guid.NewGuid().ToString("N") + ".exe");
            try
            {
                File.WriteAllBytes(probe, bytes);
                AssemblyName actual = AssemblyName.GetAssemblyName(probe);
                if (actual.Name != assemblyName || actual.Version.ToString() != AppInfo.Version + ".0")
                    throw new InvalidDataException("Installer payload version does not match: " + assemblyName);
            }
            finally { if (File.Exists(probe)) File.Delete(probe); }
        }
        public static void Install(InstallLayout layout, byte[] application, byte[] uninstaller, byte[] guide, Action<string> checkpoint)
        {
            ValidatePayload(application, "Pinch");
            ValidatePayload(uninstaller, "PinchUninstall");
            CheckPath(layout.Root);
            string app = Path.Combine(layout.Root, "Pinch.exe");
            if (File.Exists(app) && AssemblyName.GetAssemblyName(app).Name != "Pinch")
                throw new IOException("An unrelated executable occupies the installation path.");
            StopApplication(layout);
            Directory.CreateDirectory(layout.Root);
            List<string> targets = OwnedFiles.Select(f => Path.Combine(layout.Root, f)).ToList();
            if (layout.Integrate) { targets.Add(layout.DesktopShortcut); targets.Add(layout.StartShortcut); }
            Dictionary<string, byte[]> previous = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in targets)
            {
                CheckPath(path);
                previous[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            Dictionary<string, object> oldRegistration = layout.Integrate ? ReadRegistration() : null;
            List<string> changed = new List<string>();
            bool registryTouched = false;
            try
            {
                byte[][] payloads = { application, uninstaller, guide };
                for (int i = 0; i < payloads.Length; i++)
                {
                    AtomicWrite(targets[i], payloads[i]);
                    changed.Add(targets[i]);
                    if (Hash(File.ReadAllBytes(targets[i])) != Hash(payloads[i])) throw new IOException("Installed file verification failed.");
                    if (checkpoint != null) checkpoint(OwnedFiles[i]);
                }
                XElement marker = new XElement("PinchInstallation", new XAttribute("root", layout.Root), new XAttribute("version", AppInfo.Version),
                    new XAttribute("appSha256", Hash(application)), new XAttribute("installedAt", DateTime.Now.ToString("O")));
                foreach (string name in OwnedFiles) marker.Add(new XElement("file", new XAttribute("name", name)));
                AtomicWrite(targets[3], Encoding.UTF8.GetBytes(marker.ToString()));
                changed.Add(targets[3]);
                if (layout.Integrate)
                {
                    changed.Add(layout.DesktopShortcut);
                    CreateShortcut(layout.DesktopShortcut, app, layout.Root);
                    changed.Add(layout.StartShortcut);
                    CreateShortcut(layout.StartShortcut, app, layout.Root);
                    registryTouched = true;
                    Register(layout);
                }
            }
            catch (Exception failure)
            {
                List<string> rollbackErrors = new List<string>();
                foreach (string path in changed.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (previous[path] == null) { if (File.Exists(path)) File.Delete(path); }
                        else AtomicWrite(path, previous[path]);
                    }
                    catch (Exception error) { rollbackErrors.Add(path + ": " + error.Message); }
                }
                if (registryTouched)
                {
                    try { RestoreRegistration(oldRegistration); } catch (Exception error) { rollbackErrors.Add(error.Message); }
                }
                if (rollbackErrors.Count > 0) throw new IOException(failure.Message + "\r\nRollback needs attention: " + string.Join("; ", rollbackErrors), failure);
                throw;
            }
        }

        internal static void AtomicWrite(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = Path.Combine(Path.GetDirectoryName(path), ".pinch-install-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static void StopApplication(InstallLayout layout)
        {
            string target = Path.Combine(layout.Root, "Pinch.exe");
            List<Process> running = new List<Process>();
            try
            {
                foreach (Process process in Process.GetProcessesByName("Pinch"))
                {
                    bool matches = false;
                    try { matches = string.Equals(process.MainModule.FileName, target, StringComparison.OrdinalIgnoreCase); }
                    catch { }
                    if (matches) running.Add(process); else process.Dispose();
                }
                if (running.Count == 0) return;
                try { using (EventWaitHandle signal = EventWaitHandle.OpenExisting(RuntimeState.InstancePrefix + ".shutdown")) signal.Set(); }
                catch (WaitHandleCannotBeOpenedException)
                {
                    throw new IOException("\u8bf7\u5148\u53f3\u952e\u65e7\u7248 Pinch \u6258\u76d8\u56fe\u6807\uff0c\u9009\u62e9\u201c\u5f7b\u5e95\u9000\u51fa\u7a0b\u5e8f\u201d\uff0c\u7136\u540e\u91cd\u8bd5\u3002");
                }
                foreach (Process process in running)
                    if (!process.WaitForExit(60000)) throw new IOException("Pinch is finishing an image. Retry after it exits.");
            }
            finally { foreach (Process process in running) process.Dispose(); }
        }
        private static void CreateShortcut(string path, string exe, string directory)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            object shell = null, shortcut = null;
            try
            {
                Type type = Type.GetTypeFromProgID("WScript.Shell");
                shell = Activator.CreateInstance(type);
                shortcut = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                Type t = shortcut.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { directory });
                t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { exe + ",0" });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Pinch " + AppInfo.Version });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }
        private static Dictionary<string, object> ReadRegistration()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                return key == null ? null : key.GetValueNames().ToDictionary(n => n, n => key.GetValue(n));
        }
        private static void RestoreRegistration(Dictionary<string, object> snapshot)
        {
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
            if (snapshot != null) using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                foreach (KeyValuePair<string, object> item in snapshot) key.SetValue(item.Key, item.Value);
        }
        private static void Register(InstallLayout layout)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                key.SetValue("DisplayName", "Pinch"); key.SetValue("DisplayVersion", AppInfo.Version); key.SetValue("Publisher", "Han");
                key.SetValue("InstallLocation", layout.Root); key.SetValue("DisplayIcon", Path.Combine(layout.Root, "Pinch.exe") + ",0");
                key.SetValue("UninstallString", "\"" + Path.Combine(layout.Root, "PinchUninstall.exe") + "\"");
                key.SetValue("QuietUninstallString", "\"" + Path.Combine(layout.Root, "PinchUninstall.exe") + "\" --quiet");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        public static void Uninstall(InstallLayout layout)
        {
            CheckPath(layout.Root);
            string marker = Path.Combine(layout.Root, "Pinch.install.xml");
            XElement record = XElement.Load(marker);
            if ((string)record.Attribute("root") != layout.Root || record.Name != "PinchInstallation")
                throw new IOException("Installation identity mismatch.");
            StopApplication(layout);
            foreach (string name in OwnedFiles)
            {
                string file = Path.Combine(layout.Root, name);
                CheckPath(file);
                if (File.Exists(file)) File.Delete(file);
            }
            if (layout.Integrate)
            {
                foreach (string path in new[] { layout.DesktopShortcut, layout.StartShortcut })
                {
                    if (File.Exists(path) && ShortcutTargets(path, Path.Combine(layout.Root, "Pinch.exe"))) File.Delete(path);
                }
                string menuDirectory = Path.GetDirectoryName(layout.StartShortcut);
                if (Directory.Exists(menuDirectory) && !Directory.EnumerateFileSystemEntries(menuDirectory).Any()) Directory.Delete(menuDirectory);
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null && !string.Equals((string)key.GetValue("InstallLocation"), layout.Root, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Uninstall registration belongs to another location.");
                }
                Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
            }
            // User settings, logs and unknown files are retained; never recursively delete the install directory.
            if (!Directory.EnumerateFileSystemEntries(layout.Root).Any()) Directory.Delete(layout.Root);
        }
        private static bool ShortcutTargets(string path, string target)
        {
            object shell = null, shortcut = null;
            try
            {
                Type type = Type.GetTypeFromProgID("WScript.Shell"); shell = Activator.CreateInstance(type);
                shortcut = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                string value = (string)shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
                return string.Equals(value, target, StringComparison.OrdinalIgnoreCase);
            }
            finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); if (shell != null) Marshal.FinalReleaseComObject(shell); }
        }
    }
}

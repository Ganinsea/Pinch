using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace ImageCompressorFloat
{
    internal static class RuntimeState
    {
        public static string InstallDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pinch"); } }
        public static string DataDirectory
        {
            get
            {
                string config = Path.Combine(InstallDirectory, "data-root.txt");
                try
                {
                    if (File.Exists(config))
                    {
                        string value = File.ReadAllText(config).Trim();
                        if (Path.IsPathRooted(value)) return value;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                return Path.Combine(InstallDirectory, "data");
            }
        }
        public static string InstancePrefix
        {
            get
            {
                using (SHA256 sha = SHA256.Create())
                {
                    string sid = WindowsIdentity.GetCurrent().User.Value;
                    return "Local\\Han.Pinch." + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sid))).Replace("-", "").Substring(0, 20);
                }
            }
        }
        public static void Log(string directory, string message)
        {
            try
            {
                string logs = Path.Combine(directory, "logs");
                Directory.CreateDirectory(logs);
                File.AppendAllText(Path.Combine(logs, DateTime.Today.ToString("yyyy-MM-dd") + ".log"),
                    DateTime.Now.ToString("O") + " " + message.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Encoding.UTF8);
            }
            catch { /* A log failure must not stop image processing. */ }
        }
    }
}

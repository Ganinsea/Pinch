using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
namespace ImageCompressorFloat
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--compress")
            {
                int failures = 0;
                PinchSettings settings = PinchSettings.Load(RuntimeState.DataDirectory);
                foreach (string file in System.Linq.Enumerable.Skip(args, 1))
                {
                    try { Console.WriteLine(ImageCompressor.CompressFile(file, settings.MaxOutputBytes, settings.ResolveOutputDirectory(file), CancellationToken.None).OutputPath); }
                    catch (Exception error) { failures++; Console.Error.WriteLine(Path.GetFileName(file) + ": " + error.Message); }
                }
                return args.Length < 2 || failures > 0 ? 1 : 0;
            }
            string prefix = RuntimeState.InstancePrefix;
            if (args.Length == 1 && args[0] == "--shutdown")
            {
                try { using (EventWaitHandle signal = EventWaitHandle.OpenExisting(prefix + ".shutdown")) signal.Set(); return 0; }
                catch (WaitHandleCannotBeOpenedException) { return 3; }
            }
            using (Mutex mutex = new Mutex(false, prefix + ".mutex"))
            {
                bool owner;
                try { owner = mutex.WaitOne(0); } catch (AbandonedMutexException) { owner = true; }
                if (!owner)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        try { using (EventWaitHandle show = EventWaitHandle.OpenExisting(prefix + ".show")) show.Set(); break; }
                        catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(100); }
                    }
                    return 0;
                }
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, prefix + ".show"))
                    using (EventWaitHandle shutdown = new EventWaitHandle(false, EventResetMode.AutoReset, prefix + ".shutdown"))
                    using (FloatForm form = new FloatForm(RuntimeState.DataDirectory, true))
                    using (System.Windows.Forms.Timer signals = new System.Windows.Forms.Timer())
                    {
                        signals.Interval = 200;
                        signals.Tick += delegate
                        {
                            if (shutdown.WaitOne(0)) form.RequestExit();
                            else if (show.WaitOne(0)) form.RestoreFromTray();
                        };
                        signals.Start();
                        Application.Run(form);
                    }
                    return 0;
                }
                catch (Exception error)
                {
                    RuntimeState.Log(RuntimeState.DataDirectory, error.ToString());
                    MessageBox.Show(error.Message, "Pinch", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}

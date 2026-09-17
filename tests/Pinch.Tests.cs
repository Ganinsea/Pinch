using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ImageCompressorFloat;
using PinchSetup;

internal static partial class PinchTests
{
    static string root;
    static string project;
    static int checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL " + name);
        checks++; Console.WriteLine("PASS " + name);
    }
    static string Solid(string name, Color color)
    {
        string path = Path.Combine(root, name);
        using (Bitmap image = new Bitmap(120,80))
        using (Graphics g = Graphics.FromImage(image))
        { g.Clear(color); image.Save(path, name.EndsWith(".jpg") ? ImageFormat.Jpeg : ImageFormat.Png); }
        return path;
    }
    static void PumpUntil(Func<bool> ready, int timeout)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (!ready())
        {
            if (clock.ElapsedMilliseconds > timeout) throw new Exception("Timed out waiting for UI.");
            Application.DoEvents(); Thread.Sleep(10);
        }
    }
    static void Pump(int milliseconds)
    {
        Stopwatch clock = Stopwatch.StartNew();
        PumpUntil(() => clock.ElapsedMilliseconds >= milliseconds, milliseconds + 5000);
    }
    static FloatForm Form()
    {
        FloatForm form = new FloatForm(Path.Combine(root,"ui-data"),false);
        IntPtr handle = form.Handle;
        return form;
    }
    static void CollisionAndCancellation()
    {
        string source = Solid("photo.png",Color.Red);
        string existing = Solid("photoys.jpg",Color.Blue);
        byte[] before = File.ReadAllBytes(existing);
        CompressionResult result = ImageCompressor.CompressFile(source,CancellationToken.None);
        Check(result.OutputPath.EndsWith("photoys-1.jpg"),"collision numbered output");
        Check(before.SequenceEqual(File.ReadAllBytes(existing)),"existing input/output preserved");
        using (Bitmap image = new Bitmap(ImageCompressor.CompressFile(existing,CancellationToken.None).OutputPath))
            Check(image.GetPixel(20,20).B > 200,"later batch source preserves original color");
        using (CancellationTokenSource cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            bool cancelled = false;
            try { ImageCompressor.Publish(new byte[] {1,2,3},Path.Combine(root,"cancel.jpg"),cancel.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && !File.Exists(Path.Combine(root,"cancel.jpg")) && Directory.GetFiles(root,".pinch-*.tmp").Length == 0,"cancel removes temporary output");
        }
        bool missingDirectory = false;
        try { ImageCompressor.Publish(new byte[] {1},Path.Combine(root,"missing","out.jpg"),CancellationToken.None); }
        catch (DirectoryNotFoundException) { missingDirectory = true; }
        Check(missingDirectory,"write failure is reported");
    }
    static void Orientations()
    {
        Color[] colors = {Color.Red,Color.Lime,Color.Blue,Color.Yellow};
        int[][] expected = {new[]{0,1,2,3},new[]{1,0,3,2},new[]{3,2,1,0},new[]{2,3,0,1},new[]{0,2,1,3},new[]{2,0,3,1},new[]{3,1,2,0},new[]{1,3,0,2}};
        for(int orientation=1;orientation<=8;orientation++)
        {
            string input = Path.Combine(root,"orientation-"+orientation+".jpg");
            using(Bitmap bitmap = new Bitmap(120,80))
            using(Graphics graphics = Graphics.FromImage(bitmap))
            {
                for(int i=0;i<4;i++) using(Brush brush = new SolidBrush(colors[i])) graphics.FillRectangle(brush,(i%2)*60,(i/2)*40,60,40);
                bitmap.Save(input,ImageFormat.Jpeg);
            }
            byte[] jpeg = File.ReadAllBytes(input);
            byte[] exif = {255,225,0,34,69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,(byte)orientation,0,0,0,0,0,0,0};
            using(FileStream stream = File.Create(input)) {stream.Write(jpeg,0,2);stream.Write(exif,0,exif.Length);stream.Write(jpeg,2,jpeg.Length-2);}
            CompressionResult result = ImageCompressor.CompressFile(input,CancellationToken.None);
            using(Bitmap output = new Bitmap(result.OutputPath))
            {
                bool valid = output.Width == (orientation>=5?80:120) && output.Height == (orientation>=5?120:80);
                for(int i=0;i<4;i++)
                {
                    Color actual = output.GetPixel((i%2==0?1:3)*output.Width/4,(i/2==0?1:3)*output.Height/4);
                    Color wanted = colors[expected[orientation-1][i]];
                    valid &= Math.Abs(actual.R-wanted.R)<40 && Math.Abs(actual.G-wanted.G)<40 && Math.Abs(actual.B-wanted.B)<40;
                }
                Check(valid,"EXIF orientation " + orientation);
            }
        }
    }
    static string SizeAndAlpha()
    {
        string path = Path.Combine(root,"large.png");
        using(Bitmap bitmap = new Bitmap(4200,3000))
        using(Graphics graphics = Graphics.FromImage(bitmap))
        {
            Random random = new Random(5678);
            for(int y=0;y<3000;y+=12) for(int x=0;x<4200;x+=12)
                using(Brush brush = new SolidBrush(Color.FromArgb(random.Next(256),random.Next(256),random.Next(256)))) graphics.FillRectangle(brush,x,y,12,12);
            bitmap.Save(path,ImageFormat.Png);
        }
        CompressionResult result = ImageCompressor.CompressFile(path,CancellationToken.None);
        Check(result.Bytes <= 2000000 && result.Bytes == new FileInfo(result.OutputPath).Length,"decimal 2MB output cap");
        Check(result.Quality>=60,"compression maintains quality floor");
        using(Bitmap transparent = new Bitmap(30,20,PixelFormat.Format32bppArgb))
        {
            CompressionResult white = ImageCompressor.Compress(transparent,Path.Combine(root,"transparent.jpg"),CancellationToken.None);
            using(Bitmap output = new Bitmap(white.OutputPath)) Check(output.GetPixel(10,10).R>245 && output.GetPixel(10,10).G>245,"transparent pixels flattened to white");
        }
        return path;
    }
    static void QueueAndClipboard(string large)
    {
        string bad = Path.Combine(root,"text.txt"); File.WriteAllText(bad,"not image");
        string first = Solid("batch-first.png",Color.Red), last = Solid("batch-last.png",Color.Blue);
        using(FloatForm form = Form())
        {
            int ticks = 0;
            using(System.Windows.Forms.Timer heartbeat = new System.Windows.Forms.Timer {Interval=30})
            {
                heartbeat.Tick += delegate {ticks++;}; heartbeat.Start();
                form.EnqueueFiles(new[]{large,first,bad,last});
                PumpUntil(()=>!form.IsBusy,60000);
            }
            Check(ticks>2,"UI message loop stays responsive during compression");
            Check(form.SuccessCount==3 && form.FailureCount==1 && File.Exists(Path.Combine(root,"batch-lastys.jpg")),"mixed batch continues after failure");
            string status = form.StatusText;
            form.AcceptClipboard(new DataObject(DataFormats.UnicodeText,"hello"));
            Check(form.StatusText==status && !form.IsBusy,"clipboard text ignored");
            form.AcceptClipboard(new DataObject(DataFormats.FileDrop,new[]{bad,first}));
            Check(form.StatusText==status && !form.IsBusy,"non-image first clipboard file ignored");
            string copied = Solid("copied.png",Color.Aqua);
            form.AcceptClipboard(new DataObject(DataFormats.FileDrop,new[]{copied}));
            PumpUntil(()=>!form.IsBusy,10000);
            Console.WriteLine("clipboard state="+form.StatusText+" timer="+form.ResetPending+" ok="+form.SuccessCount+" failures="+form.FailureCount);
            Check(form.StatusText.StartsWith("\u5b8c\u6210") && form.ResetPending,"clipboard file produces completion and reset timer");
            form.EnqueueBitmap(new Bitmap(100,100),Path.Combine(root,"clipboard-bitmap.jpg"));
            PumpUntil(()=>!form.IsBusy,10000);
            Check(File.Exists(Path.Combine(root,"clipboard-bitmap.jpg")) && form.ResetPending,"bitmap queue follows same completion path");
            Console.WriteLine("WAIT real 30 second reset...");
            Pump(31000);
            Check(form.StatusText.StartsWith("\u62d6\u5165\u56fe\u7247") && !form.ResetPending,"real 30 second idle reset");
            Check(!form.ShowInTaskbar,"no taskbar entry");
            form.StartPosition=FormStartPosition.Manual;
            IntPtr formHandle=form.Handle;
            Label label=form.Controls.OfType<Label>().First();
            IntPtr labelHandle=label.Handle;
            form.Location=new Point(200,200);
            BindingFlags nonPublic=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(Control).GetMethod("OnMouseDown",nonPublic).Invoke(label,new object[]{new MouseEventArgs(MouseButtons.Left,1,10,10,0)});
            typeof(Control).GetMethod("OnMouseMove",nonPublic).Invoke(label,new object[]{new MouseEventArgs(MouseButtons.Left,0,11,11,0)});
            Check(form.Location==new Point(201,201),"content drag uses screen delta without titlebar jump");
            typeof(Control).GetMethod("OnMouseUp",nonPublic).Invoke(label,new object[]{new MouseEventArgs(MouseButtons.Left,1,11,11,0)});
            form.RestoreFromTray();
            Check(!form.ShowInTaskbar,"restore remains absent from taskbar");
            form.Hide();
        }
        using(FloatForm exiting = Form())
        {
            string queued = Solid("skip-on-exit.png",Color.Pink);
            exiting.EnqueueFiles(new[]{large,queued});
            exiting.RequestExit();
            PumpUntil(()=>exiting.IsDisposed,60000);
            Check(!File.Exists(Path.Combine(root,"skip-on-exitys.jpg")),"exit drains current task and drops queued work");
        }
    }
    static void InstallerTransaction()
    {
        InstallLayout layout = new InstallLayout(Path.Combine(root,"install","Pinch"),false);
        byte[] app = File.ReadAllBytes(Path.Combine(project,"bin","Pinch.exe"));
        byte[] uninstall = File.ReadAllBytes(Path.Combine(project,"bin","PinchUninstall.exe"));
        byte[] guide = Encoding.UTF8.GetBytes("Pinch guide");
        InstallerCore.Install(layout,app,uninstall,guide,null);
        Check(File.ReadAllBytes(Path.Combine(layout.Root,"Pinch.exe")).SequenceEqual(app),"clean install payload verification");
        string userFile = Path.Combine(layout.Root,"user-note.txt"); File.WriteAllText(userFile,"keep me");
        byte[] old = File.ReadAllBytes(Path.Combine(project,"tests","fixtures","legacy","Pinch.exe"));
        File.WriteAllBytes(Path.Combine(layout.Root,"Pinch.exe"),old);
        bool failed = false;
        try { InstallerCore.Install(layout,app,uninstall,guide,name=>{if(name=="PinchUninstall.exe") throw new IOException("Injected disk failure");}); }
        catch(IOException) {failed=true;}
        Check(failed && File.ReadAllBytes(Path.Combine(layout.Root,"Pinch.exe")).SequenceEqual(old),"failed update rolls back prior executable");
        Check(File.ReadAllText(userFile)=="keep me","update preserves unknown user files");
        InstallerCore.Install(layout,app,uninstall,guide,null);
        InstallerCore.Uninstall(layout);
        Check(!File.Exists(Path.Combine(layout.Root,"Pinch.exe")) && File.Exists(userFile),"uninstall removes only owned files");
    }
    [STAThread]
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        project=Path.GetFullPath(args[0]); root=Path.Combine(project,"tests","output","run-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(root);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        try
        {
            if(args.Length>1 && args[1]=="--settings-ui") { SettingsWindow(); TraySettingsWindow(); Console.WriteLine("RESULT " + checks + " focused UI checks passed; fixtures="+root); return 0; }
            CollisionAndCancellation(); Orientations(); string large=SizeAndAlpha(); InstallerTransaction(); QueueAndClipboard(large);
            SettingsAndTargets(large); BatchOrientations(); SettingsWindow(); TraySettingsWindow();
            Console.WriteLine("RESULT " + checks + " checks passed; fixtures="+root); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using ImageCompressorFloat;

internal static partial class PinchTests
{
    static void SettingsAndTargets(string large)
    {
        string data = Path.Combine(root,"settings-data"); Directory.CreateDirectory(data);
        new XElement("Pinch",new XAttribute("x",120),new XAttribute("y",130),new XAttribute("topMost",false)).Save(Path.Combine(data,"settings.xml"));
        PinchSettings migrated = PinchSettings.Load(data);
        Check(migrated.WindowPosition==new Point(120,130) && !migrated.AlwaysOnTop && migrated.MaxOutputBytes==2000000,"legacy settings migrate with defaults");
        string a = Path.Combine(root,"output-A"), b = Path.Combine(root,"output-B");
        using(FloatForm form = new FloatForm(data,false))
        {
            IntPtr handle=form.Handle;
            PinchSettings first=form.CurrentSettings;
            first.Background=Color.FromArgb(222,241,231); first.TitleColor=Color.FromArgb(42,95,76); first.OpacityPercent=72;
            first.MaxOutputBytes=350000; first.Location=SaveLocation.Custom; first.CustomDirectory=a;
            first.ResetSeconds=5; first.Notifications=false;
            form.ApplySettings(first);
            Check(form.BackColor==first.Background && Math.Abs(form.Opacity-.72)<.01,"appearance and opacity apply to floating window");
            Check(form.StatusText.Contains("0.35MB"),"idle hint reflects configured threshold");
            string second=Solid("snapshot-second.png",Color.Pink), third=Solid("new-settings.png",Color.Green);
            form.EnqueueFiles(new[]{large,second});
            PinchSettings next=first.Copy(); next.CustomDirectory=b; next.MaxOutputBytes=900000;
            form.ApplySettings(next); form.EnqueueFiles(new[]{third});
            PumpUntil(()=>!form.IsBusy,60000);
            Check(File.Exists(Path.Combine(a,"largeys.jpg")) && new FileInfo(Path.Combine(a,"largeys.jpg")).Length<=350000,"custom threshold and folder enforced");
            Check(File.Exists(Path.Combine(a,"snapshot-secondys.jpg")) && !File.Exists(Path.Combine(b,"snapshot-secondys.jpg")),"queued files retain settings snapshot");
            Check(File.Exists(Path.Combine(b,"new-settingsys.jpg")),"new tasks use updated destination");
            using(Bitmap bitmap=new Bitmap(30,20)) form.AcceptClipboard(new DataObject(DataFormats.Bitmap,bitmap));
            PumpUntil(()=>!form.IsBusy,10000);
            Check(Directory.GetFiles(b,"Pinch_clipboard_*ys.jpg").Length==1,"clipboard bitmap respects custom folder");
            Pump(5500);
            Check(!form.ResetPending && form.StatusText.Contains("0.9MB"),"custom reset interval works");
            byte[] saved=File.ReadAllBytes(Path.Combine(data,"settings.xml"));
            PinchSettings invalid=next.Copy(); invalid.MaxOutputBytes=0;
            bool rejected=false; try{form.ApplySettings(invalid);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected && saved.SequenceEqual(File.ReadAllBytes(Path.Combine(data,"settings.xml"))),"invalid settings do not overwrite saved config");
        }
        PinchSettings restored=PinchSettings.Load(data);
        Check(restored.MaxOutputBytes==900000 && restored.CustomDirectory==b && restored.OpacityPercent==72 && restored.ResetSeconds==5 && !restored.Notifications,"settings survive restart");
        using(FloatForm form=new FloatForm(data,false)) Check(form.BackColor==restored.Background && Math.Abs(form.Opacity-.72)<.01,"restart restores appearance");
        PinchSettings locations=new PinchSettings();
        Check(locations.ResolveOutputDirectory(large)==root,"source folder mode");
        Check(locations.ResolveOutputDirectory(null)==Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"bitmap source mode falls back to Desktop");
        locations.Location=SaveLocation.Desktop;
        Check(locations.ResolveOutputDirectory(large)==Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Desktop destination mode");
    }

    static string OrientedFixture(int orientation, bool bigEndian)
    {
        string directory=Path.Combine(root,"batch-exif-input"); Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,(bigEndian?"be-":"le-")+orientation+".jpg");
        Color[] colors={Color.Red,Color.Lime,Color.Blue,Color.Yellow};
        using(Bitmap bitmap=new Bitmap(120,80)) using(Graphics graphics=Graphics.FromImage(bitmap))
        {
            for(int i=0;i<4;i++) using(Brush brush=new SolidBrush(colors[i])) graphics.FillRectangle(brush,(i%2)*60,(i/2)*40,60,40);
            bitmap.Save(path,ImageFormat.Jpeg);
        }
        byte[] jpeg=File.ReadAllBytes(path);
        byte[] little={255,225,0,34,69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,(byte)orientation,0,0,0,0,0,0,0};
        byte[] big={255,225,0,34,69,120,105,102,0,0,77,77,0,42,0,0,0,8,0,1,1,18,0,3,0,0,0,1,0,(byte)orientation,0,0,0,0,0,0};
        byte[] exif=bigEndian?big:little;
        using(FileStream file=File.Create(path)){file.Write(jpeg,0,2);file.Write(exif,0,exif.Length);file.Write(jpeg,2,jpeg.Length-2);}
        return path;
    }
    static void BatchOrientations()
    {
        List<string> inputs=new List<string>();
        for(int i=1;i<=8;i++){inputs.Add(OrientedFixture(i,false));inputs.Add(OrientedFixture(i,true));}
        Dictionary<string,byte[]> originals=inputs.ToDictionary(p=>p,p=>File.ReadAllBytes(p));
        string output=Path.Combine(root,"batch-exif-output");
        using(FloatForm form=new FloatForm(Path.Combine(root,"batch-exif-data"),false))
        {
            IntPtr handle=form.Handle;
            PinchSettings settings=form.CurrentSettings;settings.Location=SaveLocation.Custom;settings.CustomDirectory=output;settings.MaxOutputBytes=200000;
            form.ApplySettings(settings);
            form.EnqueueFiles(inputs);
            PumpUntil(()=>!form.IsBusy,30000);
            Check(form.SuccessCount==16 && form.FailureCount==0,"batch processes all 16 mixed EXIF inputs");
        }
        int[][] expected={new[]{0,1,2,3},new[]{1,0,3,2},new[]{3,2,1,0},new[]{2,3,0,1},new[]{0,2,1,3},new[]{2,0,3,1},new[]{3,1,2,0},new[]{1,3,0,2}};
        Color[] colors={Color.Red,Color.Lime,Color.Blue,Color.Yellow};
        foreach(string input in inputs)
        {
            int orientation=int.Parse(Path.GetFileNameWithoutExtension(input).Substring(3));
            string result=Path.Combine(output,Path.GetFileNameWithoutExtension(input)+"ys.jpg");
            using(Bitmap bitmap=new Bitmap(result))
            {
                bool valid=bitmap.Width==(orientation>=5?80:120) && bitmap.Height==(orientation>=5?120:80);
                for(int i=0;i<4;i++)
                {
                    Color actual=bitmap.GetPixel((i%2==0?1:3)*bitmap.Width/4,(i/2==0?1:3)*bitmap.Height/4);
                    Color wanted=colors[expected[orientation-1][i]];
                    valid &= Math.Abs(actual.R-wanted.R)<40 && Math.Abs(actual.G-wanted.G)<40 && Math.Abs(actual.B-wanted.B)<40;
                }
                Check(valid && !bitmap.PropertyIdList.Contains(0x112),"batch direction normalized: "+Path.GetFileName(input));
            }
            Check(originals[input].SequenceEqual(File.ReadAllBytes(input)),"batch source unchanged: "+Path.GetFileName(input));
        }
    }
    static void SettingsWindow()
    {
        int saves=0;
        using(SettingsForm window=new SettingsForm(new PinchSettings(),s=>saves++))
        {
            window.StartPosition=FormStartPosition.Manual; window.Location=new Point(-30000,-30000); window.Opacity=0;
            window.Show(); Application.DoEvents();
            TabControl tabs=window.Controls.OfType<TabControl>().Single();
            Check(tabs.TabCount==3,"settings has appearance, compression and behavior tabs");
            for(int i=0;i<3;i++)
            {
                tabs.SelectedIndex=i; Application.DoEvents(); window.PerformLayout();
                using(Bitmap image=new Bitmap(window.Width,window.Height))
                {window.DrawToBitmap(image,new Rectangle(Point.Empty,window.Size));image.Save(Path.Combine(root,"settings-tab-"+i+".png"),ImageFormat.Png);}
            }
            FlowLayoutPanel buttons=window.Controls.OfType<FlowLayoutPanel>().Single();
            buttons.Controls.OfType<Button>().Single(b=>b.Text=="\u53d6\u6d88").PerformClick();
            Check(window.IsDisposed && saves==0,"cancel closes modeless settings without saving");
        }
        using(SettingsForm window=new SettingsForm(new PinchSettings(),s=>saves++))
        {
            window.StartPosition=FormStartPosition.Manual;window.Location=new Point(-30000,-30000);window.Opacity=0;
            window.Show();Application.DoEvents();
            window.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(b=>b.Text=="\u4fdd\u5b58").PerformClick();
            Check(window.IsDisposed && saves==1,"settings Save invokes persistence callback once");
        }
    }
    static void TraySettingsWindow()
    {
        string data=Path.Combine(root,"tray-settings-data");
        using(FloatForm form=new FloatForm(data,false))
        {
            IntPtr handle=form.Handle;
            form.ContextMenuStrip.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="\u8bbe\u7f6e...").PerformClick();
            Application.DoEvents();
            SettingsForm dialog=Application.OpenForms.OfType<SettingsForm>().Single();
            Check(dialog.Visible && !form.Visible,"tray settings opens while float is hidden");
            NumericUpDown numeric=(NumericUpDown)typeof(SettingsForm).GetField("threshold",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(dialog);
            numeric.Value=1.25M;
            dialog.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(b=>b.Text=="\u4fdd\u5b58").PerformClick();
            Check(dialog.IsDisposed && form.CurrentSettings.MaxOutputBytes==1250000 && PinchSettings.Load(data).MaxOutputBytes==1250000,"tray settings Save updates actual form and persistent config");
        }
    }
}

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace ImageCompressorFloat
{
    internal enum SaveLocation { Source, Desktop, Custom }

    internal sealed class PinchSettings
    {
        public Color Background = Color.FromArgb(255, 229, 235);
        public Color TitleColor = Color.FromArgb(216, 82, 116);
        public int OpacityPercent = 100;
        public long MaxOutputBytes = AppInfo.TargetBytes;
        public SaveLocation Location = SaveLocation.Source;
        public string CustomDirectory = "";
        public bool AlwaysOnTop = true;
        public int ResetSeconds = 30;
        public bool Notifications = true;
        public Point? WindowPosition;

        public PinchSettings Copy() { return (PinchSettings)MemberwiseClone(); }

        public void Validate()
        {
            if (OpacityPercent < 30 || OpacityPercent > 100) throw new ArgumentOutOfRangeException("OpacityPercent");
            if (MaxOutputBytes < 100000 || MaxOutputBytes > 50000000) throw new ArgumentOutOfRangeException("MaxOutputBytes");
            if (ResetSeconds < 5 || ResetSeconds > 300) throw new ArgumentOutOfRangeException("ResetSeconds");
            if (!Enum.IsDefined(typeof(SaveLocation), Location)) throw new ArgumentOutOfRangeException("Location");
            if (Location == SaveLocation.Custom)
            {
                if (string.IsNullOrWhiteSpace(CustomDirectory) || !Path.IsPathRooted(CustomDirectory))
                    throw new ArgumentException("\u8bf7\u9009\u62e9\u6709\u6548\u7684\u4fdd\u5b58\u6587\u4ef6\u5939");
                CustomDirectory = Path.GetFullPath(CustomDirectory.Trim());
                if (File.Exists(CustomDirectory)) throw new ArgumentException("\u4fdd\u5b58\u4f4d\u7f6e\u5fc5\u987b\u662f\u6587\u4ef6\u5939");
            }
        }

        public string ResolveOutputDirectory(string inputFile)
        {
            if (Location == SaveLocation.Custom) return CustomDirectory;
            if (Location == SaveLocation.Desktop || string.IsNullOrEmpty(inputFile))
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return Path.GetDirectoryName(Path.GetFullPath(inputFile));
        }

        public static PinchSettings Load(string directory)
        {
            PinchSettings settings = new PinchSettings();
            string file = Path.Combine(directory, "settings.xml");
            if (!File.Exists(file)) return settings;
            try
            {
                XElement root = XElement.Load(file);
                if (root.Name != "Pinch") throw new InvalidDataException("Invalid settings root.");
                int x, y;
                if (int.TryParse((string)root.Attribute("x"), out x) && int.TryParse((string)root.Attribute("y"), out y)) settings.WindowPosition = new Point(x, y);
                settings.AlwaysOnTop = ReadBool(root, "topMost", true);
                settings.Notifications = ReadBool(root, "notifications", true);
                settings.Background = ReadColor(root, "background", settings.Background);
                settings.TitleColor = ReadColor(root, "titleColor", settings.TitleColor);
                settings.OpacityPercent = ReadInt(root, "opacity", 100, 30, 100);
                settings.MaxOutputBytes = ReadInt(root, "maxBytes", (int)AppInfo.TargetBytes, 100000, 50000000);
                settings.ResetSeconds = ReadInt(root, "resetSeconds", 30, 5, 300);
                SaveLocation mode;
                if (Enum.TryParse((string)root.Attribute("saveLocation"), out mode) && Enum.IsDefined(typeof(SaveLocation), mode)) settings.Location = mode;
                settings.CustomDirectory = ((string)root.Attribute("customDirectory") ?? "").Trim();
                settings.Validate();
                return settings;
            }
            catch (Exception error)
            {
                RuntimeState.Log(directory, "settings-load: " + error.Message);
                return new PinchSettings();
            }
        }

        public void Save(string directory)
        {
            Validate();
            Directory.CreateDirectory(directory);
            XElement root = new XElement("Pinch", new XAttribute("version", AppInfo.Version),
                new XAttribute("topMost", AlwaysOnTop), new XAttribute("notifications", Notifications),
                new XAttribute("background", ColorHex(Background)), new XAttribute("titleColor", ColorHex(TitleColor)),
                new XAttribute("opacity", OpacityPercent), new XAttribute("maxBytes", MaxOutputBytes),
                new XAttribute("resetSeconds", ResetSeconds), new XAttribute("saveLocation", Location),
                new XAttribute("customDirectory", CustomDirectory ?? ""));
            if (WindowPosition.HasValue) { root.Add(new XAttribute("x", WindowPosition.Value.X)); root.Add(new XAttribute("y", WindowPosition.Value.Y)); }
            string file = Path.Combine(directory, "settings.xml");
            string temporary = Path.Combine(directory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                root.Save(temporary);
                if (File.Exists(file)) File.Replace(temporary, file, null); else File.Move(temporary, file);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static string ColorHex(Color color) { return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2"); }
        private static Color ReadColor(XElement root, string attribute, Color fallback)
        {
            string text = (string)root.Attribute(attribute);
            int rgb;
            return text != null && text.Length == 7 && text[0] == '#' && int.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)
                ? Color.FromArgb(255, Color.FromArgb(rgb)) : fallback;
        }
        private static int ReadInt(XElement root, string name, int fallback, int min, int max)
        {
            int value;
            return int.TryParse((string)root.Attribute(name), out value) && value >= min && value <= max ? value : fallback;
        }
        private static bool ReadBool(XElement root, string name, bool fallback)
        {
            bool value;
            return bool.TryParse((string)root.Attribute(name), out value) ? value : fallback;
        }
        internal static Color Readable(Color preferred, Color background)
        {
            double a = Luminance(preferred), b = Luminance(background);
            if ((Math.Max(a,b) + .05) / (Math.Min(a,b) + .05) >= 4.5) return preferred;
            return b > .179 ? Color.Black : Color.White;
        }
        private static double Luminance(Color c) { return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B); }
        private static double Linear(byte channel) { double x = channel / 255d; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
    }
}

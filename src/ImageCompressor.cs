using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;

namespace ImageCompressorFloat
{
    internal sealed class CompressionResult
    {
        public string OutputPath;
        public long Bytes;
        public int Width;
        public int Height;
        public int Quality;
        public bool FirstFrameOnly;
    }
    internal static class ImageCompressor
    {
        private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };
        private static readonly ImageCodecInfo Jpeg = ImageCodecInfo.GetImageEncoders().First(x => x.MimeType == "image/jpeg");
        private const long MaxPixels = 80000000;
        public static bool Supports(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        }
        public static CompressionResult CompressFile(string path, CancellationToken token)
        {
            return CompressFile(path, AppInfo.TargetBytes, null, token);
        }
        public static CompressionResult CompressFile(string path, long targetBytes, string outputDirectory, CancellationToken token)
        {
            path = Path.GetFullPath(path);
            if (!Supports(path)) throw new NotSupportedException("\u4e0d\u652f\u6301\u7684\u56fe\u7247\u683c\u5f0f");
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (Image image = Image.FromStream(stream, true, true))
                return Compress(image, Path.Combine(outputDirectory ?? Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "ys.jpg"), targetBytes, token);
        }
        public static CompressionResult Compress(Image source, string desiredPath, CancellationToken token)
        {
            return Compress(source, desiredPath, AppInfo.TargetBytes, token);
        }
        public static CompressionResult Compress(Image source, string desiredPath, long targetBytes, CancellationToken token)
        {
            if (targetBytes < 100000 || targetBytes > 50000000) throw new ArgumentOutOfRangeException("targetBytes");
            token.ThrowIfCancellationRequested();
            if ((long)source.Width * source.Height > MaxPixels)
                throw new InvalidOperationException("\u56fe\u7247\u8d85\u8fc7 8000 \u4e07\u50cf\u7d20\uff0c\u8bf7\u5148\u7f29\u5c0f\u5c3a\u5bf8");
            bool firstFrame = source.FrameDimensionsList.Any(d => source.GetFrameCount(new FrameDimension(d)) > 1);
            int orientation = ReadOrientation(source);
            using (Bitmap normalized = DrawRgb(source, source.Width, source.Height))
            {
                RotateFlipType[] operations = {
                    RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone,
                    RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone,
                    RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX,
                    RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX,
                    RotateFlipType.Rotate270FlipNone
                };
                if (orientation >= 1 && orientation <= 8) normalized.RotateFlip(operations[orientation]);
                int width = normalized.Width, height = normalized.Height;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    using (Bitmap working = DrawRgb(normalized, width, height))
                    {
                        int quality;
                        byte[] bytes = FindQuality(working, targetBytes, token, out quality);
                        if (bytes != null)
                        {
                            using (MemoryStream buffer = new MemoryStream(bytes, false))
                            using (Image check = Image.FromStream(buffer, true, true))
                            {
                                if (check.Width != width || check.Height != height || bytes.LongLength > targetBytes)
                                    throw new InvalidDataException("Output validation failed.");
                            }
                            return new CompressionResult {
                                OutputPath = Publish(bytes, desiredPath, token), Bytes = bytes.LongLength,
                                Width = width, Height = height, Quality = quality, FirstFrameOnly = firstFrame
                            };
                        }
                    }
                    if (width == 1 && height == 1) throw new InvalidOperationException("Cannot meet the size limit.");
                    width = Math.Max(1, (int)(width * 0.85));
                    height = Math.Max(1, (int)(height * 0.85));
                }
            }
        }
        internal static int ReadOrientation(Image image)
        {
            if (!image.PropertyIdList.Contains(0x112)) return 1;
            try
            {
                byte[] value = image.GetPropertyItem(0x112).Value;
                if (value.Length == 1) return value[0] >= 1 && value[0] <= 8 ? value[0] : 1;
                if (value.Length >= 2)
                {
                    int little = value[0] | (value[1] << 8);
                    if (little >= 1 && little <= 8) return little;
                    int big = (value[0] << 8) | value[1];
                    if (big >= 1 && big <= 8) return big;
                }
            }
            catch (ArgumentException) { }
            return 1;
        }
        private static byte[] FindQuality(Bitmap image, long targetBytes, CancellationToken token, out int quality)
        {
            quality = 92;
            byte[] maximum = Encode(image, quality, token);
            if (maximum.LongLength <= targetBytes) return maximum;
            quality = 60;
            byte[] best = Encode(image, quality, token);
            if (best.LongLength > targetBytes) return null;
            int low = 61, high = 91;
            while (low <= high)
            {
                int middle = (low + high) / 2;
                byte[] candidate = Encode(image, middle, token);
                if (candidate.LongLength <= targetBytes)
                { best = candidate; quality = middle; low = middle + 1; }
                else high = middle - 1;
            }
            return best;
        }
        private static byte[] Encode(Image image, int quality, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (MemoryStream buffer = new MemoryStream())
            using (EncoderParameters parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                image.Save(buffer, Jpeg, parameters);
                return buffer.ToArray();
            }
        }
        private static Bitmap DrawRgb(Image source, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    graphics.Clear(Color.White);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(source, new Rectangle(0, 0, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }
        internal static string Publish(byte[] bytes, string desiredPath, CancellationToken token)
        {
            desiredPath = Path.GetFullPath(desiredPath);
            string directory = Path.GetDirectoryName(desiredPath);
            string temp = Path.Combine(directory, ".pinch-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                // Atomic move refuses an existing destination, including races with another writer.
                for (int i = 0; i < 10000; i++)
                {
                    token.ThrowIfCancellationRequested();
                    string candidate = i == 0 ? desiredPath : Path.Combine(directory,
                        Path.GetFileNameWithoutExtension(desiredPath) + "-" + i + Path.GetExtension(desiredPath));
                    try { File.Move(temp, candidate); return candidate; }
                    catch (IOException) { if (!File.Exists(candidate) && !Directory.Exists(candidate)) throw; }
                }
                throw new IOException("Too many output name collisions.");
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}

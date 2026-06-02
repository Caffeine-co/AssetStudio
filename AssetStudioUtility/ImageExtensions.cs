using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Webp;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.CompilerServices;

namespace AssetStudio
{
    public static class ImageSharpNativeAotGuard
    {
        public static readonly object Sync = new object();
        public static readonly bool Enabled = !RuntimeFeature.IsDynamicCodeSupported
            || IsEnabled(Environment.GetEnvironmentVariable("HARUKI_ASSET_STUDIO_IMAGE_GUARD"));
        public static Action<string, long> TimingSink { get; set; }

        [ThreadStatic]
        private static int SyncDepth;

        public static void Run(Action action)
        {
            if (!Enabled || SyncDepth > 0)
            {
                action();
                return;
            }

            var waitStart = Stopwatch.GetTimestamp();
            Monitor.Enter(Sync);
            AddTiming("parallel.imagesharp_guard.wait", Stopwatch.GetTimestamp() - waitStart);
            SyncDepth++;
            var heldStart = Stopwatch.GetTimestamp();
            try
            {
                action();
            }
            finally
            {
                AddTiming("parallel.imagesharp_guard.held", Stopwatch.GetTimestamp() - heldStart);
                SyncDepth--;
                Monitor.Exit(Sync);
            }
        }

        public static T Run<T>(Func<T> action)
        {
            if (!Enabled || SyncDepth > 0)
            {
                return action();
            }

            var waitStart = Stopwatch.GetTimestamp();
            Monitor.Enter(Sync);
            AddTiming("parallel.imagesharp_guard.wait", Stopwatch.GetTimestamp() - waitStart);
            SyncDepth++;
            var heldStart = Stopwatch.GetTimestamp();
            try
            {
                return action();
            }
            finally
            {
                AddTiming("parallel.imagesharp_guard.held", Stopwatch.GetTimestamp() - heldStart);
                SyncDepth--;
                Monitor.Exit(Sync);
            }
        }

        private static void AddTiming(string name, long elapsedTicks)
        {
            var sink = TimingSink;
            if (sink != null)
            {
                sink(name, elapsedTicks);
            }
        }

        private static bool IsEnabled(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class ImageExtensions
    {
        public static void WriteToStream(this Image image, Stream stream, ImageFormat imageFormat)
        {
            ImageSharpNativeAotGuard.Run(() =>
            {
                switch (imageFormat)
                {
                    case ImageFormat.Jpeg:
                        image.SaveAsJpeg(stream);
                        break;
                    case ImageFormat.Png:
                        image.SaveAsPng(stream);
                        break;
                    case ImageFormat.Bmp:
                        image.Save(stream, new BmpEncoder
                        {
                            BitsPerPixel = BmpBitsPerPixel.Pixel32,
                            SupportTransparency = true
                        });
                        break;
                    case ImageFormat.Tga:
                        image.Save(stream, new TgaEncoder
                        {
                            BitsPerPixel = TgaBitsPerPixel.Pixel32,
                            Compression = TgaCompression.None
                        });
                        break;
                    case ImageFormat.Webp:
                        image.Save(stream, new WebpEncoder
                        {
                            FileFormat = WebpFileFormatType.Lossless,
                            Quality = 50
                        });
                        break;
                }
            });
        }

        public static MemoryStream ConvertToStream(this Image image, ImageFormat imageFormat)
        {
            var stream = new MemoryStream();
            image.WriteToStream(stream, imageFormat);
            return stream;
        }
    }
}

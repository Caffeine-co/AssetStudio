using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers.Binary;
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
        private static readonly AsyncLocal<Action<string, long>> CurrentTimingSink = new AsyncLocal<Action<string, long>>();

        public static Action<string, long> TimingSink
        {
            get => CurrentTimingSink.Value;
            set => CurrentTimingSink.Value = value;
        }

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
        private static ReadOnlySpan<byte> RgbaIrMagic => "HARUKI_RGBAIR_V1"u8;

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
                    case ImageFormat.RawRgba:
                        if (image is Image<Bgra32> bgra)
                        {
                            bgra.WriteRgbaIrToStream(stream);
                            break;
                        }
                        throw new NotSupportedException("raw_rgba export requires Image<Bgra32>");
                }
            });
        }

        public static void WriteRgbaIrToStream(this Image<Bgra32> image, Stream stream)
        {
            const int bytesPerPixel = 4;
            const int headerSize = 36;
            var width = image.Width;
            var height = image.Height;
            var stride = checked(width * bytesPerPixel);
            Span<byte> header = stackalloc byte[headerSize];
            RgbaIrMagic.CopyTo(header);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, sizeof(int)), width);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(20, sizeof(int)), height);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(24, sizeof(int)), stride);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(28, sizeof(int)), 1);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(32, sizeof(int)), 0);
            stream.Write(header);

            var rowBytes = new byte[stride];
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var source = accessor.GetRowSpan(y);
                    for (var x = 0; x < width; x++)
                    {
                        var pixel = source[x];
                        var offset = x * bytesPerPixel;
                        rowBytes[offset] = pixel.R;
                        rowBytes[offset + 1] = pixel.G;
                        rowBytes[offset + 2] = pixel.B;
                        rowBytes[offset + 3] = pixel.A;
                    }
                    stream.Write(rowBytes);
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

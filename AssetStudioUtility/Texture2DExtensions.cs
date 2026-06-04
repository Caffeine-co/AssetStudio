using System;
using System.Buffers.Binary;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AssetStudio
{
    public static class Texture2DExtensions
    {
        public static Image<Bgra32> ConvertToImage(this Texture2D m_Texture2D, bool flip)
        {
            return ImageSharpNativeAotGuard.Run(() =>
            {
                var converter = new Texture2DConverter(m_Texture2D);
                var buff = BigArrayPool<byte>.Shared.Rent(converter.OutputDataSize);
                var spanBuff = buff.AsSpan(0, converter.OutputDataSize);
                try
                {
                    if (!converter.DecodeTexture2D(buff))
                        return null;

                    Image<Bgra32> image;
                    if (converter.UsesSwitchSwizzle)
                    {
                        var uncroppedSize = converter.GetUncroppedSize();
                        image = Image.LoadPixelData<Bgra32>(spanBuff, uncroppedSize.Width, uncroppedSize.Height);
                        image.Mutate(x => x.Crop(m_Texture2D.m_Width, m_Texture2D.m_Height));
                    }
                    else
                    {
                        image = Image.LoadPixelData<Bgra32>(spanBuff, m_Texture2D.m_Width, m_Texture2D.m_Height);
                    }

                    if (flip)
                    {
                        image.Mutate(x => x.Flip(FlipMode.Vertical));
                    }
                    return image;
                }
                finally
                {
                    BigArrayPool<byte>.Shared.Return(buff, clearArray: true);
                }
            });
        }

        public static MemoryStream ConvertToStream(this Texture2D m_Texture2D, ImageFormat imageFormat, bool flip)
        {
            return ImageSharpNativeAotGuard.Run(() =>
            {
                var image = ConvertToImage(m_Texture2D, flip);
                if (image != null)
                {
                    using (image)
                    {
                        return image.ConvertToStream(imageFormat);
                    }
                }
                return null;
            });
        }

        public static bool WriteBmpToStream(this Texture2D m_Texture2D, Stream destination, bool flip)
        {
            var converter = new Texture2DConverter(m_Texture2D);
            var buff = BigArrayPool<byte>.Shared.Rent(converter.OutputDataSize);
            try
            {
                if (!converter.DecodeTexture2D(buff))
                {
                    return false;
                }

                var uncroppedSize = converter.UsesSwitchSwizzle
                    ? converter.GetUncroppedSize()
                    : new Size(m_Texture2D.m_Width, m_Texture2D.m_Height);
                WriteBgra32Bmp(
                    destination,
                    buff.AsSpan(0, converter.OutputDataSize),
                    uncroppedSize.Width,
                    m_Texture2D.m_Width,
                    m_Texture2D.m_Height,
                    flip);
                return true;
            }
            finally
            {
                BigArrayPool<byte>.Shared.Return(buff, clearArray: true);
            }
        }

        private static void WriteBgra32Bmp(Stream destination, ReadOnlySpan<byte> bgra, int sourceWidth, int width, int height, bool flip)
        {
            const int fileHeaderSize = 14;
            const int dibHeaderSize = 40;
            const int bytesPerPixel = 4;
            var rowBytes = checked(width * bytesPerPixel);
            var pixelBytes = checked(rowBytes * height);
            var pixelOffset = fileHeaderSize + dibHeaderSize;
            var fileSize = checked(pixelOffset + pixelBytes);

            Span<byte> header = stackalloc byte[pixelOffset];
            header[0] = (byte)'B';
            header[1] = (byte)'M';
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(2, sizeof(int)), fileSize);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(10, sizeof(int)), pixelOffset);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(14, sizeof(int)), dibHeaderSize);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(18, sizeof(int)), width);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(22, sizeof(int)), height);
            BinaryPrimitives.WriteInt16LittleEndian(header.Slice(26, sizeof(short)), 1);
            BinaryPrimitives.WriteInt16LittleEndian(header.Slice(28, sizeof(short)), 32);
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(34, sizeof(int)), pixelBytes);
            destination.Write(header);

            for (var fileRow = 0; fileRow < height; fileRow++)
            {
                var sourceY = flip ? fileRow : height - 1 - fileRow;
                destination.Write(bgra.Slice(sourceY * sourceWidth * bytesPerPixel, rowBytes));
            }
        }
    }
}

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace PocketFrame.Runner;

public sealed class VisualBaselineComparer
{
    public async Task<VisualBaselineComparison> CompareAsync(
        string actualPath,
        string baselinePath,
        string diffPath,
        double threshold,
        CancellationToken cancellationToken = default)
    {
        var actualBytes = await File.ReadAllBytesAsync(actualPath, cancellationToken);
        var baselineBytes = await File.ReadAllBytesAsync(baselinePath, cancellationToken);
        var actual = PngImage.Decode(actualBytes);
        var baseline = PngImage.Decode(baselineBytes);
        if (actual.Width != baseline.Width || actual.Height != baseline.Height)
        {
            return new VisualBaselineComparison
            {
                ActualPath = actualPath,
                BaselinePath = baselinePath,
                DiffPath = string.Empty,
                Width = actual.Width,
                Height = actual.Height,
                BaselineWidth = baseline.Width,
                BaselineHeight = baseline.Height,
                Threshold = threshold,
                Passed = false,
                Message = $"Image size mismatch: actual {actual.Width}x{actual.Height}, baseline {baseline.Width}x{baseline.Height}."
            };
        }

        var totalPixels = actual.Width * actual.Height;
        var changedPixels = 0;
        var diff = new byte[actual.Pixels.Length];
        for (var index = 0; index < totalPixels; index++)
        {
            var offset = index * 4;
            var changed = actual.Pixels[offset] != baseline.Pixels[offset] ||
                          actual.Pixels[offset + 1] != baseline.Pixels[offset + 1] ||
                          actual.Pixels[offset + 2] != baseline.Pixels[offset + 2] ||
                          actual.Pixels[offset + 3] != baseline.Pixels[offset + 3];
            if (changed)
            {
                changedPixels++;
                diff[offset] = 255;
                diff[offset + 1] = 32;
                diff[offset + 2] = 32;
                diff[offset + 3] = 255;
            }
            else
            {
                diff[offset] = (byte)(actual.Pixels[offset] / 4);
                diff[offset + 1] = (byte)(actual.Pixels[offset + 1] / 4);
                diff[offset + 2] = (byte)(actual.Pixels[offset + 2] / 4);
                diff[offset + 3] = 255;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(diffPath))!);
        await File.WriteAllBytesAsync(diffPath, PngImage.EncodeRgba(actual.Width, actual.Height, diff), cancellationToken);
        var changedRatio = totalPixels == 0 ? 0 : changedPixels / (double)totalPixels;
        return new VisualBaselineComparison
        {
            ActualPath = actualPath,
            BaselinePath = baselinePath,
            DiffPath = diffPath,
            Width = actual.Width,
            Height = actual.Height,
            BaselineWidth = baseline.Width,
            BaselineHeight = baseline.Height,
            ChangedPixels = changedPixels,
            TotalPixels = totalPixels,
            ChangedRatio = changedRatio,
            Threshold = threshold,
            Passed = changedRatio <= threshold,
            Message = changedRatio <= threshold
                ? "Screenshot matches baseline."
                : $"Changed ratio {changedRatio:0.####} exceeds threshold {threshold:0.####}."
        };
    }

    private sealed record PngImage(int Width, int Height, byte[] Pixels)
    {
        private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

        public static PngImage Decode(byte[] bytes)
        {
            if (bytes.Length < Signature.Length || !bytes.AsSpan(0, Signature.Length).SequenceEqual(Signature))
            {
                throw new InvalidDataException("File is not a PNG image.");
            }

            var offset = Signature.Length;
            var width = 0;
            var height = 0;
            var bitDepth = 0;
            var colorType = 0;
            var compressed = new MemoryStream();

            while (offset + 12 <= bytes.Length)
            {
                var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
                offset += 4;
                var type = Encoding.ASCII.GetString(bytes, offset, 4);
                offset += 4;
                var data = bytes.AsSpan(offset, length);
                offset += length + 4;

                if (type == "IHDR")
                {
                    width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                    height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                    bitDepth = data[8];
                    colorType = data[9];
                    var interlace = data[12];
                    if (bitDepth != 8)
                    {
                        throw new NotSupportedException("Only 8-bit PNG images are supported.");
                    }

                    if (interlace != 0)
                    {
                        throw new NotSupportedException("Interlaced PNG images are not supported.");
                    }
                }
                else if (type == "IDAT")
                {
                    compressed.Write(data);
                }
                else if (type == "IEND")
                {
                    break;
                }
            }

            var bytesPerPixel = colorType switch
            {
                0 => 1,
                2 => 3,
                6 => 4,
                _ => throw new NotSupportedException($"PNG color type {colorType} is not supported.")
            };
            var raw = Inflate(compressed.ToArray());
            var stride = width * bytesPerPixel;
            var pixels = new byte[width * height * 4];
            var previous = new byte[stride];
            var current = new byte[stride];
            var rawOffset = 0;

            for (var y = 0; y < height; y++)
            {
                var filter = raw[rawOffset++];
                raw.AsSpan(rawOffset, stride).CopyTo(current);
                rawOffset += stride;
                Unfilter(filter, current, previous, bytesPerPixel);
                CopyToRgba(current, pixels, y * width * 4, colorType);
                (previous, current) = (current, previous);
            }

            return new PngImage(width, height, pixels);
        }

        public static byte[] EncodeRgba(int width, int height, byte[] rgba)
        {
            var raw = new byte[height * (1 + width * 4)];
            var sourceOffset = 0;
            var targetOffset = 0;
            for (var y = 0; y < height; y++)
            {
                raw[targetOffset++] = 0;
                Buffer.BlockCopy(rgba, sourceOffset, raw, targetOffset, width * 4);
                sourceOffset += width * 4;
                targetOffset += width * 4;
            }

            using var output = new MemoryStream();
            output.Write(Signature);
            WriteChunk(output, "IHDR", BuildHeader(width, height));
            WriteChunk(output, "IDAT", Deflate(raw));
            WriteChunk(output, "IEND", []);
            return output.ToArray();
        }

        private static byte[] Inflate(byte[] compressed)
        {
            using var source = new MemoryStream(compressed);
            using var zlib = new ZLibStream(source, CompressionMode.Decompress);
            using var target = new MemoryStream();
            zlib.CopyTo(target);
            return target.ToArray();
        }

        private static byte[] Deflate(byte[] raw)
        {
            using var target = new MemoryStream();
            using (var zlib = new ZLibStream(target, CompressionLevel.Fastest, leaveOpen: true))
            {
                zlib.Write(raw);
            }

            return target.ToArray();
        }

        private static byte[] BuildHeader(int width, int height)
        {
            var header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
            header[8] = 8;
            header[9] = 6;
            return header;
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            output.Write(length);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes);
            output.Write(data);
            Span<byte> crcBytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crcBytes, Crc32(typeBytes, data));
            output.Write(crcBytes);
        }

        private static uint Crc32(byte[] type, byte[] data)
        {
            var crc = 0xffffffffu;
            foreach (var value in type)
            {
                crc = UpdateCrc(crc, value);
            }

            foreach (var value in data)
            {
                crc = UpdateCrc(crc, value);
            }

            return crc ^ 0xffffffffu;
        }

        private static uint UpdateCrc(uint crc, byte value)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 1 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
            }

            return crc;
        }

        private static void Unfilter(int filter, byte[] current, byte[] previous, int bytesPerPixel)
        {
            for (var index = 0; index < current.Length; index++)
            {
                var left = index >= bytesPerPixel ? current[index - bytesPerPixel] : 0;
                var up = previous[index];
                var upLeft = index >= bytesPerPixel ? previous[index - bytesPerPixel] : 0;
                current[index] = filter switch
                {
                    0 => current[index],
                    1 => (byte)(current[index] + left),
                    2 => (byte)(current[index] + up),
                    3 => (byte)(current[index] + ((left + up) / 2)),
                    4 => (byte)(current[index] + Paeth(left, up, upLeft)),
                    _ => throw new NotSupportedException($"PNG filter {filter} is not supported.")
                };
            }
        }

        private static byte Paeth(int left, int up, int upLeft)
        {
            var estimate = left + up - upLeft;
            var leftDistance = Math.Abs(estimate - left);
            var upDistance = Math.Abs(estimate - up);
            var upLeftDistance = Math.Abs(estimate - upLeft);
            if (leftDistance <= upDistance && leftDistance <= upLeftDistance)
            {
                return (byte)left;
            }

            return upDistance <= upLeftDistance ? (byte)up : (byte)upLeft;
        }

        private static void CopyToRgba(byte[] source, byte[] target, int targetOffset, int colorType)
        {
            for (var sourceOffset = 0; sourceOffset < source.Length;)
            {
                switch (colorType)
                {
                    case 0:
                        target[targetOffset++] = source[sourceOffset];
                        target[targetOffset++] = source[sourceOffset];
                        target[targetOffset++] = source[sourceOffset++];
                        target[targetOffset++] = 255;
                        break;
                    case 2:
                        target[targetOffset++] = source[sourceOffset++];
                        target[targetOffset++] = source[sourceOffset++];
                        target[targetOffset++] = source[sourceOffset++];
                        target[targetOffset++] = 255;
                        break;
                    case 6:
                        target[targetOffset++] = source[sourceOffset++];
                        target[targetOffset++] = source[sourceOffset++];
                        target[targetOffset++] = source[sourceOffset++];
                        target[targetOffset++] = source[sourceOffset++];
                        break;
                }
            }
        }
    }
}

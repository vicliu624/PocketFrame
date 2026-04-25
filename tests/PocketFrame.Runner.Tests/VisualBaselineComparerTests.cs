using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using PocketFrame.Runner;
using Xunit;

namespace PocketFrame.Runner.Tests;

public sealed class VisualBaselineComparerTests
{
    [Fact]
    public async Task CompareIdenticalImagesPassesAndWritesDiff()
    {
        using var directory = new TemporaryDirectory();
        var actual = Path.Combine(directory.Path, "actual.png");
        var baseline = Path.Combine(directory.Path, "baseline.png");
        var diff = Path.Combine(directory.Path, "diff.png");
        await File.WriteAllBytesAsync(actual, PngTestImage.Rgba(2, 1, [255, 0, 0, 255, 0, 255, 0, 255]));
        File.Copy(actual, baseline);

        var result = await new VisualBaselineComparer().CompareAsync(actual, baseline, diff, new VisualBaselineCompareOptions());

        Assert.True(result.Passed);
        Assert.Equal(0, result.ChangedPixels);
        Assert.Equal(2, result.TotalPixels);
        Assert.True(File.Exists(diff));
    }

    [Fact]
    public async Task CompareHonorsThresholdAndPixelTolerance()
    {
        using var directory = new TemporaryDirectory();
        var actual = Path.Combine(directory.Path, "actual.png");
        var baseline = Path.Combine(directory.Path, "baseline.png");
        var diff = Path.Combine(directory.Path, "diff.png");
        await File.WriteAllBytesAsync(actual, PngTestImage.Rgba(2, 1, [100, 100, 100, 255, 200, 200, 200, 255]));
        await File.WriteAllBytesAsync(baseline, PngTestImage.Rgba(2, 1, [104, 100, 100, 255, 255, 255, 255, 255]));

        var result = await new VisualBaselineComparer().CompareAsync(actual, baseline, diff, new VisualBaselineCompareOptions
        {
            Threshold = 0.5,
            PixelTolerance = 8
        });

        Assert.True(result.Passed);
        Assert.Equal(1, result.ChangedPixels);
        Assert.Equal(0.5, result.ChangedRatio);
    }

    [Fact]
    public async Task CompareHonorsRegionsAndIgnoreRegions()
    {
        using var directory = new TemporaryDirectory();
        var actual = Path.Combine(directory.Path, "actual.png");
        var baseline = Path.Combine(directory.Path, "baseline.png");
        var diff = Path.Combine(directory.Path, "diff.png");
        await File.WriteAllBytesAsync(actual, PngTestImage.Rgba(4, 1, [
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 0, 255
        ]));
        await File.WriteAllBytesAsync(baseline, PngTestImage.Rgba(4, 1, [
            255, 0, 0, 255,
            0, 0, 0, 255,
            0, 0, 0, 255,
            255, 255, 0, 255
        ]));

        var result = await new VisualBaselineComparer().CompareAsync(actual, baseline, diff, new VisualBaselineCompareOptions
        {
            Regions = { new VisualRegion(1, 0, 2, 1) },
            IgnoreRegions = { new VisualRegion(2, 0, 1, 1) }
        });

        Assert.False(result.Passed);
        Assert.Equal(1, result.TotalPixels);
        Assert.Equal(3, result.IgnoredPixels);
        Assert.Equal(1, result.ChangedPixels);
    }

    [Fact]
    public async Task CompareSizeMismatchFails()
    {
        using var directory = new TemporaryDirectory();
        var actual = Path.Combine(directory.Path, "actual.png");
        var baseline = Path.Combine(directory.Path, "baseline.png");
        var diff = Path.Combine(directory.Path, "diff.png");
        await File.WriteAllBytesAsync(actual, PngTestImage.Rgba(1, 1, [0, 0, 0, 255]));
        await File.WriteAllBytesAsync(baseline, PngTestImage.Rgba(2, 1, [0, 0, 0, 255, 0, 0, 0, 255]));

        var result = await new VisualBaselineComparer().CompareAsync(actual, baseline, diff, new VisualBaselineCompareOptions());

        Assert.False(result.Passed);
        Assert.Contains("size mismatch", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(diff));
    }

    [Fact]
    public async Task CompareFailsWhenNoPixelsAreSelected()
    {
        using var directory = new TemporaryDirectory();
        var actual = Path.Combine(directory.Path, "actual.png");
        var baseline = Path.Combine(directory.Path, "baseline.png");
        var diff = Path.Combine(directory.Path, "diff.png");
        await File.WriteAllBytesAsync(actual, PngTestImage.Rgba(1, 1, [0, 0, 0, 255]));
        File.Copy(actual, baseline);

        var result = await new VisualBaselineComparer().CompareAsync(actual, baseline, diff, new VisualBaselineCompareOptions
        {
            Regions = { new VisualRegion(9, 9, 1, 1) }
        });

        Assert.False(result.Passed);
        Assert.Equal(0, result.TotalPixels);
        Assert.Contains("No pixels", result.Message);
    }

    [Fact]
    public async Task CompareDecodesRgbAndGrayscalePngs()
    {
        using var directory = new TemporaryDirectory();
        var actual = Path.Combine(directory.Path, "actual.png");
        var baseline = Path.Combine(directory.Path, "baseline.png");
        var diff = Path.Combine(directory.Path, "diff.png");
        await File.WriteAllBytesAsync(actual, PngTestImage.Rgb(1, 1, [7, 7, 7]));
        await File.WriteAllBytesAsync(baseline, PngTestImage.Grayscale(1, 1, [7]));

        var result = await new VisualBaselineComparer().CompareAsync(actual, baseline, diff, new VisualBaselineCompareOptions());

        Assert.True(result.Passed);
    }

    private static class PngTestImage
    {
        private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

        public static byte[] Rgba(int width, int height, byte[] pixels) => Png(width, height, 6, 4, pixels);
        public static byte[] Rgb(int width, int height, byte[] pixels) => Png(width, height, 2, 3, pixels);
        public static byte[] Grayscale(int width, int height, byte[] pixels) => Png(width, height, 0, 1, pixels);

        private static byte[] Png(int width, int height, int colorType, int bytesPerPixel, byte[] pixels)
        {
            var raw = new byte[height * (1 + width * bytesPerPixel)];
            var sourceOffset = 0;
            var targetOffset = 0;
            for (var y = 0; y < height; y++)
            {
                raw[targetOffset++] = 0;
                Buffer.BlockCopy(pixels, sourceOffset, raw, targetOffset, width * bytesPerPixel);
                sourceOffset += width * bytesPerPixel;
                targetOffset += width * bytesPerPixel;
            }

            using var output = new MemoryStream();
            output.Write(Signature);
            WriteChunk(output, "IHDR", Header(width, height, colorType));
            WriteChunk(output, "IDAT", Deflate(raw));
            WriteChunk(output, "IEND", []);
            return output.ToArray();
        }

        private static byte[] Header(int width, int height, int colorType)
        {
            var header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
            header[8] = 8;
            header[9] = (byte)colorType;
            return header;
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

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            output.Write(length);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes);
            output.Write(data);
            Span<byte> crc = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeBytes, data));
            output.Write(crc);
        }

        private static uint Crc32(byte[] type, byte[] data)
        {
            var crc = 0xffffffffu;
            foreach (var value in type.Concat(data))
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) == 1 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
                }
            }

            return crc ^ 0xffffffffu;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PocketFrameVisualTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

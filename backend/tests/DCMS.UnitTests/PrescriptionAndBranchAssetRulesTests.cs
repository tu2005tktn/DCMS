using System.Text;
using DCMS.Infrastructure.Features.Prescriptions;
using DCMS.Infrastructure.Features.Settings;
using Xunit;

namespace DCMS.UnitTests;

public sealed class PrescriptionAndBranchAssetRulesTests
{
    [Theory]
    [InlineData(1, "Một")]
    [InlineData(10, "Mười")]
    [InlineData(15, "Mười lăm")]
    [InlineData(21, "Hai mươi mốt")]
    [InlineData(24, "Hai mươi tư")]
    [InlineData(105, "Một trăm lẻ năm")]
    [InlineData(1005, "Một nghìn không trăm lẻ năm")]
    [InlineData(10000, "Mười nghìn")]
    public void QuantityIsPrintedInVietnameseWords(int value, string expected) =>
        Assert.Equal(expected, VietnameseNumberWords.ToWords(value));

    private static byte[] PngChunk(string type, byte[] data)
    {
        var chunk = new byte[12 + data.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(chunk, 4);
        data.CopyTo(chunk, 8);
        return chunk;
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void PngTextChunksAreRemovedAndImageChunksKept()
    {
        var png = PngSignature.Concat(PngChunk("IHDR", new byte[13])).Concat(PngChunk("tEXt", Encoding.ASCII.GetBytes("Author\0secret")))
            .Concat(PngChunk("IDAT", [1, 2, 3])).Concat(PngChunk("IEND", [])).ToArray();
        Assert.Equal("image/png", BranchAssetStore.Detect(png));
        var cleaned = BranchAssetStore.StripMetadata(png, "image/png")!;
        Assert.DoesNotContain("tEXt", Encoding.ASCII.GetString(cleaned));
        Assert.Contains("IDAT", Encoding.ASCII.GetString(cleaned));
        Assert.Equal(png.Length - 12 - 13, cleaned.Length);
    }

    [Fact]
    public void TruncatedPngIsRejected()
    {
        var png = PngSignature.Concat(PngChunk("IHDR", new byte[13])).ToArray();
        Assert.Null(BranchAssetStore.StripMetadata(png, "image/png"));
    }

    [Fact]
    public void JpegExifSegmentIsRemoved()
    {
        byte[] jpeg = [0xFF, 0xD8,
            0xFF, 0xE1, 0x00, 0x08, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            0xFF, 0xDB, 0x00, 0x04, 0x01, 0x02,
            0xFF, 0xDA, 0x00, 0x04, 0x00, 0x00, 0x11, 0x22, 0xFF, 0x00, 0x33,
            0xFF, 0xD9];
        Assert.Equal("image/jpeg", BranchAssetStore.Detect(jpeg));
        var cleaned = BranchAssetStore.StripMetadata(jpeg, "image/jpeg")!;
        Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(cleaned));
        Assert.Equal(jpeg.Length - 10, cleaned.Length);
        Assert.Equal([0xFF, 0xD9], cleaned[^2..]);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("GIF89a")]
    public void NonRasterContentIsNotDetected(string content) =>
        Assert.Null(BranchAssetStore.Detect(Encoding.UTF8.GetBytes(content)));
}

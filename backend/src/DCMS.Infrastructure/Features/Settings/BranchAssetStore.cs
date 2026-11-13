using System.Buffers.Binary;
using DCMS.Application.Common;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;

namespace DCMS.Infrastructure.Features.Settings;

// Branch logos (docs/specs/S58-BRANCHES-AND-CHAIRS.md §1.5): verified raster bytes only, metadata stripped, private storage.
public sealed class BranchAssetStore(DcmsDbContext db, IConfiguration config)
{
    public const long MaxBytes = 2 * 1024 * 1024;

    public string Root()
    {
        var path = config["BranchAssets:StoragePath"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw Error("MSG144", 503);
        var root = Path.GetFullPath(path);
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
        {
            if ((Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                && (root.Equals(directory.FullName, StringComparison.OrdinalIgnoreCase) || root.StartsWith(directory.FullName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw Error("MSG144", 503);
        }
        Directory.CreateDirectory(root);
        return root;
    }

    private string PathFor(Guid id) => Path.Combine(Root(), id.ToString("N"));

    public static string? Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "image/webp";
        return null;
    }

    // Returns the cleaned bytes or null when the container structure is not valid.
    public static byte[]? StripMetadata(byte[] data, string type) => type switch
    {
        "image/png" => StripPng(data),
        "image/jpeg" => StripJpeg(data),
        "image/webp" => StripWebp(data),
        _ => null
    };

    private static byte[]? StripPng(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 8);
        var offset = 8;
        var sawEnd = false;
        while (offset + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
            if (length > int.MaxValue || offset + 12 + (long)length > data.Length) return null;
            var type = System.Text.Encoding.ASCII.GetString(data, offset + 4, 4);
            var total = 12 + (int)length;
            if (type is not ("tEXt" or "zTXt" or "iTXt" or "eXIf" or "tIME")) output.Write(data, offset, total);
            offset += total;
            if (type == "IEND") { sawEnd = true; break; }
        }
        return sawEnd ? output.ToArray() : null;
    }

    private static byte[]? StripJpeg(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.WriteByte(0xFF); output.WriteByte(0xD8);
        var offset = 2;
        while (offset + 2 <= data.Length)
        {
            if (data[offset] != 0xFF) return null;
            var marker = data[offset + 1];
            if (marker == 0xFF) { offset++; continue; }
            if (marker == 0xD9) { output.Write(data, offset, 2); return output.ToArray(); }
            if (marker is >= 0xD0 and <= 0xD7 or 0x01) { output.Write(data, offset, 2); offset += 2; continue; }
            if (offset + 4 > data.Length) return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2, 2));
            if (length < 2 || offset + 2 + length > data.Length) return null;
            var metadata = marker is >= 0xE1 and <= 0xEF || marker == 0xFE;
            if (!metadata) output.Write(data, offset, 2 + length);
            offset += 2 + length;
            if (marker == 0xDA)
            {
                // Entropy-coded scan data runs until the next non-RST marker; copy it verbatim.
                var start = offset;
                while (offset + 1 < data.Length && !(data[offset] == 0xFF && data[offset + 1] != 0x00 && data[offset + 1] is not (>= 0xD0 and <= 0xD7))) offset++;
                output.Write(data, start, offset - start);
            }
        }
        return null;
    }

    private static byte[]? StripWebp(byte[] data)
    {
        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
        if (riffSize + 8L != data.Length || data.Length < 20) return null;
        using var body = new MemoryStream(data.Length);
        var offset = 12;
        var vp8xOffset = -1;
        while (offset + 8 <= data.Length)
        {
            var type = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4, 4));
            var total = 8 + (long)size + (size % 2);
            if (offset + 8 + (long)size > data.Length) return null;
            if (type is not ("EXIF" or "XMP "))
            {
                if (type == "VP8X") vp8xOffset = (int)body.Position;
                body.Write(data, offset, (int)Math.Min(total, data.Length - offset));
            }
            offset += (int)total;
        }
        var chunks = body.ToArray();
        // VP8X flags: bit 3 = EXIF, bit 2 = XMP.
        if (vp8xOffset >= 0 && vp8xOffset + 8 < chunks.Length) chunks[vp8xOffset + 8] &= unchecked((byte)~0x0C);
        var output = new byte[12 + chunks.Length];
        data.AsSpan(0, 12).CopyTo(output);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4, 4), (uint)(4 + chunks.Length));
        chunks.CopyTo(output, 12);
        return output;
    }

    public async Task WriteAsync(Guid id, byte[] bytes, CancellationToken ct)
    {
        var path = PathFor(id);
        try { await File.WriteAllBytesAsync(path, bytes, ct); }
        catch (IOException) { throw Error("MSG144", 503); }
        catch (UnauthorizedAccessException) { throw Error("MSG144", 503); }
    }

    public void Discard(Guid id)
    {
        try { File.Delete(PathFor(id)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public async Task<(BranchAsset Asset, byte[] Bytes)?> ReadAsync(int branchId, Guid assetId, CancellationToken ct)
    {
        var asset = await db.BranchAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId && x.BranchId == branchId, ct);
        if (asset == null) return null;
        try { return (asset, await File.ReadAllBytesAsync(PathFor(assetId), ct)); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    // Print views embed the logo so prescribers and patients do not need settings access to see it.
    public async Task<string?> DataUrlAsync(int branchId, Guid? assetId, CancellationToken ct)
    {
        if (!assetId.HasValue) return null;
        try
        {
            var found = await ReadAsync(branchId, assetId.Value, ct);
            return found is { } x ? $"data:{x.Asset.ContentType};base64,{Convert.ToBase64String(x.Bytes)}" : null;
        }
        catch (BusinessException) { return null; }
    }
}

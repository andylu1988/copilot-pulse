using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CopilotPulse;

internal static class SkinImages
{
    internal static bool IsImageError(Exception e) => e is IOException or FileFormatException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.COMException or System.Security.SecurityException or InvalidOperationException;

    internal static BitmapSource LoadLocal(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new IOException("请选择本机图片。");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 25 * 1024 * 1024) throw new IOException("请选择不超过 25 MB 的图片。");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
        var frame = decoder.Frames[0];
        int width = frame.PixelWidth, height = frame.PixelHeight;
        if (width < 1 || height < 1 || (long)width * height > 40_000_000 || width > 20000 || height > 20000) throw new IOException("图片尺寸过大，请选择较小的图片。");
        int orientation = 1;
        try
        {
            if (frame.Metadata is BitmapMetadata metadata && metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort value) orientation = value;
        }
        catch (Exception e) when (e is NotSupportedException or System.Runtime.InteropServices.COMException or FileFormatException) { }
        stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream;
        if (width >= height) image.DecodePixelWidth = Math.Min(2560, width); else image.DecodePixelHeight = Math.Min(2560, height);
        image.EndInit(); image.Freeze();
        Transform transform = orientation switch {
            2 => new ScaleTransform(-1, 1), 3 => new RotateTransform(180), 4 => new ScaleTransform(1, -1),
            5 => new TransformGroup { Children = { new ScaleTransform(-1, 1), new RotateTransform(90) } },
            6 => new RotateTransform(90), 7 => new TransformGroup { Children = { new ScaleTransform(-1, 1), new RotateTransform(270) } },
            8 => new RotateTransform(270), _ => Transform.Identity
        };
        if (orientation is >= 2 and <= 8) { var rotated = new TransformedBitmap(image, transform); rotated.Freeze(); return rotated; }
        return image;
    }

    internal static string Import(string source, string? targetDirectory = null)
    {
        string extension = Path.GetExtension(source).ToLowerInvariant();
        if (!new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(extension)) throw new IOException("支持 PNG、JPG 和 BMP 图片。");
        var bitmap = LoadLocal(source);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var memory = new MemoryStream(); encoder.Save(memory);
        byte[] bytes = memory.ToArray();
        string name = "background-" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..24] + ".png";
        string directory = targetDirectory ?? Path.Combine(App.DataDirectory, "skins");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name);
        bool validExisting = File.Exists(path) && new FileInfo(path).Length == bytes.Length && CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(path)), SHA256.HashData(bytes));
        if (!validExisting)
        {
            string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(staging, bytes);
            File.Move(staging, path, true);
        }
        return Path.GetFullPath(path);
    }
}

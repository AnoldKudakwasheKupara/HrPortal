using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace HrPortal.Services.ProviderForms
{
    /// <summary>
    /// Reads the signature a page sends. It has to be a see-through
    /// picture with something drawn on it, so it never hides the form
    /// underneath and an empty box is not taken as signed.
    /// </summary>
    public static class SignatureImage
    {
        public const int MaxBytes = 300 * 1024;

        private const string Prefix = "data:image/png;base64,";

        private static readonly byte[] PngStart = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static byte[]? Read(string? dataUrl)
        {
            if (dataUrl == null ||
                !dataUrl.StartsWith(Prefix, StringComparison.Ordinal) ||
                dataUrl.Length > Prefix.Length + MaxBytes * 4 / 3 + 4)
            {
                return null;
            }

            try
            {
                var bytes = Convert.FromBase64String(dataUrl[Prefix.Length..]);

                return HasInk(bytes) ? bytes : null;
            }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or ArgumentException)
            {
                return null;
            }
        }

        private static bool HasInk(byte[] png)
        {
            if (png.Length < 45 || !png.AsSpan().StartsWith(PngStart))
                return false;

            var width = 0;
            var height = 0;

            using var packed = new MemoryStream();

            for (var at = PngStart.Length; at + 12 <= png.Length;)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at));

                if (length > png.Length - at - 12)
                    return false;

                var type = Encoding.ASCII.GetString(png, at + 4, 4);
                var data = png.AsSpan(at + 8, (int)length);

                if (type == "IHDR")
                {
                    if (length != 13)
                        return false;

                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);

                    var seeThrough = data[8] == 8 && data[9] == 6 && data[12] == 0;

                    if (!seeThrough || width is < 20 or > 1600 || height is < 10 or > 800)
                        return false;
                }
                else if (type == "IDAT")
                {
                    packed.Write(data);
                }

                at += (int)length + 12;
            }

            if (width == 0 || packed.Length == 0)
                return false;

            packed.Position = 0;

            using var pixels = new ZLibStream(packed, CompressionMode.Decompress);

            var row = new byte[1 + width * 4];
            var ink = false;

            for (var y = 0; y < height; y++)
            {
                if (pixels.ReadAtLeast(row, row.Length, throwOnEndOfStream: false) < row.Length)
                    return false;

                ink |= row.AsSpan(1).IndexOfAnyExcept((byte)0) >= 0;
            }

            return ink;
        }
    }
}

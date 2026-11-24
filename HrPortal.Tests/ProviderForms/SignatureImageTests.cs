using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using HrPortal.Services.ProviderForms;

namespace HrPortal.Tests.ProviderForms
{
    public class SignatureImageTests
    {
        private static byte[] Png(int width, int height, bool ink, byte colourType = 6, int? rows = null)
        {
            using var file = new MemoryStream();
            file.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            var header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
            header[8] = 8;
            header[9] = colourType;
            Chunk(file, "IHDR", header);

            using var packed = new MemoryStream();

            using (var zip = new ZLibStream(packed, CompressionLevel.Fastest, true))
            {
                for (var y = 0; y < (rows ?? height); y++)
                {
                    var row = new byte[1 + width * 4];

                    if (ink && y == 1)
                        row[8] = 255;

                    zip.Write(row);
                }
            }

            Chunk(file, "IDAT", packed.ToArray());
            Chunk(file, "IEND", Array.Empty<byte>());

            return file.ToArray();
        }

        private static void Chunk(Stream file, string type, byte[] data)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            file.Write(length);
            file.Write(Encoding.ASCII.GetBytes(type));
            file.Write(data);
            file.Write(new byte[4]);
        }

        private static byte[]? Read(byte[] png) =>
            SignatureImage.Read("data:image/png;base64," + Convert.ToBase64String(png));

        [Fact]
        public void A_drawn_signature_is_kept()
        {
            var png = Png(200, 60, true);

            Assert.Equal(png, Read(png));
        }

        [Fact]
        public void An_empty_box_is_not_a_signature() => Assert.Null(Read(Png(200, 60, false)));

        [Fact]
        public void A_picture_that_could_hide_the_form_is_refused() => Assert.Null(Read(Png(200, 60, true, 2)));

        [Fact]
        public void A_picture_that_is_too_big_or_cut_short_is_refused()
        {
            Assert.Null(Read(Png(5000, 60, true)));
            Assert.Null(Read(Png(200, 60, true, rows: 10)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("data:image/png;base64,")]
        [InlineData("data:image/png;base64,not base64!")]
        [InlineData("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=")]
        [InlineData("https://example.test/signature.png")]
        public void Anything_else_is_refused(string? sent) => Assert.Null(SignatureImage.Read(sent));

        [Fact]
        public void Broken_data_is_refused_rather_than_failing()
        {
            var png = Png(200, 60, true);

            for (var at = 8; at < png.Length; at += 3)
            {
                var broken = (byte[])png.Clone();
                broken[at] ^= 0xFF;
                Read(broken);
            }

            Assert.Null(Read(png[..40]));
        }
    }
}

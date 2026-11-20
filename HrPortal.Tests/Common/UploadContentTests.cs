using System.Text;
using HrPortal.Services;
using Microsoft.AspNetCore.Http;

namespace HrPortal.Tests.Common
{
    public class UploadContentTests
    {
        private static readonly byte[] Program = { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 };

        private static IFormFile File(string name, byte[] content) =>
            new FormFile(new MemoryStream(content), 0, content.Length, "File", name);

        public static TheoryData<string, byte[]> Genuine => new()
        {
            { "cv.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n%...") },
            { "contract.docx", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 } },
            { "old.doc", new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },
            { "photo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            { "photo.JPG", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 } },
            { "notes.txt", Encoding.UTF8.GetBytes("Plain notes\r\n") },
            { "list.csv", Encoding.UTF8.GetBytes("﻿Name,Days\nRudo,4\n") }
        };

        [Theory]
        [MemberData(nameof(Genuine))]
        public void A_file_that_is_what_its_name_says_is_accepted(string name, byte[] content)
        {
            Assert.Null(FileStorageService.Validate(File(name, content)));
        }

        [Theory]
        [InlineData("cv.pdf")]
        [InlineData("contract.docx")]
        [InlineData("photo.jpg")]
        [InlineData("notes.txt")]
        public void A_program_renamed_to_an_allowed_type_is_refused(string name)
        {
            Assert.Contains("does not look like", FileStorageService.Validate(File(name, Program)));
        }

        [Fact]
        public void A_pdf_with_a_few_bytes_before_its_header_is_still_a_pdf()
        {
            var content = Encoding.ASCII.GetBytes("\r\n  %PDF-1.4\n");

            Assert.Null(FileStorageService.Validate(File("scan.pdf", content)));
        }

        [Theory]
        [InlineData("payroll.xls", "<html><table><tr><td>Rudo</td></tr></table></html>")]
        [InlineData("letter.doc", "{\\rtf1\\ansi Dear Rudo}")]
        public void An_older_office_file_written_as_text_by_another_program_is_accepted(string name, string content)
        {
            Assert.Null(FileStorageService.Validate(File(name, Encoding.UTF8.GetBytes(content))));
        }

        [Fact]
        public void A_program_renamed_to_an_older_office_type_is_refused()
        {
            Assert.NotNull(FileStorageService.Validate(File("payroll.xls", Program)));
        }

        [Fact]
        public void A_picture_saved_as_a_word_file_is_refused()
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

            Assert.NotNull(FileStorageService.Validate(File("letter.docx", png)));
        }
    }
}

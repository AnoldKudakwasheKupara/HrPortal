using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Prints a <see cref="RecordDocument"/> in the same chrome as
    /// the signed forms, so a record downloaded from a details page
    /// and a form taken to Finance read as one company's paperwork.
    /// </summary>
    public class RecordDocumentPdfService
    {
        private readonly BrandAssets _brand;

        public RecordDocumentPdfService(BrandAssets brand)
        {
            _brand = brand;
        }

        public byte[] Render(RecordDocument record) =>
            Build(record).GeneratePdf();

        /// <summary>
        /// The document itself, kept separate from how it is
        /// rendered so it can also be exported as images for a
        /// visual check.
        /// </summary>
        public IDocument Build(RecordDocument record)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    DocumentStyle.Apply(page);

                    page.Header().Element(e => DocumentStyle.Head(
                        e, _brand, record.Title, record.Reference));

                    page.Content().Element(e => Content(e, record));

                    page.Footer().Element(e => DocumentStyle.Footer(
                        e, record.Reference));
                });
            });
        }

        private void Content(
            IContainer container,
            RecordDocument record)
        {
            DocumentStyle.Gutters(container)
                .PaddingTop(14)
                .Column(column =>
                {
                    if (!string.IsNullOrWhiteSpace(record.Headline))
                    {
                        column.Item().PaddingBottom(4).Element(e =>
                            DocumentStyle.PersonCard(
                                e,
                                record.Headline!,
                                record.Caption,
                                record.Chip));
                    }

                    var number = 0;

                    foreach (var section in record.Sections)
                    {
                        number++;

                        if (number > 1 || record.Headline != null)
                            column.Item().Element(DocumentStyle.Divider);

                        column.Item().Element(e => DocumentStyle.Section(
                            e,
                            number.ToString("00"),
                            section.Title,
                            section.Subtitle));

                        if (section.Rows.Count == 0)
                            continue;

                        column.Item().PaddingTop(7).Element(e =>
                            DocumentStyle.DetailTable(e, section.Rows));
                    }

                    foreach (var note in record.Notes)
                    {
                        column.Item().PaddingTop(9).Element(e =>
                            DocumentStyle.Note(e, note));
                    }
                });
        }
    }
}

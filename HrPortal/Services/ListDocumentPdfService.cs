using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Prints a <see cref="ListDocument"/> in the same chrome as the
    /// signed forms, so a register taken out of the portal is
    /// company stationery rather than a screenshot of a table.
    /// </summary>
    public class ListDocumentPdfService
    {
        private readonly BrandAssets _brand;

        public ListDocumentPdfService(BrandAssets brand)
        {
            _brand = brand;
        }

        public byte[] Render(ListDocument list) =>
            Build(list).GeneratePdf();

        /// <summary>
        /// The document itself, kept separate from how it is
        /// rendered so it can also be exported as images for a
        /// visual check.
        /// </summary>
        public IDocument Build(ListDocument list)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Landscape. A register is wide, and squeezing
                    // nine columns into portrait wraps every date
                    // and every reference onto two lines.
                    DocumentStyle.Apply(page, landscape: true);

                    page.Header().Element(e => DocumentStyle.Head(
                        e, _brand, list.Title, list.Reference,
                        landscape: true));

                    page.Content().Element(e => Content(e, list));

                    page.Footer().Element(e => DocumentStyle.Footer(
                        e, list.Reference));
                });
            });
        }

        private static void Content(
            IContainer container,
            ListDocument list)
        {
            DocumentStyle.Gutters(container)
                .PaddingTop(14)
                .Column(column =>
                {
                    if (!string.IsNullOrWhiteSpace(list.Caption))
                    {
                        // Only on the first sheet. The caption says
                        // which filter produced the list, and a
                        // continuation sheet repeating it would read
                        // as a second, narrower list.
                        column.Item().ShowOnce()
                            .PaddingBottom(10)
                            .Text(list.Caption)
                            .FontSize(9)
                            .FontColor(DocumentStyle.Muted);
                    }

                    if (list.Totals.Count > 0)
                    {
                        column.Item().ShowOnce()
                            .PaddingBottom(12)
                            .Element(e => DocumentStyle.Tally(
                                e, list.Totals));
                    }

                    column.Item().Element(e => DocumentStyle.ListTable(
                        e,
                        list.Columns,
                        list.Rows,
                        list.EmptyMessage));
                });
        }
    }
}

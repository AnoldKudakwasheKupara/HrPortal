using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Renders the employee next of kin form, laid out like the paper
    /// one so a printed copy can go on the personnel file.
    /// </summary>
    public class NextOfKinPdfService
    {
        private const string Ink = "#1f2933";
        private const string Muted = "#6b7280";
        private const string Rule = "#d9dee5";

        private const string NotRecorded = "--";

        private readonly BrandAssets _brand;

        public NextOfKinPdfService(BrandAssets brand)
        {
            _brand = brand;
        }

        public byte[] Render(Employee employee)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(t => t
                        .FontSize(10)
                        .FontColor(Ink)
                        .FontFamily("Lato"));

                    page.Content().Element(e => Content(e, employee));

                    page.Footer().Element(Footer);
                });
            });

            return document.GeneratePdf();
        }

        private void Content(IContainer container, Employee employee)
        {
            container.Column(column =>
            {
                column.Spacing(14);

                column.Item().Element(Letterhead);

                column.Item()
                    .AlignCenter()
                    .Text("EMPLOYEE NEXT OF KIN FORM")
                    .FontSize(14)
                    .Bold()
                    .LetterSpacing(0.04f);

                column.Item()
                    .Text(
                        "NOTE: It is important that you notify us " +
                        "immediately of any changes in the information " +
                        "provided on this form, as this is the " +
                        "information we will use in the event of an " +
                        "emergency.")
                    .FontSize(9)
                    .FontColor(Muted);

                column.Item().Element(e => Field(e, "EMPLOYEE NAME",
                    $"{employee.FirstName} {employee.LastName}"));

                column.Item().Element(e => Field(e, "DEPARTMENT/DIVISION",
                    employee.Department?.Name));

                column.Item().Element(e => Field(e, "POSITION/TITLE",
                    employee.JobTitle));

                column.Item().Element(e => Field(e, "ADDRESS",
                    employee.Address));

                column.Item().Element(e => Field(e, "PHONE NO.",
                    employee.PhoneNumber));

                column.Item().Element(e => Field(e, "EMAIL ADDRESS",
                    employee.Email));

                column.Item()
                    .PaddingTop(6)
                    .Text("EMERGENCY CONTACT INFORMATION")
                    .Bold()
                    .LetterSpacing(0.03f);

                column.Item().Element(e => Field(e,
                    "EMERGENCY CONTACT NAME",
                    employee.EmergencyContactName));

                column.Item().Element(e => Field(e,
                    "RELATIONSHIP TO EMPLOYEE",
                    employee.EmergencyContactRelationship));

                column.Item().Element(e => Field(e,
                    "ADDRESS (if different)",
                    employee.EmergencyContactAddress));

                column.Item().Element(e => Field(e,
                    "PHONE NUMBER(S)",
                    employee.EmergencyContactPhone));

                column.Item()
                    .PaddingTop(6)
                    .Text("ACKNOWLEDGEMENT")
                    .Bold()
                    .LetterSpacing(0.03f);

                column.Item().Element(e =>
                    Acknowledgement(e, employee));
            });
        }

        /// <summary>
        /// A label over the value, with a rule under it so the printed
        /// page still reads like the form it came from.
        /// </summary>

        /// <summary>
        /// The company logo at the head of the form, falling back to
        /// the name in text where the artwork cannot be read.
        /// </summary>
        private void Letterhead(IContainer container)
        {
            if (!_brand.HasLogo)
            {
                container
                    .AlignCenter()
                    .Text("INTELLEGO INVESTMENT CONSULTANTS (PVT) LTD")
                    .FontSize(11)
                    .Bold();

                return;
            }

            container
                .AlignCenter()
                .Width(190)
                .Image(_brand.Logo!);
        }

        private void Field(IContainer container, string label, string? value)
        {
            container.Column(column =>
            {
                column.Item()
                    .Text(label)
                    .FontSize(8)
                    .FontColor(Muted)
                    .LetterSpacing(0.05f);

                column.Item()
                    .PaddingTop(2)
                    .BorderBottom(1)
                    .BorderColor(Rule)
                    .PaddingBottom(3)
                    .Text(string.IsNullOrWhiteSpace(value)
                        ? NotRecorded
                        : value.Replace("\r\n", "\n").Replace('\r', '\n'));
            });
        }

        /// <summary>
        /// A ruled line with nothing on it, for signing by hand.
        /// </summary>
        private void BlankField(IContainer container, string label)
        {
            container.Column(column =>
            {
                column.Item()
                    .Text(label)
                    .FontSize(8)
                    .FontColor(Muted)
                    .LetterSpacing(0.05f);

                column.Item()
                    .PaddingTop(2)
                    .MinHeight(16)
                    .BorderBottom(1)
                    .BorderColor(Rule);
            });
        }

        private void Acknowledgement(IContainer container, Employee employee)
        {
            var name = $"{employee.FirstName} {employee.LastName}";

            if (employee.NextOfKinConfirmedAt == null)
            {
                container.Column(column =>
                {
                    column.Spacing(16);

                    column.Item()
                        .Text(
                            $"I, {name}, confirm that the above " +
                            "information is accurate and up to date to " +
                            "the best of my knowledge. I understand the " +
                            "importance of notifying the company " +
                            "immediately of any changes.");

                    column.Item().Row(row =>
                    {
                        row.RelativeItem(2).Element(e =>
                            BlankField(e, "EMPLOYEE SIGNATURE"));

                        row.ConstantItem(16);

                        row.RelativeItem().Element(e =>
                            BlankField(e, "DATE"));
                    });

                    column.Item()
                        .Text(
                            "This copy has not been confirmed in the " +
                            "portal. Sign and date it, or complete it " +
                            "under My Profile.")
                        .FontSize(8)
                        .FontColor(Muted);
                });

                return;
            }

            var confirmed = employee.NextOfKinConfirmedAt.Value
                .ToLocalTime();

            container.Column(column =>
            {
                column.Spacing(10);

                column.Item()
                    .Text(
                        $"I, {name}, confirm that the above " +
                        "information is accurate and up to date to the " +
                        "best of my knowledge. I understand the " +
                        "importance of notifying the company " +
                        "immediately of any changes.");

                column.Item()
                    .BorderLeft(2)
                    .BorderColor(Rule)
                    .PaddingLeft(8)
                    .Text(
                        "Confirmed in the portal by " +
                        $"{name} on " +
                        $"{confirmed:d MMMM yyyy} at {confirmed:HH:mm}.")
                    .FontSize(9);
            });
        }

        private void Footer(IContainer container)
        {
            container
                .PaddingTop(10)
                .BorderTop(1)
                .BorderColor(Rule)
                .PaddingTop(6)
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text($"Printed {DateTime.Now:d MMMM yyyy}")
                        .FontSize(8)
                        .FontColor(Muted);

                    row.RelativeItem()
                        .AlignRight()
                        .Text(t =>
                        {
                            t.DefaultTextStyle(s => s
                                .FontSize(8)
                                .FontColor(Muted));

                            t.Span("Page ");
                            t.CurrentPageNumber();
                            t.Span(" of ");
                            t.TotalPages();
                        });
                });
        }
    }
}

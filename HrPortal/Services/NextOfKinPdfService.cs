using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Renders the employee next of kin form, laid out like the paper
    /// one so a printed copy can go on the personnel file. The chrome
    /// comes from <see cref="DocumentStyle"/>.
    /// </summary>
    public class NextOfKinPdfService
    {
        private const string Title = "Next of Kin";

        private readonly BrandAssets _brand;

        public NextOfKinPdfService(BrandAssets brand)
        {
            _brand = brand;
        }

        public byte[] Render(Employee employee) =>
            Build(employee).GeneratePdf();

        /// <summary>
        /// The document itself, kept separate from how it is
        /// rendered so it can also be exported as images for a
        /// visual check.
        /// </summary>
        public IDocument Build(Employee employee)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    DocumentStyle.Apply(page);

                    page.Content().Element(e => Content(e, employee));

                    page.Footer().Element(e => DocumentStyle.Footer(
                        e, employee.EmployeeNumber));
                });
            });
        }

        private void Content(IContainer container, Employee employee)
        {
            container.Column(column =>
            {
                column.Spacing(7);

                column.Item().Element(e => DocumentStyle.Letterhead(
                    e, _brand, Title, employee.EmployeeNumber));

                column.Item().Element(e => DocumentStyle.Callout(
                    e,
                    "It is important that you notify us immediately " +
                    "of any changes to the information on this form. " +
                    "This is what we will use in an emergency."));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "1", "Employee"));

                column.Item().Element(e => DocumentStyle.DetailTable(
                    e, new (string, string?)[]
                    {
                        ("Employee name",
                            $"{employee.FirstName} {employee.LastName}"),
                        ("Department / division",
                            employee.Department?.Name),
                        ("Position / title", employee.JobTitle),
                        ("Address", employee.Address),
                        ("Phone no.", employee.PhoneNumber),
                        ("Email address", employee.Email)
                    }));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "2", "Emergency contact"));

                column.Item().Element(e => DocumentStyle.DetailTable(
                    e, new (string, string?)[]
                    {
                        ("Emergency contact name",
                            employee.EmergencyContactName),
                        ("Relationship to employee",
                            employee.EmergencyContactRelationship),
                        ("Address (if different)",
                            employee.EmergencyContactAddress),
                        ("Phone number(s)",
                            employee.EmergencyContactPhone)
                    }));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "3", "Acknowledgement"));

                column.Item().Element(e =>
                    Acknowledgement(e, employee));
            });
        }

        private void Acknowledgement(
            IContainer container,
            Employee employee)
        {
            var name = $"{employee.FirstName} {employee.LastName}";

            const string Declaration =
                "confirm that the above information is accurate and " +
                "up to date to the best of my knowledge. I understand " +
                "the importance of notifying the company immediately " +
                "of any changes.";

            if (employee.NextOfKinConfirmedAt == null)
            {
                container.Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text(text =>
                    {
                        text.DefaultTextStyle(s => s.LineHeight(1.45f));

                        text.Span("I, ");
                        text.Span(name).SemiBold();
                        text.Span(", " + Declaration);
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem(2).Element(e =>
                            DocumentStyle.Blank(
                                e, "Employee signature"));

                        row.ConstantItem(18);

                        row.RelativeItem().Element(e =>
                            DocumentStyle.Blank(e, "Date"));
                    });

                    column.Item().Element(e => DocumentStyle.Note(
                        e,
                        "This copy has not been confirmed in the " +
                        "portal. Sign and date it, or complete it " +
                        "under My Profile."));
                });

                return;
            }

            var confirmed = employee.NextOfKinConfirmedAt.Value
                .ToLocalTime();

            container.Column(column =>
            {
                column.Spacing(8);

                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(s => s.LineHeight(1.45f));

                    text.Span("I, ");
                    text.Span(name).SemiBold();
                    text.Span(", " + Declaration);
                });

                column.Item().Element(e => DocumentStyle.Note(
                    e,
                    $"Confirmed in the portal by {name} on " +
                    $"{confirmed:d MMMM yyyy} at {confirmed:HH:mm}."));
            });
        }
    }
}

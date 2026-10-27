using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Renders a course application as the company's CFAI courses
    /// form, which HR prints once it is approved and takes to
    /// Finance. The chrome comes from <see cref="DocumentStyle"/>;
    /// what is here is only what this particular form says.
    /// </summary>
    public class CourseApplicationPdfService
    {
        private const string Title = "Courses Application";

        private readonly BrandAssets _brand;

        public CourseApplicationPdfService(BrandAssets brand)
        {
            _brand = brand;
        }

        public byte[] Render(CourseApplication application) =>
            Build(application).GeneratePdf();

        /// <summary>
        /// The document itself, kept separate from how it is
        /// rendered so it can also be exported as images for a
        /// visual check.
        /// </summary>
        public IDocument Build(CourseApplication application)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    DocumentStyle.Apply(page);

                    page.Content().Element(e => Content(e, application));

                    page.Footer().Element(e => DocumentStyle.Footer(
                        e, application.ApplicationNumber));
                });
            });
        }

        private void Content(
            IContainer container,
            CourseApplication application)
        {
            var who = $"{application.Employee.FirstName} " +
                      $"{application.Employee.LastName}";

            container.Column(column =>
            {
                column.Spacing(6);

                column.Item().Element(e => DocumentStyle.Letterhead(
                    e, _brand, Title, application.ApplicationNumber));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "1", "Employee details"));

                column.Item().Element(e => Details(e, application));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "2", "Program intended"));

                column.Item().Element(e => Headline(
                    e, application.ProgramIntended));

                column.Item().Element(e => DocumentStyle.Field(
                    e,
                    "2.1  Justification and relevance",
                    application.Justification));

                column.Item().Element(e => DocumentStyle.Field(
                    e,
                    "2.2  Have you sat for the program before?",
                    application.HasSatBefore ? "Yes" : "No"));

                column.Item().Element(e => DocumentStyle.Field(
                    e,
                    "2.2.1  If yes, proceeding to the next level or " +
                    "re-writing?",
                    application.HasSatBefore
                        ? application.Sitting switch
                        {
                            CourseSitting.NextLevel =>
                                "Proceeding to the next level",
                            CourseSitting.Rewriting => "Re-writing",
                            _ => null
                        }
                        : "Not applicable"));

                column.Item().Element(e => DocumentStyle.Field(
                    e,
                    "2.3  Target date to sit for exam",
                    application.TargetExamDate?
                        .ToString("d MMMM yyyy")));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "3", "Pledge"));

                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(s => s.LineHeight(1.4f));

                    text.Span("I, ");
                    text.Span(who).SemiBold();
                    text.Span(", confirm that I will dedicate the " +
                              "requisite hours and effort towards the " +
                              "selected studies and will abide by the " +
                              "Intellego Educational Policy.");
                });

                column.Item().Element(e => Pledge(e, application, who));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "4", "Approval"));

                column.Item().Element(e => Outcome(e, application));

                column.Item().Element(e => Signatures(e, application));
            });
        }

        /// <summary>
        /// The one answer the whole form turns on, set larger than
        /// the fields around it so it is the first thing read.
        /// </summary>
        private void Headline(IContainer container, string? value)
        {
            container
                .BorderBottom(0.8f)
                .BorderColor(DocumentStyle.Rule)
                .PaddingBottom(5)
                .Text(string.IsNullOrWhiteSpace(value)
                    ? DocumentStyle.NotRecorded
                    : value)
                .FontSize(12)
                .SemiBold()
                .FontColor(DocumentStyle.Ink);
        }

        private void Details(
            IContainer container,
            CourseApplication application)
        {
            var employee = application.Employee;

            DocumentStyle.DetailTable(container, new (string, string?)[]
            {
                ("First name(s)", employee.FirstName),
                ("Surname", employee.LastName),
                ("I.D no.", employee.NationalId),
                ("Department", employee.Department?.Name),
                ("Position", employee.JobTitle),
                ("Address", employee.Address),
                ("Phone number", employee.PhoneNumber)
            });
        }

        private void Pledge(
            IContainer container,
            CourseApplication application,
            string who)
        {
            container.Column(column =>
            {
                column.Spacing(5);

                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Element(e =>
                        DocumentStyle.Blank(e, "Signature"));

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e =>
                        DocumentStyle.Blank(e, "Date"));
                });

                if (application.PledgedAt == null)
                    return;

                column.Item().Element(e => DocumentStyle.Note(
                    e,
                    $"Pledged in the portal by {who} on " +
                    application.PledgedAt.Value.ToLocalTime()
                        .ToString("d MMMM yyyy")));
            });
        }

        /// <summary>
        /// The approved and not approved boxes on the form, with the
        /// one that applies marked.
        /// </summary>
        private void Outcome(
            IContainer container,
            CourseApplication application)
        {
            var approved =
                application.Status == CourseApplicationStatus.Approved;

            var declined =
                application.Status is CourseApplicationStatus.Rejected
                    or CourseApplicationStatus.HrRejected;

            container.Row(row =>
            {
                row.RelativeItem().Element(e =>
                    DocumentStyle.TickBox(e, "Approved", approved));

                row.ConstantItem(18);

                row.RelativeItem().Element(e =>
                    DocumentStyle.TickBox(e, "Not approved", declined));
            });
        }

        /// <summary>
        /// The form's three signature blocks. The manager's has no
        /// counterpart in the portal and always prints blank; the
        /// other two carry whoever decided, where they have.
        /// </summary>
        private void Signatures(
            IContainer container,
            CourseApplication application)
        {
            var hr = Decision(
                application,
                CourseApplicationAction.ApprovedByHr,
                CourseApplicationAction.RejectedByHr);

            var consultant = Decision(
                application,
                CourseApplicationAction.ApprovedByManagingConsultant,
                CourseApplicationAction.RejectedByManagingConsultant);

            container.Column(column =>
            {
                column.Spacing(7);

                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Element(e =>
                        DocumentStyle.Blank(e, "Manager / supervisor"));

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e =>
                        DocumentStyle.Blank(e, "Date"));
                });

                column.Item().Element(e => Approver(
                    e, "Human resources", hr));

                column.Item().Element(e => Approver(
                    e, "Managing consultant", consultant));
            });
        }

        private CourseApplicationHistory? Decision(
            CourseApplication application,
            CourseApplicationAction approved,
            CourseApplicationAction rejected) =>
            application.History
                .Where(h => h.Action == approved || h.Action == rejected)
                .OrderByDescending(h => h.ActionDate)
                .FirstOrDefault();

        private void Approver(
            IContainer container,
            string label,
            CourseApplicationHistory? decision)
        {
            container.ShowEntire().Column(column =>
            {
                column.Spacing(4);

                column.Item()
                    .Text(label.ToUpperInvariant())
                    .FontSize(8.5f)
                    .Bold()
                    .FontColor(DocumentStyle.Green)
                    .LetterSpacing(0.05f);

                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Element(e =>
                    {
                        if (decision?.ActionedByEmployee == null)
                        {
                            DocumentStyle.Blank(
                                e, "Approver's signature");
                            return;
                        }

                        DocumentStyle.Field(
                            e,
                            "Approver's signature",
                            $"{decision.ActionedByEmployee.FirstName} " +
                            $"{decision.ActionedByEmployee.LastName}");
                    });

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e =>
                    {
                        if (decision == null)
                        {
                            DocumentStyle.Blank(e, "Date");
                            return;
                        }

                        DocumentStyle.Field(
                            e,
                            "Date",
                            decision.ActionDate.ToLocalTime()
                                .ToString("d MMMM yyyy"));
                    });
                });

                if (!string.IsNullOrWhiteSpace(decision?.Comment))
                {
                    column.Item().Element(e => DocumentStyle.Note(
                        e, decision!.Comment!));
                }
            });
        }
    }
}

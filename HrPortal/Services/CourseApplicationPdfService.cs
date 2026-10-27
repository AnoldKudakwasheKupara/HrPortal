using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Renders a course application as the company's CFAI courses
    /// form, which HR prints once it is approved and takes to
    /// Finance. Laid out the way the portal shows it, so the printed
    /// copy and the screen read as one document.
    /// </summary>
    public class CourseApplicationPdfService
    {
        private const string Title = "Courses Application";

        /// <summary>Matches the limit the portal puts on the box.</summary>
        private const int JustificationLimit = 500;

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

                    page.Header().Element(e => DocumentStyle.Head(
                        e, _brand, Title, application.ApplicationNumber));

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
            var employee = application.Employee;

            var who = $"{employee.FirstName} {employee.LastName}";

            DocumentStyle.Gutters(container)
                .PaddingTop(8)
                .Column(column =>
                {
                    column.Item().Element(e => DocumentStyle.Section(
                        e,
                        "01",
                        "Employee details",
                        "Your current employment information"));

                    column.Item().PaddingTop(5).Element(e =>
                        Employee(e, employee, who));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e,
                        "02",
                        "Program intended",
                        "Details of the course or certification you " +
                        "intend to pursue"));

                    column.Item().PaddingTop(5).Element(e =>
                        Program(e, application));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e,
                        "03",
                        "Pledge",
                        "Agreement to commit to the programme"));

                    column.Item().PaddingTop(5).Element(e =>
                        Pledge(e, application, who));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e,
                        "04",
                        "Approval",
                        "To be completed by the relevant approvers"));

                    column.Item().PaddingTop(5).Element(e =>
                        Approval(e, application));
                });
        }

        /// <summary>
        /// Who the form is about: the card on the left names them,
        /// the table on the right carries what the record holds.
        /// </summary>
        private void Employee(
            IContainer container,
            Employee employee,
            string who)
        {
            container.Row(row =>
            {
                row.RelativeItem(42).AlignTop().Element(e =>
                    DocumentStyle.PersonCard(
                        e,
                        who,
                        employee.Department?.Name,
                        string.IsNullOrWhiteSpace(employee.JobTitle)
                            ? "Employee"
                            : employee.JobTitle));

                row.ConstantItem(24);

                row.RelativeItem(58).Element(e =>
                    DocumentStyle.DetailTable(e, new (string, string?)[]
                    {
                        ("First name(s)", employee.FirstName),
                        ("Surname", employee.LastName),
                        ("I.D No.", employee.NationalId),
                        ("Department", employee.Department?.Name),
                        ("Position", employee.JobTitle),
                        ("Address", employee.Address),
                        ("Phone number", employee.PhoneNumber)
                    }));
            });
        }

        private void Program(
            IContainer container,
            CourseApplication application)
        {
            container.Column(column =>
            {
                column.Spacing(9);

                column.Item().Element(e => DocumentStyle.Field(
                    e,
                    "Program / Course",
                    application.ProgramIntended,
                    required: true,
                    trailingIcon: Icons.Chevron));

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Element(e => DocumentStyle.Label(
                            e,
                            "2.1 Justification and relevance",
                            required: true));

                        left.Item().PaddingTop(4).Element(e =>
                            DocumentStyle.Box(
                                e,
                                application.Justification,
                                minHeight: 42));

                        left.Item().Element(e => DocumentStyle.Counter(
                            e,
                            application.Justification,
                            JustificationLimit));
                    });

                    row.ConstantItem(24);

                    row.RelativeItem().Column(right =>
                    {
                        right.Item().Element(e => DocumentStyle.Label(
                            e,
                            "2.2 Have you sat for the program before?",
                            required: true));

                        right.Item().PaddingTop(10).Element(e =>
                            DocumentStyle.Choice(e, new[]
                            {
                                ("Yes", application.HasSatBefore),
                                ("No", !application.HasSatBefore)
                            }));
                    });
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "2.2.1 If yes, proceeding to the next level " +
                        "or re-writing?",
                        application.HasSatBefore
                            ? application.Sitting switch
                            {
                                CourseSitting.NextLevel =>
                                    "Proceeding to the next level",
                                CourseSitting.Rewriting => "Re-writing",
                                _ => null
                            }
                            : null,
                        // The portal greys this out when the answer
                        // above is no, and so does the paper.
                        dead: !application.HasSatBefore));

                    row.ConstantItem(24);

                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "2.3 Target date to sit for exam",
                        application.TargetExamDate?
                            .ToString("d MMMM yyyy"),
                        required: true,
                        icon: Icons.Calendar));
                });
            });
        }

        private void Pledge(
            IContainer container,
            CourseApplication application,
            string who)
        {
            container.Column(column =>
            {
                column.Item().PaddingLeft(37).Element(e =>
                    DocumentStyle.Callout(e, text =>
                    {
                        text.Span("I, ");
                        text.Span(who).Bold().FontColor(DocumentStyle.Green);
                        text.Span(", confirm that I will dedicate the " +
                                  "requisite hours and effort towards " +
                                  "the selected studies and will abide " +
                                  "by the Intellego Educational Policy.");
                    }));

                column.Item().PaddingTop(10).PaddingLeft(37).Row(labels =>
                {
                    labels.RelativeItem().Element(e =>
                        DocumentStyle.Label(e, "Signature", true));

                    labels.ConstantItem(24);

                    labels.RelativeItem().Element(e =>
                        DocumentStyle.Label(e, "Date", true));
                });

                column.Item().PaddingTop(4).PaddingLeft(37).Row(row =>
                {
                    // Pledging in the portal is the applicant's
                    // signature, so their name stands in it. An
                    // application that was never pledged leaves the
                    // box empty for a pen, rather than printing a
                    // name nobody put there.
                    row.RelativeItem().Element(e => DocumentStyle.Box(
                        e,
                        application.PledgedAt == null ? null : who,
                        "Sign here",
                        Icons.Pen));

                    row.ConstantItem(24);

                    row.RelativeItem().Element(e => DocumentStyle.Box(
                        e,
                        DocumentStyle.Stamp(
                            application.SubmittedAt
                            ?? application.PledgedAt),
                        "Select date",
                        Icons.Calendar));
                });
            });
        }

        /// <summary>
        /// The outcome boxes and the three signature lines. The
        /// manager's has no counterpart in the portal and always
        /// prints blank; the other two carry whoever decided, where
        /// they have.
        /// </summary>
        private void Approval(
            IContainer container,
            CourseApplication application)
        {
            var approved =
                application.Status == CourseApplicationStatus.Approved;

            var declined =
                application.Status is CourseApplicationStatus.Rejected
                    or CourseApplicationStatus.HrRejected;

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
                column.Spacing(4);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e =>
                        DocumentStyle.Outcome(e, "Approved", approved));

                    row.ConstantItem(24);

                    row.RelativeItem().Element(e => DocumentStyle.Outcome(
                        e, "Not approved", declined, adverse: true));
                });

                column.Item().PaddingTop(3).Element(e =>
                    DocumentStyle.SignatureRow(
                        e, "Manager / Supervisor", null, null));

                column.Item().Element(e => Approver(
                    e, "Human Resources", hr));

                column.Item().Element(e => Approver(
                    e, "Managing Consultant", consultant));

                var comments = new[] { hr, consultant }
                    .Where(d => !string.IsNullOrWhiteSpace(d?.Comment))
                    .Select(d => d!.Comment!)
                    .ToList();

                foreach (var comment in comments)
                {
                    column.Item().PaddingTop(2).Element(e =>
                        DocumentStyle.Note(e, comment));
                }
            });
        }

        private void Approver(
            IContainer container,
            string role,
            CourseApplicationHistory? decision)
        {
            var signedBy = decision?.ActionedByEmployee == null
                ? null
                : $"{decision.ActionedByEmployee.FirstName} " +
                  $"{decision.ActionedByEmployee.LastName}";

            var date = DocumentStyle.Stamp(decision?.ActionDate);

            DocumentStyle.SignatureRow(container, role, signedBy, date);
        }

        private CourseApplicationHistory? Decision(
            CourseApplication application,
            CourseApplicationAction approved,
            CourseApplicationAction rejected) =>
            application.History
                .Where(h => h.Action == approved || h.Action == rejected)
                .OrderByDescending(h => h.ActionDate)
                .FirstOrDefault();
    }
}

using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Renders a course application as the company's CFAI courses
    /// form, which HR prints once it is approved and takes to
    /// Finance.
    /// </summary>
    public class CourseApplicationPdfService
    {
        private const string Ink = "#1f2933";
        private const string Muted = "#6b7280";
        private const string Rule = "#d9dee5";
        private const string Wash = "#f3f5f7";

        public byte[] Render(CourseApplication application)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.4f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t
                        .FontSize(9)
                        .FontColor(Ink)
                        .FontFamily("Lato"));

                    page.Content().Element(e => Content(e, application));

                    page.Footer().Element(e => Footer(e, application));
                });
            });

            return document.GeneratePdf();
        }

        private void Content(
            IContainer container,
            CourseApplication application)
        {
            var who = $"{application.Employee.FirstName} " +
                      $"{application.Employee.LastName}";

            container.Column(column =>
            {
                column.Spacing(7);

                column.Item().AlignCenter()
                    .Text("INTELLEGO INVESTMENT CONSULTANTS (PVT) LTD")
                    .FontSize(10.5f)
                    .Bold();

                column.Item().AlignCenter()
                    .Text("COURSES APPLICATION FORM")
                    .FontSize(12)
                    .Bold()
                    .LetterSpacing(0.04f);

                column.Item().Element(Heading1);

                column.Item().Element(e => Details(e, application));

                column.Item().Element(e => Field(
                    e,
                    "2. PROGRAM INTENDED",
                    application.ProgramIntended));

                column.Item().Element(e => Field(
                    e,
                    "2.1 JUSTIFICATION AND RELEVANCE",
                    application.Justification));

                column.Item().Element(e => Field(
                    e,
                    "2.2 HAVE YOU SAT FOR THE PROGRAM BEFORE?",
                    application.HasSatBefore ? "YES" : "NO"));

                column.Item().Element(e => Field(
                    e,
                    "2.2.1 IF YES, PROCEEDING TO THE NEXT LEVEL OR " +
                    "RE-WRITING?",
                    application.HasSatBefore
                        ? application.Sitting switch
                        {
                            CourseSitting.NextLevel =>
                                "Proceeding to the next level",
                            CourseSitting.Rewriting => "Re-writing",
                            _ => null
                        }
                        : "Not applicable"));

                column.Item().Element(e => Field(
                    e,
                    "2.3 TARGET DATE TO SIT FOR EXAM",
                    application.TargetExamDate?
                        .ToString("d MMMM yyyy")));

                column.Item().PaddingTop(4)
                    .Text("3. PLEDGE")
                    .Bold()
                    .LetterSpacing(0.03f);

                column.Item().Text(text =>
                {
                    text.Span("I, ");
                    text.Span(who).Bold();
                    text.Span(", confirm that I will dedicate the " +
                              "requisite hours and effort towards the " +
                              "selected studies and will abide by the " +
                              "Intellego Educational Policy.");
                });

                column.Item().Element(e => Pledge(e, application, who));

                column.Item().PaddingTop(4)
                    .Text("4. APPROVAL SECTION")
                    .Bold()
                    .LetterSpacing(0.03f);

                column.Item().Element(e => Outcome(e, application));

                column.Item().Element(e => Signatures(e, application));
            });
        }

        private void Heading1(IContainer container)
        {
            container
                .Text("1. EMPLOYEE DETAILS")
                .Bold()
                .LetterSpacing(0.03f);
        }

        private void Details(
            IContainer container,
            CourseApplication application)
        {
            var employee = application.Employee;

            var rows = new (string Label, string? Value)[]
            {
                ("FIRST NAME(S)", employee.FirstName),
                ("SURNAME", employee.LastName),
                ("I.D NO.", employee.NationalId),
                ("DEPARTMENT", employee.Department?.Name),
                ("POSITION", employee.JobTitle),
                ("ADDRESS", employee.Address),
                ("PHONE NUMBER", employee.PhoneNumber)
            };

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(130);
                    columns.RelativeColumn();
                });

                foreach (var row in rows)
                {
                    table.Cell()
                        .Border(1)
                        .BorderColor(Rule)
                        .Background(Wash)
                        .Padding(3.5f)
                        .Text(row.Label)
                        .FontSize(8)
                        .FontColor(Muted);

                    table.Cell()
                        .Border(1)
                        .BorderColor(Rule)
                        .Padding(3.5f)
                        .Text(string.IsNullOrWhiteSpace(row.Value)
                            ? "--"
                            : row.Value);
                }
            });
        }

        private void Pledge(
            IContainer container,
            CourseApplication application,
            string who)
        {
            container.Column(column =>
            {
                column.Spacing(4);

                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Element(e =>
                        Blank(e, "SIGNATURE"));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e => Blank(e, "DATE"));
                });

                if (application.PledgedAt == null)
                    return;

                column.Item()
                    .Text($"Pledged in the portal by {who} on " +
                          application.PledgedAt.Value.ToLocalTime()
                              .ToString("d MMMM yyyy"))
                    .FontSize(8)
                    .FontColor(Muted);
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
                    Box(e, "APPROVED", approved));

                row.ConstantItem(16);

                row.RelativeItem().Element(e =>
                    Box(e, "NOT APPROVED", declined));
            });
        }

        private void Box(IContainer container, string label, bool marked)
        {
            container
                .Border(1)
                .BorderColor(Rule)
                .Background(marked ? Wash : Colors.White)
                .Padding(6)
                .Row(row =>
                {
                    row.ConstantItem(16)
                        .Text(marked ? "X" : " ")
                        .Bold();

                    row.RelativeItem()
                        .Text(label)
                        .FontSize(9)
                        .Bold();
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
                        Blank(e, "MANAGER/SUPERVISOR"));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e => Blank(e, "DATE"));
                });

                column.Item().Element(e => Approver(
                    e, "HUMAN RESOURCES", hr));

                column.Item().Element(e => Approver(
                    e, "MANAGING CONSULTANT", consultant));
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
                column.Spacing(3);

                column.Item()
                    .Text(label + ":")
                    .FontSize(9)
                    .Bold();

                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Element(e =>
                    {
                        if (decision?.ActionedByEmployee == null)
                        {
                            Blank(e, "APPROVER'S SIGNATURE");
                            return;
                        }

                        Field(
                            e,
                            "APPROVER'S SIGNATURE",
                            $"{decision.ActionedByEmployee.FirstName} " +
                            $"{decision.ActionedByEmployee.LastName}");
                    });

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e =>
                    {
                        if (decision == null)
                        {
                            Blank(e, "DATE");
                            return;
                        }

                        Field(
                            e,
                            "DATE",
                            decision.ActionDate.ToLocalTime()
                                .ToString("d MMMM yyyy"));
                    });
                });

                if (!string.IsNullOrWhiteSpace(decision?.Comment))
                {
                    column.Item()
                        .Text(decision!.Comment)
                        .FontSize(8)
                        .FontColor(Muted);
                }
            });
        }

        private void Field(
            IContainer container,
            string label,
            string? value)
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
                    .Text(string.IsNullOrWhiteSpace(value) ? "--" : value);
            });
        }

        private void Blank(IContainer container, string label)
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

        private void Footer(
            IContainer container,
            CourseApplication application)
        {
            container
                .PaddingTop(8)
                .BorderTop(1)
                .BorderColor(Rule)
                .PaddingTop(5)
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text($"{application.ApplicationNumber} - " +
                              $"printed {DateTime.Now:d MMMM yyyy}")
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

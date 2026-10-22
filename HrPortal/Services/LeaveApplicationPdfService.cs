using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Renders a leave request as the company's leave application
    /// form, so a printed copy reads like the paper one it replaced.
    /// </summary>
    public class LeaveApplicationPdfService
    {
        private const string Ink = "#1f2933";
        private const string Muted = "#6b7280";
        private const string Rule = "#d9dee5";
        private const string Box = "#f3f5f7";

        private readonly ApplicationDbContext _context;

        private readonly BrandAssets _brand;

        public LeaveApplicationPdfService(
            ApplicationDbContext context,
            BrandAssets brand)
        {
            _context = context;
            _brand = brand;
        }

        /// <summary>
        /// Looks up what the form prints beside the request itself:
        /// every leave type, so the boxes match the paper form, and
        /// the balance behind the type applied for.
        /// </summary>
        public async Task<byte[]> RenderAsync(LeaveRequest request)
        {
            var leaveTypes = await _context.LeaveTypes
                .AsNoTracking()
                .Where(l => l.IsActive || l.Id == request.LeaveTypeId)
                .OrderBy(l => l.Name)
                .ToListAsync();

            var balance = await _context.LeaveBalances
                .AsNoTracking()
                .FirstOrDefaultAsync(b =>
                    b.EmployeeId == request.EmployeeId &&
                    b.LeaveTypeId == request.LeaveTypeId &&
                    b.Year == request.StartDate.Year);

            return Render(request, leaveTypes, balance);
        }

        private byte[] Render(
            LeaveRequest request,
            IReadOnlyList<LeaveType> leaveTypes,
            LeaveBalance? balance)
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

                    page.Content().Element(e =>
                        Content(e, request, leaveTypes, balance));

                    page.Footer().Element(e => Footer(e, request));
                });
            });

            return document.GeneratePdf();
        }

        private void Content(
            IContainer container,
            LeaveRequest request,
            IReadOnlyList<LeaveType> leaveTypes,
            LeaveBalance? balance)
        {
            container.Column(column =>
            {
                column.Spacing(12);

                column.Item().Element(Letterhead);

                column.Item().AlignCenter()
                    .Text("LEAVE APPLICATION FORM")
                    .FontSize(13)
                    .Bold()
                    .LetterSpacing(0.04f);

                column.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Element(e => Field(e,
                        "FIRST NAME", request.Employee.FirstName));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e => Field(e,
                        "SURNAME", request.Employee.LastName));
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => Field(e,
                        "EMPLOYEE NUMBER",
                        request.Employee.EmployeeNumber));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e => Field(e,
                        "DEPARTMENT",
                        request.Employee.Department?.Name));
                });

                column.Item().PaddingTop(6)
                    .Text("TYPE OF LEAVE - USE APPROPRIATE BOX (IN DAYS)")
                    .Bold()
                    .FontSize(9)
                    .LetterSpacing(0.03f);

                column.Item().Element(e =>
                    Boxes(e, request, leaveTypes, balance));

                column.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Element(e => Field(e,
                        "DATE FROM",
                        request.StartDate.ToString("d MMMM yyyy")));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e => Field(e,
                        "TO",
                        request.EndDate.ToString("d MMMM yyyy")));
                });

                if (!string.IsNullOrWhiteSpace(request.Reason))
                {
                    column.Item().Element(e =>
                        Field(e, "REASON", request.Reason));
                }

                column.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem(2).Element(e =>
                        Field(e, "SIGNATURE OF APPLICANT",
                            $"Submitted in the portal by " +
                            $"{request.Employee.FirstName} " +
                            $"{request.Employee.LastName}"));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e => Field(e, "DATE",
                        request.SubmittedAt.ToLocalTime()
                            .ToString("d MMMM yyyy")));
                });

                column.Item().PaddingTop(6)
                    .Text("AUTHORISED BY (H.O.D OR MANAGER)")
                    .Bold()
                    .FontSize(9)
                    .LetterSpacing(0.03f);

                column.Item().Element(e => Authorisations(e, request));

                column.Item().PaddingTop(6)
                    .Text("For official use")
                    .Bold()
                    .FontSize(9);

                column.Item().Element(e => Blank(e,
                    "Does the applicant qualify to go on leave? Yes or no"));

                column.Item().Element(e => Blank(e, "Reason"));
            });
        }

        /// <summary>
        /// The leave type boxes. Only the type applied for carries a
        /// figure, since an application covers one type; the rest are
        /// left blank as they are on the paper form.
        /// </summary>
        private void Boxes(
            IContainer container,
            LeaveRequest request,
            IReadOnlyList<LeaveType> leaveTypes,
            LeaveBalance? balance)
        {
            var rows = leaveTypes
                .Select(t => (
                    Label: t.Name.ToUpperInvariant(),
                    Value: t.Id == request.LeaveTypeId
                        ? request.NumberOfDays.ToString("0.##")
                        : null))
                .ToList();

            rows.Add(("TOTAL LEAVE DAYS TAKEN",
                balance?.UsedDays.ToString("0.##")));

            rows.Add(("TOTAL LEAVE DAYS ACCRUED",
                balance?.AllocatedDays.ToString("0.##")));

            rows.Add(("LEAVE DAYS BALANCE",
                balance?.RemainingDays.ToString("0.##")));

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                });

                foreach (var row in rows)
                {
                    table.Cell()
                        .BorderBottom(1)
                        .BorderColor(Rule)
                        .PaddingVertical(5)
                        .Text(row.Label)
                        .FontSize(9);

                    table.Cell()
                        .Border(1)
                        .BorderColor(Rule)
                        .Background(row.Value == null ? Colors.White : Box)
                        .PaddingVertical(5)
                        .AlignCenter()
                        .Text(row.Value ?? " ")
                        .FontSize(9)
                        .Bold();
                }
            });
        }

        /// <summary>
        /// Leave is authorised by HR and then the Managing Director,
        /// so the form's single signature line becomes one line per
        /// decision actually taken.
        /// </summary>
        private void Authorisations(
            IContainer container,
            LeaveRequest request)
        {
            var decisions = request.History
                .Where(h =>
                    h.Action == LeaveRequestAction.ApprovedByHR ||
                    h.Action == LeaveRequestAction.ApprovedByMD ||
                    h.Action == LeaveRequestAction.RejectedByHR ||
                    h.Action == LeaveRequestAction.RejectedByMD)
                .OrderBy(h => h.ActionDate)
                .ToList();

            if (decisions.Count == 0)
            {
                container.Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem(2).Element(e =>
                            Blank(e, "NAME & SIGNATURE"));

                        row.ConstantItem(16);

                        row.RelativeItem().Element(e =>
                            Blank(e, "DATE"));
                    });

                    column.Item()
                        .Text("Not yet authorised.")
                        .FontSize(8)
                        .FontColor(Muted);
                });

                return;
            }

            container.Column(column =>
            {
                column.Spacing(8);

                foreach (var decision in decisions)
                {
                    var who = decision.ActionedByEmployee == null
                        ? "--"
                        : $"{decision.ActionedByEmployee.FirstName} " +
                          $"{decision.ActionedByEmployee.LastName}";

                    var stage =
                        decision.Action == LeaveRequestAction.ApprovedByHR
                            ? "Approved by HR"
                        : decision.Action == LeaveRequestAction.RejectedByHR
                            ? "Rejected by HR"
                        : decision.Action == LeaveRequestAction.ApprovedByMD
                            ? "Approved by the Managing Director"
                        : "Rejected by the Managing Director";

                    column.Item().Row(row =>
                    {
                        row.RelativeItem(2).Element(e =>
                            Field(e, stage.ToUpperInvariant(), who));

                        row.ConstantItem(16);

                        row.RelativeItem().Element(e => Field(e, "DATE",
                            decision.ActionDate.ToLocalTime()
                                .ToString("d MMMM yyyy")));
                    });

                    if (!string.IsNullOrWhiteSpace(decision.Comment))
                    {
                        column.Item()
                            .Text(decision.Comment)
                            .FontSize(8)
                            .FontColor(Muted);
                    }
                }
            });
        }


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
                    .Text(string.IsNullOrWhiteSpace(value) ? "--" : value);
            });
        }

        /// <summary>
        /// A ruled line with nothing on it, to be completed by hand.
        /// </summary>
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

        private void Footer(IContainer container, LeaveRequest request)
        {
            container
                .PaddingTop(10)
                .BorderTop(1)
                .BorderColor(Rule)
                .PaddingTop(6)
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text($"{request.RequestNumber} - " +
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

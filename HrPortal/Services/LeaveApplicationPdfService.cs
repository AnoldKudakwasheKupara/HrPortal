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
    /// The chrome comes from <see cref="DocumentStyle"/>.
    /// </summary>
    public class LeaveApplicationPdfService
    {
        private const string Title = "Leave Application";

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

            return Build(request, leaveTypes, balance).GeneratePdf();
        }

        /// <summary>
        /// The document itself, kept separate from how it is
        /// rendered so it can also be exported as images for a
        /// visual check.
        /// </summary>
        public IDocument Build(
            LeaveRequest request,
            IReadOnlyList<LeaveType> leaveTypes,
            LeaveBalance? balance)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    DocumentStyle.Apply(page);

                    page.Content().Element(e =>
                        Content(e, request, leaveTypes, balance));

                    page.Footer().Element(e => DocumentStyle.Footer(
                        e, request.RequestNumber));
                });
            });
        }

        private void Content(
            IContainer container,
            LeaveRequest request,
            IReadOnlyList<LeaveType> leaveTypes,
            LeaveBalance? balance)
        {
            var who = $"{request.Employee.FirstName} " +
                      $"{request.Employee.LastName}";

            container.Column(column =>
            {
                column.Spacing(6);

                column.Item().Element(e => DocumentStyle.Letterhead(
                    e, _brand, Title, request.RequestNumber));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "1", "Applicant"));

                column.Item().Element(e => DocumentStyle.DetailTable(
                    e, new (string, string?)[]
                    {
                        ("First name", request.Employee.FirstName),
                        ("Surname", request.Employee.LastName),
                        ("Employee number",
                            request.Employee.EmployeeNumber),
                        ("Department",
                            request.Employee.Department?.Name)
                    }));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "2", "Type of leave  (in days)"));

                column.Item().Element(e =>
                    Boxes(e, request, leaveTypes, balance));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "3", "Period applied for"));

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "Date from",
                        request.StartDate.ToString("d MMMM yyyy")));

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "To",
                        request.EndDate.ToString("d MMMM yyyy")));
                });

                if (!string.IsNullOrWhiteSpace(request.Reason))
                {
                    column.Item().Element(e => DocumentStyle.Field(
                        e, "Reason", request.Reason));
                }

                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Element(e => DocumentStyle.Field(
                        e, "Signature of applicant", who));

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "Date",
                        request.SubmittedAt.ToLocalTime()
                            .ToString("d MMMM yyyy")));
                });

                column.Item().Element(e => DocumentStyle.Note(
                    e, $"Submitted in the portal by {who}."));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "4", "Authorised by (H.O.D or manager)"));

                column.Item().Element(e => Authorisations(e, request));

                column.Item().Element(e => DocumentStyle.Section(
                    e, "5", "For official use"));

                column.Item().Element(e => DocumentStyle.Blank(
                    e,
                    "Does the applicant qualify to go on leave? " +
                    "Yes or no"));

                column.Item().Element(e => DocumentStyle.Blank(
                    e, "Reason"));
            });
        }

        /// <summary>
        /// The leave type boxes. Only the type applied for carries a
        /// figure, since an application covers one type; the rest are
        /// left blank as they are on the paper form. The row applied
        /// for is set in the brand green so the eye finds it without
        /// having to read every label.
        /// </summary>
        private void Boxes(
            IContainer container,
            LeaveRequest request,
            IReadOnlyList<LeaveType> leaveTypes,
            LeaveBalance? balance)
        {
            var rows = leaveTypes
                .Select(t => (
                    Label: t.Name,
                    Value: t.Id == request.LeaveTypeId
                        ? request.NumberOfDays.ToString("0.##")
                        : null,
                    Summary: false))
                .ToList();

            rows.Add(("Total leave days taken",
                balance?.UsedDays.ToString("0.##"), true));

            rows.Add(("Total leave days accrued",
                balance?.AllocatedDays.ToString("0.##"), true));

            rows.Add(("Leave days balance",
                balance?.RemainingDays.ToString("0.##"), true));

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(74);
                });

                var firstSummary = rows.FindIndex(r => r.Summary);

                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var applied = row.Value != null && !row.Summary;

                    // A heavier rule where the leave types end and the
                    // running totals begin, so the two are not read as
                    // one list.
                    var divider = i == firstSummary;

                    table.Cell()
                        .BorderTop(divider ? 1.2f : 0)
                        .BorderBottom(0.6f)
                        .BorderColor(DocumentStyle.Rule)
                        .PaddingVertical(4)
                        .Text(row.Label.ToUpperInvariant())
                        .FontSize(8)
                        .SemiBold()
                        .FontColor(applied || row.Summary
                            ? DocumentStyle.Green
                            : DocumentStyle.Muted)
                        .LetterSpacing(0.05f);

                    table.Cell()
                        .BorderTop(divider ? 1.2f : 0.6f)
                        .BorderBottom(0.6f)
                        .BorderLeft(0.6f)
                        .BorderRight(0.6f)
                        .BorderColor(applied
                            ? DocumentStyle.Green
                            : DocumentStyle.Rule)
                        .Background(row.Value == null
                            ? Colors.White
                            : DocumentStyle.Wash)
                        .PaddingVertical(4)
                        .AlignCenter()
                        .Text(row.Value ?? " ")
                        .FontSize(9.5f)
                        .Bold()
                        .FontColor(applied
                            ? DocumentStyle.Green
                            : DocumentStyle.Ink);
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
                    column.Spacing(6);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem(2).Element(e =>
                            DocumentStyle.Blank(e, "Name & signature"));

                        row.ConstantItem(18);

                        row.RelativeItem().Element(e =>
                            DocumentStyle.Blank(e, "Date"));
                    });

                    column.Item().Element(e => DocumentStyle.Note(
                        e, "Not yet authorised."));
                });

                return;
            }

            container.Column(column =>
            {
                column.Spacing(6);

                foreach (var decision in decisions)
                {
                    var who = decision.ActionedByEmployee == null
                        ? null
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
                            DocumentStyle.Field(e, stage, who));

                        row.ConstantItem(18);

                        row.RelativeItem().Element(e =>
                            DocumentStyle.Field(
                                e,
                                "Date",
                                decision.ActionDate.ToLocalTime()
                                    .ToString("d MMMM yyyy")));
                    });

                    if (!string.IsNullOrWhiteSpace(decision.Comment))
                    {
                        column.Item().Element(e => DocumentStyle.Note(
                            e, decision.Comment!));
                    }
                }
            });
        }
    }
}

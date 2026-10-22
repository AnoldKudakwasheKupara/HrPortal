using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Renders a loan application as the company's form, so a printed
    /// copy reads like the paper one it replaced.
    /// </summary>
    public class LoanApplicationPdfService
    {
        private const string Ink = "#1f2933";
        private const string Muted = "#6b7280";
        private const string Rule = "#d9dee5";

        public byte[] Render(LoanApplication application)
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

                    page.Content().Element(e => Content(e, application));

                    page.Footer().Element(e => Footer(e, application));
                });
            });

            return document.GeneratePdf();
        }

        private void Content(
            IContainer container,
            LoanApplication application)
        {
            var who = $"{application.Employee.FirstName} " +
                      $"{application.Employee.LastName}";

            container.Column(column =>
            {
                column.Spacing(11);

                column.Item().AlignCenter()
                    .Text("INTELLEGO INVESTMENT CONSULTANTS (PVT) LTD")
                    .FontSize(11)
                    .Bold();

                column.Item().Element(e => Standing(e, application));

                column.Item().AlignRight().Element(e => Field(
                    e,
                    "DATE",
                    application.CreatedAt.ToLocalTime()
                        .ToString("d MMMM yyyy"),
                    140));

                column.Item().PaddingTop(2)
                    .Text("RE: LOAN APPLICATION FORM")
                    .Bold()
                    .LetterSpacing(0.03f);

                column.Item().Text(text =>
                {
                    text.Span("I, ");
                    text.Span(who).Bold();
                    text.Span(" hereby request you to grant to me a " +
                              "loan of ");
                    text.Span(LoanCurrencies.Format(
                            application.Currency,
                            application.AmountRequested))
                        .Bold();
                    text.Span(" (");
                    text.Span(LoanCurrencies.InWords(
                        application.Currency,
                        application.AmountRequested));
                    text.Span(") in words.");
                });

                column.Item().Text(
                    "I hereby authorize the company to deduct the " +
                    "above loan amount from my salary in equal " +
                    $"installment of {application.RepaymentMonths} " +
                    "months. If in the meantime, I resign from my post " +
                    "or am removed from the company for any reason " +
                    "whatsoever, I am fully liable to repay the " +
                    "remaining amount of the loan after adjustment of " +
                    "all dues to me by the company before my " +
                    "departure.");

                column.Item().PaddingTop(2).Row(row =>
                {
                    row.RelativeItem().Element(e => Field(
                        e,
                        "AMOUNT REQUIRED",
                        LoanCurrencies.Format(
                            application.Currency,
                            application.AmountRequested)));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e => Field(
                        e,
                        "AMOUNT AUTHORIZED",
                        application.AmountAuthorised == null
                            ? null
                            : LoanCurrencies.Format(
                                application.Currency,
                                application.AmountAuthorised.Value)));
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => Field(
                        e,
                        "REPAYMENT PERIOD",
                        $"{application.RepaymentMonths} months"));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e => Field(
                        e,
                        "WITH EFFECT FROM",
                        application.RepaymentStartDate?
                            .ToString("d MMMM yyyy")));
                });

                column.Item().Element(e => Field(
                    e,
                    "PURPOSE (ATTACH RECEIPTS OR QUOTATIONS)",
                    application.Purpose));

                if (application.Attachments.Any())
                {
                    column.Item().Text(
                        "Attached: " +
                        string.Join(
                            ", ",
                            application.Attachments
                                .OrderBy(a => a.UploadedAt)
                                .Select(a => a.OriginalFileName)))
                        .FontSize(8)
                        .FontColor(Muted);
                }

                column.Item().PaddingTop(6).Text("Thanking You.");
                column.Item().Text("Yours Faithfully,");

                column.Item().Element(e => Signature(e, application, who));

                column.Item().PaddingTop(6).Element(e =>
                    Sanction(e, application));
            });
        }

        /// <summary>
        /// The block at the head of the paper form. The portal records
        /// applications rather than loan balances, so only the new
        /// instalment is known; the rest is left for payroll.
        /// </summary>
        private void Standing(
            IContainer container,
            LoanApplication application)
        {
            container.Column(column =>
            {
                column.Spacing(6);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e =>
                        Blank(e, "OUTSTANDING LOAN"));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e =>
                        Blank(e, "DATE TO CLEAR OUTSTANDING LOAN"));
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e =>
                        Blank(e, "CURRENT DEDUCTION P.M."));

                    row.ConstantItem(16);

                    row.RelativeItem().Element(e => Field(
                        e,
                        "NEW INSTALLMENT PER MONTH",
                        application.MonthlyInstalment == null
                            ? null
                            : LoanCurrencies.Format(
                                application.Currency,
                                application.MonthlyInstalment.Value)));
                });

                column.Item()
                    .Text("Outstanding loan, the date it clears and " +
                          "the current deduction are completed by " +
                          "payroll, which holds them.")
                    .FontSize(8)
                    .FontColor(Muted);
            });
        }

        private void Signature(
            IContainer container,
            LoanApplication application,
            string who)
        {
            container.Column(column =>
            {
                column.Item().PaddingTop(10).Element(e =>
                    Blank(e, "NAME & SIGNATURE OF THE EMPLOYEE", 320));

                if (application.SubmittedAt == null)
                    return;

                column.Item()
                    .Text($"Submitted in the portal by {who} on " +
                          application.SubmittedAt.Value.ToLocalTime()
                              .ToString("d MMMM yyyy"))
                    .FontSize(8)
                    .FontColor(Muted);
            });
        }

        private void Sanction(
            IContainer container,
            LoanApplication application)
        {
            var decision = application.History
                .Where(h =>
                    h.Action == LoanApplicationAction.Approved ||
                    h.Action == LoanApplicationAction.Rejected)
                .OrderByDescending(h => h.ActionDate)
                .FirstOrDefault();

            container.Column(column =>
            {
                column.Spacing(6);

                if (decision == null)
                {
                    column.Item().Element(e =>
                        Blank(e, "LOAN SANCTIONED BY", 320));

                    column.Item().Element(e =>
                        Blank(e, "SIGNATURE", 320));

                    column.Item()
                        .Text("Not yet sanctioned.")
                        .FontSize(8)
                        .FontColor(Muted);

                    return;
                }

                var sanctionedBy = decision.ActionedByEmployee == null
                    ? null
                    : $"{decision.ActionedByEmployee.FirstName} " +
                      $"{decision.ActionedByEmployee.LastName}";

                column.Item().Element(e => Field(
                    e,
                    decision.Action == LoanApplicationAction.Approved
                        ? "LOAN SANCTIONED BY"
                        : "DECLINED BY",
                    sanctionedBy,
                    320));

                column.Item().Element(e => Blank(e, "SIGNATURE", 320));

                column.Item()
                    .Text(
                        (decision.Action == LoanApplicationAction.Approved
                            ? "Sanctioned in the portal on "
                            : "Declined in the portal on ") +
                        decision.ActionDate.ToLocalTime()
                            .ToString("d MMMM yyyy") +
                        (string.IsNullOrWhiteSpace(decision.Comment)
                            ? "."
                            : $". {decision.Comment}"))
                    .FontSize(8)
                    .FontColor(Muted);
            });
        }

        private void Field(
            IContainer container,
            string label,
            string? value,
            float? width = null)
        {
            var target = width == null
                ? container
                : container.Width(width.Value);

            target.Column(column =>
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
        private void Blank(
            IContainer container,
            string label,
            float? width = null)
        {
            var target = width == null
                ? container
                : container.Width(width.Value);

            target.Column(column =>
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
            LoanApplication application)
        {
            container
                .PaddingTop(10)
                .BorderTop(1)
                .BorderColor(Rule)
                .PaddingTop(6)
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

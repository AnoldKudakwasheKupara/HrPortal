using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Renders a loan application as the company's form, so a printed
    /// copy reads like the paper one it replaced. The chrome comes
    /// from <see cref="DocumentStyle"/>.
    ///
    /// This one is a letter rather than a questionnaire, so the
    /// sections frame the request instead of numbering questions.
    /// </summary>
    public class LoanApplicationPdfService
    {
        private const string Title = "Loan Application";

        private readonly BrandAssets _brand;

        public LoanApplicationPdfService(BrandAssets brand)
        {
            _brand = brand;
        }

        public byte[] Render(LoanApplication application) =>
            Build(application).GeneratePdf();

        /// <summary>
        /// The document itself, kept separate from how it is
        /// rendered so it can also be exported as images for a
        /// visual check.
        /// </summary>
        public IDocument Build(LoanApplication application)
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
            LoanApplication application)
        {
            var who = $"{application.Employee.FirstName} " +
                      $"{application.Employee.LastName}";

            DocumentStyle.Gutters(container).PaddingTop(20).Column(column =>
            {
                column.Spacing(6);

                column.Item().Element(e => DocumentStyle.Section(
                    e,
                    "01",
                    "Existing commitments",
                    "Completed by payroll, which holds these"));

                column.Item().Element(e => Standing(e, application));

                column.Item().Element(e => DocumentStyle.Section(
                    e,
                    "02",
                    "The request",
                    "The amount applied for and the undertaking"));

                column.Item().AlignRight().Width(170)
                    .Element(e => DocumentStyle.Field(
                        e,
                        "Date",
                        DocumentStyle.Stamp(
                            application.SubmittedAt
                            ?? application.CreatedAt),
                        icon: Icons.Calendar));

                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(s => s.LineHeight(1.45f));

                    text.Span("I, ");
                    text.Span(who).SemiBold();
                    text.Span(" hereby request you to grant to me a " +
                              "loan of ");
                    text.Span(LoanCurrencies.Format(
                            application.Currency,
                            application.AmountRequested))
                        .SemiBold()
                        .FontColor(DocumentStyle.Green);
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
                    "departure.")
                    .LineHeight(1.45f);

                column.Item().Element(e => DocumentStyle.Section(
                    e,
                    "03",
                    "Amount and repayment",
                    "What is authorised and how it is recovered"));

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "Amount required",
                        LoanCurrencies.Format(
                            application.Currency,
                            application.AmountRequested)));

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "Amount authorized",
                        application.AmountAuthorised == null
                            ? null
                            : LoanCurrencies.Format(
                                application.Currency,
                                application.AmountAuthorised.Value)));
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "Repayment period",
                        $"{application.RepaymentMonths} months"));

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "With effect from",
                        application.RepaymentStartDate?
                            .ToString("d MMMM yyyy")));
                });

                column.Item().Element(e => DocumentStyle.Field(
                    e,
                    "Purpose (attach receipts or quotations)",
                    application.Purpose));

                if (application.Attachments.Any())
                {
                    column.Item().Element(e => DocumentStyle.Note(
                        e,
                        "Attached: " +
                        string.Join(
                            ", ",
                            application.Attachments
                                .OrderBy(a => a.UploadedAt)
                                .Select(a => a.OriginalFileName))));
                }

                column.Item().PaddingTop(4).Text("Thanking You.");
                column.Item().Text("Yours Faithfully,");

                column.Item().Element(e => Signature(e, application, who));

                column.Item().Element(e => DocumentStyle.Section(
                    e,
                    "04",
                    "Sanction",
                    "To be completed by the relevant approvers"));

                column.Item().Element(e => Sanction(e, application));
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
                        DocumentStyle.Field(e, "Outstanding loan", null));

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e, "Date to clear outstanding loan", null,
                        icon: Icons.Calendar));
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e, "Current deduction p.m.", null));

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e => DocumentStyle.Field(
                        e,
                        "New installment per month",
                        application.MonthlyInstalment == null
                            ? null
                            : LoanCurrencies.Format(
                                application.Currency,
                                application.MonthlyInstalment.Value)));
                });

                column.Item().Element(e => DocumentStyle.Note(
                    e,
                    "Outstanding loan, the date it clears and the " +
                    "current deduction are completed by payroll, " +
                    "which holds them."));
            });
        }

        private void Signature(
            IContainer container,
            LoanApplication application,
            string who)
        {
            container.Column(column =>
            {
                column.Item().PaddingTop(8).Width(320).Element(e =>
                    DocumentStyle.Blank(
                        e, "Name & signature of the employee"));

                if (application.SubmittedAt == null)
                    return;

                column.Item().PaddingTop(4).Element(e =>
                    DocumentStyle.Note(
                        e,
                        $"Submitted in the portal by {who} on " +
                        DocumentStyle.Stamp(application.SubmittedAt)));
            });
        }

        /// <summary>
        /// The supervisor's line: who recommended it and when, or
        /// an empty box where the application has not been through
        /// a supervisor -- somebody with nobody above them, or a
        /// copy printed before it was sent.
        /// </summary>
        private void Recommendation(
            IContainer container,
            LoanApplication application)
        {
            var decision = application.History
                .Where(h =>
                    h.Action ==
                        LoanApplicationAction.ApprovedBySupervisor ||
                    h.Action ==
                        LoanApplicationAction.RejectedBySupervisor)
                .OrderByDescending(h => h.ActionDate)
                .FirstOrDefault();

            var who = decision?.ActionedByEmployee == null
                ? null
                : $"{decision.ActionedByEmployee.FirstName} " +
                  $"{decision.ActionedByEmployee.LastName}";

            var label =
                decision?.Action ==
                    LoanApplicationAction.RejectedBySupervisor
                    ? "Declined by (manager / supervisor)"
                    : "Recommended by (manager / supervisor)";

            container.Column(column =>
            {
                column.Spacing(6);

                column.Item().Row(row =>
                {
                    // Nothing decided yet leaves a box that says to
                    // sign it, the way the sanction below does: this
                    // form is also filled in by hand where somebody
                    // has no supervisor on record.
                    row.RelativeItem(2).Element(e =>
                    {
                        if (decision == null)
                            DocumentStyle.Blank(e, label);
                        else
                            DocumentStyle.Field(e, label, who);
                    });

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e =>
                    {
                        if (decision == null)
                        {
                            DocumentStyle.Blank(
                                e, "Date", "Select date",
                                icon: Icons.Calendar);
                        }
                        else
                        {
                            DocumentStyle.Field(
                                e,
                                "Date",
                                DocumentStyle.Stamp(decision.ActionDate),
                                icon: Icons.Calendar);
                        }
                    });
                });

                if (!string.IsNullOrWhiteSpace(decision?.Comment))
                {
                    column.Item().Element(e => DocumentStyle.Note(
                        e, decision!.Comment!));
                }
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

                // The supervisor sees the application before the
                // Managing Director does, and the form showed no
                // sign of it: a sanctioned loan printed as though
                // nobody between the applicant and the MD had ever
                // looked at it.
                column.Item().Element(e =>
                    Recommendation(e, application));

                if (decision == null)
                {
                    column.Item().Width(320).Element(e =>
                        DocumentStyle.Blank(e, "Loan sanctioned by"));

                    column.Item().Width(320).Element(e =>
                        DocumentStyle.Blank(e, "Signature"));

                    column.Item().Element(e => DocumentStyle.Note(
                        e, "Not yet sanctioned."));

                    return;
                }

                var approved =
                    decision.Action == LoanApplicationAction.Approved;

                var sanctionedBy = decision.ActionedByEmployee == null
                    ? null
                    : $"{decision.ActionedByEmployee.FirstName} " +
                      $"{decision.ActionedByEmployee.LastName}";

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(e =>
                        DocumentStyle.Outcome(
                            e, "Sanctioned", approved));

                    row.ConstantItem(18);

                    row.RelativeItem().Element(e =>
                        DocumentStyle.Outcome(
                            e, "Declined", !approved, adverse: true));
                });

                column.Item().Width(320).Element(e =>
                    DocumentStyle.Field(
                        e,
                        approved ? "Loan sanctioned by" : "Declined by",
                        sanctionedBy));

                column.Item().Width(320).Element(e =>
                    DocumentStyle.Blank(e, "Signature"));

                column.Item().Element(e => DocumentStyle.Note(
                    e,
                    (approved
                        ? "Sanctioned in the portal on "
                        : "Declined in the portal on ") +
                    DocumentStyle.Stamp(decision.ActionDate) +
                    (string.IsNullOrWhiteSpace(decision.Comment)
                        ? "."
                        : $". {decision.Comment}")));
            });
        }
    }
}

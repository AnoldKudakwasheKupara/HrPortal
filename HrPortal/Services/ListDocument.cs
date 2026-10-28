namespace HrPortal.Services
{
    /// <summary>
    /// One column of a printed list. The weight is relative, so the
    /// columns always add up to the width of the paper -- a list has
    /// no say over how wide A4 is, and a column measured in points
    /// is one layout exception waiting for a longer serial number.
    /// </summary>
    public sealed record ListColumn(
        string Heading,
        float Weight = 1f,
        bool AlignRight = false);

    /// <summary>
    /// A screen full of rows, on paper: the register an auditor asks
    /// for, the queue somebody takes into a meeting, the staff list
    /// that goes in the file.
    ///
    /// <see cref="RecordDocument"/> prints one record down the page.
    /// This prints many across it, and the two share the banner and
    /// the footer so a register and a form read as one company's
    /// paperwork.
    /// </summary>
    public sealed class ListDocument
    {
        public ListDocument(string title, string? caption = null)
        {
            Title = title;
            Caption = caption;
        }

        /// <summary>What the list is, shown in the banner.</summary>
        public string Title { get; }

        /// <summary>
        /// A line under the heading saying what this is a list of,
        /// and which filter produced it -- a page showing 4 of 40
        /// assets is misleading without it.
        /// </summary>
        public string? Caption { get; set; }

        public List<ListColumn> Columns { get; } = new();

        public List<IReadOnlyList<string?>> Rows { get; } = new();

        /// <summary>
        /// The figures that belong with the list: how many there are,
        /// how many are active, what the column adds up to.
        /// </summary>
        public List<(string Label, string Value)> Totals { get; } = new();

        /// <summary>Shown in place of the rows when there are none.</summary>
        public string EmptyMessage { get; set; } = "Nothing to show.";

        public ListDocument Column(
            string heading,
            float weight = 1f,
            bool alignRight = false)
        {
            Columns.Add(new ListColumn(heading, weight, alignRight));
            return this;
        }

        public ListDocument Row(params string?[] cells)
        {
            Rows.Add(cells);
            return this;
        }

        public ListDocument Total(string label, string value)
        {
            Totals.Add((label, value));
            return this;
        }

        public ListDocument Total(string label, int value) =>
            Total(label, value.ToString("N0"));

        /// <summary>
        /// Repeated in the banner and the footer. A register is only
        /// evidence of anything alongside how many rows it had, so
        /// the count travels with it.
        /// </summary>
        public string Reference =>
            Rows.Count == 1 ? "1 record" : $"{Rows.Count:N0} records";

        /// <summary>
        /// The file name the browser is offered. Dated, because the
        /// same list downloaded next month is a different document
        /// and should not overwrite this one.
        /// </summary>
        public string FileName =>
            $"{Title} - {DateTime.Today:yyyy-MM-dd}.pdf";

        // ---- cell helpers ---------------------------------------
        //
        // A blank cell in a table reads as "we hold nothing here",
        // which is worth knowing, so these never collapse a row.

        public static string Text(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? DocumentStyle.NotRecorded
                : value.Trim();

        public static string Date(DateTime? value) =>
            value == null
                ? DocumentStyle.NotRecorded
                : value.Value.ToString("d MMM yyyy");

        public static string Stamp(DateTime? value) =>
            DocumentStyle.Stamp(value) ?? DocumentStyle.NotRecorded;

        public static string Yes(bool value) => value ? "Yes" : "No";

        public static string Number(decimal? value, string format = "N2") =>
            value == null
                ? DocumentStyle.NotRecorded
                : value.Value.ToString(format);
    }
}

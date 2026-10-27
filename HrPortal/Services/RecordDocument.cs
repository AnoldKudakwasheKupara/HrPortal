namespace HrPortal.Services
{
    /// <summary>
    /// One block of a printed record: a heading and the rows under
    /// it. The number is supplied by the document, so a section does
    /// not have to know where it sits.
    /// </summary>
    public sealed class RecordSection
    {
        public RecordSection(string title, string? subtitle = null)
        {
            Title = title;
            Subtitle = subtitle;
        }

        public string Title { get; }

        public string? Subtitle { get; }

        public List<(string Label, string? Value)> Rows { get; } = new();

        /// <summary>
        /// Adds a row. A value that is empty still prints, because a
        /// blank on a record says "we hold nothing here", which is
        /// itself worth knowing.
        /// </summary>
        public RecordSection Add(string label, string? value)
        {
            Rows.Add((label, value));
            return this;
        }

        public RecordSection Add(string label, DateTime? value) =>
            Add(label, value?.ToString("d MMMM yyyy"));

        public RecordSection Add(string label, bool value) =>
            Add(label, value ? "Yes" : "No");

        public RecordSection Add(string label, decimal? value) =>
            Add(label, value?.ToString("N2"));

        public RecordSection Add(string label, int? value) =>
            Add(label, value?.ToString("N0"));

        /// <summary>A moment, to the minute.</summary>
        public RecordSection AddStamp(string label, DateTime? value) =>
            Add(label, DocumentStyle.Stamp(value));
    }

    /// <summary>
    /// What a details page looks like once it is on paper: the same
    /// banner and sections the forms use, filled from whatever record
    /// the page was showing.
    ///
    /// The forms earn their own services because each is a specific
    /// piece of company stationery with its own clauses and signature
    /// blocks. A details page is not stationery -- it is a record
    /// printed out -- so one shape serves all of them, and a new page
    /// needs a list of rows rather than a new document.
    /// </summary>
    public sealed class RecordDocument
    {
        public RecordDocument(string title, string? reference = null)
        {
            Title = title;
            Reference = reference;
        }

        /// <summary>What the document is, shown in the banner.</summary>
        public string Title { get; }

        /// <summary>
        /// The record's own number, repeated in the footer so a loose
        /// page can be placed.
        /// </summary>
        public string? Reference { get; }

        /// <summary>The record's name, in the card at the head.</summary>
        public string? Headline { get; set; }

        /// <summary>A line under the name: a department, a vendor.</summary>
        public string? Caption { get; set; }

        /// <summary>A status, as a chip beside the name.</summary>
        public string? Chip { get; set; }

        public List<RecordSection> Sections { get; } = new();

        /// <summary>
        /// Anything the record carries as free text: a comment, a
        /// reason, a description too long for a table row.
        /// </summary>
        public List<string> Notes { get; } = new();

        public RecordSection Section(
            string title,
            string? subtitle = null)
        {
            var section = new RecordSection(title, subtitle);
            Sections.Add(section);
            return section;
        }

        public RecordDocument Note(string? text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                Notes.Add(text);

            return this;
        }

        /// <summary>
        /// The file name the browser is offered. Keeps the reference
        /// where there is one, so two downloads of the same kind do
        /// not overwrite each other.
        /// </summary>
        public string FileName =>
            string.IsNullOrWhiteSpace(Reference)
                ? $"{Title}.pdf"
                : $"{Title} - {Reference}.pdf";
    }
}

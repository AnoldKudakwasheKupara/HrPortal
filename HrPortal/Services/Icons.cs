namespace HrPortal.Services
{
    /// <summary>
    /// The small marks the forms use, drawn as vectors.
    ///
    /// They are not characters on purpose. A glyph can be missing
    /// from a typeface -- Montserrat has no tick -- and since QuestPDF
    /// 2026.9 that stops the document rather than degrading it. A
    /// path cannot go missing, and it stays sharp at any size the
    /// paper asks for.
    /// </summary>
    public static class Icons
    {
        private const string Line = "#9aa0a6";

        private static string Stroke(string body, string colour) =>
            "<svg xmlns='http://www.w3.org/2000/svg' " +
            "viewBox='0 0 24 24' fill='none' stroke='" + colour +
            "' stroke-width='1.9' stroke-linecap='round' " +
            "stroke-linejoin='round'>" + body + "</svg>";

        /// <summary>Marks a box somebody signs by hand.</summary>
        public static readonly string Pen = Stroke(
            "<path d='M16.5 3.2a2.6 2.6 0 0 1 3.7 3.7L7.4 19.6 " +
            "2.6 21.4l1.8-4.8Z'/><path d='M14.8 5 19 9.2'/>",
            Line);

        /// <summary>Marks a box that takes a date.</summary>
        public static readonly string Calendar = Stroke(
            "<rect x='3' y='4.5' width='18' height='17' rx='2.4'/>" +
            "<path d='M16 2.5v4M8 2.5v4M3 10h18'/>",
            Line);

        /// <summary>Marks a box chosen from a fixed list.</summary>
        public static readonly string Chevron = Stroke(
            "<path d='m6 9.5 6 6 6-6'/>", "#5f6368");

        /// <summary>An option not taken.</summary>
        public static readonly string RadioOff =
            "<svg xmlns='http://www.w3.org/2000/svg' " +
            "viewBox='0 0 24 24'><circle cx='12' cy='12' r='9.4' " +
            "fill='none' stroke='#c4c9ce' stroke-width='1.9'/></svg>";

        /// <summary>The option taken.</summary>
        public static readonly string RadioOn =
            "<svg xmlns='http://www.w3.org/2000/svg' " +
            "viewBox='0 0 24 24'><circle cx='12' cy='12' r='9.4' " +
            "fill='none' stroke='" + DocumentStyle.Good +
            "' stroke-width='2.4'/><circle cx='12' cy='12' r='4.6' " +
            "fill='" + DocumentStyle.Good + "'/></svg>";

        /// <summary>An outcome box nobody has marked.</summary>
        public static string EmptyBox(string colour) =>
            "<svg xmlns='http://www.w3.org/2000/svg' " +
            "viewBox='0 0 24 24'><rect x='2.6' y='2.6' width='18.8' " +
            "height='18.8' rx='3.4' fill='#ffffff' stroke='" + colour +
            "' stroke-width='1.9'/></svg>";

        /// <summary>The outcome that applies.</summary>
        public static string CheckedBox(string colour) =>
            "<svg xmlns='http://www.w3.org/2000/svg' " +
            "viewBox='0 0 24 24'><rect x='2.6' y='2.6' width='18.8' " +
            "height='18.8' rx='3.4' fill='" + colour + "'/>" +
            "<path d='m6.8 12.4 3.6 3.6 6.8-7.4' fill='none' " +
            "stroke='#ffffff' stroke-width='2.4' " +
            "stroke-linecap='round' stroke-linejoin='round'/></svg>";
    }
}

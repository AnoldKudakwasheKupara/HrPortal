namespace HrPortal.Services
{
    /// <summary>
    /// How personal details are shown to somebody who may know they
    /// exist but does not need the whole of them.
    /// </summary>
    public static class PersonalData
    {
        /// <summary>
        /// An ID number with all but its last three characters hidden,
        /// enough to tell two people apart without giving the number
        /// away.
        /// </summary>
        public static string? MaskId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var trimmed = id.Trim();

            return trimmed.Length <= 3
                ? new string('•', trimmed.Length)
                : new string('•', trimmed.Length - 3) + trimmed[^3..];
        }
    }
}

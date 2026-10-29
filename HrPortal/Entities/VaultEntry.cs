namespace HrPortal.Entities
{
    /// <summary>
    /// A login the IT team shares: the office router, the Wi-Fi, a
    /// supplier's portal, a software licence account.
    ///
    /// The password and the notes are held encrypted. The key is not
    /// in the database, so neither the database nor a backup of it is
    /// enough to read them.
    /// </summary>
    public class VaultEntry
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Category { get; set; }

        public string? Username { get; set; }

        public string? Url { get; set; }

        public string EncryptedPassword { get; set; } = string.Empty;

        public string? EncryptedNotes { get; set; }

        public DateTime PasswordChangedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? CreatedByName { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? UpdatedByName { get; set; }
    }
}

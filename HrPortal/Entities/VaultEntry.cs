namespace HrPortal.Entities
{
    /// <summary>
    /// A login for one of the company's local systems: the service, the
    /// username and the password. Category, Url and EncryptedNotes are no
    /// longer asked for.
    ///
    /// The password is held encrypted. The key is not in the database,
    /// so the database alone is not enough to read it.
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

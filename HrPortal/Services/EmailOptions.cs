namespace HrPortal.Services
{
    public enum EmailDelivery
    {
        /// <summary>Hand the message to an SMTP server.</summary>
        Smtp,

        /// <summary>
        /// Write the message to disk as a .eml file instead of
        /// sending it. Useful on a development machine, and the only
        /// way to see what would have been sent without a mail server.
        /// </summary>
        File
    }

    /// <summary>
    /// How the portal sends mail. Disabled by default, so a fresh
    /// clone behaves exactly as before until someone configures it.
    /// </summary>
    public class EmailOptions
    {
        public const string Section = "Email";

        public bool Enabled { get; set; }

        public EmailDelivery Delivery { get; set; } = EmailDelivery.Smtp;

        /// <summary>Where .eml files go under File delivery.</summary>
        public string PickupDirectory { get; set; } = "App_Data/mail";

        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        public bool UseStartTls { get; set; } = true;

        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string FromAddress { get; set; } = string.Empty;

        public string FromName { get; set; } = "My Intellego";

        /// <summary>
        /// Used to turn a notification's link into something clickable
        /// in the email. Left blank, the email carries no link.
        /// </summary>
        public string? PortalBaseUrl { get; set; }
    }
}

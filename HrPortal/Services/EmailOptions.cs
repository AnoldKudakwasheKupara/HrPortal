namespace HrPortal.Services
{
    public enum EmailDelivery
    {
        Smtp,

        File
    }

    public class EmailOptions
    {
        public const string Section = "Email";

        public bool Enabled { get; set; }

        public EmailDelivery Delivery { get; set; } = EmailDelivery.Smtp;

        public string PickupDirectory { get; set; } = "App_Data/mail";

        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        public bool UseStartTls { get; set; } = true;

        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string FromAddress { get; set; } = string.Empty;

        public string FromName { get; set; } = "My Intellego";

        public string? PortalBaseUrl { get; set; }
    }
}

namespace HrPortal.Services.Notifications
{
    public interface INotificationChannel
    {
        string Name { get; }

        bool IsEnabled { get; }

        Task<DeliveryReport> DeliverAsync(
            IReadOnlyList<OutboundNotification> batch,
            CancellationToken cancellationToken = default);
    }

    public record DeliveryReport(int Delivered, int Failed)
    {
        public bool AllDelivered => Failed == 0;
    }

    public record OutboundNotification(
        int EmployeeId,
        string EmailAddress,
        string? DisplayName,
        string Title,
        string Message,
        string? Link);
}

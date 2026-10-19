namespace HrPortal.Services.Notifications
{
    public interface INotificationChannel
    {
        string Name { get; }

        bool IsEnabled { get; }

        Task DeliverAsync(
            IReadOnlyList<OutboundNotification> batch,
            CancellationToken cancellationToken = default);
    }

    public record OutboundNotification(
        int EmployeeId,
        string EmailAddress,
        string? DisplayName,
        string Title,
        string Message,
        string? Link);
}

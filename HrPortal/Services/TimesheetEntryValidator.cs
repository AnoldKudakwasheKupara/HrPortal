using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class TimesheetEntryValidator
    {
        private const int MinutesInDay = 24 * 60;

        public const string WorkDateField = "WorkDate";
        public const string AssetField = "AssetId";
        public const string StartTimeField = "StartTime";
        public const string EndTimeField = "EndTime";
        public const string HoursField = "Hours";
        public const string FormField = "";

        private readonly ApplicationDbContext _context;

        public TimesheetEntryValidator(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TimesheetValidationResult> ValidateAsync(
            int employeeId,
            DateTime workDate,
            int? assetId,
            TimeOnly? startTime,
            TimeOnly? endTime,
            decimal? hours,
            int? excludeId,
            bool modelAlreadyInvalid)
        {
            var result = new TimesheetValidationResult();

            if (workDate.Date > DateTime.Today)
            {
                result.Add(
                    WorkDateField,
                    "You cannot log work for a future date.");
            }

            if (assetId.HasValue &&
                !await _context.Assets.AnyAsync(a => a.Id == assetId.Value))
            {
                result.Add(
                    AssetField,
                    "Choose an asset from the register.");
            }

            var minutes = 0;

            if (startTime.HasValue != endTime.HasValue)
            {
                result.Add(
                    startTime.HasValue ? EndTimeField : StartTimeField,
                    "Give both a start and a finish time, or neither.");
            }
            else if (startTime.HasValue && endTime.HasValue)
            {
                if (endTime.Value <= startTime.Value)
                {
                    result.Add(
                        EndTimeField,
                        "The finish time must be after the start time.");
                }
                else
                {
                    var span = endTime.Value - startTime.Value;

                    minutes = (int)Math.Round(span.TotalMinutes);
                }
            }
            else if (hours.HasValue)
            {
                minutes = (int)Math.Round(hours.Value * 60);
            }
            else
            {
                result.Add(
                    HoursField,
                    "Give the hours spent, or a start and finish time.");
            }

            if (minutes <= 0 && !modelAlreadyInvalid && result.IsValid)
            {
                result.Add(
                    HoursField,
                    "The time spent must be more than zero.");
            }

            if (minutes > 0)
            {
                var alreadyLogged = await _context.TimesheetEntries
                    .Where(t =>
                        t.EmployeeId == employeeId &&
                        t.WorkDate == workDate.Date &&
                        (excludeId == null || t.Id != excludeId.Value))
                    .SumAsync(t => (int?)t.DurationMinutes) ?? 0;

                if (alreadyLogged + minutes > MinutesInDay)
                {
                    result.Add(
                        FormField,
                        $"That takes {workDate:d MMM} past 24 hours: " +
                        $"{TimesheetEntry.FormatDuration(alreadyLogged)} is " +
                        "already logged for that day.");
                }
            }

            result.Minutes = minutes;

            return result;
        }
    }

    public class TimesheetValidationResult
    {
        public int Minutes { get; set; }

        public List<TimesheetValidationError> Errors { get; } = new();

        public bool IsValid => Errors.Count == 0;

        public void Add(string field, string message)
        {
            Errors.Add(new TimesheetValidationError(field, message));
        }
    }

    public record TimesheetValidationError(string Field, string Message);
}

using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.ViewModels;
using HrPortal.Services;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reporting
{
    public class AssetReportService
    {
        private readonly ApplicationDbContext _context;

        public AssetReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReportPayload> AssetsAsync(
            ReportFilter filter,
            bool export)
        {
            var query = _context.Assets
                .Include(a => a.AssignedToEmployee)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(a => a.Company == filter.Company);

            if (filter.Condition.HasValue)
                query = query.Where(a => a.Condition == filter.Condition.Value);

            if (filter.EmployeeId.HasValue)
            {
                query = query.Where(a =>
                    a.AssignedToEmployeeId == filter.EmployeeId.Value);
            }

            var rows = await query
                .OrderBy(a => a.Company)
                .ThenBy(a => a.DeviceModel)
                .Select(a => new AssetReportRow
                {
                    Company = a.Company ?? "--",
                    DeviceModel = a.DeviceModel,
                    Processor = a.Processor,
                    SerialNumber = a.SerialNumber,
                    ProcuredOn = a.ProcuredOn,
                    Condition = a.Condition,
                    IsInUse = a.IsInUse,
                    AssignedTo = a.AssignedToEmployee != null
                        ? a.AssignedToEmployee.FirstName + " " +
                          a.AssignedToEmployee.LastName
                        : "Unassigned",
                    Notes = a.Notes
                })
                .ToListAsync();

            if (export)
            {
                return ReportPayload.ForCsv(
                    "assets",
                    new[]
                    {
                        "Company", "Device / model", "Processor",
                        "Serial", "Procured", "Condition", "In use",
                        "Assigned to", "Notes"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Company, r.DeviceModel, r.Processor,
                        r.SerialNumber, ReportMath.D(r.ProcuredOn),
                        r.Condition.ToString(), r.IsInUse ? "Yes" : "No",
                        r.AssignedTo, r.Notes
                    }));
            }

            var model = new ReportViewModel<AssetReportRow>
            {
                Title = "Asset register",
                Description =
                    "Machines on the register, their condition and holder.",
                Action = "Assets",
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Assets", rows.Count.ToString()),
                    new ReportStat(
                        "Good",
                        rows.Count(r => r.Condition == AssetCondition.Good)
                            .ToString()),
                    new ReportStat(
                        "Needing attention",
                        rows.Count(r =>
                            r.Condition == AssetCondition.Old ||
                            r.Condition == AssetCondition.Bad).ToString(),
                        "old or bad"),
                    new ReportStat(
                        "Unassigned",
                        rows.Count(r => r.AssignedTo == "Unassigned")
                            .ToString()),
                    new ReportStat(
                        "Not in use",
                        rows.Count(r => !r.IsInUse).ToString())
                },
                Coverage = string.IsNullOrWhiteSpace(filter.Company)
                    ? "All companies"
                    : filter.Company
            };

            var byCondition = new ReportBreakdown("By condition", "machines");

            foreach (var c in Enum.GetValues<AssetCondition>())
            {
                var count = rows.Count(r => r.Condition == c);

                if (count > 0)
                {
                    byCondition.Add(
                        c.ToString(),
                        count,
                        c == AssetCondition.Good ? "good"
                            : c == AssetCondition.Bad ? "critical"
                            : "warning");
                }
            }

            var byCompany = new ReportBreakdown("By company", "machines");

            foreach (var g in rows
                .GroupBy(r => r.Company)
                .OrderByDescending(g => g.Count()))
            {
                byCompany.Add(g.Key, g.Count());
            }

            var byHolder = new ReportBreakdown("Most machines held", "machines");

            foreach (var g in rows
                .Where(r => r.AssignedTo != "Unassigned")
                .GroupBy(r => r.AssignedTo)
                .OrderByDescending(g => g.Count())
                .Take(8))
            {
                byHolder.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { byCondition, byCompany, byHolder }
                    .Where(b => b.Items.Count > 0));

            return ReportPayload.ForView(model);
        }

        public async Task<ReportPayload> VendorsAsync(
            ReportFilter filter,
            bool export)
        {
            var query = _context.Vendors.AsNoTracking().AsQueryable();

            if (!filter.IncludeInactive)
                query = query.Where(v => v.IsActive);

            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(v => v.Category == filter.Company);

            var rows = await query
                .OrderBy(v => v.Category)
                .ThenBy(v => v.Name)
                .Select(v => new VendorReportRow
                {
                    Name = v.Name,
                    ProductOrService = v.ProductOrService,
                    Category = v.Category,
                    ContactPerson = v.ContactPerson,
                    Email = v.Email,
                    PhoneNumber = v.PhoneNumber,
                    IsActive = v.IsActive
                })
                .ToListAsync();

            if (export)
            {
                return ReportPayload.ForCsv(
                    "vendors",
                    new[]
                    {
                        "Vendor", "Product or service", "Category",
                        "Contact person", "Email", "Phone", "In use"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Name, r.ProductOrService, r.Category,
                        r.ContactPerson, r.Email, r.PhoneNumber,
                        r.IsActive ? "Yes" : "No"
                    }));
            }

            var noContact = rows.Count(r =>
                string.IsNullOrWhiteSpace(r.Email) &&
                string.IsNullOrWhiteSpace(r.PhoneNumber));

            var model = new ReportViewModel<VendorReportRow>
            {
                Title = "Vendors",
                Description =
                    "Who we buy from, what they supply and how " +
                    "reachable they are.",
                Action = "Vendors",
                Filter = filter,
                Rows = rows,
                Coverage = filter.IncludeInactive
                    ? "All vendors"
                    : "Vendors in use",
                Stats =
                {
                    new ReportStat("Vendors", rows.Count.ToString()),
                    new ReportStat(
                        "In use", rows.Count(r => r.IsActive).ToString()),
                    new ReportStat(
                        "Categories",
                        rows.Select(r => r.Category ?? "Uncategorised")
                            .Distinct().Count().ToString()),
                    new ReportStat(
                        "No contact details",
                        noContact.ToString(),
                        "no email or phone")
                }
            };

            var byCategory = new ReportBreakdown("By category", "vendors");

            foreach (var g in rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Category)
                    ? "Uncategorised"
                    : r.Category!)
                .OrderByDescending(g => g.Count()))
            {
                byCategory.Add(g.Key, g.Count());
            }

            var contactable = new ReportBreakdown(
                "Contact details held", "vendors")
                .Add("Email and phone",
                    rows.Count(r =>
                        !string.IsNullOrWhiteSpace(r.Email) &&
                        !string.IsNullOrWhiteSpace(r.PhoneNumber)),
                    "good")
                .Add("One of the two",
                    rows.Count(r =>
                        string.IsNullOrWhiteSpace(r.Email) !=
                        string.IsNullOrWhiteSpace(r.PhoneNumber)),
                    "warning")
                .Add("Neither", noContact, "critical");

            model.Breakdowns.AddRange(
                new[] { byCategory, contactable }
                    .Where(b => b.Total > 0));

            return ReportPayload.ForView(model);
        }

        public async Task<ReportPayload> MaintenanceAsync(
            ReportFilter filter,
            bool export)
        {
            var today = DateTime.Today;

            var query = _context.AssetMaintenances
                .AsNoTracking()
                .Include(m => m.Asset)
                    .ThenInclude(a => a.AssignedToEmployee)
                .Include(m => m.Vendor)
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(m => m.ScheduledFor >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(m => m.ScheduledFor <= filter.To.Value);

            if (filter.VendorId.HasValue)
                query = query.Where(m => m.VendorId == filter.VendorId.Value);

            query = filter.Show switch
            {
                "overdue" => query.Where(m =>
                    m.Status == MaintenanceStatus.Scheduled &&
                    m.ScheduledFor < today),

                "scheduled" => query.Where(m =>
                    m.Status == MaintenanceStatus.Scheduled),

                "completed" => query.Where(m =>
                    m.Status == MaintenanceStatus.Completed),

                "repairs" => query.Where(m =>
                    m.Type == MaintenanceType.Repair),

                _ => query
            };

            var rows = await query
                .OrderByDescending(m => m.ScheduledFor)
                .Select(m => new MaintenanceReportRow
                {
                    Asset = m.Asset.DeviceModel,
                    SerialNumber = m.Asset.SerialNumber,
                    HeldBy = m.Asset.AssignedToEmployee != null
                        ? m.Asset.AssignedToEmployee.FirstName + " " +
                          m.Asset.AssignedToEmployee.LastName
                        : string.Empty,
                    Type = m.Type,
                    Status = m.Status,
                    ScheduledFor = m.ScheduledFor,
                    CompletedOn = m.CompletedOn,
                    DoneBy = m.Vendor != null
                        ? m.Vendor.Name
                        : (m.PerformedBy ?? string.Empty),
                    Cost = m.Cost,
                    Notes = m.Notes,
                    IsOverdue =
                        m.Status == MaintenanceStatus.Scheduled &&
                        m.ScheduledFor < today,
                    DaysToComplete = m.CompletedOn != null
                        ? EF.Functions.DateDiffDay(
                            m.ScheduledFor, m.CompletedOn.Value)
                        : (int?)null
                })
                .ToListAsync();

            if (export)
            {
                return ReportPayload.ForCsv(
                    "asset-maintenance",
                    new[]
                    {
                        "Machine", "Serial number", "Held by", "Work",
                        "Status", "Scheduled for", "Completed on",
                        "Days to complete", "Done by", "Cost",
                        "Work done / notes"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Asset,
                        r.SerialNumber,
                        r.HeldBy,
                        r.Type.ToString(),
                        r.IsOverdue ? "Overdue" : r.Status.ToString(),
                        r.ScheduledFor.ToString("yyyy-MM-dd"),
                        ReportMath.D(r.CompletedOn),
                        r.DaysToComplete?.ToString(),
                        r.DoneBy,
                        r.Cost?.ToString("0.00"),
                        r.Notes
                    }));
            }

            var completed = rows
                .Where(r => r.Status == MaintenanceStatus.Completed)
                .ToList();

            var overdue = rows.Count(r => r.IsOverdue);

            var turnaround = completed
                .Where(r => r.DaysToComplete.HasValue)
                .Select(r => (double)r.DaysToComplete!.Value)
                .ToList();

            var model = new ReportViewModel<MaintenanceReportRow>
            {
                Title = "Asset maintenance",
                Description =
                    "Work scheduled and done on the fleet, what it cost " +
                    "and who did it.",
                Action = "Maintenance",
                Filter = filter,
                Rows = rows,
                Coverage = ReportMath.Period(filter) +
                    filter.Show switch
                    {
                        "overdue" => ", overdue work only",
                        "scheduled" => ", work still outstanding",
                        "completed" => ", completed work only",
                        "repairs" => ", repairs only",
                        _ => string.Empty
                    },
                Stats =
                {
                    new ReportStat("Jobs", rows.Count.ToString()),
                    new ReportStat(
                        "Completed",
                        completed.Count.ToString(),
                        rows.Count == 0
                            ? null
                            : $"{(decimal)completed.Count / rows.Count:P0} of those shown"),
                    new ReportStat(
                        "Overdue",
                        overdue.ToString(),
                        overdue == 0 ? "nothing outstanding" : "past the date"),
                    new ReportStat(
                        "Spend",
                        rows.Sum(r => r.Cost ?? 0m).ToString("N2"),
                        "on the work shown")
                }
            };

            if (turnaround.Any())
            {
                model.Stats.Add(new ReportStat(
                    "Average turnaround",
                    $"{turnaround.Average():0.#} days",
                    "scheduled to completed"));
            }

            var byType = new ReportBreakdown("Spend by work type", "");

            foreach (var g in rows
                .GroupBy(r => r.Type)
                .OrderByDescending(g => g.Sum(r => r.Cost ?? 0m)))
            {
                byType.Add(g.Key.ToString(), g.Sum(r => r.Cost ?? 0m));
            }

            var byStatus = new ReportBreakdown("By status", "jobs")
                .Add("Overdue", overdue, "critical")
                .Add(
                    "Scheduled",
                    rows.Count(r =>
                        r.Status == MaintenanceStatus.Scheduled &&
                        !r.IsOverdue),
                    "warning")
                .Add("Completed", completed.Count, "good")
                .Add(
                    "Cancelled",
                    rows.Count(r => r.Status == MaintenanceStatus.Cancelled));

            var byMachine = new ReportBreakdown(
                "Most worked on", "jobs");

            foreach (var g in rows
                .GroupBy(r => r.Asset)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Sum(r => r.Cost ?? 0m))
                .Take(8))
            {
                byMachine.Add(g.Key, g.Count());
            }

            var byProvider = new ReportBreakdown("Spend by provider", "");

            foreach (var g in rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.DoneBy)
                    ? "Not recorded"
                    : r.DoneBy)
                .OrderByDescending(g => g.Sum(r => r.Cost ?? 0m))
                .Take(8))
            {
                byProvider.Add(g.Key, g.Sum(r => r.Cost ?? 0m));
            }

            model.Breakdowns.AddRange(
                new[] { byStatus, byType, byMachine, byProvider }
                    .Where(b => b.Total > 0));

            return ReportPayload.ForView(model);
        }

        public async Task<ReportPayload> LicencesAsync(
            ReportFilter filter,
            bool export)
        {
            var today = DateTime.Today;
            var soon = today.AddDays(ExpiryWindows.LicenceDays);

            var query = _context.Licenses
                .AsNoTracking()
                .Include(l => l.Vendor)
                .Include(l => l.AssignedToEmployee)
                .Include(l => l.Renewals)
                .AsQueryable();

            if (!filter.IncludeInactive)
                query = query.Where(l => l.Status == LicenseStatus.Active);

            if (filter.LicenceType.HasValue)
                query = query.Where(l => l.Type == filter.LicenceType.Value);

            if (filter.VendorId.HasValue)
                query = query.Where(l => l.VendorId == filter.VendorId.Value);

            query = filter.Show switch
            {
                "expired" => query.Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate < today),

                "expiring" => query.Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate >= today &&
                    l.ExpiryDate <= soon),

                "autorenew" => query.Where(l => l.AutoRenews),

                _ => query
            };

            var rows = await query
                .OrderBy(l => l.ExpiryDate)
                .Select(l => new LicenseReportRow
                {
                    Name = l.Name,
                    Type = l.Type,
                    Status = l.Status,
                    Reference = l.Reference,
                    Vendor = l.Vendor != null ? l.Vendor.Name : string.Empty,
                    HeldBy = l.AssignedToEmployee != null
                        ? l.AssignedToEmployee.FirstName + " " +
                          l.AssignedToEmployee.LastName
                        : string.Empty,
                    Seats = l.Seats,
                    StartDate = l.StartDate,
                    ExpiryDate = l.ExpiryDate,
                    Cost = l.Cost,
                    AutoRenews = l.AutoRenews,
                    Renewals = l.Renewals.Count,
                    LastRenewedOn = l.Renewals
                        .OrderByDescending(r => r.RenewedOn)
                        .Select(r => (DateTime?)r.RenewedOn)
                        .FirstOrDefault(),
                    DaysToExpiry = EF.Functions.DateDiffDay(
                        today, l.ExpiryDate)
                })
                .ToListAsync();

            if (export)
            {
                return ReportPayload.ForCsv(
                    "licences",
                    new[]
                    {
                        "Licence", "Type", "Status", "Reference", "Vendor",
                        "Held by", "Seats", "Started", "Expires",
                        "Days to expiry", "Cost", "Auto renews",
                        "Renewals", "Last renewed"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Name,
                        r.Type.ToString(),
                        r.Status.ToString(),
                        r.Reference,
                        r.Vendor,
                        r.HeldBy,
                        r.Seats?.ToString(),
                        ReportMath.D(r.StartDate),
                        r.ExpiryDate.ToString("yyyy-MM-dd"),
                        r.DaysToExpiry.ToString(),
                        r.Cost?.ToString("0.00"),
                        r.AutoRenews ? "Yes" : "No",
                        r.Renewals.ToString(),
                        ReportMath.D(r.LastRenewedOn)
                    }));
            }

            var live = rows
                .Where(r => r.Status == LicenseStatus.Active)
                .ToList();

            var expired = live.Count(r => r.DaysToExpiry < 0);

            var expiring = live.Count(r =>
                r.DaysToExpiry >= 0 &&
                r.DaysToExpiry <= ExpiryWindows.LicenceDays);

            var model = new ReportViewModel<LicenseReportRow>
            {
                Title = "Licences and renewals",
                Description =
                    "What expires when, what it costs and who supplies it.",
                Action = "Licences",
                Filter = filter,
                Rows = rows,
                Coverage = ReportMath.Slice(filter, soon),
                Stats =
                {
                    new ReportStat("Licences", rows.Count.ToString()),
                    new ReportStat(
                        "Lapsed",
                        expired.ToString(),
                        expired == 0 ? "none overdue" : "past the renewal date"),
                    new ReportStat(
                        "Expiring soon",
                        expiring.ToString(),
                        $"within {ExpiryWindows.LicenceDays} days"),
                    new ReportStat(
                        "Annual cost",
                        live.Sum(r => r.Cost ?? 0m).ToString("N2"),
                        "across live licences")
                }
            };

            var byType = new ReportBreakdown("By type", "licences");

            foreach (var g in rows
                .GroupBy(r => r.Type)
                .OrderByDescending(g => g.Count()))
            {
                byType.Add(g.Key.ToString(), g.Count());
            }

            var byVendor = new ReportBreakdown("Cost by vendor", "");

            foreach (var g in live
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Vendor)
                    ? "Not on the register"
                    : r.Vendor)
                .OrderByDescending(g => g.Sum(r => r.Cost ?? 0m))
                .Take(8))
            {
                byVendor.Add(g.Key, g.Sum(r => r.Cost ?? 0m));
            }

            var byWindow = new ReportBreakdown("Renewal window", "licences")
                .Add("Lapsed", expired, "critical")
                .Add(
                    $"Next {ExpiryWindows.LicenceDays} days",
                    expiring,
                    "warning")
                .Add(
                    "Later this year",
                    live.Count(r =>
                        r.DaysToExpiry > ExpiryWindows.LicenceDays &&
                        r.DaysToExpiry <= 365))
                .Add(
                    "Beyond a year",
                    live.Count(r => r.DaysToExpiry > 365),
                    "good");

            model.Breakdowns.AddRange(
                new[] { byWindow, byType, byVendor }
                    .Where(b => b.Total > 0));

            return ReportPayload.ForView(model);
        }
    }
}

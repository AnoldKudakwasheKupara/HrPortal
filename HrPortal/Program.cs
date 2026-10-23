using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.Services.Reporting;
using HrPortal.Services.Notifications;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No 'DefaultConnection' connection string was found. Set one in " +
        "appsettings.Development.json, in user secrets " +
        "(dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" " +
        "\"...\"), or in the ConnectionStrings__DefaultConnection " +
        "environment variable.");
}

builder.Configuration.AddDatabaseSettings(connectionString);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sql => sql.EnableRetryOnFailure()
    ));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;

        options.User.RequireUniqueEmail = true;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders()
    .AddSignInManager<EmployeeSignInManager>()
    .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<LeaveBalanceService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<HrContactService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<WorkingCalendar>();
builder.Services.AddScoped<TimesheetRegisterService>();
builder.Services.AddScoped<TimesheetEntryValidator>();
builder.Services.AddScoped<PolicyService>();
builder.Services.AddScoped<EmployeeRecordService>();
builder.Services.AddScoped<LeaveReportService>();
builder.Services.AddScoped<TrainingReportService>();
builder.Services.AddScoped<PeopleReportService>();
builder.Services.AddScoped<AssetReportService>();
builder.Services.AddScoped<OperationsReportService>();
builder.Services.AddScoped<ReportsOverviewService>();
builder.Services.AddScoped<LeaveWorkflowService>();
builder.Services.AddScoped<LoanWorkflowService>();
builder.Services.AddScoped<CourseApplicationWorkflowService>();
builder.Services.AddScoped<RecordScope>();
builder.Services.AddScoped<TimesheetAccessService>();
builder.Services.AddScoped<WeeklyReportBuilder>();


builder.Services.Configure<LeaveOptions>(
    builder.Configuration.GetSection(LeaveOptions.Section));

builder.Services.AddSingleton<EmailService>();

builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.Section));

builder.Services.AddSingleton<BrandAssets>();
builder.Services.AddSingleton<WeeklyReportPdfService>();
builder.Services.AddSingleton<NextOfKinPdfService>();
builder.Services.AddScoped<LeaveApplicationPdfService>();
builder.Services.AddSingleton<LoanApplicationPdfService>();
builder.Services.AddSingleton<CourseApplicationPdfService>();

builder.Services.Configure<WeeklyReportBranding>(
    builder.Configuration.GetSection(WeeklyReportBranding.Section));

builder.Services.AddScoped<
    IAuthorizationHandler,
    EmployeeOwnedHandler>();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;

    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();

    options.MimeTypes = ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "image/svg+xml" });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.AddControllersWithViews();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToAreaFolder(
        "Identity",
        "/Account");

    options.Conventions.AllowAnonymousToAreaPage(
        "Identity",
        "/Error");

    options.Conventions.AuthorizeAreaFolder(
        "Identity",
        "/Account/Manage");
});

var app = builder.Build();

if (DatabaseConfigurationProvider.LastLoadError != null)
{
    app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Settings")
        .LogWarning(
            DatabaseConfigurationProvider.LastLoadError,
            "Settings could not be read from the database, so the " +
            "values in appsettings.json are being used instead. " +
            "Anything an administrator changed on the settings page " +
            "is not in effect.");
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var db =
        services.GetRequiredService<ApplicationDbContext>();

    try
    {
        await db.Database.MigrateAsync();

        var userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();

        var roleManager =
            services.GetRequiredService<RoleManager<IdentityRole<int>>>();

        await DbSeeder.SeedAdministratorAsync(
            userManager,
            roleManager);

        await DbSeeder.SeedAdministratorEmployeeAsync(
            userManager,
            db);

        await DbSeeder.BackfillEmployeeRoleAsync(
            userManager,
            db);

        var storage = services.GetRequiredService<FileStorageService>();

        storage.MoveLegacyUploads(
            services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Uploads"));
    }
    catch (SqlException ex)
    {
        var target = new SqlConnectionStringBuilder(connectionString);

        var logger = services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

        logger.LogError(
            ex,
            "Could not prepare the database '{Database}' on server " +
            "'{Server}'. Check that SQL Server is installed and running " +
            "on this machine and that your Windows account can reach it, " +
            "or point 'ConnectionStrings:DefaultConnection' at a server " +
            "you can use. See README.md for first-run setup.",
            target.InitialCatalog,
            target.DataSource);

        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseResponseCompression();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var oneWeek = TimeSpan.FromDays(7);

        ctx.Context.Response.GetTypedHeaders().CacheControl =
            new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = oneWeek
            };
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

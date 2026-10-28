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

// Since QuestPDF 2026.9 a glyph missing from every registered font
// stops generation outright. These are HR forms carrying names,
// addresses and free-text comments, and an employee whose name holds
// one unusual character should not be unable to print their own
// paperwork. The documents fall back from Montserrat to the Lato
// QuestPDF ships with, and anything neither font has prints as a
// replacement mark rather than failing the download.
QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;

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
builder.Services.AddScoped<NotificationBadgeService>();

builder.Services.AddScoped<SupportContactsService>();

builder.Services.AddScoped<ApproverDirectory>();
builder.Services.AddScoped<TrainingAssignmentService>();
builder.Services.AddScoped<SupervisorService>();

builder.Services.AddScoped<SupervisorQueueService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<WorkingCalendar>();
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
builder.Services.AddScoped<WeeklyReportBuilder>();


builder.Services.AddSingleton<EmailService>();

builder.Services.AddScoped<INotificationChannel, EmailNotificationChannel>();

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.Section));

builder.Services.AddSingleton<BrandAssets>();
builder.Services.AddSingleton<WeeklyReportPdfService>();
builder.Services.AddSingleton<RecordDocumentPdfService>();
builder.Services.AddSingleton<ListDocumentPdfService>();

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

    // Deliberately not the framework's default list, which includes
    // text/html and application/json. Compressing a response that
    // holds a secret alongside anything an attacker can influence
    // lets them read the secret back out of the compressed size --
    // the BREACH attack -- and every page here carries an antiforgery
    // token a few lines away from a search box. Stylesheets, scripts
    // and fonts keep no secrets and echo nothing back, and they are
    // where the weight is anyway.
    options.MimeTypes = new[]
    {
        "text/css",
        "text/javascript",
        "application/javascript",
        "image/svg+xml",
        "font/woff",
        "font/woff2"
    };
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

HrPortal.Services.DocumentFonts.Register(
    app.Environment,
    app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger(nameof(HrPortal.Services.DocumentFonts)));

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

        var adminSeed = AdminSeed.From(
            app.Configuration,
            app.Environment.IsDevelopment());

        await DbSeeder.SeedAdministratorAsync(
            userManager,
            roleManager,
            adminSeed,
            app.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(DbSeeder)));

        await DbSeeder.SeedAdministratorEmployeeAsync(
            userManager,
            db,
            adminSeed.Email);

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

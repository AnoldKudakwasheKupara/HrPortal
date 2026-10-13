using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community licence: free for organisations under the
// revenue threshold set out in its licensing terms.
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
    .AddSignInManager<EmployeeSignInManager>();

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
builder.Services.AddScoped<LeaveWorkflowService>();
builder.Services.AddScoped<RecordScope>();
builder.Services.AddScoped<TimesheetAccessService>();
builder.Services.AddScoped<WeeklyReportBuilder>();
builder.Services.AddScoped<RequisitionWorkflowService>();

builder.Services.Configure<RequisitionOptions>(
    builder.Configuration.GetSection(RequisitionOptions.Section));
builder.Services.AddSingleton<WeeklyReportPdfService>();

builder.Services.Configure<WeeklyReportBranding>(
    builder.Configuration.GetSection(WeeklyReportBranding.Section));


builder.Services.AddScoped<
    IAuthorizationHandler,
    EmployeeOwnedHandler>();

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

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

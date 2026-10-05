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

// A new machine can point at its own SQL Server without editing the
// committed appsettings.json, via user secrets, an environment
// variable, or ConnectionStrings__DefaultConnection.
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
    // Must come after AddDefaultUI, which registers the default
    // SignInManager itself and would otherwise replace this one.
    .AddSignInManager<EmployeeSignInManager>();

// Re-check the security stamp often enough that a user who is
// deactivated mid-session loses access promptly.
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

// Scoped, not singleton: the handler resolves the caller's
// employee record, which needs the request's DbContext.
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
        // Creates the database on a machine that has never run the app
        // before, then brings it up to the latest migration. Must happen
        // before anything else touches the database.
        await db.Database.MigrateAsync();

        var userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();

        var roleManager =
            services.GetRequiredService<RoleManager<IdentityRole<int>>>();

        await DbSeeder.SeedAdministratorAsync(
            userManager,
            roleManager);
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
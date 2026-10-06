# My Intellego

Staff portal for Intellego Investment Consultants: leave, training,
employee records and the IT asset register. ASP.NET Core MVC with
ASP.NET Identity and EF Core (SQL Server).

The solution, project and database are still named `HrPortal` — only
the product name changed.

## First run on a new machine

```bash
dotnet run --project HrPortal/HrPortal.csproj
```

That is the whole setup. On startup the app applies any pending EF Core
migrations, which **creates `HrPortalDB` if it does not exist yet**, then seeds
the `Administrator` role and account. You do not need to run
`dotnet ef database update` by hand, and you do not need to create the database
in SSMS first.

### Sign in

The seeded administrator is defined in
[`HrPortal/Data/DbSeeder.cs`](HrPortal/Data/DbSeeder.cs). Change that password
before this runs anywhere other than a development machine.

### If the database still will not open

The default connection string in
[`appsettings.json`](HrPortal/appsettings.json) is:

```
Server=.;Database=HrPortalDB;Trusted_Connection=True;TrustServerCertificate=True;
```

`Server=.` means *the default SQL Server instance on this machine*. A machine
that has Visual Studio but no SQL Server Express/Developer instance has no such
server, and a machine with only a named instance will not match either. Point
the app at whatever server that machine actually has, without editing the
committed file:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=HrPortalDB;Trusted_Connection=True;TrustServerCertificate=True;" --project HrPortal/HrPortal.csproj
```

User secrets live outside the repository, so each machine keeps its own. An
environment variable named `ConnectionStrings__DefaultConnection` works too, and
takes precedence over `appsettings.json` the same way.

Common server values:

| Setup | `Server=` |
| --- | --- |
| SQL Server Express/Developer, default instance | `.` or `localhost` |
| SQL Server Express, named instance | `.\SQLEXPRESS` |
| LocalDB (ships with Visual Studio) | `(localdb)\mssqllocaldb` |

## Database changes

After editing an entity, add a migration; it is applied automatically the next
time the app starts.

```bash
dotnet ef migrations add <Name> --project HrPortal/HrPortal.csproj
```

`dotnet ef` is a global tool: `dotnet tool install --global dotnet-ef`.

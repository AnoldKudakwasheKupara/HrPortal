# My Intellego

Staff portal for Intellego Investment Consultants: leave, training,
employee records and the IT asset register. ASP.NET Core MVC with
ASP.NET Identity and EF Core on PostgreSQL. It is hosted on Render.

The solution, project and database are still named `HrPortal` — only
the product name changed.

## First run on a new machine

You need PostgreSQL (16 or later) running locally, and a login the portal
can use that is allowed to create databases. Create one once, as the
`postgres` superuser:

```sql
CREATE ROLE hrportal_dev LOGIN CREATEDB PASSWORD 'choose-a-password';
```

Then tell the portal about it. The connection string goes in user secrets,
which live outside the repository — the repository is public, so a password
must never go in `appsettings.json`:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=HrPortal;Username=hrportal_dev;Password=choose-a-password" --project HrPortal/HrPortal.csproj
```

And run it:

```bash
dotnet run --project HrPortal/HrPortal.csproj
```

On startup the app applies any pending EF Core migrations, which **creates
the `HrPortal` database if it does not exist yet**, then seeds the
`Administrator` role and account. You do not need to run
`dotnet ef database update` by hand.

### Sign in

In Development the seeded administrator is `admin@hrportal.local` with the
password in [`HrPortal/Data/DbSeeder.cs`](HrPortal/Data/DbSeeder.cs).
Anywhere else no account is created unless `SeedAdministrator:Email` and
`SeedAdministrator:Password` are configured.

### If the database will not open

The startup log names the database and host it tried. Check that PostgreSQL
is running, that the login in the connection string exists and has
`CREATEDB`, and that the password matches. An environment variable named
`ConnectionStrings__DefaultConnection` works in place of user secrets, and so
does a `postgres://user:password@host:5432/database` address in
`DATABASE_URL`.

## Database changes

After editing an entity, add a migration; it is applied automatically the next
time the app starts.

```bash
dotnet ef migrations add <Name> --project HrPortal/HrPortal.csproj
```

`dotnet ef` is a global tool: `dotnet tool install --global dotnet-ef`.

Dates are stored as `timestamp without time zone`, the way SQL Server's
`datetime2` stored them, through Npgsql's legacy timestamp switch at the top
of `Program.cs`. Remove it and every date typed into a form will be refused
by the database.

## Hosting on Render

[`render.yaml`](render.yaml) describes everything: a Docker web service in
Frankfurt built from the [`Dockerfile`](Dockerfile), a 5 GB disk mounted on
`App_Data` for uploads, outgoing mail and the sign-in keys, and a PostgreSQL
database whose address reaches the app as `DATABASE_URL`.

To create it, in the Render dashboard choose **New → Blueprint**, pick this
repository, and fill in the two values it asks for:

| Variable | Value |
| --- | --- |
| `SeedAdministrator__Email` | the first administrator's email |
| `SeedAdministrator__Password` | their first password (8+ characters, upper, lower and a digit) |

The administrator is created on the first start only, so the password
variable can be deleted afterwards. Every push to `master` redeploys.

Email is off until configured. It goes out through Microsoft Graph from a
Microsoft 365 mailbox. Under the service's **Environment** tab, set
`Email__Enabled` to `true`, `Email__Delivery` to `Graph`, and fill in
`Email__FromAddress` (the sending mailbox), `Email__TenantId`,
`Email__ClientId`, `Email__ClientSecret` and `Email__PortalBaseUrl` (the
portal's address, used in links).

The app registration needs no Graph permission of its own in Entra.
Sending is granted in Exchange Online, limited to the one mailbox:

```powershell
New-ServicePrincipal -AppId <client id> -ObjectId <enterprise app object id> -DisplayName "My Intellego"
New-ManagementScope -Name "My Intellego mailbox" -RecipientRestrictionFilter "PrimarySmtpAddress -eq 'no-reply@intellego-ic.com'"
New-ManagementRoleAssignment -App <enterprise app object id> -Role "Application Mail.Send" -CustomResourceScope "My Intellego mailbox"
```

Granting Mail.Send in Entra as well would let the app send as anyone, so
leave it out there.

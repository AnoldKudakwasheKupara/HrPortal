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

Tests live in `HrPortal.Tests` and run without a database:

```bash
dotnet test
```

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

The password vault (Administrators → Password Vault) holds a service,
username and password for each local system. There is nothing to set up: the
portal makes its encryption key the first time it is needed and keeps it in
`App_Data/keys/vault.key`, on the persistent disk. Backups include the key, so
keep them safe, and do not delete that file: anything stored becomes unreadable
without it.

Sanctions screening (Compliance → Sanctions Screening) is open only to
users with the Compliance Officer role; administrators do not see it
unless they are given that role too. Names are raised as possible matches
from a similarity of 75 out of 100; to raise fewer, set
`Sanctions__NameMatchThreshold` higher (60 to 99). It reads PDFs that
contain text; a scanned image is kept but reported as unreadable, to be
checked by hand.

Leave balances: a leave type with **Days earned per month** set (Leave
Types) has a running balance per employee. HR uploads the starting figures
under Leave Balances → Upload Balances. The portal adds the monthly days on
the 1st, catching up any months missed while it was off, deducts leave at
final approval, allows the balance to go below zero, and carries it across
years. Each change is listed on the balance's page and on the employee's My
Leave page.

## The group's companies

One service and one database serve every company in the group, today
Intellego and MMC Capital. The web address a person uses decides which
company's portal they see: its name, colours and logo, its staff and its
records. Any address not listed belongs to the main company, which is
Intellego unless `Brand__Key` says otherwise.

On Render, add each company's address to the same service as a custom domain
and set, for MMC Capital say:

- `Brand__Hosts__mmc`: the addresses that show MMC, without `https://`,
  separated by commas, such as `hr.mmccapitalzim.com`.
- `Brand__Urls__mmc`: where its emails link to, such as
  `https://hr.mmccapitalzim.com`. The main company uses
  `Email__PortalBaseUrl`.

Running locally, `http://mmc.localhost:<port>` shows MMC.

MMC Capital also has its own Render service, `mmcportal` in
[`render.yaml`](render.yaml), on the same database. It sets `Brand__Key` to
`mmc`, so every address it serves shows MMC. Its email settings are copied
from `hrportal`. Two settings keep the copies from getting in each other's
way:

- `BackgroundJobs__Enabled=false`: only `hrportal` sends the daily reminders
  and adds the monthly leave days.
- `Vault__Enabled=false`: the vault key is on `hrportal`'s disk, so the
  password vault is only on Intellego's portal.

Each service has its own disk, so a file uploaded on one portal does not
open on the other.

**Emails.** Every email is sent as the company the person works for (their
home company): its name, logo, colours and links, whichever portal or job
sent it. `Brand__Urls__mmc` and `Brand__Urls__intellego` tell each service
where the other portal is, so links open the right one.

Microsoft 365 shows the sending mailbox's own name, so MMC's emails need an
MMC mailbox. MMC is in a separate Microsoft 365 organisation, so register an
app there the same way as above (Exchange `Application Mail.Send`, scoped to
the MMC mailbox only), and set these on **both** services, since `hrportal`
sends the reminders and `mmcportal` the rest:

| Variable | Value |
| --- | --- |
| `Brand__Mail__mmc__FromAddress` | the MMC sending mailbox, e.g. `no-reply@mmccapitalzim.com` |
| `Brand__Mail__mmc__TenantId` | MMC's tenant ID |
| `Brand__Mail__mmc__ClientId` | the app's client ID in MMC's tenant |
| `Brand__Mail__mmc__ClientSecret` | its client secret |

Until they are set, MMC's emails are branded MMC but go out from Intellego's
mailbox, so they show Intellego's mailbox name as the sender. Give the MMC
mailbox the display name the staff should see, such as "My MMC".

**People.** Every employee has a home company and works for one or more
companies, both set on the employee form. Somebody who works for both, such
as the IT team, has one record, one sign-in and one leave balance, and sees
the same requests on either portal. Nobody can sign in to the portal of a
company they do not work for; the administrator can sign in everywhere.
Taking somebody off a company ends any sign-in they have there.

**Roles.** Administrator and Employee hold everywhere. HR Officer, MD,
Compliance Officer and any role added later are given on the Roles page of
the company's own portal and hold only there, so the HR Manager at MMC is an
ordinary employee on Intellego's portal. Everybody who held a role before
companies were added holds it at Intellego.

Two roles only look. A Leave Viewer sees every leave application at the
company. A Records Viewer sees every leave balance and every leave, loan and
course application from the moment it is sent, whoever it is waiting on,
under Records in the menu. Neither can approve, edit or delete anything.

**What is separate.** Each portal shows its own staff, and their leave,
balances, loans, course applications, documents, certifications, training
and to-dos; its own policies, document folders, training courses, company
weekly reports and departments. A department can instead serve the whole
group, as IT does.

**What is shared.** Assets, vendors, licences, the IT log and the password
vault; leave types, public holidays and document categories; the audit log,
settings and notifications. Sanctions screening is one register for the whole
group for now.

**Approvals.** A request is decided by the HR and directors of the company
that employs the person, whichever portal it was made on. The other portal
shows it without the buttons. Supervisors act wherever they are. At MMC,
HR's approval of leave is final; loans and courses end with a director, and
either director may decide.

**Adding a company**, say with the key `example`:

1. Add `HrPortal/Brands/example.json`:

   ```json
   {
     "PortalName": "Example Portal",
     "ShortName": "Example",
     "FullName": "Example Holdings",
     "LegalName": "Example Holdings (Pvt) Ltd",
     "LetterheadName": "EXAMPLE HOLDINGS\n(PVT) LTD",
     "Tagline": "First line | Second line",
     "Colors": {
       "Primary": "#102a6b",
       "Sidebar": "#0a1a44",
       "Accent": "#b8860b",
       "AccentSoft": "#e8cf8a"
     }
   }
   ```

   The other shades are worked out from those four. Sidebar, Primary and Accent
   carry white text, so they must be dark enough for it. To set a shade exactly,
   add it under `"Overrides"`, using a name from `CssVariables` in
   `Services/Brand.cs`.

   A company that names its approvers differently, or whose HR has the last
   word on leave, says so in the same file, as `Brands/mmc.json` does:
   `HrTitle`, `DirectorTitle`, `TheDirector`, `TheDirectorShort`,
   `CourseApproverTitle`, `TheCourseApprover` and `LeaveEndsWithHr`.
2. Add its artwork to `HrPortal/wwwroot/brands/example/`, under the same names as
   `brands/intellego`: `logo.png`, `logo-on-dark.png`, `logo-email.png`,
   `icon-32.png`, `icon-180.png` and `icon-512.png`.
3. Add its front page wording in `HrPortal/Views/Brands/example/_About.cshtml`. The
   page still works without it, showing no About section.
4. Set `Brand__Hosts__example` and `Brand__Urls__example`, and add the address
   to the Render service. The company itself is added to the database when the
   portal starts.

`dotnet test` checks every brand has its artwork and readable colours.

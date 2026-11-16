using System.Text.RegularExpressions;
using HrPortal.Entities;
using HrPortal.Services.Sanctions;

namespace HrPortal.Services
{
    /// <summary>What reading a complaints register found.</summary>
    public sealed class ComplaintImport
    {
        public List<Complaint> Complaints { get; } = new();

        public int Received { get; set; }

        public List<string> Problems { get; } = new();
    }

    /// <summary>
    /// Reads a complaints register kept in a spreadsheet, such as the
    /// one MMC Stockbrokers kept before the portal. The columns are
    /// found by their headings, wherever the heading row sits, and the
    /// dates are read day first in whichever way they were typed.
    /// </summary>
    public static class ComplaintImporter
    {
        private enum Column
        {
            ReceivedOn,
            ClientName,
            ClientPhone,
            ClientEmail,
            Nature,
            StaffInvolved,
            ActionTaken,
            ResolvedOn,
            Status,
            Remarks
        }

        private static readonly Dictionary<Column, string[]> Headings = new()
        {
            [Column.ReceivedOn] = ["datereceived", "received", "date", "datereported", "dateofcomplaint"],
            [Column.ClientName] = ["clientname", "client", "name", "complainant", "customer", "customername"],
            [Column.ClientPhone] = ["phonenumber", "phone", "contactnumber", "cellnumber", "mobile", "telephone"],
            [Column.ClientEmail] = ["emailadress", "emailaddress", "email", "emailadd"],
            [Column.Nature] = ["natureofthecomplaint", "natureofcomplaint", "nature", "complaint", "details"],
            [Column.StaffInvolved] = ["staffmemberinvolved", "staffinvolved", "staffmember", "staff"],
            [Column.ActionTaken] = ["actiontaken", "action"],
            [Column.ResolvedOn] = ["resolutiondate", "dateresolved", "resolvedon", "resolved"],
            [Column.Status] = ["status"],
            [Column.Remarks] = ["remarks", "comments", "notes", "comment"]
        };

        private static string Simple(string heading) =>
            Regex.Replace(heading.ToLowerInvariant(), "[^a-z]", "");

        public static ComplaintImport Read(Stream file, string fileName)
        {
            var table = ClientListReader.ReadTable(file, fileName);
            var rows = new List<List<string>> { table.Headings };
            rows.AddRange(table.Rows);

            var result = new ComplaintImport();

            var headingRow = rows.FindIndex(r =>
                r.Select(c => Simple(c ?? string.Empty)).Any(c => Headings[Column.ClientName].Contains(c)));

            if (headingRow < 0)
            {
                result.Problems.Add(
                    "No column is headed Client Name, so nothing could be read. " +
                    "Keep the heading row from the register.");

                return result;
            }

            var map = new Dictionary<Column, int>();
            var headings = rows[headingRow];

            for (var i = 0; i < headings.Count; i++)
            {
                var heading = Simple(headings[i] ?? string.Empty);

                foreach (var (column, names) in Headings)
                {
                    if (!map.ContainsKey(column) && names.Contains(heading))
                    {
                        map[column] = i;
                        break;
                    }
                }
            }

            foreach (var row in rows.Skip(headingRow + 1))
            {
                string? Get(Column column) =>
                    map.TryGetValue(column, out var i) && i < row.Count && !string.IsNullOrWhiteSpace(row[i])
                        ? Regex.Replace(row[i].Trim(), @"[ \t]+", " ")
                        : null;

                if (row.All(string.IsNullOrWhiteSpace))
                    continue;

                result.Received++;

                var name = Get(Column.ClientName);

                if (name == null)
                {
                    result.Problems.Add(
                        $"Complaint {result.Received} has no client name, so it was left out.");
                    continue;
                }

                var (email, spill) = SplitEmail(Get(Column.ClientEmail));
                var nature = Get(Column.Nature);

                if (spill != null)
                    nature = nature == null ? spill : $"{spill} {nature}";

                var receivedText = Get(Column.ReceivedOn);
                var received = ClientListReader.ParseDate(receivedText);

                if (receivedText != null && received == null)
                {
                    result.Problems.Add(
                        $"{name}: the date received \"{receivedText}\" could not be read, so it was left blank.");
                }

                var resolvedText = Get(Column.ResolvedOn);
                var resolved = ClientListReader.ParseDate(resolvedText);

                if (resolvedText != null && resolved == null)
                {
                    result.Problems.Add(
                        $"{name}: the resolution date \"{resolvedText}\" could not be read, so it was left blank.");
                }

                var statusText = Get(Column.Status)?.ToLowerInvariant();

                var status = statusText is "resolved" or "closed" or "done" ||
                             (statusText == null && resolved != null)
                    ? ComplaintStatus.Resolved
                    : ComplaintStatus.Pending;

                result.Complaints.Add(new Complaint
                {
                    ReceivedOn = received,
                    ClientName = Cap(name, 200)!,
                    ClientPhone = Cap(Get(Column.ClientPhone), 50),
                    ClientEmail = Cap(email, 200),
                    Nature = Cap(nature, 4000),
                    StaffInvolved = Cap(Get(Column.StaffInvolved), 300),
                    ActionTaken = Cap(Get(Column.ActionTaken), 4000),
                    ResolvedOn = status == ComplaintStatus.Resolved ? resolved : null,
                    Status = status,
                    Remarks = Cap(Get(Column.Remarks), 4000)
                });
            }

            return result;
        }

        /// <summary>
        /// An email cell sometimes has the complaint typed after the
        /// address. The address is kept as the email and the rest is
        /// handed back to go with the complaint.
        /// </summary>
        public static (string? Email, string? Leftover) SplitEmail(string? cell)
        {
            if (string.IsNullOrWhiteSpace(cell))
                return (null, null);

            var parts = cell.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var address = parts.FirstOrDefault(p => p.Contains('@'));

            if (address == null)
                return (null, cell.Trim());

            var rest = string.Join(' ', parts.Where(p => p != address));

            return (address, rest.Length == 0 ? null : rest);
        }

        /// <summary>
        /// Two entries for the same client on the same day about the
        /// same thing are one complaint, so a register can be loaded
        /// again after more rows are added without doubling up.
        /// </summary>
        public static string Key(Complaint c) =>
            string.Join("|",
                Simple(c.ClientName),
                c.ReceivedOn?.ToString("yyyy-MM-dd"),
                Simple(c.Nature ?? string.Empty));

        private static string? Cap(string? value, int length) =>
            value == null ? null : value.Length <= length ? value : value[..length];
    }
}

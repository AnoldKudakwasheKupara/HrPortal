using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HrPortal.Services.Sanctions
{
    /// <summary>The details a client list can give about a client.</summary>
    public enum ClientField
    {
        FullName,
        FirstName,
        MiddleName,
        LastName,
        CompanyName,
        DateOfBirth,
        Nationality,
        IdNumber,
        PassportNumber,
        RegistrationNumber,
        Reference
    }

    public sealed record ClientRow(
        int RowNumber,
        string Name,
        bool? IsCompany,
        string? IdNumber,
        string? PassportNumber,
        string? RegistrationNumber,
        DateTime? DateOfBirth,
        string? Nationality,
        string? Reference);

    /// <summary>A spreadsheet as read: its heading row and the rows under it.</summary>
    public sealed class ClientTable
    {
        public List<string> Headings { get; init; } = new();

        public List<List<string>> Rows { get; init; } = new();

        public string? Cell(List<string> row, int column) =>
            column >= 0 && column < row.Count && !string.IsNullOrWhiteSpace(row[column])
                ? row[column].Trim()
                : null;
    }

    public sealed class ClientImport
    {
        public List<ClientRow> Clients { get; } = new();

        public int Received { get; set; }

        public int Invalid { get; set; }

        public int Duplicates { get; set; }

        /// <summary>Each row that was left out or only partly read, and why.</summary>
        public List<string> Problems { get; } = new();
    }

    /// <summary>
    /// Reads the client list the compliance officer uploads, from a
    /// CSV file or the first sheet of an Excel workbook, with a heading
    /// row. Columns are recognised by their headings in the usual ways
    /// they are written; when they cannot be, the officer says which
    /// column is which.
    ///
    /// Every row is checked: a row without a name is left out, a date
    /// of birth that cannot be read is left blank rather than guessed,
    /// and a row that repeats another exactly is counted as a duplicate.
    /// </summary>
    public static class ClientListReader
    {
        private static readonly Dictionary<ClientField, string[]> Headings = new()
        {
            [ClientField.FullName] = new[] { "full name", "fullname", "name", "client name", "customer name", "client", "customer", "account name", "names" },
            [ClientField.FirstName] = new[] { "first name", "firstname", "forename", "forenames", "given name", "given names", "first names" },
            [ClientField.MiddleName] = new[] { "middle name", "middlename", "middle names", "other names" },
            [ClientField.LastName] = new[] { "last name", "lastname", "surname", "family name" },
            [ClientField.CompanyName] = new[] { "company name", "company", "business name", "entity name", "organisation", "organization", "organisation name", "organization name", "trading name", "registered name" },
            [ClientField.DateOfBirth] = new[] { "date of birth", "dob", "d.o.b", "d.o.b.", "birth date", "birthdate", "born" },
            [ClientField.Nationality] = new[] { "nationality", "citizenship", "country of citizenship" },
            [ClientField.IdNumber] = new[] { "id number", "id no", "id no.", "id", "national id", "national id number", "national id no", "identity number", "identity no", "id/passport", "id / passport", "id or passport" },
            [ClientField.PassportNumber] = new[] { "passport number", "passport no", "passport no.", "passport" },
            [ClientField.RegistrationNumber] = new[] { "registration number", "registration no", "registration no.", "company registration", "company registration number", "reg number", "reg no", "reg no.", "company number" },
            [ClientField.Reference] = new[] { "client reference", "reference", "ref", "client code", "code", "account number", "account no", "account", "client number", "customer number", "client id", "customer id" }
        };

        public static string Describe(ClientField field) =>
            field switch
            {
                ClientField.FullName => "Full name",
                ClientField.FirstName => "First name",
                ClientField.MiddleName => "Middle name",
                ClientField.LastName => "Last name",
                ClientField.CompanyName => "Company name",
                ClientField.DateOfBirth => "Date of birth",
                ClientField.Nationality => "Nationality",
                ClientField.IdNumber => "ID number",
                ClientField.PassportNumber => "Passport number",
                ClientField.RegistrationNumber => "Registration number",
                _ => "Client reference"
            };

        public static ClientTable ReadTable(Stream file, string fileName)
        {
            var rows = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".xlsx" => ReadXlsx(file),
                ".csv" or ".txt" => ReadCsv(file),
                _ => throw new InvalidDataException(
                    "Upload the client list as an Excel workbook (.xlsx) or a CSV file.")
            };

            rows = rows.Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();

            if (rows.Count == 0)
                return new ClientTable();

            return new ClientTable
            {
                Headings = rows[0].Select(h => h.Trim()).ToList(),
                Rows = rows.Skip(1).ToList()
            };
        }

        /// <summary>The columns recognised from their headings.</summary>
        public static Dictionary<ClientField, int> AutoMap(IReadOnlyList<string> headings)
        {
            var map = new Dictionary<ClientField, int>();

            static string Simple(string h) => Regex.Replace(h.Trim().ToLowerInvariant(), @"[\s_]+", " ");

            for (var i = 0; i < headings.Count; i++)
            {
                var heading = Simple(headings[i]);

                foreach (var (field, names) in Headings)
                {
                    if (!map.ContainsKey(field) && names.Contains(heading))
                    {
                        map[field] = i;
                        break;
                    }
                }
            }

            return map;
        }

        /// <summary>Whether the columns chosen give a name for each client.</summary>
        public static bool HasName(IReadOnlyDictionary<ClientField, int> map) =>
            map.ContainsKey(ClientField.FullName) ||
            map.ContainsKey(ClientField.CompanyName) ||
            map.ContainsKey(ClientField.FirstName) ||
            map.ContainsKey(ClientField.LastName);

        public static ClientImport Import(ClientTable table, IReadOnlyDictionary<ClientField, int> map)
        {
            var result = new ClientImport { Received = table.Rows.Count };
            var seen = new HashSet<string>();

            string? Get(List<string> row, ClientField field) =>
                map.TryGetValue(field, out var column) ? table.Cell(row, column) : null;

            for (var i = 0; i < table.Rows.Count; i++)
            {
                var row = table.Rows[i];
                var number = i + 2;

                var company = Get(row, ClientField.CompanyName);
                var person = Get(row, ClientField.FullName) ??
                             string.Join(" ", new[]
                                 {
                                     Get(row, ClientField.FirstName),
                                     Get(row, ClientField.MiddleName),
                                     Get(row, ClientField.LastName)
                                 }
                                 .Where(p => p != null));

                var name = !string.IsNullOrWhiteSpace(person) ? person : company;

                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Invalid++;
                    result.Problems.Add($"Row {number}: no name, so it was left out.");
                    continue;
                }

                if (name.Length > 500)
                {
                    result.Invalid++;
                    result.Problems.Add($"Row {number}: the name is longer than 500 characters, so it was left out.");
                    continue;
                }

                var dobText = Get(row, ClientField.DateOfBirth);
                var dob = ParseDate(dobText);

                if (dobText != null && dob == null)
                    result.Problems.Add($"Row {number}: the date of birth \"{dobText}\" could not be read, so it was left blank.");

                var client = new ClientRow(
                    number,
                    Regex.Replace(name.Trim(), @"\s+", " "),
                    string.IsNullOrWhiteSpace(person) ? true
                        : company != null || map.ContainsKey(ClientField.FirstName) || map.ContainsKey(ClientField.LastName) ? false
                        : null,
                    Cap(Get(row, ClientField.IdNumber)),
                    Cap(Get(row, ClientField.PassportNumber)),
                    Cap(Get(row, ClientField.RegistrationNumber)),
                    dob,
                    Cap(Get(row, ClientField.Nationality)),
                    Cap(Get(row, ClientField.Reference)));

                var key = string.Join("|",
                    SanctionsListParser.Normalise(client.Name),
                    SanctionsListParser.Normalise(client.IdNumber),
                    SanctionsListParser.Normalise(client.PassportNumber),
                    SanctionsListParser.Normalise(client.RegistrationNumber),
                    client.DateOfBirth?.ToString("yyyy-MM-dd"),
                    SanctionsListParser.Normalise(client.Reference));

                if (!seen.Add(key))
                {
                    result.Duplicates++;
                    result.Problems.Add($"Row {number}: the same as an earlier row ({client.Name}), so it was counted once.");
                    continue;
                }

                result.Clients.Add(client);
            }

            return result;
        }

        private static string? Cap(string? value) =>
            value == null ? null : value.Length <= 100 ? value : value[..100];

        /// <summary>
        /// A date of birth as clients' records write it: day first, as
        /// in Zimbabwe (31/12/1980, 31-12-1980, 31 Dec 1980), ISO
        /// (1980-12-31), or an Excel date number. Anything else is not
        /// guessed at.
        /// </summary>
        public static DateTime? ParseDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            text = text.Trim();

            if (Regex.IsMatch(text, @"^\d{5}(\.\d+)?$") &&
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) &&
                serial > 1 && serial < 80000)
            {
                return DateTime.FromOADate(Math.Floor(serial)).Date;
            }

            var formats = new[]
            {
                "d/M/yyyy", "d-M-yyyy", "d.M.yyyy", "yyyy-M-d", "yyyy/M/d",
                "d MMM yyyy", "d MMMM yyyy", "d-MMM-yyyy", "d MMM. yyyy",
                "d/M/yyyy H:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-M-d H:mm:ss"
            };

            return DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date) &&
                   date.Year >= 1900 && date <= DateTime.Today
                ? date.Date
                : null;
        }
        internal static List<List<string>> ReadCsv(Stream file)
        {
            using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var text = reader.ReadToEnd();
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;

            var delimiter = text.Split('\n').FirstOrDefault()?.Count(c => c == ';') >
                            text.Split('\n').FirstOrDefault()?.Count(c => c == ',')
                ? ';'
                : ',';

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    quoted = true;
                }
                else if (c == delimiter)
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                }
                else if (c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else if (c != '\r')
                {
                    cell.Append(c);
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// The first worksheet of an .xlsx file. A workbook is a zip
        /// of XML parts; the text of most cells is kept once in a
        /// shared table and referred to by number.
        /// </summary>
        private static List<List<string>> ReadXlsx(Stream file)
        {
            using var zip = new ZipArchive(file, ZipArchiveMode.Read);

            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";

            var shared = new List<string>();

            if (zip.GetEntry("xl/sharedStrings.xml") is { } sharedEntry)
            {
                using var stream = sharedEntry.Open();

                shared = XDocument.Load(stream)
                    .Descendants(main + "si")
                    .Select(si => string.Concat(si.Descendants(main + "t").Select(t => t.Value)))
                    .ToList();
            }

            var sheetPath = "xl/worksheets/sheet1.xml";

            if (zip.GetEntry("xl/workbook.xml") is { } workbook &&
                zip.GetEntry("xl/_rels/workbook.xml.rels") is { } rels)
            {
                using var wb = workbook.Open();
                using var rs = rels.Open();

                var firstSheet = XDocument.Load(wb).Descendants(main + "sheet").FirstOrDefault();
                var relId = firstSheet?.Attribute(rel + "id")?.Value;

                var target = XDocument.Load(rs)
                    .Descendants(pkg + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Id")?.Value == relId)
                    ?.Attribute("Target")?.Value;

                if (!string.IsNullOrEmpty(target))
                    sheetPath = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            }

            var sheet = zip.GetEntry(sheetPath)
                ?? throw new InvalidDataException("The workbook has no worksheet.");

            using var sheetStream = sheet.Open();

            var rows = new List<List<string>>();

            foreach (var row in XDocument.Load(sheetStream).Descendants(main + "row"))
            {
                var cells = new List<string>();

                foreach (var c in row.Elements(main + "c"))
                {
                    var column = ColumnIndex(c.Attribute("r")?.Value);

                    while (column >= 0 && cells.Count < column)
                        cells.Add(string.Empty);

                    var type = c.Attribute("t")?.Value;
                    var value = c.Element(main + "v")?.Value ?? string.Empty;

                    var text = type switch
                    {
                        "s" when int.TryParse(value, out var index) && index < shared.Count => shared[index],
                        "inlineStr" => string.Concat(c.Descendants(main + "t").Select(t => t.Value)),
                        _ => value
                    };

                    cells.Add(text);
                }

                rows.Add(cells);
            }

            return rows;
        }

        private static int ColumnIndex(string? cellReference)
        {
            if (string.IsNullOrEmpty(cellReference))
                return -1;

            var index = 0;

            foreach (var c in cellReference.TakeWhile(char.IsLetter))
                index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);

            return index - 1;
        }
    }
}

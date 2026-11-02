using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace HrPortal.Services.Sanctions
{
    public sealed record ClientRow(string Name, string? IdNumber, string? Reference);

    /// <summary>
    /// Reads the client list the compliance officer uploads, from a
    /// CSV file or the first sheet of an Excel workbook. The columns
    /// are found by their headings: one with "name" in it, one with
    /// "ID", "passport" or "registration", and one with "reference",
    /// "code" or "account". Without recognisable headings the first
    /// column is taken as the name.
    /// </summary>
    public static class ClientListReader
    {
        public static List<ClientRow> Read(Stream file, string fileName)
        {
            var rows = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".xlsx" => ReadXlsx(file),
                ".csv" or ".txt" => ReadCsv(file),
                _ => throw new InvalidDataException(
                    "Upload the client list as an Excel workbook (.xlsx) or a CSV file.")
            };

            return ToClients(rows);
        }

        private static List<ClientRow> ToClients(List<List<string>> rows)
        {
            rows = rows.Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();

            if (rows.Count == 0)
                return new List<ClientRow>();

            var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();

            var name = header.FindIndex(h => h.Contains("name"));
            var id = header.FindIndex(h =>
                h.Contains("passport") || h.Contains("registration") ||
                h == "id" || h.Contains("id no") || h.Contains("id number") ||
                h.Contains("national id") || h.Contains("identity"));
            var reference = header.FindIndex(h =>
                h.Contains("ref") || h.Contains("code") || h.Contains("account"));

            var hasHeader = name >= 0;

            if (!hasHeader)
                name = 0;

            string? Cell(List<string> row, int index) =>
                index >= 0 && index < row.Count && !string.IsNullOrWhiteSpace(row[index])
                    ? row[index].Trim()
                    : null;

            return rows
                .Skip(hasHeader ? 1 : 0)
                .Select(r => new ClientRow(
                    Cell(r, name) ?? string.Empty,
                    Cell(r, id),
                    Cell(r, reference)))
                .Where(c => c.Name.Length > 0)
                .ToList();
        }

        private static List<List<string>> ReadCsv(Stream file)
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

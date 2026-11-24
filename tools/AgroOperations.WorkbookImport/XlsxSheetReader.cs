using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace AgroOperations.WorkbookImport;

public sealed class WorkbookFormatException(string message) : Exception(message);

/// <summary>
/// Reads the cell text of one worksheet from an .xlsx/.xlsm package using only its XML parts
/// (workbook, workbook relationships, shared strings, the sheet). Macro parts are never opened.
/// </summary>
public static class XlsxSheetReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>Returns row number -> 0-based column -> cell text. Error cells and empty cells are absent.</summary>
    public static SortedDictionary<int, Dictionary<int, string>> Read(string path, string sheetName)
    {
        using var zip = ZipFile.OpenRead(path);
        var workbook = Load(zip, "xl/workbook.xml") ?? throw new WorkbookFormatException("Workbook part xl/workbook.xml not found");
        var sheet = workbook.Descendants(Main + "sheet")
            .FirstOrDefault(s => string.Equals(((string?)s.Attribute("name"))?.Trim(), sheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new WorkbookFormatException($"Sheet {sheetName} not found");
        var relationId = (string?)sheet.Attribute(Relationship + "id") ?? throw new WorkbookFormatException($"Sheet {sheetName} has no relationship");
        var relations = Load(zip, "xl/_rels/workbook.xml.rels") ?? throw new WorkbookFormatException("Workbook relationships not found");
        var target = relations.Descendants(PackageRelationships + "Relationship")
            .FirstOrDefault(r => (string?)r.Attribute("Id") == relationId)?.Attribute("Target")?.Value
            ?? throw new WorkbookFormatException($"Sheet {sheetName} relationship not found");
        var sheetXml = Load(zip, target.StartsWith('/') ? Normalize(target) : Normalize("xl/" + target))
            ?? throw new WorkbookFormatException($"Sheet {sheetName} part not found");
        var shared = SharedStrings(zip);

        var rows = new SortedDictionary<int, Dictionary<int, string>>();
        var rowNumber = 0;
        foreach (var row in sheetXml.Descendants(Main + "row"))
        {
            rowNumber = int.TryParse((string?)row.Attribute("r"), out var explicitRow) ? explicitRow : rowNumber + 1;
            var cells = new Dictionary<int, string>();
            var column = -1;
            foreach (var cell in row.Elements(Main + "c"))
            {
                column = (string?)cell.Attribute("r") is { } reference ? ColumnIndex(reference) : column + 1;
                if (Text(cell, shared) is { Length: > 0 } text) cells[column] = text;
            }
            if (cells.Count > 0) rows[rowNumber] = cells;
        }
        return rows;
    }

    private static string? Text(XElement cell, IReadOnlyList<string> shared)
    {
        var value = cell.Element(Main + "v")?.Value;
        switch ((string?)cell.Attribute("t"))
        {
            case "s":
                return int.TryParse(value, out var index) && index >= 0 && index < shared.Count
                    ? shared[index] : throw new WorkbookFormatException("Shared string index out of range");
            case "inlineStr":
                return cell.Element(Main + "is") is { } inline ? RichText(inline) : null;
            case "e":
                return null;
            default:
                return value;
        }
    }

    // Phonetic runs (rPh) are reading hints, not cell text.
    private static string RichText(XElement item) =>
        string.Concat(item.Descendants(Main + "t").Where(t => t.Parent?.Name != Main + "rPh").Select(t => t.Value));

    private static List<string> SharedStrings(ZipArchive zip) =>
        Load(zip, "xl/sharedStrings.xml")?.Root?.Elements(Main + "si").Select(RichText).ToList() ?? [];

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var letter in reference.TakeWhile(char.IsAsciiLetter)) index = index * 26 + (char.ToUpperInvariant(letter) - 'A' + 1);
        return index > 0 ? index - 1 : throw new WorkbookFormatException("Cell reference without column");
    }

    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else if (part != ".") parts.Add(part);
        }
        return string.Join('/', parts);
    }

    private static XDocument? Load(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }
}

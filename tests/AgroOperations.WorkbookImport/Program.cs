using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;
using AgroOperations.Api.Domain;
using AgroOperations.Api.Persistence;
using AgroOperations.WorkbookImport;
using Npgsql;

// Every record value here is invented; the importer runs against a synthetic workbook built below.
var baseConnection = Environment.GetEnvironmentVariable("AGRO_TEST_DATABASE");
if (string.IsNullOrWhiteSpace(baseConnection))
{
    Console.Error.WriteLine("AGRO_TEST_DATABASE is required for isolated PostgreSQL integration tests");
    return 2;
}

var root = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(root, "AgroOperations.slnx"))) root = Path.GetDirectoryName(root) ?? throw new Exception("Repository root not found");
var migrations = Path.Combine(root, "src", "AgroOperations.Api", "Persistence", "Migrations");
var schema = "test_" + Guid.NewGuid().ToString("N");
var scoped = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schema }.ConnectionString;
var temp = Directory.CreateTempSubdirectory("agro-workbook-import-");
using var admin = new NpgsqlConnection(baseConnection);
admin.Open();
using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) create.ExecuteNonQuery();
try
{
    using (var connection = new NpgsqlConnection(scoped))
    {
        connection.Open();
        foreach (var file in new[] { "001_operations.sql", "003_crops.sql", "004_agronomy.sql", "005_import_records.sql",
                     "006_catalogs.sql", "007_attributes.sql", "008_work_orders.sql" })
        {
            using var command = new NpgsqlCommand(File.ReadAllText(Path.Combine(migrations, file)), connection);
            command.ExecuteNonQuery();
        }
    }
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
    }

    var contractors = new ContractorCatalog(new PostgresContractorRepository(scoped));
    var inputs = new InputCatalog(new PostgresInputRepository(scoped));
    var provenance = new ImportProvenanceService(new PostgresImportRecordRepository(scoped));
    contractors.Create(new ContractorDraft("Contratista Preexistente", null, null, null, null, false));
    inputs.Create(new InputDraft("insumo ñandú existente", null, null, null, null, null, "l", null, null, null, false));

    // Sheet layout: two blocks share the header "INSUMOS "; the unit block sits next to UNIDADES and the dose
    // block next to DOSIS MIN / DOSIS MAX / COSTO/LT. Columns are located by header text, never by index.
    const int Labor = 0, NameA = 1, Units = 2, Contractor = 5, NameB = 7, DoseMin = 8, DoseMax = 9, Cost = 10;
    var cells = new Dictionary<(int Row, int Col), Cell>
    {
        [(1, Labor)] = new("LABOR"), [(1, NameA)] = new("INSUMOS "), [(1, Units)] = new("UNIDADES"), [(1, 4)] = new("MOMENTO"),
        [(1, Contractor)] = new("CONTRATISTA"), [(1, 6)] = new("OTRO"), [(1, NameB)] = new("INSUMOS "),
        [(1, DoseMin)] = new("DOSIS MIN"), [(1, DoseMax)] = new("DOSIS MAX"), [(1, Cost)] = new("COSTO/LT"),
        [(2, Labor)] = new("Labor de prueba"),
        [(2, Contractor)] = new("PROPIO"),
        [(3, Contractor)] = new("Contratista Ñu Uno", Kind.RichShared),
        [(4, Contractor)] = new("Contratista Prueba Dos"),
        [(5, Contractor)] = new("CONTRATISTA PREEXISTENTE"),
        [(6, Contractor)] = new(" contratista prueba dos "),
        [(8, Contractor)] = new("Contratista Prueba Tres", Kind.Inline),
    };
    string[] units = ["lt", "Lt", "kg", "Kg", "KG", "cm3", "Cm3", "cc", "gr", "Gr", "gr/ha", "cc/qq", "bolsa", "Bolsas", "BOLSAS",
        "pack", "Pack", "Unidad", "Cajas", " m3 "];
    string[] normalized = ["l", "l", "kg", "kg", "kg", "cm3", "cm3", "cm3", "g", "g", "g/ha", "cm3/qq", "bolsa", "bolsa", "bolsa",
        "pack", "pack", "unidad", "caja", "m3"];
    string[] suffixes = [" (7 SL)", " (3 SC)", " (25 EC)", " (SL)"];
    var inputNames = Enumerable.Range(0, units.Length).Select(i => $"Insumo prueba {i:00}" + (i < suffixes.Length ? suffixes[i] : "")).ToArray();
    for (var i = 0; i < units.Length; i++)
    {
        cells[(i + 2, NameA)] = new(inputNames[i], i % 2 == 0 ? Kind.Shared : Kind.Inline);
        cells[(i + 2, Units)] = new(units[i]);
    }
    cells[(22, NameA)] = new("Insumo Ñandú existente");
    cells[(22, Units)] = new("lt");
    cells[(23, NameA)] = new("Insumo prueba sin unidad");
    cells[(24, NameA)] = new("INSUMO PRUEBA 00 (7 SL)");
    cells[(24, Units)] = new("lt");
    void DoseRow(int row, string name, Cell? min, Cell? max, Cell? cost)
    {
        cells[(row, NameB)] = new(name);
        if (min is not null) cells[(row, DoseMin)] = min;
        if (max is not null) cells[(row, DoseMax)] = max;
        if (cost is not null) cells[(row, Cost)] = cost;
    }
    DoseRow(2, "INSUMO PRUEBA 03 (SL)", Cell.Number("1"), Cell.Number("1"), new("abc"));
    DoseRow(3, "  insumo prueba 00 (7 sl)  ", Cell.Number("0.30000000000000004"), Cell.Number("1.5"), Cell.Number("12.25"));
    DoseRow(4, "Insumo prueba 01 (3 SC)", Cell.Number("2"), Cell.Number("1"), Cell.Number("5"));
    DoseRow(5, "Insumo prueba 02 (25 EC)", new("n/d"), Cell.Number("3"), null);
    DoseRow(6, "Insumo inexistente prueba", Cell.Number("1"), Cell.Number("2"), null);
    DoseRow(7, "Insumo prueba 04", null, null, Cell.Number("7"));

    var workbook = Path.Combine(temp.FullName, "synthetic.xlsm");
    WriteWorkbook(workbook, cells, withTargetSheet: true);
    var withoutSheet = Path.Combine(temp.FullName, "without-sheet.xlsx");
    WriteWorkbook(withoutSheet, cells, withTargetSheet: false);

    var transcript = new StringBuilder();
    (int Code, string Out, string Err) Import(string? connection, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = WorkbookImporter.Run(args, connection, output, error);
        transcript.Append(output).Append(error);
        return (code, output.ToString(), error.ToString());
    }
    string State()
    {
        var contractorRows = string.Join(";", contractors.List().Select(c => $"{c.Id}:{c.Name}:{c.Revision}"));
        var inputRows = string.Join(";", inputs.List().Select(i => $"{i.Id}:{i.CommercialName}:{i.Revision}"));
        return $"{contractorRows}|{inputRows}|{provenance.List().Count}";
    }
    var initial = State();

    var usage = Import(scoped);
    Check(usage.Code == 2 && usage.Err.Contains("--workbook <path>"), "missing --workbook is a usage error");
    var unknown = Import(scoped, "--workbook", workbook, "--force");
    Check(unknown.Code == 2 && unknown.Err.Contains("Unknown argument"), "unknown argument is a usage error");
    var missingFile = Import(scoped, "--workbook", Path.Combine(temp.FullName, "absent.xlsx"));
    Check(missingFile.Code == 2 && missingFile.Err.Contains("Workbook file not found"), "missing workbook file");
    var missingSheet = Import(scoped, "--workbook", withoutSheet);
    Check(missingSheet.Code == 2 && missingSheet.Err.Contains("Sheet ACTIV-INSUMOS-MOMENTOS not found"), "missing sheet");
    var remote = Import(new NpgsqlConnectionStringBuilder(scoped) { Host = "192.0.2.10" }.ConnectionString, "--workbook", workbook);
    Check(remote.Code == 2 && remote.Err.Contains("loopback"), "non-loopback database refused");
    var noDatabase = Import(null, "--workbook", workbook);
    Check(noDatabase.Code == 2 && noDatabase.Err.Contains("AGRO_LOCAL_DATABASE is required"), "write mode requires a database");
    Check(State() == initial, "rejected invocations wrote nothing");

    var parseOnly = Import(null, "--workbook", workbook, "--dry-run");
    Check(parseOnly.Code == 0, "dry run without database: " + parseOnly.Err);
    Check(parseOnly.Out.Contains("Contractors: to create 5, existing not checked, workbook duplicates skipped 1, rejected 0."), "parse-only contractor counts");
    Check(parseOnly.Out.Contains("Inputs: to create 21, existing not checked, workbook duplicates skipped 1, rejected 1, doses skipped 2, without dose data 17, unmatched dose rows 1, costs skipped 1."),
        "parse-only input counts");

    var dryRun = Import(scoped, "--workbook", workbook, "--dry-run");
    Check(dryRun.Code == 0, "dry run: " + dryRun.Err);
    Check(dryRun.Out.Contains("Contractors: to create 3, existing skipped 2, workbook duplicates skipped 1, rejected 0."), "dry-run contractor counts");
    Check(dryRun.Out.Contains("Inputs: to create 20, existing skipped 1, workbook duplicates skipped 1, rejected 1, doses skipped 2, without dose data 16, unmatched dose rows 1, costs skipped 1."),
        "dry-run input counts");
    Check(dryRun.Out.Contains("Row 23: input rejected (Unit is required)."), "dry run names the rejected row number");
    Check(dryRun.Out.Contains("Dry run: nothing was written."), "dry run says nothing was written");
    Check(State() == initial, "dry run wrote nothing");

    var first = Import(scoped, "--workbook", workbook);
    Check(first.Code == 0, "import: " + first.Err);
    Check(first.Out.Contains("Contractors: created 3, existing skipped 2, workbook duplicates skipped 1, rejected 0."), "import contractor counts");
    Check(first.Out.Contains("Inputs: created 20, existing skipped 1, workbook duplicates skipped 1, rejected 1, doses skipped 2, without dose data 16, unmatched dose rows 1, costs skipped 1."),
        "import input counts");
    Check(!first.Out.Contains("Dry run"), "a real import is not reported as a dry run");

    var contractorList = contractors.List();
    Check(contractorList.Count == 5, "seed, pre-existing and three imported contractors");
    Check(contractorList.Count(c => c.Name.Equals("propio", StringComparison.OrdinalIgnoreCase)) == 1, "PROPIO matched the seeded Propio");
    Check(contractorList.Count(c => c.Name.Equals("contratista preexistente", StringComparison.OrdinalIgnoreCase)) == 1, "existing contractor not duplicated");
    foreach (var name in new[] { "Contratista Ñu Uno", "Contratista Prueba Dos", "Contratista Prueba Tres" })
        Check(contractorList.Any(c => c is { OwnEquipment: false, TaxId: null } && c.Name == name), "imported contractor " + name);
    Check(!contractorList.Any(c => c.Name == "Contratista señuelo"), "other sheets are not imported");

    var inputList = inputs.List();
    Check(inputList.Count == 21, "pre-existing plus twenty imported inputs");
    Check(!inputList.Any(i => i.CommercialName == "Insumo prueba sin unidad"), "row without unit rejected");
    for (var i = 0; i < units.Length; i++)
    {
        var input = inputList.Single(p => p.CommercialName == inputNames[i]);
        Check(input.Unit == normalized[i], $"unit {i} normalized");
        Check(input is { ActiveIngredient: null, Concentration: null, Category: null, HollowCone: false }, $"input {i} leaves unverifiable fields empty");
    }
    InputProduct Input(int index) => inputList.Single(p => p.CommercialName == inputNames[index]);
    Check(Input(0) is { MixOrder: 7, Formulation: "SL", DoseMin: 0.3m, DoseMax: 1.5m, ReferenceCost: 12.25m }, "mix order, formulation, doses and cost");
    Check(Input(1) is { MixOrder: 3, Formulation: "SC", DoseMin: null, DoseMax: null, ReferenceCost: 5m }, "min > max dose pair skipped, cost kept");
    Check(Input(2) is { MixOrder: null, Formulation: null, DoseMin: null, DoseMax: null, ReferenceCost: null }, "out-of-range mix order and non-numeric dose skipped");
    Check(Input(3) is { MixOrder: null, Formulation: null, DoseMin: 1m, DoseMax: 1m, ReferenceCost: null }, "no mix order without number, non-numeric cost skipped");
    Check(Input(4) is { DoseMin: null, DoseMax: null, ReferenceCost: 7m }, "blank dose pair with cost");
    Check(Input(5) is { MixOrder: null, Formulation: null, DoseMin: null, DoseMax: null, ReferenceCost: null }, "input without dose row");

    var records = provenance.List();
    Check(records.Count == 23, "one import record per created record");
    Check(records.All(r => !r.Verified), "imported records are unverified");
    var contractorIds = contractorList.ToDictionary(c => c.Id, c => c.Name);
    var inputIds = inputList.ToDictionary(i => i.Id, i => i.CommercialName);
    Check(records.Count(r => r.EntityType == "contractor" && contractorIds.ContainsKey(r.EntityId)) == 3, "contractor provenance");
    Check(records.Count(r => r.EntityType == "input" && inputIds.ContainsKey(r.EntityId)) == 20, "input provenance");
    ImportRecord RecordOf(Guid id) => records.Single(r => r.EntityId == id);
    JsonElement Payload(ImportRecord record) => JsonDocument.Parse(record.RawPayloadJson).RootElement;
    var nu = RecordOf(contractorList.Single(c => c.Name == "Contratista Ñu Uno").Id);
    Check(nu.SourceReference == "ACTIV-INSUMOS-MOMENTOS!3", "contractor row reference");
    Check(Payload(nu).GetProperty("CONTRATISTA").GetString() == "Contratista Ñu Uno", "contractor raw payload keeps the non-ASCII cell");
    Check(RecordOf(contractorList.Single(c => c.Name == "Contratista Prueba Tres").Id).SourceReference == "ACTIV-INSUMOS-MOMENTOS!8", "row after a blank row");
    var zero = RecordOf(Input(0).Id);
    Check(zero.SourceReference == "ACTIV-INSUMOS-MOMENTOS!2", "input row reference");
    var zeroPayload = Payload(zero);
    Check(zeroPayload.GetProperty("INSUMOS").GetString() == inputNames[0] && zeroPayload.GetProperty("UNIDADES").GetString() == "lt",
        "input raw payload keeps the unit block cells");
    Check(zeroPayload.GetProperty("dose_source_reference").GetString() == "ACTIV-INSUMOS-MOMENTOS!3"
          && zeroPayload.GetProperty("DOSIS MIN").GetString() == "0.30000000000000004"
          && zeroPayload.GetProperty("DOSIS MAX").GetString() == "1.5" && zeroPayload.GetProperty("COSTO/LT").GetString() == "12.25",
        "input raw payload keeps the dose block cells");
    Check(!Payload(RecordOf(Input(5).Id)).TryGetProperty("dose_source_reference", out _), "no dose block reference without a dose row");

    var afterImport = State();
    var rerun = Import(scoped, "--workbook", workbook);
    Check(rerun.Code == 0, "rerun: " + rerun.Err);
    Check(rerun.Out.Contains("Contractors: created 0, existing skipped 5, workbook duplicates skipped 1, rejected 0."), "rerun contractor counts");
    Check(rerun.Out.Contains("Inputs: created 0, existing skipped 21, workbook duplicates skipped 1, rejected 1, doses skipped 0, without dose data 0, unmatched dose rows 1, costs skipped 0."),
        "rerun input counts");
    Check(State() == afterImport, "rerun created nothing");

    var allOutput = transcript.ToString();
    Check(!allOutput.Contains("contratista", StringComparison.OrdinalIgnoreCase) && !allOutput.Contains("insumo ", StringComparison.OrdinalIgnoreCase)
          && !allOutput.Contains("propio", StringComparison.OrdinalIgnoreCase) && !allOutput.Contains("labor de", StringComparison.OrdinalIgnoreCase),
        "console output contains no record names");
    Check(!allOutput.Contains(temp.FullName, StringComparison.OrdinalIgnoreCase), "console output does not echo the workbook path");

    Console.WriteLine("PASS: workbook import of contractors and inputs with header lookup, unit normalization, mix order, dose checks, idempotent provenance and dry run");
    return 0;
}
catch (Exception failure)
{
    // Caught so the schema and temp directory below are always removed.
    Console.Error.WriteLine("FAIL: " + failure.Message);
    return 1;
}
finally
{
    using (var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin)) drop.ExecuteNonQuery();
    temp.Delete(recursive: true);
}

static void WriteWorkbook(string path, Dictionary<(int Row, int Col), Cell> cells, bool withTargetSheet)
{
    const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    var shared = new List<string>();
    string SheetXml(Dictionary<(int Row, int Col), Cell> sheetCells)
    {
        var xml = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"{Main}\"><sheetData>");
        foreach (var row in sheetCells.GroupBy(c => c.Key.Row).OrderBy(g => g.Key))
        {
            xml.Append($"<row r=\"{row.Key}\">");
            foreach (var (key, cell) in row.OrderBy(c => c.Key.Col))
            {
                var reference = ColumnName(key.Col) + key.Row;
                var text = SecurityElement.Escape(cell.Text);
                switch (cell.Kind)
                {
                    case Kind.Number:
                        xml.Append($"<c r=\"{reference}\"><v>{text}</v></c>");
                        break;
                    case Kind.Inline:
                        xml.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{text}</t></is></c>");
                        break;
                    case Kind.RichShared:
                        var split = cell.Text.Length / 2;
                        shared.Add($"<si><r><t xml:space=\"preserve\">{SecurityElement.Escape(cell.Text[..split])}</t></r><r><rPr><b/></rPr><t xml:space=\"preserve\">{SecurityElement.Escape(cell.Text[split..])}</t></r><rPh sb=\"0\" eb=\"1\"><t>X</t></rPh></si>");
                        xml.Append($"<c r=\"{reference}\" t=\"s\"><v>{shared.Count - 1}</v></c>");
                        break;
                    default:
                        shared.Add($"<si><t xml:space=\"preserve\">{text}</t></si>");
                        xml.Append($"<c r=\"{reference}\" t=\"s\"><v>{shared.Count - 1}</v></c>");
                        break;
                }
            }
            xml.Append("</row>");
        }
        return xml.Append("</sheetData></worksheet>").ToString();
    }
    var decoy = SheetXml(new() { [(1, 0)] = new("CONTRATISTA"), [(2, 0)] = new("Contratista señuelo") });
    var target = withTargetSheet ? SheetXml(cells) : null;
    using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
    void Entry(string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
    Entry("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
    Entry("xl/workbook.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"{Main}\" xmlns:r=\"{Rel}\"><sheets>"
        + "<sheet name=\"Orden\" sheetId=\"1\" r:id=\"rId1\"/>"
        + (withTargetSheet ? "<sheet name=\"ACTIV-INSUMOS-MOMENTOS\" sheetId=\"2\" r:id=\"rId2\"/>" : "")
        + "</sheets></workbook>");
    Entry("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + $"<Relationship Id=\"rId1\" Type=\"{Rel}/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
        + $"<Relationship Id=\"rId2\" Type=\"{Rel}/worksheet\" Target=\"/xl/worksheets/sheet7.xml\"/>"
        + $"<Relationship Id=\"rId3\" Type=\"{Rel}/sharedStrings\" Target=\"sharedStrings.xml\"/></Relationships>");
    Entry("xl/worksheets/sheet1.xml", decoy);
    if (target is not null) Entry("xl/worksheets/sheet7.xml", target);
    Entry("xl/sharedStrings.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><sst xmlns=\"{Main}\" count=\"{shared.Count}\" uniqueCount=\"{shared.Count}\">{string.Concat(shared)}</sst>");
    // A macro part is present but must never be read or run.
    using (var macro = zip.CreateEntry("xl/vbaProject.bin").Open()) macro.Write("not a real VBA project"u8);
}

static string ColumnName(int index)
{
    var name = "";
    for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name;
    return name;
}

enum Kind { Shared, Inline, RichShared, Number }

sealed record Cell(string Text, Kind Kind = Kind.Shared)
{
    public static Cell Number(string text) => new(text, Kind.Number);
}

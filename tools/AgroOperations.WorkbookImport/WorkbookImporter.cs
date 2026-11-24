using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using AgroOperations.Api.Domain;
using AgroOperations.Api.Persistence;
using Npgsql;

namespace AgroOperations.WorkbookImport;

/// <summary>
/// Imports contractors and inputs from the owner's workbook into the private local database, marking each created
/// record as imported and unverified. Console output carries counts and row numbers only, never record values.
/// </summary>
public static partial class WorkbookImporter
{
    public const string SheetName = "ACTIV-INSUMOS-MOMENTOS";
    private const string Usage = "Usage: AgroOperations.WorkbookImport --workbook <path> [--dry-run]";

    private sealed record ContractorRow(int Row, ContractorDraft Draft, string Payload);
    private sealed record InputRow(int Row, InputDraft Draft, string Payload);
    private sealed record DoseRow(int Row, string? Min, string? Max, string? Cost);

    private sealed class Counts
    {
        public int Planned, Existing, Duplicates, Rejected, DosesSkipped, WithoutDoses, UnmatchedDoses, CostsSkipped;
    }

    public static int Run(string[] args, string? connectionString, TextWriter output, TextWriter error)
    {
        string? workbook = null;
        var dryRun = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dry-run") dryRun = true;
            else if (args[i] == "--workbook" && i + 1 < args.Length) workbook = args[++i];
            else return Fail(error, 2, $"Unknown argument at position {i + 1}. {Usage}");
        }
        if (string.IsNullOrWhiteSpace(workbook)) return Fail(error, 2, Usage);
        if (!File.Exists(workbook)) return Fail(error, 2, "Workbook file not found.");
        var hasDatabase = !string.IsNullOrWhiteSpace(connectionString);
        if (!hasDatabase && !dryRun) return Fail(error, 2, "AGRO_LOCAL_DATABASE is required to import.");
        if (hasDatabase)
        {
            string? host;
            try { host = new NpgsqlConnectionStringBuilder(connectionString).Host; }
            catch (ArgumentException) { return Fail(error, 2, "AGRO_LOCAL_DATABASE is not a valid connection string."); }
            if (host is not ("127.0.0.1" or "localhost")) return Fail(error, 2, "The workbook import requires a loopback-only PostgreSQL host.");
        }

        SortedDictionary<int, Dictionary<int, string>> sheet;
        try { sheet = XlsxSheetReader.Read(workbook, SheetName); }
        catch (Exception e) when (e is WorkbookFormatException or InvalidDataException or IOException or XmlException)
        {
            return Fail(error, 2, e is WorkbookFormatException ? e.Message : "Workbook could not be read as an .xlsx/.xlsm package.");
        }

        try
        {
            return Import(sheet, hasDatabase ? connectionString! : null, dryRun, output, error);
        }
        catch (WorkbookFormatException e) { return Fail(error, 2, e.Message); }
        catch (PostgresException e) { return Fail(error, 1, $"Database error (SQLSTATE {e.SqlState}); the import stopped."); }
        catch (NpgsqlException) { return Fail(error, 1, "Cannot reach the local database; the import stopped."); }
    }

    private static int Import(SortedDictionary<int, Dictionary<int, string>> sheet, string? connectionString, bool dryRun,
        TextWriter output, TextWriter error)
    {
        var header = sheet.TryGetValue(1, out var first) ? first : throw new WorkbookFormatException("Header row 1 is empty");
        var contractorColumn = Single(header, "CONTRATISTA");
        var unitColumn = Single(header, "UNIDADES");
        var nameColumns = Columns(header, "INSUMOS");
        if (nameColumns.Count != 2) throw new WorkbookFormatException($"Expected two INSUMOS columns in row 1, found {nameColumns.Count}");
        var unitBlock = nameColumns.Where(c => Math.Abs(c - unitColumn) == 1).ToList();
        if (unitBlock.Count != 1) throw new WorkbookFormatException("Expected exactly one INSUMOS column next to UNIDADES");
        var nameColumn = unitBlock[0];
        var doseNameColumn = nameColumns.Single(c => c != nameColumn);
        var minColumn = Single(header, "DOSIS MIN");
        var maxColumn = Single(header, "DOSIS MAX");
        var costColumn = Single(header, "COSTO/LT");
        string Key(int column) => header[column].Trim();
        string? Cell(Dictionary<int, string> row, int column) => row.TryGetValue(column, out var value) ? value : null;
        string Reference(int row) => $"{SheetName}!{row}";

        IReadOnlyList<Contractor> existingContractors = [];
        IReadOnlyList<InputProduct> existingInputs = [];
        if (connectionString is not null)
        {
            existingContractors = new ContractorCatalog(new PostgresContractorRepository(connectionString)).List();
            existingInputs = new InputCatalog(new PostgresInputRepository(connectionString)).List();
        }
        var rejections = new List<string>();

        // Contractors: one per non-empty CONTRATISTA cell; the first spelling of a name wins.
        var contractorCounts = new Counts();
        var contractorPlan = new List<ContractorRow>();
        var contractorNames = new HashSet<string>(existingContractors.Select(c => c.Name.Trim()), StringComparer.OrdinalIgnoreCase);
        var seenContractors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validateContractor = new ContractorCatalog(new ValidationOnlyContractors());
        foreach (var (row, cells) in sheet.Where(r => r.Key > 1))
        {
            if (Cell(cells, contractorColumn) is not { } raw || string.IsNullOrWhiteSpace(raw)) continue;
            var name = raw.Trim();
            if (!seenContractors.Add(name)) { contractorCounts.Duplicates++; continue; }
            if (contractorNames.Contains(name)) { contractorCounts.Existing++; continue; }
            try
            {
                var draft = new ContractorDraft(name, null, null, null, null, false);
                validateContractor.Create(draft);
                contractorPlan.Add(new(row, draft, Payload(new() { [Key(contractorColumn)] = raw })));
            }
            catch (DomainValidationException e) { contractorCounts.Rejected++; rejections.Add($"Row {row}: contractor rejected ({e.Message})."); }
        }
        contractorCounts.Planned = contractorPlan.Count;

        // Dose ranges and reference costs come from the second INSUMOS block, joined by exact trimmed name.
        var doses = new Dictionary<string, DoseRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var (row, cells) in sheet.Where(r => r.Key > 1))
            if (Cell(cells, doseNameColumn) is { } doseName && !string.IsNullOrWhiteSpace(doseName))
                doses.TryAdd(doseName.Trim(), new(row, Cell(cells, minColumn), Cell(cells, maxColumn), Cell(cells, costColumn)));

        var inputCounts = new Counts();
        var inputPlan = new List<InputRow>();
        var inputNames = new HashSet<string>(existingInputs.Select(i => i.CommercialName.Trim()), StringComparer.OrdinalIgnoreCase);
        var seenInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validateInput = new InputCatalog(new ValidationOnlyInputs());
        foreach (var (row, cells) in sheet.Where(r => r.Key > 1))
        {
            if (Cell(cells, nameColumn) is not { } raw || string.IsNullOrWhiteSpace(raw)) continue;
            var name = raw.Trim();
            if (!seenInputs.Add(name)) { inputCounts.Duplicates++; continue; }
            if (inputNames.Contains(name)) { inputCounts.Existing++; continue; }
            var rawUnit = Cell(cells, unitColumn);
            var payload = new Dictionary<string, string?> { [Key(nameColumn)] = raw, [Key(unitColumn)] = rawUnit };
            decimal? doseMin = null, doseMax = null, cost = null;
            bool dosesSkipped = false, costSkipped = false;
            if (doses.TryGetValue(name, out var dose))
            {
                payload["dose_source_reference"] = Reference(dose.Row);
                payload[Key(minColumn)] = dose.Min;
                payload[Key(maxColumn)] = dose.Max;
                payload[Key(costColumn)] = dose.Cost;
                if (!string.IsNullOrWhiteSpace(dose.Min) || !string.IsNullOrWhiteSpace(dose.Max))
                {
                    if (Number(dose.Min) is > 0m and var min && Number(dose.Max) is > 0m and var max && min <= max) (doseMin, doseMax) = (min, max);
                    else dosesSkipped = true;
                }
                if (!string.IsNullOrWhiteSpace(dose.Cost))
                {
                    if (Number(dose.Cost) is >= 0m and var value) cost = value;
                    else costSkipped = true;
                }
            }
            var (mixOrder, formulation) = MixOrder(name);
            var draft = new InputDraft(name, null, null, formulation, mixOrder, null, Unit(rawUnit), doseMin, doseMax, cost, false);
            try { validateInput.Create(draft); }
            catch (DomainValidationException e) { inputCounts.Rejected++; rejections.Add($"Row {row}: input rejected ({e.Message})."); continue; }
            inputPlan.Add(new(row, draft, Payload(payload)));
            if (dosesSkipped) inputCounts.DosesSkipped++;
            else if (doseMin is null) inputCounts.WithoutDoses++;
            if (costSkipped) inputCounts.CostsSkipped++;
        }
        inputCounts.Planned = inputPlan.Count;
        var inputKeys = new HashSet<string>(sheet.Where(r => r.Key > 1).Select(r => Cell(r.Value, nameColumn)?.Trim())
            .OfType<string>().Where(n => n.Length > 0), StringComparer.OrdinalIgnoreCase);
        inputCounts.UnmatchedDoses = doses.Keys.Count(k => !inputKeys.Contains(k));

        if (!dryRun)
        {
            var contractors = new ContractorCatalog(new PostgresContractorRepository(connectionString!));
            var inputs = new InputCatalog(new PostgresInputRepository(connectionString!));
            var provenance = new ImportProvenanceService(new PostgresImportRecordRepository(connectionString!));
            // The repositories open their own connections, so a record and its provenance cannot share a transaction:
            // the record is created first and a provenance failure stops the import with an error.
            foreach (var item in contractorPlan)
            {
                Contractor created;
                try { created = contractors.Create(item.Draft); }
                catch (DomainDuplicateException) { contractorCounts.Planned--; contractorCounts.Existing++; continue; }
                if (!Recorded(provenance, "contractor", created.Id, Reference(item.Row), item.Payload))
                    return Fail(error, 1, $"Row {item.Row}: contractor created but its import record failed; add its provenance before rerunning.");
            }
            foreach (var item in inputPlan)
            {
                InputProduct created;
                try { created = inputs.Create(item.Draft); }
                catch (DomainDuplicateException) { inputCounts.Planned--; inputCounts.Existing++; continue; }
                if (!Recorded(provenance, "input", created.Id, Reference(item.Row), item.Payload))
                    return Fail(error, 1, $"Row {item.Row}: input created but its import record failed; add its provenance before rerunning.");
            }
        }

        var verb = dryRun ? "to create" : "created";
        var existing = (Counts c) => connectionString is null ? "existing not checked" : $"existing skipped {c.Existing}";
        output.WriteLine($"Sheet {SheetName}: {contractorPlan.Count + contractorCounts.Existing + contractorCounts.Rejected + contractorCounts.Duplicates} contractor rows, "
            + $"{inputPlan.Count + inputCounts.Existing + inputCounts.Rejected + inputCounts.Duplicates} input rows, {doses.Count} dose rows.");
        output.WriteLine($"Contractors: {verb} {contractorCounts.Planned}, {existing(contractorCounts)}, "
            + $"workbook duplicates skipped {contractorCounts.Duplicates}, rejected {contractorCounts.Rejected}.");
        output.WriteLine($"Inputs: {verb} {inputCounts.Planned}, {existing(inputCounts)}, workbook duplicates skipped {inputCounts.Duplicates}, "
            + $"rejected {inputCounts.Rejected}, doses skipped {inputCounts.DosesSkipped}, without dose data {inputCounts.WithoutDoses}, "
            + $"unmatched dose rows {inputCounts.UnmatchedDoses}, costs skipped {inputCounts.CostsSkipped}.");
        foreach (var rejection in rejections) output.WriteLine(rejection);
        output.WriteLine(dryRun ? "Dry run: nothing was written." : "Imported records are marked unverified for human review.");
        return 0;
    }

    private static bool Recorded(ImportProvenanceService provenance, string entityType, Guid id, string reference, string payload)
    {
        try { provenance.RecordOnce(entityType, id, reference, payload); return true; }
        catch (Exception e) when (e is NpgsqlException or DomainValidationException) { return false; }
    }

    private static int Single(Dictionary<int, string> header, string name) => Columns(header, name) switch
    {
        [var column] => column,
        var found => throw new WorkbookFormatException($"Expected one {name} column in row 1, found {found.Count}"),
    };

    private static List<int> Columns(Dictionary<int, string> header, string name) =>
        header.Where(h => string.Equals(h.Value.Trim(), name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Key).Order().ToList();

    // Workbook numbers are stored as invariant doubles (0.30000000000000004); the catalog keeps 4 decimals.
    private static decimal? Number(string? raw) =>
        decimal.TryParse(raw?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Round(value, 4, MidpointRounding.AwayFromZero) : null;

    // Only the trailing "(<mix order> <FORMULATION>)" is trusted; active ingredient and category stay for a human.
    [GeneratedRegex(@"\((\d{1,2})\s+([A-Za-z]{1,6})\)$")]
    private static partial Regex MixOrderPattern();

    private static (short? MixOrder, string? Formulation) MixOrder(string name)
    {
        var match = MixOrderPattern().Match(name);
        return match.Success && short.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) is >= 1 and <= 20 and var order
            ? (order, match.Groups[2].Value) : (null, null);
    }

    private static readonly Dictionary<string, string> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lt"] = "l", ["kg"] = "kg", ["cm3"] = "cm3", ["cc"] = "cm3", ["gr"] = "g", ["gr/ha"] = "g/ha", ["cc/qq"] = "cm3/qq",
        ["bolsa"] = "bolsa", ["bolsas"] = "bolsa", ["pack"] = "pack", ["unidad"] = "unidad", ["cajas"] = "caja",
    };

    private static string Unit(string? raw) => raw?.Trim() is { } unit ? Units.GetValueOrDefault(unit, unit) : "";

    private static string Payload(Dictionary<string, string?> cells) => JsonSerializer.Serialize(cells);

    private static int Fail(TextWriter error, int code, string message)
    {
        error.WriteLine(message);
        return code;
    }

    // Run the catalogs' own validation without storing anything.
    private sealed class ValidationOnlyContractors : IContractorRepository
    {
        public Contractor Create(ContractorDraft c) => new(Guid.Empty, c.Name, c.TaxId, c.Phone, c.Email, c.Notes, c.OwnEquipment, 1);
        public Contractor Update(Guid id, long revision, ContractorDraft contractor) => throw new NotSupportedException();
        public IReadOnlyList<Contractor> List() => [];
    }

    private sealed class ValidationOnlyInputs : IInputRepository
    {
        public InputProduct Create(InputDraft i) => new(Guid.Empty, i.CommercialName, i.ActiveIngredient, i.Concentration, i.Formulation,
            i.MixOrder, i.Category, i.Unit, i.DoseMin, i.DoseMax, i.ReferenceCost, i.HollowCone, 1);
        public InputProduct Update(Guid id, long revision, InputDraft input) => throw new NotSupportedException();
        public IReadOnlyList<InputProduct> List() => [];
    }
}

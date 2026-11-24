using System.Globalization;
using System.Text;
using AgroOperations.Api.Domain;
using AgroOperations.Api.Persistence;

namespace AgroOperations.WorkbookImport;

/// <summary>
/// Clients, fields and plots from BASEDATOS, and the numbering of their commercial firms from Registros.
/// Existing records are matched by trimmed case-insensitive name within their parent and never duplicated.
/// </summary>
public static partial class WorkbookImporter
{
    public const string PlotSheetName = "BASEDATOS";
    public const string OrderSheetName = "Registros";

    private sealed record PlotColumns(int Code, int Client, int Field, int Plot, int Area, Dictionary<int, string> Header);
    private sealed record OrderColumns(int Number, int Firm);

    private sealed class HierarchyCounts
    {
        public int Rows, Blank, ClientsCreated, ClientsExisting, ClientsRejected, FieldsCreated, FieldsExisting, FieldsRejected,
            PlotsCreated, PlotsExisting, CodesSet, CodesSkipped, PlotsRejected, OrderRows, Firms, Renamed, NumbersSet, Unmatched;
    }

    private static PlotColumns LocatePlotColumns(SortedDictionary<int, Dictionary<int, string>> sheet)
    {
        var header = sheet.TryGetValue(1, out var first) ? first : throw new WorkbookFormatException($"Header row 1 of {PlotSheetName} is empty");
        return new(Single(header, "CODIGO LOTE"), Single(header, "CLIENTE"), Single(header, "ESTABLECIMIENTO"), Single(header, "LOTE"),
            Single(header, "SUP"), header);
    }

    // The order number header is written "Nº ORDEN" or "N° ORDEN"; both signs compare equal.
    private static OrderColumns LocateOrderColumns(SortedDictionary<int, Dictionary<int, string>> sheet)
    {
        var header = sheet.TryGetValue(1, out var first) ? first : throw new WorkbookFormatException($"Header row 1 of {OrderSheetName} is empty");
        int Find(string name, string label) => header.Where(h => HeaderKey(h.Value) == HeaderKey(name)).Select(h => h.Key).ToList() switch
        {
            [var column] => column,
            var found => throw new WorkbookFormatException($"Expected one {label} column in row 1 of {OrderSheetName}, found {found.Count}"),
        };
        return new(Find("N° ORDEN", "N° ORDEN"), Find("FIRMA COMERCIAL", "FIRMA COMERCIAL"));
    }

    private static string HeaderKey(string text) => NameKey(text).Replace('º', '°');

    /// <summary>Trimmed, upper case, without accents, "_" as a space and single spaces: how a firm is matched to its client.</summary>
    internal static string NameKey(string text)
    {
        var decomposed = text.Replace('_', ' ').ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var letters = decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        return string.Join(' ', new string(letters).Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Returns a failure exit code, or null when the sections completed and their lines were added.</summary>
    private static int? ImportHierarchy(SortedDictionary<int, Dictionary<int, string>> plotSheet, PlotColumns columns,
        SortedDictionary<int, Dictionary<int, string>> orderSheet, OrderColumns orderColumns, string? connectionString, bool dryRun,
        List<string> lines, TextWriter error)
    {
        // A dry run replays the same steps on an in-memory copy of the stored records, so its counts match a real run.
        ICommercialFirmRepository firmStore;
        IOperationsRepository operationStore;
        if (dryRun)
        {
            var firmCopy = new InMemoryFirms(connectionString is null ? [] : new PostgresCommercialFirmRepository(connectionString).List(null));
            operationStore = connectionString is null ? new InMemoryOperations(firmCopy) : InMemoryOperations.Copy(new PostgresOperationsRepository(connectionString), firmCopy);
            firmStore = firmCopy;
        }
        else
        {
            operationStore = new PostgresOperationsRepository(connectionString!);
            firmStore = new PostgresCommercialFirmRepository(connectionString!);
        }
        var operations = new OperationsService(operationStore);
        var firms = new CommercialFirmCatalog(firmStore);
        var provenance = dryRun ? null : new ImportProvenanceService(new PostgresImportRecordRepository(connectionString!));
        var validate = new OperationsService(new ValidationOnlyOperations());

        var counts = new HierarchyCounts();
        var rejections = new List<string>();
        var clients = new Dictionary<string, Client>(StringComparer.OrdinalIgnoreCase);
        foreach (var client in operations.ListClients()) clients.TryAdd(client.Name.Trim(), client);
        var fields = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in operations.ListFields()) fields.TryAdd(ChildKey(field.ClientId, field.Name), field);
        var plots = new Dictionary<string, Plot>(StringComparer.OrdinalIgnoreCase);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plot in operations.ListPlots())
        {
            plots.TryAdd(ChildKey(plot.FieldId, plot.Name), plot);
            if (plot.Code is { } code) codes.Add(code);
        }
        var counted = new HashSet<Guid>();
        string? Cell(Dictionary<int, string> row, int column) => row.TryGetValue(column, out var value) ? value : null;

        foreach (var (row, cells) in plotSheet.Where(r => r.Key > 1))
        {
            string? clientName = Cell(cells, columns.Client), fieldName = Cell(cells, columns.Field), plotName = Cell(cells, columns.Plot);
            if (string.IsNullOrWhiteSpace(clientName) && string.IsNullOrWhiteSpace(fieldName) && string.IsNullOrWhiteSpace(plotName))
            {
                counts.Blank++;
                continue;
            }
            counts.Rows++;
            // The whole row is checked first so a rejected row writes nothing.
            ClientDraft clientDraft;
            FieldDraft fieldDraft;
            PlotDraft plotDraft;
            try { clientDraft = new(validate.CreateClient(new ClientDraft(clientName ?? "")).Name); }
            catch (DomainValidationException e) { counts.ClientsRejected++; rejections.Add($"Row {row}: client rejected ({e.Message})."); continue; }
            try { fieldDraft = new(validate.CreateField(Guid.Empty, new FieldDraft(fieldName ?? "")).Name); }
            catch (DomainValidationException e) { counts.FieldsRejected++; rejections.Add($"Row {row}: field rejected ({e.Message})."); continue; }
            try
            {
                var checkedPlot = validate.CreatePlot(Guid.Empty, new PlotDraft(plotName ?? "", Number(Cell(cells, columns.Area)) ?? 0m, Cell(cells, columns.Code)));
                plotDraft = new(checkedPlot.Name, checkedPlot.AreaHectares, checkedPlot.Code);
            }
            catch (DomainValidationException e) { counts.PlotsRejected++; rejections.Add($"Row {row}: plot rejected ({e.Message})."); continue; }

            var reference = $"{PlotSheetName}!{row}";
            var payload = RowPayload(columns.Header, cells);
            if (!clients.TryGetValue(clientDraft.Name, out var client))
            {
                client = operations.CreateClient(clientDraft);
                clients[clientDraft.Name] = client;
                counted.Add(client.Id);
                counts.ClientsCreated++;
                if (!Traced(provenance, "client", client.Id, reference, payload, row, error)) return 1;
            }
            else if (counted.Add(client.Id)) counts.ClientsExisting++;

            if (!fields.TryGetValue(ChildKey(client.Id, fieldDraft.Name), out var field))
            {
                field = operations.CreateField(client.Id, fieldDraft);
                fields[ChildKey(client.Id, field.Name)] = field;
                counted.Add(field.Id);
                counts.FieldsCreated++;
                if (!Traced(provenance, "field", field.Id, reference, payload, row, error)) return 1;
            }
            else if (counted.Add(field.Id)) counts.FieldsExisting++;

            var plotKey = ChildKey(field.Id, plotDraft.Name);
            if (plots.TryGetValue(plotKey, out var plot))
            {
                if (counted.Add(plot.Id)) counts.PlotsExisting++;
                // Only a missing code is filled in; a stored code, name or area is never replaced.
                if (plot.Code is not null || plotDraft.Code is not { } code) continue;
                if (codes.Contains(code)) { counts.CodesSkipped++; continue; }
                try { plots[plotKey] = operations.UpdatePlot(plot.Id, plot.Revision, new PlotDraft(plot.Name, plot.AreaHectares, code)); }
                catch (Exception e) when (e is DomainDuplicateException or DomainConflictException) { counts.CodesSkipped++; continue; }
                codes.Add(code);
                counts.CodesSet++;
                continue;
            }
            if (plotDraft.Code is { } taken && codes.Contains(taken))
            {
                counts.CodesSkipped++;
                plotDraft = plotDraft with { Code = null };
            }
            try { plot = operations.CreatePlot(field.Id, plotDraft); }
            catch (DomainDuplicateException)
            {
                counts.CodesSkipped++;
                plot = operations.CreatePlot(field.Id, plotDraft with { Code = null });
            }
            plots[plotKey] = plot;
            if (plot.Code is { } stored) codes.Add(stored);
            counted.Add(plot.Id);
            counts.PlotsCreated++;
            if (!Traced(provenance, "plot", plot.Id, reference, payload, row, error)) return 1;
        }

        // Firms: the highest numeric order number per firm, the first spelling of each name wins.
        var orderFirms = new Dictionary<string, (string Spelling, int? Highest)>();
        foreach (var (_, cells) in orderSheet.Where(r => r.Key > 1))
        {
            if (Cell(cells, orderColumns.Firm) is not { } rawFirm || string.IsNullOrWhiteSpace(rawFirm)) continue;
            counts.OrderRows++;
            var key = NameKey(rawFirm);
            var number = Number(Cell(cells, orderColumns.Number)) is { } value && value == decimal.Truncate(value) && value is >= 1m and < int.MaxValue
                ? (int?)value : null;
            orderFirms[key] = orderFirms.TryGetValue(key, out var seen)
                ? seen with { Highest = Max(seen.Highest, number) }
                : (rawFirm.Trim(), number);
        }
        counts.Firms = orderFirms.Count;
        var clientsByKey = operations.ListClients().ToLookup(c => NameKey(c.Name));
        foreach (var (key, (spelling, highest)) in orderFirms)
        {
            // Only the firm created with the client's name is updated; no firm is created for a firm without a client.
            var firm = clientsByKey[key].ToList() is [var owner] ? firms.List(owner.Id).FirstOrDefault(f => NameKey(f.Name) == key) : null;
            if (firm is null) { counts.Unmatched++; continue; }
            var rename = firm.Name != spelling;
            int? next = highest is { } top && top + 1 > firm.NextWorkOrderNumber ? top + 1 : null;
            if (!rename && next is null) continue;
            try { firms.Update(firm.Id, firm.Revision, rename ? spelling : firm.Name, firm.TaxId, next); }
            catch (Exception e) when (e is DomainDuplicateException or DomainConflictException) { counts.Unmatched++; continue; }
            if (rename) counts.Renamed++;
            if (next is not null) counts.NumbersSet++;
        }

        var created = dryRun ? "to create" : "created";
        var existing = (int count) => connectionString is null ? "existing not checked" : $"existing skipped {count}";
        lines.Add($"Sheet {PlotSheetName}: {counts.Rows} plot rows, {counts.Blank} blank rows skipped.");
        lines.Add($"Clients: {created} {counts.ClientsCreated}, {existing(counts.ClientsExisting)}, rejected {counts.ClientsRejected}.");
        lines.Add($"Fields: {created} {counts.FieldsCreated}, {existing(counts.FieldsExisting)}, rejected {counts.FieldsRejected}.");
        lines.Add($"Plots: {created} {counts.PlotsCreated}, {existing(counts.PlotsExisting)}, {(dryRun ? "codes to set" : "codes set")} {counts.CodesSet}, "
            + $"codes skipped {counts.CodesSkipped}, rejected {counts.PlotsRejected}.");
        lines.AddRange(rejections);
        lines.Add($"Sheet {OrderSheetName}: {counts.OrderRows} order rows, {counts.Firms} firms.");
        lines.Add($"Firms: {(dryRun ? "to rename" : "renamed")} {counts.Renamed}, {(dryRun ? "next number to set" : "next number set")} {counts.NumbersSet}, "
            + $"unmatched {counts.Unmatched}.");
        return null;
    }

    private static int? Max(int? a, int? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);

    private static string ChildKey(Guid parent, string name) => $"{parent:N}|{name.Trim()}";

    private static bool Traced(ImportProvenanceService? provenance, string entityType, Guid id, string reference, string payload, int row, TextWriter error)
    {
        if (provenance is null || Recorded(provenance, entityType, id, reference, payload)) return true;
        Fail(error, 1, $"Row {row}: {entityType} created but its import record failed; add its provenance before rerunning.");
        return false;
    }

    // Every cell of the row, keyed by its header text or, without one, by its column letter.
    private static string RowPayload(Dictionary<int, string> header, Dictionary<int, string> cells)
    {
        var payload = new Dictionary<string, string?>();
        foreach (var (column, value) in cells.OrderBy(c => c.Key))
        {
            var key = header.TryGetValue(column, out var name) && !string.IsNullOrWhiteSpace(name) ? name.Trim() : ColumnName(column);
            payload[payload.ContainsKey(key) ? $"{key} ({ColumnName(column)})" : key] = value;
        }
        return Payload(payload);
    }

    private static string ColumnName(int index)
    {
        var name = "";
        for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }

    private sealed class ValidationOnlyOperations : IOperationsRepository
    {
        public Client CreateClient(ClientDraft c) => new(Guid.Empty, c.Name, c.TaxId, c.Producers, c.Email, c.Phone, c.Address, 1);
        public Field CreateField(Guid clientId, FieldDraft f) => new(Guid.Empty, clientId, f.Name, f.Tenure, f.Locality, 1);
        public Plot CreatePlot(Guid fieldId, PlotDraft p) => new(Guid.Empty, fieldId, p.Name, p.AreaHectares, p.Code, 1);
        public Client UpdateClient(Guid id, long revision, ClientDraft client) => throw new NotSupportedException();
        public Field UpdateField(Guid id, long revision, FieldDraft field) => throw new NotSupportedException();
        public Plot UpdatePlot(Guid id, long revision, PlotDraft plot) => throw new NotSupportedException();
        public Client? GetClient(Guid id) => null;
        public Field? GetField(Guid id) => null;
        public Plot? GetPlot(Guid id) => null;
        public IReadOnlyList<Client> ListClients() => [];
        public IReadOnlyList<Field> ListFields(Guid? clientId) => [];
        public IReadOnlyList<Plot> ListPlots(Guid? fieldId) => [];
    }

    /// <summary>Dry-run stand-in for the stored hierarchy; creating a client also creates its firm, as the database does.</summary>
    private sealed class InMemoryOperations(InMemoryFirms firms) : IOperationsRepository
    {
        private readonly List<Client> clients = [];
        private readonly List<Field> fields = [];
        private readonly List<Plot> plots = [];

        public static InMemoryOperations Copy(IOperationsRepository source, InMemoryFirms firms)
        {
            var copy = new InMemoryOperations(firms);
            copy.clients.AddRange(source.ListClients());
            copy.fields.AddRange(source.ListFields(null));
            copy.plots.AddRange(source.ListPlots(null));
            return copy;
        }

        public Client CreateClient(ClientDraft c)
        {
            var client = new Client(Guid.NewGuid(), c.Name, c.TaxId, c.Producers, c.Email, c.Phone, c.Address, 1);
            clients.Add(client);
            firms.CreateUnlessTaken(client.Id, client.Name);
            return client;
        }
        public Field CreateField(Guid clientId, FieldDraft f) => Add(fields, new Field(Guid.NewGuid(), clientId, f.Name, f.Tenure, f.Locality, 1));
        public Plot CreatePlot(Guid fieldId, PlotDraft p) => Add(plots, new Plot(Guid.NewGuid(), fieldId, p.Name, p.AreaHectares, p.Code, 1));
        public Plot UpdatePlot(Guid id, long revision, PlotDraft p)
        {
            var index = plots.FindIndex(x => x.Id == id && x.Revision == revision);
            if (index < 0) throw new DomainConflictException();
            return plots[index] = plots[index] with { Name = p.Name, AreaHectares = p.AreaHectares, Code = p.Code, Revision = revision + 1 };
        }
        public Client UpdateClient(Guid id, long revision, ClientDraft client) => throw new NotSupportedException();
        public Field UpdateField(Guid id, long revision, FieldDraft field) => throw new NotSupportedException();
        public Client? GetClient(Guid id) => clients.Find(c => c.Id == id);
        public Field? GetField(Guid id) => fields.Find(f => f.Id == id);
        public Plot? GetPlot(Guid id) => plots.Find(p => p.Id == id);
        public IReadOnlyList<Client> ListClients() => clients.ToList();
        public IReadOnlyList<Field> ListFields(Guid? clientId) => fields.Where(f => clientId is null || f.ClientId == clientId).ToList();
        public IReadOnlyList<Plot> ListPlots(Guid? fieldId) => plots.Where(p => fieldId is null || p.FieldId == fieldId).ToList();

        private static T Add<T>(List<T> list, T item)
        {
            list.Add(item);
            return item;
        }
    }

    private sealed class InMemoryFirms(IReadOnlyList<CommercialFirm> stored) : ICommercialFirmRepository
    {
        private readonly List<CommercialFirm> firms = stored.ToList();

        public void CreateUnlessTaken(Guid clientId, string name)
        {
            if (!firms.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                firms.Add(new(Guid.NewGuid(), clientId, name, null, 1, 1));
        }
        public CommercialFirm Create(Guid clientId, string name, string? taxId) => throw new NotSupportedException();
        public CommercialFirm Update(Guid id, long revision, string name, string? taxId, int? nextWorkOrderNumber)
        {
            var index = firms.FindIndex(f => f.Id == id && f.Revision == revision);
            if (index < 0) throw new DomainConflictException();
            if (firms.Any(f => f.Id != id && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))) throw new DomainDuplicateException();
            return firms[index] = firms[index] with
            {
                Name = name, TaxId = taxId, NextWorkOrderNumber = nextWorkOrderNumber ?? firms[index].NextWorkOrderNumber, Revision = revision + 1,
            };
        }
        public IReadOnlyList<CommercialFirm> List(Guid? clientId) => firms.Where(f => clientId is null || f.ClientId == clientId).ToList();
    }
}

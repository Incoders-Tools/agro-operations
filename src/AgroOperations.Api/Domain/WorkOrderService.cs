namespace AgroOperations.Api.Domain;

/// <summary>FieldId is the plot's field; an order has no field of its own.</summary>
public sealed record WorkOrderPlot(Guid PlotId, Guid FieldId, decimal AreaHectares, decimal WorkedAreaHectares, bool WorkedAreaExceedsPlot);
public sealed record WorkOrderLine(Guid Id, short Position, Guid InputId, decimal DosePerHectare, string Unit, decimal Total, bool DoseOutOfRange);
public sealed record WorkOrder(Guid Id, Guid FirmId, int Number, DateOnly IssuedOn, Guid LaborTypeId, Guid? StageId, Guid ContractorId,
    Guid? CampaignId, Guid? CropId, string? Variety, string? Observations, string? ResponsibleEngineer, string Status, string? CancelReason,
    DateTimeOffset CreatedAt, long Revision, IReadOnlyList<WorkOrderPlot> Plots, IReadOnlyList<WorkOrderLine> Lines);
/// <summary>FieldIds are the distinct fields of the order's plots, ordered by field name.</summary>
public sealed record WorkOrderSummary(Guid Id, Guid FirmId, int Number, DateOnly IssuedOn, Guid LaborTypeId, Guid? StageId, Guid ContractorId,
    IReadOnlyList<Guid> FieldIds, string Status, int PlotCount, long Revision);

public sealed record WorkOrderPlotDraft(Guid PlotId, decimal WorkedAreaHectares);
public sealed record WorkOrderLineDraft(Guid InputId, decimal DosePerHectare);
/// <summary>Writes replace the header, plots and lines; firm and number are never part of a draft.</summary>
public sealed record WorkOrderDraft(DateOnly? IssuedOn, Guid LaborTypeId, Guid? StageId, Guid ContractorId, Guid? CampaignId,
    Guid? CropId, string? Variety, string? Observations, string? ResponsibleEngineer, IReadOnlyList<WorkOrderPlotDraft>? Plots,
    IReadOnlyList<WorkOrderLineDraft>? Lines);

public sealed record WorkOrderDocumentFirm(string Name, string? TaxId);
public sealed record WorkOrderDocumentLabor(string Name, string? Abbreviation);
public sealed record WorkOrderDocumentContractor(string Name, bool OwnEquipment);
public sealed record WorkOrderDocumentPlot(string FieldName, string PlotName, string? PlotCode, decimal AreaHectares, decimal WorkedAreaHectares,
    bool WorkedAreaExceedsPlot);
public sealed record WorkOrderDocumentLine(short Position, string InputName, string? Formulation, short? MixOrder, decimal DosePerHectare, string Unit,
    decimal Total, bool DoseOutOfRange, bool HollowCone);
/// <summary>
/// Everything a printed OT shows, with names instead of ids. Plots are ordered by field then plot name;
/// the plot area and line unit are the snapshots stored with the order.
/// </summary>
public sealed record WorkOrderDocument(int Number, DateOnly IssuedOn, string Status, string? CancelReason, WorkOrderDocumentFirm Firm, string ClientName,
    WorkOrderDocumentLabor Labor, string? StageName, WorkOrderDocumentContractor Contractor, string? CampaignName, string? CropName, string? Variety,
    IReadOnlyList<WorkOrderDocumentPlot> Plots, IReadOnlyList<WorkOrderDocumentLine> Lines, string? Observations, string? ResponsibleEngineer)
{
    private static readonly string[] Months = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];
    // Windows rejects these in file names; the browser uses the name when saving the print view as PDF.
    private static readonly char[] Forbidden = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public decimal TotalWorkedAreaHectares => Plots.Sum(plot => plot.WorkedAreaHectares);

    /// <summary>
    /// The workbook name without extension: FIRMA_ORDEN_n, then CAMPO_(LOTE) for each plot, the labor abbreviation
    /// (or its name) and the d-mmm date, such as 10-nov.
    /// </summary>
    public string FileName
    {
        get
        {
            var parts = new List<string> { Firm.Name.ToUpperInvariant(), "ORDEN", Number.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            foreach (var plot in Plots)
            {
                parts.Add(plot.FieldName.ToUpperInvariant());
                parts.Add($"({plot.PlotName})");
            }
            parts.Add(Labor.Abbreviation ?? Labor.Name);
            parts.Add($"{IssuedOn.Day}-{Months[IssuedOn.Month - 1]}");
            return new string(string.Join("_", parts).Select(c => c < ' ' || Array.IndexOf(Forbidden, c) >= 0 ? '-' : c).ToArray());
        }
    }
}

public interface IWorkOrderRepository
{
    /// <summary>lineTotals holds the total of each draft line, in order.</summary>
    WorkOrder Create(Guid firmId, WorkOrderDraft order, IReadOnlyList<decimal> lineTotals);
    WorkOrder Update(Guid id, long revision, WorkOrderDraft order, IReadOnlyList<decimal> lineTotals);
    /// <summary>Moves an emitida order to status; a missing order is not found, a closed one not editable.</summary>
    WorkOrder Close(Guid id, long revision, string status, string? cancelReason);
    WorkOrder? Get(Guid id);
    /// <summary>number matches the order number; text is contained, without regard to case, in a firm, field or plot name or plot code.</summary>
    IReadOnlyList<WorkOrderSummary> List(Guid? firmId, string? status, int? number, string? text);
    WorkOrderDocument? Document(Guid id);
}

/// <summary>
/// A work order (OT) as the workbook `Orden` sheet: a labor on one or more plots of one or more fields of the firm's client,
/// with its input lines.
/// Numbers are consecutive per commercial firm and never reused; only an emitida order can change.
/// </summary>
public sealed class WorkOrderService(IWorkOrderRepository repository)
{
    private static readonly string[] Statuses = ["emitida", "realizada", "cancelada"];

    private static bool FitsArea(decimal value) => value > 0 && decimal.Round(value, 4) == value && value <= 99999999.9999m;

    private static (WorkOrderDraft Order, IReadOnlyList<decimal> LineTotals) Validated(WorkOrderDraft draft)
    {
        if (draft.IssuedOn is null) throw new DomainValidationException("Issued on is required");
        var plots = draft.Plots ?? [];
        if (plots.Count == 0) throw new DomainValidationException("At least one plot is required");
        if (plots.Select(plot => plot.PlotId).Distinct().Count() != plots.Count) throw new DomainValidationException("Each plot can appear only once");
        if (!plots.All(plot => FitsArea(plot.WorkedAreaHectares)))
            throw new DomainValidationException("Worked area must be positive and fit numeric(12,4)");
        var lines = draft.Lines ?? [];
        if (!lines.All(line => FitsArea(line.DosePerHectare))) throw new DomainValidationException("Dose must be positive and fit numeric(12,4)");
        var workedArea = plots.Sum(plot => plot.WorkedAreaHectares);
        // Rounded half away from zero, as PostgreSQL rounds numeric values.
        var totals = lines.Select(line => decimal.Round(line.DosePerHectare * workedArea, 4, MidpointRounding.AwayFromZero)).ToArray();
        if (!totals.All(total => total > 0 && total <= 9999999999.9999m))
            throw new DomainValidationException("Line total must be positive and fit numeric(14,4)");
        return (draft with
        {
            Variety = CatalogText.Optional(draft.Variety), Observations = CatalogText.Optional(draft.Observations),
            ResponsibleEngineer = CatalogText.Optional(draft.ResponsibleEngineer), Plots = plots, Lines = lines
        }, totals);
    }

    public WorkOrder Create(Guid firmId, WorkOrderDraft order)
    {
        var (validated, totals) = Validated(order);
        return repository.Create(firmId, validated, totals);
    }

    public WorkOrder Update(Guid id, long revision, WorkOrderDraft order)
    {
        var (validated, totals) = Validated(order);
        return repository.Update(id, revision, validated, totals);
    }

    /// <summary>The order keeps its number and stays queryable.</summary>
    public WorkOrder Cancel(Guid id, long revision, string? reason) =>
        repository.Close(id, revision, "cancelada", CatalogText.Required(reason, "Reason"));
    public WorkOrder Complete(Guid id, long revision) => repository.Close(id, revision, "realizada", null);
    public WorkOrder? Get(Guid id) => repository.Get(id);

    public WorkOrderDocument? Document(Guid id) => repository.Document(id);

    /// <summary>
    /// Without a firm, status or search, every work order. A search matches the order number when it is an integer,
    /// or text in the firm, field or plot names or plot codes.
    /// </summary>
    public IReadOnlyList<WorkOrderSummary> List(Guid? firmId = null, string? status = null, string? q = null)
    {
        var filter = CatalogText.Optional(status)?.ToLowerInvariant();
        if (filter is not null && Array.IndexOf(Statuses, filter) < 0)
            throw new DomainValidationException($"Status must be one of: {string.Join(", ", Statuses)}");
        var text = CatalogText.Optional(q);
        int? number = int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        return repository.List(firmId, filter, number, text);
    }
}

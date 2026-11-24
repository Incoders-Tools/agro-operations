namespace AgroOperations.Api.Domain;

public sealed record WorkOrderPlot(Guid PlotId, decimal AreaHectares, decimal WorkedAreaHectares, bool WorkedAreaExceedsPlot);
public sealed record WorkOrderLine(Guid Id, short Position, Guid InputId, decimal DosePerHectare, string Unit, decimal Total, bool DoseOutOfRange);
public sealed record WorkOrder(Guid Id, Guid FirmId, int Number, DateOnly IssuedOn, Guid LaborTypeId, Guid? StageId, Guid ContractorId, Guid FieldId,
    Guid? CampaignId, Guid? CropId, string? Variety, string? Observations, string? ResponsibleEngineer, string Status, string? CancelReason,
    DateTimeOffset CreatedAt, long Revision, IReadOnlyList<WorkOrderPlot> Plots, IReadOnlyList<WorkOrderLine> Lines);
public sealed record WorkOrderSummary(Guid Id, Guid FirmId, int Number, DateOnly IssuedOn, Guid LaborTypeId, Guid? StageId, Guid ContractorId,
    Guid FieldId, string Status, int PlotCount, long Revision);

public sealed record WorkOrderPlotDraft(Guid PlotId, decimal WorkedAreaHectares);
public sealed record WorkOrderLineDraft(Guid InputId, decimal DosePerHectare);
/// <summary>Writes replace the header, plots and lines; firm and number are never part of a draft.</summary>
public sealed record WorkOrderDraft(DateOnly? IssuedOn, Guid LaborTypeId, Guid? StageId, Guid ContractorId, Guid FieldId, Guid? CampaignId,
    Guid? CropId, string? Variety, string? Observations, string? ResponsibleEngineer, IReadOnlyList<WorkOrderPlotDraft>? Plots,
    IReadOnlyList<WorkOrderLineDraft>? Lines);

public interface IWorkOrderRepository
{
    /// <summary>lineTotals holds the total of each draft line, in order.</summary>
    WorkOrder Create(Guid firmId, WorkOrderDraft order, IReadOnlyList<decimal> lineTotals);
    WorkOrder Update(Guid id, long revision, WorkOrderDraft order, IReadOnlyList<decimal> lineTotals);
    /// <summary>Moves an emitida order to status; a missing order is not found, a closed one not editable.</summary>
    WorkOrder Close(Guid id, long revision, string status, string? cancelReason);
    WorkOrder? Get(Guid id);
    IReadOnlyList<WorkOrderSummary> List(Guid? firmId, string? status);
}

/// <summary>
/// A work order (OT) as the workbook `Orden` sheet: a labor on one or more plots of one field, with its input lines.
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

    /// <summary>Without a firm or status, every work order.</summary>
    public IReadOnlyList<WorkOrderSummary> List(Guid? firmId = null, string? status = null)
    {
        var filter = CatalogText.Optional(status)?.ToLowerInvariant();
        if (filter is not null && Array.IndexOf(Statuses, filter) < 0)
            throw new DomainValidationException($"Status must be one of: {string.Join(", ", Statuses)}");
        return repository.List(firmId, filter);
    }
}

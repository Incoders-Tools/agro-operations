using AgroOperations.Api.Domain;
using Npgsql;
using NpgsqlTypes;

namespace AgroOperations.Api.Persistence;

/// <summary>Every write runs in one transaction, so a rejected request leaves orders, plots, lines and the firm's next number unchanged.</summary>
public sealed class PostgresWorkOrderRepository(string connectionString) : IWorkOrderRepository
{
    private const string HeaderColumns = """
        id,firm_id,number,issued_on,labor_type_id,stage_id,contractor_id,field_id,campaign_id,crop_id,
        variety,observations,responsible_engineer,status,cancel_reason,created_at,revision
        """;

    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static Guid? Id(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetGuid(i);
    private static NpgsqlParameter Uuid(string name, Guid? value) => new(name, NpgsqlDbType.Uuid) { Value = (object?)value ?? DBNull.Value };
    private static NpgsqlParameter Varchar(string name, string? value) => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    private NpgsqlConnection Open()
    {
        var db = new NpgsqlConnection(connectionString);
        db.Open();
        return db;
    }

    public WorkOrder Create(Guid firmId, WorkOrderDraft order, IReadOnlyList<decimal> lineTotals)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        // Taking the number first locks the firm row until commit: concurrent creates of one firm serialize,
        // and a rejected create rolls the number back with everything else.
        int number;
        Guid clientId;
        using (var allocate = new NpgsqlCommand("""
            UPDATE commercial_firms SET next_work_order_number=next_work_order_number+1 WHERE id=@firm
            RETURNING next_work_order_number-1, client_id
            """, db, tx))
        {
            allocate.Parameters.AddWithValue("firm", firmId);
            using var row = allocate.ExecuteReader();
            if (!row.Read()) throw new DomainNotFoundException();
            number = row.GetInt32(0);
            clientId = row.GetGuid(1);
        }
        var (plotAreas, units) = References(db, tx, clientId, order);
        var id = Guid.NewGuid();
        using (var insert = new NpgsqlCommand("""
            INSERT INTO work_orders(id,firm_id,number,issued_on,labor_type_id,stage_id,contractor_id,field_id,campaign_id,crop_id,
              variety,observations,responsible_engineer)
            VALUES (@id,@firm,@number,@issuedOn,@labor,@stage,@contractor,@field,@campaign,@crop,@variety,@observations,@engineer)
            """, db, tx))
        {
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("firm", firmId);
            insert.Parameters.AddWithValue("number", number);
            BindHeader(insert, order);
            try { insert.ExecuteNonQuery(); }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DomainDuplicateException("Duplicate number");
            }
        }
        WriteChildren(db, tx, id, order, lineTotals, plotAreas, units);
        tx.Commit();
        return Read(db, id)!;
    }

    public WorkOrder Update(Guid id, long revision, WorkOrderDraft order, IReadOnlyList<decimal> lineTotals)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        Guid clientId;
        using (var current = new NpgsqlCommand("""
            SELECT w.status, w.revision, f.client_id FROM work_orders w JOIN commercial_firms f ON f.id=w.firm_id
            WHERE w.id=@id FOR UPDATE OF w
            """, db, tx))
        {
            current.Parameters.AddWithValue("id", id);
            using var row = current.ExecuteReader();
            if (!row.Read()) throw new DomainNotFoundException();
            if (row.GetString(0) != "emitida") throw new DomainStateException("Work order is not editable");
            if (row.GetInt64(1) != revision) throw new DomainConflictException();
            clientId = row.GetGuid(2);
        }
        var (plotAreas, units) = References(db, tx, clientId, order);
        using (var update = new NpgsqlCommand("""
            UPDATE work_orders SET issued_on=@issuedOn,labor_type_id=@labor,stage_id=@stage,contractor_id=@contractor,field_id=@field,
              campaign_id=@campaign,crop_id=@crop,variety=@variety,observations=@observations,responsible_engineer=@engineer,revision=revision+1
            WHERE id=@id;
            DELETE FROM work_order_lines WHERE work_order_id=@id;
            DELETE FROM work_order_plots WHERE work_order_id=@id;
            """, db, tx))
        {
            update.Parameters.AddWithValue("id", id);
            BindHeader(update, order);
            update.ExecuteNonQuery();
        }
        WriteChildren(db, tx, id, order, lineTotals, plotAreas, units);
        tx.Commit();
        return Read(db, id)!;
    }

    public WorkOrder Close(Guid id, long revision, string status, string? cancelReason)
    {
        using var db = Open();
        using (var close = new NpgsqlCommand("""
            UPDATE work_orders SET status=@status,cancel_reason=@reason,revision=revision+1
            WHERE id=@id AND revision=@revision AND status='emitida'
            """, db))
        {
            close.Parameters.AddWithValue("id", id);
            close.Parameters.AddWithValue("revision", revision);
            close.Parameters.AddWithValue("status", status);
            close.Parameters.Add(Varchar("reason", cancelReason));
            if (close.ExecuteNonQuery() == 1) return Read(db, id)!;
        }
        using var current = new NpgsqlCommand("SELECT status FROM work_orders WHERE id=@id", db);
        current.Parameters.AddWithValue("id", id);
        if (current.ExecuteScalar() is not string stored) throw new DomainNotFoundException();
        if (stored != "emitida") throw new DomainStateException("Work order is not editable");
        throw new DomainConflictException();
    }

    public WorkOrder? Get(Guid id)
    {
        using var db = Open();
        return Read(db, id);
    }

    public IReadOnlyList<WorkOrderSummary> List(Guid? firmId, string? status)
    {
        using var db = Open();
        var filters = new List<string>();
        if (firmId is not null) filters.Add("w.firm_id=@firm");
        if (status is not null) filters.Add("w.status=@status");
        using var cmd = new NpgsqlCommand($"""
            SELECT w.id,w.firm_id,w.number,w.issued_on,w.labor_type_id,w.stage_id,w.contractor_id,w.field_id,w.status,
              (SELECT count(*) FROM work_order_plots p WHERE p.work_order_id=w.id)::integer,w.revision
            FROM work_orders w {(filters.Count == 0 ? "" : "WHERE " + string.Join(" AND ", filters))}
            ORDER BY w.issued_on DESC,w.number DESC,w.id
            """, db);
        if (firmId is { } firm) cmd.Parameters.AddWithValue("firm", firm);
        if (status is not null) cmd.Parameters.AddWithValue("status", status);
        using var row = cmd.ExecuteReader();
        var orders = new List<WorkOrderSummary>();
        while (row.Read())
            orders.Add(new(row.GetGuid(0), row.GetGuid(1), row.GetInt32(2), row.GetFieldValue<DateOnly>(3), row.GetGuid(4), Id(row, 5),
                row.GetGuid(6), row.GetGuid(7), row.GetString(8), row.GetInt32(9), row.GetInt64(10)));
        return orders;
    }

    private static void BindHeader(NpgsqlCommand cmd, WorkOrderDraft order)
    {
        cmd.Parameters.AddWithValue("issuedOn", order.IssuedOn!.Value);
        cmd.Parameters.AddWithValue("labor", order.LaborTypeId);
        cmd.Parameters.Add(Uuid("stage", order.StageId));
        cmd.Parameters.AddWithValue("contractor", order.ContractorId);
        cmd.Parameters.AddWithValue("field", order.FieldId);
        cmd.Parameters.Add(Uuid("campaign", order.CampaignId));
        cmd.Parameters.Add(Uuid("crop", order.CropId));
        cmd.Parameters.Add(Varchar("variety", order.Variety));
        cmd.Parameters.Add(Varchar("observations", order.Observations));
        cmd.Parameters.Add(Varchar("engineer", order.ResponsibleEngineer));
    }

    /// <summary>
    /// Missing referenced rows are not found; a field of another client or a plot of another field is invalid.
    /// Returns the current plot areas and input units, which the order stores as its snapshot.
    /// </summary>
    private static (Dictionary<Guid, decimal> PlotAreas, Dictionary<Guid, string> Units) References(
        NpgsqlConnection db, NpgsqlTransaction tx, Guid clientId, WorkOrderDraft order)
    {
        Guid? fieldClient;
        using (var header = new NpgsqlCommand("""
            SELECT (SELECT client_id FROM fields WHERE id=@field),
              EXISTS(SELECT 1 FROM labor_types WHERE id=@labor) AND EXISTS(SELECT 1 FROM contractors WHERE id=@contractor)
              AND (@stage IS NULL OR EXISTS(SELECT 1 FROM stages WHERE id=@stage))
              AND (@campaign IS NULL OR EXISTS(SELECT 1 FROM campaigns WHERE id=@campaign))
              AND (@crop IS NULL OR EXISTS(SELECT 1 FROM crops WHERE id=@crop))
            """, db, tx))
        {
            header.Parameters.AddWithValue("field", order.FieldId);
            header.Parameters.AddWithValue("labor", order.LaborTypeId);
            header.Parameters.AddWithValue("contractor", order.ContractorId);
            header.Parameters.Add(Uuid("stage", order.StageId));
            header.Parameters.Add(Uuid("campaign", order.CampaignId));
            header.Parameters.Add(Uuid("crop", order.CropId));
            using var row = header.ExecuteReader();
            row.Read();
            fieldClient = Id(row, 0);
            if (fieldClient is null || !row.GetBoolean(1)) throw new DomainNotFoundException();
        }
        var plotIds = order.Plots!.Select(plot => plot.PlotId).ToArray();
        var plotAreas = new Dictionary<Guid, decimal>();
        var plotFields = new List<Guid>();
        using (var plots = new NpgsqlCommand("SELECT id,field_id,area_hectares FROM plots WHERE id = ANY(@ids)", db, tx))
        {
            plots.Parameters.AddWithValue("ids", plotIds);
            using var row = plots.ExecuteReader();
            while (row.Read())
            {
                plotAreas[row.GetGuid(0)] = row.GetDecimal(2);
                plotFields.Add(row.GetGuid(1));
            }
        }
        var units = new Dictionary<Guid, string>();
        var inputIds = order.Lines!.Select(line => line.InputId).Distinct().ToArray();
        using (var inputs = new NpgsqlCommand("SELECT id,unit FROM inputs WHERE id = ANY(@ids)", db, tx))
        {
            inputs.Parameters.AddWithValue("ids", inputIds);
            using var row = inputs.ExecuteReader();
            while (row.Read()) units[row.GetGuid(0)] = row.GetString(1);
        }
        if (plotAreas.Count != plotIds.Length || units.Count != inputIds.Length) throw new DomainNotFoundException();
        if (fieldClient != clientId) throw new DomainValidationException("Field must belong to the firm's client");
        if (plotFields.Any(field => field != order.FieldId)) throw new DomainValidationException("Every plot must belong to the work order's field");
        return (plotAreas, units);
    }

    private static void WriteChildren(NpgsqlConnection db, NpgsqlTransaction tx, Guid id, WorkOrderDraft order, IReadOnlyList<decimal> lineTotals,
        Dictionary<Guid, decimal> plotAreas, Dictionary<Guid, string> units)
    {
        foreach (var plot in order.Plots!)
        {
            using var insert = new NpgsqlCommand(
                "INSERT INTO work_order_plots(work_order_id,plot_id,area_hectares,worked_area_hectares) VALUES (@order,@plot,@area,@worked)", db, tx);
            insert.Parameters.AddWithValue("order", id);
            insert.Parameters.AddWithValue("plot", plot.PlotId);
            insert.Parameters.AddWithValue("area", plotAreas[plot.PlotId]);
            insert.Parameters.AddWithValue("worked", plot.WorkedAreaHectares);
            insert.ExecuteNonQuery();
        }
        for (var i = 0; i < order.Lines!.Count; i++)
        {
            var line = order.Lines[i];
            using var insert = new NpgsqlCommand("""
                INSERT INTO work_order_lines(id,work_order_id,position,input_id,dose_per_hectare,unit,total)
                VALUES (@id,@order,@position,@input,@dose,@unit,@total)
                """, db, tx);
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("order", id);
            insert.Parameters.AddWithValue("position", (short)(i + 1));
            insert.Parameters.AddWithValue("input", line.InputId);
            insert.Parameters.AddWithValue("dose", line.DosePerHectare);
            insert.Parameters.AddWithValue("unit", units[line.InputId]);
            insert.Parameters.AddWithValue("total", lineTotals[i]);
            insert.ExecuteNonQuery();
        }
    }

    // The warning flags compare with the stored plot area snapshot and with the input's current dose range.
    private static WorkOrder? Read(NpgsqlConnection db, Guid id)
    {
        var plots = new List<WorkOrderPlot>();
        using (var cmd = new NpgsqlCommand("""
            SELECT w.plot_id,w.area_hectares,w.worked_area_hectares,w.worked_area_hectares > w.area_hectares
            FROM work_order_plots w JOIN plots p ON p.id=w.plot_id WHERE w.work_order_id=@id ORDER BY p.name,p.id
            """, db))
        {
            cmd.Parameters.AddWithValue("id", id);
            using var row = cmd.ExecuteReader();
            while (row.Read()) plots.Add(new(row.GetGuid(0), row.GetDecimal(1), row.GetDecimal(2), row.GetBoolean(3)));
        }
        var lines = new List<WorkOrderLine>();
        using (var cmd = new NpgsqlCommand("""
            SELECT l.id,l.position,l.input_id,l.dose_per_hectare,l.unit,l.total,
              coalesce(l.dose_per_hectare < i.dose_min, false) OR coalesce(l.dose_per_hectare > i.dose_max, false)
            FROM work_order_lines l JOIN inputs i ON i.id=l.input_id WHERE l.work_order_id=@id ORDER BY l.position
            """, db))
        {
            cmd.Parameters.AddWithValue("id", id);
            using var row = cmd.ExecuteReader();
            while (row.Read())
                lines.Add(new(row.GetGuid(0), row.GetInt16(1), row.GetGuid(2), row.GetDecimal(3), row.GetString(4), row.GetDecimal(5), row.GetBoolean(6)));
        }
        using var header = new NpgsqlCommand($"SELECT {HeaderColumns} FROM work_orders WHERE id=@id", db);
        header.Parameters.AddWithValue("id", id);
        using var r = header.ExecuteReader();
        if (!r.Read()) return null;
        return new(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetFieldValue<DateOnly>(3), r.GetGuid(4), Id(r, 5), r.GetGuid(6), r.GetGuid(7),
            Id(r, 8), Id(r, 9), Text(r, 10), Text(r, 11), Text(r, 12), r.GetString(13), Text(r, 14), r.GetFieldValue<DateTimeOffset>(15),
            r.GetInt64(16), plots, lines);
    }
}

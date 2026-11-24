using AgroOperations.Api.Domain;
using Npgsql;
using NpgsqlTypes;

namespace AgroOperations.Api.Persistence;

/// <summary>
/// Quote writes and decisions lock the comparison row, so a quote never changes while a decision on its comparison commits;
/// every write runs in one transaction, so a rejected request leaves the comparison, its quotes and lines unchanged.
/// </summary>
public sealed class PostgresQuoteComparisonRepository(string connectionString) : IQuoteComparisonRepository
{
    private const string ComparisonColumns = "id,client_id,title,requested_on,status,chosen_quote_id,decision_reason,revision";

    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static Guid? Id(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetGuid(i);
    private static NpgsqlParameter Uuid(string name, Guid? value) => new(name, NpgsqlDbType.Uuid) { Value = (object?)value ?? DBNull.Value };
    private static NpgsqlParameter Varchar(string name, string? value) => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };
    private static QuoteComparison MapComparison(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetFieldValue<DateOnly>(3), r.GetString(4), Id(r, 5), Text(r, 6), r.GetInt64(7));

    private NpgsqlConnection Open()
    {
        var db = new NpgsqlConnection(connectionString);
        db.Open();
        return db;
    }

    public QuoteComparison Create(Guid clientId, string title, DateOnly requestedOn)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            INSERT INTO quote_comparisons(id,client_id,title,requested_on) SELECT @id,id,@title,@requestedOn FROM clients WHERE id=@client
            RETURNING {ComparisonColumns}
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("client", clientId);
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("requestedOn", requestedOn);
        using var row = cmd.ExecuteReader();
        if (!row.Read()) throw new DomainNotFoundException();
        return MapComparison(row);
    }

    public QuoteComparison Update(Guid id, long revision, string title, DateOnly requestedOn) =>
        Change(id, revision, "abierta", "title=@title,requested_on=@requestedOn", cmd =>
        {
            cmd.Parameters.AddWithValue("title", title);
            cmd.Parameters.AddWithValue("requestedOn", requestedOn);
        });

    public QuoteComparison Decide(Guid id, long revision, Guid quoteId, string reason) =>
        Change(id, revision, "abierta", "status='decidida',chosen_quote_id=@quote,decision_reason=@reason", cmd =>
        {
            // A quote of another comparison is checked here, before the UPDATE, so it is not found rather than a key violation.
            using (var quote = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM quotes WHERE id=@quote AND comparison_id=@id)", cmd.Connection, cmd.Transaction))
            {
                quote.Parameters.AddWithValue("quote", quoteId);
                quote.Parameters.AddWithValue("id", id);
                if (quote.ExecuteScalar() is not true) throw new DomainNotFoundException();
            }
            cmd.Parameters.AddWithValue("quote", quoteId);
            cmd.Parameters.AddWithValue("reason", reason);
        });

    // The chosen quote and the reason stay until the next decision.
    public QuoteComparison Reopen(Guid id, long revision) => Change(id, revision, "decidida", "status='abierta'", _ => { });

    /// <summary>
    /// Locks the comparison, then a missing one is not found, one in another status is a state error and a stale revision a conflict.
    /// bind may run checks in the same transaction before it adds the parameters of set.
    /// </summary>
    private QuoteComparison Change(Guid id, long revision, string requiredStatus, string set, Action<NpgsqlCommand> bind)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        RequireStatus(db, tx, id, requiredStatus, revision);
        using var update = new NpgsqlCommand($"UPDATE quote_comparisons SET {set},revision=revision+1 WHERE id=@id RETURNING {ComparisonColumns}", db, tx);
        update.Parameters.AddWithValue("id", id);
        bind(update);
        QuoteComparison changed;
        using (var row = update.ExecuteReader())
        {
            row.Read();
            changed = MapComparison(row);
        }
        tx.Commit();
        return changed;
    }

    private static void RequireStatus(NpgsqlConnection db, NpgsqlTransaction tx, Guid id, string requiredStatus, long? revision)
    {
        using var current = new NpgsqlCommand("SELECT status,revision FROM quote_comparisons WHERE id=@id FOR UPDATE", db, tx);
        current.Parameters.AddWithValue("id", id);
        using var row = current.ExecuteReader();
        if (!row.Read()) throw new DomainNotFoundException();
        if (row.GetString(0) != requiredStatus)
            throw new DomainStateException(requiredStatus == "abierta" ? "Comparison is not open" : "Comparison is not decided");
        if (revision is { } expected && row.GetInt64(1) != expected) throw new DomainConflictException();
    }

    public IReadOnlyList<QuoteComparison> List(Guid? clientId, string? status)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            SELECT {ComparisonColumns} FROM quote_comparisons WHERE (@client IS NULL OR client_id=@client) AND (@status IS NULL OR status=@status)
            ORDER BY requested_on DESC,created_at DESC,id
            """, db);
        cmd.Parameters.Add(Uuid("client", clientId));
        cmd.Parameters.Add(Varchar("status", status));
        using var row = cmd.ExecuteReader();
        var comparisons = new List<QuoteComparison>();
        while (row.Read()) comparisons.Add(MapComparison(row));
        return comparisons;
    }

    public (QuoteComparison Comparison, IReadOnlyList<Quote> Quotes, IReadOnlyDictionary<Guid, string> InputNames)? Detail(Guid id)
    {
        using var db = Open();
        // One snapshot, so the quotes match the comparison they are read with.
        using var tx = db.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
        QuoteComparison comparison;
        using (var cmd = new NpgsqlCommand($"SELECT {ComparisonColumns} FROM quote_comparisons WHERE id=@id", db, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            using var row = cmd.ExecuteReader();
            if (!row.Read()) return null;
            comparison = MapComparison(row);
        }
        var quotes = ReadQuotes(db, tx, "q.comparison_id=@id", id);
        var names = new Dictionary<Guid, string>();
        using (var cmd = new NpgsqlCommand("""
            SELECT DISTINCT i.id,i.commercial_name FROM quote_lines l JOIN quotes q ON q.id=l.quote_id JOIN inputs i ON i.id=l.input_id WHERE q.comparison_id=@id
            """, db, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            using var row = cmd.ExecuteReader();
            while (row.Read()) names[row.GetGuid(0)] = row.GetString(1);
        }
        tx.Commit();
        return (comparison, quotes, names);
    }

    public Quote CreateQuote(Guid comparisonId, QuoteDraft quote)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        RequireStatus(db, tx, comparisonId, "abierta", null);
        var units = References(db, tx, quote.ProviderId!.Value, quote);
        var id = Guid.NewGuid();
        using (var insert = new NpgsqlCommand("""
            INSERT INTO quotes(id,comparison_id,provider_id,received_on,currency,notes) VALUES (@id,@comparison,@provider,@receivedOn,@currency,@notes)
            """, db, tx))
        {
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("comparison", comparisonId);
            BindQuote(insert, quote.ProviderId!.Value, quote);
            insert.ExecuteNonQuery();
        }
        WriteLines(db, tx, id, quote, units);
        var created = ReadQuotes(db, tx, "q.id=@id", id).Single();
        tx.Commit();
        return created;
    }

    public Quote UpdateQuote(Guid id, long revision, QuoteDraft quote)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        Guid comparisonId, providerId;
        long stored;
        using (var current = new NpgsqlCommand("SELECT comparison_id,provider_id,revision FROM quotes WHERE id=@id FOR UPDATE", db, tx))
        {
            current.Parameters.AddWithValue("id", id);
            using var row = current.ExecuteReader();
            if (!row.Read()) throw new DomainNotFoundException();
            comparisonId = row.GetGuid(0);
            providerId = quote.ProviderId ?? row.GetGuid(1);
            stored = row.GetInt64(2);
        }
        RequireStatus(db, tx, comparisonId, "abierta", null);
        if (stored != revision) throw new DomainConflictException();
        var units = References(db, tx, providerId, quote);
        using (var update = new NpgsqlCommand("""
            UPDATE quotes SET provider_id=@provider,received_on=@receivedOn,currency=@currency,notes=@notes,revision=revision+1 WHERE id=@id;
            DELETE FROM quote_lines WHERE quote_id=@id;
            """, db, tx))
        {
            update.Parameters.AddWithValue("id", id);
            BindQuote(update, providerId, quote);
            update.ExecuteNonQuery();
        }
        WriteLines(db, tx, id, quote, units);
        var updated = ReadQuotes(db, tx, "q.id=@id", id).Single();
        tx.Commit();
        return updated;
    }

    private static void BindQuote(NpgsqlCommand cmd, Guid providerId, QuoteDraft quote)
    {
        cmd.Parameters.AddWithValue("provider", providerId);
        cmd.Parameters.AddWithValue("receivedOn", quote.ReceivedOn!.Value);
        cmd.Parameters.AddWithValue("currency", quote.Currency!);
        cmd.Parameters.Add(Varchar("notes", quote.Notes));
    }

    /// <summary>A missing provider or input is not found. Returns the input units, which a line without its own unit takes.</summary>
    private static Dictionary<Guid, string> References(NpgsqlConnection db, NpgsqlTransaction tx, Guid providerId, QuoteDraft quote)
    {
        using (var provider = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM providers WHERE id=@provider)", db, tx))
        {
            provider.Parameters.AddWithValue("provider", providerId);
            if (provider.ExecuteScalar() is not true) throw new DomainNotFoundException();
        }
        var inputIds = quote.Lines!.Where(line => line.InputId is not null).Select(line => line.InputId!.Value).Distinct().ToArray();
        var units = new Dictionary<Guid, string>();
        using (var inputs = new NpgsqlCommand("SELECT id,unit FROM inputs WHERE id = ANY(@ids)", db, tx))
        {
            inputs.Parameters.AddWithValue("ids", inputIds);
            using var row = inputs.ExecuteReader();
            while (row.Read()) units[row.GetGuid(0)] = row.GetString(1);
        }
        if (units.Count != inputIds.Length) throw new DomainNotFoundException();
        return units;
    }

    private static void WriteLines(NpgsqlConnection db, NpgsqlTransaction tx, Guid quoteId, QuoteDraft quote, Dictionary<Guid, string> units)
    {
        for (var i = 0; i < quote.Lines!.Count; i++)
        {
            var line = quote.Lines[i];
            using var insert = new NpgsqlCommand("""
                INSERT INTO quote_lines(id,quote_id,position,input_id,product_text,quantity,unit,unit_price)
                VALUES (@id,@quote,@position,@input,@product,@quantity,@unit,@unitPrice)
                """, db, tx);
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("quote", quoteId);
            insert.Parameters.AddWithValue("position", (short)(i + 1));
            insert.Parameters.Add(Uuid("input", line.InputId));
            insert.Parameters.Add(Varchar("product", line.ProductText));
            insert.Parameters.AddWithValue("quantity", line.Quantity);
            insert.Parameters.AddWithValue("unit", line.Unit ?? units[line.InputId!.Value]);
            insert.Parameters.AddWithValue("unitPrice", line.UnitPrice);
            insert.ExecuteNonQuery();
        }
    }

    // Quotes in the order a comparison lists them: received first, then created first.
    private static List<Quote> ReadQuotes(NpgsqlConnection db, NpgsqlTransaction tx, string filter, Guid id)
    {
        var lines = new Dictionary<Guid, List<QuoteLine>>();
        using (var cmd = new NpgsqlCommand($"""
            SELECT l.quote_id,l.id,l.position,l.input_id,l.product_text,l.quantity,l.unit,l.unit_price
            FROM quote_lines l JOIN quotes q ON q.id=l.quote_id WHERE {filter} ORDER BY l.quote_id,l.position
            """, db, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            using var row = cmd.ExecuteReader();
            while (row.Read())
            {
                if (!lines.TryGetValue(row.GetGuid(0), out var quoteLines)) lines[row.GetGuid(0)] = quoteLines = [];
                quoteLines.Add(new(row.GetGuid(1), row.GetInt16(2), Id(row, 3), Text(row, 4), row.GetDecimal(5), row.GetString(6), row.GetDecimal(7)));
            }
        }
        using var header = new NpgsqlCommand($"""
            SELECT q.id,q.comparison_id,q.provider_id,q.received_on,q.currency,q.notes,q.revision FROM quotes q WHERE {filter}
            ORDER BY q.received_on,q.created_at,q.id
            """, db, tx);
        header.Parameters.AddWithValue("id", id);
        using var r = header.ExecuteReader();
        var quotes = new List<Quote>();
        while (r.Read())
            quotes.Add(new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetFieldValue<DateOnly>(3), r.GetString(4), Text(r, 5),
                lines.TryGetValue(r.GetGuid(0), out var quoteLines) ? quoteLines : [], r.GetInt64(6)));
        return quotes;
    }
}

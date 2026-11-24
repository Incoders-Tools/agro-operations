using AgroOperations.Api.Domain;
using Npgsql;
using NpgsqlTypes;

namespace AgroOperations.Api.Persistence;

/// <summary>Providers, their contacts, the clients linked to them and their reference prices.</summary>
public sealed class PostgresProviderRepository(string connectionString) : IProviderRepository
{
    private const string ProviderColumns = "id,name,kind,tax_id,website,phone,email,notes,revision";
    private const string ContactColumns = "id,provider_id,name,role,phone,email,notes,revision";
    private const string PriceColumns = "id,provider_id,input_id,price,currency,unit,valid_on,notes,revision";

    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static NpgsqlParameter Uuid(string name, Guid? value) => new(name, NpgsqlDbType.Uuid) { Value = (object?)value ?? DBNull.Value };
    private static NpgsqlParameter Varchar(string name, string? value) => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    private static Provider MapProvider(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetString(1), r.GetString(2), Text(r, 3), Text(r, 4), Text(r, 5), Text(r, 6), Text(r, 7), r.GetInt64(8));
    private static ProviderContact MapContact(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), Text(r, 4), Text(r, 5), Text(r, 6), r.GetInt64(7));
    private static ProviderPrice MapPrice(NpgsqlDataReader r) =>
        new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetDecimal(3), r.GetString(4), r.GetString(5), r.GetFieldValue<DateOnly>(6), Text(r, 7), r.GetInt64(8));

    private NpgsqlConnection Open()
    {
        var db = new NpgsqlConnection(connectionString);
        db.Open();
        return db;
    }

    private static void Bind(NpgsqlCommand cmd, ProviderDraft provider)
    {
        cmd.Parameters.AddWithValue("name", provider.Name);
        cmd.Parameters.AddWithValue("kind", provider.Kind!);
        cmd.Parameters.Add(Varchar("taxId", provider.TaxId));
        cmd.Parameters.Add(Varchar("website", provider.Website));
        cmd.Parameters.Add(Varchar("phone", provider.Phone));
        cmd.Parameters.Add(Varchar("email", provider.Email));
        cmd.Parameters.Add(Varchar("notes", provider.Notes));
    }

    private static void Bind(NpgsqlCommand cmd, ProviderContactDraft contact)
    {
        cmd.Parameters.AddWithValue("name", contact.Name);
        cmd.Parameters.AddWithValue("role", contact.Role!);
        cmd.Parameters.Add(Varchar("phone", contact.Phone));
        cmd.Parameters.Add(Varchar("email", contact.Email));
        cmd.Parameters.Add(Varchar("notes", contact.Notes));
    }

    /// <summary>Runs a revision-guarded UPDATE ... RETURNING; no row is a stale revision when the record exists, else not found.</summary>
    private static T Revised<T>(NpgsqlConnection db, NpgsqlCommand cmd, string table, Guid id, Func<NpgsqlDataReader, T> map)
    {
        using (var row = cmd.ExecuteReader())
            if (row.Read()) return map(row);
        using var exists = new NpgsqlCommand($"SELECT EXISTS(SELECT 1 FROM {table} WHERE id=@id)", db);
        exists.Parameters.AddWithValue("id", id);
        if ((bool)exists.ExecuteScalar()!) throw new DomainConflictException();
        throw new DomainNotFoundException();
    }

    public Provider Create(ProviderDraft provider)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            INSERT INTO providers(id,name,kind,tax_id,website,phone,email,notes) VALUES (@id,@name,@kind,@taxId,@website,@phone,@email,@notes)
            RETURNING {ProviderColumns}
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        Bind(cmd, provider);
        try
        {
            using var row = cmd.ExecuteReader();
            row.Read();
            return MapProvider(row);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
    }

    public Provider Update(Guid id, long revision, ProviderDraft provider)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            UPDATE providers SET name=@name,kind=@kind,tax_id=@taxId,website=@website,phone=@phone,email=@email,notes=@notes,revision=revision+1
            WHERE id=@id AND revision=@revision RETURNING {ProviderColumns}
            """, db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        Bind(cmd, provider);
        try { return Revised(db, cmd, "providers", id, MapProvider); }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException();
        }
    }

    public IReadOnlyList<Provider> List(string? kind)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"SELECT {ProviderColumns} FROM providers WHERE @kind IS NULL OR kind=@kind ORDER BY name,id", db);
        cmd.Parameters.Add(Varchar("kind", kind));
        using var row = cmd.ExecuteReader();
        var providers = new List<Provider>();
        while (row.Read()) providers.Add(MapProvider(row));
        return providers;
    }

    public ProviderContact CreateContact(Guid providerId, ProviderContactDraft contact)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            INSERT INTO provider_contacts(id,provider_id,name,role,phone,email,notes)
            SELECT @id,id,@name,@role,@phone,@email,@notes FROM providers WHERE id=@provider
            RETURNING {ContactColumns}
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("provider", providerId);
        Bind(cmd, contact);
        using var row = cmd.ExecuteReader();
        if (!row.Read()) throw new DomainNotFoundException();
        return MapContact(row);
    }

    public ProviderContact UpdateContact(Guid id, long revision, ProviderContactDraft contact)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            UPDATE provider_contacts SET name=@name,role=@role,phone=@phone,email=@email,notes=@notes,revision=revision+1
            WHERE id=@id AND revision=@revision RETURNING {ContactColumns}
            """, db);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("revision", revision);
        Bind(cmd, contact);
        return Revised(db, cmd, "provider_contacts", id, MapContact);
    }

    public IReadOnlyList<ProviderContact> ListContacts(Guid? providerId)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"SELECT {ContactColumns} FROM provider_contacts WHERE @provider IS NULL OR provider_id=@provider ORDER BY name,id", db);
        cmd.Parameters.Add(Uuid("provider", providerId));
        using var row = cmd.ExecuteReader();
        var contacts = new List<ProviderContact>();
        while (row.Read()) contacts.Add(MapContact(row));
        return contacts;
    }

    public ClientProvider Link(Guid clientId, Guid providerId)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand("""
            INSERT INTO client_providers(id,client_id,provider_id)
            SELECT @id,c.id,p.id FROM clients c, providers p WHERE c.id=@client AND p.id=@provider
            RETURNING id,client_id,provider_id
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("client", clientId);
        cmd.Parameters.AddWithValue("provider", providerId);
        try
        {
            using var row = cmd.ExecuteReader();
            if (!row.Read()) throw new DomainNotFoundException();
            return new(row.GetGuid(0), row.GetGuid(1), row.GetGuid(2));
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DomainDuplicateException("Duplicate link");
        }
    }

    public void Unlink(Guid id)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand("DELETE FROM client_providers WHERE id=@id", db);
        cmd.Parameters.AddWithValue("id", id);
        if (cmd.ExecuteNonQuery() == 0) throw new DomainNotFoundException();
    }

    public IReadOnlyList<ClientProvider> ListLinks(Guid? clientId, Guid? providerId)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand("""
            SELECT l.id,l.client_id,l.provider_id FROM client_providers l JOIN clients c ON c.id=l.client_id JOIN providers p ON p.id=l.provider_id
            WHERE (@client IS NULL OR l.client_id=@client) AND (@provider IS NULL OR l.provider_id=@provider)
            ORDER BY c.name,c.id,p.name,p.id
            """, db);
        cmd.Parameters.Add(Uuid("client", clientId));
        cmd.Parameters.Add(Uuid("provider", providerId));
        using var row = cmd.ExecuteReader();
        var links = new List<ClientProvider>();
        while (row.Read()) links.Add(new(row.GetGuid(0), row.GetGuid(1), row.GetGuid(2)));
        return links;
    }

    public ProviderPrice CreatePrice(ProviderPriceDraft price)
    {
        using var db = Open();
        // The unit defaults to the input's own unit, read in the same statement.
        using var cmd = new NpgsqlCommand($"""
            INSERT INTO provider_prices(id,provider_id,input_id,price,currency,unit,valid_on,notes)
            SELECT @id,p.id,i.id,@price,@currency,coalesce(@unit,i.unit),@validOn,@notes FROM providers p, inputs i WHERE p.id=@provider AND i.id=@input
            RETURNING {PriceColumns}
            """, db);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        BindPrice(cmd, price.ProviderId, price.InputId, price);
        using var row = cmd.ExecuteReader();
        if (!row.Read()) throw new DomainNotFoundException();
        return MapPrice(row);
    }

    public ProviderPrice UpdatePrice(Guid id, long revision, ProviderPriceDraft price)
    {
        using var db = Open();
        using var tx = db.BeginTransaction();
        Guid providerId, inputId;
        using (var current = new NpgsqlCommand("SELECT revision,provider_id,input_id FROM provider_prices WHERE id=@id FOR UPDATE", db, tx))
        {
            current.Parameters.AddWithValue("id", id);
            using var row = current.ExecuteReader();
            if (!row.Read()) throw new DomainNotFoundException();
            if (row.GetInt64(0) != revision) throw new DomainConflictException();
            providerId = price.ProviderId ?? row.GetGuid(1);
            inputId = price.InputId ?? row.GetGuid(2);
        }
        using var update = new NpgsqlCommand($"""
            UPDATE provider_prices pp SET provider_id=p.id,input_id=i.id,price=@price,currency=@currency,unit=coalesce(@unit,i.unit),valid_on=@validOn,
              notes=@notes,revision=pp.revision+1
            FROM providers p, inputs i WHERE pp.id=@id AND p.id=@provider AND i.id=@input
            RETURNING {string.Join(",", PriceColumns.Split(',').Select(column => "pp." + column))}
            """, db, tx);
        update.Parameters.AddWithValue("id", id);
        BindPrice(update, providerId, inputId, price);
        ProviderPrice updated;
        using (var row = update.ExecuteReader())
        {
            if (!row.Read()) throw new DomainNotFoundException();
            updated = MapPrice(row);
        }
        tx.Commit();
        return updated;
    }

    private static void BindPrice(NpgsqlCommand cmd, Guid? providerId, Guid? inputId, ProviderPriceDraft price)
    {
        cmd.Parameters.Add(Uuid("provider", providerId));
        cmd.Parameters.Add(Uuid("input", inputId));
        cmd.Parameters.AddWithValue("price", price.Price);
        cmd.Parameters.AddWithValue("currency", price.Currency!);
        cmd.Parameters.Add(Varchar("unit", price.Unit));
        cmd.Parameters.AddWithValue("validOn", price.ValidOn!.Value);
        cmd.Parameters.Add(Varchar("notes", price.Notes));
    }

    public IReadOnlyList<ProviderPrice> ListPrices(Guid? providerId, Guid? inputId)
    {
        using var db = Open();
        using var cmd = new NpgsqlCommand($"""
            SELECT {PriceColumns} FROM provider_prices WHERE (@provider IS NULL OR provider_id=@provider) AND (@input IS NULL OR input_id=@input)
            ORDER BY valid_on DESC,id
            """, db);
        cmd.Parameters.Add(Uuid("provider", providerId));
        cmd.Parameters.Add(Uuid("input", inputId));
        using var row = cmd.ExecuteReader();
        var prices = new List<ProviderPrice>();
        while (row.Read()) prices.Add(MapPrice(row));
        return prices;
    }
}

using AgroOperations.Api.Domain;
using AgroOperations.Api.Persistence;
using Npgsql;

var baseConnection = Environment.GetEnvironmentVariable("AGRO_TEST_DATABASE");
if (string.IsNullOrWhiteSpace(baseConnection))
{
    Console.Error.WriteLine("AGRO_TEST_DATABASE is required for isolated PostgreSQL integration tests");
    return 2;
}

var schema = "test_" + Guid.NewGuid().ToString("N");
var builder = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schema };
using var admin = new NpgsqlConnection(baseConnection);
admin.Open();
using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) create.ExecuteNonQuery();
try
{
    using (var connection = new NpgsqlConnection(builder.ConnectionString))
    {
        connection.Open();
        var migration = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/AgroOperations.Api/Persistence/Migrations/001_operations.sql"));
        using var command = new NpgsqlCommand(File.ReadAllText(migration), connection);
        command.ExecuteNonQuery();
        var cropsMigration = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/AgroOperations.Api/Persistence/Migrations/003_crops.sql"));
        using var cropsCommand = new NpgsqlCommand(File.ReadAllText(cropsMigration), connection);
        cropsCommand.ExecuteNonQuery();
    }
    var service = new OperationsService(new PostgresOperationsRepository(builder.ConnectionString));
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
    }
    static void Reject<T>(Action action, string label) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}: {label}");
    }
    Reject<DomainValidationException>(() => service.CreateClient(" \t"), "blank client");
    Check(service.GetClient(Guid.NewGuid()) is null, "unknown client");
    var client = service.CreateClient("Client");
    Reject<DomainValidationException>(() => service.CreateField(client.Id, " "), "blank field");
    Reject<DomainNotFoundException>(() => service.CreateField(Guid.NewGuid(), "Field"), "missing client");
    var field = service.CreateField(client.Id, "Field");
    Reject<DomainValidationException>(() => service.CreatePlot(field.Id, " ", 1), "blank plot");
    Reject<DomainNotFoundException>(() => service.CreatePlot(Guid.NewGuid(), "Plot", 1), "missing field");
    foreach (var area in new[] { 0m, -1m, 0.00001m, 100000000m })
        Reject<DomainValidationException>(() => service.CreatePlot(field.Id, "Plot", area), "invalid area");
    var plot = service.CreatePlot(field.Id, "Plot", 1.2500m);
    Check(client.Id != Guid.Empty && field.Id != Guid.Empty && plot.Id != Guid.Empty &&
          field.ClientId == client.Id && plot.FieldId == field.Id && plot.AreaHectares == 1.2500m &&
          client.Revision == 1 && field.Revision == 1 && plot.Revision == 1, "initial hierarchy");
    Reject<DomainValidationException>(() => service.UpdateClient(client.Id, client.Revision, " "), "blank client update");
    Reject<DomainValidationException>(() => service.UpdateField(field.Id, field.Revision, " "), "blank field update");
    Reject<DomainValidationException>(() => service.UpdatePlot(plot.Id, plot.Revision, " ", 2), "blank plot update");
    foreach (var area in new[] { 0m, -1m, 0.00001m, 100000000m })
        Reject<DomainValidationException>(() => service.UpdatePlot(plot.Id, plot.Revision, "Changed", area), "invalid plot update");
    Check(service.GetClient(client.Id) == client && service.GetField(field.Id) == field && service.GetPlot(plot.Id) == plot, "rejected values unchanged");
    var updatedClient = service.UpdateClient(client.Id, client.Revision, "New client");
    var updatedField = service.UpdateField(field.Id, field.Revision, "New field");
    var updatedPlot = service.UpdatePlot(plot.Id, plot.Revision, "New plot", 2m);
    Reject<DomainConflictException>(() => service.UpdateClient(client.Id, client.Revision, "Stale"), "client conflict");
    Reject<DomainConflictException>(() => service.UpdateField(field.Id, field.Revision, "Stale"), "field conflict");
    Reject<DomainConflictException>(() => service.UpdatePlot(plot.Id, plot.Revision, "Stale", 3m), "plot conflict");
    Check(service.GetClient(client.Id) == updatedClient && service.GetField(field.Id) == updatedField &&
          service.GetPlot(plot.Id) == updatedPlot && updatedClient.Revision == 2 &&
          updatedField.Revision == 2 && updatedPlot.Revision == 2, "stale writes unchanged");
    Reject<DomainNotFoundException>(() => service.UpdatePlot(Guid.NewGuid(), 1, "Missing", 1m), "missing update");

    string Snapshot()
    {
        using var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand("""
            SELECT concat_ws('|',
              (SELECT coalesce(string_agg(to_jsonb(c)::text, ',' ORDER BY c.id), '') FROM clients c),
              (SELECT coalesce(string_agg(to_jsonb(f)::text, ',' ORDER BY f.id), '') FROM fields f),
              (SELECT coalesce(string_agg(to_jsonb(p)::text, ',' ORDER BY p.id), '') FROM plots p),
              (SELECT coalesce(string_agg(to_jsonb(c)::text, ',' ORDER BY c.id), '') FROM crops c))
            """, connection);
        return (string)command.ExecuteScalar()!;
    }
    var before = Snapshot();
    Reject<DomainNotFoundException>(() => service.CreatePlot(Guid.NewGuid(), "Orphan", 1m), "orphan plot");
    Check(Snapshot() == before, "orphan creation changed stored rows");
    Reject<DomainValidationException>(() => service.UpdatePlot(plot.Id, updatedPlot.Revision, "Plot", 0.00001m), "invalid precision");
    Check(Snapshot() == before, "invalid precision changed stored rows");

    using var gate = new Barrier(3);
    Task<Plot?> Compete(string name) => Task.Run(() =>
    {
        gate.SignalAndWait();
        try { return service.UpdatePlot(plot.Id, updatedPlot.Revision, name, 3m); }
        catch (DomainConflictException error)
        {
            Check(error.Message == "Stale revision", "wrong conflict message");
            return null;
        }
    });
    var first = Compete("First");
    var second = Compete("Second");
    gate.SignalAndWait();
    await Task.WhenAll(first, second);
    Check((first.Result is null) != (second.Result is null), "expected exactly one winner");
    var winner = first.Result ?? second.Result!;
    Check(winner.Revision == updatedPlot.Revision + 1 && service.GetPlot(plot.Id) == winner,
          "stale contender overwrote winner");
    var crops = new CropCatalog(new PostgresCropRepository(builder.ConnectionString));
    Reject<DomainValidationException>(() => crops.Create(" "), "blank crop");
    var crop = crops.Create("Trigo");
    Check(crop.Revision == 1 && crops.List().Any(item => item == crop), "crop creation/listing");
    var cropBefore = Snapshot();
    Reject<DomainValidationException>(() => crops.Create("  TRIGO  "), "duplicate crop");
    Reject<DomainValidationException>(() => crops.Create(" "), "blank crop");
    Check(Snapshot() == cropBefore, "rejected crop creates changed stored rows");
    var changedCrop = crops.Update(crop.Id, crop.Revision, "Trigo candeal");
    cropBefore = Snapshot();
    Reject<DomainConflictException>(() => crops.Update(crop.Id, crop.Revision, "Stale crop"), "crop conflict");
    Reject<DomainNotFoundException>(() => crops.Update(Guid.NewGuid(), 1, "Wheat"), "missing crop");
    Check(Snapshot() == cropBefore && crops.List().Single(item => item.Id == crop.Id) == changedCrop,
          "crop rejected writes changed stored rows");
    Console.WriteLine("PASS: isolated PostgreSQL service boundary, validation, hierarchy, crops and concurrent revisions");
    return 0;
}
finally
{
    using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
    drop.ExecuteNonQuery();
}

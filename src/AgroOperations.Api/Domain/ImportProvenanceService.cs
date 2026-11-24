namespace AgroOperations.Api.Domain;

public sealed record ImportRecord(Guid Id, string EntityType, Guid EntityId, string SourceReference, bool Verified, string RawPayloadJson);

public interface IImportRecordRepository
{
    ImportRecord? Find(string entityType, Guid entityId);
    ImportRecord Create(string entityType, Guid entityId, string sourceReference, string rawPayloadJson);
    IReadOnlyList<ImportRecord> List();
}

/// <summary>
/// Traces a client/field/plot/contractor/input back to the exact bounded import row that created it.
/// A record here never proves the underlying values are agronomically verified.
/// </summary>
public sealed class ImportProvenanceService(IImportRecordRepository repository)
{
    private static readonly string[] EntityTypes = ["client", "field", "plot", "contractor", "input"];

    public ImportRecord? Find(string entityType, Guid entityId) => repository.Find(entityType, entityId);

    public ImportRecord RecordOnce(string entityType, Guid entityId, string sourceReference, string rawPayloadJson)
    {
        if (Array.IndexOf(EntityTypes, entityType) < 0)
            throw new DomainValidationException("Entity type must be client, field, plot, contractor or input");
        var existing = repository.Find(entityType, entityId);
        if (existing is not null) return existing;
        return repository.Create(entityType, entityId,
            string.IsNullOrWhiteSpace(sourceReference) ? throw new DomainValidationException("Source reference is required") : sourceReference.Trim(),
            rawPayloadJson);
    }

    public IReadOnlyList<ImportRecord> List() => repository.List();
}

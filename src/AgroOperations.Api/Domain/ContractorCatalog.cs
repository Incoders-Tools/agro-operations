namespace AgroOperations.Api.Domain;

public sealed record Contractor(Guid Id, string Name, string? TaxId, string? Phone, string? Email, string? Notes, bool OwnEquipment, long Revision);
public sealed record ContractorDraft(string Name, string? TaxId, string? Phone, string? Email, string? Notes, bool OwnEquipment);

public interface IContractorRepository
{
    Contractor Create(ContractorDraft contractor);
    Contractor Update(Guid id, long revision, ContractorDraft contractor);
    IReadOnlyList<Contractor> List();
}

/// <summary>A contractor performs labors on a work order; "own equipment" marks the client's own machinery.</summary>
public sealed class ContractorCatalog(IContractorRepository repository)
{
    private static ContractorDraft Validated(ContractorDraft draft) => new(
        CatalogText.Required(draft.Name, "Name"), TaxId(draft.TaxId), CatalogText.Optional(draft.Phone),
        Email(draft.Email), CatalogText.Optional(draft.Notes), draft.OwnEquipment);

    // The CUIT is stored as its 11 digits; hyphens and spaces are the only accepted separators.
    private static string? TaxId(string? value)
    {
        if (CatalogText.Optional(value) is not { } taxId) return null;
        var digits = taxId.Replace("-", "").Replace(" ", "");
        return digits.Length == 11 && digits.All(char.IsAsciiDigit)
            ? digits : throw new DomainValidationException("CUIT must have 11 digits");
    }

    private static string? Email(string? value)
    {
        if (CatalogText.Optional(value) is not { } email) return null;
        var at = email.IndexOf('@');
        return at > 0 && at < email.Length - 1 && email.IndexOf('@', at + 1) < 0
            ? email : throw new DomainValidationException("Email must contain a single @ with text on both sides");
    }

    public Contractor Create(ContractorDraft contractor) => repository.Create(Validated(contractor));
    public Contractor Update(Guid id, long revision, ContractorDraft contractor) => repository.Update(id, revision, Validated(contractor));
    public IReadOnlyList<Contractor> List() => repository.List();
}

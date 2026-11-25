namespace AgroOperations.Api.Domain;

public sealed record Staff(Guid Id, string FullName, string? Title, string Role, string? LicenseNumber, string? Email, string? Phone, bool Active,
    string? Notes, long Revision);
/// <summary>A draft without an active flag is active.</summary>
public sealed record StaffDraft(string? FullName, string? Title, string? Role, string? LicenseNumber, string? Email, string? Phone, bool Active = true,
    string? Notes = null);

public interface IStaffRepository
{
    Staff Create(StaffDraft staff);
    Staff Update(Guid id, long revision, StaffDraft staff);
    Staff? Get(Guid id);
    /// <summary>Both filters are optional and combine.</summary>
    IReadOnlyList<Staff> List(string? role, bool? active);
}

/// <summary>
/// Company personnel, such as the engineers responsible for work orders. Separate from login accounts, which can link to a
/// staff member later. Staff is never deleted: a member who leaves is deactivated.
/// </summary>
public sealed class StaffCatalog(IStaffRepository repository)
{
    public const string Engineer = "ingeniero";
    private static readonly string[] Roles = [Engineer, "administrativo", "otro"];

    private static string Role(string? value)
    {
        var role = CatalogText.Optional(value)?.ToLowerInvariant();
        return role is not null && Array.IndexOf(Roles, role) >= 0
            ? role : throw new DomainValidationException($"Role must be one of: {string.Join(", ", Roles)}");
    }

    private static StaffDraft Validated(StaffDraft draft) => new(
        CatalogText.Limited(CatalogText.Required(draft.FullName, "Full name"), 200, "Full name"), CatalogText.Limited(draft.Title, 100, "Title"),
        Role(draft.Role), CatalogText.Limited(draft.LicenseNumber, 50, "License number"), CatalogText.Email(draft.Email), CatalogText.Optional(draft.Phone),
        draft.Active, CatalogText.Optional(draft.Notes));

    public Staff Create(StaffDraft staff) => repository.Create(Validated(staff));
    public Staff Update(Guid id, long revision, StaffDraft staff) => repository.Update(id, revision, Validated(staff));
    public Staff? Get(Guid id) => repository.Get(id);
    /// <summary>Without filters, every staff member by name; an unknown role is rejected rather than matching nothing.</summary>
    public IReadOnlyList<Staff> List(string? role = null, bool? active = null) =>
        repository.List(CatalogText.Optional(role) is null ? null : Role(role), active);
}

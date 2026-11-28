namespace AgroOperations.Api.Domain;

/// <summary>Revision 0 means nothing is stored yet.</summary>
public sealed record UserPreferences(string Theme, long Revision);

public interface IUserPreferenceRepository
{
    UserPreferences? Get(string identityEmail);
    /// <summary>Fails with a stale revision when the identity already has preferences.</summary>
    UserPreferences Create(string identityEmail, string theme);
    /// <summary>Fails with a stale revision when the revision differs or the identity has no preferences.</summary>
    UserPreferences Update(string identityEmail, long revision, string theme);
}

/// <summary>
/// Preferences of the logged-in identity, keyed by its email compared case-insensitively. Light is the default theme and is
/// returned without writing when the identity has stored nothing.
/// </summary>
public sealed class UserPreferenceService(IUserPreferenceRepository repository)
{
    public const string Light = "light";
    private static readonly UserPreferences Defaults = new(Light, 0);

    private static string Identity(string? email) => CatalogText.Required(email, "Identity email").ToLowerInvariant();

    private static string Theme(string? value)
    {
        var theme = CatalogText.Optional(value)?.ToLowerInvariant();
        return theme is Light or "dark" ? theme : throw new DomainValidationException("Theme must be light or dark");
    }

    public UserPreferences Get(string? identityEmail) => repository.Get(Identity(identityEmail)) ?? Defaults;

    /// <summary>Revision 0 creates the preferences; any other revision updates them.</summary>
    public UserPreferences Save(string? identityEmail, long revision, string? theme)
    {
        var identity = Identity(identityEmail);
        var validTheme = Theme(theme);
        return revision == 0 ? repository.Create(identity, validTheme) : repository.Update(identity, revision, validTheme);
    }
}

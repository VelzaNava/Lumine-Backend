namespace Lumine.Backend.Configuration;

/// <summary>
/// Decides which accounts are administrators, from the <c>AdminEmails</c> list
/// in configuration. Comparison is case-insensitive on both sides.
/// </summary>
public sealed class AdminDirectory
{
    private readonly HashSet<string> _adminEmails;

    public AdminDirectory(IConfiguration configuration)
    {
        var configured = configuration.GetSection("AdminEmails").Get<string[]>() ?? [];
        _adminEmails = configured
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(Normalize)
            .ToHashSet(StringComparer.Ordinal);
    }

    public bool IsAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(email) && _adminEmails.Contains(Normalize(email));

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

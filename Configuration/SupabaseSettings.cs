using System.ComponentModel.DataAnnotations;

namespace Lumine.Backend.Configuration;

/// <summary>
/// Connection settings for the Supabase project, bound from the <c>Supabase</c>
/// configuration section and validated at startup.
/// </summary>
public sealed class SupabaseSettings
{
    public const string SectionName = "Supabase";

    /// <summary>Project URL, e.g. <c>https://your-project.supabase.co</c>.</summary>
    [Required, Url]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The <c>service_role</c> key. Server-side only: it bypasses row-level
    /// security, so it must never ship inside the mobile app.
    /// </summary>
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>The public <c>anon</c> key, used for the public auth endpoints.</summary>
    [Required]
    public string AnonKey { get; set; } = string.Empty;

    /// <summary>Project URL without a trailing slash.</summary>
    public string BaseUrl => Url.TrimEnd('/');
}

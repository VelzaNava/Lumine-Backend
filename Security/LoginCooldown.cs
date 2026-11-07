using Lumine.Backend.Errors;
using Microsoft.Extensions.Caching.Memory;

namespace Lumine.Backend.Security;

/// <summary>
/// After a wrong password, that email can't sign in again for 10 seconds, which
/// makes guessing passwords impractically slow. Kept in memory: the API runs as a
/// single instance, and a restart only clears waits that would have ended anyway.
/// </summary>
public sealed class LoginCooldown(IMemoryCache cache, TimeProvider clock)
{
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    /// <summary>Throws 429 with the time left if this email is still waiting after a wrong password.</summary>
    public void EnsureAllowed(string email)
    {
        if (!cache.TryGetValue(Key(email), out DateTimeOffset until))
        {
            return;
        }

        var left = until - clock.GetUtcNow();
        if (left > TimeSpan.Zero)
        {
            throw ApiException.TooManyRequests(
                $"Please wait {Seconds(left)} seconds before trying again.", left);
        }
    }

    /// <summary>Starts the wait after a wrong password and returns the error to send.</summary>
    public ApiException WrongPassword(string email)
    {
        cache.Set(Key(email), clock.GetUtcNow() + Wait, Wait);
        return ApiException.Unauthorized(
            $"Wrong email or password. You can try again in {Seconds(Wait)} seconds.", Wait);
    }

    private static string Key(string email) => "login-cooldown:" + email;

    private static int Seconds(TimeSpan time) => (int)Math.Ceiling(time.TotalSeconds);
}

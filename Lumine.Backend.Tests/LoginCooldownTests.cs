using Lumine.Backend.Errors;
using Lumine.Backend.Security;
using Microsoft.Extensions.Caching.Memory;

namespace Lumine.Backend.Tests;

/// <summary>The 10-second wait after a wrong password.</summary>
public sealed class LoginCooldownTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly ManualClock _clock = new();
    private readonly LoginCooldown _cooldown;

    public LoginCooldownTests() =>
        _cooldown = new LoginCooldown(new MemoryCache(new MemoryCacheOptions()), _clock);

    [Fact]
    public void Signing_in_is_allowed_before_any_wrong_password() =>
        _cooldown.EnsureAllowed("user@lumine.test");

    [Fact]
    public void A_wrong_password_answers_401_and_says_when_to_retry()
    {
        var error = _cooldown.WrongPassword("user@lumine.test");

        Assert.Equal(401, error.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(10), error.RetryAfter);
    }

    [Fact]
    public void After_a_wrong_password_that_email_waits_with_the_time_left()
    {
        _cooldown.WrongPassword("user@lumine.test");
        _clock.Now += TimeSpan.FromSeconds(3);

        var error = Assert.Throws<ApiException>(() => _cooldown.EnsureAllowed("user@lumine.test"));

        Assert.Equal(429, error.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), error.RetryAfter);
        Assert.Contains("7 seconds", error.Message);
    }

    [Fact]
    public void Other_emails_are_not_affected()
    {
        _cooldown.WrongPassword("user@lumine.test");

        _cooldown.EnsureAllowed("other@lumine.test");
    }

    [Fact]
    public void The_wait_ends_after_10_seconds()
    {
        _cooldown.WrongPassword("user@lumine.test");
        _clock.Now += TimeSpan.FromSeconds(10);

        _cooldown.EnsureAllowed("user@lumine.test");
    }
}

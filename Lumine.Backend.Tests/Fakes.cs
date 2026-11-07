using Lumine.Backend.Contracts;
using Lumine.Backend.Errors;
using Lumine.Backend.Security;
using Lumine.Backend.Services;
using Microsoft.AspNetCore.Http;

namespace Lumine.Backend.Tests;

public sealed record TestAccount(Guid Id, string Email, string Token);

public static class TestAccounts
{
    public static readonly TestAccount Admin =
        new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), "admin@lumine.test", "admin-token");

    public static readonly TestAccount User =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"), "user@lumine.test", "user-token");

    public static readonly TestAccount OtherUser =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"), "other@lumine.test", "other-token");

    public static IReadOnlyList<TestAccount> All { get; } = [Admin, User, OtherUser];
}

/// <summary>Accepts exactly the tokens of <see cref="TestAccounts"/>.</summary>
internal sealed class FakeTokenValidator : IAccessTokenValidator
{
    public Task<AuthenticatedUser?> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        var account = TestAccounts.All.FirstOrDefault(candidate => candidate.Token == accessToken);
        return Task.FromResult(account is null ? null : new AuthenticatedUser(account.Id.ToString(), account.Email));
    }
}

internal sealed class FakeAuthService : IAuthService
{
    public const string CorrectPassword = "correct-password";

    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        request.Password == CorrectPassword
            ? Task.FromResult(Response(TestAccounts.User))
            : throw ApiException.Unauthorized(
                "Wrong email or password. You can try again in 10 seconds.", TimeSpan.FromSeconds(10));

    public Task<AuthResponse> RefreshAsync(RefreshSessionRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Response(TestAccounts.User));

    public Task<CodeSentResponse> SendSignUpCodeAsync(OtpRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new CodeSentResponse("Verification code sent.", request.Email));

    public Task<AuthResponse> VerifyAndRegisterAsync(VerifyAndRegisterRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Response(TestAccounts.User));

    public Task<CodeSentResponse> SendPasswordResetCodeAsync(ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new CodeSentResponse("Reset code sent.", request.Email));

    public Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Response(TestAccounts.User));

    private static AuthResponse Response(TestAccount account) => new()
    {
        AccessToken = account.Token,
        RefreshToken = "refresh-token",
        Email = account.Email,
        UserId = account.Id.ToString(),
    };
}

internal sealed class FakeJewelryService : IJewelryService
{
    public static readonly JewelryDto Ring = new()
    {
        Id = Guid.Parse("99a38e9f-5573-4c26-a453-98183f78c539"),
        CreatedAt = "2026-05-22T03:21:12+00:00",
        Name = "Two Toned Ring",
        Type = "ring",
        Material = "Gold",
        Price = 25303m,
        IsAvailable = true,
        IsArEnabled = true,
    };

    public Task<IReadOnlyList<JewelryDto>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<JewelryDto>>([Ring]);

    public Task<JewelryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        id == Ring.Id ? Task.FromResult(Ring) : throw ApiException.NotFound("Jewelry not found.");

    public Task<JewelryDto> CreateAsync(JewelryRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new JewelryDto { Id = Guid.NewGuid(), Name = request.Name, Type = request.Type, Price = request.Price });

    public Task<JewelryDto> UpdateAsync(Guid id, JewelryRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new JewelryDto { Id = id, Name = request.Name, Type = request.Type, Price = request.Price });

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<ImageUploadResponse> UploadImageAsync(IFormFile? file, CancellationToken cancellationToken) =>
        Task.FromResult(new ImageUploadResponse("https://example.supabase.co/storage/v1/object/public/jewelry/test.jpg"));
}

internal sealed class FakeProfileService : IProfileService
{
    public Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(new UserProfileResponse { UserId = userId.ToString(), Username = "tester" });

    public Task<UserProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new UserProfileResponse { UserId = userId.ToString(), Username = request.Username ?? string.Empty });

    public Task<AvatarResponse> UploadAvatarAsync(Guid userId, IFormFile? file, CancellationToken cancellationToken) =>
        Task.FromResult(new AvatarResponse($"https://example.supabase.co/storage/v1/object/public/avatars/{userId}.jpg"));

    public Task<IReadOnlyList<string>> GetFavoriteIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([FakeJewelryService.Ring.Id.ToString()]);

    public Task AddFavoriteAsync(Guid userId, Guid jewelryId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveFavoriteAsync(Guid userId, Guid jewelryId, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeAdminUserService : IAdminUserService
{
    public Task<IReadOnlyList<AdminUserInfo>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AdminUserInfo>>(
            TestAccounts.All.Select(account => AdminUserInfo.From(account.Id.ToString(), account.Email, profile: null)).ToList());

    public Task<AdminUserInfo> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(AdminUserInfo.From(userId.ToString(), "user@lumine.test", profile: null));

    public Task DeleteAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Records who submitted the last rating, so tests can check it came from the token.</summary>
public sealed class FakeEvaluationService : IEvaluationService
{
    public Guid? LastSubmittedBy { get; private set; }

    public Task SubmitAsync(Guid userId, SubmitEvaluationRequest request, CancellationToken cancellationToken)
    {
        LastSubmittedBy = userId;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EvaluationSummary>> GetSummaryAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EvaluationSummary>>([]);

    public Task<IReadOnlyList<EvaluationDto>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EvaluationDto>>([]);
}

/// <summary>Records which accounts were deleted, and with which email, so tests can check both came from the token.</summary>
public sealed class FakeAccountService : IAccountService
{
    private readonly List<(Guid UserId, string Email)> _deleted = [];

    public IReadOnlyList<(Guid UserId, string Email)> Deleted
    {
        get { lock (_deleted) return _deleted.ToList(); }
    }

    public Task DeleteOwnAccountAsync(Guid userId, string email, string password, CancellationToken cancellationToken)
    {
        lock (_deleted) _deleted.Add((userId, email));
        return Task.CompletedTask;
    }
}

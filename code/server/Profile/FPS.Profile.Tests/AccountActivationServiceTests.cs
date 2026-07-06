using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.Extensions.Options;
using Moq;

namespace FPS.Profile.Tests;

// AUTH009 (#738) — pending-account activation gate: issue a hashed one-time challenge for an inactive
// user (blocked by the shared deactivation gate), activate on the correct token+challenge before expiry
// within the attempt limit, and fail closed on expired/wrong/reused/superseded/revoked/cross-tenant.
public sealed class AccountActivationServiceTests
{
    private const string Tenant = "tenant-1";
    private const string User = "user-1";
    private const string Email = "jan.novak@greenlogistics.example";
    private const string Token = "one-time-activation-token";

    private readonly InMemoryAccountActivationRepository repo = new();
    private readonly InMemoryProfileRepository profiles = new();
    private readonly InMemoryDeactivatedUserStore deactivated = new();
    private readonly Mock<IVerificationTokenGenerator> tokenGen = new();
    private readonly Mock<IAccountActivationSender> sender = new();
    private readonly Mock<IAccountActivationAuditSink> audit = new();
    private readonly MutableClock clock = new(DateTimeOffset.Parse("2026-07-06T12:00:00Z"));
    private readonly AccountActivationService service;

    public AccountActivationServiceTests()
    {
        tokenGen.Setup(g => g.Generate()).Returns(Token);
        service = Build(clock);
    }

    private AccountActivationService Build(TimeProvider timeProvider) => new(
        repo, profiles, deactivated, tokenGen.Object, sender.Object, audit.Object, timeProvider,
        Options.Create(new AccountActivationOptions { Ttl = TimeSpan.FromHours(72), MaxAttempts = 3 }));

    private async Task SeedProfile(
        string tenant = Tenant, string user = User, ProfileStatus status = ProfileStatus.Inactive, string? email = Email) =>
        await profiles.SaveAsync(new UserProfile
        {
            TenantId = tenant, UserId = user, Status = status, NotificationAddress = email, FactSource = "admin-entry",
        });

    [Fact]
    public async Task Issue_CreatesPendingChallenge_HashesToken_AndKeepsUserBlocked()
    {
        await SeedProfile();

        var result = await service.IssueAsync(Tenant, User, default);

        Assert.True(result.Issued);
        Assert.False(string.IsNullOrWhiteSpace(result.ChallengeId));
        var record = await repo.GetAsync(Tenant, User, default);
        Assert.NotNull(record);
        Assert.Equal(AccountActivationState.Pending, record!.State);
        Assert.Equal(Email, record.IdentityEmail);
        // Only the hash is stored — never the plaintext token, anywhere on the record.
        Assert.NotEqual(Token, record.TokenHash);
        Assert.DoesNotContain(Token, new[] { record.TokenHash, record.ChallengeId, record.IdentityEmail });
        // A pending user is blocked by the shared deactivation gate until activation.
        Assert.True(deactivated.IsDeactivated(Tenant, User));
        sender.Verify(s => s.SendAsync(Tenant, User, Email, result.ChallengeId!, Token, It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.RequestedAsync(Tenant, User, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Issue_Rejects_WhenUserNotFound()
    {
        var result = await service.IssueAsync(Tenant, "missing", default);
        Assert.False(result.Issued);
        Assert.Equal("user_not_found", result.RejectionReason);
    }

    [Fact]
    public async Task Issue_Rejects_WhenAlreadyActive()
    {
        await SeedProfile(status: ProfileStatus.Active);
        var result = await service.IssueAsync(Tenant, User, default);
        Assert.False(result.Issued);
        Assert.Equal("already_active", result.RejectionReason);
    }

    [Fact]
    public async Task Issue_Rejects_WhenEmailInvalid()
    {
        await SeedProfile(email: "not-an-email");
        var result = await service.IssueAsync(Tenant, User, default);
        Assert.False(result.Issued);
        Assert.Equal("email_invalid", result.RejectionReason);
    }

    [Fact]
    public async Task Confirm_ActivatesAccount_ReopensGate_AndSetsProfileActive()
    {
        await SeedProfile();
        var issued = await service.IssueAsync(Tenant, User, default);

        var outcome = await service.ConfirmAsync(issued.ChallengeId!, Token, default);

        Assert.True(outcome.Activated);
        var record = await repo.GetAsync(Tenant, User, default);
        Assert.Equal(AccountActivationState.Activated, record!.State);
        var profile = await profiles.GetAsync(Tenant, User, default);
        Assert.True(profile!.IsActive);
        Assert.False(deactivated.IsDeactivated(Tenant, User)); // gate reopened
        audit.Verify(a => a.SucceededAsync(Tenant, User, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_Rejects_UnknownChallenge()
    {
        var outcome = await service.ConfirmAsync("no-such-challenge", Token, default);
        Assert.False(outcome.Activated);
        Assert.Equal("invalid_challenge", outcome.RejectionReason);
    }

    [Fact]
    public async Task Confirm_Rejects_Expired_AndUserStaysBlocked()
    {
        await SeedProfile();
        var issued = await service.IssueAsync(Tenant, User, default);
        clock.Now = clock.Now.AddHours(73); // past the 72h TTL

        var outcome = await service.ConfirmAsync(issued.ChallengeId!, Token, default);

        Assert.False(outcome.Activated);
        Assert.Equal("expired", outcome.RejectionReason);
        Assert.True(deactivated.IsDeactivated(Tenant, User));
        Assert.False((await profiles.GetAsync(Tenant, User, default))!.IsActive);
    }

    [Fact]
    public async Task Confirm_Rejects_WrongToken_AndCountsAttempt()
    {
        await SeedProfile();
        var issued = await service.IssueAsync(Tenant, User, default);

        var outcome = await service.ConfirmAsync(issued.ChallengeId!, "wrong-token", default);

        Assert.False(outcome.Activated);
        Assert.Equal("invalid_token", outcome.RejectionReason);
        Assert.Equal(1, (await repo.GetAsync(Tenant, User, default))!.AttemptCount);
        Assert.True(deactivated.IsDeactivated(Tenant, User));
    }

    [Fact]
    public async Task Confirm_Rejects_AfterTooManyAttempts()
    {
        await SeedProfile();
        var issued = await service.IssueAsync(Tenant, User, default);
        for (var i = 0; i < 3; i++)
            await service.ConfirmAsync(issued.ChallengeId!, "wrong", default);

        // The 4th attempt (even with the RIGHT token) is refused: the attempt limit is exhausted.
        var outcome = await service.ConfirmAsync(issued.ChallengeId!, Token, default);
        Assert.False(outcome.Activated);
        Assert.Equal("too_many_attempts", outcome.RejectionReason);
    }

    [Fact]
    public async Task Confirm_Rejects_Reuse_AfterSuccess()
    {
        await SeedProfile();
        var issued = await service.IssueAsync(Tenant, User, default);
        await service.ConfirmAsync(issued.ChallengeId!, Token, default);

        var reuse = await service.ConfirmAsync(issued.ChallengeId!, Token, default);
        Assert.False(reuse.Activated);
        Assert.Equal("invalid_challenge", reuse.RejectionReason); // no longer Pending
    }

    [Fact]
    public async Task Issue_Supersedes_PriorChallenge()
    {
        await SeedProfile();
        var first = await service.IssueAsync(Tenant, User, default);
        tokenGen.Setup(g => g.Generate()).Returns("second-token");
        var second = await service.IssueAsync(Tenant, User, default);

        // Old challenge + old token no longer activates.
        var old = await service.ConfirmAsync(first.ChallengeId!, Token, default);
        Assert.False(old.Activated);
        // New challenge + new token does.
        var fresh = await service.ConfirmAsync(second.ChallengeId!, "second-token", default);
        Assert.True(fresh.Activated);
        audit.Verify(a => a.SupersededAsync(Tenant, User, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_IsTenantIsolated()
    {
        await SeedProfile(tenant: "tenant-a", user: "shared-user");
        await SeedProfile(tenant: "tenant-b", user: "shared-user");
        var a = await service.IssueAsync("tenant-a", "shared-user", default);
        await service.IssueAsync("tenant-b", "shared-user", default);

        var outcome = await service.ConfirmAsync(a.ChallengeId!, Token, default);

        Assert.True(outcome.Activated);
        Assert.True((await profiles.GetAsync("tenant-a", "shared-user", default))!.IsActive);
        // Tenant B is untouched: its account stays inactive and blocked.
        Assert.False((await profiles.GetAsync("tenant-b", "shared-user", default))!.IsActive);
        Assert.True(deactivated.IsDeactivated("tenant-b", "shared-user"));
    }

    [Fact]
    public async Task Revoke_MarksRevoked_AndConfirmThenFails()
    {
        await SeedProfile();
        var issued = await service.IssueAsync(Tenant, User, default);

        Assert.True(await service.RevokeAsync(Tenant, User, default));
        Assert.Equal(AccountActivationState.Revoked, (await repo.GetAsync(Tenant, User, default))!.State);

        var outcome = await service.ConfirmAsync(issued.ChallengeId!, Token, default);
        Assert.False(outcome.Activated);
        Assert.Equal("invalid_challenge", outcome.RejectionReason);
        audit.Verify(a => a.RevokedAsync(Tenant, User, It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

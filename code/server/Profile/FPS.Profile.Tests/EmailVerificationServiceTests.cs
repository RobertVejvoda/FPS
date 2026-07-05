using FPS.Profile.Application;
using FPS.Profile.Domain;
using FPS.Profile.Infrastructure;
using Microsoft.Extensions.Options;
using Moq;

namespace FPS.Profile.Tests;

// AUTH008 (#729) — the email ownership verification state machine: issue a hashed one-time token, verify
// it before expiry within the attempt limit, and fail closed on expired/wrong/reused/superseded tokens.
public sealed class EmailVerificationServiceTests
{
    private const string Tenant = "tenant-1";
    private const string User = "user-1";
    private const string Email = "jan.novak@greenlogistics.example";
    private const string Token = "one-time-token-abc";

    private readonly InMemoryEmailVerificationRepository repo = new();
    private readonly Mock<IVerificationTokenGenerator> tokenGen = new();
    private readonly Mock<IEmailVerificationSender> sender = new();
    private readonly Mock<IEmailVerificationAuditSink> audit = new();
    private readonly FixedTimeProvider clock = new(DateTimeOffset.Parse("2026-07-04T12:00:00Z"));
    private readonly EmailVerificationService service;

    public EmailVerificationServiceTests()
    {
        tokenGen.Setup(g => g.Generate()).Returns(Token);
        service = new EmailVerificationService(
            repo, tokenGen.Object, sender.Object, audit.Object, clock,
            Options.Create(new EmailVerificationOptions { Ttl = TimeSpan.FromHours(1), MaxAttempts = 3 }));
    }

    [Fact]
    public async Task RequestThenConfirm_ValidToken_MarksVerified()
    {
        Assert.Null(await service.RequestAsync(Tenant, User, Email));

        var outcome = await service.ConfirmAsync(Tenant, User, Token);

        Assert.True(outcome.Verified);
        var record = await repo.GetAsync(Tenant, User);
        Assert.Equal(EmailVerificationState.Verified, record!.State);
        Assert.True(record.IsVerifiedFor(Email));
        audit.Verify(a => a.SucceededAsync(Tenant, User, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Request_StoresOnlyHash_NeverPlaintext()
    {
        await service.RequestAsync(Tenant, User, Email);

        var record = await repo.GetAsync(Tenant, User);
        Assert.NotNull(record);
        Assert.NotEqual(Token, record!.TokenHash);
        Assert.DoesNotContain(Token, record.TokenHash);
        sender.Verify(s => s.SendAsync(Tenant, User, Email.ToLowerInvariant(), Token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_WrongToken_Rejects_AndCountsAttempt()
    {
        await service.RequestAsync(Tenant, User, Email);

        var outcome = await service.ConfirmAsync(Tenant, User, "not-the-token");

        Assert.False(outcome.Verified);
        Assert.Equal("invalid_token", outcome.RejectionReason);
        Assert.Equal(1, (await repo.GetAsync(Tenant, User))!.AttemptCount);
    }

    [Fact]
    public async Task Confirm_ExpiredToken_FailsClosed()
    {
        await service.RequestAsync(Tenant, User, Email);
        clock.Now = clock.Now.AddHours(2); // past the 1h TTL

        var outcome = await service.ConfirmAsync(Tenant, User, Token);

        Assert.False(outcome.Verified);
        Assert.Equal("expired", outcome.RejectionReason);
        Assert.Equal(EmailVerificationState.Expired, (await repo.GetAsync(Tenant, User))!.State);
        audit.Verify(a => a.ExpiredAsync(Tenant, User, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_ReusedToken_FailsClosedOnSecondUse()
    {
        await service.RequestAsync(Tenant, User, Email);
        Assert.True((await service.ConfirmAsync(Tenant, User, Token)).Verified);

        var second = await service.ConfirmAsync(Tenant, User, Token);

        Assert.False(second.Verified);
        Assert.Equal("no_pending_verification", second.RejectionReason);
    }

    [Fact]
    public async Task Request_Supersedes_PriorTokenNoLongerValid()
    {
        await service.RequestAsync(Tenant, User, Email); // token = "one-time-token-abc"
        tokenGen.Setup(g => g.Generate()).Returns("second-token");
        await service.RequestAsync(Tenant, User, Email); // supersedes with a new token

        var outcome = await service.ConfirmAsync(Tenant, User, Token); // old token

        Assert.False(outcome.Verified);
        Assert.Equal("invalid_token", outcome.RejectionReason);
        Assert.True((await service.ConfirmAsync(Tenant, User, "second-token")).Verified);
    }

    [Fact]
    public async Task Confirm_TooManyAttempts_FailsClosed()
    {
        await service.RequestAsync(Tenant, User, Email);
        for (var i = 0; i < 3; i++) await service.ConfirmAsync(Tenant, User, "wrong");

        var outcome = await service.ConfirmAsync(Tenant, User, Token); // correct token, but over the limit

        Assert.False(outcome.Verified);
        Assert.Equal("too_many_attempts", outcome.RejectionReason);
    }

    [Fact]
    public async Task EmailChange_ResetsVerification_ForOldAddress()
    {
        await service.RequestAsync(Tenant, User, Email);
        Assert.True((await service.ConfirmAsync(Tenant, User, Token)).Verified);

        // The account email changes; verification is requested for the new address.
        tokenGen.Setup(g => g.Generate()).Returns("new-token");
        await service.RequestAsync(Tenant, User, "new.address@greenlogistics.example");

        var record = await repo.GetAsync(Tenant, User);
        Assert.False(record!.IsVerifiedFor(Email));                         // old address no longer trusted
        Assert.Equal(EmailVerificationState.Pending, record.State);          // must re-verify the new one
    }

    [Fact]
    public async Task Request_InvalidEmail_ReturnsError_AndDoesNotSend()
    {
        var error = await service.RequestAsync(Tenant, User, "not-an-email");

        Assert.Equal("email_invalid", error);
        Assert.Null(await repo.GetAsync(Tenant, User));
        sender.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

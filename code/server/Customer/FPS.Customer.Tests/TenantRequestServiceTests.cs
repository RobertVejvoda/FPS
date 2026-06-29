using FPS.Customer.Application;
using FPS.Customer.Domain;
using FPS.Customer.Infrastructure;

namespace FPS.Customer.Tests;

public sealed class TenantRequestServiceTests
{
    private readonly InMemoryTenantRequestRepository repo = new();
    private readonly FakeTurnstile turnstile = new();
    private readonly FakeNotifier notifier = new();
    private readonly TenantRequestService service;

    public TenantRequestServiceTests() => service = new TenantRequestService(repo, turnstile, notifier);

    private Task<(TenantRequest? request, string? error)> Submit(
        string company = "Acme Logistics", string domain = "acme.com",
        string email = "jo@acme.com", string message = "30 sites, looking to pilot.", string? token = "tok")
        => service.SubmitAsync(company, domain, email, message, token, "203.0.113.5", CancellationToken.None);

    [Fact]
    public async Task Submit_Valid_CreatesRequest_NotifiesSales_NoProvisioning()
    {
        var (request, error) = await Submit();

        Assert.Null(error);
        Assert.NotNull(request);
        Assert.Equal(TenantRequestStatus.Requested, request!.Status);
        Assert.Equal("acme.com", request.PrimaryDomain);
        Assert.Equal("jo@acme.com", request.ContactEmail);
        Assert.Single(notifier.Notified);
        Assert.Equal(request.RequestId, notifier.Notified[0].RequestId);
        Assert.Single(await repo.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Submit_TurnstileFails_NoRecord_NoNotification()
    {
        turnstile.Result = false;

        var (request, error) = await Submit();

        Assert.NotNull(error);
        Assert.Null(request);
        Assert.Empty(await repo.ListAsync(CancellationToken.None));
        Assert.Empty(notifier.Notified);
    }

    [Theory]
    [InlineData("", "acme.com", "jo@acme.com")]          // missing company
    [InlineData("Acme", "not a domain", "jo@acme.com")]  // invalid domain
    [InlineData("Acme", "acme.com", "not-an-email")]     // invalid email
    public async Task Submit_InvalidInput_ReturnsError_NoRecord(string company, string domain, string email)
    {
        var (request, error) = await service.SubmitAsync(company, domain, email, "", "tok", "ip", CancellationToken.None);

        Assert.NotNull(error);
        Assert.Null(request);
        Assert.Empty(await repo.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Submit_NormalisesDomainScheme()
    {
        var (request, _) = await Submit(domain: "HTTPS://Acme.com/");
        Assert.Equal("acme.com", request!.PrimaryDomain);
    }

    [Fact]
    public async Task Submit_DuplicateOpenEmail_IsCollapsed_WithNeutralAcknowledgement()
    {
        var (first, _) = await Submit();
        var (second, error) = await Submit(); // same email, request still open

        // No email-existence oracle: the duplicate gets an accepted-style ack (no error) but
        // creates no second record and triggers no second sales alert. The id differs so a
        // caller cannot tell it was collapsed.
        Assert.Null(error);
        Assert.NotNull(second);
        Assert.NotEqual(first!.RequestId, second!.RequestId);
        Assert.Single(await repo.ListAsync(CancellationToken.None));
        Assert.Single(notifier.Notified);
    }

    [Fact]
    public async Task Submit_OverlongEmail_IsRejected_NoRecord()
    {
        var email = new string('a', 250) + "@e.com"; // 256 chars > 254 cap

        var (request, error) = await Submit(email: email);

        Assert.NotNull(error);
        Assert.Null(request);
        Assert.Empty(await repo.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Submit_OverlongDomain_IsRejected_NoRecord()
    {
        var label = new string('a', 60);
        var domain = string.Join('.', label, label, label, label, label); // 304 chars > 253 cap

        var (request, error) = await Submit(domain: domain);

        Assert.NotNull(error);
        Assert.Null(request);
        Assert.Empty(await repo.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Approve_TransitionsAndStampsActor()
    {
        var (created, _) = await Submit();

        var (decided, error) = await service.ApproveAsync(created!.RequestId, "actor-hash", "good fit", CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(TenantRequestStatus.Approved, decided!.Status);
        Assert.Equal("actor-hash", decided.DecidedByHash);
        Assert.NotNull(decided.DecidedAt);
    }

    [Fact]
    public async Task Decide_AlreadyDecided_ReturnsError()
    {
        var (created, _) = await Submit();
        await service.ApproveAsync(created!.RequestId, "a", null, CancellationToken.None);

        var (request, error) = await service.RejectAsync(created.RequestId, "b", null, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Null(request);
    }

    [Fact]
    public async Task List_ReturnsAllRequests()
    {
        await Submit(email: "a@x.com");
        await Submit(email: "b@x.com");

        Assert.Equal(2, (await service.ListAsync(CancellationToken.None)).Count);
    }

    private sealed class FakeTurnstile : ITurnstileVerifier
    {
        public bool Result { get; set; } = true;
        public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct) => Task.FromResult(Result);
    }

    private sealed class FakeNotifier : ITenantRequestNotifier
    {
        public List<TenantRequest> Notified { get; } = [];
        public Task NotifySalesAsync(TenantRequest request, CancellationToken ct)
        {
            Notified.Add(request);
            return Task.CompletedTask;
        }
    }
}

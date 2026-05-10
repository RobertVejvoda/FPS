using FPS.Booking.Application.Queries;

namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetMyBookingsHandlerTests
{
    private readonly Mock<IBookingQueryRepository> queryRepository = new();
    private readonly Mock<ITenantPolicyService> policyService = new();
    private readonly GetMyBookingsHandler handler;

    private static readonly TenantPolicy DefaultPolicy = new(
        DailyRequestCap: 500,
        DrawCutOffTime: new TimeOnly(18, 0),
        TimeZoneId: "UTC",
        SameDayBookingEnabled: true,
        AllocationLookbackDays: 10);

    private static readonly BookingListResult EmptyResult =
        new([], null);

    public GetMyBookingsHandlerTests()
    {
        handler = new GetMyBookingsHandler(queryRepository.Object, policyService.Object);

        policyService
            .Setup(s => s.GetEffectivePolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultPolicy);

        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<DateOnly?>(), It.IsAny<string?>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyResult);
    }

    // ── Default from date ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NoFromDate_UsesLookbackDefault()
    {
        DateOnly? capturedFrom = null;
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<DateOnly?>(), It.IsAny<string?>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly, DateOnly?, string?, int, string?, CancellationToken>(
                (_, _, from, _, _, _, _, _) => capturedFrom = from)
            .ReturnsAsync(EmptyResult);

        await handler.Handle(QueryWith(), CancellationToken.None);

        var expected = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
        Assert.NotNull(capturedFrom);
        Assert.Equal(expected, capturedFrom!.Value);
    }

    [Fact]
    public async Task Handle_ExplicitFromDate_UsesProvidedDate()
    {
        var explicitFrom = new DateOnly(2026, 6, 1);
        DateOnly? capturedFrom = null;
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<DateOnly?>(), It.IsAny<string?>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly, DateOnly?, string?, int, string?, CancellationToken>(
                (_, _, from, _, _, _, _, _) => capturedFrom = from)
            .ReturnsAsync(EmptyResult);

        await handler.Handle(QueryWith(from: explicitFrom), CancellationToken.None);

        Assert.Equal(explicitFrom, capturedFrom);
    }

    // ── Page size cap ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PageSizeOver100_CapsAt100()
    {
        int? capturedPageSize = null;
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<DateOnly?>(), It.IsAny<string?>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly, DateOnly?, string?, int, string?, CancellationToken>(
                (_, _, _, _, _, ps, _, _) => capturedPageSize = ps)
            .ReturnsAsync(EmptyResult);

        await handler.Handle(QueryWith(pageSize: 999), CancellationToken.None);

        Assert.Equal(100, capturedPageSize);
    }

    // ── User scoping ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PassesRequestorIdToRepository()
    {
        string? capturedRequestorId = null;
        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<DateOnly?>(), It.IsAny<string?>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateOnly, DateOnly?, string?, int, string?, CancellationToken>(
                (_, requestorId, _, _, _, _, _, _) => capturedRequestorId = requestorId)
            .ReturnsAsync(EmptyResult);

        await handler.Handle(QueryWith(requestorId: "user-42"), CancellationToken.None);

        Assert.Equal("user-42", capturedRequestorId);
    }

    // ── Returns result as-is ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsRepositoryResultDirectly()
    {
        var items = new List<BookingListItem>
        {
            new(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                new TimeOnly(9, 0), new TimeOnly(17, 0), null,
                "Pending", null, null, "cancel",
                DateTime.UtcNow, DateTime.UtcNow)
        };
        var expected = new BookingListResult(items, "next-token");

        queryRepository
            .Setup(r => r.GetByRequestorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<DateOnly?>(), It.IsAny<string?>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await handler.Handle(QueryWith(), CancellationToken.None);

        Assert.Equal(1, result.Items.Count);
        Assert.Equal("next-token", result.NextCursor);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static GetMyBookingsQuery QueryWith(
        string tenantId = "tenant-1",
        string requestorId = "user-1",
        DateOnly? from = null,
        DateOnly? to = null,
        string? status = null,
        int pageSize = 50,
        string? cursor = null)
        => new(tenantId, requestorId, from, to, status, pageSize, cursor);
}

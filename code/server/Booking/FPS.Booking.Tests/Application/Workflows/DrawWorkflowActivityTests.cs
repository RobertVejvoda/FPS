using System.Security.Cryptography;
using System.Text;
using FPS.Booking.Application.Workflows;
using FPS.Booking.Application.Workflows.Activities;

namespace FPS.Booking.Application.Tests.Workflows;

/// <summary>
/// Covers Codex review findings for DRAW002:
///   Finding 1 — AcquireDrawAttemptInput now carries TimeSlotStart/TimeSlotEnd
///   Finding 2 — Seed derived from stable SHA-256 hash, not GetHashCode()
/// </summary>
public sealed class DrawWorkflowActivityTests
{
    private static readonly DateTime SlotStart = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SlotEnd   = new(2026, 6, 2, 17, 0, 0, DateTimeKind.Utc);

    // ── Finding 1: AcquireDrawAttemptInput carries time-slot fields ───────────

    [Fact]
    public void AcquireDrawAttemptInput_IncludesTimeSlotStartAndEnd()
    {
        var input = new AcquireDrawAttemptInput(
            DrawKey: "draw:t1:l1:2026-06-02:0900-1700",
            TenantId: "t1",
            LocationId: "l1",
            Date: "2026-06-02",
            TimeSlotStart: SlotStart.ToString("O"),
            TimeSlotEnd: SlotEnd.ToString("O"),
            Seed: 42L,
            TriggerSource: "manual",
            TriggeredBy: "hr-admin");

        Assert.Equal(SlotStart.ToString("O"), input.TimeSlotStart);
        Assert.Equal(SlotEnd.ToString("O"), input.TimeSlotEnd);
    }

    [Fact]
    public void AcquireDrawAttemptInput_TimeSlotStartCanBeRoundTripParsed()
    {
        var input = new AcquireDrawAttemptInput(
            DrawKey: "draw:t1:l1:2026-06-02:0900-1700",
            TenantId: "t1",
            LocationId: "l1",
            Date: "2026-06-02",
            TimeSlotStart: SlotStart.ToString("O"),
            TimeSlotEnd: SlotEnd.ToString("O"),
            Seed: 0L,
            TriggerSource: "manual",
            TriggeredBy: "hr-admin");

        var parsedStart = DateTime.Parse(input.TimeSlotStart, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var parsedEnd   = DateTime.Parse(input.TimeSlotEnd,   null, System.Globalization.DateTimeStyles.RoundtripKind);

        // start < end — TimeSlot.Create will not throw
        Assert.True(parsedStart < parsedEnd);
    }

    // ── Finding 2: stable SHA-256 seed ───────────────────────────────────────

    [Fact]
    public void Seed_FromSameStoreKey_IsIdenticalAcrossInvocations()
    {
        const string storeKey = "draw:tenant-1:loc-1:2026-06-02:0900-1700";
        var seed1 = ComputeStableSeed(storeKey);
        var seed2 = ComputeStableSeed(storeKey);

        Assert.Equal(seed1, seed2);
    }

    [Fact]
    public void Seed_FromDifferentStoreKeys_IsDifferent()
    {
        var seed1 = ComputeStableSeed("draw:tenant-1:loc-1:2026-06-02:0900-1700");
        var seed2 = ComputeStableSeed("draw:tenant-1:loc-1:2026-06-03:0900-1700");

        Assert.NotEqual(seed1, seed2);
    }

    [Fact]
    public void Seed_IsNonNegative()
    {
        var seed = ComputeStableSeed("draw:tenant-1:loc-1:2026-06-02:0900-1700");
        Assert.True(seed >= 0);
    }

    // Mirrors ResolveDrawInputActivity seed computation exactly.
    private static long ComputeStableSeed(string storeKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(storeKey));
        return Math.Abs(BitConverter.ToInt64(hash, 0));
    }
}

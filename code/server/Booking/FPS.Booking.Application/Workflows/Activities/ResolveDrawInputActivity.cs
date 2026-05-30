using System.Security.Cryptography;
using System.Text;
using Dapr.Workflow;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Workflows.Activities;

// Validates inputs, resolves effective tenant policy, and computes the canonical
// draw key and seed. No draw attempt exists yet when this activity runs.
public sealed class ResolveDrawInputActivity(ITenantPolicyService policyService)
    : WorkflowActivity<DrawWorkflowInput, ResolvedDrawInput>
{
    public override async Task<ResolvedDrawInput> RunAsync(
        WorkflowActivityContext context, DrawWorkflowInput input)
    {
        var date = DateOnly.Parse(input.Date);
        var slotStart = DateTime.Parse(input.TimeSlotStart, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var slotEnd = DateTime.Parse(input.TimeSlotEnd, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var timeSlot = TimeSlot.Create(slotStart, slotEnd);
        var drawKey = DrawKey.Create(input.TenantId, input.LocationId, date, timeSlot);
        var storeKey = drawKey.ToStoreKey();
        var keyBytes = Encoding.UTF8.GetBytes(storeKey);
        var hash = SHA256.HashData(keyBytes);
        var seed = Math.Abs(BitConverter.ToInt64(hash, 0));

        var policy = await policyService.GetEffectivePolicyAsync(input.TenantId, input.LocationId);
        return new ResolvedDrawInput(storeKey, seed, policy.AllocationLookbackDays);
    }
}

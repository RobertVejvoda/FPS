using Dapr.Client;
using FPS.SharedKernel.Infrastructure;

namespace FPS.Customer.Tests;

// PLAT003C-C1 — the Customer-side remote purger adapter delegates to a store-owning service's
// internal /purge/tenant endpoint over Dapr service invocation and returns the reported count.
//
// The invocation path itself is a two-line pass-through with no branching: it calls
// DaprClient.InvokeMethodAsync and returns response.Count; a failed invocation is simply not caught,
// so it propagates and SandboxResetService records Failed + skips the reseed (fail closed). That
// path is not unit-tested here because DaprClient.InvokeMethodAsync is non-virtual and cannot be
// mocked (no test in this repo mocks it) — it is exercised end-to-end by the C3 live seed→reset gate.
// What matters to unit test is the orchestrator-visible contract: Service name and the immutable-
// evidence flag, which decides whether the orchestrator invokes this purger outside a sandbox reset.
public sealed class DaprRemoteTenantStorePurgerTests
{
    private static DaprClient AnyDapr() => new DaprClientBuilder().Build();

    [Fact]
    public void NormalStore_ExposesServiceAndIsNotImmutable()
    {
        var purger = new DaprRemoteTenantStorePurger(AnyDapr(), "fairspot-booking", "booking", isImmutableEvidence: false);

        Assert.Equal("booking", purger.Service);
        Assert.False(purger.IsImmutableEvidence);
    }

    [Fact]
    public void EvidenceStore_IsFlaggedImmutable_SoOrchestratorSkipsItOutsideSandboxReset()
    {
        var purger = new DaprRemoteTenantStorePurger(AnyDapr(), "fairspot-audit", "audit", isImmutableEvidence: true);

        Assert.Equal("audit", purger.Service);
        Assert.True(purger.IsImmutableEvidence);
    }
}

namespace FPS.SharedKernel.Infrastructure;

/// <summary>
/// Cross-service tenant-purge transport contract (PLAT003C). The <see cref="TenantPurgeOrchestrator"/>
/// runs in the Customer process, but most stores live in other services; each store-owning service
/// exposes an internal <c>POST /purge/tenant</c> endpoint and Customer invokes it via Dapr service
/// invocation (reusing the #635 erasure transport). Kept in the shared kernel so caller and callee
/// bind the same shape.
/// </summary>
public sealed record TenantPurgeRequest(string TenantId, bool SandboxReset);

/// <summary>The result of a per-service tenant purge: the service name and the number of records removed.</summary>
public sealed record TenantPurgeResponse(string Service, int Count);

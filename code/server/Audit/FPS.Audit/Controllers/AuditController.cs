using FPS.Audit.Application;
using FPS.Audit.Domain;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Audit.Controllers;

[ApiController]
[Authorize(Roles = $"{AuditRoles.Auditor},{AuditRoles.Admin}")]
public sealed class AuditController(
    AuditQueryService queryService,
    IPiiMappingRepository piiMappingRepository,
    ICurrentUser currentUser) : ControllerBase
{
    private const int MaxActorReferenceBatch = 200;
    private const int ShortRefLength = 6;

    [HttpGet("/audit")]
    public async Task<IActionResult> Query([FromQuery] AuditQueryRequest query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await queryService.QueryAsync(query, currentUser.TenantId, cancellationToken);
        return Ok(result);
    }

    // Auditor workspace drill-down: given a batch of actor hashes from the
    // visible rows, returns the underlying user id (and the short ref the
    // table is showing) so the UI can join with /profile/hr/display-names
    // for name resolution. Issue #482 — the raw userId never appears in
    // audit records, so this is the only path from "A3F1B2" back to "who".
    [HttpPost("/audit/actor-references")]
    [ProducesResponseType(typeof(ActorReferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResolveActorReferences(
        [FromBody] ActorReferencesRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        if (request.ActorHashes is null)
            return BadRequest(new { error = "actorHashes is required." });

        if (request.ActorHashes.Count > MaxActorReferenceBatch)
            return BadRequest(new { error = $"Batch size must not exceed {MaxActorReferenceBatch}." });

        var distinct = request.ActorHashes
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinct.Length == 0)
            return Ok(new ActorReferencesResponse(new Dictionary<string, ActorReferenceItem>()));

        var mappings = await piiMappingRepository.GetByActorHashesAsync(
            currentUser.TenantId, distinct, cancellationToken);

        var items = new Dictionary<string, ActorReferenceItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hash, mapping) in mappings)
        {
            items[hash] = new ActorReferenceItem(
                ActorHash: hash,
                UserId: mapping.UserId,
                ShortRef: ShortRefFromHash(hash));
        }

        return Ok(new ActorReferencesResponse(items));
    }

    // The short ref is the first ShortRefLength hex chars of the actor hash,
    // uppercased — matches displayActorRef in the web client so the value
    // returned here is exactly what the auditor sees in the table.
    private static string ShortRefFromHash(string actorHash) =>
        actorHash.Length <= ShortRefLength
            ? actorHash.ToUpperInvariant()
            : actorHash[..ShortRefLength].ToUpperInvariant();
}

public sealed record ActorReferencesRequest(IReadOnlyList<string>? ActorHashes);
public sealed record ActorReferencesResponse(IReadOnlyDictionary<string, ActorReferenceItem> Items);
public sealed record ActorReferenceItem(string ActorHash, string UserId, string ShortRef);

[ApiController]
[Authorize(Roles = $"{AuditRoles.Auditor},{AuditRoles.Admin}")]
public sealed class PiiMappingController(PiiErasureService erasureService, ICurrentUser currentUser) : ControllerBase
{
    [HttpDelete("/audit/pii-mappings/{userId}")]
    public async Task<IActionResult> Delete(string userId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var requestorHash = FPS.Audit.Application.Pseudonymiser.Hash(currentUser.UserId) ?? string.Empty;
        await erasureService.DeleteByUserIdAsync(userId, currentUser.TenantId, requestorHash, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize(Roles = $"{AuditRoles.Admin}")]
public sealed class AuditRetentionController(
    AuditRetentionService retentionService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("/audit/retention")]
    [ProducesResponseType(typeof(RetentionExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Execute(
        [FromBody] AuditRetentionRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var retentionDays = request.RetentionDays ?? AuditRetentionPolicy.DefaultRetentionDays;
        if (retentionDays < 1)
            return BadRequest("RetentionDays must be at least 1.");

        var policy = new AuditRetentionPolicy(currentUser.TenantId, retentionDays);
        var result = await retentionService.ExecuteAsync(policy, cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Authorize(Roles = $"{AuditRoles.Auditor},{AuditRoles.Admin}")]
public sealed class AuditIntegrityController(
    AuditIntegrityService integrityService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("/audit/integrity")]
    [ProducesResponseType(typeof(IntegrityVerificationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Verify(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? expectedHash,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var result = await integrityService.VerifyAsync(currentUser.TenantId, from, to, expectedHash, cancellationToken);
        return Ok(result);
    }

    [HttpGet("/audit/export")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditExportRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.TenantId))
            return Unauthorized();

        var records = await integrityService.ExportAsync(currentUser.TenantId, from, to, cancellationToken);
        return Ok(records);
    }
}

public sealed record AuditRetentionRequest(int? RetentionDays);

internal static class AuditRoles
{
    internal const string Auditor = "auditor";
    internal const string Admin = "admin";
}

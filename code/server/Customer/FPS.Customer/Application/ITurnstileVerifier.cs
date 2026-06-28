namespace FPS.Customer.Application;

/// <summary>
/// Server-side verification of a Cloudflare Turnstile token for the public, unauthenticated
/// intake path. Implementations must fail closed when a token is missing.
/// </summary>
public interface ITurnstileVerifier
{
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct);
}

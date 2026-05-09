namespace FPS.Identity.Tests.Controllers;

// Identity controller tests require Keycloak integration — implement in Phase 2.
public class AuthControllerTests
{
    [Fact(Skip = "Requires Keycloak — implement in Phase 2")]
    public Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid() => Task.CompletedTask;

    [Fact(Skip = "Requires Keycloak — implement in Phase 2")]
    public Task Register_ReturnsOk_WhenUserIsRegisteredSuccessfully() => Task.CompletedTask;
}

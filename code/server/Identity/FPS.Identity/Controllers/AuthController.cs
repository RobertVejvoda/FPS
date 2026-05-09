using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace FPS.Identity.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(ILogger<AuthController> logger, HttpClient httpClient) : ControllerBase
    {
        private readonly ILogger<AuthController> logger = logger;

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var keycloakUrl = "https://your-keycloak-server/auth/realms/your-realm/protocol/openid-connect/token";
            var clientId = "your-client-id";
            var clientSecret = "your-client-secret";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", request.Username),
                new KeyValuePair<string, string>("password", request.Password)
            });

            var response = await httpClient.PostAsync(keycloakUrl, content);
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                return Ok(tokenResponse);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Handle MFA challenge
                var mfaChallenge = await response.Content.ReadFromJsonAsync<MfaChallengeResponse>();
                return Unauthorized(mfaChallenge);
            }

            return Unauthorized();
        }

        [HttpPost("verify-mfa")]
        public async Task<IActionResult> VerifyMfa([FromBody] VerifyMfaRequest request)
        {
            var keycloakUrl = "https://your-keycloak-server/auth/realms/your-realm/protocol/openid-connect/token";
            var clientId = "your-client-id";
            var clientSecret = "your-client-secret";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", request.Username),
                new KeyValuePair<string, string>("password", request.Password),
                new KeyValuePair<string, string>("totp", request.MfaToken) // Assuming TOTP is used for MFA
            });

            var response = await httpClient.PostAsync(keycloakUrl, content);
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                return Ok(tokenResponse);
            }

            return Unauthorized();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var keycloakUrl = "https://your-keycloak-server/auth/admin/realms/your-realm/users";
            var adminToken = "your-admin-token"; // You need to obtain an admin token to create users

            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            var user = new
            {
                username = request.Username,
                email = request.Email,
                enabled = true,
                credentials = new[]
                {
                    new { type = "password", value = request.Password, temporary = false }
                }
            };

            var response = await httpClient.PostAsJsonAsync(keycloakUrl, user);
            if (response.IsSuccessStatusCode)
            {
                return Ok(new { Message = "User registered successfully" });
            }

            return BadRequest(await response.Content.ReadAsStringAsync());
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var keycloakUrl = $"https://your-keycloak-server/auth/admin/realms/your-realm/users/{request.UserId}/reset-password";
            var adminToken = "your-admin-token"; // You need to obtain an admin token to reset passwords

            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            var password = new
            {
                type = "password",
                value = request.NewPassword,
                temporary = false
            };

            var response = await httpClient.PutAsJsonAsync(keycloakUrl, password);
            if (response.IsSuccessStatusCode)
            {
                return Ok(new { Message = "Password reset successfully" });
            }

            return BadRequest(await response.Content.ReadAsStringAsync());
        }

        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            // Implement email confirmation logic here
            // This might involve verifying a token sent to the user's email

            return Ok(new { Message = "Email confirmed successfully" });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var keycloakUrl = "https://your-keycloak-server/auth/realms/your-realm/protocol/openid-connect/token";
            var clientId = "your-client-id";
            var clientSecret = "your-client-secret";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", request.RefreshToken)
            });

            var response = await httpClient.PostAsync(keycloakUrl, content);
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                return Ok(tokenResponse);
            }

            return Unauthorized();
        }

        [HttpPost("sso-login")]
        public async Task<IActionResult> SsoLogin([FromBody] SsoLoginRequest request)
        {
            var ssoUrl = "https://your-sso-server/auth/realms/your-realm/protocol/openid-connect/token";
            var clientId = "your-client-id";
            var clientSecret = "your-client-secret";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", request.Code),
                new KeyValuePair<string, string>("redirect_uri", request.RedirectUri)
            });

            var response = await httpClient.PostAsync(ssoUrl, content);
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                return Ok(tokenResponse);
            }

            return Unauthorized();
        }

        [HttpPost("social-login")]
        public async Task<IActionResult> SocialLogin([FromBody] SocialLoginRequest request)
        {
            var socialUrl = $"https://your-social-provider.com/oauth2/v4/token";
            var clientId = "your-client-id";
            var clientSecret = "your-client-secret";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", request.Code),
                new KeyValuePair<string, string>("redirect_uri", request.RedirectUri)
            });

            var response = await httpClient.PostAsync(socialUrl, content);
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                return Ok(tokenResponse);
            }

            return Unauthorized();
        }
    }

    public class LoginRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
    }

    public class RegisterRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public required string UserId { get; set; }
        public required string NewPassword { get; set; }
    }

    public class ConfirmEmailRequest
    {
        public required string UserId { get; set; }
        public required string Token { get; set; }
    }

    public class RefreshTokenRequest
    {
        public required string RefreshToken { get; set; }
    }

    public class VerifyMfaRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string MfaToken { get; set; }
    }

    public class TokenResponse
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required int ExpiresIn { get; set; }
    }

    public class MfaChallengeResponse
    {
        public required string ChallengeType { get; set; }
        public required string ChallengeId { get; set; }
    }

    public class SsoLoginRequest
    {
        public required string Code { get; set; }
        public required string RedirectUri { get; set; }
    }

    public class SocialLoginRequest
    {
        public required string Code { get; set; }
        public required string RedirectUri { get; set; }
    }
}
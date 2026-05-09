using FPS.Identity.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace FPS.Identity.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly AuthController controller;

        public AuthControllerTests()
        {
            Mock<ILogger<AuthController>> loggerMock1 = new();
            Mock<HttpClient> httpClientMock1 = new();
            controller = new AuthController(loggerMock1.Object, httpClientMock1.Object);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Username = "invalid",
                Password = "invalid"
            };

            // Act
            var result = await controller.Login(loginRequest);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Register_ReturnsOk_WhenUserIsRegisteredSuccessfully()
        {
            // Arrange
            var registerRequest = new RegisterRequest
            {
                Username = "testuser",
                Password = "password",
                Email = "testuser@example.com"
            };

            // Act
            var result = await controller.Register(registerRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("User registered successfully", ((dynamic)okResult.Value).Message);
        }
    }
}
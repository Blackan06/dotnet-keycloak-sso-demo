using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace IdentityServer.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AuthController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username/password is required" });
            }

            var kcSection = _configuration.GetSection("Keycloak");
            var authority = kcSection["Authority"];   // http://localhost:8080/realms/demo
            var clientId = kcSection["ClientId"];
            var clientSecret = kcSection["ClientSecret"];

            // Token endpoint của Keycloak:
            var tokenEndpoint = $"{authority}/protocol/openid-connect/token";

            var client = _httpClientFactory.CreateClient();

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["username"] = request.Username,
                ["password"] = request.Password,
                ["scope"] = "openid profile email"
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form)
            };

            var response = await client.SendAsync(httpRequest);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return Unauthorized(new { message = "Login failed", detail = error });
            }

            var json = await response.Content.ReadAsStringAsync();

            // Bạn có thể parse thành object, hoặc trả raw JSON luôn
            // Ở đây mình parse sơ cho dễ dùng
            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

            return Ok(tokenData);
        }
    }
}

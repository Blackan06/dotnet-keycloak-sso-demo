using KeyClockServer.Model;
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
            var authority = kcSection["Authority"];  
            var clientId = kcSection["ClientId"];
            var clientSecret = kcSection["ClientSecret"];

            var tokenEndpoint = $"{authority}/protocol/openid-connect/token";

            var client = _httpClientFactory.CreateClient();

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId!,
                ["client_secret"] = clientSecret!,
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

           
            var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

            return Ok(tokenData);
        }

        [HttpPost("logout")]
        [Authorize] 
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { message = "RefreshToken is required" });
            }

            var kcSection = _configuration.GetSection("Keycloak");
            var authority = kcSection["Authority"];   
            var clientId = kcSection["ClientId"];
            var clientSecret = kcSection["ClientSecret"];

            var logoutEndpoint = $"{authority}/protocol/openid-connect/logout";

            var client = _httpClientFactory.CreateClient();

            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId!,
                ["client_secret"] = clientSecret!,
                ["refresh_token"] = request.RefreshToken
            };

            var response = await client.PostAsync(
                logoutEndpoint,
                new FormUrlEncodedContent(form));

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode,
                    new { message = "Logout failed", detail = body });
            }

            return Ok(new { message = "Logged out from Keycloak" });
        }
    }
}

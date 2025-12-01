using KeyClockServer.Model;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace IdentityServer.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public UsersController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var realm = "demo";
            var baseUrl = "http://keycloak:8080"; // host Keycloak trên máy bạn
            var clientId = "dotnet-client";            // client built-in của Keycloak
            var username = "admin";                // admin Keycloak
            var password = "admin";                // pass admin Keycloak

            var client = _httpClientFactory.CreateClient();

            // 1. Lấy admin access token
            var tokenRes = await client.PostAsync(
                $"{baseUrl}/realms/master/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = clientId,
                    ["username"] = username,
                    ["password"] = password
                }));

            if (!tokenRes.IsSuccessStatusCode)
            {
                var err = await tokenRes.Content.ReadAsStringAsync();
                return StatusCode(500, new { message = "Cannot get admin token", detail = err });
            }

            var tokenJson = await tokenRes.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(tokenJson);
            var adminToken = doc.RootElement.GetProperty("access_token").GetString();

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            // 2. Tạo user ở Keycloak
            var userPayload = new
            {
                username = req.Username,
                email = req.Email,
                enabled = true,
                firstName = req.FirstName,
                lastName = req.LastName,
                emailVerified = true,          // nếu bạn không dùng verify email
                requiredActions = new string[] { } // clear required actions
            };


            var createUserRes = await client.PostAsync(
                $"{baseUrl}/admin/realms/{realm}/users",
                new StringContent(JsonSerializer.Serialize(userPayload), Encoding.UTF8, "application/json"));

            if (!createUserRes.IsSuccessStatusCode)
            {
                var err = await createUserRes.Content.ReadAsStringAsync();
                return StatusCode(500, new { message = "Create user failed", detail = err });
            }

            // Lấy Location header chứa URL user mới, trích id
            var location = createUserRes.Headers.Location?.ToString(); // .../users/{id}
            var userId = location?.Split('/').LastOrDefault();

            // 3. Set password cho user
            var passPayload = new
            {
                type = "password",
                value = req.Password,
                temporary = false
            };

            var setPassRes = await client.PutAsync(
                $"{baseUrl}/admin/realms/{realm}/users/{userId}/reset-password",
                new StringContent(JsonSerializer.Serialize(passPayload), Encoding.UTF8, "application/json"));

            if (!setPassRes.IsSuccessStatusCode)
            {
                var err = await setPassRes.Content.ReadAsStringAsync();
                return StatusCode(500, new { message = "Set password failed", detail = err });
            }

            return Ok(new { message = "User created in Keycloak", keycloakUserId = userId });
        }
    }
}
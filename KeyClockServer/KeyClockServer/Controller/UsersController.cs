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
            var kc = _config.GetSection("Keycloak");

            var realm = kc["Realm"];
            var baseUrl = kc["BaseUrl"];
            var adminCli = kc["AdminClientId"];
            var adminUser = kc["AdminUser"];
            var adminPass = kc["AdminPassword"];

            var client = _httpClientFactory.CreateClient();

            var tokenRes = await client.PostAsync(
                $"{baseUrl}/realms/master/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = adminCli,
                    ["username"] = adminUser,
                    ["password"] = adminPass
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
            var userPayload = new
            {
                username = req.Username,
                email = req.Email,
                enabled = true,
                firstName = req.FirstName,
                lastName = req.LastName,
                emailVerified = true,
                attributes = new Dictionary<string, string[]>
                {
                    ["department"] = (req.Departments != null && req.Departments.Any())
                        ? req.Departments.ToArray()
                        : Array.Empty<string>()
                },

                requiredActions = new string[] { } 
            };


            var createUserRes = await client.PostAsync(
                $"{baseUrl}/admin/realms/{realm}/users",
                new StringContent(JsonSerializer.Serialize(userPayload), Encoding.UTF8, "application/json"));

            if (!createUserRes.IsSuccessStatusCode)
            {
                var err = await createUserRes.Content.ReadAsStringAsync();
                return StatusCode(500, new { message = "Create user failed", detail = err });
            }

            var location = createUserRes.Headers.Location?.ToString(); 
            var userId = location?.Split('/').LastOrDefault();

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
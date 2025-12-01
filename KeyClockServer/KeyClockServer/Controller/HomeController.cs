using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


[ApiController]
[Route("api/[controller]")]
public class HomeController : ControllerBase
{
    // Endpoint public, không cần token
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping()
    {
        return Ok(new { message = "pong", time = DateTime.UtcNow });
    }

    // Endpoint bảo vệ, cần token Keycloak
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var name = User.Identity?.Name;
        var claims = User.Claims.Select(c => new { c.Type, c.Value });

        return Ok(new
        {
            userName = name,
            claims
        });
    }
}

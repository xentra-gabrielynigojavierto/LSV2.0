using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            service = "Flow",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("/info")]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            service = "Flow",
            version = "1.0.0",
            description = "LegalSynq Flow workflow + task service",
            timestamp = DateTime.UtcNow
        });
    }
}

using Microsoft.AspNetCore.Mvc;

namespace OpenParking.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            modules = new[]
            {
                new { name = "User & Access (Student 1)", status = "Healthy" },
                new { name = "Space & Availability (Student 2)", status = "Healthy" },
                new { name = "Booking & Payment (Student 3)", status = "Healthy" },
                new { name = "Enforcement & AI Orchestration (Student 4)", status = "Healthy" }
            }
        });
    }
}

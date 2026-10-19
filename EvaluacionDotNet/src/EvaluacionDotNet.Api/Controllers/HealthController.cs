using EvaluacionDotNet.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvaluacionDotNet.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var connected = await db.Database.CanConnectAsync(cancellationToken);
        return StatusCode(connected ? 200 : 503, new
        {
            status = connected ? "healthy" : "unhealthy",
            database = connected ? "connected" : "unavailable"
        });
    }
}

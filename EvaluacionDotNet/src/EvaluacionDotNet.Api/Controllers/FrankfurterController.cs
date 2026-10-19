using EvaluacionDotNet.Api.Contracts;
using EvaluacionDotNet.Api.Infrastructure;
using EvaluacionDotNet.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvaluacionDotNet.Api.Controllers;

[ApiController, Authorize, Route("frankfurter")]
public class FrankfurterController(FrankfurterService service) : ControllerBase
{
    [HttpGet("latest")]
    public async Task<IActionResult> Latest([FromQuery(Name = "base")] string from,
        [FromQuery] string target, CancellationToken ct) =>
        Ok(await service.Fetch("latest", from, target, null, null, null, ct));

    [HttpGet("historical")]
    public async Task<IActionResult> Historical([FromQuery(Name = "base")] string from,
        [FromQuery] string target, [FromQuery] DateOnly date, CancellationToken ct) =>
        Ok(await service.Fetch("historical", from, target, date, null, null, ct));

    [HttpGet("timeseries")]
    public async Task<IActionResult> Timeseries([FromQuery(Name = "base")] string from,
        [FromQuery] string target, [FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken ct) =>
        Ok(await service.Fetch("timeseries", from, target, null, start, end, ct));

    [HttpGet("convert")]
    public async Task<IActionResult> ConvertAmount([FromQuery] string from, [FromQuery] string to,
        [FromQuery] decimal amount, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        if (amount <= 0 || amount > 1_000_000_000_000m) throw new ApiException(400, "amount debe ser mayor que cero y menor o igual que 1000000000000.");
        var rate = (await service.Fetch(date is null ? "latest" : "historical", from, to, date, null, null, ct))[0];
        return Ok(new { from = rate.BaseCurrency, to = rate.TargetCurrency, amount, rate.Rate, rate.Date, convertedAmount = decimal.Round(amount * rate.Rate, 4) });
    }

    [HttpPost("import/{kind}")]
    public async Task<IActionResult> Import(string kind, ImportRequest request, CancellationToken ct) =>
        Ok(await service.Import(kind.ToLowerInvariant(), request, ct));
}

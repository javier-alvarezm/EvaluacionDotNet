using EvaluacionDotNet.Api.Contracts;
using EvaluacionDotNet.Api.Data;
using EvaluacionDotNet.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EvaluacionDotNet.Api.Controllers;

[ApiController, Authorize, Route("rates")]
public class RatesController(AppDbContext db) : ControllerBase
{
    /// <summary>Devuelve la lista de tasas de cambio almacenadas.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.ExchangeRates.AsNoTracking().OrderByDescending(r => r.Date).ThenBy(r => r.Id).ToListAsync(ct));

    /// <summary>Devuelve una tasa de cambio específica por Id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var rate = await db.ExchangeRates.FindAsync([id], ct);
        return rate is null ? NotFound() : Ok(rate);
    }

    /// <summary>Crea nuevas tasas de cambio.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(RateRequest request, CancellationToken ct)
    {
        var error = await ValidateRate(request, ct);
        if (error is not null) return BadRequest(error);
        var rate = new ExchangeRate();
        Assign(rate, request);
        db.ExchangeRates.Add(rate);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = rate.Id }, rate);
    }

    /// <summary>Actualiza una tasa de cambio existente por Id.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, RateRequest request, CancellationToken ct)
    {
        var rate = await db.ExchangeRates.FindAsync([id], ct);
        if (rate is null) return NotFound();
        var error = await ValidateRate(request, ct);
        if (error is not null) return BadRequest(error);
        Assign(rate, request);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Elimina una tasa de cambio por Id.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var affected = await db.ExchangeRates.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
        return affected == 0 ? NotFound() : NoContent();
    }

    /// <summary>Devuelve las tasas de cambio por moneda base.</summary>
    [HttpGet("currency/{baseCurrency}")]
    public async Task<IActionResult> ByCurrency(string baseCurrency, CancellationToken ct) =>
        Ok(await db.ExchangeRates.AsNoTracking().Where(r => r.BaseCurrency == baseCurrency.ToUpperInvariant())
            .OrderByDescending(r => r.Date).ThenBy(r => r.Id).ToListAsync(ct));

    /// <summary>Actualiza las tasas de cambio por moneda base (requiere pasar un body con los datos a actualizar).</summary>
    [HttpPut("currency/{baseCurrency}")]
    public async Task<IActionResult> UpdateByCurrency(string baseCurrency, BulkRateRequest request, CancellationToken ct)
    {
        var query = db.ExchangeRates.Where(r => r.BaseCurrency == baseCurrency.ToUpperInvariant());
        var affected = await query.ExecuteUpdateAsync(set => set.SetProperty(r => r.Rate, request.Rate), ct);
        return affected == 0 ? NotFound() : Ok(new { updated = affected });
    }

    /// <summary>Elimina las tasas de cambio por moneda base.</summary>
    [HttpDelete("currency/{baseCurrency}")]
    public async Task<IActionResult> DeleteByCurrency(string baseCurrency, CancellationToken ct)
    {
        var affected = await db.ExchangeRates.Where(r => r.BaseCurrency == baseCurrency.ToUpperInvariant()).ExecuteDeleteAsync(ct);
        return affected == 0 ? NotFound() : Ok(new { deleted = affected });
    }

    /// <summary>Devuelve el valor promedio de la tasa de cambio entre las fechas especificadas.</summary>
    [HttpGet("average")]
    public async Task<IActionResult> Average([FromQuery(Name = "base")] string baseCurrency,
        [FromQuery(Name = "target")] string targetCurrency, [FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken ct)
    {
        if (start == default || end == default || start > end) return BadRequest("Indica start y end válidos, con start <= end.");
        var query = InRange(baseCurrency, targetCurrency, start, end);
        var values = await query.GroupBy(r => 1).Select(g => new { average = g.Average(r => r.Rate), count = g.Count() }).SingleOrDefaultAsync(ct);
        return values is null ? NotFound() : Ok(new { baseCurrency = baseCurrency.ToUpperInvariant(), targetCurrency = targetCurrency.ToUpperInvariant(), start, end, values.average, values.count });
    }

    /// <summary>Devuelve los valores mínimo y máximo de la tasa de cambio entre las fechas especificadas.</summary>
    [HttpGet("minmax")]
    public async Task<IActionResult> MinMax([FromQuery(Name = "base")] string baseCurrency,
        [FromQuery(Name = "target")] string targetCurrency, [FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken ct)
    {
        if (start == default || end == default || start > end) return BadRequest("Indica start y end válidos, con start <= end.");
        var values = await InRange(baseCurrency, targetCurrency, start, end).GroupBy(r => 1)
            .Select(g => new { min = g.Min(r => r.Rate), max = g.Max(r => r.Rate), count = g.Count() }).SingleOrDefaultAsync(ct);
        return values is null ? NotFound() : Ok(new { baseCurrency = baseCurrency.ToUpperInvariant(), targetCurrency = targetCurrency.ToUpperInvariant(), start, end, values.min, values.max, values.count });
    }

    private IQueryable<ExchangeRate> InRange(string from, string to, DateOnly start, DateOnly end) =>
        db.ExchangeRates.AsNoTracking().Where(r => r.BaseCurrency == from.ToUpperInvariant() && r.TargetCurrency == to.ToUpperInvariant()
            && r.Date >= start.ToDateTime(TimeOnly.MinValue) && r.Date <= end.ToDateTime(TimeOnly.MinValue));

    private async Task<string?> ValidateRate(RateRequest request, CancellationToken ct)
    {
        var from = request.BaseCurrency.ToUpperInvariant();
        var to = request.TargetCurrency.ToUpperInvariant();
        if (from.Length != 3 || from.Any(c => c is < 'A' or > 'Z')) return "La moneda base debe tener tres letras.";
        if (from == to) return "La moneda base debe ser diferente a la moneda destino.";
        if (request.Date == default || request.Date.Date > DateTime.UtcNow.Date) return "La fecha es obligatoria y no puede ser futura.";
        if (!await db.Currencies.AnyAsync(c => c.Symbol == from, ct) || !await db.Currencies.AnyAsync(c => c.Symbol == to, ct))
            return "Las monedas deben existir en Currency. Puedes crearlas importando tasas desde Frankfurter.";
        return null;
    }

    private static void Assign(ExchangeRate rate, RateRequest request)
    {
        rate.BaseCurrency = request.BaseCurrency.ToUpperInvariant();
        rate.TargetCurrency = request.TargetCurrency.ToUpperInvariant();
        rate.Rate = request.Rate;
        rate.Date = request.Date.Date;
    }
}

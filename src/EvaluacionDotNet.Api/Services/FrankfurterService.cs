using System.Net;
using System.Text.Json;
using EvaluacionDotNet.Api.Contracts;
using EvaluacionDotNet.Api.Data;
using EvaluacionDotNet.Api.Infrastructure;
using EvaluacionDotNet.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaluacionDotNet.Api.Services;

public class FrankfurterService(IHttpClientFactory factory, AppDbContext db)
{
    private readonly HttpClient client = factory.CreateClient("Frankfurter");

    public async Task<List<RatePoint>> Fetch(string kind, string from, string to, DateOnly? date,
        DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        from = Normalize(from);
        to = Normalize(to);
        if (from == to) throw new ApiException(400, "Las monedas deben ser diferentes.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var first = new DateOnly(1999, 1, 4);
        string path;
        switch (kind)
        {
            case "latest": path = "latest"; break;
            case "historical":
                if (date is null || date < first || date > today) throw new ApiException(400, "date debe estar entre 1999-01-04 y hoy.");
                path = date.Value.ToString("yyyy-MM-dd");
                break;
            case "timeseries":
                if (start is null || end is null || start < first || start > end || end > today || end.Value.DayNumber - start.Value.DayNumber > 366)
                    throw new ApiException(400, "Indica start y end desde 1999-01-04, sin fechas futuras, con un máximo de 366 días de diferencia.");
                path = $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}";
                break;
            default: throw new ApiException(400, "El tipo de consulta debe ser latest, historical o timeseries.");
        }
        using var document = await GetDocument($"{path}?base={from}&symbols={to}", ct);
        var root = document.RootElement;
        var points = new List<RatePoint>();
        if (kind == "timeseries")
        {
            foreach (var day in root.GetProperty("rates").EnumerateObject())
                if (day.Value.TryGetProperty(to, out var rate))
                    points.Add(new RatePoint(from, to, rate.GetDecimal(), DateOnly.ParseExact(day.Name, "yyyy-MM-dd")));
        }
        else if (root.GetProperty("rates").TryGetProperty(to, out var value))
            points.Add(new RatePoint(from, to, value.GetDecimal(), DateOnly.ParseExact(root.GetProperty("date").GetString()!, "yyyy-MM-dd")));
        if (points.Count == 0) throw new ApiException(404, "Frankfurter no tiene tasas para la consulta indicada.");
        return points.OrderBy(p => p.Date).ToList();
    }

    public async Task<object> Import(string kind, ImportRequest request, CancellationToken ct)
    {
        var points = await Fetch(kind, request.BaseCurrency, request.TargetCurrency, request.Date, request.Start, request.End, ct);
        using var currencies = await GetDocument("currencies", ct);
        var symbols = new[] { points[0].BaseCurrency, points[0].TargetCurrency };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var symbol in symbols)
        {
            if (!await db.Currencies.AnyAsync(c => c.Symbol == symbol, ct))
            {
                var name = currencies.RootElement.TryGetProperty(symbol, out var label) ? label.GetString()! : symbol;
                db.Currencies.Add(new Currency { Symbol = symbol, Name = name });
            }
        }
        await db.SaveChangesAsync(ct);
        var firstDate = points[0].Date.ToDateTime(TimeOnly.MinValue);
        var lastDate = points[^1].Date.ToDateTime(TimeOnly.MinValue);
        var existing = await db.ExchangeRates.Where(r => r.BaseCurrency == symbols[0] && r.TargetCurrency == symbols[1]
            && r.Date >= firstDate && r.Date <= lastDate).ToDictionaryAsync(r => DateOnly.FromDateTime(r.Date), ct);
        var created = 0;
        foreach (var point in points)
        {
            if (existing.TryGetValue(point.Date, out var row)) row.Rate = point.Rate;
            else
            {
                db.ExchangeRates.Add(new ExchangeRate { BaseCurrency = point.BaseCurrency, TargetCurrency = point.TargetCurrency,
                    Date = point.Date.ToDateTime(TimeOnly.MinValue), Rate = point.Rate });
                created++;
            }
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new { created, updated = points.Count - created, rates = points };
    }

    private async Task<JsonDocument> GetDocument(string path, CancellationToken ct)
    {
        using var response = await client.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => 400,
                HttpStatusCode.NotFound => 404,
                _ => 502
            }, "Frankfurter rechazó la consulta o no tiene información para esas monedas y fechas.");
        try { return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); }
        catch (JsonException) { throw new ApiException(502, "Frankfurter devolvió una respuesta inválida."); }
    }

    private static string Normalize(string value)
    {
        if (value.Length != 3 || value.Any(c => !char.IsAsciiLetter(c))) throw new ApiException(400, "Las monedas deben tener tres letras.");
        return value.ToUpperInvariant();
    }
}

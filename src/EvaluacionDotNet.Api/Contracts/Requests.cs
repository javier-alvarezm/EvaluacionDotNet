using System.ComponentModel.DataAnnotations;

namespace EvaluacionDotNet.Api.Contracts;

public record RateRequest(
    [Required, RegularExpression("^[A-Za-z]{3}$")] string BaseCurrency,
    [Required, RegularExpression("^[A-Za-z]{3}$")] string TargetCurrency,
    [Range(typeof(decimal), "0.000000000001", "9999999999999999", ParseLimitsInInvariantCulture = true)] decimal Rate,
    DateTime Date);

public record BulkRateRequest(
    [Range(typeof(decimal), "0.000000000001", "9999999999999999", ParseLimitsInInvariantCulture = true)] decimal Rate);

public record ImportRequest(
    [Required, RegularExpression("^[A-Za-z]{3}$")] string BaseCurrency,
    [Required, RegularExpression("^[A-Za-z]{3}$")] string TargetCurrency,
    DateOnly? Date,
    DateOnly? Start,
    DateOnly? End);

public record RatePoint(string BaseCurrency, string TargetCurrency, decimal Rate, DateOnly Date);

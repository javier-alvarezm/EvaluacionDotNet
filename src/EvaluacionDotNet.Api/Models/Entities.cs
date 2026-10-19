namespace EvaluacionDotNet.Api.Models;

public class Currency
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
}

public class ExchangeRate
{
    public int Id { get; set; }
    public string BaseCurrency { get; set; } = "";
    public string TargetCurrency { get; set; } = "";
    public decimal Rate { get; set; }
    public DateTime Date { get; set; }
}


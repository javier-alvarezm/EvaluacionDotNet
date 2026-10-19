using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EvaluacionDotNet.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ApiException e => (e.Status, e.Message),
            DbUpdateException => (409, "La operación entra en conflicto con los datos existentes."),
            HttpRequestException => (502, "No fue posible consultar Frankfurter."),
            TaskCanceledException when !ct.IsCancellationRequested => (504, "Frankfurter no respondió a tiempo."),
            _ => (500, "Ocurrió un error al procesar la solicitud.")
        };
        if (status >= 500) logger.LogError(exception, "Error procesando {Path}", context.Request.Path);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = title, Instance = context.Request.Path,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken: ct);
        return true;
    }
}

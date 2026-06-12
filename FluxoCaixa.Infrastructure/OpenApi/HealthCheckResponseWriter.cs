using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace FluxoCaixa.Api.Infrastructure.OpenApi
{
    public static class HealthCheckResponseWriter
    {
        public static async Task WriteJsonResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                status = report.Status.ToString(),
                tempo_total_ms = report.TotalDuration.TotalMilliseconds,
                componentes = report.Entries.Select(entry => new
                {
                    nome = entry.Key,
                    status = entry.Value.Status.ToString(),
                    tempo_ms = entry.Value.Duration.TotalMilliseconds,
                    erro = entry.Value.Exception?.Message
                })
            };

            var jsonOpcoes = new JsonSerializerOptions { WriteIndented = true };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, jsonOpcoes)
            );
        }
    }
}

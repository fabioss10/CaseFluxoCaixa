using FluxoCaixa.Api.Infrastructure.Configurations;
using FluxoCaixa.Api.Infrastructure.OpenApi;
using FluxoCaixa.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FluxoCaixa.Api.Infrastructure.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureSecurity(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtOptions = new JwtOptions();
            configuration.GetSection("Jwt").Bind(jwtOptions);

            services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

            if (string.IsNullOrEmpty(jwtOptions.SecretKey))
            {
                throw new InvalidOperationException("A chave secreta do JWT não foi configurada.");
            }

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // TRATAMENTO CUSTOMIZADO PARA REJEIÇÃO DE TOKEN (401)
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        var erro401 = new { sucesso = false, erro = "Não Autorizado", mensagem = "Acesso negado. Token ausente, inválido ou expirado." };
                        await context.Response.WriteAsync(JsonSerializer.Serialize(erro401));
                    },
                    // TRATAMENTO CUSTOMIZADO PARA FALTA DE PERMISSÃO (403)
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";

                        var erro403 = new { sucesso = false, erro = "Proibido", mensagem = "Seu usuário não possui o nível de acesso ou escopo necessário para esta operação." };
                        await context.Response.WriteAsync(JsonSerializer.Serialize(erro403));
                    }
                };
            });

            // CORREÇÃO DO ARRAY DE ESCOPOS: Usa a validação via Assert para ler strings simples e arrays
            services.AddAuthorization(options =>
            {
                options.AddPolicy("EscopoGrava", policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireAssertion(context =>
                              context.User.HasClaim(c => c.Type == "scope" &&
                              (c.Value == "fluxocaixa.write" || c.Value.Contains("fluxocaixa.write")))));

                options.AddPolicy("EscopoLeitura", policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireAssertion(context =>
                              context.User.HasClaim(c => c.Type == "scope" &&
                              (c.Value == "fluxocaixa.read" || c.Value.Contains("fluxocaixa.read")))));
            });

            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            });


            // ============================================================================
            // HEALTH CHECKS CORPORATIVOS - DIAGNÓSTICOS AVANÇADOS
            // ============================================================================
            services.AddHealthChecks()
                // 1. Diagnóstico do Banco de Dados (Leve e rápido)
                .AddDbContextCheck<FluxoCaixaDbContext>(
                    name: "sqlserver_db",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "db", "ready" },
                    customTestQuery: (context, cancellationToken) => context.Database.CanConnectAsync(cancellationToken)
                )

                // 2. Diagnóstico de Memória Alocada (Evita pane por falta de RAM no container)
                .AddCheck("memoria_ram_api", () =>
                {
                    var memoriaGastaBytes = Process.GetCurrentProcess().WorkingSet64;
                    var memoriaGastaMegabytes = memoriaGastaBytes / (1024 * 1024);

                    // Define um limite de segurança (exemplo: Alerta se o container passar de 512MB)
                    long limiteMaximoMb = 512;

                    if (memoriaGastaMegabytes > limiteMaximoMb)
                    {
                        return HealthCheckResult.Degraded($"Consumo de RAM elevado: {memoriaGastaMegabytes}MB utilizados de um limite de {limiteMaximoMb}MB.");
                    }

                    return HealthCheckResult.Healthy($"Consumo de RAM saudável: {memoriaGastaMegabytes}MB utilizados.");
                }, tags: new[] { "infra" });




            // ============================================================================
            // OBSERVABILIDADE DO .NET 10 (O QUE HÁ DE MELHOR E MAIS MODERNO)
            // ============================================================================
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("FluxoCaixa.Api"))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation() // Captura rotas, latência e RPS automaticamente
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddMeter("System.Net.Http")
                    // Envia as métricas em background via protocolo universal estável
                    .AddOtlpExporter(options =>
                    {
                        // Aponta para a porta padrão do Painel de Controle da Microsoft (Aspire)
                        options.Endpoint = new Uri("http://localhost:18889");
                    }));




            return services;
        }
    }
}

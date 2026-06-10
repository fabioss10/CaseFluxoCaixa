using FluentValidation;
using FluxoCaixa.Api.Infrastructure.Extensions;
using FluxoCaixa.Api.Infrastructure.OpenApi;
using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Application.Services;
using FluxoCaixa.Application.Validators;
using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
using FluxoCaixa.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configura o Kestrel para responder na porta da API (7248) e na porta do Health (8081)

builder.WebHost.ConfigureKestrel(options =>
{
    // Porta da API configurada explicitamente para usar HTTPS/SSL 
    options.ListenAnyIP(7248, listenOptions =>
    {
        listenOptions.UseHttps();
    });

    // Porta de Infraestrutura (Health/Metrics) mantida em HTTP comum (Padrão de mercado)
    options.ListenAnyIP(8081);
});

builder.Services.AddControllers();

// Ativa JWT, Políticas e o OpenAPI/Swagger com o Transformer isolado
builder.Services.AddInfrastructureSecurity(builder.Configuration);

// Banco de dados com alta vazão (Pool)
builder.Services.AddDbContextPool<FluxoCaixaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Injeção de dependência dos repositórios e serviços
builder.Services.AddScoped<ILancamentoRepository, LancamentoRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<ICriarLancamentoService, CriarLancamentoService>();
builder.Services.AddScoped<IConsultarSaldoService, ConsultarSaldoService>();
builder.Services.AddScoped<IProcessadorOutboxService, ProcessadorOutboxService>();
builder.Services.AddScoped<ISaldoConsolidadoRepository, SaldoConsolidadoRepository>();
builder.Services.AddScoped<IUnitOfWorkRepository, UnitOfWork>();

// Validações
builder.Services.AddValidatorsFromAssemblyContaining<CriarLancamentoRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();



var app = builder.Build();


// 3. Pipeline do HTTP request
if (app.Environment.IsDevelopment())
{
    // Rota leve para máquinas - Responde APENAS se a requisição vier na porta 8081
    app.MapHealthChecks("/healthz")
       .RequireHost("*:8081");

    // Rota detalhada JSON - Responde APENAS se a requisição vier na porta 8081
    app.MapHealthChecks("/healthz/detail", new() { ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse })
       .RequireHost("*:8081");

    

    // Adiciona uma rota secundária amigável que renderiza uma página HTML com gráficos
    app.UseMetricServer();
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Fluxo Caixa API v1");
    });
}

app.UseWhen(
    context => context.Connection.LocalPort != 8081,
    appBuilder => appBuilder.UseHttpsRedirection()
);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();



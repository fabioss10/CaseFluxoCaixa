using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Application.Services;
using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
using FluxoCaixa.Infrastructure.Repositories;
using FluxoCaixa.Worker;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<FluxoCaixaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configura os logs nativos para exportar via OTLP (Dashboard)
builder.Logging.AddOpenTelemetry(logging => logging.AddOtlpExporter());

// Configura as métricas e traces nativos para o Dashboard
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddOtlpExporter())
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddOtlpExporter());


///<summary>
///Registrando injeção de dependência para o serviço de trabalho (OutboxWorker) que será responsável por processar os eventos da tabela de outbox e enviá-los para mensageria. 
/// </summary>
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddScoped<IProcessadorOutboxService, ProcessadorOutboxService>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<ISaldoConsolidadoRepository, SaldoConsolidadoRepository>();

builder.Services.AddScoped<IUnitOfWorkRepository, UnitOfWork>();

var host = builder.Build();
host.Run();

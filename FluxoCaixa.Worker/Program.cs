using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Application.Services;
using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
using FluxoCaixa.Infrastructure.Repositories;
using FluxoCaixa.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<FluxoCaixaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

///<summary>
///Registrando injeção de dependência para o serviço de trabalho (OutboxWorker) que será responsável por processar os eventos da tabela de outbox e enviá-los para mensageria. 
/// </summary>
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddScoped<IProcessadorOutboxService, ProcessadorOutboxService>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<ISaldoConsolidadoRepository, SaldoConsolidadoRepository>();

var host = builder.Build();
host.Run();

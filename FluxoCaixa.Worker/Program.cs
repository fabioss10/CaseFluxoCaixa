using FluxoCaixa.Worker;
using FluxoCaixa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<FluxoCaixaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

///<summary>
///Registrando injeção de dependência para o serviço de trabalho (OutboxWorker) que será responsável por processar os eventos da tabela de outbox e enviá-los para mensageria. 
/// </summary>
builder.Services.AddHostedService<OutboxWorker>();

var host = builder.Build();
host.Run();

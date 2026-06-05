using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;


namespace FluxoCaixa.Worker
{
    /// <summary>
    /// O OutboxWorker é um serviço hospedado (Hosted Service) que roda em segundo plano,
    /// monitorando a tabela de outbox e garantindo que os eventos sejam processados de forma confiável e eficiente, 
    /// mesmo em cenários de alta concorrência ou falhas temporárias.
    /// </summary>
    public class OutboxWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public OutboxWorker(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {

                /// <summary>
                /// Abre um escopo temporário delimitado por chaves {}. Isso garante que o método Dispose() 
                /// do UnitOfWork e do DbContext seja disparado IMEDIATAMENTE após o processamento terminar.
                /// Evita que a conexão com o banco fique presa durante os 3 segundos de Task.Delay, 
                /// prevenindo Connection Pooling Starvation e otimizando o uso de memória no Worker.
                /// </summary>
                using (var scope = _scopeFactory.CreateScope())
                {
                    var processador = scope.ServiceProvider
                        .GetRequiredService<IProcessadorOutboxService>();

                    await processador.ProcessarAsync(stoppingToken);
                }

                //Espera 3 segundos antes de rodar novamente.Isso evita:
                //loop agressivo
                //sobrecarga no banco
                //consumo excessivo de CPU
                await Task.Delay(
                    3000,
                    stoppingToken);
            }
        }
        
    }
}

using FluxoCaixa.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;


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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<FluxoCaixaDbContext>();

                var eventos = await db.OutboxEvents
                    .Where(x => x.Status == Domain.Enums.StatusEvento.Pendente)
                    .ToListAsync(stoppingToken);

                foreach (var evento in eventos)
                {
                    try
                    {
                        // SIMULA envio para mensageria
                        Console.WriteLine($"Processando evento {evento.Id}");
                        //

                        evento.MarcarComoProcessado();

                        db.OutboxEvents.Update(evento);
                    }
                    catch
                    {
                        evento.MarcarComoErro();
                        db.OutboxEvents.Update(evento);
                    }
                    
                }

                await db.SaveChangesAsync(stoppingToken);

                //Espera 3 segundos antes de rodar novamente.Isso evita:
                //loop agressivo
                //sobrecarga no banco
                //consumo excessivo de CPU

                await Task.Delay(3000, stoppingToken);
            }
        }
    }
}

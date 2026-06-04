using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using FluxoCaixa.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FluxoCaixa.Application.Services
{
    public class ProcessadorOutboxService
     : IProcessadorOutboxService
    {
    
    private readonly IOutboxEventRepository _outboxRepository;
    private readonly ISaldoConsolidadoRepository _saldoRepository;

        public ProcessadorOutboxService(
        IOutboxEventRepository outboxRepository,
        ISaldoConsolidadoRepository saldoRepository)
        {
            _outboxRepository = outboxRepository;
            _saldoRepository = saldoRepository;
        }

        public async Task ProcessarAsync(
        CancellationToken cancellationToken)
        {
            var eventos =
                await _outboxRepository
                    .ObterPendentesAsync();

            foreach (var evento in eventos)
            {
                try
                {
                    // processamento
                    var lancamento =
                    JsonSerializer.Deserialize<Lancamento>(
                        evento.Payload);

                    var data =
                    lancamento.DataCriacao.Date;

                    var saldo =
                        await _saldoRepository
                            .ObterPorDataAsync(DateOnly.FromDateTime(data));

                    if (saldo == null)
                    {
                        saldo = SaldoConsolidado
                            .CriarComLancamento(lancamento);

                        await _saldoRepository
                            .AdicionarAsync(saldo);
                    }
                    else
                    {
                        saldo.AplicarLancamento(lancamento);

                        await _saldoRepository
                            .AtualizarAsync(saldo);
                    }



                    evento.MarcarComoProcessado();

                    await _outboxRepository
                        .AtualizarAsync(evento);

                }
                catch
                {
                    evento.MarcarComoErro();

                    await _outboxRepository
                        .AtualizarAsync(evento);
                }
            }
        }


    }



}

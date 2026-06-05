using FluxoCaixa.Application.DTOs;
using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Interfaces;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FluxoCaixa.Application.Services
{
    public class CriarLancamentoService : ICriarLancamentoService
    {
        // Injeta apenas o Unit of Work para garantir o ponto único de transação
        private readonly IUnitOfWorkRepository _uow;

        public CriarLancamentoService(IUnitOfWorkRepository uow)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        // Adicionado o CancellationToken para repassar ao CommitAsync no final do fluxo
        public async Task<Guid> ExecutarAsync(CriarLancamentoRequest request, CancellationToken cancellationToken = default)
        {
            var lancamento = new Lancamento(
                request.Tipo,
                request.Valor);

            // Adiciona o lançamento no Change Tracker (em memória)
            await _uow.Lancamentos.AdicionarAsync(lancamento);

            var payload = JsonSerializer.Serialize(new
            {
                lancamento.Id,
                lancamento.Tipo,
                lancamento.Valor,
                lancamento.DataCriacao
            });

            var outboxEvent = new OutboxEvent(
                lancamento.Id,
                payload);

            // Adiciona o evento do Outbox no Change Tracker (em memória)
            await _uow.OutboxEvents.AdicionarAsync(outboxEvent);

            
            // O comando abaixo persiste tanto o Lançamento quanto o OutboxEvent em uma ÚNICA transação do banco.
            // Se o banco falhar ao gravar o evento do Outbox, o Lançamento sofre Rollback automático.
            // Isso impede o maior erro em arquiteturas de microsserviços: salvar o dado mas não disparar o evento.
            await _uow.CommitAsync(cancellationToken);

            return lancamento.Id;
        }
    }
}

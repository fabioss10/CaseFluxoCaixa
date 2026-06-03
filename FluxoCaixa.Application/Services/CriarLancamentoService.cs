using FluxoCaixa.Application.DTOs;
using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FluxoCaixa.Application.Services
{
    public class CriarLancamentoService
    : ICriarLancamentoService
    {
        private readonly ILancamentoRepository _lancamentoRepository;
        private readonly IOutboxEventRepository _outboxRepository;

        public CriarLancamentoService(
            ILancamentoRepository lancamentoRepository,
            IOutboxEventRepository outboxRepository)
        {
            _lancamentoRepository = lancamentoRepository;
            _outboxRepository = outboxRepository;
        }

        public async Task<Guid> ExecutarAsync(
        CriarLancamentoRequest request)
        {
            var lancamento = new Lancamento(
                request.Tipo,
                request.Valor);

            await _lancamentoRepository.AdicionarAsync(
                lancamento);

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

            await _outboxRepository.AdicionarAsync(
                outboxEvent);

            return lancamento.Id;
        }
    }
}

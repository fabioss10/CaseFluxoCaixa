using FluxoCaixa.Application.DTOs;
using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Application.Services
{
    public class ConsultarSaldoService
     : IConsultarSaldoService
    {
        private readonly ISaldoConsolidadoRepository
            _saldoRepository;

        public ConsultarSaldoService(
            ISaldoConsolidadoRepository saldoRepository)
        {
            _saldoRepository = saldoRepository;
        }

        public async Task<SaldoDiarioResponse?>
            ObterPorDataAsync(DateOnly data)
        {
            var saldo =
                await _saldoRepository
                    .ObterPorDataAsync(data);

            if (saldo == null)
                return new SaldoDiarioResponse
                {
                    Data = data,
                    TotalCreditos = 0,
                    TotalDebitos = 0,
                    Saldo = 0,
                    UltimaAtualizacao = null
                };

            return new SaldoDiarioResponse
            {
                Data = saldo.Data,
                TotalCreditos = saldo.TotalCreditos,
                TotalDebitos = saldo.TotalDebitos,
                Saldo = saldo.Saldo,
                UltimaAtualizacao =
                    saldo.UltimaAtualizacao
            };
        }
    }
}

using FluxoCaixa.Application.DTOs;
using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Domain.Interfaces;

namespace FluxoCaixa.Application.Services
{
    public class ConsultarSaldoService : IConsultarSaldoService
    {


        private readonly ISaldoConsolidadoRepository _saldoRepository;

        public ConsultarSaldoService(ISaldoConsolidadoRepository saldoRepository)
        {
            _saldoRepository = saldoRepository ?? throw new ArgumentNullException(nameof(saldoRepository));
        }

        // Adicionado CancellationToken para repassar à consulta de I/O do banco de dados
        public async Task<SaldoDiarioResponse?> ObterPorDataAsync(DateOnly data, CancellationToken cancellationToken = default)
        {
            // Repassando o token para o repositório realizar a busca de forma segura
            var saldo = await _saldoRepository.ObterPorDataAsync(data, cancellationToken);

            if (saldo == null)
            {
                return new SaldoDiarioResponse
                {
                    Data = data,
                    TotalCreditos = 0,
                    TotalDebitos = 0,
                    Saldo = 0,
                    UltimaAtualizacao = null
                };
            }

            return new SaldoDiarioResponse
            {
                Data = saldo.Data,
                TotalCreditos = saldo.TotalCreditos,
                TotalDebitos = saldo.TotalDebitos,
                Saldo = saldo.Saldo,
                UltimaAtualizacao = saldo.UltimaAtualizacao
            };
        }
    }
}

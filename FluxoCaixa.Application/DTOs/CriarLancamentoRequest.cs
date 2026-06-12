using FluxoCaixa.Domain.Enums;

namespace FluxoCaixa.Application.DTOs
{
    public class CriarLancamentoRequest
    {
        public TipoLancamento Tipo { get; set; }

        public decimal Valor { get; set; }
    }
}

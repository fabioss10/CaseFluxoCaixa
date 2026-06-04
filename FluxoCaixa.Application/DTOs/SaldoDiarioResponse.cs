using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Application.DTOs
{
    public class SaldoDiarioResponse
    {
        public DateOnly Data { get; set; }

        public decimal TotalCreditos { get; set; }

        public decimal TotalDebitos { get; set; }

        public decimal Saldo { get; set; }

        public DateTime? UltimaAtualizacao { get; set; }
    }
}

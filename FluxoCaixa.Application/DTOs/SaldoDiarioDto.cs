using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Application.DTOs
{
    internal class SaldoDiarioDto
    {
        public DateTime Data { get; set; }
        public decimal Creditos { get; set; }
        public decimal Debitos { get; set; }
        public decimal Saldo { get; set; }
    }
}

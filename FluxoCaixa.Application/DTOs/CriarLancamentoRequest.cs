using FluxoCaixa.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Application.DTOs
{
    public class CriarLancamentoRequest
    {
        public TipoLancamento Tipo { get; set; }

        public decimal Valor { get; set; }
    }
}

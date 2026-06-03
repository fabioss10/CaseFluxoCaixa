using FluxoCaixa.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Domain.Entities
{
    public class Lancamento
    {
        public Guid Id { get; private set; }

        public TipoLancamento Tipo { get; private set; }

        public decimal Valor { get; private set; }

        public DateTime DataCriacao { get; private set; }

        private Lancamento()
        {
        }

        public Lancamento(
            TipoLancamento tipo,
            decimal valor)
        {
            if (valor <= 0)
                throw new ArgumentException(
                    "O valor deve ser maior que zero.");
            Id = Guid.CreateVersion7();
            Tipo = tipo;
            Valor = valor;
            DataCriacao = DateTime.UtcNow;
        }
    }
}

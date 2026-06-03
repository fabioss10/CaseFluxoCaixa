using FluxoCaixa.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Domain.Entities
{
    public class Lancamento
    {
        public Guid Id { get; private set; }

        public Guid UsuarioId { get; private set; }

        public TipoLancamento Tipo { get; private set; }

        public decimal Valor { get; private set; }

        public DateTime DataCriacao { get; private set; }

        private Lancamento()
        {
        }

        public Lancamento(
            Guid usuarioId,
            TipoLancamento tipo,
            decimal valor)
        {
            Id = Guid.NewGuid();
            UsuarioId = usuarioId;
            Tipo = tipo;
            Valor = valor;
            DataCriacao = DateTime.UtcNow;
        }
    }
}

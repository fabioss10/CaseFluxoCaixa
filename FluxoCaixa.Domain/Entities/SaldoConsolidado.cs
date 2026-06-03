using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Domain.Entities
{
    public class SaldoConsolidado
    {
        public Guid UsuarioId { get; private set; }

        public decimal Saldo { get; private set; }

        public DateTime UltimaAtualizacao { get; private set; }

        private SaldoConsolidado()
        {
        }

        public SaldoConsolidado(Guid usuarioId)
        {
            UsuarioId = usuarioId;
            Saldo = 0;
            UltimaAtualizacao = DateTime.UtcNow;
        }

        public void AdicionarCredito(decimal valor)
        {
            Saldo += valor;
            UltimaAtualizacao = DateTime.UtcNow;
        }

        public void AdicionarDebito(decimal valor)
        {
            Saldo -= valor;
            UltimaAtualizacao = DateTime.UtcNow;
        }
    }
}

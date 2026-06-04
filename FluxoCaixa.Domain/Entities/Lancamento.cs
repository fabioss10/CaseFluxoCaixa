using FluxoCaixa.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.X86;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FluxoCaixa.Domain.Entities
{
    public class Lancamento
    {
        /// <summary>
        /// Identificador único baseado no formato UUIDv7 (Guid Versão 7).
        /// 
        /// **Ordenação Temporal e Performance:** Diferente dos GUIDs tradicionais, o v7 incorpora um componente de data/hora (timestamp), 
        /// o que garante ordenação cronológica nativa. Isso reduz drasticamente a fragmentação de índices (page splits) em bancos relacionais, 
        /// otimizando a performance de inserção em cenários de alta concorrência.
        /// 
        /// **Arquitetura Desacoplada vs IDENTITY:** O padrão Outbox exige que o payload do evento seja gerado na aplicação contendo o ID da 
        /// entidade antes da persistência. O uso de chaves sequenciais (IDENTITY) forçaria a aplicação a esperar o retorno do banco para descobrir o ID, 
        /// gerando travas (locks) sequenciais no banco e quebrando o fluxo assíncrono.
        /// 
        /// **Idempotência Global:** O UUIDv7 funciona como uma chave de idempotência global nativa para toda a infraestrutura de mensageria, 
        /// impedindo o processamento duplicado de transações financeiras nos consumidores.
        /// </summary>
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

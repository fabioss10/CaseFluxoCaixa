using FluxoCaixa.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Domain.Entities
{

    /// <summary>
    /// Este sistema foi projetado seguindo a abordagem de **Arquitetura Evolutiva**.
    /// Iniciamos com o padrão **Transactional Outbox** local para garantir a consistência dos dados desde o primeiro dia,
    /// estruturando a base ideal para migrar para uma infraestrutura de **Mensageria Distribuída** sem alterar as regras de negócio.
    /// 
    /// A tabela Outbox é um componente de passagem, não um histórico de longo prazo. Os dados são removidos ou arquivados 
    /// após a confirmação do envio. A Outbox sempre será a única fonte da verdade para os eventos, independente do contexto 
    /// ou do broker de mensageria escolhido (seja RabbitMQ, Kafka ou outro), garantindo a integridade dos dados e a confiabilidade do sistema.
    /// 
    /// A mensageria será desacoplada, ligada estritamente à leitura desta tabela. Para cenários de crescimento elevado, 
    /// a solução pode evoluir com técnicas de particionamento por data, arquivamento de lançamentos históricos e 
    /// políticas rígidas de retenção, garantindo eficiência operacional mesmo com volumes significativamente maiores.
    /// </summary>
    public class OutboxEvent
    {
        public Guid Id { get; private set; }

        public Guid LancamentoId { get; private set; }

        public string EventType { get; private set; }

        public string Payload { get; private set; }

        public StatusEvento Status { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime? ProcessedAt { get; private set; }

        private OutboxEvent()
        {
        }

        public OutboxEvent(
            Guid aggregateId,
            string payload)
        {
            Id = Guid.CreateVersion7();
            LancamentoId = aggregateId;
            EventType = "LancamentoCriado";
            Payload = payload;
            Status = StatusEvento.Pendente;
            CreatedAt = DateTime.UtcNow;
        }

        public void MarcarComoProcessado()
        {
            Status = StatusEvento.Processado;
            ProcessedAt = DateTime.UtcNow;
        }

        public void MarcarComoErro()
        {
            Status = StatusEvento.Erro;
        }
    }
}

using FluxoCaixa.Domain.Enums;

namespace FluxoCaixa.Domain.Entities
{


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

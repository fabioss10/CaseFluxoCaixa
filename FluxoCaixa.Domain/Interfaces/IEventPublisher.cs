using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Domain.Interfaces
{
    //a abstração permite evolução futura para RabbitMQ, Kafka ou Azure Service Bus sem impacto nas regras de negócio.
    public interface IEventPublisher
    {
        Task PublishAsync(string eventType, string payload);
    }
}

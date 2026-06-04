using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Application.Interfaces
{
    public interface IProcessadorOutboxService
    {
        Task ProcessarAsync(CancellationToken cancellationToken);
    }
}

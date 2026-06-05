using FluxoCaixa.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FluxoCaixa.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SaldosController : ControllerBase
    {
        private readonly IConsultarSaldoService _service;

        public SaldosController(IConsultarSaldoService service)
        {
            //conceito fail fast
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        [HttpGet("{data}", Name = "ObterSaldoPorData")]
        public async Task<IActionResult> ObterPorData(string data, CancellationToken cancellationToken)
        {
            if (!DateOnly.TryParse(data, out var dataConsulta))
            {
                return BadRequest("Formato de data inválido. Use o padrão YYYY-MM-DD.");
            }

            // Repassando o token de cancelamento HTTP até o repositório de leitura
            var saldo = await _service.ObterPorDataAsync(dataConsulta, cancellationToken);

            if (saldo == null)
            {
                return NotFound();
            }

            return Ok(saldo);
        }
    }
}

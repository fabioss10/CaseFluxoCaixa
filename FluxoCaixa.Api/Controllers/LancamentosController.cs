using FluxoCaixa.Application.DTOs;
using FluxoCaixa.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FluxoCaixa.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LancamentosController : ControllerBase
    {
        private readonly ICriarLancamentoService _service;

        public LancamentosController(ICriarLancamentoService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] CriarLancamentoRequest request)
        {
            var id = await _service.ExecutarAsync(request);
            return Ok(id);
        }
    }
    [ApiController]
    [Route("api/saldos")]
    public class SaldosController
        : ControllerBase
    {
        private readonly IConsultarSaldoService
            _service;

        public SaldosController(
            IConsultarSaldoService service)
        {
            _service = service;
        }

        [HttpGet("{data}")]
        public async Task<IActionResult>
            ObterPorData(string data)
        {
            if (!DateOnly.TryParse(
                    data,
                    out var dataConsulta))
            {
                return BadRequest(
                    "Data inválida.");
            }

            var saldo =
                await _service
                    .ObterPorDataAsync(
                        dataConsulta);

            if (saldo == null)
                return NotFound();

            return Ok(saldo);
        }
    }
}


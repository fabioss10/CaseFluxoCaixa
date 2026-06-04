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
}

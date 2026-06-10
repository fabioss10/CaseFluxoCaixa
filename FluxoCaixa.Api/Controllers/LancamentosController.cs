using FluxoCaixa.Application.DTOs;
using FluxoCaixa.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization; 
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FluxoCaixa.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LancamentosController : ControllerBase
    {
        private readonly ICriarLancamentoService _service;

        public LancamentosController(ICriarLancamentoService service)
        {
            //conceito fail fast
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        
        
        [HttpPost]
        [Authorize(Policy = "EscopoGrava")] // Aplica OAuth 2.0 + RBAC (Apenas escrita)
        public async Task<IActionResult> Criar([FromBody] CriarLancamentoRequest request, CancellationToken cancellationToken)
        {
            var id = await _service.ExecutarAsync(request, cancellationToken);

            var dataHojeFormatada = DateTime.Today.ToString("yyyy-MM-dd");

            
            return Ok(new
            {
                Resultado="inserido com sucesso",
                Id = id,
                UrlConsultaSaldoDoDia = $"/api/saldos/{dataHojeFormatada}"
            });
        }

    }
    
}

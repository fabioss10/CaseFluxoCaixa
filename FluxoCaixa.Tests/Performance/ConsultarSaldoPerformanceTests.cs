using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace FluxoCaixa.Tests.Performance
{
    public class ConsultarSaldoPerformanceTests
    {
        [Fact]
        public void ExecutarTesteDeCarga_DeveSuportar50RequisicoesPorSegundo_ComMaximo5PorCentoDeFalhas()
        {
            // 1. ARRANGE: Configura o cliente HTTP e a data de teste
            var httpClient = new HttpClient();
            var dataTeste = DateTime.Today.ToString("yyyy-MM-dd");

            
            var cenario = Scenario.Create("consultar_saldo_diario", async context =>
            {
                try
                {
                    // Altere a porta (7248) para a porta real em que o seu Swagger roda localmente
                    var response = await httpClient.GetAsync($"https://localhost:7248/api/saldos/{dataTeste}");

                    // Se responder HTTP 200 OK ou HTTP 404 NotFound (data sem saldo), a requisição foi bem-sucedida
                    if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return Response.Ok();
                }
                catch
                {
                    return Response.Fail();
                }

                return Response.Fail();
            })
            // 2. ACT: Configura para injetar EXATAMENTE 50 requisições por segundo (RPS) durante 30 segundos
            .WithLoadSimulations(
                Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
            );

            // Executa o motor do teste de carga
            var resultado = NBomberRunner
                .RegisterScenarios(cenario)
                .Run();

            // 3. ASSERT: O juiz valida se a taxa de erro cumpre os critérios " 50 requisições por segundo, com no máximo 5% de perda de requisições." 
            var totalRequisicoes = resultado.AllFailCount + resultado.AllOkCount;

            // Proteção contra divisão por zero caso o teste não execute nenhuma chamada
            if (totalRequisicoes == 0) totalRequisicoes = 1;

            double percentualFalhas = ((double)resultado.AllFailCount / totalRequisicoes) * 100;

            // Valida o requisito de negócio restrito: falhas não podem passar de 5%
            Assert.True(percentualFalhas <= 5.0, $"O índice de perda foi de {percentualFalhas}%, superando o limite tolerável de 5%.");
        }
    }
}

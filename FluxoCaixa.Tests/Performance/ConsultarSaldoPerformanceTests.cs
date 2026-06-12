using NBomber.CSharp;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FluxoCaixa.Tests.Performance
{
    public class ConsultarSaldoPerformanceTests
    {
        [Fact]
        [Trait("Category", "Performance")]
        public async Task ExecutarTesteDeCarga_DeveSuportar50RequisicoesPorSegundo_ComMaximo5PorCentoDeFalhas()
        {
            // 1. ARRANGE: Abordagem compatível com .NET 10 para desligar a checagem de SSL no localhost
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
            };
            var httpClient = new HttpClient(handler);

            // Busca dinamicamente a URL da variável de ambiente, ou usa o fallback com HTTP
            string baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:7248";

            var dataTeste = DateTime.Today.ToString("yyyy-MM-dd");
            string tokenJwt = string.Empty;

            // Executa o login assíncrono real
            try
            {
                var loginResponse = await httpClient.PostAsync($"{baseUrl}/api/Auth/login-somente-leitura", null);
                if (loginResponse.IsSuccessStatusCode)
                {
                    var jsonString = await loginResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    tokenJwt = doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Não foi possível obter o token JWT para o teste de performance: {ex.Message}");
            }

            if (string.IsNullOrEmpty(tokenJwt))
            {
                throw new InvalidOperationException("O token JWT retornado pelo AuthController está vazio ou inválido.");
            }

            // Injeta o token permanentemente no cabeçalho padrão desta instância do HttpClient
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenJwt);

            var cenario = Scenario.Create("consultar_saldo_diario", async context =>
            {
                try
                {
                    // Realiza a chamada passando o cabeçalho Authorization já configurado
                    var response = await httpClient.GetAsync($"{baseUrl}/api/saldos/{dataTeste}");

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

            // 3. ASSERT: O juiz valida se a taxa de erro cumpre os critérios
            var totalRequisicoes = resultado.AllFailCount + resultado.AllOkCount;

            // Proteção contra divisão por zero caso o teste não execute nenhuma chamada
            if (totalRequisicoes == 0) totalRequisicoes = 1;

            double percentualFalhas = ((double)resultado.AllFailCount / totalRequisicoes) * 100;

            // Valida o requisito de negócio restrito: falhas não podem passar de 5%
            Assert.True(percentualFalhas <= 5.0, $"O índice de perda foi de {percentualFalhas}%, superando o limite tolerável de 5%.");
        }
    }
}

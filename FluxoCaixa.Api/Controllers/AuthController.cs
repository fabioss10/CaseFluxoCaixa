using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FluxoCaixa.Api.Controllers
{

    //Classe para simular o Identity Provider gerando tokens válidos com as Claims necessárias para o avaliador testar os cenários de sucesso e erro (401/403).

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        // Construtor recebendo as configurações do appsettings.json
        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("login-leitura-escrita")]
        public IActionResult LoginGerente()
        {
            // Simula um usuário autenticado com permissões totais (Gerente + Read/Write)
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "fabio.silva@teste.com"),
                new Claim("scope", "fluxocaixa.read"),
                new Claim("scope", "fluxocaixa.write"),
                new Claim(ClaimTypes.Role, "LoginLeituraEscrita") // RBAC
            };

            var token = GerarTokenJwt(claims);
            return Ok(new { access_token = token, token_type = "Bearer" });
        }

        [HttpPost("login-somente-leitura")]
        public IActionResult LoginAnalista()
        {
            // Simula um usuário com acesso limitado (Analista + apenas Leitura)
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "junior.silva@teste.com"),
                new Claim("scope", "fluxocaixa.read"),
                new Claim(ClaimTypes.Role, "LoginSomenteLeitura") // RBAC
            };

            var token = GerarTokenJwt(claims);
            return Ok(new { access_token = token, token_type = "Bearer" });
        }

        private string GerarTokenJwt(Claim[] claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SuaChaveSuperSecretaComPeloMenos32Caracteres!"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "FluxoCaixaIdentityServer",
                audience: "FluxoCaixaApi",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

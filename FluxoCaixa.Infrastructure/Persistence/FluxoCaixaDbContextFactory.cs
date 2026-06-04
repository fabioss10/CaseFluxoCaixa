using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

/// <summary>
/// classe criada para o funcionamento do migrations do Entity Framework Core,
/// permitindo que o EF Core encontre a string de conexão e as configurações necessárias para criar o contexto de banco de dados durante o processo de migração.
/// </summary>

namespace FluxoCaixa.Infrastructure.Persistence
{
    public class FluxoCaixaDbContextFactory : IDesignTimeDbContextFactory<FluxoCaixaDbContext>
    {
        public FluxoCaixaDbContext CreateDbContext(string[] args)
        {
            // 1. Define o caminho para buscar o appsettings.json no projeto da API
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "../FluxoCaixa.API");

            // 2. Carrega o arquivo de configuração
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 3. Obtém a string de conexão 
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<FluxoCaixaDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new FluxoCaixaDbContext(optionsBuilder.Options);
        }
    }
}
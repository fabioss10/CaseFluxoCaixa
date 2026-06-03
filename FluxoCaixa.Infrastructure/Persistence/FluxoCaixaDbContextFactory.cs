using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;


namespace FluxoCaixa.Infrastructure.Persistence
{
    public class FluxoCaixaDbContextFactory
    : IDesignTimeDbContextFactory<FluxoCaixaDbContext>
    {
        public FluxoCaixaDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FluxoCaixaDbContext>();

            optionsBuilder.UseSqlServer(
                "Server=localhost\\SQLEXPRESS;Database=FluxoCaixaDb;Trusted_Connection=True;TrustServerCertificate=True");

            return new FluxoCaixaDbContext(optionsBuilder.Options);
        }
    }
}

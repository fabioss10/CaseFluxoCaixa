using FluxoCaixa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Infrastructure.Persistence
{
    public class FluxoCaixaDbContext : DbContext
    {
        public FluxoCaixaDbContext(DbContextOptions<FluxoCaixaDbContext> options)
            : base(options)
        {
        }

        public DbSet<Lancamento> Lancamentos { get; set; }
        public DbSet<OutboxEvent> OutboxEvents { get; set; }

        public DbSet<SaldoConsolidado> SaldosConsolidados { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SaldoConsolidado>()
                .HasKey(x => x.Data);

            // Mapeamento do Índice Filtrado de Alta Performance
            modelBuilder.Entity<OutboxEvent>(entity =>
            {
                entity.HasIndex(x => x.CreatedAt)
                      .HasDatabaseName("IX_OutboxEvents_Status_Pendente_Erro")
                      .HasFilter("[Status] = 1 OR [Status] = 3");
            });
        }

    }
}

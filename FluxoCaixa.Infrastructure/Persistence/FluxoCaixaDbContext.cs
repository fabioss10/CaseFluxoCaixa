using FluxoCaixa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

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
        }

    }
}

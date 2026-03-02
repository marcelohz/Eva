using Microsoft.EntityFrameworkCore;
using Eva.Models;

namespace Eva.Data
{
    public partial class EvaDbContext : DbContext
    {
        public EvaDbContext(DbContextOptions<EvaDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Papel> Papeis { get; set; }
        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Veiculo> Veiculos { get; set; }
        public DbSet<Motorista> Motoristas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Default schema for web-related tables
            modelBuilder.HasDefaultSchema("web");

            // Define Papel primary key
            modelBuilder.Entity<Papel>()
                .HasKey(p => p.Nome);

            // Map entities to their specific schemas
            modelBuilder.Entity<Empresa>()
                .ToTable("empresa", "geral");

            modelBuilder.Entity<Veiculo>()
                .ToTable("veiculo", "geral");

            modelBuilder.Entity<Motorista>()
                .ToTable("motorista", "eventual");
        }
    }
}
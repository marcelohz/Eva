using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Eva.Models;

namespace Eva.Data
{
    public partial class EvaDbContext : DbContext
    {
        private readonly string? _empresaCnpj;
        private readonly bool _isAnalista;

        public EvaDbContext(DbContextOptions<EvaDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            // Intercept the current web request to find out who is making the database query
            var user = httpContextAccessor.HttpContext?.User;

            _isAnalista = user?.IsInRole("ANALISTA") ?? false;

            // We look for a custom claim containing the CNPJ
            _empresaCnpj = user?.FindFirstValue("EmpresaCnpj");
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

            // =========================================================
            // GLOBAL QUERY FILTERS (The Magic Multi-Tenant Security)
            // =========================================================

            // Entity Framework will invisibly append this WHERE clause to EVERY query
            modelBuilder.Entity<Veiculo>()
                .HasQueryFilter(v => _isAnalista || v.EmpresaCnpj == _empresaCnpj);

            modelBuilder.Entity<Motorista>()
                .HasQueryFilter(m => _isAnalista || m.EmpresaCnpj == _empresaCnpj);

            modelBuilder.Entity<Empresa>()
                .HasQueryFilter(e => _isAnalista || e.Cnpj == _empresaCnpj);
        }
    }
}
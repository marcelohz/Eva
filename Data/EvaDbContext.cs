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
            var user = httpContextAccessor.HttpContext?.User;
            _isAnalista = user?.IsInRole("ANALISTA") ?? false;
            _empresaCnpj = user?.FindFirstValue("EmpresaCnpj");
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Papel> Papeis { get; set; }
        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Veiculo> Veiculos { get; set; }
        public DbSet<Motorista> Motoristas { get; set; }
        public DbSet<FluxoPendencia> FluxoPendencias { get; set; }
        public DbSet<VPendenciaAtual> VPendenciasAtuais { get; set; }

        // --- NEW DOCUMENT DBSETS ---
        public DbSet<Documento> Documentos { get; set; }
        public DbSet<DocumentoEmpresa> DocumentoEmpresas { get; set; }
        public DbSet<DocumentoVeiculo> DocumentoVeiculos { get; set; }
        public DbSet<DocumentoMotorista> DocumentoMotoristas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("web");

            modelBuilder.Entity<Papel>().HasKey(p => p.Nome);
            modelBuilder.Entity<Empresa>().ToTable("empresa", "geral");
            modelBuilder.Entity<Veiculo>().ToTable("veiculo", "geral");
            modelBuilder.Entity<Motorista>().ToTable("motorista", "eventual");

            modelBuilder.Entity<VPendenciaAtual>()
                .ToView("v_pendencia_atual", "eventual")
                .HasKey(v => v.Id);

            // GLOBAL QUERY FILTERS
            modelBuilder.Entity<Veiculo>()
                .HasQueryFilter(v => _isAnalista || v.EmpresaCnpj == _empresaCnpj);

            modelBuilder.Entity<Motorista>()
                .HasQueryFilter(m => _isAnalista || m.EmpresaCnpj == _empresaCnpj);

            modelBuilder.Entity<Empresa>()
                .HasQueryFilter(e => _isAnalista || e.Cnpj == _empresaCnpj);

            // Note: We generally don't apply Global Query Filters to Documentos directly
            // because they are accessed via the parent entity (Veiculo/Empresa) which is already filtered.
        }
    }
}
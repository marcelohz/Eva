using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Eva.Models;

namespace Eva.Data
{
    public class EvaDbContext : DbContext
    {
        private readonly string? _empresaCnpj;
        private readonly string? _userEmail;
        private readonly bool _hasTotalAccess;
        private readonly bool _isEmpresaMaster;

        public EvaDbContext(DbContextOptions<EvaDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            var user = httpContextAccessor.HttpContext?.User;

            // Access control flags
            _hasTotalAccess = (user?.IsInRole("ANALISTA") ?? false) || (user?.IsInRole("ADMIN") ?? false);
            _isEmpresaMaster = user?.IsInRole("EMPRESA") ?? false;
            _empresaCnpj = user?.FindFirstValue("EmpresaCnpj");
            _userEmail = user?.FindFirstValue(ClaimTypes.Email) ?? user?.Identity?.Name;

            // Initialize DbSets to satisfy non-nullable warnings
            Usuarios = Set<Usuario>();
            Papeis = Set<Papel>();
            Empresas = Set<Empresa>();
            Veiculos = Set<Veiculo>();
            Motoristas = Set<Motorista>();
            FluxoPendencias = Set<FluxoPendencia>();
            VPendenciasAtuais = Set<VPendenciaAtual>();
            Documentos = Set<Documento>();
            DocumentoEmpresas = Set<DocumentoEmpresa>();
            DocumentoVeiculos = Set<DocumentoVeiculo>();
            DocumentoMotoristas = Set<DocumentoMotorista>();
            TokensValidacaoEmail = Set<TokenValidacaoEmail>();
            DocumentoTipos = Set<DocumentoTipo>();
            DocumentoTipoPermissoes = Set<DocumentoTipoPermissao>();
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Papel> Papeis { get; set; }
        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Veiculo> Veiculos { get; set; }
        public DbSet<Motorista> Motoristas { get; set; }
        public DbSet<FluxoPendencia> FluxoPendencias { get; set; }
        public DbSet<VPendenciaAtual> VPendenciasAtuais { get; set; }
        public DbSet<Documento> Documentos { get; set; }
        public DbSet<DocumentoEmpresa> DocumentoEmpresas { get; set; }
        public DbSet<DocumentoVeiculo> DocumentoVeiculos { get; set; }
        public DbSet<DocumentoMotorista> DocumentoMotoristas { get; set; }
        public DbSet<TokenValidacaoEmail> TokensValidacaoEmail { get; set; }
        public DbSet<DocumentoTipo> DocumentoTipos { get; set; }
        public DbSet<DocumentoTipoPermissao> DocumentoTipoPermissoes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("web");

            // Table Mappings
            modelBuilder.Entity<Papel>().HasKey(p => p.Nome);
            modelBuilder.Entity<Empresa>().ToTable("empresa", "geral");
            modelBuilder.Entity<Veiculo>().ToTable("veiculo", "geral");
            modelBuilder.Entity<Motorista>().ToTable("motorista", "eventual");
            modelBuilder.Entity<VPendenciaAtual>().ToView("v_pendencia_atual", "eventual").HasKey(v => v.Id);
            modelBuilder.Entity<TokenValidacaoEmail>().ToTable("token_validacao_email", "web");

            modelBuilder.Entity<DocumentoTipoPermissao>()
                .HasKey(dtp => new { dtp.TipoNome, dtp.EntidadeTipo });

            // Token Configuration
            modelBuilder.Entity<TokenValidacaoEmail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasOne(d => d.Usuario)
                    .WithMany()
                    .HasForeignKey(d => d.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Global Query Filters for Data Isolation
            modelBuilder.Entity<Veiculo>().HasQueryFilter(v => _hasTotalAccess || v.EmpresaCnpj == _empresaCnpj);
            modelBuilder.Entity<Motorista>().HasQueryFilter(m => _hasTotalAccess || m.EmpresaCnpj == _empresaCnpj);
            modelBuilder.Entity<Empresa>().HasQueryFilter(e => _hasTotalAccess || e.Cnpj == _empresaCnpj);

            modelBuilder.Entity<Usuario>().HasQueryFilter(u => _hasTotalAccess ||
                                                              (_isEmpresaMaster && u.EmpresaCnpj == _empresaCnpj) ||
                                                              u.Email == _userEmail);
        }
    }
}
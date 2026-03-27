using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Security.Claims;
using System;
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

            _hasTotalAccess = (user?.IsInRole("ANALISTA") ?? false) || (user?.IsInRole("ADMIN") ?? false);
            _isEmpresaMaster = user?.IsInRole("EMPRESA") ?? false;
            _empresaCnpj = user?.FindFirstValue("EmpresaCnpj");
            _userEmail = user?.FindFirstValue(ClaimTypes.Email) ?? user?.Identity?.Name;

            Usuarios = Set<Usuario>();
            Papeis = Set<Papel>();
            Empresas = Set<Empresa>();
            Veiculos = Set<Veiculo>();
            Motoristas = Set<Motorista>();
            Documentos = Set<Documento>();
            Submissoes = Set<Submissao>();
            SubmissaoDados = Set<SubmissaoDados>();
            SubmissaoDocumentos = Set<SubmissaoDocumento>();
            EntidadeDocumentosAtuais = Set<EntidadeDocumentoAtual>();
            SubmissaoEventos = Set<SubmissaoEvento>();
            TokensValidacaoEmail = Set<TokenValidacaoEmail>();
            DocumentoTipos = Set<DocumentoTipo>();
            DocumentoTipoVinculos = Set<DocumentoTipoVinculo>();

            Viagens = Set<Viagem>();
            ViagemTipos = Set<ViagemTipo>();
            Passageiros = Set<Passageiro>();
            DocumentoViagens = Set<DocumentoViagem>();
            Regioes = Set<Regiao>();
            Municipios = Set<Municipio>();
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Papel> Papeis { get; set; }
        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Veiculo> Veiculos { get; set; }
        public DbSet<Motorista> Motoristas { get; set; }
        public DbSet<Documento> Documentos { get; set; }
        public DbSet<Submissao> Submissoes { get; set; }
        public DbSet<SubmissaoDados> SubmissaoDados { get; set; }
        public DbSet<SubmissaoDocumento> SubmissaoDocumentos { get; set; }
        public DbSet<EntidadeDocumentoAtual> EntidadeDocumentosAtuais { get; set; }
        public DbSet<SubmissaoEvento> SubmissaoEventos { get; set; }
        public DbSet<TokenValidacaoEmail> TokensValidacaoEmail { get; set; }
        public DbSet<DocumentoTipo> DocumentoTipos { get; set; }
        public DbSet<DocumentoTipoVinculo> DocumentoTipoVinculos { get; set; }
        public DbSet<Viagem> Viagens { get; set; }
        public DbSet<ViagemTipo> ViagemTipos { get; set; }
        public DbSet<Passageiro> Passageiros { get; set; }
        public DbSet<DocumentoViagem> DocumentoViagens { get; set; }
        public DbSet<Regiao> Regioes { get; set; }
        public DbSet<Municipio> Municipios { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            configurationBuilder
                .Properties<DateTime>()
                .HaveConversion<UtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("web");

            modelBuilder.Entity<Papel>().HasKey(p => p.Nome);
            modelBuilder.Entity<Empresa>().ToTable("empresa", "geral");
            modelBuilder.Entity<Veiculo>().ToTable("veiculo", "geral");
            modelBuilder.Entity<Motorista>().ToTable("motorista", "eventual");
            modelBuilder.Entity<Regiao>().ToTable("regiao", "geral");
            modelBuilder.Entity<Municipio>().ToTable("municipio", "geral");
            modelBuilder.Entity<TokenValidacaoEmail>().ToTable("token_validacao_email", "web");
            modelBuilder.Entity<Submissao>().ToTable("submissao", "eventual");
            modelBuilder.Entity<SubmissaoDados>().ToTable("submissao_dados", "eventual");
            modelBuilder.Entity<SubmissaoDocumento>().ToTable("submissao_documento", "eventual");
            modelBuilder.Entity<EntidadeDocumentoAtual>().ToTable("entidade_documento_atual", "eventual");
            modelBuilder.Entity<SubmissaoEvento>().ToTable("submissao_evento", "eventual");

            // --- PROTEÇÃO DE SCHEMA E REGRAS ---
            modelBuilder.Entity<DocumentoTipo>().ToTable("documento_tipo", "eventual");
            modelBuilder.Entity<DocumentoTipoVinculo>().ToTable("documento_tipo_vinculo", "eventual");

            modelBuilder.Entity<DocumentoTipoVinculo>()
                .HasKey(dtv => new { dtv.TipoNome, dtv.EntidadeTipo });

            modelBuilder.Entity<Submissao>()
                .HasIndex(s => new { s.EntidadeTipo, s.EntidadeId, s.Status });

            modelBuilder.Entity<SubmissaoDados>()
                .HasIndex(sd => sd.SubmissaoId)
                .IsUnique();

            modelBuilder.Entity<EntidadeDocumentoAtual>()
                .HasIndex(eda => new { eda.EntidadeTipo, eda.EntidadeId, eda.DocumentoTipoNome });

            modelBuilder.Entity<TokenValidacaoEmail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasOne(d => d.Usuario)
                    .WithMany()
                    .HasForeignKey(d => d.UsuarioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Passageiro>()
                .HasOne(p => p.Viagem)
                .WithMany(v => v.Passageiros)
                .HasForeignKey(p => p.ViagemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Veiculo>().HasQueryFilter(v => _hasTotalAccess || v.EmpresaCnpj == _empresaCnpj);
            modelBuilder.Entity<Motorista>().HasQueryFilter(m => _hasTotalAccess || m.EmpresaCnpj == _empresaCnpj);
            modelBuilder.Entity<Empresa>().HasQueryFilter(e => _hasTotalAccess || e.Cnpj == _empresaCnpj);

            modelBuilder.Entity<Viagem>().HasQueryFilter(v => _hasTotalAccess || v.EmpresaCnpj == _empresaCnpj);

            // Phase 1: Match child query filter for Passageiro to eliminate EF Core warnings
            modelBuilder.Entity<Passageiro>().HasQueryFilter(p => _hasTotalAccess || (p.Viagem != null && p.Viagem.EmpresaCnpj == _empresaCnpj));

            // Phase 1: Apply global Ativo filter and maintain multi-tenancy access rules
            modelBuilder.Entity<Usuario>().HasQueryFilter(u => u.Ativo &&
                                                              (_hasTotalAccess ||
                                                              (_isEmpresaMaster && u.EmpresaCnpj == _empresaCnpj) ||
                                                              u.Email == _userEmail));

            // Phase 1: Match child query filter for Token to eliminate EF Core warnings
            modelBuilder.Entity<TokenValidacaoEmail>().HasQueryFilter(t => t.Usuario != null &&
                                                                           t.Usuario.Ativo &&
                                                                           (_hasTotalAccess ||
                                                                           (_isEmpresaMaster && t.Usuario.EmpresaCnpj == _empresaCnpj) ||
                                                                           t.Usuario.Email == _userEmail));
        }
    }

    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(
                v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }
}

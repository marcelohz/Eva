using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Services;

[Trait("Category", "integration")]
public class PendenciaServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AvancarEntidadeAsync_deve_criar_status_incompleto_quando_documento_obrigatorio_estiver_faltando()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var service = new PendenciaService(context, new FakeBackgroundJobClient());

        await service.AvancarEntidadeAsync("EMPRESA", "12345678000199");

        var atual = await context.VPendenciasAtuais
            .FirstOrDefaultAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.NotNull(atual);
        Assert.Equal("INCOMPLETO", atual!.Status);
        Assert.Null(atual.Analista);
    }

    [Fact]
    public async Task SalvarDadosPropostosAsync_deve_atualizar_ticket_existente_em_incompleto()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var service = new PendenciaService(context, new FakeBackgroundJobClient());

        await service.SalvarDadosPropostosAsync("EMPRESA", "12345678000199", """{"nome":"Empresa Draft 1"}""");
        await service.SalvarDadosPropostosAsync("EMPRESA", "12345678000199", """{"nome":"Empresa Draft 2"}""");

        var historico = await context.FluxoPendencias
            .Where(f => f.EntidadeTipo == "EMPRESA" && f.EntidadeId == "12345678000199")
            .OrderBy(f => f.Id)
            .ToListAsync();

        Assert.Single(historico);
        Assert.Equal("INCOMPLETO", historico[0].Status);
        Assert.Equal("""{"nome":"Empresa Draft 2"}""", historico[0].DadosPropostos);
    }

    [Fact]
    public async Task AprovarAsync_deve_aplicar_dados_propostos_e_vincular_documento_mais_recente()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");
        await AddEmpresaDocumentoAsync(
            context,
            "12345678000199",
            "CARTAO_CNPJ",
            DateTime.UtcNow.AddDays(-3));

        var oldDoc = await AddEmpresaDocumentoAsync(
            context,
            "12345678000199",
            "CARTAO_CNPJ",
            DateTime.UtcNow.AddDays(-2));

        var newDoc = await AddEmpresaDocumentoAsync(
            context,
            "12345678000199",
            "CARTAO_CNPJ",
            DateTime.UtcNow.AddDays(-1));

        var service = new PendenciaService(context, new FakeBackgroundJobClient());

        await service.SalvarDadosPropostosAsync("EMPRESA", "12345678000199", """{"nome":"Empresa Atualizada"}""");

        var docsAguardando = await context.Documentos
            .Where(d => d.Id == oldDoc.Id || d.Id == newDoc.Id)
            .OrderBy(d => d.Id)
            .ToListAsync();
        var ticketAguardando = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.Equal("AGUARDANDO_ANALISE", ticketAguardando.Status);
        Assert.Null(docsAguardando.Single(d => d.Id == oldDoc.Id).FluxoPendenciaId);
        Assert.Equal(ticketAguardando.Id, docsAguardando.Single(d => d.Id == newDoc.Id).FluxoPendenciaId);

        await service.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");
        await service.AprovarAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");

        var empresa = await context.Empresas.IgnoreQueryFilters().FirstAsync(e => e.Cnpj == "12345678000199");
        var docs = await context.Documentos
            .Where(d => d.Id == oldDoc.Id || d.Id == newDoc.Id)
            .OrderBy(d => d.Id)
            .ToListAsync();
        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.Equal("Empresa Atualizada", empresa.Nome);
        Assert.Equal("APROVADO", atual.Status);
        Assert.NotNull(docs.Single(d => d.Id == oldDoc.Id).FluxoPendenciaId);
        Assert.NotNull(docs.Single(d => d.Id == newDoc.Id).FluxoPendenciaId);
        Assert.NotNull(docs.Single(d => d.Id == oldDoc.Id).AprovadoEm);
    }

    [Fact]
    public async Task AprovarAsync_deve_aplicar_dados_corrigidos_apos_rejeicao_e_ressubmissao()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");
        await AddEmpresaDocumentoAsync(
            context,
            "12345678000199",
            "CARTAO_CNPJ",
            DateTime.UtcNow.AddDays(-1));

        var service = new PendenciaService(context, new FakeBackgroundJobClient());

        await service.AvancarEntidadeAsync("EMPRESA", "12345678000199");
        await service.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");
        await service.RejeitarAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br", "Corrigir razÃ£o social");

        await service.SalvarDadosPropostosAsync("EMPRESA", "12345678000199", """{"nome":"Empresa Corrigida","nomeFantasia":"Empresa Corrigida"}""");

        var reaberto = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.Equal("AGUARDANDO_ANALISE", reaberto.Status);
        Assert.Contains("Empresa Corrigida", reaberto.DadosPropostos);

        await service.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");
        await service.AprovarAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");

        var empresa = await context.Empresas.IgnoreQueryFilters().FirstAsync(e => e.Cnpj == "12345678000199");
        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.Equal("Empresa Corrigida", empresa.Nome);
        Assert.Equal("Empresa Corrigida", empresa.NomeFantasia);
        Assert.Equal("APROVADO", atual.Status);
    }

    [Fact]
    public async Task RejeitarAsync_deve_registrar_motivo_e_status_final()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var service = new PendenciaService(context, new FakeBackgroundJobClient());

        await service.AvancarEntidadeAsync("EMPRESA", "12345678000199");
        await service.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");
        await service.RejeitarAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br", "Documento inválido");

        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.Equal("REJEITADO", atual.Status);
        Assert.Equal("Documento inválido", atual.Motivo);
        Assert.Equal("analista@metroplan.rs.gov.br", atual.Analista);
    }

    [Fact]
    public async Task SalvarDadosPropostosAsync_deve_reabrir_empresa_rejeitada_em_incompleto_quando_documento_obrigatorio_estiver_faltando()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var service = new PendenciaService(context, new FakeBackgroundJobClient());

        await service.AvancarEntidadeAsync("EMPRESA", "12345678000199");
        await service.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");
        await service.RejeitarAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br", "Corrigir cadastro");

        await service.SalvarDadosPropostosAsync("EMPRESA", "12345678000199", """{"nome":"Empresa Corrigida"}""");

        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");
        var historico = await context.FluxoPendencias
            .Where(f => f.EntidadeTipo == "EMPRESA" && f.EntidadeId == "12345678000199")
            .OrderBy(f => f.Id)
            .ToListAsync();

        Assert.Equal("INCOMPLETO", atual.Status);
        Assert.Null(atual.Analista);
        Assert.Contains("Empresa Corrigida", atual.DadosPropostos);
        Assert.Equal(4, historico.Count);
        Assert.Equal("REJEITADO", historico[^2].Status);
        Assert.Equal("INCOMPLETO", historico[^1].Status);
    }

    [Fact]
    public async Task SalvarDadosPropostosAsync_deve_reabrir_veiculo_rejeitado_em_aguardando_analise_quando_documentacao_estiver_completa()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");
        context.Veiculos.Add(new Veiculo
        {
            Placa = "ABC1D23",
            EmpresaCnpj = "12345678000199",
            Modelo = "Ônibus"
        });
        await context.SaveChangesAsync();
        await AddVeiculoDocumentoAsync(context, "ABC1D23", "CRLV", DateTime.UtcNow.AddDays(-1));

        var service = new PendenciaService(context, new FakeBackgroundJobClient());

        await service.AvancarEntidadeAsync("VEICULO", "ABC1D23");
        await service.IniciarAnaliseAsync("VEICULO", "ABC1D23", "analista@metroplan.rs.gov.br");
        await service.RejeitarAsync("VEICULO", "ABC1D23", "analista@metroplan.rs.gov.br", "Corrigir placa");

        await service.SalvarDadosPropostosAsync("VEICULO", "ABC1D23", """{"modelo":"Ônibus Corrigido"}""");

        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "VEICULO" && p.EntidadeId == "ABC1D23");

        Assert.Equal("AGUARDANDO_ANALISE", atual.Status);
        Assert.Contains("Ônibus Corrigido", atual.DadosPropostos);
    }

    [Fact]
    public async Task SalvarDadosPropostosAsync_deve_reabrir_motorista_rejeitado_em_aguardando_analise_quando_documentacao_estiver_completa()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var motorista = new Motorista
        {
            EmpresaCnpj = "12345678000199",
            Nome = "João da Silva",
            Cpf = "12345678901",
            Cnh = "1234567890"
        };
        context.Motoristas.Add(motorista);
        await context.SaveChangesAsync();
        await AddMotoristaDocumentoAsync(context, motorista.Id, "CNH", DateTime.UtcNow.AddDays(-1));

        var service = new PendenciaService(context, new FakeBackgroundJobClient());

        await service.AvancarEntidadeAsync("MOTORISTA", motorista.Id.ToString());
        await service.IniciarAnaliseAsync("MOTORISTA", motorista.Id.ToString(), "analista@metroplan.rs.gov.br");
        await service.RejeitarAsync("MOTORISTA", motorista.Id.ToString(), "analista@metroplan.rs.gov.br", "Corrigir CNH");

        await service.SalvarDadosPropostosAsync("MOTORISTA", motorista.Id.ToString(), """{"nome":"João Corrigido"}""");

        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "MOTORISTA" && p.EntidadeId == motorista.Id.ToString());

        Assert.Equal("AGUARDANDO_ANALISE", atual.Status);
        Assert.Contains("João Corrigido", atual.DadosPropostos);
    }

    [Fact]
    public async Task AprovarAsync_deve_enfileirar_email_de_aprovacao_para_empresa()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var jobs = new RecordingBackgroundJobClient();
        var service = new PendenciaService(context, jobs);

        await service.AvancarEntidadeAsync("EMPRESA", "12345678000199");
        await service.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");
        await service.AprovarAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");

        var job = Assert.Single(jobs.EnqueuedJobs);
        Assert.Equal("SendEmailAsync", job.Method.Name);
        Assert.Equal("empresa@teste.com", job.Args[0]);
        Assert.Contains("Aprova", job.Args[1]?.ToString());
        Assert.Contains("Aprovado", job.Args[2]?.ToString());
    }

    [Fact]
    public async Task RejeitarAsync_deve_enfileirar_email_de_pendencia_com_motivo()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var jobs = new RecordingBackgroundJobClient();
        var service = new PendenciaService(context, jobs);

        await service.AvancarEntidadeAsync("EMPRESA", "12345678000199");
        await service.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");
        await service.RejeitarAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br", "Documento inválido");

        var job = Assert.Single(jobs.EnqueuedJobs);
        Assert.Equal("SendEmailAsync", job.Method.Name);
        Assert.Equal("empresa@teste.com", job.Args[0]);
        Assert.Contains("Pend", job.Args[1]?.ToString());
        Assert.Contains("Documento", job.Args[2]?.ToString());
    }

    private static async Task SeedEmpresaAsync(Eva.Data.EvaDbContext context, string cnpj, string email)
    {
        context.Empresas.Add(new Empresa
        {
            Cnpj = cnpj,
            Nome = "Empresa Teste",
            NomeFantasia = "Empresa Teste",
            Email = email
        });

        context.Usuarios.Add(new Usuario
        {
            PapelNome = "EMPRESA",
            Email = email,
            Nome = "Empresa Teste",
            EmpresaCnpj = cnpj,
            Senha = "hash",
            Ativo = true,
            EmailValidado = true
        });

        await context.SaveChangesAsync();
    }

    private static async Task<Documento> AddEmpresaDocumentoAsync(
        Eva.Data.EvaDbContext context,
        string empresaCnpj,
        string tipoDocumento,
        DateTime dataUpload)
    {
        var documento = new Documento
        {
            DocumentoTipoNome = tipoDocumento,
            Conteudo = [1, 2, 3],
            NomeArquivo = $"{tipoDocumento}.pdf",
            ContentType = "application/pdf",
            DataUpload = dataUpload
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();

        context.DocumentoEmpresas.Add(new DocumentoEmpresa
        {
            Id = documento.Id,
            EmpresaCnpj = empresaCnpj
        });

        await context.SaveChangesAsync();
        return documento;
    }

    private static async Task<Documento> AddVeiculoDocumentoAsync(
        Eva.Data.EvaDbContext context,
        string placa,
        string tipoDocumento,
        DateTime dataUpload)
    {
        var documento = new Documento
        {
            DocumentoTipoNome = tipoDocumento,
            Conteudo = [1, 2, 3],
            NomeArquivo = $"{tipoDocumento}.pdf",
            ContentType = "application/pdf",
            DataUpload = dataUpload
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();

        context.DocumentoVeiculos.Add(new DocumentoVeiculo
        {
            Id = documento.Id,
            VeiculoPlaca = placa
        });

        await context.SaveChangesAsync();
        return documento;
    }

    private static async Task<Documento> AddMotoristaDocumentoAsync(
        Eva.Data.EvaDbContext context,
        int motoristaId,
        string tipoDocumento,
        DateTime dataUpload)
    {
        var documento = new Documento
        {
            DocumentoTipoNome = tipoDocumento,
            Conteudo = [1, 2, 3],
            NomeArquivo = $"{tipoDocumento}.pdf",
            ContentType = "application/pdf",
            DataUpload = dataUpload
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();

        context.DocumentoMotoristas.Add(new DocumentoMotorista
        {
            Id = documento.Id,
            MotoristaId = motoristaId
        });

        await context.SaveChangesAsync();
        return documento;
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString();

        public bool ChangeState(string jobId, IState state, string expectedState) => true;

        public bool Delete(string jobId) => true;

        public string Enqueue(Job job) => Guid.NewGuid().ToString();

        public string Schedule(Job job, TimeSpan delay) => Guid.NewGuid().ToString();

        public string Schedule(Job job, DateTimeOffset enqueueAt) => Guid.NewGuid().ToString();

        public string ContinueJobWith(string parentId, Job job) => Guid.NewGuid().ToString();

        public string ContinueJobWith(string parentId, Job job, IState nextState) => Guid.NewGuid().ToString();

        public string ContinueJobWith(string parentId, string queue, Job job, IState nextState) => Guid.NewGuid().ToString();

        public string Requeue(string jobId) => jobId;
    }

    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        public List<Job> EnqueuedJobs { get; } = [];

        public string Create(Job job, IState state)
        {
            EnqueuedJobs.Add(job);
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;

        public bool Delete(string jobId) => true;

        public string Enqueue(Job job)
        {
            EnqueuedJobs.Add(job);
            return Guid.NewGuid().ToString();
        }

        public string Schedule(Job job, TimeSpan delay) => Guid.NewGuid().ToString();

        public string Schedule(Job job, DateTimeOffset enqueueAt) => Guid.NewGuid().ToString();

        public string ContinueJobWith(string parentId, Job job) => Guid.NewGuid().ToString();

        public string ContinueJobWith(string parentId, Job job, IState nextState) => Guid.NewGuid().ToString();

        public string ContinueJobWith(string parentId, string queue, Job job, IState nextState) => Guid.NewGuid().ToString();

        public string Requeue(string jobId) => jobId;
    }
}

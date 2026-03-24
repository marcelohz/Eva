using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Services;

[Trait("Category", "integration")]
public class AnalystReviewServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task IniciarAnaliseAsync_deve_bloquear_ticket_para_o_analista()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var pendenciaService = new PendenciaService(context, new FakeBackgroundJobClient());
        await pendenciaService.AvancarEntidadeAsync("EMPRESA", "12345678000199");

        var service = new AnalystReviewService(context, pendenciaService, new EntityStatusService(context));

        var result = await service.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");
        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.True(result.Success);
        Assert.Equal("EM_ANALISE", atual.Status);
        Assert.Equal("analista@metroplan.rs.gov.br", atual.Analista);
    }

    [Fact]
    public async Task AprovarAsync_deve_falhar_quando_documento_obrigatorio_estiver_faltando()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var pendenciaService = new PendenciaService(context, new FakeBackgroundJobClient());
        await pendenciaService.AvancarEntidadeAsync("EMPRESA", "12345678000199");
        await pendenciaService.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");

        var service = new AnalystReviewService(context, pendenciaService, new EntityStatusService(context));

        var result = await service.AprovarAsync(
            "EMPRESA",
            "12345678000199",
            "analista@metroplan.rs.gov.br",
            new Dictionary<int, DateOnly?>());

        Assert.False(result.Success);
        Assert.Contains("Faltam documentos obrigatórios", result.ErrorMessage);
    }

    [Fact]
    public async Task AprovarAsync_deve_atualizar_validade_do_documento_e_concluir_aprovacao()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var documento = await AddEmpresaDocumentoAsync(
            context,
            "12345678000199",
            "CARTAO_CNPJ",
            DateTime.UtcNow.AddDays(-1));

        var pendenciaService = new PendenciaService(context, new FakeBackgroundJobClient());
        await pendenciaService.AvancarEntidadeAsync("EMPRESA", "12345678000199");
        await pendenciaService.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");

        var service = new AnalystReviewService(context, pendenciaService, new EntityStatusService(context));
        var novaValidade = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        var result = await service.AprovarAsync(
            "EMPRESA",
            "12345678000199",
            "analista@metroplan.rs.gov.br",
            new Dictionary<int, DateOnly?> { [documento.Id] = novaValidade });

        var docAtualizado = await context.Documentos.FirstAsync(d => d.Id == documento.Id);
        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.True(result.Success);
        Assert.Equal("Aprovação concluída com sucesso.", result.SuccessMessage);
        Assert.Equal(novaValidade, docAtualizado.Validade);
        Assert.Equal("APROVADO", atual.Status);
    }

    [Fact]
    public async Task RejeitarAsync_deve_exigir_motivo()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var pendenciaService = new PendenciaService(context, new FakeBackgroundJobClient());
        await pendenciaService.AvancarEntidadeAsync("EMPRESA", "12345678000199");
        await pendenciaService.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");

        var service = new AnalystReviewService(context, pendenciaService, new EntityStatusService(context));

        var result = await service.RejeitarAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br", "");

        Assert.False(result.Success);
        Assert.Equal("O motivo é obrigatório para rejeições.", result.ErrorMessage);
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
}

using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Services;

[Trait("Category", "integration")]
public class EntityStatusServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetHealthAsync_deve_separar_situacao_operacional_da_ultima_submissao_quando_entidade_permanece_legal()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199");

        var submissaoService = new SubmissaoService(context);
        var reviewService = new AnalystReviewService(context, new EntityStatusService(context));

        var documentoAprovado = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", documentoAprovado.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissaoAprovada = await context.Submissoes.SingleAsync();
        var docSubAprovado = await context.SubmissaoDocumentos.SingleAsync();
        await reviewService.IniciarAnaliseSubmissaoAsync(submissaoAprovada.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDadosAsync(submissaoAprovada.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDocumentoAsync(docSubAprovado.Id, "analista@metroplan.rs.gov.br", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        await reviewService.AprovarSubmissaoAsync(submissaoAprovada.Id, "analista@metroplan.rs.gov.br");

        await submissaoService.SalvarDadosPropostosAsync(
            "EMPRESA",
            "12345678000199",
            """{"cnpj":"12345678000199","nome":"Empresa Rejeitada","nomeFantasia":"Empresa Rejeitada","email":"empresa@teste.com"}""",
            "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissaoRejeitada = await context.Submissoes.OrderByDescending(s => s.Id).FirstAsync();
        await reviewService.IniciarAnaliseSubmissaoAsync(submissaoRejeitada.Id, "analista@metroplan.rs.gov.br");
        await reviewService.RejeitarDadosAsync(submissaoRejeitada.Id, "analista@metroplan.rs.gov.br", "Dados inv\u00E1lidos");
        await reviewService.RejeitarSubmissaoAsync(submissaoRejeitada.Id, "analista@metroplan.rs.gov.br", "\u00DAltima tentativa rejeitada");

        var health = await new EntityStatusService(context).GetHealthAsync("EMPRESA", "12345678000199");

        Assert.True(health.IsLegal);
        Assert.Equal(global::Eva.Workflow.WorkflowStatus.Aprovado, health.OperationalStatus);
        Assert.Equal(global::Eva.Workflow.SubmissaoWorkflow.Rejeitada, health.LatestSubmissionStatus);
        Assert.Equal("\u00DAltima tentativa rejeitada", health.LastRejectionReason);
    }

    [Fact]
    public async Task GetHealthAsync_deve_expor_status_pendente_quando_houver_submissao_aguardando_analise()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199");

        var submissaoService = new SubmissaoService(context);
        var documentoAprovado = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", documentoAprovado.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var health = await new EntityStatusService(context).GetHealthAsync("EMPRESA", "12345678000199");

        Assert.Equal(global::Eva.Workflow.WorkflowStatus.AguardandoAnalise, health.CurrentStatus);
        Assert.Equal(global::Eva.Workflow.SubmissaoWorkflow.AguardandoAnalise, health.LatestSubmissionStatus);
        Assert.False(health.IsLegal);
        Assert.Contains("CARTAO_CNPJ", health.PendingDocs);
    }

    [Fact]
    public async Task GetHealthAsync_deve_expor_ultima_submissao_em_edicao_sem_perder_legalidade_operacional()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199");

        var submissaoService = new SubmissaoService(context);
        var reviewService = new AnalystReviewService(context, new EntityStatusService(context));

        var documentoAprovado = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", documentoAprovado.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissaoAprovada = await context.Submissoes.SingleAsync();
        var docSubAprovado = await context.SubmissaoDocumentos.SingleAsync();
        await reviewService.IniciarAnaliseSubmissaoAsync(submissaoAprovada.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDadosAsync(submissaoAprovada.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDocumentoAsync(docSubAprovado.Id, "analista@metroplan.rs.gov.br", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        await reviewService.AprovarSubmissaoAsync(submissaoAprovada.Id, "analista@metroplan.rs.gov.br");

        await submissaoService.SalvarDadosPropostosAsync(
            "EMPRESA",
            "12345678000199",
            """{"cnpj":"12345678000199","nome":"Empresa em Edicao","nomeFantasia":"Empresa em Edicao","email":"empresa@teste.com"}""",
            "empresa@teste.com");

        var health = await new EntityStatusService(context).GetHealthAsync("EMPRESA", "12345678000199");

        Assert.True(health.IsLegal);
        Assert.Equal(global::Eva.Workflow.WorkflowStatus.Aprovado, health.OperationalStatus);
        Assert.Equal(global::Eva.Workflow.SubmissaoWorkflow.EmEdicao, health.LatestSubmissionStatus);
    }

    [Fact]
    public async Task GetHealthAsync_deve_manter_legalidade_com_documento_canonico_aprovado_mesmo_apos_rejeicao_de_substituicao()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199");

        var submissaoService = new SubmissaoService(context);
        var reviewService = new AnalystReviewService(context, new EntityStatusService(context));

        var documentoInicial = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", documentoInicial.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissaoInicial = await context.Submissoes.SingleAsync();
        var docInicial = await context.SubmissaoDocumentos.SingleAsync();
        await reviewService.IniciarAnaliseSubmissaoAsync(submissaoInicial.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDadosAsync(submissaoInicial.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDocumentoAsync(docInicial.Id, "analista@metroplan.rs.gov.br", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));
        await reviewService.AprovarSubmissaoAsync(submissaoInicial.Id, "analista@metroplan.rs.gov.br");

        var documentoSubstituto = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", documentoSubstituto.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissaoRejeitada = await context.Submissoes.OrderByDescending(s => s.Id).FirstAsync();
        var docSubstituto = await context.SubmissaoDocumentos
            .Where(sd => sd.SubmissaoId == submissaoRejeitada.Id)
            .OrderByDescending(sd => sd.Id)
            .FirstAsync();
        await reviewService.IniciarAnaliseSubmissaoAsync(submissaoRejeitada.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDadosAsync(submissaoRejeitada.Id, "analista@metroplan.rs.gov.br");
        await reviewService.RejeitarDocumentoAsync(docSubstituto.Id, "analista@metroplan.rs.gov.br", "Documento ilegivel");
        await reviewService.RejeitarSubmissaoAsync(submissaoRejeitada.Id, "analista@metroplan.rs.gov.br", "Corrigir documento");

        var health = await new EntityStatusService(context).GetHealthAsync("EMPRESA", "12345678000199");
        var documentoAtual = await context.EntidadeDocumentosAtuais.SingleAsync();

        Assert.True(health.IsLegal);
        Assert.Equal(global::Eva.Workflow.WorkflowStatus.Aprovado, health.OperationalStatus);
        Assert.Equal(global::Eva.Workflow.SubmissaoWorkflow.Rejeitada, health.LatestSubmissionStatus);
        Assert.Equal(documentoInicial.Id, documentoAtual.DocumentoId);
    }

    private static async Task SeedEmpresaAsync(Eva.Data.EvaDbContext context, string cnpj)
    {
        context.Empresas.Add(new Empresa
        {
            Cnpj = cnpj,
            Nome = "Empresa Teste",
            NomeFantasia = "Empresa Teste",
            Email = "empresa@teste.com"
        });

        context.Usuarios.Add(new Usuario
        {
            PapelNome = "EMPRESA",
            Email = "empresa@teste.com",
            Nome = "Empresa Teste",
            EmpresaCnpj = cnpj,
            Senha = "hash",
            Ativo = true,
            EmailValidado = true
        });

        await context.SaveChangesAsync();
    }

    private static async Task<Documento> CriarDocumentoAsync(Eva.Data.EvaDbContext context, string tipo)
    {
        var documento = new Documento
        {
            DocumentoTipoNome = tipo,
            Conteudo = [1, 2, 3],
            NomeArquivo = $"{tipo}.pdf",
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();
        return documento;
    }
}

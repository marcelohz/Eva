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
    public async Task GetHealthAsync_deve_expor_status_rejeitado_mesmo_com_entidade_ainda_legal()
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
        await reviewService.RejeitarDadosAsync(submissaoRejeitada.Id, "analista@metroplan.rs.gov.br", "Dados inválidos");
        await reviewService.RejeitarSubmissaoAsync(submissaoRejeitada.Id, "analista@metroplan.rs.gov.br", "Última tentativa rejeitada");

        var health = await new EntityStatusService(context).GetHealthAsync("EMPRESA", "12345678000199");

        Assert.True(health.IsLegal);
        Assert.Equal(global::Eva.Workflow.WorkflowStatus.Rejeitado, health.CurrentStatus);
        Assert.Equal("Última tentativa rejeitada", health.LastRejectionReason);
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
        Assert.False(health.IsLegal);
        Assert.Contains("CARTAO_CNPJ", health.PendingDocs);
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

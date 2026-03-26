using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Eva.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Services;

[Trait("Category", "integration")]
public class SubmissaoServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetOrCreateDraftAsync_deve_criar_draft_e_dados_aprovados_quando_iguais_ao_live()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var service = new SubmissaoService(context);

        var draft = await service.GetOrCreateDraftAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        Assert.Equal(SubmissaoWorkflow.EmEdicao, draft.Submissao.Status);
        Assert.Equal(SubmissaoWorkflow.RevisaoAprovada, draft.Dados.StatusRevisao);
        Assert.True(draft.Dados.CarregadoDoLive);
        Assert.Empty(draft.Documentos);
    }

    [Fact]
    public async Task VincularDocumentoAoDraftAsync_deve_substituir_tipo_simples()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var service = new SubmissaoService(context);
        await service.GetOrCreateDraftAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var primeiro = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        var segundo = await CriarDocumentoAsync(context, "CARTAO_CNPJ");

        await service.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", primeiro.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await service.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", segundo.Id, "CARTAO_CNPJ", "empresa@teste.com");

        var draft = await service.GetDraftAsync("EMPRESA", "12345678000199");
        Assert.NotNull(draft);

        var docs = draft!.Documentos.Where(d => d.DocumentoTipoNome == "CARTAO_CNPJ").OrderBy(d => d.Id).ToList();
        Assert.Equal(2, docs.Count);
        Assert.False(docs[0].AtivoNaSubmissao);
        Assert.True(docs[1].AtivoNaSubmissao);
    }

    [Fact]
    public async Task VincularDocumentoAoDraftAsync_deve_manter_multiplos_ativos_para_identidade_socio()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var service = new SubmissaoService(context);
        await service.GetOrCreateDraftAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var primeiro = await CriarDocumentoAsync(context, "IDENTIDADE_SOCIO");
        var segundo = await CriarDocumentoAsync(context, "IDENTIDADE_SOCIO");

        await service.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", primeiro.Id, "IDENTIDADE_SOCIO", "empresa@teste.com");
        await service.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", segundo.Id, "IDENTIDADE_SOCIO", "empresa@teste.com");

        var draft = await service.GetDraftAsync("EMPRESA", "12345678000199");
        Assert.NotNull(draft);

        var docs = draft!.Documentos.Where(d => d.DocumentoTipoNome == "IDENTIDADE_SOCIO").ToList();
        Assert.Equal(2, docs.Count);
        Assert.All(docs, d => Assert.True(d.AtivoNaSubmissao));
    }

    [Fact]
    public async Task ValidarDraftAsync_deve_exigir_ao_menos_um_documento_de_tipo_obrigatorio()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var service = new SubmissaoService(context);
        await service.GetOrCreateDraftAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var result = await service.ValidarDraftAsync("EMPRESA", "12345678000199");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("CARTAO_CNPJ"));
    }

    [Fact]
    public async Task GetDocumentosParaEdicaoAsync_deve_expor_status_e_motivo_do_documento_rejeitado_na_ultima_submissao()
    {
        await using var context = _database.CreateDbContext();
        await SeedMotoristaAsync(context, "12345678901", "empresa@teste.com");

        var submissaoService = new SubmissaoService(context);
        var reviewService = new AnalystReviewService(context, new EntityStatusService(context));

        var cnh = await CriarDocumentoAsync(context, "CNH");
        await submissaoService.VincularDocumentoAoDraftAsync("MOTORISTA", "12345678901", cnh.Id, "CNH", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("MOTORISTA", "12345678901", "empresa@teste.com");

        var submissao = await context.Submissoes.SingleAsync();
        var submissaoDocumento = await context.SubmissaoDocumentos.SingleAsync();
        await reviewService.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDadosAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await reviewService.RejeitarDocumentoAsync(submissaoDocumento.Id, "analista@metroplan.rs.gov.br", "CNH ilegível");
        await reviewService.RejeitarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br", "Corrigir documentação");

        var documentos = await submissaoService.GetDocumentosParaEdicaoAsync("MOTORISTA", "12345678901");

        var docEdicao = Assert.Single(documentos);
        Assert.Equal("CNH", docEdicao.Documento.DocumentoTipoNome);
        Assert.Equal(SubmissaoWorkflow.RevisaoRejeitada, docEdicao.StatusRevisao);
        Assert.Equal("CNH ilegível", docEdicao.MotivoRejeicao);
    }

    [Fact]
    public async Task RemoverDocumentoDoDraftAsync_deve_manter_lista_vazia_quando_draft_ficar_sem_docs()
    {
        await using var context = _database.CreateDbContext();
        await SeedMotoristaAsync(context, "22222222222", "empresa@teste.com");

        var documento = await CriarDocumentoAsync(context, "CNH");

        var submissaoService = new SubmissaoService(context);
        var reviewService = new AnalystReviewService(context, new EntityStatusService(context));

        await submissaoService.VincularDocumentoAoDraftAsync("MOTORISTA", "22222222222", documento.Id, "CNH", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("MOTORISTA", "22222222222", "empresa@teste.com");

        var submissao = await context.Submissoes.SingleAsync();
        var submissaoDocumento = await context.SubmissaoDocumentos.SingleAsync();
        await reviewService.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDadosAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await reviewService.RejeitarDocumentoAsync(submissaoDocumento.Id, "analista@metroplan.rs.gov.br", "CNH ilegível");
        await reviewService.RejeitarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br", "Corrigir documento");

        await submissaoService.RemoverDocumentoDoDraftAsync("MOTORISTA", "22222222222", documento.Id, "empresa@teste.com");

        var documentos = await submissaoService.GetDocumentosParaEdicaoAsync("MOTORISTA", "22222222222");

        Assert.Empty(documentos);
    }

    [Fact]
    public async Task GetDocumentosParaEdicaoAsync_deve_exibir_documento_oficial_aprovado_apos_aprovacao_final()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var submissaoService = new SubmissaoService(context);
        var reviewService = new AnalystReviewService(context, new EntityStatusService(context));

        var cartao = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", cartao.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissao = await context.Submissoes.SingleAsync();
        var submissaoDocumento = await context.SubmissaoDocumentos.SingleAsync();

        await reviewService.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDadosAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await reviewService.AprovarDocumentoAsync(submissaoDocumento.Id, "analista@metroplan.rs.gov.br", null);
        await reviewService.AprovarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");

        var documentos = await submissaoService.GetDocumentosParaEdicaoAsync("EMPRESA", "12345678000199");

        var docEdicao = Assert.Single(documentos);
        Assert.Equal(SubmissaoWorkflow.RevisaoAprovada, docEdicao.StatusRevisao);
        Assert.True(docEdicao.CarregadoDeDocumentoAtual);
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

    private static async Task SeedMotoristaAsync(Eva.Data.EvaDbContext context, string cpf, string emailEmpresa)
    {
        await SeedEmpresaAsync(context, "12345678000199", emailEmpresa);

        context.Motoristas.Add(new Motorista
        {
            Cpf = cpf,
            EmpresaCnpj = "12345678000199",
            Nome = "Motorista Teste",
            Cnh = "12345678901",
            Email = "motorista@teste.com",
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

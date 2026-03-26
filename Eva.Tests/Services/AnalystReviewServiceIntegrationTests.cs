using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Eva.Workflow;
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
    public async Task IniciarAnaliseSubmissaoAsync_deve_bloquear_submissao_para_o_analista()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var submissaoService = new SubmissaoService(context);
        var submissao = await CriarSubmissaoEmpresaAguardandoAnaliseAsync(context, submissaoService, "12345678000199");
        var service = new AnalystReviewService(context, new EntityStatusService(context));

        var result = await service.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");

        var atualizada = await context.Submissoes.FirstAsync(s => s.Id == submissao.Id);

        Assert.True(result.Success);
        Assert.Equal(SubmissaoWorkflow.EmAnalise, atualizada.Status);
        Assert.Equal("analista@metroplan.rs.gov.br", atualizada.AnalistaAtual);
    }

    [Fact]
    public async Task AprovarSubmissaoAsync_deve_promover_dados_e_documentos_oficiais()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var submissaoService = new SubmissaoService(context);
        await submissaoService.SalvarDadosPropostosAsync(
            "EMPRESA",
            "12345678000199",
            """{"cnpj":"12345678000199","nome":"Empresa Corrigida","nomeFantasia":"Empresa Corrigida","email":"empresa@teste.com"}""",
            "empresa@teste.com");

        var documento = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", documento.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissao = await context.Submissoes.SingleAsync();
        var docSub = await context.SubmissaoDocumentos.SingleAsync();
        var service = new AnalystReviewService(context, new EntityStatusService(context));

        await service.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await service.AprovarDadosAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await service.AprovarDocumentoAsync(docSub.Id, "analista@metroplan.rs.gov.br", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)));

        var result = await service.AprovarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");

        var empresa = await context.Empresas.IgnoreQueryFilters().FirstAsync(e => e.Cnpj == "12345678000199");
        var submissaoAprovada = await context.Submissoes.FirstAsync(s => s.Id == submissao.Id);
        var atual = await context.EntidadeDocumentosAtuais.SingleAsync();
        var health = await new EntityStatusService(context).GetHealthAsync("EMPRESA", "12345678000199");

        Assert.True(result.Success);
        Assert.Equal(SubmissaoWorkflow.Aprovada, submissaoAprovada.Status);
        Assert.Equal("Empresa Corrigida", empresa.Nome);
        Assert.Equal(documento.Id, atual.DocumentoId);
        Assert.True(health.IsLegal);
    }

    [Fact]
    public async Task RejeitarSubmissaoAsync_deve_exigir_item_rejeitado_antes_da_rejeicao_final()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var submissaoService = new SubmissaoService(context);
        var submissao = await CriarSubmissaoEmpresaAguardandoAnaliseAsync(context, submissaoService, "12345678000199");
        var service = new AnalystReviewService(context, new EntityStatusService(context));
        var documentoObrigatorio = await context.SubmissaoDocumentos.SingleAsync(sd => sd.DocumentoTipoNome == "CARTAO_CNPJ");

        await service.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");

        var falha = await service.RejeitarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br", "Observacao final");
        Assert.False(falha.Success);

        await service.RejeitarDadosAsync(submissao.Id, "analista@metroplan.rs.gov.br", "Dados inconsistentes");
        await service.AprovarDocumentoAsync(documentoObrigatorio.Id, "analista@metroplan.rs.gov.br", null);
        var sucesso = await service.RejeitarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br", "Observacao final");

        var rejeitada = await context.Submissoes.FirstAsync(s => s.Id == submissao.Id);

        Assert.True(sucesso.Success);
        Assert.Equal(SubmissaoWorkflow.Rejeitada, rejeitada.Status);
        Assert.Equal("Observacao final", rejeitada.ObservacaoAnalista);
    }

    [Fact]
    public async Task RejeitarSubmissaoAsync_deve_aceitar_documento_opcional_rejeitado()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var submissaoService = new SubmissaoService(context);
        await submissaoService.SalvarDadosPropostosAsync(
            "EMPRESA",
            "12345678000199",
            """{"cnpj":"12345678000199","nome":"Empresa Teste","nomeFantasia":"Empresa Teste","email":"empresa@teste.com"}""",
            "empresa@teste.com");

        var cartao = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        var identidade = await CriarDocumentoAsync(context, "IDENTIDADE_SOCIO");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", cartao.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", identidade.Id, "IDENTIDADE_SOCIO", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissao = await context.Submissoes.SingleAsync();
        var documentos = await context.SubmissaoDocumentos.OrderBy(sd => sd.Id).ToListAsync();
        var documentoObrigatorio = documentos.First(sd => sd.DocumentoTipoNome == "CARTAO_CNPJ");
        var documentoOpcional = documentos.First(sd => sd.DocumentoTipoNome == "IDENTIDADE_SOCIO");
        var service = new AnalystReviewService(context, new EntityStatusService(context));

        await service.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await service.AprovarDadosAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await service.AprovarDocumentoAsync(documentoObrigatorio.Id, "analista@metroplan.rs.gov.br", null);
        await service.RejeitarDocumentoAsync(documentoOpcional.Id, "analista@metroplan.rs.gov.br", "Documento opcional inválido");

        var result = await service.RejeitarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br", "Observacao final");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task AprovarSubmissaoAsync_deve_bloquear_documento_rejeitado_ativo()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var submissaoService = new SubmissaoService(context);
        await submissaoService.SalvarDadosPropostosAsync(
            "EMPRESA",
            "12345678000199",
            """{"cnpj":"12345678000199","nome":"Empresa Teste","nomeFantasia":"Empresa Teste","email":"empresa@teste.com"}""",
            "empresa@teste.com");

        var cartao = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        var identidade = await CriarDocumentoAsync(context, "IDENTIDADE_SOCIO");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", cartao.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", identidade.Id, "IDENTIDADE_SOCIO", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissao = await context.Submissoes.SingleAsync();
        var documentos = await context.SubmissaoDocumentos.OrderBy(sd => sd.Id).ToListAsync();
        var documentoObrigatorio = documentos.First(sd => sd.DocumentoTipoNome == "CARTAO_CNPJ");
        var documentoOpcional = documentos.First(sd => sd.DocumentoTipoNome == "IDENTIDADE_SOCIO");
        var service = new AnalystReviewService(context, new EntityStatusService(context));

        await service.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await service.AprovarDadosAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await service.AprovarDocumentoAsync(documentoObrigatorio.Id, "analista@metroplan.rs.gov.br", null);
        await service.RejeitarDocumentoAsync(documentoOpcional.Id, "analista@metroplan.rs.gov.br", "Documento opcional inválido");

        var result = await service.AprovarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");

        Assert.False(result.Success);
        Assert.Contains("rejeitados", result.ErrorMessage);
    }

    [Fact]
    public async Task AprovarSubmissaoAsync_deve_exigir_todos_os_itens_revisados()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var submissaoService = new SubmissaoService(context);
        await submissaoService.SalvarDadosPropostosAsync(
            "EMPRESA",
            "12345678000199",
            """{"cnpj":"12345678000199","nome":"Empresa Teste","nomeFantasia":"Empresa Teste","email":"empresa@teste.com"}""",
            "empresa@teste.com");

        var cartao = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        var identidade = await CriarDocumentoAsync(context, "IDENTIDADE_SOCIO");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", cartao.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", identidade.Id, "IDENTIDADE_SOCIO", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissao = await context.Submissoes.SingleAsync();
        var documentoObrigatorio = await context.SubmissaoDocumentos.SingleAsync(sd => sd.DocumentoTipoNome == "CARTAO_CNPJ");
        var service = new AnalystReviewService(context, new EntityStatusService(context));

        await service.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await service.AprovarDadosAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await service.AprovarDocumentoAsync(documentoObrigatorio.Id, "analista@metroplan.rs.gov.br", null);

        var result = await service.AprovarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");

        Assert.False(result.Success);
        Assert.Contains("revisados", result.ErrorMessage);
    }

    [Fact]
    public async Task RejeitarSubmissaoAsync_deve_exigir_todos_os_itens_revisados()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var submissaoService = new SubmissaoService(context);
        await submissaoService.SalvarDadosPropostosAsync(
            "EMPRESA",
            "12345678000199",
            """{"cnpj":"12345678000199","nome":"Empresa Teste","nomeFantasia":"Empresa Teste","email":"empresa@teste.com"}""",
            "empresa@teste.com");

        var cartao = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        var identidade = await CriarDocumentoAsync(context, "IDENTIDADE_SOCIO");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", cartao.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", identidade.Id, "IDENTIDADE_SOCIO", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissao = await context.Submissoes.SingleAsync();
        var documentoObrigatorio = await context.SubmissaoDocumentos.SingleAsync(sd => sd.DocumentoTipoNome == "CARTAO_CNPJ");
        var service = new AnalystReviewService(context, new EntityStatusService(context));

        await service.IniciarAnaliseSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br");
        await service.RejeitarDadosAsync(submissao.Id, "analista@metroplan.rs.gov.br", "Dados inválidos");
        await service.AprovarDocumentoAsync(documentoObrigatorio.Id, "analista@metroplan.rs.gov.br", null);

        var result = await service.RejeitarSubmissaoAsync(submissao.Id, "analista@metroplan.rs.gov.br", "Observacao final");

        Assert.False(result.Success);
        Assert.Contains("Revise todos os itens", result.ErrorMessage);
    }

    private static async Task<Submissao> CriarSubmissaoEmpresaAguardandoAnaliseAsync(Eva.Data.EvaDbContext context, SubmissaoService submissaoService, string cnpj)
    {
        await submissaoService.SalvarDadosPropostosAsync(
            "EMPRESA",
            cnpj,
            $"{{\"cnpj\":\"{cnpj}\",\"nome\":\"Empresa Teste\",\"nomeFantasia\":\"Empresa Teste\",\"email\":\"empresa@teste.com\"}}",
            "empresa@teste.com");

        var documento = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", cnpj, documento.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", cnpj, "empresa@teste.com");

        return await context.Submissoes.SingleAsync();
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

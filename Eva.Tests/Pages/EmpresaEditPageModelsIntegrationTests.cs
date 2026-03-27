using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Pages.Empresa;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Eva.Workflow;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Pages;

[Trait("Category", "integration")]
public class EmpresaEditPageModelsIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EditarEmpresa_OnPostAsync_deve_retornar_page_com_erro_quando_submissao_esta_em_analise()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");

        var submissaoService = new SubmissaoService(context);
        var documento = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", documento.Id, "CARTAO_CNPJ", "empresa@teste.com");
        await submissaoService.EnviarParaAnaliseAsync("EMPRESA", "12345678000199", "empresa@teste.com");

        var submissao = await context.Submissoes.SingleAsync();
        submissao.Status = SubmissaoWorkflow.EmAnalise;
        submissao.AnalistaAtual = "analista@metroplan.rs.gov.br";
        await context.SaveChangesAsync();

        var page = CreateEditarEmpresaModel(context, httpContext);
        page.Input = new EmpresaVM
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Alterada",
            NomeFantasia = "Empresa Alterada"
        };

        var result = await page.OnPostAsync();

        var pageResult = Assert.IsType<PageResult>(result);
        Assert.Same(pageResult, result);
        Assert.Contains(page.ModelState[string.Empty]!.Errors, e => e.ErrorMessage == EmpresaEntityEditGuardService.LockedMessage);
    }

    [Fact]
    public async Task EditarEmpresa_OnPostEnviarAsync_deve_enviar_draft_para_analise()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");

        var submissaoService = new SubmissaoService(context);
        var documento = await CriarDocumentoAsync(context, "CARTAO_CNPJ");
        await submissaoService.VincularDocumentoAoDraftAsync("EMPRESA", "12345678000199", documento.Id, "CARTAO_CNPJ", "empresa@teste.com");

        var page = CreateEditarEmpresaModel(context, httpContext);
        page.Input = new EmpresaVM
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Teste",
            NomeFantasia = "Empresa Teste",
            Email = "empresa@teste.com"
        };

        var result = await page.OnPostEnviarAsync();

        Assert.IsType<RedirectToPageResult>(result);

        var submissao = await context.Submissoes.SingleAsync();
        Assert.Equal(SubmissaoWorkflow.AguardandoAnalise, submissao.Status);
    }

    private static EditarEmpresaModel CreateEditarEmpresaModel(Eva.Data.EvaDbContext context, DefaultHttpContext httpContext)
    {
        var currentUserService = new CurrentUserService(context, new HttpContextAccessor { HttpContext = httpContext });
        var submissaoService = new SubmissaoService(context);
        var arquivoService = new ArquivoService(context, submissaoService);
        var guardService = new EmpresaEntityEditGuardService(context, currentUserService);
        var entityStatusService = new EntityStatusService(context);

        var page = new EditarEmpresaModel(context, submissaoService, arquivoService, currentUserService, guardService, entityStatusService);
        InitializePageModel(page, httpContext);
        return page;
    }

    private static void InitializePageModel(PageModel page, DefaultHttpContext httpContext)
    {
        page.PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
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

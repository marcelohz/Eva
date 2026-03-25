using Eva.Data;
using Eva.Models;
using Eva.Pages.Empresa;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Pages;

[Trait("Category", "integration")]
public class MinhasViagensPageModelIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OnGetAsync_deve_permitir_nova_viagem_quando_empresa_esta_legal()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);

        await SeedEmpresaLegalAsync(context, "12345678000199");

        var page = CreatePageModel(context, httpContext);

        var result = await page.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(page.PodeCriarNovaViagem);
        Assert.Null(page.BloqueioNovaViagemMensagem);
    }

    [Fact]
    public async Task OnGetAsync_deve_bloquear_nova_viagem_quando_empresa_nao_esta_legal()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);

        await SeedEmpresaAsync(context, "12345678000199");

        var page = CreatePageModel(context, httpContext);

        var result = await page.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(page.PodeCriarNovaViagem);
        Assert.Equal(
            "Sua empresa precisa estar com o cadastro e a documentação regularizados para cadastrar novas viagens.",
            page.BloqueioNovaViagemMensagem);
    }

    private static MinhasViagensModel CreatePageModel(EvaDbContext context, DefaultHttpContext httpContext)
    {
        var statusService = new EntityStatusService(context);
        var viagemManagementService = new ViagemManagementService(context);

        return new MinhasViagensModel(context, statusService, viagemManagementService)
        {
            PageContext = new PageContext
            {
                HttpContext = httpContext,
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                    new EmptyModelMetadataProvider(),
                    new ModelStateDictionary())
            },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };
    }

    private static async Task SeedEmpresaAsync(EvaDbContext context, string cnpj)
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

    private static async Task SeedEmpresaLegalAsync(EvaDbContext context, string cnpj)
    {
        await SeedEmpresaAsync(context, cnpj);

        var fluxo = new FluxoPendencia
        {
            EntidadeTipo = "EMPRESA",
            EntidadeId = cnpj,
            Status = "APROVADO",
            Analista = "analista@metroplan.rs.gov.br",
            CriadoEm = DateTime.UtcNow
        };

        context.FluxoPendencias.Add(fluxo);
        await context.SaveChangesAsync();

        var documento = new Documento
        {
            DocumentoTipoNome = "CARTAO_CNPJ",
            Conteudo = [1, 2, 3],
            NomeArquivo = "cartao-cnpj.pdf",
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow,
            AprovadoEm = DateTime.UtcNow,
            FluxoPendenciaId = fluxo.Id
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();

        context.DocumentoEmpresas.Add(new DocumentoEmpresa
        {
            Id = documento.Id,
            EmpresaCnpj = cnpj
        });

        await context.SaveChangesAsync();
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}

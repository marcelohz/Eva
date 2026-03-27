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
public class NovaEntidadePageModelsIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NovoVeiculo_OnPostAsync_deve_criar_row_live_e_abrir_submissao_em_edicao()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");

        var page = CreateNovoVeiculoModel(context, httpContext);
        page.Input = new VeiculoVM
        {
            Placa = "abc1234",
            Modelo = "Modelo Teste",
            ChassiNumero = "CHASSI123",
            Renavan = "RENAVAN123",
            PotenciaMotor = 250,
            VeiculoCombustivelNome = "DIESEL",
            NumeroLugares = 42,
            AnoFabricacao = 2020,
            ModeloAno = 2021
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./EditarVeiculo", redirect.PageName);

        var veiculo = await context.Veiculos.SingleAsync(v => v.Placa == "ABC1234");
        var submissao = await context.Submissoes.SingleAsync(s => s.EntidadeTipo == "VEICULO" && s.EntidadeId == "ABC1234");
        var dados = await context.SubmissaoDados.SingleAsync(sd => sd.SubmissaoId == submissao.Id);

        Assert.Equal("12345678000199", veiculo.EmpresaCnpj);
        Assert.Equal("Modelo Teste", veiculo.Modelo);
        Assert.Equal(SubmissaoWorkflow.EmEdicao, submissao.Status);
        Assert.Contains("Modelo Teste", dados.DadosPropostos);
    }

    [Fact]
    public async Task NovoMotorista_OnPostAsync_deve_criar_row_live_e_abrir_submissao_em_edicao()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");

        var page = CreateNovoMotoristaModel(context, httpContext);
        page.Input = new MotoristaVM
        {
            Nome = "Motorista Teste",
            Cpf = "12345678901",
            Cnh = "98765432100",
            Email = "motorista@teste.com"
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./EditarMotorista", redirect.PageName);

        var motorista = await context.Motoristas.SingleAsync(m => m.Cpf == "12345678901");
        var submissao = await context.Submissoes.SingleAsync(s => s.EntidadeTipo == "MOTORISTA" && s.EntidadeId == motorista.Id.ToString());
        var dados = await context.SubmissaoDados.SingleAsync(sd => sd.SubmissaoId == submissao.Id);

        Assert.Equal("12345678000199", motorista.EmpresaCnpj);
        Assert.Equal("Motorista Teste", motorista.Nome);
        Assert.Equal(SubmissaoWorkflow.EmEdicao, submissao.Status);
        Assert.Contains("Motorista Teste", dados.DadosPropostos);
    }

    private static NovoVeiculoModel CreateNovoVeiculoModel(Eva.Data.EvaDbContext context, DefaultHttpContext httpContext)
    {
        var submissaoService = new SubmissaoService(context);
        var arquivoService = new ArquivoService(context, submissaoService);
        var page = new NovoVeiculoModel(context, submissaoService, arquivoService);
        InitializePageModel(page, httpContext);
        return page;
    }

    private static NovoMotoristaModel CreateNovoMotoristaModel(Eva.Data.EvaDbContext context, DefaultHttpContext httpContext)
    {
        var submissaoService = new SubmissaoService(context);
        var arquivoService = new ArquivoService(context, submissaoService);
        var page = new NovoMotoristaModel(context, submissaoService, arquivoService);
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
}

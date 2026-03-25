using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Pages.Empresa;
using Eva.Services;
using Eva.Tests.Infrastructure;
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
public class NovaViagemPageModelIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OnPostAsync_deve_criar_viagem_e_redirecionar_para_pagamento_quando_tudo_esta_legal()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseViagemRefsAsync(context);
        await SeedEmpresaLegalAsync(context, "12345678000199");
        await SeedVeiculoLegalAsync(context, "ABC1D23", "12345678000199");
        var motoristaId = await SeedMotoristaLegalAsync(context, "12345678000199", "João da Silva");

        Assert.True(await context.Municipios.AnyAsync(m => m.Nome == "PORTO ALEGRE"));
        Assert.True(await context.Municipios.AnyAsync(m => m.Nome == "CANOAS"));

        var page = CreatePageModel(context, httpContext);
        page.Input = CreateValidInput("ABC1D23", motoristaId);
        page.AcaoSubmit = "PagarAgora";

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/PagamentoViagem", redirect.PageName);

        var viagem = await context.Viagens.Include(v => v.Passageiros).SingleAsync();
        Assert.Equal("12345678000199", viagem.EmpresaCnpj);
        Assert.Equal("ABC1D23", viagem.VeiculoPlaca);
        Assert.Equal(motoristaId, viagem.MotoristaId);
        Assert.False(viagem.Pago);
        Assert.Equal(2, viagem.Passageiros.Count);
        Assert.Equal(viagem.Id, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task OnPostAsync_deve_criar_viagem_e_redirecionar_para_minhas_viagens_quando_usuario_escolhe_pagar_depois()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseViagemRefsAsync(context);
        await SeedEmpresaLegalAsync(context, "12345678000199");
        await SeedVeiculoLegalAsync(context, "ABC1D23", "12345678000199");
        var motoristaId = await SeedMotoristaLegalAsync(context, "12345678000199", "João da Silva");

        Assert.True(await context.Municipios.AnyAsync(m => m.Nome == "PORTO ALEGRE"));
        Assert.True(await context.Municipios.AnyAsync(m => m.Nome == "CANOAS"));

        var page = CreatePageModel(context, httpContext);
        page.Input = CreateValidInput("ABC1D23", motoristaId);
        page.AcaoSubmit = "PagarDepois";

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/MinhasViagens", redirect.PageName);
        Assert.Single(await context.Viagens.ToListAsync());
    }

    [Fact]
    public async Task OnPostAsync_deve_retornar_page_e_nao_criar_viagem_quando_empresa_nao_esta_legal()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseViagemRefsAsync(context);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedVeiculoLegalAsync(context, "ABC1D23", "12345678000199");
        var motoristaId = await SeedMotoristaLegalAsync(context, "12345678000199", "João da Silva");

        var page = CreatePageModel(context, httpContext);
        page.Input = CreateValidInput("ABC1D23", motoristaId);

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(await context.Viagens.ToListAsync());
        Assert.True(page.ModelState.ErrorCount > 0);
    }

    [Fact]
    public async Task OnGetAsync_deve_redirecionar_para_minhas_viagens_quando_empresa_nao_esta_legal()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseViagemRefsAsync(context);
        await SeedEmpresaAsync(context, "12345678000199");

        var page = CreatePageModel(context, httpContext);

        var result = await page.OnGetAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/MinhasViagens", redirect.PageName);
        Assert.Equal(
            "Sua empresa precisa estar com o cadastro e a documentação regularizados para cadastrar novas viagens.",
            page.TempData["MensagemAviso"]);
    }

    [Fact]
    public async Task OnPostAsync_deve_retornar_page_e_nao_criar_viagem_quando_veiculo_nao_esta_legal()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseViagemRefsAsync(context);
        await SeedEmpresaLegalAsync(context, "12345678000199");

        context.Veiculos.Add(new Veiculo
        {
            Placa = "ABC1D23",
            EmpresaCnpj = "12345678000199",
            Modelo = "Ônibus"
        });
        await context.SaveChangesAsync();

        var motoristaId = await SeedMotoristaLegalAsync(context, "12345678000199", "João da Silva");

        var page = CreatePageModel(context, httpContext);
        page.Input = CreateValidInput("ABC1D23", motoristaId);

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(await context.Viagens.ToListAsync());
        Assert.True(page.ModelState.ContainsKey("Input.VeiculoPlaca"));
    }

    [Fact]
    public async Task OnPostAsync_deve_criar_viagem_quando_motorista_auxiliar_estiver_legal()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseViagemRefsAsync(context);
        await SeedEmpresaLegalAsync(context, "12345678000199");
        await SeedVeiculoLegalAsync(context, "ABC1D23", "12345678000199");
        var motoristaPrincipalId = await SeedMotoristaLegalAsync(context, "12345678000199", "JoÃ£o da Silva");
        var motoristaAuxId = await SeedMotoristaLegalAsync(context, "12345678000199", "Carlos Auxiliar");

        var page = CreatePageModel(context, httpContext);
        page.Input = CreateValidInput("ABC1D23", motoristaPrincipalId);
        page.Input.MotoristaAuxId = motoristaAuxId;
        page.AcaoSubmit = "PagarDepois";

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/MinhasViagens", redirect.PageName);

        var viagem = await context.Viagens.SingleAsync();
        Assert.Equal(motoristaAuxId, viagem.MotoristaAuxId);
    }

    [Fact]
    public async Task OnPostAsync_deve_retornar_page_quando_motorista_auxiliar_nao_esta_legal()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseViagemRefsAsync(context);
        await SeedEmpresaLegalAsync(context, "12345678000199");
        await SeedVeiculoLegalAsync(context, "ABC1D23", "12345678000199");
        var motoristaPrincipalId = await SeedMotoristaLegalAsync(context, "12345678000199", "JoÃ£o da Silva");
        var motoristaAuxId = await SeedMotoristaAsync(context, "12345678000199", "Auxiliar Bloqueado");

        var page = CreatePageModel(context, httpContext);
        page.Input = CreateValidInput("ABC1D23", motoristaPrincipalId);
        page.Input.MotoristaAuxId = motoristaAuxId;

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(await context.Viagens.ToListAsync());
        Assert.True(page.ModelState.ContainsKey("Input.MotoristaAuxId"));
    }

    [Fact]
    public async Task OnGetAsync_deve_carregar_veiculos_e_motoristas_bloqueados_como_desabilitados()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedBaseViagemRefsAsync(context);
        await SeedEmpresaLegalAsync(context, "12345678000199");
        await SeedVeiculoLegalAsync(context, "ABC1D23", "12345678000199");

        context.Veiculos.Add(new Veiculo
        {
            Placa = "XYZ9Z99",
            EmpresaCnpj = "12345678000199",
            Modelo = "Micro-ônibus"
        });

        var motoristaLegalId = await SeedMotoristaLegalAsync(context, "12345678000199", "João da Silva");
        var motoristaBloqueadoId = await SeedMotoristaAsync(context, "12345678000199", "Motorista Bloqueado");

        await context.SaveChangesAsync();

        var page = CreatePageModel(context, httpContext);

        var result = await page.OnGetAsync();

        Assert.IsType<PageResult>(result);

        var veiculoLegal = page.VeiculosValidos.Single(v => v.Value == "ABC1D23");
        var veiculoBloqueado = page.VeiculosValidos.Single(v => v.Value == "XYZ9Z99");
        var motoristaLegal = page.MotoristasValidos.Single(v => v.Value == motoristaLegalId.ToString());
        var motoristaBloqueado = page.MotoristasValidos.Single(v => v.Value == motoristaBloqueadoId.ToString());

        Assert.False(veiculoLegal.Disabled);
        Assert.True(veiculoBloqueado.Disabled);
        Assert.Contains("Bloqueado", veiculoBloqueado.Text);

        Assert.False(motoristaLegal.Disabled);
        Assert.True(motoristaBloqueado.Disabled);
        Assert.Contains("Bloqueado", motoristaBloqueado.Text);
    }

    [Fact]
    public async Task OnGetMunicipiosAsync_deve_retornar_municipios_filtrados_por_regiao()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);

        context.Regioes.AddRange(
            new Regiao { Codigo = "RMPOA", Nome = "Região Metropolitana", Ordem = 1 },
            new Regiao { Codigo = "SERRA", Nome = "Serra", Ordem = 2 });
        context.Municipios.AddRange(
            new Municipio { Nome = "PORTO ALEGRE", RegiaoCodigo = "RMPOA" },
            new Municipio { Nome = "CANOAS", RegiaoCodigo = "RMPOA" },
            new Municipio { Nome = "CAXIAS DO SUL", RegiaoCodigo = "SERRA" });
        await context.SaveChangesAsync();

        var page = CreatePageModel(context, httpContext);

        var result = await page.OnGetMunicipiosAsync("RMPOA");

        var json = Assert.IsType<JsonResult>(result);
        var values = Assert.IsAssignableFrom<IEnumerable<object>>(json.Value);
        var serialized = System.Text.Json.JsonSerializer.Serialize(values);

        Assert.Contains("PORTO ALEGRE", serialized);
        Assert.Contains("CANOAS", serialized);
        Assert.DoesNotContain("CAXIAS DO SUL", serialized);
    }

    private static NovaViagemModel CreatePageModel(Eva.Data.EvaDbContext context, DefaultHttpContext httpContext)
    {
        var currentUserService = new CurrentUserService(context, new HttpContextAccessor { HttpContext = httpContext });
        var statusService = new EntityStatusService(context);
        var creationService = new ViagemCreationService(context);
        var rulesService = new ViagemRulesService(context, statusService);

        var page = new NovaViagemModel(context, currentUserService, statusService, creationService, rulesService)
        {
            PageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
            {
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };

        return page;
    }

    private static NovaViagemVM CreateValidInput(string veiculoPlaca, int motoristaId)
    {
        return new NovaViagemVM
        {
            ViagemTipoNome = "EVENTUAL",
            NomeContratante = "Contratante Teste",
            CpfCnpjContratante = "12345678901",
            RegiaoCodigo = "RMPOA",
            IdaEm = new DateTime(2026, 4, 10, 8, 0, 0),
            VoltaEm = new DateTime(2026, 4, 10, 18, 0, 0),
            MunicipioOrigem = "PORTO ALEGRE",
            MunicipioDestino = "CANOAS",
            VeiculoPlaca = veiculoPlaca,
            MotoristaId = motoristaId,
            Passageiros =
            [
                new PassageiroVM { Nome = "Pessoa 1", Documento = "11111111111" },
                new PassageiroVM { Nome = "Pessoa 2", Documento = "22222222222" }
            ]
        };
    }

    private static async Task SeedBaseViagemRefsAsync(Eva.Data.EvaDbContext context)
    {
        context.ViagemTipos.Add(new ViagemTipo { Nome = "EVENTUAL" });
        context.Regioes.Add(new Regiao { Codigo = "RMPOA", Nome = "Região Metropolitana", Ordem = 1 });
        context.Municipios.AddRange(
            new Municipio { Nome = "PORTO ALEGRE", RegiaoCodigo = "RMPOA" },
            new Municipio { Nome = "CANOAS", RegiaoCodigo = "RMPOA" });
        await context.SaveChangesAsync();
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

    private static async Task SeedEmpresaLegalAsync(Eva.Data.EvaDbContext context, string cnpj)
    {
        await SeedEmpresaAsync(context, cnpj);
        await SeedDocumentoEmpresaAsync(context, cnpj, "CARTAO_CNPJ");
    }

    private static async Task SeedVeiculoLegalAsync(Eva.Data.EvaDbContext context, string placa, string empresaCnpj)
    {
        context.Veiculos.Add(new Veiculo
        {
            Placa = placa,
            EmpresaCnpj = empresaCnpj,
            Modelo = "Ônibus"
        });

        await context.SaveChangesAsync();
        await SeedDocumentoVeiculoAsync(context, placa, "CRLV");
    }

    private static async Task<int> SeedMotoristaLegalAsync(Eva.Data.EvaDbContext context, string empresaCnpj, string nome)
    {
        var motorista = new Motorista
        {
            EmpresaCnpj = empresaCnpj,
            Nome = nome,
            Cpf = Guid.NewGuid().ToString("N")[..11],
            Cnh = Guid.NewGuid().ToString("N")[..11]
        };

        context.Motoristas.Add(motorista);
        await context.SaveChangesAsync();
        await SeedDocumentoMotoristaAsync(context, motorista.Id, "CNH");
        return motorista.Id;
    }

    private static async Task<int> SeedMotoristaAsync(Eva.Data.EvaDbContext context, string empresaCnpj, string nome)
    {
        var motorista = new Motorista
        {
            EmpresaCnpj = empresaCnpj,
            Nome = nome,
            Cpf = Guid.NewGuid().ToString("N")[..11],
            Cnh = Guid.NewGuid().ToString("N")[..11]
        };

        context.Motoristas.Add(motorista);
        await context.SaveChangesAsync();
        return motorista.Id;
    }

    private static async Task SeedDocumentoEmpresaAsync(Eva.Data.EvaDbContext context, string empresaCnpj, string tipo)
    {
        var fluxo = await SeedFluxoAsync(context, "EMPRESA", empresaCnpj);
        var documento = await SeedDocumentoAsync(context, tipo, fluxo.Id);
        context.DocumentoEmpresas.Add(new DocumentoEmpresa { Id = documento.Id, EmpresaCnpj = empresaCnpj });
        await context.SaveChangesAsync();
    }

    private static async Task SeedDocumentoVeiculoAsync(Eva.Data.EvaDbContext context, string placa, string tipo)
    {
        var fluxo = await SeedFluxoAsync(context, "VEICULO", placa);
        var documento = await SeedDocumentoAsync(context, tipo, fluxo.Id);
        context.DocumentoVeiculos.Add(new DocumentoVeiculo { Id = documento.Id, VeiculoPlaca = placa });
        await context.SaveChangesAsync();
    }

    private static async Task SeedDocumentoMotoristaAsync(Eva.Data.EvaDbContext context, int motoristaId, string tipo)
    {
        var fluxo = await SeedFluxoAsync(context, "MOTORISTA", motoristaId.ToString());
        var documento = await SeedDocumentoAsync(context, tipo, fluxo.Id);
        context.DocumentoMotoristas.Add(new DocumentoMotorista { Id = documento.Id, MotoristaId = motoristaId });
        await context.SaveChangesAsync();
    }

    private static async Task<FluxoPendencia> SeedFluxoAsync(Eva.Data.EvaDbContext context, string entidadeTipo, string entidadeId)
    {
        var fluxo = new FluxoPendencia
        {
            EntidadeTipo = entidadeTipo,
            EntidadeId = entidadeId,
            Status = "APROVADO",
            Analista = "analista@metroplan.rs.gov.br",
            CriadoEm = DateTime.UtcNow
        };

        context.FluxoPendencias.Add(fluxo);
        await context.SaveChangesAsync();
        return fluxo;
    }

    private static async Task<Documento> SeedDocumentoAsync(Eva.Data.EvaDbContext context, string tipo, int fluxoId)
    {
        var documento = new Documento
        {
            DocumentoTipoNome = tipo,
            Conteudo = [1, 2, 3],
            NomeArquivo = $"{tipo.ToLowerInvariant()}.pdf",
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow,
            AprovadoEm = DateTime.UtcNow,
            FluxoPendenciaId = fluxoId
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();
        return documento;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}

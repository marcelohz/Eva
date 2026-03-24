using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Pages.Empresa;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    public async Task EditarEmpresa_OnPostAsync_deve_retornar_page_com_erro_quando_empresa_esta_em_analise()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedFluxoAsync(context, "EMPRESA", "12345678000199", "EM_ANALISE");

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
    public async Task EditarVeiculo_OnGetAsync_deve_retornar_not_found_quando_veiculo_nao_pertence_a_empresa_logada()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedEmpresaAsync(context, "99999999000199");
        await SeedVeiculoAsync(context, "ABC1D23", "99999999000199");

        var page = CreateEditarVeiculoModel(context, httpContext);

        var result = await page.OnGetAsync("ABC1D23");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditarVeiculo_OnPostUploadAsync_deve_retornar_redirect_sem_salvar_documento_quando_veiculo_esta_em_analise()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedVeiculoAsync(context, "ABC1D23", "12345678000199");
        await SeedFluxoAsync(context, "VEICULO", "ABC1D23", "EM_ANALISE");

        var page = CreateEditarVeiculoModel(context, httpContext);
        page.TipoDocumentoUpload = "CRLV";
        page.UploadArquivo = CreatePdfFormFile("crlv.pdf");

        var result = await page.OnPostUploadAsync("ABC1D23");

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("ABC1D23", redirect.RouteValues!["id"]);
        Assert.Empty(await context.Documentos.ToListAsync());
    }

    [Fact]
    public async Task EditarEmpresa_OnPostUploadAsync_deve_substituir_documento_rejeitado_e_reabrir_fluxo()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedDocumentoEmpresaAsync(context, "12345678000199", "CARTAO_CNPJ", "cartao-antigo.pdf");
        await SeedFluxoAsync(context, "EMPRESA", "12345678000199", "REJEITADO", motivo: "Corrigir documento");

        var page = CreateEditarEmpresaModel(context, httpContext);
        page.TipoDocumentoUpload = "CARTAO_CNPJ";
        page.UploadArquivo = CreatePdfFormFile("cartao-novo.pdf");

        await Assert.ThrowsAsync<ArgumentNullException>(() => page.OnPostUploadAsync());

        var docs = await context.DocumentoEmpresas
            .Where(de => de.EmpresaCnpj == "12345678000199")
            .Include(de => de.Documento)
            .Select(de => de.Documento)
            .ToListAsync();
        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        var doc = Assert.Single(docs);
        Assert.Equal("cartao-novo.pdf", doc.NomeArquivo);
        Assert.Equal("AGUARDANDO_ANALISE", atual.Status);
    }

    [Fact]
    public async Task EditarMotorista_OnGetAsync_deve_retornar_not_found_quando_motorista_nao_pertence_a_empresa_logada()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedEmpresaAsync(context, "99999999000199");
        var motoristaId = await SeedMotoristaAsync(context, "99999999000199", "Motorista Externo");

        var page = CreateEditarMotoristaModel(context, httpContext);

        var result = await page.OnGetAsync(motoristaId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task EditarVeiculo_OnPostUploadAsync_deve_substituir_documento_rejeitado_e_reabrir_fluxo()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedVeiculoAsync(context, "ABC1D23", "12345678000199");
        await SeedDocumentoVeiculoAsync(context, "ABC1D23", "CRLV", "crlv-antigo.pdf");
        await SeedFluxoAsync(context, "VEICULO", "ABC1D23", "REJEITADO", motivo: "Corrigir CRLV");

        var page = CreateEditarVeiculoModel(context, httpContext);
        page.TipoDocumentoUpload = "CRLV";
        page.UploadArquivo = CreatePdfFormFile("crlv-novo.pdf");

        await Assert.ThrowsAsync<ArgumentNullException>(() => page.OnPostUploadAsync("ABC1D23"));

        var docs = await context.DocumentoVeiculos
            .Where(dv => dv.VeiculoPlaca == "ABC1D23")
            .Include(dv => dv.Documento)
            .Select(dv => dv.Documento)
            .ToListAsync();
        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "VEICULO" && p.EntidadeId == "ABC1D23");

        var doc = Assert.Single(docs);
        Assert.Equal("crlv-novo.pdf", doc.NomeArquivo);
        Assert.Equal("AGUARDANDO_ANALISE", atual.Status);
    }

    [Fact]
    public async Task EditarMotorista_OnPostAsync_deve_retornar_page_com_erro_quando_motorista_esta_em_analise()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        var motoristaId = await SeedMotoristaAsync(context, "12345678000199", "João da Silva");
        await SeedFluxoAsync(context, "MOTORISTA", motoristaId.ToString(), "EM_ANALISE");

        var page = CreateEditarMotoristaModel(context, httpContext);
        page.Input = new MotoristaVM
        {
            Id = motoristaId,
            Nome = "João Alterado",
            Cpf = "12345678901",
            Cnh = "98765432100"
        };

        var result = await page.OnPostAsync();

        var pageResult = Assert.IsType<PageResult>(result);
        Assert.Same(pageResult, result);
        Assert.Contains(page.ModelState[string.Empty]!.Errors, e => e.ErrorMessage == EmpresaEntityEditGuardService.LockedMessage);
    }

    [Fact]
    public async Task EditarMotorista_OnPostUploadAsync_deve_substituir_documento_rejeitado_e_reabrir_fluxo()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        var motoristaId = await SeedMotoristaAsync(context, "12345678000199", "JoÃ£o da Silva");
        await SeedDocumentoMotoristaAsync(context, motoristaId, "CNH", "cnh-antiga.pdf");
        await SeedFluxoAsync(context, "MOTORISTA", motoristaId.ToString(), "REJEITADO", motivo: "Corrigir CNH");

        var page = CreateEditarMotoristaModel(context, httpContext);
        page.UploadArquivo = CreatePdfFormFile("cnh-nova.pdf");

        await Assert.ThrowsAsync<ArgumentNullException>(() => page.OnPostUploadAsync(motoristaId));

        var docs = await context.DocumentoMotoristas
            .Where(dm => dm.MotoristaId == motoristaId)
            .Include(dm => dm.Documento)
            .Select(dm => dm.Documento)
            .ToListAsync();
        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "MOTORISTA" && p.EntidadeId == motoristaId.ToString());

        var doc = Assert.Single(docs);
        Assert.Equal("cnh-nova.pdf", doc.NomeArquivo);
        Assert.Equal("AGUARDANDO_ANALISE", atual.Status);
    }

    [Fact]
    public async Task EditarEmpresa_OnPostAsync_deve_reabrir_fluxo_quando_empresa_foi_rejeitada_e_corrigida()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedFluxoAsync(context, "EMPRESA", "12345678000199", "REJEITADO", motivo: "Corrigir cadastro");

        var page = CreateEditarEmpresaModel(context, httpContext);
        page.Input = new EmpresaVM
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Corrigida",
            NomeFantasia = "Empresa Corrigida"
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/MinhaEmpresa", redirect.PageName);

        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.Equal("INCOMPLETO", atual.Status);
        Assert.Contains("Empresa Corrigida", atual.DadosPropostos);
        Assert.Contains("12345678000199", atual.DadosPropostos);
    }

    [Fact]
    public async Task EditarVeiculo_OnPostAsync_deve_reabrir_fluxo_quando_veiculo_foi_rejeitado_e_corrigido()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedVeiculoAsync(context, "ABC1D23", "12345678000199");
        await SeedFluxoAsync(context, "VEICULO", "ABC1D23", "REJEITADO", motivo: "Corrigir dados");

        var page = CreateEditarVeiculoModel(context, httpContext);
        page.Input = new VeiculoVM
        {
            Placa = "ABC1D23",
            Modelo = "Ônibus Corrigido",
            NumeroLugares = 42,
            AnoFabricacao = 2020,
            ModeloAno = 2021
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/MeusVeiculos", redirect.PageName);

        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "VEICULO" && p.EntidadeId == "ABC1D23");

        Assert.Equal("INCOMPLETO", atual.Status);
        Assert.Contains("Ônibus Corrigido", atual.DadosPropostos);
    }

    [Fact]
    public async Task EditarMotorista_OnPostAsync_deve_reabrir_fluxo_quando_motorista_foi_rejeitado_e_corrigido()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        var motoristaId = await SeedMotoristaAsync(context, "12345678000199", "João da Silva");
        await SeedFluxoAsync(context, "MOTORISTA", motoristaId.ToString(), "REJEITADO", motivo: "Corrigir CNH");

        var page = CreateEditarMotoristaModel(context, httpContext);
        page.Input = new MotoristaVM
        {
            Id = motoristaId,
            Nome = "João Corrigido",
            Cpf = "12345678901",
            Cnh = "98765432100"
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./MeusMotoristas", redirect.PageName);

        var atual = await context.VPendenciasAtuais
            .FirstAsync(p => p.EntidadeTipo == "MOTORISTA" && p.EntidadeId == motoristaId.ToString());

        Assert.Equal("INCOMPLETO", atual.Status);
        Assert.Contains("João Corrigido", atual.DadosPropostos);
    }

    [Fact]
    public async Task EditarEmpresa_OnPostAsync_nao_deve_reabrir_fluxo_quando_empresa_rejeitada_for_salva_sem_alteracoes()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedFluxoAsync(context, "EMPRESA", "12345678000199", "REJEITADO", motivo: "Corrigir cadastro");

        var page = CreateEditarEmpresaModel(context, httpContext);
        page.Input = new EmpresaVM
        {
            Cnpj = "12345678000199",
            Nome = "Empresa 12345678000199",
            NomeFantasia = "Empresa 12345678000199",
            Email = "12345678000199@teste.com"
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/MinhaEmpresa", redirect.PageName);

        var historico = await context.FluxoPendencias
            .Where(f => f.EntidadeTipo == "EMPRESA" && f.EntidadeId == "12345678000199")
            .OrderBy(f => f.Id)
            .ToListAsync();

        Assert.Single(historico);
        Assert.Equal("REJEITADO", historico[0].Status);
    }

    [Fact]
    public async Task EditarVeiculo_OnPostAsync_nao_deve_reabrir_fluxo_quando_veiculo_rejeitado_for_salvo_sem_alteracoes()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        await SeedVeiculoAsync(context, "ABC1D23", "12345678000199");
        await SeedFluxoAsync(context, "VEICULO", "ABC1D23", "REJEITADO", motivo: "Corrigir dados");

        var page = CreateEditarVeiculoModel(context, httpContext);
        page.Input = new VeiculoVM
        {
            Placa = "ABC1D23",
            Modelo = "Ônibus"
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Empresa/MeusVeiculos", redirect.PageName);

        var historico = await context.FluxoPendencias
            .Where(f => f.EntidadeTipo == "VEICULO" && f.EntidadeId == "ABC1D23")
            .OrderBy(f => f.Id)
            .ToListAsync();

        Assert.Single(historico);
        Assert.Equal("REJEITADO", historico[0].Status);
    }

    [Fact]
    public async Task EditarMotorista_OnPostAsync_nao_deve_reabrir_fluxo_quando_motorista_rejeitado_for_salvo_sem_alteracoes()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);
        await SeedEmpresaAsync(context, "12345678000199");
        var motorista = new Motorista
        {
            EmpresaCnpj = "12345678000199",
            Nome = "João da Silva",
            Cpf = "12345678901",
            Cnh = "1234567890"
        };
        context.Motoristas.Add(motorista);
        await context.SaveChangesAsync();
        var motoristaId = motorista.Id;
        await SeedFluxoAsync(context, "MOTORISTA", motoristaId.ToString(), "REJEITADO", motivo: "Corrigir CNH");

        var page = CreateEditarMotoristaModel(context, httpContext);
        page.Input = new MotoristaVM
        {
            Id = motoristaId,
            Nome = "João da Silva",
            Cpf = "12345678901",
            Cnh = "1234567890"
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./MeusMotoristas", redirect.PageName);

        var historico = await context.FluxoPendencias
            .Where(f => f.EntidadeTipo == "MOTORISTA" && f.EntidadeId == motoristaId.ToString())
            .OrderBy(f => f.Id)
            .ToListAsync();

        Assert.Single(historico);
        Assert.Equal("REJEITADO", historico[0].Status);
    }

    private static EditarEmpresaModel CreateEditarEmpresaModel(Eva.Data.EvaDbContext context, DefaultHttpContext httpContext)
    {
        var currentUserService = new CurrentUserService(context, new HttpContextAccessor { HttpContext = httpContext });
        var pendenciaService = new PendenciaService(context, new TestBackgroundJobClient());
        var arquivoService = new ArquivoService(context, pendenciaService);
        var guardService = new EmpresaEntityEditGuardService(context, currentUserService, pendenciaService);

        var page = new EditarEmpresaModel(context, pendenciaService, arquivoService, currentUserService, guardService);
        InitializePageModel(page, httpContext);
        return page;
    }

    private static EditarVeiculoModel CreateEditarVeiculoModel(Eva.Data.EvaDbContext context, DefaultHttpContext httpContext)
    {
        var currentUserService = new CurrentUserService(context, new HttpContextAccessor { HttpContext = httpContext });
        var pendenciaService = new PendenciaService(context, new TestBackgroundJobClient());
        var arquivoService = new ArquivoService(context, pendenciaService);
        var guardService = new EmpresaEntityEditGuardService(context, currentUserService, pendenciaService);

        var page = new EditarVeiculoModel(context, pendenciaService, arquivoService, currentUserService, guardService);
        InitializePageModel(page, httpContext);
        return page;
    }

    private static EditarMotoristaModel CreateEditarMotoristaModel(Eva.Data.EvaDbContext context, DefaultHttpContext httpContext)
    {
        var currentUserService = new CurrentUserService(context, new HttpContextAccessor { HttpContext = httpContext });
        var pendenciaService = new PendenciaService(context, new TestBackgroundJobClient());
        var arquivoService = new ArquivoService(context, pendenciaService);
        var guardService = new EmpresaEntityEditGuardService(context, currentUserService, pendenciaService);

        var page = new EditarMotoristaModel(context, pendenciaService, arquivoService, currentUserService, guardService);
        InitializePageModel(page, httpContext);
        return page;
    }

    private static void InitializePageModel(PageModel page, DefaultHttpContext httpContext)
    {
        var pageContext = new PageContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()))
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };

        page.PageContext = pageContext;
    }

    private static async Task SeedEmpresaAsync(Eva.Data.EvaDbContext context, string cnpj)
    {
        context.Empresas.Add(new Empresa
        {
            Cnpj = cnpj,
            Nome = $"Empresa {cnpj}",
            NomeFantasia = $"Empresa {cnpj}",
            Email = $"{cnpj}@teste.com"
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedVeiculoAsync(Eva.Data.EvaDbContext context, string placa, string empresaCnpj)
    {
        context.Veiculos.Add(new Veiculo
        {
            Placa = placa,
            EmpresaCnpj = empresaCnpj,
            Modelo = "Ônibus"
        });

        await context.SaveChangesAsync();
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

    private static async Task SeedFluxoAsync(Eva.Data.EvaDbContext context, string entidadeTipo, string entidadeId, string status, string? motivo = null)
    {
        context.FluxoPendencias.Add(new FluxoPendencia
        {
            EntidadeTipo = entidadeTipo,
            EntidadeId = entidadeId,
            Status = status,
            Analista = "analista@metroplan.rs.gov.br",
            Motivo = motivo,
            CriadoEm = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedDocumentoEmpresaAsync(Eva.Data.EvaDbContext context, string empresaCnpj, string tipoDocumento, string nomeArquivo)
    {
        var documento = new Documento
        {
            DocumentoTipoNome = tipoDocumento,
            Conteudo = [1, 2, 3],
            NomeArquivo = nomeArquivo,
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow.AddDays(-1)
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();

        context.DocumentoEmpresas.Add(new DocumentoEmpresa
        {
            Id = documento.Id,
            EmpresaCnpj = empresaCnpj
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedDocumentoVeiculoAsync(Eva.Data.EvaDbContext context, string placa, string tipoDocumento, string nomeArquivo)
    {
        var documento = new Documento
        {
            DocumentoTipoNome = tipoDocumento,
            Conteudo = [1, 2, 3],
            NomeArquivo = nomeArquivo,
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow.AddDays(-1)
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();

        context.DocumentoVeiculos.Add(new DocumentoVeiculo
        {
            Id = documento.Id,
            VeiculoPlaca = placa
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedDocumentoMotoristaAsync(Eva.Data.EvaDbContext context, int motoristaId, string tipoDocumento, string nomeArquivo)
    {
        var documento = new Documento
        {
            DocumentoTipoNome = tipoDocumento,
            Conteudo = [1, 2, 3],
            NomeArquivo = nomeArquivo,
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow.AddDays(-1)
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();

        context.DocumentoMotoristas.Add(new DocumentoMotorista
        {
            Id = documento.Id,
            MotoristaId = motoristaId
        });

        await context.SaveChangesAsync();
    }

    private static IFormFile CreatePdfFormFile(string fileName)
    {
        var bytes = "%PDF-1.4 teste"u8.ToArray();
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }
}

using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Services;

[Trait("Category", "integration")]
public class ArquivoServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SalvarDocumentoAsync_deve_salvar_documento_vincular_empresa_e_abrir_fluxo()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var pendenciaService = new PendenciaService(context, new TestBackgroundJobClient());
        var arquivoService = new ArquivoService(context, pendenciaService);

        var file = CreatePdfFormFile("cartao-cnpj.pdf");

        var documento = await arquivoService.SalvarDocumentoAsync(file, "CARTAO_CNPJ", "EMPRESA", "12345678000199");

        var docPersistido = await context.Documentos.FirstAsync(d => d.Id == documento.Id);
        var link = await context.DocumentoEmpresas.FirstOrDefaultAsync(de => de.Id == documento.Id);
        var atual = await context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.Equal("cartao-cnpj.pdf", docPersistido.NomeArquivo);
        Assert.Equal("application/pdf", docPersistido.ContentType);
        Assert.NotNull(docPersistido.Hash);
        Assert.NotNull(link);
        Assert.Equal("12345678000199", link!.EmpresaCnpj);
        Assert.NotNull(atual);
        Assert.Equal("AGUARDANDO_ANALISE", atual!.Status);
    }

    [Fact]
    public async Task SalvarDocumentoAsync_deve_rejeitar_arquivo_com_assinatura_invalida()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var pendenciaService = new PendenciaService(context, new TestBackgroundJobClient());
        var arquivoService = new ArquivoService(context, pendenciaService);

        var file = CreateFormFile("texto.txt", "text/plain", "arquivo invalido"u8.ToArray());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            arquivoService.SalvarDocumentoAsync(file, "CARTAO_CNPJ", "EMPRESA", "12345678000199"));

        Assert.Equal("Formato de arquivo não suportado. Apenas PDF, JPG e PNG são permitidos.", ex.Message);
    }

    [Fact]
    public async Task DeletarDocumentoAsync_deve_remover_documento_e_retornar_entidade_para_incompleto()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var pendenciaService = new PendenciaService(context, new TestBackgroundJobClient());
        var arquivoService = new ArquivoService(context, pendenciaService);

        var documento = await arquivoService.SalvarDocumentoAsync(
            CreatePdfFormFile("cartao-cnpj.pdf"),
            "CARTAO_CNPJ",
            "EMPRESA",
            "12345678000199");

        await arquivoService.DeletarDocumentoAsync(documento.Id, "EMPRESA", "12345678000199");

        var docRemovido = await context.Documentos.FirstOrDefaultAsync(d => d.Id == documento.Id);
        var atual = await context.VPendenciasAtuais.FirstAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == "12345678000199");

        Assert.Null(docRemovido);
        Assert.Equal("INCOMPLETO", atual.Status);
        Assert.Null(atual.Analista);
    }

    [Fact]
    public async Task DeletarDocumentoAsync_deve_bloquear_retorno_para_fila_quando_item_esta_com_outro_analista()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var pendenciaService = new PendenciaService(context, new TestBackgroundJobClient());
        var arquivoService = new ArquivoService(context, pendenciaService);

        var documento = await arquivoService.SalvarDocumentoAsync(
            CreatePdfFormFile("cartao-cnpj.pdf"),
            "CARTAO_CNPJ",
            "EMPRESA",
            "12345678000199");

        await pendenciaService.IniciarAnaliseAsync("EMPRESA", "12345678000199", "analista@metroplan.rs.gov.br");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            arquivoService.DeletarDocumentoAsync(documento.Id, "EMPRESA", "12345678000199"));

        Assert.Equal("Apenas o analista atual pode devolver o item para a fila, ou solicite a um Administrador.", ex.Message);
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

    private static IFormFile CreatePdfFormFile(string fileName)
    {
        var content = "%PDF-1.4 teste"u8.ToArray();
        return CreateFormFile(fileName, "application/pdf", content);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}

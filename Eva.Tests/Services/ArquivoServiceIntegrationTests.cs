using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Eva.Workflow;
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
    public async Task SalvarDocumentoAsync_deve_salvar_documento_e_vincular_ao_draft_da_submissao()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var arquivoService = new ArquivoService(context, new SubmissaoService(context));

        var documento = await arquivoService.SalvarDocumentoAsync(
            CreatePdfFormFile("cartao-cnpj.pdf"),
            "CARTAO_CNPJ",
            "EMPRESA",
            "12345678000199");

        var draft = await context.Submissoes.SingleAsync();
        var draftDoc = await context.SubmissaoDocumentos.SingleAsync();

        Assert.Equal(SubmissaoWorkflow.EmEdicao, draft.Status);
        Assert.Equal(documento.Id, draftDoc.DocumentoId);
        Assert.Equal(SubmissaoWorkflow.RevisaoPendente, draftDoc.StatusRevisao);
        Assert.True(draftDoc.AtivoNaSubmissao);
    }

    [Fact]
    public async Task SalvarDocumentoAsync_deve_manter_multiplos_ativos_para_identidade_socio()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var arquivoService = new ArquivoService(context, new SubmissaoService(context));

        await arquivoService.SalvarDocumentoAsync(CreatePdfFormFile("socio-1.pdf"), "IDENTIDADE_SOCIO", "EMPRESA", "12345678000199");
        await arquivoService.SalvarDocumentoAsync(CreatePdfFormFile("socio-2.pdf"), "IDENTIDADE_SOCIO", "EMPRESA", "12345678000199");

        var docs = await context.SubmissaoDocumentos
            .Where(sd => sd.DocumentoTipoNome == "IDENTIDADE_SOCIO")
            .OrderBy(sd => sd.Id)
            .ToListAsync();

        Assert.Equal(2, docs.Count);
        Assert.All(docs, d => Assert.True(d.AtivoNaSubmissao));
    }

    [Fact]
    public async Task SalvarDocumentoAsync_deve_rejeitar_arquivo_com_assinatura_invalida()
    {
        await using var context = _database.CreateDbContext();
        await SeedEmpresaAsync(context, "12345678000199", "empresa@teste.com");

        var arquivoService = new ArquivoService(context, new SubmissaoService(context));

        var file = CreateFormFile("texto.txt", "text/plain", "arquivo invalido"u8.ToArray());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            arquivoService.SalvarDocumentoAsync(file, "CARTAO_CNPJ", "EMPRESA", "12345678000199"));

        Assert.Equal("Formato de arquivo não suportado. Apenas PDF, JPG e PNG são permitidos.", ex.Message);
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

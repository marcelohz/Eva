using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;

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
    public async Task GetHealthAsync_deve_indicar_documento_obrigatorio_faltante_para_empresa()
    {
        await using var context = _database.CreateDbContext();

        context.Empresas.Add(new Empresa
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Teste"
        });

        await context.SaveChangesAsync();

        var service = new EntityStatusService(context);

        var health = await service.GetHealthAsync("EMPRESA", "12345678000199");

        Assert.False(health.IsLegal);
        Assert.Contains("CARTAO_CNPJ", health.MissingMandatoryDocs);
        Assert.Equal("INCOMPLETO", health.AnalystStatus);
        Assert.Equal("INCOMPLETO", health.CurrentStatus);
    }

    [Fact]
    public async Task GetHealthAsync_deve_considerar_empresa_legal_quando_aprovada_e_com_documento_obrigatorio_aprovado()
    {
        await using var context = _database.CreateDbContext();

        context.Empresas.Add(new Empresa
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Teste"
        });

        await context.SaveChangesAsync();

        var fluxo = new FluxoPendencia
        {
            EntidadeTipo = "EMPRESA",
            EntidadeId = "12345678000199",
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
            EmpresaCnpj = "12345678000199"
        });

        await context.SaveChangesAsync();

        var service = new EntityStatusService(context);

        var health = await service.GetHealthAsync("EMPRESA", "12345678000199");

        Assert.True(health.IsLegal);
        Assert.Empty(health.MissingMandatoryDocs);
        Assert.Empty(health.ExpiredDocs);
        Assert.Equal("APROVADO", health.AnalystStatus);
        Assert.Equal("APROVADO", health.CurrentStatus);
    }

    [Fact]
    public async Task GetHealthAsync_deve_bloquear_veiculo_com_documento_obrigatorio_vencido()
    {
        await using var context = _database.CreateDbContext();

        context.Empresas.Add(new Empresa
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Teste"
        });

        context.Veiculos.Add(new Veiculo
        {
            Placa = "ABC1D23",
            EmpresaCnpj = "12345678000199",
            Modelo = "Ônibus"
        });

        await context.SaveChangesAsync();

        var fluxo = new FluxoPendencia
        {
            EntidadeTipo = "VEICULO",
            EntidadeId = "ABC1D23",
            Status = "APROVADO",
            Analista = "analista@metroplan.rs.gov.br",
            CriadoEm = DateTime.UtcNow
        };

        context.FluxoPendencias.Add(fluxo);
        await context.SaveChangesAsync();

        var documento = new Documento
        {
            DocumentoTipoNome = "CRLV",
            Conteudo = [1, 2, 3],
            NomeArquivo = "crlv.pdf",
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow,
            AprovadoEm = DateTime.UtcNow.AddDays(-30),
            Validade = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            FluxoPendenciaId = fluxo.Id
        };

        context.Documentos.Add(documento);
        await context.SaveChangesAsync();

        context.DocumentoVeiculos.Add(new DocumentoVeiculo
        {
            Id = documento.Id,
            VeiculoPlaca = "ABC1D23"
        });

        await context.SaveChangesAsync();

        var service = new EntityStatusService(context);

        var health = await service.GetHealthAsync("VEICULO", "ABC1D23");

        Assert.False(health.IsLegal);
        Assert.Contains("CRLV", health.ExpiredDocs);
        Assert.Equal("APROVADO", health.AnalystStatus);
    }
}

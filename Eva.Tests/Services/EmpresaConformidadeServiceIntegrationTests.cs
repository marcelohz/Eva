using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;

namespace Eva.Tests.Services;

[Trait("Category", "integration")]
public class EmpresaConformidadeServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetResumoConformidadeAsync_deve_retornar_dashboard_sem_pendencias_criticas_quando_tudo_esta_aprovado()
    {
        await using var context = _database.CreateDbContext();

        await SeedEmpresaAsync(context, "12345678000199", "Empresa Teste");
        await SeedVeiculoAsync(context, "ABC1D23", "12345678000199", "Ônibus Executivo");
        var motoristaId = await SeedMotoristaAsync(context, "12345678000199", "João da Silva");

        await AprovarEmpresaComDocumentoAsync(context, "12345678000199");
        await AprovarVeiculoComDocumentoAsync(context, "ABC1D23");
        await AprovarMotoristaComDocumentoAsync(context, motoristaId);

        var service = new EmpresaConformidadeService(context, new EntityStatusService(context));

        var dashboard = await service.GetResumoConformidadeAsync("12345678000199");

        Assert.False(dashboard.TemPendenciasCriticas);
        Assert.Equal(0, dashboard.TotalPendenciasCriticas);

        Assert.NotNull(dashboard.Empresa);
        Assert.Equal("APROVADO", dashboard.Empresa!.StatusGeral);
        Assert.False(dashboard.Empresa.IsBlocked);

        var veiculo = Assert.Single(dashboard.Veiculos);
        Assert.Equal("APROVADO", veiculo.StatusGeral);
        Assert.False(veiculo.IsBlocked);

        var motorista = Assert.Single(dashboard.Motoristas);
        Assert.Equal("APROVADO", motorista.StatusGeral);
        Assert.False(motorista.IsBlocked);
    }

    [Fact]
    public async Task GetResumoConformidadeAsync_deve_contar_veiculo_sem_documento_obrigatorio_como_pendencia_critica()
    {
        await using var context = _database.CreateDbContext();

        await SeedEmpresaAsync(context, "12345678000199", "Empresa Teste");
        await SeedVeiculoAsync(context, "ABC1D23", "12345678000199", "Ônibus Executivo");
        var motoristaId = await SeedMotoristaAsync(context, "12345678000199", "João da Silva");

        await AprovarEmpresaComDocumentoAsync(context, "12345678000199");
        await AprovarMotoristaComDocumentoAsync(context, motoristaId);

        var service = new EmpresaConformidadeService(context, new EntityStatusService(context));

        var dashboard = await service.GetResumoConformidadeAsync("12345678000199");

        Assert.True(dashboard.TemPendenciasCriticas);
        Assert.Equal(1, dashboard.TotalPendenciasCriticas);

        var veiculo = Assert.Single(dashboard.Veiculos);
        Assert.True(veiculo.IsBlocked);
        Assert.Equal("INCOMPLETO", veiculo.StatusGeral);
        Assert.Contains("CRLV", veiculo.DocumentosFaltantes);
    }

    [Fact]
    public async Task GetResumoConformidadeAsync_deve_exibir_motivo_de_rejeicao_do_motorista()
    {
        await using var context = _database.CreateDbContext();

        await SeedEmpresaAsync(context, "12345678000199", "Empresa Teste");
        var motoristaId = await SeedMotoristaAsync(context, "12345678000199", "João da Silva");

        await AprovarEmpresaComDocumentoAsync(context, "12345678000199");
        await AprovarMotoristaComDocumentoAsync(context, motoristaId);
        await RejeitarMotoristaAsync(context, motoristaId, "CNH ilegível");

        var service = new EmpresaConformidadeService(context, new EntityStatusService(context));

        var dashboard = await service.GetResumoConformidadeAsync("12345678000199");

        Assert.True(dashboard.TemPendenciasCriticas);
        Assert.Equal(1, dashboard.TotalPendenciasCriticas);

        var motorista = Assert.Single(dashboard.Motoristas);
        Assert.True(motorista.IsBlocked);
        Assert.Equal("REJEITADO", motorista.StatusGeral);
        Assert.Equal("CNH ilegível", motorista.MotivoRejeicao);
    }

    [Fact]
    public async Task GetResumoConformidadeAsync_deve_listar_documento_em_analise_sem_marcar_entidade_como_bloqueada_quando_nao_ha_outras_pendencias()
    {
        await using var context = _database.CreateDbContext();

        await SeedEmpresaAsync(context, "12345678000199", "Empresa Teste");
        await SeedDocumentoEmpresaAsync(
            context,
            "12345678000199",
            "CARTAO_CNPJ",
            "AGUARDANDO_ANALISE",
            aprovadoEm: null);

        var service = new EmpresaConformidadeService(context, new EntityStatusService(context));

        var dashboard = await service.GetResumoConformidadeAsync("12345678000199");

        Assert.NotNull(dashboard.Empresa);
        Assert.Equal("AGUARDANDO_ANALISE", dashboard.Empresa!.StatusGeral);
        Assert.False(dashboard.Empresa.IsBlocked);
        Assert.Contains("CARTAO_CNPJ", dashboard.Empresa.DocumentosEmAnalise);
        Assert.Equal(0, dashboard.TotalPendenciasCriticas);
    }

    private static async Task SeedEmpresaAsync(Eva.Data.EvaDbContext context, string cnpj, string nome)
    {
        context.Empresas.Add(new Empresa
        {
            Cnpj = cnpj,
            Nome = nome,
            NomeFantasia = nome,
            Email = "empresa@teste.com"
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedVeiculoAsync(Eva.Data.EvaDbContext context, string placa, string empresaCnpj, string modelo)
    {
        context.Veiculos.Add(new Veiculo
        {
            Placa = placa,
            EmpresaCnpj = empresaCnpj,
            Modelo = modelo
        });

        await context.SaveChangesAsync();
    }

    private static async Task<int> SeedMotoristaAsync(Eva.Data.EvaDbContext context, string empresaCnpj, string nome)
    {
        var motorista = new Motorista
        {
            EmpresaCnpj = empresaCnpj,
            Nome = nome,
            Cpf = "12345678901",
            Cnh = Guid.NewGuid().ToString("N")[..11]
        };

        context.Motoristas.Add(motorista);
        await context.SaveChangesAsync();
        return motorista.Id;
    }

    private static Task AprovarEmpresaComDocumentoAsync(Eva.Data.EvaDbContext context, string cnpj) =>
        SeedDocumentoEmpresaAsync(context, cnpj, "CARTAO_CNPJ", "APROVADO", DateTime.UtcNow);

    private static Task AprovarVeiculoComDocumentoAsync(Eva.Data.EvaDbContext context, string placa) =>
        SeedDocumentoVeiculoAsync(context, placa, "CRLV", "APROVADO", DateTime.UtcNow);

    private static Task AprovarMotoristaComDocumentoAsync(Eva.Data.EvaDbContext context, int motoristaId) =>
        SeedDocumentoMotoristaAsync(context, motoristaId, "CNH", "APROVADO", DateTime.UtcNow);

    private static async Task RejeitarMotoristaAsync(Eva.Data.EvaDbContext context, int motoristaId, string motivo)
    {
        context.FluxoPendencias.Add(new FluxoPendencia
        {
            EntidadeTipo = "MOTORISTA",
            EntidadeId = motoristaId.ToString(),
            Status = "REJEITADO",
            Analista = "analista@metroplan.rs.gov.br",
            Motivo = motivo,
            CriadoEm = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedDocumentoEmpresaAsync(
        Eva.Data.EvaDbContext context,
        string empresaCnpj,
        string tipo,
        string statusFluxo,
        DateTime? aprovadoEm)
    {
        var fluxo = await CriarFluxoAsync(context, "EMPRESA", empresaCnpj, statusFluxo);

        var documento = new Documento
        {
            DocumentoTipoNome = tipo,
            Conteudo = [1, 2, 3],
            NomeArquivo = $"{tipo.ToLowerInvariant()}.pdf",
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow,
            AprovadoEm = aprovadoEm,
            FluxoPendenciaId = fluxo.Id
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

    private static async Task SeedDocumentoVeiculoAsync(
        Eva.Data.EvaDbContext context,
        string placa,
        string tipo,
        string statusFluxo,
        DateTime? aprovadoEm)
    {
        var fluxo = await CriarFluxoAsync(context, "VEICULO", placa, statusFluxo);

        var documento = new Documento
        {
            DocumentoTipoNome = tipo,
            Conteudo = [1, 2, 3],
            NomeArquivo = $"{tipo.ToLowerInvariant()}.pdf",
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow,
            AprovadoEm = aprovadoEm,
            FluxoPendenciaId = fluxo.Id
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

    private static async Task SeedDocumentoMotoristaAsync(
        Eva.Data.EvaDbContext context,
        int motoristaId,
        string tipo,
        string statusFluxo,
        DateTime? aprovadoEm)
    {
        var fluxo = await CriarFluxoAsync(context, "MOTORISTA", motoristaId.ToString(), statusFluxo);

        var documento = new Documento
        {
            DocumentoTipoNome = tipo,
            Conteudo = [1, 2, 3],
            NomeArquivo = $"{tipo.ToLowerInvariant()}.pdf",
            ContentType = "application/pdf",
            DataUpload = DateTime.UtcNow,
            AprovadoEm = aprovadoEm,
            FluxoPendenciaId = fluxo.Id
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

    private static async Task<FluxoPendencia> CriarFluxoAsync(
        Eva.Data.EvaDbContext context,
        string entidadeTipo,
        string entidadeId,
        string status)
    {
        var fluxo = new FluxoPendencia
        {
            EntidadeTipo = entidadeTipo,
            EntidadeId = entidadeId,
            Status = status,
            Analista = status == "AGUARDANDO_ANALISE" || status == "INCOMPLETO" ? null : "analista@metroplan.rs.gov.br",
            CriadoEm = DateTime.UtcNow
        };

        context.FluxoPendencias.Add(fluxo);
        await context.SaveChangesAsync();
        return fluxo;
    }
}

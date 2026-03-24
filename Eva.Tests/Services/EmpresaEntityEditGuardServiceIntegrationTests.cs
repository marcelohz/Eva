using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Eva.Tests.Services;

[Trait("Category", "integration")]
public class EmpresaEntityEditGuardServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.EnsureReadyAsync();
        await _database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CheckEmpresaAsync_deve_bloquear_edicao_quando_status_esta_em_analise()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);

        context.Empresas.Add(new Empresa
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Teste",
            NomeFantasia = "Empresa Teste",
            Email = "empresa@teste.com"
        });

        context.FluxoPendencias.Add(new FluxoPendencia
        {
            EntidadeTipo = "EMPRESA",
            EntidadeId = "12345678000199",
            Status = "EM_ANALISE",
            Analista = "analista@metroplan.rs.gov.br",
            CriadoEm = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, httpContext);

        var result = await service.CheckEmpresaAsync("12345678000199");

        Assert.True(result.ExistsAndBelongsToCurrentEmpresa);
        Assert.True(result.IsLocked);
        Assert.False(result.CanEdit);
        Assert.Equal("EM_ANALISE", result.Status);
    }

    [Fact]
    public async Task CheckVeiculoAsync_deve_retornar_nao_encontrado_quando_veiculo_nao_pertence_a_empresa_logada()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);

        context.Empresas.AddRange(
            new Empresa
            {
                Cnpj = "12345678000199",
                Nome = "Empresa A",
                NomeFantasia = "Empresa A",
                Email = "empresa-a@teste.com"
            },
            new Empresa
            {
                Cnpj = "99999999000199",
                Nome = "Empresa B",
                NomeFantasia = "Empresa B",
                Email = "empresa-b@teste.com"
            });

        context.Veiculos.Add(new Veiculo
        {
            Placa = "ABC1D23",
            EmpresaCnpj = "99999999000199",
            Modelo = "Ônibus"
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, httpContext);

        var result = await service.CheckVeiculoAsync("ABC1D23");

        Assert.True(result.HasCurrentEmpresa);
        Assert.False(result.ExistsAndBelongsToCurrentEmpresa);
        Assert.False(result.CanEdit);
    }

    [Fact]
    public async Task CheckMotoristaAsync_deve_retornar_nao_encontrado_quando_motorista_nao_pertence_a_empresa_logada()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);

        context.Empresas.AddRange(
            new Empresa
            {
                Cnpj = "12345678000199",
                Nome = "Empresa A",
                NomeFantasia = "Empresa A",
                Email = "empresa-a@teste.com"
            },
            new Empresa
            {
                Cnpj = "99999999000199",
                Nome = "Empresa B",
                NomeFantasia = "Empresa B",
                Email = "empresa-b@teste.com"
            });

        var motorista = new Motorista
        {
            EmpresaCnpj = "99999999000199",
            Nome = "Motorista Externo",
            Cpf = "12345678901",
            Cnh = "98765432100"
        };

        context.Motoristas.Add(motorista);
        await context.SaveChangesAsync();

        var service = CreateService(context, httpContext);

        var result = await service.CheckMotoristaAsync(motorista.Id);

        Assert.True(result.HasCurrentEmpresa);
        Assert.False(result.ExistsAndBelongsToCurrentEmpresa);
        Assert.False(result.CanEdit);
    }

    [Fact]
    public async Task CheckVeiculoAsync_deve_permitir_edicao_quando_veiculo_pertence_a_empresa_e_nao_esta_bloqueado()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = _database.CreateDbContext(httpContext);

        context.Empresas.Add(new Empresa
        {
            Cnpj = "12345678000199",
            Nome = "Empresa Teste",
            NomeFantasia = "Empresa Teste",
            Email = "empresa@teste.com"
        });

        context.Veiculos.Add(new Veiculo
        {
            Placa = "ABC1D23",
            EmpresaCnpj = "12345678000199",
            Modelo = "Ônibus"
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, httpContext);

        var result = await service.CheckVeiculoAsync("abc1d23");

        Assert.True(result.CanEdit);
        Assert.False(result.IsLocked);
        Assert.Equal("ABC1D23", result.EntityId);
    }

    private static EmpresaEntityEditGuardService CreateService(Eva.Data.EvaDbContext context, DefaultHttpContext httpContext)
    {
        var currentUserService = new CurrentUserService(context, new HttpContextAccessor { HttpContext = httpContext });
        var pendenciaService = new PendenciaService(context, new TestBackgroundJobClient());
        return new EmpresaEntityEditGuardService(context, currentUserService, pendenciaService);
    }
}

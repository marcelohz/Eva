using Eva.Models;
using Eva.Services;
using Eva.Tests.Infrastructure;

namespace Eva.Tests.Services;

[Trait("Category", "unit")]
public class ViagemRulesServiceTests
{
    [Fact]
    public async Task ValidateEligibilityAsync_deve_bloquear_empresa_ilegal()
    {
        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(ValidateEligibilityAsync_deve_bloquear_empresa_ilegal));

        var statusService = new FakeEntityStatusService()
            .WithHealth("EMPRESA", "12345678000199", new EntityHealthReport { IsLegal = false });

        var service = new ViagemRulesService(context, statusService);

        var result = await service.ValidateEligibilityAsync(CreateRequest());

        Assert.False(result.IsAllowed);
        Assert.Equal(ViagemEligibilityFailure.CompanyNotLegal, result.Failure);
    }

    [Fact]
    public async Task ValidateEligibilityAsync_deve_permitir_cenario_totalmente_legal()
    {
        await using var context = await CreateContextWithLegalParticipantsAsync(
            nameof(ValidateEligibilityAsync_deve_permitir_cenario_totalmente_legal));

        var legalHealth = new EntityHealthReport { IsLegal = true };
        var statusService = new FakeEntityStatusService()
            .WithHealth("EMPRESA", "12345678000199", legalHealth)
            .WithHealth("VEICULO", "ABC1D23", legalHealth)
            .WithHealth("MOTORISTA", "42", legalHealth);

        var service = new ViagemRulesService(context, statusService);

        var result = await service.ValidateEligibilityAsync(CreateRequest());

        Assert.True(result.IsAllowed);
        Assert.Equal(ViagemEligibilityFailure.None, result.Failure);
    }

    [Fact]
    public async Task ValidateEligibilityAsync_deve_bloquear_motorista_auxiliar_igual_ao_principal()
    {
        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(ValidateEligibilityAsync_deve_bloquear_motorista_auxiliar_igual_ao_principal));

        var service = new ViagemRulesService(context, new FakeEntityStatusService());

        var result = await service.ValidateEligibilityAsync(CreateRequest(motoristaAuxId: 42));

        Assert.False(result.IsAllowed);
        Assert.Equal(ViagemEligibilityFailure.DuplicateDriver, result.Failure);
        Assert.Equal("Input.MotoristaAuxId", result.FieldKey);
    }

    [Theory]
    [InlineData(false, true, true, null, ViagemEligibilityFailure.CompanyNotLegal, null)]
    [InlineData(true, false, true, null, ViagemEligibilityFailure.VehicleNotLegal, "Input.VeiculoPlaca")]
    [InlineData(true, true, false, null, ViagemEligibilityFailure.DriverNotLegal, "Input.MotoristaId")]
    [InlineData(true, true, true, false, ViagemEligibilityFailure.AuxDriverNotLegal, "Input.MotoristaAuxId")]
    [InlineData(true, true, true, true, ViagemEligibilityFailure.None, null)]
    public async Task ValidateEligibilityAsync_deve_respeitar_matriz_de_legalidade_dos_participantes(
        bool empresaLegal,
        bool veiculoLegal,
        bool motoristaLegal,
        bool? motoristaAuxLegal,
        ViagemEligibilityFailure expectedFailure,
        string? expectedFieldKey)
    {
        var testName = $"{nameof(ValidateEligibilityAsync_deve_respeitar_matriz_de_legalidade_dos_participantes)}_{empresaLegal}_{veiculoLegal}_{motoristaLegal}_{motoristaAuxLegal}";
        await using var context = await CreateContextWithLegalParticipantsAsync(testName, includeAuxDriver: motoristaAuxLegal.HasValue);

        var statusService = new FakeEntityStatusService()
            .WithHealth("EMPRESA", "12345678000199", new EntityHealthReport { IsLegal = empresaLegal })
            .WithHealth("VEICULO", "ABC1D23", new EntityHealthReport { IsLegal = veiculoLegal })
            .WithHealth("MOTORISTA", "42", new EntityHealthReport { IsLegal = motoristaLegal });

        if (motoristaAuxLegal.HasValue)
        {
            statusService.WithHealth("MOTORISTA", "43", new EntityHealthReport { IsLegal = motoristaAuxLegal.Value });
        }

        var service = new ViagemRulesService(context, statusService);
        var result = await service.ValidateEligibilityAsync(CreateRequest(motoristaAuxId: motoristaAuxLegal.HasValue ? 43 : null));

        Assert.Equal(expectedFailure == ViagemEligibilityFailure.None, result.IsAllowed);
        Assert.Equal(expectedFailure, result.Failure);
        Assert.Equal(expectedFieldKey, result.FieldKey);
    }

    [Fact]
    public async Task ValidateEligibilityAsync_deve_bloquear_veiculo_que_nao_pertence_a_empresa()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(ValidateEligibilityAsync_deve_bloquear_veiculo_que_nao_pertence_a_empresa),
            httpContext);

        context.Veiculos.Add(new Veiculo
        {
            Placa = "ABC1D23",
            EmpresaCnpj = "99999999000199",
            Modelo = "Ônibus"
        });

        context.Motoristas.Add(new Motorista
        {
            Id = 42,
            EmpresaCnpj = "12345678000199",
            Nome = "Motorista Teste",
            Cpf = "12345678901",
            Cnh = "1234567890"
        });

        await context.SaveChangesAsync();

        var legalHealth = new EntityHealthReport { IsLegal = true };
        var statusService = new FakeEntityStatusService()
            .WithHealth("EMPRESA", "12345678000199", legalHealth)
            .WithHealth("VEICULO", "ABC1D23", legalHealth)
            .WithHealth("MOTORISTA", "42", legalHealth);

        var service = new ViagemRulesService(context, statusService);
        var result = await service.ValidateEligibilityAsync(CreateRequest());

        Assert.False(result.IsAllowed);
        Assert.Equal(ViagemEligibilityFailure.VehicleNotFound, result.Failure);
        Assert.Equal("Input.VeiculoPlaca", result.FieldKey);
    }

    [Fact]
    public async Task ValidateEligibilityAsync_deve_bloquear_motorista_que_nao_pertence_a_empresa()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(ValidateEligibilityAsync_deve_bloquear_motorista_que_nao_pertence_a_empresa),
            httpContext);

        context.Veiculos.Add(new Veiculo
        {
            Placa = "ABC1D23",
            EmpresaCnpj = "12345678000199",
            Modelo = "Ônibus"
        });

        context.Motoristas.Add(new Motorista
        {
            Id = 42,
            EmpresaCnpj = "99999999000199",
            Nome = "Motorista Externo",
            Cpf = "12345678901",
            Cnh = "1234567890"
        });

        await context.SaveChangesAsync();

        var legalHealth = new EntityHealthReport { IsLegal = true };
        var statusService = new FakeEntityStatusService()
            .WithHealth("EMPRESA", "12345678000199", legalHealth)
            .WithHealth("VEICULO", "ABC1D23", legalHealth)
            .WithHealth("MOTORISTA", "42", legalHealth);

        var service = new ViagemRulesService(context, statusService);
        var result = await service.ValidateEligibilityAsync(CreateRequest());

        Assert.False(result.IsAllowed);
        Assert.Equal(ViagemEligibilityFailure.DriverNotFound, result.Failure);
        Assert.Equal("Input.MotoristaId", result.FieldKey);
    }

    [Fact]
    public async Task ValidateEligibilityAsync_deve_bloquear_motorista_auxiliar_que_nao_pertence_a_empresa()
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(ValidateEligibilityAsync_deve_bloquear_motorista_auxiliar_que_nao_pertence_a_empresa),
            httpContext);

        context.Veiculos.Add(new Veiculo
        {
            Placa = "ABC1D23",
            EmpresaCnpj = "12345678000199",
            Modelo = "Ônibus"
        });

        context.Motoristas.AddRange(
            new Motorista
            {
                Id = 42,
                EmpresaCnpj = "12345678000199",
                Nome = "Motorista Principal",
                Cpf = "12345678901",
                Cnh = "1234567890"
            },
            new Motorista
            {
                Id = 43,
                EmpresaCnpj = "99999999000199",
                Nome = "Motorista Auxiliar Externo",
                Cpf = "10987654321",
                Cnh = "0987654321"
            });

        await context.SaveChangesAsync();

        var legalHealth = new EntityHealthReport { IsLegal = true };
        var statusService = new FakeEntityStatusService()
            .WithHealth("EMPRESA", "12345678000199", legalHealth)
            .WithHealth("VEICULO", "ABC1D23", legalHealth)
            .WithHealth("MOTORISTA", "42", legalHealth)
            .WithHealth("MOTORISTA", "43", legalHealth);

        var service = new ViagemRulesService(context, statusService);
        var result = await service.ValidateEligibilityAsync(CreateRequest(motoristaAuxId: 43));

        Assert.False(result.IsAllowed);
        Assert.Equal(ViagemEligibilityFailure.AuxDriverNotFound, result.Failure);
        Assert.Equal("Input.MotoristaAuxId", result.FieldKey);
    }

    [Fact]
    public async Task ValidateEligibilityAsync_deve_bloquear_viagem_sem_passageiros()
    {
        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(ValidateEligibilityAsync_deve_bloquear_viagem_sem_passageiros));

        var service = new ViagemRulesService(context, new FakeEntityStatusService());
        var result = await service.ValidateEligibilityAsync(CreateRequest(passageiroCount: 0));

        Assert.False(result.IsAllowed);
        Assert.Equal(ViagemEligibilityFailure.EmptyPassengers, result.Failure);
        Assert.Equal("Input.Passageiros", result.FieldKey);
    }

    [Fact]
    public async Task ValidateEligibilityAsync_deve_bloquear_viagem_com_volta_antes_da_ida()
    {
        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(ValidateEligibilityAsync_deve_bloquear_viagem_com_volta_antes_da_ida));

        var service = new ViagemRulesService(context, new FakeEntityStatusService());
        var result = await service.ValidateEligibilityAsync(CreateRequest(
            idaEm: new DateTime(2026, 4, 1, 8, 0, 0),
            voltaEm: new DateTime(2026, 4, 1, 7, 0, 0)));

        Assert.False(result.IsAllowed);
        Assert.Equal(ViagemEligibilityFailure.ReturnBeforeDeparture, result.Failure);
        Assert.Equal("Input.VoltaEm", result.FieldKey);
    }

    private static ViagemEligibilityRequest CreateRequest(
        DateTime? idaEm = null,
        DateTime? voltaEm = null,
        int? motoristaAuxId = null,
        int passageiroCount = 3)
    {
        return new ViagemEligibilityRequest
        {
            EmpresaCnpj = "12345678000199",
            IdaEm = idaEm ?? new DateTime(2026, 4, 1, 8, 0, 0),
            VoltaEm = voltaEm ?? new DateTime(2026, 4, 1, 18, 0, 0),
            VeiculoPlaca = "ABC1D23",
            MotoristaId = 42,
            MotoristaAuxId = motoristaAuxId,
            PassageiroCount = passageiroCount
        };
    }

    private static async Task<Eva.Data.EvaDbContext> CreateContextWithLegalParticipantsAsync(string databaseName, bool includeAuxDriver = false)
    {
        var httpContext = TestHttpContextBuilder.WithEmpresa("12345678000199").Build();
        var context = TestDbContextFactory.CreateInMemoryContext(databaseName, httpContext);

        context.Veiculos.Add(new Veiculo
        {
            Placa = "ABC1D23",
            EmpresaCnpj = "12345678000199",
            Modelo = "Ônibus"
        });

        context.Motoristas.Add(new Motorista
        {
            Id = 42,
            EmpresaCnpj = "12345678000199",
            Nome = "Motorista Teste",
            Cpf = "12345678901",
            Cnh = "1234567890"
        });

        if (includeAuxDriver)
        {
            context.Motoristas.Add(new Motorista
            {
                Id = 43,
                EmpresaCnpj = "12345678000199",
                Nome = "Motorista Auxiliar",
                Cpf = "10987654321",
                Cnh = "0987654321"
            });
        }

        await context.SaveChangesAsync();
        return context;
    }

    private sealed class FakeEntityStatusService : IEntityStatusService
    {
        private readonly Dictionary<(string Tipo, string Id), EntityHealthReport> _reports = new();

        public FakeEntityStatusService WithHealth(string entityType, string entityId, EntityHealthReport report)
        {
            _reports[(entityType, entityId)] = report;
            return this;
        }

        public Task<EntityHealthReport> GetHealthAsync(string entityType, string entityId)
        {
            return Task.FromResult(_reports.GetValueOrDefault((entityType, entityId), new EntityHealthReport()));
        }

        public Task<Dictionary<string, EntityHealthReport>> GetBulkHealthAsync(string entityType, IEnumerable<string> entityIds)
        {
            var result = entityIds.ToDictionary(
                id => id,
                id => _reports.GetValueOrDefault((entityType, id), new EntityHealthReport()));

            return Task.FromResult(result);
        }
    }
}

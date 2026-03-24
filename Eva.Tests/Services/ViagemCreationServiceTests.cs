using Eva.Models.ViewModels;
using Eva.Services;
using Eva.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Eva.Tests.Services;

[Trait("Category", "unit")]
public class ViagemCreationServiceTests
{
    [Fact]
    public async Task CreateAsync_deve_criar_viagem_com_passageiros_e_pagamento_pendente()
    {
        var httpContext = TestHttpContextBuilder
            .WithEmpresa("12345678000199")
            .Build();

        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(CreateAsync_deve_criar_viagem_com_passageiros_e_pagamento_pendente),
            httpContext);

        var service = new ViagemCreationService(context);

        var result = await service.CreateAsync(new ViagemCreationRequest
        {
            EmpresaCnpj = "12345678000199",
            Input = new NovaViagemVM
            {
                ViagemTipoNome = "EVENTUAL",
                NomeContratante = "Cliente Teste",
                CpfCnpjContratante = "12345678901",
                RegiaoCodigo = "R01",
                IdaEm = new DateTime(2026, 4, 1, 8, 0, 0),
                VoltaEm = new DateTime(2026, 4, 1, 18, 0, 0),
                MunicipioOrigem = "Porto Alegre",
                MunicipioDestino = "Canoas",
                VeiculoPlaca = "ABC1D23",
                MotoristaId = 42,
                Descricao = "Viagem de teste",
                Passageiros =
                [
                    new PassageiroVM { Nome = "Passageiro 1", Documento = "11111111111" },
                    new PassageiroVM { Nome = "Passageiro 2", Documento = "22222222222" }
                ]
            }
        });

        var viagem = await context.Viagens.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == result.ViagemId);

        Assert.NotNull(viagem);
        Assert.False(viagem!.Pago);
        Assert.Equal(385.50m, viagem.Valor);
        Assert.Equal("12345678000199", viagem.EmpresaCnpj);
        Assert.Equal(2, context.Passageiros.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task CreateAsync_deve_usar_valor_base_quando_origem_e_destino_forem_iguais()
    {
        var httpContext = TestHttpContextBuilder
            .WithEmpresa("12345678000199")
            .Build();

        await using var context = TestDbContextFactory.CreateInMemoryContext(
            nameof(CreateAsync_deve_usar_valor_base_quando_origem_e_destino_forem_iguais),
            httpContext);

        var service = new ViagemCreationService(context);

        var result = await service.CreateAsync(new ViagemCreationRequest
        {
            EmpresaCnpj = "12345678000199",
            Input = new NovaViagemVM
            {
                ViagemTipoNome = "EVENTUAL",
                NomeContratante = "Cliente Teste",
                CpfCnpjContratante = "12345678901",
                RegiaoCodigo = "R01",
                IdaEm = new DateTime(2026, 4, 1, 8, 0, 0),
                VoltaEm = new DateTime(2026, 4, 1, 18, 0, 0),
                MunicipioOrigem = "Porto Alegre",
                MunicipioDestino = "Porto Alegre",
                VeiculoPlaca = "ABC1D23",
                MotoristaId = 42,
                Passageiros =
                [
                    new PassageiroVM { Nome = "Passageiro 1", Documento = "11111111111" }
                ]
            }
        });

        Assert.Equal(150.00m, result.Valor);
    }
}

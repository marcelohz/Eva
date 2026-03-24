using System;
using Eva.Models;
using Eva.Services;

namespace Eva.Tests.Services;

[Trait("Category", "unit")]
public class ViagemManagementServiceTests
{
    [Fact]
    public void GetAccess_deve_permitir_edicao_completa_quando_viagem_nao_esta_paga()
    {
        var service = new ViagemManagementService(null!);

        var result = service.GetAccess(new Viagem
        {
            Pago = false,
            IdaEm = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc)
        }, new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ViagemEditMode.Full, result.EditMode);
        Assert.Equal("Editar", result.ActionLabel);
    }

    [Fact]
    public void GetAccess_deve_permitir_apenas_passageiros_quando_viagem_paga_esta_fora_da_janela_de_bloqueio()
    {
        var service = new ViagemManagementService(null!);

        var result = service.GetAccess(new Viagem
        {
            Pago = true,
            IdaEm = new DateTime(2026, 4, 1, 14, 30, 0, DateTimeKind.Utc)
        }, new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ViagemEditMode.PassengersOnly, result.EditMode);
        Assert.Equal("Editar Passageiros", result.ActionLabel);
    }

    [Fact]
    public void GetAccess_deve_bloquear_edicao_quando_viagem_paga_esta_dentro_da_janela_de_bloqueio()
    {
        var service = new ViagemManagementService(null!);

        var result = service.GetAccess(new Viagem
        {
            Pago = true,
            IdaEm = new DateTime(2026, 4, 1, 9, 30, 0, DateTimeKind.Utc)
        }, new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ViagemEditMode.ReadOnly, result.EditMode);
        Assert.Equal("Detalhes", result.ActionLabel);
    }
}

using System;
using System.Threading.Tasks;
using Eva.Data;
using Microsoft.EntityFrameworkCore;

namespace Eva.Services
{
    public enum ViagemEligibilityFailure
    {
        None = 0,
        ReturnBeforeDeparture,
        DuplicateDriver,
        EmptyPassengers,
        CompanyNotLegal,
        VehicleNotFound,
        VehicleNotLegal,
        DriverNotFound,
        DriverNotLegal,
        AuxDriverNotFound,
        AuxDriverNotLegal
    }

    public class ViagemEligibilityRequest
    {
        public string EmpresaCnpj { get; set; } = string.Empty;
        public DateTime IdaEm { get; set; }
        public DateTime VoltaEm { get; set; }
        public string VeiculoPlaca { get; set; } = string.Empty;
        public int MotoristaId { get; set; }
        public int? MotoristaAuxId { get; set; }
        public int PassageiroCount { get; set; }
    }

    public class ViagemEligibilityResult
    {
        public bool IsAllowed => Failure == ViagemEligibilityFailure.None;
        public ViagemEligibilityFailure Failure { get; init; }
        public string? FieldKey { get; init; }

        public static ViagemEligibilityResult Allowed() => new() { Failure = ViagemEligibilityFailure.None };

        public static ViagemEligibilityResult Denied(ViagemEligibilityFailure failure, string? fieldKey = null) =>
            new()
            {
                Failure = failure,
                FieldKey = fieldKey
            };
    }

    public interface IViagemRulesService
    {
        Task<ViagemEligibilityResult> ValidateEligibilityAsync(ViagemEligibilityRequest request);
    }

    public class ViagemRulesService : IViagemRulesService
    {
        private readonly EvaDbContext _context;
        private readonly IEntityStatusService _statusService;

        public ViagemRulesService(EvaDbContext context, IEntityStatusService statusService)
        {
            _context = context;
            _statusService = statusService;
        }

        public async Task<ViagemEligibilityResult> ValidateEligibilityAsync(ViagemEligibilityRequest request)
        {
            if (request.VoltaEm <= request.IdaEm)
            {
                return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.ReturnBeforeDeparture, "Input.VoltaEm");
            }

            if (request.MotoristaId != 0 &&
                request.MotoristaAuxId.HasValue &&
                request.MotoristaAuxId.Value != 0 &&
                request.MotoristaId == request.MotoristaAuxId.Value)
            {
                return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.DuplicateDriver, "Input.MotoristaAuxId");
            }

            if (request.PassageiroCount <= 0)
            {
                return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.EmptyPassengers, "Input.Passageiros");
            }

            var empresaHealth = await _statusService.GetHealthAsync("EMPRESA", request.EmpresaCnpj);
            if (!empresaHealth.IsLegal)
            {
                return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.CompanyNotLegal);
            }

            var veiculo = await _context.Veiculos
                .FirstOrDefaultAsync(v => v.Placa == request.VeiculoPlaca && v.EmpresaCnpj == request.EmpresaCnpj);

            if (veiculo == null)
            {
                return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.VehicleNotFound, "Input.VeiculoPlaca");
            }

            var veiculoHealth = await _statusService.GetHealthAsync("VEICULO", request.VeiculoPlaca);
            if (!veiculoHealth.IsLegal)
            {
                return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.VehicleNotLegal, "Input.VeiculoPlaca");
            }

            var motorista = await _context.Motoristas
                .FirstOrDefaultAsync(m => m.Id == request.MotoristaId && m.EmpresaCnpj == request.EmpresaCnpj);

            if (motorista == null)
            {
                return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.DriverNotFound, "Input.MotoristaId");
            }

            var motoristaHealth = await _statusService.GetHealthAsync("MOTORISTA", request.MotoristaId.ToString());
            if (!motoristaHealth.IsLegal)
            {
                return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.DriverNotLegal, "Input.MotoristaId");
            }

            if (request.MotoristaAuxId.HasValue && request.MotoristaAuxId.Value > 0)
            {
                var motoristaAux = await _context.Motoristas
                    .FirstOrDefaultAsync(m => m.Id == request.MotoristaAuxId.Value && m.EmpresaCnpj == request.EmpresaCnpj);

                if (motoristaAux == null)
                {
                    return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.AuxDriverNotFound, "Input.MotoristaAuxId");
                }

                var auxHealth = await _statusService.GetHealthAsync("MOTORISTA", request.MotoristaAuxId.Value.ToString());
                if (!auxHealth.IsLegal)
                {
                    return ViagemEligibilityResult.Denied(ViagemEligibilityFailure.AuxDriverNotLegal, "Input.MotoristaAuxId");
                }
            }

            return ViagemEligibilityResult.Allowed();
        }
    }
}

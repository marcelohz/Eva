using Eva.Data;
using Microsoft.EntityFrameworkCore;

namespace Eva.Services
{
    public interface IEmpresaEntityEditGuardService
    {
        Task<EmpresaEntityEditGuardResult> CheckEmpresaAsync(string empresaCnpj);
        Task<EmpresaEntityEditGuardResult> CheckVeiculoAsync(string placa);
        Task<EmpresaEntityEditGuardResult> CheckMotoristaAsync(int motoristaId);
    }

    public sealed class EmpresaEntityEditGuardService : IEmpresaEntityEditGuardService
    {
        public const string LockedMessage = "Este registro está em análise e não pode ser alterado no momento.";

        private readonly EvaDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public EmpresaEntityEditGuardService(
            EvaDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<EmpresaEntityEditGuardResult> CheckEmpresaAsync(string empresaCnpj)
        {
            var currentEmpresaCnpj = await GetCurrentEmpresaCnpjAsync();
            if (string.IsNullOrWhiteSpace(currentEmpresaCnpj))
            {
                return EmpresaEntityEditGuardResult.NoCurrentEmpresa();
            }

            var exists = await _context.Empresas.AnyAsync(e => e.Cnpj == empresaCnpj);
            if (!exists)
            {
                return EmpresaEntityEditGuardResult.NotFound(currentEmpresaCnpj);
            }

            if (!string.Equals(currentEmpresaCnpj, empresaCnpj, StringComparison.Ordinal))
            {
                return EmpresaEntityEditGuardResult.Forbidden(currentEmpresaCnpj);
            }

            var status = await _context.Submissoes
                .Where(s => s.EntidadeTipo == "EMPRESA" && s.EntidadeId == empresaCnpj)
                .OrderByDescending(s => s.Id)
                .Select(s => s.Status)
                .FirstOrDefaultAsync();
            return EmpresaEntityEditGuardResult.FromStatus(currentEmpresaCnpj, status);
        }

        public async Task<EmpresaEntityEditGuardResult> CheckVeiculoAsync(string placa)
        {
            var currentEmpresaCnpj = await GetCurrentEmpresaCnpjAsync();
            if (string.IsNullOrWhiteSpace(currentEmpresaCnpj))
            {
                return EmpresaEntityEditGuardResult.NoCurrentEmpresa();
            }

            var normalizedPlaca = placa.ToUpperInvariant().Trim();
            var belongsToCurrentEmpresa = await _context.Veiculos
                .AnyAsync(v => v.Placa.ToUpper() == normalizedPlaca && v.EmpresaCnpj == currentEmpresaCnpj);

            if (!belongsToCurrentEmpresa)
            {
                return EmpresaEntityEditGuardResult.NotFound(currentEmpresaCnpj);
            }

            var status = await _context.Submissoes
                .Where(s => s.EntidadeTipo == "VEICULO" && s.EntidadeId == normalizedPlaca)
                .OrderByDescending(s => s.Id)
                .Select(s => s.Status)
                .FirstOrDefaultAsync();
            return EmpresaEntityEditGuardResult.FromStatus(currentEmpresaCnpj, status, normalizedPlaca);
        }

        public async Task<EmpresaEntityEditGuardResult> CheckMotoristaAsync(int motoristaId)
        {
            var currentEmpresaCnpj = await GetCurrentEmpresaCnpjAsync();
            if (string.IsNullOrWhiteSpace(currentEmpresaCnpj))
            {
                return EmpresaEntityEditGuardResult.NoCurrentEmpresa();
            }

            var belongsToCurrentEmpresa = await _context.Motoristas
                .AnyAsync(m => m.Id == motoristaId && m.EmpresaCnpj == currentEmpresaCnpj);

            if (!belongsToCurrentEmpresa)
            {
                return EmpresaEntityEditGuardResult.NotFound(currentEmpresaCnpj);
            }

            var status = await _context.Submissoes
                .Where(s => s.EntidadeTipo == "MOTORISTA" && s.EntidadeId == motoristaId.ToString())
                .OrderByDescending(s => s.Id)
                .Select(s => s.Status)
                .FirstOrDefaultAsync();
            return EmpresaEntityEditGuardResult.FromStatus(currentEmpresaCnpj, status, motoristaId.ToString());
        }

        private async Task<string?> GetCurrentEmpresaCnpjAsync()
        {
            var user = await _currentUserService.GetCurrentUserAsync();
            return string.IsNullOrWhiteSpace(user?.EmpresaCnpj) ? null : user.EmpresaCnpj;
        }
    }

    public sealed record EmpresaEntityEditGuardResult(
        bool HasCurrentEmpresa,
        bool ExistsAndBelongsToCurrentEmpresa,
        bool IsLocked,
        string? CurrentEmpresaCnpj,
        string? EntityId,
        string? Status)
    {
        public bool CanEdit => HasCurrentEmpresa && ExistsAndBelongsToCurrentEmpresa && !IsLocked;

        public static EmpresaEntityEditGuardResult NoCurrentEmpresa() =>
            new(false, false, false, null, null, null);

        public static EmpresaEntityEditGuardResult NotFound(string currentEmpresaCnpj) =>
            new(true, false, false, currentEmpresaCnpj, null, null);

        public static EmpresaEntityEditGuardResult Forbidden(string currentEmpresaCnpj) =>
            new(true, false, false, currentEmpresaCnpj, null, null);

        public static EmpresaEntityEditGuardResult FromStatus(string currentEmpresaCnpj, string? status, string? entityId = null) =>
            new(
                true,
                true,
                status == Workflow.SubmissaoWorkflow.AguardandoAnalise ||
                status == Workflow.SubmissaoWorkflow.EmAnalise,
                currentEmpresaCnpj,
                entityId,
                status);
    }
}

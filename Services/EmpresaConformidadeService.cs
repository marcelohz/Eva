using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Workflow;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eva.Services
{
    public interface IEmpresaConformidadeService
    {
        Task<ConformidadeDashboardVM> GetResumoConformidadeAsync(string empresaCnpj);
    }

    public class EmpresaConformidadeService : IEmpresaConformidadeService
    {
        private readonly EvaDbContext _context;
        private readonly IEntityStatusService _entityStatusService;

        public EmpresaConformidadeService(EvaDbContext context, IEntityStatusService entityStatusService)
        {
            _context = context;
            _entityStatusService = entityStatusService;
        }

        public async Task<ConformidadeDashboardVM> GetResumoConformidadeAsync(string empresaCnpj)
        {
            var vm = new ConformidadeDashboardVM();

            // 1. Fetch Entities associated with the Company
            var empresa = await _context.Empresas.FirstOrDefaultAsync(e => e.Cnpj == empresaCnpj);
            if (empresa == null) return vm;

            var veiculos = await _context.Veiculos.Where(v => v.EmpresaCnpj == empresaCnpj).ToListAsync();
            var motoristas = await _context.Motoristas.Where(m => m.EmpresaCnpj == empresaCnpj).ToListAsync();

            var veiculoPlacas = veiculos.Select(v => v.Placa).ToList();
            var motoristaIds = motoristas.Select(m => m.Id.ToString()).ToList();

            // 2. Delegate to the core Health Engine
            var empresaHealthDict = await _entityStatusService.GetBulkHealthAsync("EMPRESA", new[] { empresaCnpj });
            var veiculosHealth = await _entityStatusService.GetBulkHealthAsync("VEICULO", veiculoPlacas);
            var motoristasHealth = await _entityStatusService.GetBulkHealthAsync("MOTORISTA", motoristaIds);

            // 3. Map Empresa
            var empHealth = empresaHealthDict.GetValueOrDefault(empresaCnpj, new EntityHealthReport());
            vm.Empresa = MapearEntidade(empresaCnpj, empresa.Nome, empHealth);
            if (vm.Empresa.IsBlocked) vm.TotalPendenciasCriticas++;

            // 4. Map Veiculos
            foreach (var veiculo in veiculos)
            {
                var health = veiculosHealth.GetValueOrDefault(veiculo.Placa, new EntityHealthReport());
                var entVm = MapearEntidade(veiculo.Placa, string.IsNullOrWhiteSpace(veiculo.Modelo) ? veiculo.Placa : $"{veiculo.Modelo} ({veiculo.Placa})", health);
                vm.Veiculos.Add(entVm);

                if (entVm.IsBlocked) vm.TotalPendenciasCriticas++;
            }

            // 5. Map Motoristas
            foreach (var motorista in motoristas)
            {
                var idStr = motorista.Id.ToString();
                var health = motoristasHealth.GetValueOrDefault(idStr, new EntityHealthReport());
                var entVm = MapearEntidade(idStr, motorista.Nome, health);
                vm.Motoristas.Add(entVm);

                if (entVm.IsBlocked) vm.TotalPendenciasCriticas++;
            }

            return vm;
        }

        private static ConformidadeEntidadeVM MapearEntidade(string id, string? nome, EntityHealthReport health)
        {
            var vm = new ConformidadeEntidadeVM
            {
                Id = id,
                Nome = string.IsNullOrWhiteSpace(nome) ? "N/A" : nome,
                StatusGeral = health.CurrentStatus ?? WorkflowStatus.Incompleto,
                MotivoRejeicao = health.LastRejectionReason,
                DocumentosFaltantes = health.MissingMandatoryDocs?.ToList() ?? new List<string>(),
                DocumentosVencidos = health.ExpiredDocs?.ToList() ?? new List<string>(),
                DocumentosEmAnalise = health.PendingDocs?.ToList() ?? new List<string>()
            };

            return vm;
        }
    }
}

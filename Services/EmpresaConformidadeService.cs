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

            // 3. Fetch Analyst Rejection Motives from the current workflow views
            var pendenciasAtuais = await _context.VPendenciasAtuais
                .Where(p => (p.EntidadeTipo.Trim().ToUpper() == "EMPRESA" && p.EntidadeId == empresaCnpj) ||
                            (p.EntidadeTipo.Trim().ToUpper() == "VEICULO" && veiculoPlacas.Contains(p.EntidadeId)) ||
                            (p.EntidadeTipo.Trim().ToUpper() == "MOTORISTA" && motoristaIds.Contains(p.EntidadeId)))
                .ToListAsync();

            // 4. Map Empresa
            var empHealth = empresaHealthDict.GetValueOrDefault(empresaCnpj, new EntityHealthReport());
            var empPendencia = pendenciasAtuais.FirstOrDefault(p => p.EntidadeTipo.Trim().ToUpper() == "EMPRESA" && p.EntidadeId == empresaCnpj);

            vm.Empresa = MapearEntidade(empresaCnpj, empresa.Nome, empHealth, empPendencia);
            if (vm.Empresa.IsBlocked) vm.TotalPendenciasCriticas++;

            // 5. Map Veiculos
            foreach (var veiculo in veiculos)
            {
                var health = veiculosHealth.GetValueOrDefault(veiculo.Placa, new EntityHealthReport());
                var pendencia = pendenciasAtuais.FirstOrDefault(p => p.EntidadeTipo.Trim().ToUpper() == "VEICULO" && p.EntidadeId == veiculo.Placa);

                var entVm = MapearEntidade(veiculo.Placa, string.IsNullOrWhiteSpace(veiculo.Modelo) ? veiculo.Placa : $"{veiculo.Modelo} ({veiculo.Placa})", health, pendencia);
                vm.Veiculos.Add(entVm);

                if (entVm.IsBlocked) vm.TotalPendenciasCriticas++;
            }

            // 6. Map Motoristas
            foreach (var motorista in motoristas)
            {
                var idStr = motorista.Id.ToString();
                var health = motoristasHealth.GetValueOrDefault(idStr, new EntityHealthReport());
                var pendencia = pendenciasAtuais.FirstOrDefault(p => p.EntidadeTipo.Trim().ToUpper() == "MOTORISTA" && p.EntidadeId == idStr);

                var entVm = MapearEntidade(idStr, motorista.Nome, health, pendencia);
                vm.Motoristas.Add(entVm);

                if (entVm.IsBlocked) vm.TotalPendenciasCriticas++;
            }

            return vm;
        }

        private ConformidadeEntidadeVM MapearEntidade(string id, string? nome, EntityHealthReport health, VPendenciaAtual? pendencia)
        {
            var vm = new ConformidadeEntidadeVM
            {
                Id = id,
                Nome = string.IsNullOrWhiteSpace(nome) ? "N/A" : nome,
                StatusGeral = health.CurrentStatus ?? WorkflowStatus.Incompleto,
                MotivoRejeicao = pendencia?.Motivo,
                DocumentosFaltantes = health.MissingMandatoryDocs?.ToList() ?? new List<string>(),
                DocumentosVencidos = health.ExpiredDocs?.ToList() ?? new List<string>(),
                DocumentosEmAnalise = health.PendingDocs?.ToList() ?? new List<string>()
            };

            return vm;
        }
    }
}

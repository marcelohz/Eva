using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;
using Eva.Workflow;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class MeusVeiculosModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly IEntityStatusService _statusService;

        public MeusVeiculosModel(EvaDbContext context, PendenciaService pendenciaService, IEntityStatusService statusService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
            _statusService = statusService;
        }

        public List<Veiculo> Veiculos { get; set; } = new();
        public Dictionary<string, EntityHealthReport> HealthReports { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            Veiculos = await _context.Veiculos
                .OrderBy(v => v.Placa)
                .ToListAsync();

            var placas = Veiculos.Select(v => v.Placa).ToList();
            HealthReports = await _statusService.GetBulkHealthAsync("VEICULO", placas);

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string placa)
        {
            if (string.IsNullOrEmpty(placa)) return RedirectToPage();

            var status = await _pendenciaService.GetStatusAsync("VEICULO", placa);
            if (status == WorkflowStatus.EmAnalise) return RedirectToPage();

            var veiculoInDb = await _context.Veiculos.FirstOrDefaultAsync(v => v.Placa == placa);

            if (veiculoInDb != null)
            {
                _context.Veiculos.Remove(veiculoInDb);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}

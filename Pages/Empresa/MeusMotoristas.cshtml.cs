using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class MeusMotoristasModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly IEntityStatusService _statusService;

        public MeusMotoristasModel(EvaDbContext context, PendenciaService pendenciaService, IEntityStatusService statusService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
            _statusService = statusService;
        }

        public List<Motorista> Motoristas { get; set; } = new();
        public Dictionary<string, EntityHealthReport> HealthReports { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            Motoristas = await _context.Motoristas
                .OrderBy(m => m.Nome)
                .ToListAsync();

            var ids = Motoristas.Select(m => m.Id.ToString()).ToList();
            HealthReports = await _statusService.GetBulkHealthAsync("MOTORISTA", ids);

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            if (id <= 0) return RedirectToPage();

            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());
            if (status == "EM_ANALISE") return RedirectToPage();

            var motoristaInDb = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id);

            if (motoristaInDb != null)
            {
                _context.Motoristas.Remove(motoristaInDb);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
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
using System.Security.Claims;
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class MeusMotoristasModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly IEntityStatusService _statusService;

        public MeusMotoristasModel(EvaDbContext context, IEntityStatusService statusService)
        {
            _context = context;
            _statusService = statusService;
        }

        public List<Motorista> Motoristas { get; set; } = new();
        public Dictionary<string, EntityHealthReport> HealthReports { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            Motoristas = await _context.Motoristas
                .Where(m => m.EmpresaCnpj == user.EmpresaCnpj)
                .OrderBy(m => m.Nome)
                .ToListAsync();

            var ids = Motoristas.Select(m => m.Id.ToString()).ToList();
            HealthReports = await _statusService.GetBulkHealthAsync("MOTORISTA", ids);

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            if (id <= 0) return RedirectToPage();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            var health = await _statusService.GetHealthAsync("MOTORISTA", id.ToString());
            if (health.IsLocked) return RedirectToPage();

            var motoristaInDb = await _context.Motoristas
                .FirstOrDefaultAsync(m => m.Id == id && m.EmpresaCnpj == user.EmpresaCnpj);

            if (motoristaInDb != null)
            {
                _context.Motoristas.Remove(motoristaInDb);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}

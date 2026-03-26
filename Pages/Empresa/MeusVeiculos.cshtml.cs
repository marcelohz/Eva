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
    public class MeusVeiculosModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly IEntityStatusService _statusService;

        public MeusVeiculosModel(EvaDbContext context, IEntityStatusService statusService)
        {
            _context = context;
            _statusService = statusService;
        }

        public List<Veiculo> Veiculos { get; set; } = new();
        public Dictionary<string, EntityHealthReport> HealthReports { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            Veiculos = await _context.Veiculos
                .Where(v => v.EmpresaCnpj == user.EmpresaCnpj)
                .OrderBy(v => v.Placa)
                .ToListAsync();

            var placas = Veiculos.Select(v => v.Placa).ToList();
            HealthReports = await _statusService.GetBulkHealthAsync("VEICULO", placas);

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string placa)
        {
            if (string.IsNullOrEmpty(placa)) return RedirectToPage();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            var health = await _statusService.GetHealthAsync("VEICULO", placa);
            if (health.IsLocked) return RedirectToPage();

            var veiculoInDb = await _context.Veiculos
                .FirstOrDefaultAsync(v => v.Placa == placa && v.EmpresaCnpj == user.EmpresaCnpj);

            if (veiculoInDb != null)
            {
                _context.Veiculos.Remove(veiculoInDb);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}

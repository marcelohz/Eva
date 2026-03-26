using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Eva.Services;
using Eva.Models.ViewModels;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class MinhaEmpresaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly IEmpresaConformidadeService _conformidadeService;

        public MinhaEmpresaModel(EvaDbContext context, IEmpresaConformidadeService conformidadeService)
        {
            _context = context;
            _conformidadeService = conformidadeService;
        }

        public int TotalVeiculos { get; set; }
        public int TotalMotoristas { get; set; }

        public ConformidadeDashboardVM ResumoConformidade { get; set; } = new ConformidadeDashboardVM();

        public async Task<IActionResult> OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            TotalVeiculos = await _context.Veiculos
                .CountAsync(v => v.EmpresaCnpj == user.EmpresaCnpj);

            TotalMotoristas = await _context.Motoristas
                .CountAsync(m => m.EmpresaCnpj == user.EmpresaCnpj);

            ResumoConformidade = await _conformidadeService.GetResumoConformidadeAsync(user.EmpresaCnpj);
            return Page();
        }
    }
}

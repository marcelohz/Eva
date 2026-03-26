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
    public class CentralConformidadeModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly IEmpresaConformidadeService _conformidadeService;

        public CentralConformidadeModel(EvaDbContext context, IEmpresaConformidadeService conformidadeService)
        {
            _context = context;
            _conformidadeService = conformidadeService;
        }

        public ConformidadeDashboardVM ResumoConformidade { get; set; } = new ConformidadeDashboardVM();

        public async Task<IActionResult> OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            ResumoConformidade = await _conformidadeService.GetResumoConformidadeAsync(user.EmpresaCnpj);
            return Page();
        }
    }
}

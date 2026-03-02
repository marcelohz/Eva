using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.Security.Claims;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA")]
    public class MeusVeiculosModel : PageModel
    {
        private readonly EvaDbContext _context;

        public MeusVeiculosModel(EvaDbContext context)
        {
            _context = context;
        }

        public List<Veiculo> Veiculos { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            Veiculos = await _context.Veiculos
                .Where(v => v.EmpresaCnpj == user.EmpresaCnpj)
                .OrderBy(v => v.Placa)
                .ToListAsync();

            return Page();
        }
    }
}
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
    public class MeusMotoristasModel : PageModel
    {
        private readonly EvaDbContext _context;

        public MeusMotoristasModel(EvaDbContext context)
        {
            _context = context;
        }

        public List<Motorista> Motoristas { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            Motoristas = await _context.Motoristas
                .Where(m => m.EmpresaCnpj == user.EmpresaCnpj)
                .OrderBy(m => m.Nome)
                .ToListAsync();

            return Page();
        }
    }
}
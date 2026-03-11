using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class MinhasViagensModel : PageModel
    {
        private readonly EvaDbContext _context;

        public MinhasViagensModel(EvaDbContext context)
        {
            _context = context;
        }

        public List<Viagem> Viagens { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            // Segurança: Garante que a empresa só veja suas próprias viagens
            Viagens = await _context.Viagens
                .Where(v => v.EmpresaCnpj == user.EmpresaCnpj)
                .OrderByDescending(v => v.Id)
                .ToListAsync();

            return Page();
        }
    }
}
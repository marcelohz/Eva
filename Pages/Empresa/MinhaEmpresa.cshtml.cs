using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using System.Security.Claims;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA")]
    public class MinhaEmpresaModel : PageModel
    {
        private readonly EvaDbContext _context;

        public MinhaEmpresaModel(EvaDbContext context)
        {
            _context = context;
        }

        public int TotalVeiculos { get; set; }
        public int TotalMotoristas { get; set; }

        public async Task OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user != null && !string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                TotalVeiculos = await _context.Veiculos
                    .CountAsync(v => v.EmpresaCnpj == user.EmpresaCnpj);

                TotalMotoristas = await _context.Motoristas
                    .CountAsync(m => m.EmpresaCnpj == user.EmpresaCnpj);
            }
        }
    }
}
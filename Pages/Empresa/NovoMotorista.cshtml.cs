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
    public class NovoMotoristaModel : PageModel
    {
        private readonly EvaDbContext _context;

        public NovoMotoristaModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Motorista Motorista { get; set; } = new();

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            // Assign company and setup defaults based on your schema
            Motorista.EmpresaCnpj = user.EmpresaCnpj;
            Motorista.DataCadastro = DateTime.UtcNow;

            _context.Motoristas.Add(Motorista);
            await _context.SaveChangesAsync();

            return RedirectToPage("./MeusMotoristas");
        }
    }
}
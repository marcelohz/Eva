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
    public class EditarMotoristaModel : PageModel
    {
        private readonly EvaDbContext _context;

        public EditarMotoristaModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Motorista Motorista { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null) return RedirectToPage("/Login");

            // Fetch and verify ownership
            Motorista = await _context.Motoristas
                .FirstOrDefaultAsync(m => m.Id == id && m.EmpresaCnpj == user.EmpresaCnpj);

            if (Motorista == null) return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            // Fetch the existing record to update
            var motoristaInDb = await _context.Motoristas
                .FirstOrDefaultAsync(m => m.Id == Motorista.Id && m.EmpresaCnpj == user.EmpresaCnpj);

            if (motoristaInDb == null) return NotFound();

            // Update allowed fields
            motoristaInDb.Nome = Motorista.Nome;
            motoristaInDb.Cpf = Motorista.Cpf;
            motoristaInDb.Cnh = Motorista.Cnh;
            motoristaInDb.Email = Motorista.Email;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MotoristaExists(Motorista.Id)) return NotFound();
                else throw;
            }

            return RedirectToPage("./MeusMotoristas");
        }

        private bool MotoristaExists(int id)
        {
            return _context.Motoristas.Any(e => e.Id == id);
        }
    }
}
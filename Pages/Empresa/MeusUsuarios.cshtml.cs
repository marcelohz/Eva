using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.Security.Claims;

namespace Eva.Pages.Empresa
{
    // THE SAFETY LOCK: Only the Master account can access this page.
    [Authorize(Roles = "EMPRESA")]
    public class MeusUsuariosModel : PageModel
    {
        private readonly EvaDbContext _context;

        public MeusUsuariosModel(EvaDbContext context)
        {
            _context = context;
        }

        public List<Usuario> Usuarios { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            // Global Query Filter guarantees only this CNPJ's users are returned
            Usuarios = await _context.Usuarios
                .OrderBy(u => u.Nome)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var loggedInEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            var userToDelete = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

            if (userToDelete != null)
            {
                // Prevent the Master account from accidentally deleting itself
                if (userToDelete.Email == loggedInEmail)
                {
                    TempData["ErrorMessage"] = "Ação bloqueada: Você não pode excluir a sua própria conta principal.";
                    return RedirectToPage();
                }

                _context.Usuarios.Remove(userToDelete);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Acesso do usuário revogado com sucesso.";
            }

            return RedirectToPage();
        }
    }
}
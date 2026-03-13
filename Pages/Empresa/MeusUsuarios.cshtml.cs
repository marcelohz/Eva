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
            var cnpj = User.FindFirstValue("EmpresaCnpj");

            // We use IgnoreQueryFilters so the admin can see revoked (Ativo = false) users for auditing.
            // Because we ignore global filters, we MUST manually enforce the multi-tenant isolation (EmpresaCnpj).
            Usuarios = await _context.Usuarios
                .IgnoreQueryFilters()
                .Where(u => u.EmpresaCnpj == cnpj)
                .OrderByDescending(u => u.Ativo) // Put active users at the top, revoked at the bottom
                .ThenBy(u => u.Nome)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var loggedInEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

            var userToDelete = await _context.Usuarios
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (userToDelete != null)
            {
                // Prevent the Master account from accidentally deleting itself
                if (userToDelete.Email == loggedInEmail)
                {
                    TempData["ErrorMessage"] = "Ação bloqueada: Você não pode excluir a sua própria conta principal.";
                    return RedirectToPage();
                }

                // Phase 2: Convert Hard Delete to Soft Delete
                userToDelete.Ativo = false;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Acesso do usuário revogado com sucesso.";
            }

            return RedirectToPage();
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;

namespace Eva.Pages.Metroplan.Admin
{
    [Authorize(Roles = "ADMIN")]
    public class IndexModel : PageModel
    {
        private readonly EvaDbContext _context;

        public IndexModel(EvaDbContext context)
        {
            _context = context;
        }

        public List<Usuario> Analistas { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Fetch all users who are strictly Analistas
            Analistas = await _context.Usuarios
                .Where(u => u.PapelNome == "ANALISTA")
                .OrderBy(u => u.Nome)
                .ToListAsync();
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.Security.Claims;

namespace Eva.Pages.Metroplan.Analista
{
    [Authorize(Roles = "ANALISTA")]
    public class IndexModel : PageModel
    {
        private readonly EvaDbContext _context;

        public IndexModel(EvaDbContext context)
        {
            _context = context;
        }

        public List<VPendenciaAtual> Fila { get; set; } = new();
        public string AnalistaAtual { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            AnalistaAtual = User.FindFirstValue(ClaimTypes.Email) ?? "";

            Fila = await _context.VPendenciasAtuais
                .Where(p => p.Status == "AGUARDANDO_ANALISE" || p.Status == "EM_ANALISE")
                .OrderBy(p => p.CriadoEm)
                .ToListAsync();
        }
    }
}
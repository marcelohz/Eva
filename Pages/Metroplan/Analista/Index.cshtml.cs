using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.Security.Claims;

namespace Eva.Pages.Metroplan.Analista
{
    [Authorize(Policy = "AcessoAnalista")]
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
                // PRIMARY SORT: "Is this assigned to me?" (True comes first)
                .OrderByDescending(p => p.Analista == AnalistaAtual)
                // SECONDARY SORT: Oldest tickets first
                .ThenBy(p => p.CriadoEm)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostDesatribuirAsync(string tipo, string id)
        {
            if (!User.IsInRole("ADMIN"))
            {
                return Forbid();
            }

            var lastPendencia = await _context.FluxoPendencias
                .Where(f => f.EntidadeTipo == tipo && f.EntidadeId == id)
                .OrderByDescending(f => f.CriadoEm)
                .FirstOrDefaultAsync();

            if (lastPendencia != null && lastPendencia.Status == "EM_ANALISE")
            {
                var novaPendencia = new FluxoPendencia
                {
                    EntidadeTipo = tipo,
                    EntidadeId = id,
                    Status = "AGUARDANDO_ANALISE",
                    Analista = null,
                    Motivo = "Desatribuição forçada por Administrador.",
                    CriadoEm = DateTime.UtcNow
                };

                _context.FluxoPendencias.Add(novaPendencia);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
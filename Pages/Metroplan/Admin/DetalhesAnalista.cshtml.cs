using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eva.Pages.Metroplan.Admin
{
    [Authorize(Roles = "ADMIN")]
    public class DetalhesAnalistaModel : PageModel
    {
        private readonly EvaDbContext _context;

        public DetalhesAnalistaModel(EvaDbContext context)
        {
            _context = context;
        }

        public Usuario? Analista { get; set; }
        public List<VPendenciaAtual> TicketsEmAnalise { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Analista = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && u.PapelNome == "ANALISTA");

            if (Analista == null)
            {
                return NotFound();
            }

            // Find all tickets currently locked by this specific analyst's email
            TicketsEmAnalise = await _context.VPendenciasAtuais
                .Where(p => p.Status == WorkflowStatus.EmAnalise && p.Analista == Analista.Email)
                .OrderBy(p => p.CriadoEm)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDesatribuirAsync(int id, string tipo, string entidadeId)
        {
            if (!User.IsInRole("ADMIN"))
            {
                return Forbid();
            }

            // Verify the last state of the ticket to ensure it is still EM_ANALISE
            var lastPendencia = await _context.FluxoPendencias
                .Where(f => f.EntidadeTipo == tipo && f.EntidadeId == entidadeId)
                .OrderByDescending(f => f.CriadoEm)
                .FirstOrDefaultAsync();

            if (lastPendencia != null && lastPendencia.Status == WorkflowStatus.EmAnalise)
            {
                // Create a new history record to release the ticket back to the queue
                var novaPendencia = new FluxoPendencia
                {
                    EntidadeTipo = tipo,
                    EntidadeId = entidadeId,
                    Status = WorkflowStatus.AguardandoAnalise,
                    Analista = null,
                    Motivo = "DesatribuiÃ§Ã£o forÃ§ada por Administrador via painel do analista.",
                    CriadoEm = DateTime.UtcNow
                };

                _context.FluxoPendencias.Add(novaPendencia);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"O ticket {entidadeId} foi desatribuÃ­do com sucesso.";
            }

            // Redirect back to the same analyst's detail page
            return RedirectToPage(new { id = id });
        }
    }
}

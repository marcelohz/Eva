using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eva.Data;
using Eva.Models;
using Eva.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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
        public List<Submissao> SubmissoesEmAnalise { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Analista = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && u.PapelNome == "ANALISTA");

            if (Analista == null)
            {
                return NotFound();
            }

            SubmissoesEmAnalise = await _context.Submissoes
                .Where(s => s.Status == SubmissaoWorkflow.EmAnalise && s.AnalistaAtual == Analista.Email)
                .OrderBy(s => s.AtualizadoEm)
                .ThenBy(s => s.Id)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDesatribuirAsync(int id, int submissaoId)
        {
            if (!User.IsInRole("ADMIN"))
            {
                return Forbid();
            }

            var submissao = await _context.Submissoes.FirstOrDefaultAsync(s => s.Id == submissaoId);
            if (submissao != null && submissao.Status == SubmissaoWorkflow.EmAnalise)
            {
                submissao.Status = SubmissaoWorkflow.AguardandoAnalise;
                submissao.AnalistaAtual = null;
                submissao.AtualizadoEm = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"A submissão #{submissao.Id} foi desatribuída com sucesso.";
            }

            return RedirectToPage(new { id });
        }
    }
}

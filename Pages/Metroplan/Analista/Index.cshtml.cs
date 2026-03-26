using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Eva.Data;
using Eva.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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

        public List<FilaSubmissaoVm> Fila { get; set; } = new();
        public string AnalistaAtual { get; set; } = string.Empty;

        public sealed class FilaSubmissaoVm
        {
            public int SubmissaoId { get; init; }
            public string EntidadeTipo { get; init; } = string.Empty;
            public string EntidadeId { get; init; } = string.Empty;
            public string Status { get; init; } = string.Empty;
            public string? AnalistaDaSubmissao { get; init; }
            public DateTime CriadoEm { get; init; }
            public DateTime? SubmetidoEm { get; init; }
        }

        public async Task OnGetAsync()
        {
            AnalistaAtual = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

            Fila = await _context.Submissoes
                .Where(s => s.Status == SubmissaoWorkflow.AguardandoAnalise || s.Status == SubmissaoWorkflow.EmAnalise)
                .OrderByDescending(s => s.AnalistaAtual == AnalistaAtual)
                .ThenBy(s => s.SubmetidoEm ?? s.CriadoEm)
                .Select(s => new FilaSubmissaoVm
                {
                    SubmissaoId = s.Id,
                    EntidadeTipo = s.EntidadeTipo,
                    EntidadeId = s.EntidadeId,
                    Status = s.Status,
                    AnalistaDaSubmissao = s.AnalistaAtual,
                    CriadoEm = s.CriadoEm,
                    SubmetidoEm = s.SubmetidoEm
                })
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostDesatribuirAsync(int submissaoId)
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
            }

            return RedirectToPage();
        }
    }
}

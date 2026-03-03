using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;
using System.Security.Claims;

namespace Eva.Pages.Metroplan.Analista
{
    [Authorize(Roles = "ANALISTA")]
    public class RevisaoModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;

        public RevisaoModel(EvaDbContext context, PendenciaService pendenciaService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
        }

        [BindProperty(SupportsGet = true)]
        public string Tipo { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string Id { get; set; } = string.Empty;

        [BindProperty]
        public string MotivoRejeicao { get; set; } = string.Empty;

        public VPendenciaAtual? Ticket { get; set; }
        public List<FluxoPendencia> Historico { get; set; } = new();
        public bool IsLockedByMe { get; set; }
        public bool IsLockedByOther { get; set; }

        // Data Models
        public Veiculo? Veiculo { get; set; }
        public Motorista? Motorista { get; set; }
        public Eva.Models.Empresa? Empresa { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Tipo) || string.IsNullOrEmpty(Id)) return RedirectToPage("./Index");

            Ticket = await _context.VPendenciasAtuais
                .FirstOrDefaultAsync(p => p.EntidadeTipo == Tipo && p.EntidadeId == Id);

            if (Ticket == null) return RedirectToPage("./Index");

            var email = User.FindFirstValue(ClaimTypes.Email);
            IsLockedByMe = Ticket.Status == "EM_ANALISE" && Ticket.Analista == email;
            IsLockedByOther = Ticket.Status == "EM_ANALISE" && Ticket.Analista != email;

            Historico = await _pendenciaService.GetHistoricoAsync(Tipo, Id);

            // Fetch specific entity data ignoring Global Query Filters since Analyst needs to see it
            if (Tipo == "VEICULO")
                Veiculo = await _context.Veiculos.IgnoreQueryFilters().Include(v => v.Empresa).FirstOrDefaultAsync(v => v.Placa == Id);
            else if (Tipo == "MOTORISTA" && int.TryParse(Id, out int mId))
                Motorista = await _context.Motoristas.IgnoreQueryFilters().Include(m => m.Empresa).FirstOrDefaultAsync(m => m.Id == mId);
            else if (Tipo == "EMPRESA")
                Empresa = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == Id);

            return Page();
        }

        public async Task<IActionResult> OnPostIniciarAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (email != null)
            {
                await _pendenciaService.IniciarAnaliseAsync(Tipo, Id, email);
            }
            return RedirectToPage(new { tipo = Tipo, id = Id });
        }

        public async Task<IActionResult> OnPostAprovarAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (email != null)
            {
                await _pendenciaService.AprovarAsync(Tipo, Id, email);
            }
            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnPostRejeitarAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (email != null && !string.IsNullOrWhiteSpace(MotivoRejeicao))
            {
                await _pendenciaService.RejeitarAsync(Tipo, Id, email, MotivoRejeicao);
            }
            return RedirectToPage("./Index");
        }
    }
}
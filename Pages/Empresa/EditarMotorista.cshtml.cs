using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;
using Eva.Workflow;
using System.Security.Claims;
using System.Text.Json;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA")]
    public class EditarMotoristaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly ArquivoService _arquivoService;

        public EditarMotoristaModel(EvaDbContext context, PendenciaService pendenciaService, ArquivoService arquivoService)
        {
            _context = context; _pendenciaService = pendenciaService; _arquivoService = arquivoService;
        }

        [BindProperty] public Motorista Motorista { get; set; } = null!;
        [BindProperty] public IFormFile? UploadArquivo { get; set; }

        public string? PendenciaStatus { get; set; }
        public List<Documento> Cnhs { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "MOTORISTA" && p.EntidadeId == id.ToString());

            // Checking DadosPropostos to load the draft if it exists
            if (ticket != null && (ticket.Status == WorkflowValidator.AguardandoAnalise || ticket.Status == WorkflowValidator.EmAnalise) && !string.IsNullOrEmpty(ticket.DadosPropostos))
            {
                Motorista = JsonSerializer.Deserialize<Motorista>(ticket.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Motorista();
                Motorista.Id = id; // Ensure ID safety
            }
            else
            {
                var dbMotorista = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id);
                if (dbMotorista == null) return NotFound();
                Motorista = dbMotorista;
            }

            await LoadAuxiliaryData(id);
            return Page();
        }

        private async Task LoadAuxiliaryData(int id)
        {
            PendenciaStatus = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());
            Cnhs = await _context.DocumentoMotoristas.Where(dm => dm.MotoristaId == id).Include(dm => dm.Documento).Select(dm => dm.Documento).Where(d => d.DocumentoTipoNome == "CNH").OrderByDescending(d => d.DataUpload).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { await LoadAuxiliaryData(Motorista.Id); return Page(); }

            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", Motorista.Id.ToString());
            if (status == WorkflowValidator.EmAnalise)
            {
                ModelState.AddModelError("", "Este registro está em análise e não pode ser alterado no momento.");
                await LoadAuxiliaryData(Motorista.Id);
                return Page();
            }

            // Serialize the form and save it to the ticket as DadosPropostos
            var dadosPropostos = JsonSerializer.Serialize(Motorista);
            await _pendenciaService.SalvarDadosPropostosAsync("MOTORISTA", Motorista.Id.ToString(), dadosPropostos);

            return RedirectToPage("./MeusMotoristas");
        }

        public async Task<IActionResult> OnPostUploadAsync([FromRoute] int id)
        {
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());
            if (status == WorkflowValidator.EmAnalise) return RedirectToPage(new { id = id });

            if (UploadArquivo != null && UploadArquivo.Length > 0)
            {
                var existingId = await _context.DocumentoMotoristas.Where(dm => dm.MotoristaId == id && dm.Documento.DocumentoTipoNome == "CNH").Select(dm => dm.Documento.Id).FirstOrDefaultAsync();
                if (existingId > 0) await _arquivoService.DeletarDocumentoAsync(existingId, "MOTORISTA", id.ToString());
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, "CNH", "MOTORISTA", id.ToString());
            }
            return RedirectToPage(new { id = id });
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int docId, [FromRoute] int id)
        {
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());
            if (status == WorkflowValidator.EmAnalise) return RedirectToPage(new { id = id });

            await _arquivoService.DeletarDocumentoAsync(docId, "MOTORISTA", id.ToString());
            return RedirectToPage(new { id = id });
        }
    }
}
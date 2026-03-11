using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using Eva.Workflow;
using System.Security.Claims;
using System.Text.Json;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class EditarMotoristaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly ArquivoService _arquivoService;

        public EditarMotoristaModel(EvaDbContext context, PendenciaService pendenciaService, ArquivoService arquivoService)
        {
            _context = context; _pendenciaService = pendenciaService; _arquivoService = arquivoService;
        }

        [BindProperty] public MotoristaVM Input { get; set; } = new();
        [BindProperty] public IFormFile? UploadArquivo { get; set; }

        public string? PendenciaStatus { get; set; }
        public string? RejeicaoMotivo { get; set; }
        public List<Documento> Cnhs { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "MOTORISTA" && p.EntidadeId == id.ToString());

            if (ticket != null && (ticket.Status == WorkflowValidator.AguardandoAnalise || ticket.Status == WorkflowValidator.EmAnalise || ticket.Status == WorkflowValidator.Rejeitado) && !string.IsNullOrEmpty(ticket.DadosPropostos))
            {
                Input = JsonSerializer.Deserialize<MotoristaVM>(ticket.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new MotoristaVM();
                Input.Id = id;
            }
            else
            {
                var dbMotorista = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id);
                if (dbMotorista == null) return NotFound();

                Input = new MotoristaVM
                {
                    Id = dbMotorista.Id,
                    Nome = dbMotorista.Nome,
                    Cpf = dbMotorista.Cpf,
                    Cnh = dbMotorista.Cnh,
                    Email = dbMotorista.Email
                };
            }

            await LoadAuxiliaryData(id);
            return Page();
        }

        private async Task LoadAuxiliaryData(int id)
        {
            var ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "MOTORISTA" && p.EntidadeId == id.ToString());
            PendenciaStatus = ticket?.Status;
            RejeicaoMotivo = ticket?.Motivo;

            Cnhs = await _context.DocumentoMotoristas.Where(dm => dm.MotoristaId == id).Include(dm => dm.Documento).Select(dm => dm.Documento).Where(d => d.DocumentoTipoNome == "CNH").OrderByDescending(d => d.DataUpload).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { await LoadAuxiliaryData(Input.Id); return Page(); }

            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", Input.Id.ToString());
            if (status == WorkflowValidator.EmAnalise)
            {
                ModelState.AddModelError("", "Este registro está em análise e não pode ser alterado no momento.");
                await LoadAuxiliaryData(Input.Id);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _pendenciaService.SalvarDadosPropostosAsync("MOTORISTA", Input.Id.ToString(), dadosPropostos);

            return RedirectToPage("./MeusMotoristas");
        }

        public async Task<IActionResult> OnPostUploadAsync([FromRoute] int id)
        {
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());

            if (status == WorkflowValidator.EmAnalise)
            {
                Input.Id = id;
                await LoadAuxiliaryData(id);
                return Partial("_MotoristaDocs", this);
            }

            if (UploadArquivo != null && UploadArquivo.Length > 0)
            {
                var existingId = await _context.DocumentoMotoristas.Where(dm => dm.MotoristaId == id && dm.Documento.DocumentoTipoNome == "CNH").Select(dm => dm.Documento.Id).FirstOrDefaultAsync();
                if (existingId > 0) await _arquivoService.DeletarDocumentoAsync(existingId, "MOTORISTA", id.ToString());
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, "CNH", "MOTORISTA", id.ToString());
            }

            // SAFETY LOCK 3: Model Refill
            Input.Id = id;
            await LoadAuxiliaryData(id);
            return Partial("_MotoristaDocs", this);
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int docId, [FromRoute] int id)
        {
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());

            if (status == WorkflowValidator.EmAnalise)
            {
                Input.Id = id;
                await LoadAuxiliaryData(id);
                return Partial("_MotoristaDocs", this);
            }

            await _arquivoService.DeletarDocumentoAsync(docId, "MOTORISTA", id.ToString());

            // SAFETY LOCK 3: Model Refill
            Input.Id = id;
            await LoadAuxiliaryData(id);
            return Partial("_MotoristaDocs", this);
        }
    }
}
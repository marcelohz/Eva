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
    [Authorize(Roles = "EMPRESA")]
    public class EditarVeiculoModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly ArquivoService _arquivoService;

        public EditarVeiculoModel(EvaDbContext context, PendenciaService pendenciaService, ArquivoService arquivoService)
        {
            _context = context; _pendenciaService = pendenciaService; _arquivoService = arquivoService;
        }

        [BindProperty] public VeiculoVM Input { get; set; } = new();
        [BindProperty] public IFormFile? UploadArquivo { get; set; }
        [BindProperty] public string? TipoDocumentoUpload { get; set; }

        public string? PendenciaStatus { get; set; }
        public string? RejeicaoMotivo { get; set; } // ADDED: To display analyst feedback

        public List<Documento> Crlvs { get; set; } = new();
        public List<Documento> Laudos { get; set; } = new();
        public List<Documento> Apolices { get; set; } = new();
        public List<Documento> Comprovantes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var userCnpj = User.FindFirstValue("EmpresaCnpj");
            if (string.IsNullOrEmpty(userCnpj)) return RedirectToPage("/Login");

            var normalizedId = id.ToUpper().Trim();

            var ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "VEICULO" && p.EntidadeId == normalizedId);

            // Checks for active drafts including REJEITADO
            if (ticket != null && (ticket.Status == WorkflowValidator.AguardandoAnalise || ticket.Status == WorkflowValidator.EmAnalise || ticket.Status == WorkflowValidator.Rejeitado) && !string.IsNullOrEmpty(ticket.DadosPropostos))
            {
                Input = JsonSerializer.Deserialize<VeiculoVM>(ticket.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new VeiculoVM();
                Input.Placa = normalizedId;
            }
            else
            {
                var v = await _context.Veiculos.FirstOrDefaultAsync(v => v.Placa.ToUpper() == normalizedId && v.EmpresaCnpj == userCnpj);
                if (v == null) return NotFound();

                Input = new VeiculoVM
                {
                    Placa = v.Placa,
                    Modelo = v.Modelo ?? "",
                    ChassiNumero = v.ChassiNumero,
                    Renavan = v.Renavan,
                    PotenciaMotor = v.PotenciaMotor,
                    VeiculoCombustivelNome = v.VeiculoCombustivelNome,
                    NumeroLugares = v.NumeroLugares,
                    AnoFabricacao = v.AnoFabricacao,
                    ModeloAno = v.ModeloAno
                };
            }

            await LoadAuxiliaryData(normalizedId);
            return Page();
        }

        private async Task LoadAuxiliaryData(string id)
        {
            var ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "VEICULO" && p.EntidadeId == id);
            PendenciaStatus = ticket?.Status;
            RejeicaoMotivo = ticket?.Motivo; // POPULATED: Fetched from the view

            var docs = await _context.DocumentoVeiculos.Where(dv => dv.VeiculoPlaca == id).Include(dv => dv.Documento).Select(dv => dv.Documento).ToListAsync();
            Crlvs = docs.Where(d => d.DocumentoTipoNome == "CRLV").OrderByDescending(d => d.DataUpload).ToList();
            Laudos = docs.Where(d => d.DocumentoTipoNome == "LAUDO_INSPECAO").OrderByDescending(d => d.DataUpload).ToList();
            Apolices = docs.Where(d => d.DocumentoTipoNome == "APOLICE_SEGURO").OrderByDescending(d => d.DataUpload).ToList();
            Comprovantes = docs.Where(d => d.DocumentoTipoNome == "COMPROVANTE_PAGAMENTO").OrderByDescending(d => d.DataUpload).ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { await LoadAuxiliaryData(Input.Placa); return Page(); }

            var status = await _pendenciaService.GetStatusAsync("VEICULO", Input.Placa);
            if (status == WorkflowValidator.EmAnalise)
            {
                ModelState.AddModelError("", "Este registro está em análise e não pode ser alterado no momento.");
                await LoadAuxiliaryData(Input.Placa);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _pendenciaService.SalvarDadosPropostosAsync("VEICULO", Input.Placa, dadosPropostos);

            return RedirectToPage("/Empresa/MeusVeiculos");
        }

        public async Task<IActionResult> OnPostUploadAsync([FromRoute] string id)
        {
            var status = await _pendenciaService.GetStatusAsync("VEICULO", id);
            if (status == WorkflowValidator.EmAnalise) return RedirectToPage(new { id = id });

            if (UploadArquivo != null && !string.IsNullOrEmpty(TipoDocumentoUpload))
            {
                var existingId = await _context.DocumentoVeiculos.Where(dv => dv.VeiculoPlaca == id && dv.Documento.DocumentoTipoNome == TipoDocumentoUpload).Select(dv => dv.Documento.Id).FirstOrDefaultAsync();
                if (existingId > 0) await _arquivoService.DeletarDocumentoAsync(existingId, "VEICULO", id);
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, TipoDocumentoUpload, "VEICULO", id);
            }
            return RedirectToPage(new { id = id });
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int docId, [FromRoute] string id)
        {
            var status = await _pendenciaService.GetStatusAsync("VEICULO", id);
            if (status == WorkflowValidator.EmAnalise) return RedirectToPage(new { id = id });

            await _arquivoService.DeletarDocumentoAsync(docId, "VEICULO", id);
            return RedirectToPage(new { id = id });
        }
    }
}
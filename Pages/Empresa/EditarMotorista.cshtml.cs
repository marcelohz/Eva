using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using Eva.Workflow;
using System.Text.Json;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class EditarMotoristaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly ArquivoService _arquivoService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmpresaEntityEditGuardService _editGuardService;

        public EditarMotoristaModel(
            EvaDbContext context,
            PendenciaService pendenciaService,
            ArquivoService arquivoService,
            ICurrentUserService currentUserService,
            IEmpresaEntityEditGuardService editGuardService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
            _arquivoService = arquivoService;
            _currentUserService = currentUserService;
            _editGuardService = editGuardService;
        }

        [BindProperty] public MotoristaVM Input { get; set; } = new();
        [BindProperty] public IFormFile? UploadArquivo { get; set; }

        public string? PendenciaStatus { get; set; }
        public string? RejeicaoMotivo { get; set; }
        public List<Documento> Cnhs { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (string.IsNullOrWhiteSpace(_currentUserService.GetCurrentEmpresaCnpj())) return RedirectToPage("/Login");

            var guard = await _editGuardService.CheckMotoristaAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            var ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "MOTORISTA" && p.EntidadeId == id.ToString());

            if (ticket != null && (ticket.Status == WorkflowValidator.Incompleto || ticket.Status == WorkflowValidator.AguardandoAnalise || ticket.Status == WorkflowValidator.EmAnalise || ticket.Status == WorkflowValidator.Rejeitado) && !string.IsNullOrEmpty(ticket.DadosPropostos))
            {
                Input = JsonSerializer.Deserialize<MotoristaVM>(ticket.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new MotoristaVM();
                Input.Id = id;
            }
            else
            {
                var dbMotorista = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id && m.EmpresaCnpj == guard.CurrentEmpresaCnpj);
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

            var guard = await _editGuardService.CheckMotoristaAsync(Input.Id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();
            if (guard.IsLocked)
            {
                ModelState.AddModelError("", EmpresaEntityEditGuardService.LockedMessage);
                await LoadAuxiliaryData(Input.Id);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _pendenciaService.SalvarDadosPropostosAsync("MOTORISTA", Input.Id.ToString(), dadosPropostos);

            return RedirectToPage("./MeusMotoristas");
        }

        public async Task<IActionResult> OnPostUploadAsync([FromRoute] int id)
        {
            var guard = await _editGuardService.CheckMotoristaAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            if (guard.IsLocked)
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

            Input.Id = id;
            await LoadAuxiliaryData(id);
            return Partial("_MotoristaDocs", this);
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int docId, [FromRoute] int id)
        {
            var guard = await _editGuardService.CheckMotoristaAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            if (guard.IsLocked)
            {
                Input.Id = id;
                await LoadAuxiliaryData(id);
                return Partial("_MotoristaDocs", this);
            }

            await _arquivoService.DeletarDocumentoAsync(docId, "MOTORISTA", id.ToString());

            Input.Id = id;
            await LoadAuxiliaryData(id);
            return Partial("_MotoristaDocs", this);
        }
    }
}

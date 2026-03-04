using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;
using Eva.Workflow;
using System.Security.Claims;

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
            var dbMotorista = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id);
            if (dbMotorista == null) return NotFound();
            Motorista = dbMotorista;
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
            if (status == WorkflowValidator.EmAnalise) return Page();

            var mInDb = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == Motorista.Id);
            if (mInDb == null) return NotFound();
            mInDb.Nome = Motorista.Nome; mInDb.Cpf = Motorista.Cpf; mInDb.Cnh = Motorista.Cnh; mInDb.Email = Motorista.Email;

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
                await _pendenciaService.AvancarEntidadeAsync("MOTORISTA", mInDb.Id.ToString()); // RESTORED
            }
            return RedirectToPage("./MeusMotoristas");
        }

        public async Task<IActionResult> OnPostUploadAsync([FromRoute] int id)
        {
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
            await _arquivoService.DeletarDocumentoAsync(docId, "MOTORISTA", id.ToString());
            return RedirectToPage(new { id = id });
        }
    }
}
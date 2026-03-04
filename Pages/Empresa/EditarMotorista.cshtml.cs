using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;
using Eva.Workflow; // Assuming you created the Workflow folder/namespace
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
            _context = context;
            _pendenciaService = pendenciaService;
            _arquivoService = arquivoService;
        }

        [BindProperty]
        public Motorista Motorista { get; set; } = null!;

        [BindProperty]
        public IFormFile? UploadArquivo { get; set; }

        public string? PendenciaStatus { get; set; }
        public List<Documento> Cnhs { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            // FIX CS8601: Check for null before assignment
            var dbMotorista = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id);
            if (dbMotorista == null) return NotFound();

            Motorista = dbMotorista;

            await LoadAuxiliaryData(id);
            return Page();
        }

        private async Task LoadAuxiliaryData(int id)
        {
            PendenciaStatus = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());
            Cnhs = await _context.DocumentoMotoristas
                .Where(dm => dm.MotoristaId == id)
                .Include(dm => dm.Documento)
                .Select(dm => dm.Documento)
                .Where(d => d.DocumentoTipoNome == "CNH")
                .OrderByDescending(d => d.DataUpload)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // [PATCH]: Preserve typed input and only reload lists on error
            if (!ModelState.IsValid) { await LoadAuxiliaryData(Motorista.Id); return Page(); }

            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", Motorista.Id.ToString());
            if (status == "EM_ANALISE") return await ReloadWithLockError(Motorista.Id);

            var mInDb = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == Motorista.Id);
            if (mInDb == null) return NotFound();

            mInDb.Nome = Motorista.Nome;
            mInDb.Cpf = Motorista.Cpf;
            mInDb.Cnh = Motorista.Cnh;
            mInDb.Email = Motorista.Email;

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
                await _pendenciaService.AvancarEntidadeAsync("MOTORISTA", mInDb.Id.ToString());
            }

            return RedirectToPage("./MeusMotoristas");
        }

        // [PATCH]: Use [FromRoute] to ensure ID is captured from URL
        public async Task<IActionResult> OnPostUploadAsync([FromRoute] int id)
        {
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());
            if (status == "EM_ANALISE") return await ReloadWithLockError(id);

            if (UploadArquivo != null && UploadArquivo.Length > 0)
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, "CNH", "MOTORISTA", id.ToString());

            return RedirectToPage(new { id = id });
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int docId, [FromRoute] int id)
        {
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());
            if (status == "EM_ANALISE") return await ReloadWithLockError(id);

            await _arquivoService.DeletarDocumentoAsync(docId, "MOTORISTA", id.ToString());
            return RedirectToPage(new { id = id });
        }

        private async Task<IActionResult> ReloadWithLockError(int id)
        {
            ModelState.AddModelError(string.Empty, "Este motorista está em análise e não pode ser alterado.");
            await LoadAuxiliaryData(id);
            return Page();
        }
    }
}
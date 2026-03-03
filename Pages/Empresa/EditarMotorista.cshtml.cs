using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;
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
            // Fetch Driver safely
            Motorista = (await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id))!;

            if (Motorista == null) return NotFound();

            PendenciaStatus = await _pendenciaService.GetStatusAsync("MOTORISTA", Motorista.Id.ToString());

            // Fetch Documents (CNH)
            Cnhs = await _context.DocumentoMotoristas
                .Where(dm => dm.MotoristaId == id)
                .Include(dm => dm.Documento)
                .Select(dm => dm.Documento)
                .Where(d => d.DocumentoTipoNome == "CNH")
                .OrderByDescending(d => d.DataUpload)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return await ReloadPage(Motorista.Id);

            // Lock Check
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", Motorista.Id.ToString());
            if (status == "EM_ANALISE") return await ReloadPageWithLockError(Motorista.Id);

            var motoristaInDb = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == Motorista.Id);
            if (motoristaInDb == null) return NotFound();

            // Update Fields
            motoristaInDb.Nome = Motorista.Nome;
            motoristaInDb.Cpf = Motorista.Cpf;
            motoristaInDb.Cnh = Motorista.Cnh;
            motoristaInDb.Email = Motorista.Email;

            bool hasChanges = _context.ChangeTracker.HasChanges();

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
                await _pendenciaService.AvancarEntidadeAsync("MOTORISTA", motoristaInDb.Id.ToString());
            }

            return RedirectToPage("./MeusMotoristas");
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            // Lock Check
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", Motorista.Id.ToString());
            if (status == "EM_ANALISE") return await ReloadPageWithLockError(Motorista.Id);

            if (UploadArquivo != null && UploadArquivo.Length > 0)
            {
                // We use "CNH" as the standard type for drivers
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, "CNH", "MOTORISTA", Motorista.Id.ToString());
            }

            return RedirectToPage(new { id = Motorista.Id });
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int docId)
        {
            // Lock Check
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", Motorista.Id.ToString());
            if (status == "EM_ANALISE") return await ReloadPageWithLockError(Motorista.Id);

            // Security Check: Ensure doc belongs to this driver, and driver belongs to logged-in company
            // (Note: Global Query Filters handle the "Driver belongs to Company" part automatically via _context.Motoristas)

            var docLink = await _context.DocumentoMotoristas
                .FirstOrDefaultAsync(dm => dm.Id == docId && dm.MotoristaId == Motorista.Id);

            if (docLink != null)
            {
                var motoristaExists = await _context.Motoristas.AnyAsync(m => m.Id == Motorista.Id);
                if (motoristaExists)
                {
                    await _arquivoService.DeletarDocumentoAsync(docId, "MOTORISTA", Motorista.Id.ToString());
                }
            }

            return RedirectToPage(new { id = Motorista.Id });
        }

        private async Task<IActionResult> ReloadPage(int id)
        {
            await OnGetAsync(id);
            return Page();
        }

        private async Task<IActionResult> ReloadPageWithLockError(int id)
        {
            ModelState.AddModelError(string.Empty, "Este motorista está em análise e não pode ser alterado.");
            return await ReloadPage(id);
        }
    }
}
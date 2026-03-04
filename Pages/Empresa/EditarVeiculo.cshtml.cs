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
        public List<Documento> Crlvs { get; set; } = new();
        public List<Documento> Laudos { get; set; } = new();
        public List<Documento> Apolices { get; set; } = new();
        public List<Documento> Comprovantes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var userCnpj = User.FindFirstValue("EmpresaCnpj");
            // Normalize ID to prevent 404 on case mismatch
            var v = await _context.Veiculos.FirstOrDefaultAsync(v => v.Placa.ToUpper() == id.ToUpper().Trim() && v.EmpresaCnpj == userCnpj);
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
            await LoadAuxiliaryData(v.Placa);
            return Page();
        }

        private async Task LoadAuxiliaryData(string id)
        {
            PendenciaStatus = await _pendenciaService.GetStatusAsync("VEICULO", id);
            var docs = await _context.DocumentoVeiculos.Where(dv => dv.VeiculoPlaca == id).Include(dv => dv.Documento).Select(dv => dv.Documento).ToListAsync();
            Crlvs = docs.Where(d => d.DocumentoTipoNome == "CRLV").OrderByDescending(d => d.DataUpload).ToList();
            Laudos = docs.Where(d => d.DocumentoTipoNome == "LAUDO_INSPECAO").OrderByDescending(d => d.DataUpload).ToList();
            Apolices = docs.Where(d => d.DocumentoTipoNome == "APOLICE_SEGURO").OrderByDescending(d => d.DataUpload).ToList();
            Comprovantes = docs.Where(d => d.DocumentoTipoNome == "COMPROVANTE_PAGAMENTO").OrderByDescending(d => d.DataUpload).ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { await LoadAuxiliaryData(Input.Placa); return Page(); }
            var userCnpj = User.FindFirstValue("EmpresaCnpj");
            var vInDb = await _context.Veiculos.FirstOrDefaultAsync(v => v.Placa == Input.Placa && v.EmpresaCnpj == userCnpj);
            if (vInDb == null) return NotFound();

            vInDb.Modelo = Input.Modelo; vInDb.ChassiNumero = Input.ChassiNumero; vInDb.Renavan = Input.Renavan;
            vInDb.PotenciaMotor = Input.PotenciaMotor; vInDb.VeiculoCombustivelNome = Input.VeiculoCombustivelNome;
            vInDb.NumeroLugares = Input.NumeroLugares; vInDb.AnoFabricacao = Input.AnoFabricacao; vInDb.ModeloAno = Input.ModeloAno;

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
                await _pendenciaService.AvancarEntidadeAsync("VEICULO", vInDb.Placa); // RESTORED
            }
            return RedirectToPage("/Empresa/MeusVeiculos");
        }

        public async Task<IActionResult> OnPostUploadAsync([FromRoute] string id)
        {
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
            await _arquivoService.DeletarDocumentoAsync(docId, "VEICULO", id);
            return RedirectToPage(new { id = id });
        }
    }
}
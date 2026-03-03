using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
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
            _context = context;
            _pendenciaService = pendenciaService;
            _arquivoService = arquivoService;
        }

        [BindProperty]
        public VeiculoVM Input { get; set; } = new();

        [BindProperty]
        public IFormFile? UploadArquivo { get; set; }

        // FIXED: Changed to nullable string? so it doesn't block the main Save action
        [BindProperty]
        public string? TipoDocumentoUpload { get; set; }

        public string? PendenciaStatus { get; set; }

        // Document Lists
        public List<Documento> Crlvs { get; set; } = new();
        public List<Documento> Laudos { get; set; } = new();
        public List<Documento> Apolices { get; set; } = new();
        public List<Documento> Comprovantes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null) return RedirectToPage("/Login");

            // Fetch Vehicle
            var vehicleInDb = await _context.Veiculos
                .FirstOrDefaultAsync(v => v.Placa == id && v.EmpresaCnpj == user.EmpresaCnpj);

            if (vehicleInDb == null) return NotFound();

            Input = new VeiculoVM
            {
                Placa = vehicleInDb.Placa,
                Modelo = vehicleInDb.Modelo ?? "",
                ChassiNumero = vehicleInDb.ChassiNumero,
                Renavan = vehicleInDb.Renavan,
                PotenciaMotor = vehicleInDb.PotenciaMotor,
                VeiculoCombustivelNome = vehicleInDb.VeiculoCombustivelNome,
                NumeroLugares = vehicleInDb.NumeroLugares,
                AnoFabricacao = vehicleInDb.AnoFabricacao,
                ModeloAno = vehicleInDb.ModeloAno
            };

            PendenciaStatus = await _pendenciaService.GetStatusAsync("VEICULO", vehicleInDb.Placa);

            // Fetch All Documents for this Vehicle
            var allDocs = await _context.DocumentoVeiculos
                .Where(dv => dv.VeiculoPlaca == id)
                .Include(dv => dv.Documento)
                .Select(dv => dv.Documento)
                .ToListAsync();

            // Filter into buckets
            Crlvs = allDocs.Where(d => d.DocumentoTipoNome == "CRLV").OrderByDescending(d => d.DataUpload).ToList();
            Laudos = allDocs.Where(d => d.DocumentoTipoNome == "LAUDO_INSPECAO").OrderByDescending(d => d.DataUpload).ToList();
            Apolices = allDocs.Where(d => d.DocumentoTipoNome == "APOLICE_SEGURO").OrderByDescending(d => d.DataUpload).ToList();
            Comprovantes = allDocs.Where(d => d.DocumentoTipoNome == "COMPROVANTE_PAGAMENTO").OrderByDescending(d => d.DataUpload).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Now this will be VALID because TipoDocumentoUpload is optional
            if (!ModelState.IsValid) return await ReloadPage(Input.Placa);

            var status = await _pendenciaService.GetStatusAsync("VEICULO", Input.Placa);
            if (status == "EM_ANALISE") return await ReloadPageWithLockError(Input.Placa);

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null) return RedirectToPage("/Login");

            var vehicleInDb = await _context.Veiculos
                .FirstOrDefaultAsync(v => v.Placa == Input.Placa && v.EmpresaCnpj == user.EmpresaCnpj);

            if (vehicleInDb == null) return NotFound();

            // Update
            vehicleInDb.Modelo = Input.Modelo;
            vehicleInDb.ChassiNumero = Input.ChassiNumero;
            vehicleInDb.Renavan = Input.Renavan;
            vehicleInDb.PotenciaMotor = Input.PotenciaMotor;
            vehicleInDb.VeiculoCombustivelNome = Input.VeiculoCombustivelNome;
            vehicleInDb.NumeroLugares = Input.NumeroLugares;
            vehicleInDb.AnoFabricacao = Input.AnoFabricacao;
            vehicleInDb.ModeloAno = Input.ModeloAno;

            bool hasChanges = _context.ChangeTracker.HasChanges();

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
                await _pendenciaService.AvancarEntidadeAsync("VEICULO", vehicleInDb.Placa);
            }

            // Redirect back to the list
            return RedirectToPage("/Empresa/MeusVeiculos");
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            var status = await _pendenciaService.GetStatusAsync("VEICULO", Input.Placa);
            if (status == "EM_ANALISE") return await ReloadPageWithLockError(Input.Placa);

            if (UploadArquivo != null && UploadArquivo.Length > 0 && !string.IsNullOrEmpty(TipoDocumentoUpload))
            {
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, TipoDocumentoUpload, "VEICULO", Input.Placa);
            }

            return RedirectToPage(new { id = Input.Placa });
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int id)
        {
            var status = await _pendenciaService.GetStatusAsync("VEICULO", Input.Placa);
            if (status == "EM_ANALISE") return await ReloadPageWithLockError(Input.Placa);

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null) return RedirectToPage("/Login");

            var docLink = await _context.DocumentoVeiculos
                .Include(dv => dv.Documento)
                .FirstOrDefaultAsync(dv => dv.Id == id && dv.VeiculoPlaca == Input.Placa);

            if (docLink != null)
            {
                var vehicle = await _context.Veiculos.FirstOrDefaultAsync(v => v.Placa == Input.Placa && v.EmpresaCnpj == user.EmpresaCnpj);
                if (vehicle != null)
                {
                    await _arquivoService.DeletarDocumentoAsync(id, "VEICULO", Input.Placa);
                }
            }

            return RedirectToPage(new { id = Input.Placa });
        }

        private async Task<IActionResult> ReloadPage(string placa)
        {
            await OnGetAsync(placa);
            return Page();
        }

        private async Task<IActionResult> ReloadPageWithLockError(string placa)
        {
            ModelState.AddModelError(string.Empty, "Este veículo está em análise e não pode ser alterado.");
            return await ReloadPage(placa);
        }
    }
}
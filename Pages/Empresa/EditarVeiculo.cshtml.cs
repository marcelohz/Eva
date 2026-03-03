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

        public string? PendenciaStatus { get; set; }
        public List<Documento> Crlvs { get; set; } = new();

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

            // Fetch Documents (CRLV)
            Crlvs = await _context.DocumentoVeiculos
                .Where(dv => dv.VeiculoPlaca == id)
                .Include(dv => dv.Documento)
                .Select(dv => dv.Documento)
                .Where(d => d.DocumentoTipoNome == "CRLV")
                .OrderByDescending(d => d.DataUpload)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return await ReloadPage(Input.Placa);

            // Lock Check
            var status = await _pendenciaService.GetStatusAsync("VEICULO", Input.Placa);
            if (status == "EM_ANALISE") return await ReloadPageWithLockError(Input.Placa);

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (user == null) return RedirectToPage("/Login");

            var vehicleInDb = await _context.Veiculos
                .FirstOrDefaultAsync(v => v.Placa == Input.Placa && v.EmpresaCnpj == user.EmpresaCnpj);

            if (vehicleInDb == null) return NotFound();

            // Update Fields
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

            return RedirectToPage("./MeusVeiculos");
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            // Lock Check
            var status = await _pendenciaService.GetStatusAsync("VEICULO", Input.Placa);
            if (status == "EM_ANALISE") return await ReloadPageWithLockError(Input.Placa);

            if (UploadArquivo != null && UploadArquivo.Length > 0)
            {
                // We use "CRLV" as the standard type for vehicles
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, "CRLV", "VEICULO", Input.Placa);
            }

            // Redirect back to the same page (GET) to refresh the list
            return RedirectToPage(new { id = Input.Placa });
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int id)
        {
            // Lock Check
            var status = await _pendenciaService.GetStatusAsync("VEICULO", Input.Placa);
            if (status == "EM_ANALISE") return await ReloadPageWithLockError(Input.Placa);

            // Security: Ensure this doc actually belongs to a vehicle owned by this company
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            // Verify link via database
            var docLink = await _context.DocumentoVeiculos
                .Include(dv => dv.Documento)
                .FirstOrDefaultAsync(dv => dv.Id == id && dv.VeiculoPlaca == Input.Placa);

            if (docLink != null)
            {
                // Double check vehicle ownership
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
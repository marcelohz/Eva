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
    public class NovoVeiculoModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly ArquivoService _arquivoService;

        public NovoVeiculoModel(EvaDbContext context, PendenciaService pendenciaService, ArquivoService arquivoService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
            _arquivoService = arquivoService;
        }

        [BindProperty]
        public VeiculoVM Input { get; set; } = new();

        [BindProperty] public IFormFile? UploadCrlv { get; set; }
        [BindProperty] public IFormFile? UploadLaudo { get; set; }
        [BindProperty] public IFormFile? UploadApolice { get; set; }
        [BindProperty] public IFormFile? UploadComprovante { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj)) return RedirectToPage("/Login");

            bool placaExists = await _context.Veiculos.AnyAsync(v => v.Placa == Input.Placa);
            if (placaExists)
            {
                ModelState.AddModelError("Input.Placa", "Esta placa já está cadastrada no sistema.");
                return Page();
            }

            var veiculo = new Veiculo
            {
                EmpresaCnpj = user.EmpresaCnpj,
                Placa = Input.Placa.ToUpper(),
                Modelo = Input.Modelo,
                ChassiNumero = Input.ChassiNumero,
                Renavan = Input.Renavan,
                PotenciaMotor = Input.PotenciaMotor,
                VeiculoCombustivelNome = Input.VeiculoCombustivelNome,
                NumeroLugares = Input.NumeroLugares,
                AnoFabricacao = Input.AnoFabricacao,
                ModeloAno = Input.ModeloAno
                // EventualStatus removed! The status is managed via FluxoPendencias
            };

            _context.Veiculos.Add(veiculo);
            await _context.SaveChangesAsync();

            if (UploadCrlv != null) await _arquivoService.SalvarDocumentoAsync(UploadCrlv, "CRLV", "VEICULO", veiculo.Placa);
            if (UploadLaudo != null) await _arquivoService.SalvarDocumentoAsync(UploadLaudo, "LAUDO_INSPECAO", "VEICULO", veiculo.Placa);
            if (UploadApolice != null) await _arquivoService.SalvarDocumentoAsync(UploadApolice, "APOLICE_SEGURO", "VEICULO", veiculo.Placa);
            if (UploadComprovante != null) await _arquivoService.SalvarDocumentoAsync(UploadComprovante, "COMPROVANTE_PAGAMENTO", "VEICULO", veiculo.Placa);

            // This triggers the workflow and makes it appear in the Analyst's queue!
            await _pendenciaService.AvancarEntidadeAsync("VEICULO", veiculo.Placa);

            return RedirectToPage("./MeusVeiculos");
        }
    }
}
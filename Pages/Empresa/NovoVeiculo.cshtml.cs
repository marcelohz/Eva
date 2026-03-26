using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using System.Security.Claims;
using System.Text.Json;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class NovoVeiculoModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ISubmissaoService _submissaoService;
        private readonly ArquivoService _arquivoService;

        public NovoVeiculoModel(EvaDbContext context, ISubmissaoService submissaoService, ArquivoService arquivoService)
        {
            _context = context;
            _submissaoService = submissaoService;
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
            };

            _context.Veiculos.Add(veiculo);
            await _context.SaveChangesAsync();

            var dadosPropostos = JsonSerializer.Serialize(new VeiculoVM
            {
                Placa = veiculo.Placa,
                Modelo = veiculo.Modelo ?? string.Empty,
                ChassiNumero = veiculo.ChassiNumero,
                Renavan = veiculo.Renavan,
                PotenciaMotor = veiculo.PotenciaMotor,
                VeiculoCombustivelNome = veiculo.VeiculoCombustivelNome,
                NumeroLugares = veiculo.NumeroLugares,
                AnoFabricacao = veiculo.AnoFabricacao,
                ModeloAno = veiculo.ModeloAno
            });
            await _submissaoService.SalvarDadosPropostosAsync("VEICULO", veiculo.Placa, dadosPropostos, userEmail);

            if (UploadCrlv != null) await _arquivoService.SalvarDocumentoAsync(UploadCrlv, "CRLV", "VEICULO", veiculo.Placa);
            if (UploadLaudo != null) await _arquivoService.SalvarDocumentoAsync(UploadLaudo, "LAUDO_INSPECAO", "VEICULO", veiculo.Placa);
            if (UploadApolice != null) await _arquivoService.SalvarDocumentoAsync(UploadApolice, "APOLICE_SEGURO", "VEICULO", veiculo.Placa);
            if (UploadComprovante != null) await _arquivoService.SalvarDocumentoAsync(UploadComprovante, "COMPROVANTE_PAGAMENTO", "VEICULO", veiculo.Placa);

            return RedirectToPage("./EditarVeiculo", new { id = veiculo.Placa });
        }
    }
}

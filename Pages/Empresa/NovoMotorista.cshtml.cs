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
    public class NovoMotoristaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ISubmissaoService _submissaoService;
        private readonly ArquivoService _arquivoService;

        public NovoMotoristaModel(EvaDbContext context, ISubmissaoService submissaoService, ArquivoService arquivoService)
        {
            _context = context;
            _submissaoService = submissaoService;
            _arquivoService = arquivoService;
        }

        [BindProperty]
        public MotoristaVM Input { get; set; } = new();

        [BindProperty]
        public IFormFile? UploadCnh { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj)) return RedirectToPage("/Login");

            // Map the secure VM to the actual database model
            var motorista = new Motorista
            {
                EmpresaCnpj = user.EmpresaCnpj,
                Nome = Input.Nome,
                Cpf = Input.Cpf,
                Cnh = Input.Cnh,
                Email = Input.Email
            };

            _context.Motoristas.Add(motorista);
            await _context.SaveChangesAsync();

            var dadosPropostos = JsonSerializer.Serialize(new MotoristaVM
            {
                Id = motorista.Id,
                Nome = motorista.Nome,
                Cpf = motorista.Cpf,
                Cnh = motorista.Cnh,
                Email = motorista.Email
            });
            await _submissaoService.SalvarDadosPropostosAsync("MOTORISTA", motorista.Id.ToString(), dadosPropostos, userEmail);

            if (UploadCnh != null)
            {
                await _arquivoService.SalvarDocumentoAsync(UploadCnh, "CNH", "MOTORISTA", motorista.Id.ToString());
            }

            return RedirectToPage("./EditarMotorista", new { id = motorista.Id });
        }
    }
}

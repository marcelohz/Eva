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
    public class NovoMotoristaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly ArquivoService _arquivoService;

        public NovoMotoristaModel(EvaDbContext context, PendenciaService pendenciaService, ArquivoService arquivoService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
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
                // EventualStatus removed! The status is managed via FluxoPendencias
            };

            _context.Motoristas.Add(motorista);
            await _context.SaveChangesAsync();

            if (UploadCnh != null)
            {
                await _arquivoService.SalvarDocumentoAsync(UploadCnh, "CNH", "MOTORISTA", motorista.Id.ToString());
            }

            // This triggers the workflow and makes it appear in the Analyst's queue!
            await _pendenciaService.AvancarEntidadeAsync("MOTORISTA", motorista.Id.ToString());

            return RedirectToPage("./MeusMotoristas");
        }
    }
}
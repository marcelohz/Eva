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
        public Motorista Motorista { get; set; } = new();

        // New: Bind property for the file directly on the create form
        [BindProperty]
        public IFormFile? UploadCnh { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj)) return RedirectToPage("/Login");

            Motorista.EmpresaCnpj = user.EmpresaCnpj;

            // PostgreSQL will handle CreatedEm via DEFAULT now()

            _context.Motoristas.Add(Motorista);
            await _context.SaveChangesAsync();
            // ^-- CRITICAL: The driver is now in the DB, so we have an ID for the foreign key.

            // 2. Save Document (Sequential Logic)
            if (UploadCnh != null)
            {
                await _arquivoService.SalvarDocumentoAsync(UploadCnh, "CNH", "MOTORISTA", Motorista.Id.ToString());
            }

            // 3. Trigger Workflow
           // await _pendenciaService.AvancarEntidadeAsync("MOTORISTA", Motorista.Id.ToString());

            return RedirectToPage("./MeusMotoristas");
        }
    }
}
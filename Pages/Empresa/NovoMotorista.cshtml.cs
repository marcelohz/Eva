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

        public NovoMotoristaModel(EvaDbContext context, PendenciaService pendenciaService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
        }

        [BindProperty]
        public Motorista Motorista { get; set; } = new();

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj)) return RedirectToPage("/Login");

            Motorista.EmpresaCnpj = user.EmpresaCnpj;
            Motorista.DataCadastro = DateTime.UtcNow;

            _context.Motoristas.Add(Motorista);
            await _context.SaveChangesAsync();

            // Fire the workflow trigger!
            await _pendenciaService.AvancarEntidadeAsync("MOTORISTA", Motorista.Id.ToString());

            return RedirectToPage("./MeusMotoristas");
        }
    }
}
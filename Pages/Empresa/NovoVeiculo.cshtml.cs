using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.Security.Claims;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA")]
    public class NovoVeiculoModel : PageModel
    {
        private readonly EvaDbContext _context;

        public NovoVeiculoModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Veiculo Veiculo { get; set; } = new();

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                return RedirectToPage("/Login");
            }

            // Clean plate and assign company
            Veiculo.EmpresaCnpj = user.EmpresaCnpj;
            Veiculo.Placa = Veiculo.Placa.ToUpper().Replace("-", "").Trim();

            // Your schema uses DateOnly for data_inclusao_eventual
            Veiculo.DataInclusaoEventual = DateOnly.FromDateTime(DateTime.Now);

            _context.Veiculos.Add(Veiculo);
            await _context.SaveChangesAsync();

            return RedirectToPage("./MeusVeiculos");
        }
    }
}
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
    public class EditarVeiculoModel : PageModel
    {
        private readonly EvaDbContext _context;

        public EditarVeiculoModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Veiculo Veiculo { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null) return RedirectToPage("/Login");

            // We fetch by Placa (id) and ensure it belongs to this user's company
            Veiculo = await _context.Veiculos
                .FirstOrDefaultAsync(v => v.Placa == id && v.EmpresaCnpj == user.EmpresaCnpj);

            if (Veiculo == null) return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            // Check if vehicle exists and belongs to user
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            var vehicleInDb = await _context.Veiculos
                .FirstOrDefaultAsync(v => v.Placa == Veiculo.Placa && v.EmpresaCnpj == user.EmpresaCnpj);

            if (vehicleInDb == null) return NotFound();

            // Update only the fields we allow
            vehicleInDb.Modelo = Veiculo.Modelo;
            vehicleInDb.ChassiNumero = Veiculo.ChassiNumero;
            vehicleInDb.Renavan = Veiculo.Renavan;
            vehicleInDb.AnoFabricacao = Veiculo.AnoFabricacao;
            vehicleInDb.ModeloAno = Veiculo.ModeloAno;
            vehicleInDb.NumeroLugares = Veiculo.NumeroLugares;
            vehicleInDb.VeiculoCombustivelNome = Veiculo.VeiculoCombustivelNome;
            vehicleInDb.CorPrincipalNome = Veiculo.CorPrincipalNome;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VeiculoExists(Veiculo.Placa)) return NotFound();
                else throw;
            }

            return RedirectToPage("./MeusVeiculos");
        }

        private bool VeiculoExists(string placa)
        {
            return _context.Veiculos.Any(e => e.Placa == placa);
        }
    }
}
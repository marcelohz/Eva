using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
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
        public VeiculoVM Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null) return RedirectToPage("/Login");

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

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            // Added this safety check to satisfy the compiler and protect the app
            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj)) return RedirectToPage("/Login");

            var vehicleInDb = await _context.Veiculos
                .FirstOrDefaultAsync(v => v.Placa == Input.Placa && v.EmpresaCnpj == user.EmpresaCnpj);

            if (vehicleInDb == null) return NotFound();

            vehicleInDb.Modelo = Input.Modelo;
            vehicleInDb.ChassiNumero = Input.ChassiNumero;
            vehicleInDb.Renavan = Input.Renavan;
            vehicleInDb.PotenciaMotor = Input.PotenciaMotor;
            vehicleInDb.VeiculoCombustivelNome = Input.VeiculoCombustivelNome;
            vehicleInDb.NumeroLugares = Input.NumeroLugares;
            vehicleInDb.AnoFabricacao = Input.AnoFabricacao;
            vehicleInDb.ModeloAno = Input.ModeloAno;

            await _context.SaveChangesAsync();
            return RedirectToPage("./MeusVeiculos");
        }
    }
}
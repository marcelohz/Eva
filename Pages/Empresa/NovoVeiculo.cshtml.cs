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
    public class NovoVeiculoModel : PageModel
    {
        private readonly EvaDbContext _context;

        public NovoVeiculoModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public VeiculoVM Input { get; set; } = new();

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj)) return RedirectToPage("/Login");

            var novoVeiculo = new Veiculo
            {
                Placa = Input.Placa.ToUpper().Replace("-", "").Trim(),
                Modelo = Input.Modelo,
                ChassiNumero = Input.ChassiNumero,
                Renavan = Input.Renavan,
                PotenciaMotor = Input.PotenciaMotor,
                VeiculoCombustivelNome = Input.VeiculoCombustivelNome,
                NumeroLugares = Input.NumeroLugares,
                AnoFabricacao = Input.AnoFabricacao,
                ModeloAno = Input.ModeloAno,
                EmpresaCnpj = user.EmpresaCnpj,
                DataInclusaoEventual = DateOnly.FromDateTime(DateTime.Now)
            };

            _context.Veiculos.Add(novoVeiculo);
            await _context.SaveChangesAsync();

            return RedirectToPage("./MeusVeiculos");
        }
    }
}
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
    [Authorize(Roles = "EMPRESA,ANALISTA")]
    public class MeusVeiculosModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;

        public MeusVeiculosModel(EvaDbContext context, PendenciaService pendenciaService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
        }

        public List<Veiculo> Veiculos { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            Veiculos = await _context.Veiculos
                .OrderBy(v => v.Placa)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string placa)
        {
            if (string.IsNullOrEmpty(placa)) return RedirectToPage();

            // Safety lock against deleting while analyzing
            var status = await _pendenciaService.GetStatusAsync("VEICULO", placa);
            if (status == "EM_ANALISE") return RedirectToPage();

            var veiculoInDb = await _context.Veiculos.FirstOrDefaultAsync(v => v.Placa == placa);

            if (veiculoInDb != null)
            {
                _context.Veiculos.Remove(veiculoInDb);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
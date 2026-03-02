using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA,ANALISTA")]
    public class MeusMotoristasModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;

        public MeusMotoristasModel(EvaDbContext context, PendenciaService pendenciaService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
        }

        public List<Motorista> Motoristas { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            Motoristas = await _context.Motoristas
                .OrderBy(m => m.Nome)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            if (id <= 0) return RedirectToPage();

            // Safety lock against deleting while analyzing
            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", id.ToString());
            if (status == "EM_ANALISE") return RedirectToPage();

            var motoristaInDb = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id);

            if (motoristaInDb != null)
            {
                _context.Motoristas.Remove(motoristaInDb);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
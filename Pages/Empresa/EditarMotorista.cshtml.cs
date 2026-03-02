using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA")]
    public class EditarMotoristaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;

        public EditarMotoristaModel(EvaDbContext context, PendenciaService pendenciaService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
        }

        [BindProperty]
        public Motorista Motorista { get; set; } = null!;

        public string? PendenciaStatus { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Motorista = (await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id))!;

            if (Motorista == null) return NotFound();

            PendenciaStatus = await _pendenciaService.GetStatusAsync("MOTORISTA", Motorista.Id.ToString());

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var status = await _pendenciaService.GetStatusAsync("MOTORISTA", Motorista.Id.ToString());
            if (status == "EM_ANALISE")
            {
                PendenciaStatus = status;
                ModelState.AddModelError(string.Empty, "Este registro está atualmente em análise e não pode ser alterado no momento.");
                return Page();
            }

            var motoristaInDb = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == Motorista.Id);

            if (motoristaInDb == null) return NotFound();

            motoristaInDb.Nome = Motorista.Nome;
            motoristaInDb.Cpf = Motorista.Cpf;
            motoristaInDb.Cnh = Motorista.Cnh;
            motoristaInDb.Email = Motorista.Email;

            // THE DIRTY CHECK: Verify if EF Core detected any actual property modifications
            bool hasChanges = _context.ChangeTracker.HasChanges();

            try
            {
                if (hasChanges)
                {
                    await _context.SaveChangesAsync();
                    await _pendenciaService.AvancarEntidadeAsync("MOTORISTA", motoristaInDb.Id.ToString());
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MotoristaExists(Motorista.Id)) return NotFound();
                else throw;
            }

            return RedirectToPage("./MeusMotoristas");
        }

        private bool MotoristaExists(int id)
        {
            return _context.Motoristas.Any(e => e.Id == id);
        }
    }
}
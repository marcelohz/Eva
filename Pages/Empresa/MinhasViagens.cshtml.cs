using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class MinhasViagensModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly IEntityStatusService _statusService;
        private readonly IViagemManagementService _viagemManagementService;

        public MinhasViagensModel(
            EvaDbContext context,
            IEntityStatusService statusService,
            IViagemManagementService viagemManagementService)
        {
            _context = context;
            _statusService = statusService;
            _viagemManagementService = viagemManagementService;
        }

        public List<Viagem> Viagens { get; set; } = new();
        public bool PodeCriarNovaViagem { get; set; }
        public string? BloqueioNovaViagemMensagem { get; set; }
        public Dictionary<int, ViagemManagementAccessResult> ViagemAccess { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            // Segurança: Garante que a empresa só veja suas próprias viagens
            Viagens = await _context.Viagens
                .Where(v => v.EmpresaCnpj == user.EmpresaCnpj)
                .OrderByDescending(v => v.Id)
                .ToListAsync();

            ViagemAccess = Viagens.ToDictionary(v => v.Id, v => _viagemManagementService.GetAccess(v));

            var empresaHealth = await _statusService.GetHealthAsync("EMPRESA", user.EmpresaCnpj);
            PodeCriarNovaViagem = empresaHealth.IsLegal;
            if (!PodeCriarNovaViagem)
            {
                BloqueioNovaViagemMensagem = "Sua empresa precisa estar com o cadastro e a documentacao regularizados para cadastrar novas viagens.";
            }

            return Page();
        }
    }
}

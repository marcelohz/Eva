using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class PagamentoViagemModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IViagemRulesService _viagemRulesService;

        public PagamentoViagemModel(
            EvaDbContext context,
            ICurrentUserService currentUserService,
            IViagemRulesService viagemRulesService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _viagemRulesService = viagemRulesService;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public Viagem ViagemAtual { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _currentUserService.GetCurrentUserAsync();

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            var viagem = await _context.Viagens
                .Include(v => v.Veiculo)
                .FirstOrDefaultAsync(v => v.Id == Id && v.EmpresaCnpj == user.EmpresaCnpj);

            if (viagem == null)
                return NotFound("Viagem não encontrada ou acesso negado.");

            if (viagem.Pago)
            {
                TempData["MensagemAviso"] = "Esta viagem já se encontra paga e ativa.";
                return RedirectToPage("/Empresa/MinhasViagens");
            }

            var eligibility = await BuildEligibilityAsync(viagem);
            if (!eligibility.IsAllowed)
            {
                TempData["MensagemAviso"] = "Esta viagem possui pendências e não pode ser paga enquanto a empresa, o veículo ou os motoristas não estiverem regulares.";
                return RedirectToPage("/Empresa/EditarViagem", new { id = viagem.Id });
            }

            ViagemAtual = viagem;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _currentUserService.GetCurrentUserAsync();

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            var viagem = await _context.Viagens
                .FirstOrDefaultAsync(v => v.Id == Id && v.EmpresaCnpj == user.EmpresaCnpj);

            if (viagem == null)
                return NotFound();

            if (viagem.Pago)
                return RedirectToPage("/Empresa/MinhasViagens");

            var eligibility = await BuildEligibilityAsync(viagem);
            if (!eligibility.IsAllowed)
            {
                TempData["MensagemAviso"] = "Esta viagem possui pendências e não pode ser paga enquanto a empresa, o veículo ou os motoristas não estiverem regulares.";
                return RedirectToPage("/Empresa/EditarViagem", new { id = viagem.Id });
            }

            viagem.Pago = true;
            await _context.SaveChangesAsync();

            TempData["MensagemSucesso"] = $"Pagamento da viagem #{viagem.Id:D5} confirmado com sucesso! A viagem agora está ativa.";

            return RedirectToPage("/Empresa/MinhasViagens");
        }

        private async Task<ViagemEligibilityResult> BuildEligibilityAsync(Viagem viagem)
        {
            return await _viagemRulesService.ValidateEligibilityAsync(new ViagemEligibilityRequest
            {
                EmpresaCnpj = viagem.EmpresaCnpj,
                IdaEm = viagem.IdaEm,
                VoltaEm = viagem.VoltaEm,
                VeiculoPlaca = viagem.VeiculoPlaca,
                MotoristaId = viagem.MotoristaId,
                MotoristaAuxId = viagem.MotoristaAuxId,
                PassageiroCount = await _context.Passageiros.CountAsync(p => p.ViagemId == viagem.Id)
            });
        }
    }
}

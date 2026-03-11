using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class PagamentoViagemModel : PageModel
    {
        private readonly EvaDbContext _context;

        public PagamentoViagemModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public Viagem ViagemAtual { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            // Segurança: Busca a viagem exigindo que ela pertença ao CNPJ da empresa logada
            var viagem = await _context.Viagens
                .Include(v => v.Veiculo)
                .FirstOrDefaultAsync(v => v.Id == Id && v.EmpresaCnpj == user.EmpresaCnpj);

            if (viagem == null)
                return NotFound("Viagem não encontrada ou acesso negado.");

            if (viagem.Pago)
            {
                // Se já estiver paga, não faz sentido acessar o checkout. Redireciona para a lista.
                TempData["MensagemAviso"] = "Esta viagem já encontra-se paga e ativa.";
                return RedirectToPage("/Empresa/MinhasViagens");
            }

            ViagemAtual = viagem;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            var viagem = await _context.Viagens
                .FirstOrDefaultAsync(v => v.Id == Id && v.EmpresaCnpj == user.EmpresaCnpj);

            if (viagem == null)
                return NotFound();

            if (viagem.Pago)
                return RedirectToPage("/Empresa/MinhasViagens");

            // --- MOCK DE INTEGRAÇÃO DE PAGAMENTO ---
            // Aqui futuramente entrará a chamada para a API do Banco (Gateway de Pagamento / PIX).
            // Por enquanto, apenas consolidamos a transação simulada mudando o status no banco.

            viagem.Pago = true;
            await _context.SaveChangesAsync();

            TempData["MensagemSucesso"] = $"Pagamento da viagem #{viagem.Id:D5} confirmado com sucesso! A viagem agora está ativa.";

            return RedirectToPage("/Empresa/MinhasViagens");
        }
    }
}
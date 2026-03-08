using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.ComponentModel.DataAnnotations;

namespace Eva.Pages
{
    public class ConfirmarAcessoModel : PageModel
    {
        private readonly EvaDbContext _context;

        public ConfirmarAcessoModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ConfirmarSenhaVM Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = string.Empty;

        public class ConfirmarSenhaVM
        {
            [Required(ErrorMessage = "A senha é obrigatória.")]
            [MinLength(6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres.")]
            [DataType(DataType.Password)]
            public string Senha { get; set; } = string.Empty;

            [Required(ErrorMessage = "A confirmação de senha é obrigatória.")]
            [Compare("Senha", ErrorMessage = "As senhas não coincidem.")]
            [DataType(DataType.Password)]
            public string ConfirmarSenha { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

            // Check if token exists and is valid
            var tokenValidacao = await _context.TokensValidacaoEmail
                .Include(t => t.Usuario)
                .IgnoreQueryFilters() // Important: User is not logged in yet
                .FirstOrDefaultAsync(t => t.Token == token && t.ExpiraEm > DateTime.UtcNow);

            if (tokenValidacao == null)
            {
                TempData["ErrorMessage"] = "O link de confirmação é inválido ou expirou.";
                return RedirectToPage("/Login");
            }

            Token = token;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            // 1. Fetch token and user again to process
            var tokenValidacao = await _context.TokensValidacaoEmail
                .Include(t => t.Usuario)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Token == Token && t.ExpiraEm > DateTime.UtcNow);

            if (tokenValidacao == null || tokenValidacao.Usuario == null)
            {
                TempData["ErrorMessage"] = "Sessão de confirmação expirada. Inicie o processo novamente.";
                return RedirectToPage("/Login");
            }

            // 2. Update User
            var user = tokenValidacao.Usuario;
            user.Senha = Input.Senha; // Ideally hashed, but keeping to your current string storage
            user.EmailValidado = true;
            user.Ativo = true;

            // 3. Cleanup: Remove the token so it's a one-time use
            _context.TokensValidacaoEmail.Remove(tokenValidacao);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Sua conta foi ativada com sucesso! Você já pode realizar o login.";
            return RedirectToPage("/Login");
        }
    }
}
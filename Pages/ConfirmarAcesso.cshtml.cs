using Microsoft.AspNetCore.Identity; // Added
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

        [BindProperty] public ConfirmarSenhaVM Input { get; set; } = new();
        [BindProperty(SupportsGet = true)] public string Token { get; set; } = string.Empty;

        public class ConfirmarSenhaVM
        {
            [Required(ErrorMessage = "A senha é obrigatória.")]
            [MinLength(6, ErrorMessage = "Mínimo 6 caracteres.")]
            [DataType(DataType.Password)]
            public string Senha { get; set; } = string.Empty;

            [Required(ErrorMessage = "Confirme sua senha.")]
            [Compare("Senha", ErrorMessage = "As senhas não coincidem.")]
            [DataType(DataType.Password)]
            public string ConfirmarSenha { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

            var tokenValidacao = await _context.TokensValidacaoEmail
                .Include(t => t.Usuario)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Token == token && t.ExpiraEm > DateTime.UtcNow);

            if (tokenValidacao == null)
            {
                TempData["ErrorMessage"] = "Link inválido ou expirado.";
                return RedirectToPage("/Login");
            }

            Token = token;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var tokenValidacao = await _context.TokensValidacaoEmail
                .Include(t => t.Usuario)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Token == Token && t.ExpiraEm > DateTime.UtcNow);

            if (tokenValidacao == null || tokenValidacao.Usuario == null) return RedirectToPage("/Login");

            var user = tokenValidacao.Usuario;
            var hasher = new PasswordHasher<Usuario>();

            // Production: Securely hash the password
            user.Senha = hasher.HashPassword(user, Input.Senha);
            user.EmailValidado = true;
            user.Ativo = true;

            _context.TokensValidacaoEmail.Remove(tokenValidacao);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Conta ativada com sucesso!";
            return RedirectToPage("/Login");
        }
    }
}
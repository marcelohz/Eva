using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.ComponentModel.DataAnnotations;

namespace Eva.Pages.Metroplan.Admin
{
    [Authorize(Roles = "ADMIN")]
    public class NovoAnalistaModel : PageModel
    {
        private readonly EvaDbContext _context;

        public NovoAnalistaModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public NovoAnalistaInput Input { get; set; } = new();

        public class NovoAnalistaInput
        {
            [Required(ErrorMessage = "O Nome é obrigatório")]
            public string Nome { get; set; } = string.Empty;

            [Required(ErrorMessage = "O E-mail é obrigatório")]
            [EmailAddress(ErrorMessage = "Formato de e-mail inválido")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "A Senha é obrigatória")]
            [MinLength(6, ErrorMessage = "A senha deve ter pelo menos 6 caracteres")]
            [DataType(DataType.Password)]
            public string Senha { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userExists = await _context.Usuarios.AnyAsync(u => u.Email == Input.Email);
            if (userExists)
            {
                ModelState.AddModelError(string.Empty, "Este e-mail já está cadastrado no sistema.");
                return Page();
            }

            var novoUsuario = new Usuario
            {
                Nome = Input.Nome,
                Email = Input.Email,
                PapelNome = "ANALISTA",
                EmailValidado = true,
                CriadoEm = DateTime.UtcNow // CHANGED HERE
            };

            var hasher = new PasswordHasher<Usuario>();
            novoUsuario.Senha = hasher.HashPassword(novoUsuario, Input.Senha);

            _context.Usuarios.Add(novoUsuario);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
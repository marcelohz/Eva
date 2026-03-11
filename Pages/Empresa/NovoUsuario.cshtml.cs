using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class NovoUsuarioModel : PageModel
    {
        private readonly EvaDbContext _context;

        public NovoUsuarioModel(EvaDbContext context) { _context = context; }

        [BindProperty] public NovoUsuarioVM Input { get; set; } = new();

        public class NovoUsuarioVM
        {
            [Required(ErrorMessage = "O nome é obrigatório.")]
            public string Nome { get; set; } = string.Empty;

            [Required(ErrorMessage = "O e-mail é obrigatório.")]
            [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "O CPF é obrigatório.")]
            public string Cpf { get; set; } = string.Empty;

            [Required(ErrorMessage = "A senha é obrigatória.")]
            [MinLength(6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres.")]
            public string Senha { get; set; } = string.Empty;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var emailExists = await _context.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == Input.Email);
            if (emailExists)
            {
                ModelState.AddModelError("Input.Email", "Este e-mail já está em uso por outra conta no sistema.");
                return Page();
            }

            var cnpj = User.FindFirstValue("EmpresaCnpj");

            var novoUsuario = new Usuario
            {
                Nome = Input.Nome,
                Email = Input.Email,
                Cpf = Input.Cpf,
                EmpresaCnpj = cnpj,
                PapelNome = "USUARIO_EMPRESA",
                Ativo = true,
                EmailValidado = true,
                CriadoEm = DateTime.UtcNow
            };

            var hasher = new PasswordHasher<Usuario>();
            novoUsuario.Senha = hasher.HashPassword(novoUsuario, Input.Senha);

            _context.Usuarios.Add(novoUsuario);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Colaborador {novoUsuario.Nome} cadastrado com sucesso!";
            return RedirectToPage("./MeusUsuarios");
        }
    }
}
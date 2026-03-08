using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA")]
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

            // We use IgnoreQueryFilters() here to check the absolute entire database 
            // to ensure no one else in any other company is using this email.
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
                Senha = Input.Senha,
                EmpresaCnpj = cnpj,
                PapelNome = "USUARIO_EMPRESA", // Forces the Sub-User role
                Ativo = true,
                EmailValidado = true, // Auto-validate since it was created internally
                CriadoEm = DateTime.UtcNow
            };

            _context.Usuarios.Add(novoUsuario);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Colaborador {novoUsuario.Nome} cadastrado com sucesso!";
            return RedirectToPage("./MeusUsuarios");
        }
    }
}
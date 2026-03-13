using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Services;
using Hangfire;
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class NovoUsuarioModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly IBackgroundJobClient _backgroundJobs;

        public NovoUsuarioModel(EvaDbContext context, IBackgroundJobClient backgroundJobs)
        {
            _context = context;
            _backgroundJobs = backgroundJobs;
        }

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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var novoUsuario = new Usuario
                {
                    Nome = Input.Nome,
                    Email = Input.Email,
                    Cpf = Input.Cpf,
                    EmpresaCnpj = cnpj,
                    PapelNome = "USUARIO_EMPRESA",
                    Ativo = true,
                    EmailValidado = false, // Security: Must be validated by the user via the email link
                    CriadoEm = DateTime.UtcNow // Using UTC for PostgreSQL compliance
                };

                // Dummy initial password just to satisfy database model integrity before the user defines their own
                var hasher = new PasswordHasher<Usuario>();
                novoUsuario.Senha = hasher.HashPassword(novoUsuario, Guid.NewGuid().ToString());

                _context.Usuarios.Add(novoUsuario);
                await _context.SaveChangesAsync();

                // Generate secure cryptographic token
                string secureToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

                var validacao = new TokenValidacaoEmail
                {
                    UsuarioId = novoUsuario.Id,
                    Token = secureToken,
                    CriadoEm = DateTime.UtcNow,
                    ExpiraEm = DateTime.UtcNow.AddHours(48) // Token expires in 48 hours
                };

                _context.TokensValidacaoEmail.Add(validacao);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Create the callback URL using the secureToken
                var callbackUrl = Url.Page(
                    "/ConfirmarAcesso",
                    pageHandler: null,
                    values: new { token = secureToken },
                    protocol: Request.Scheme);

                // Enqueue the welcome email with the secure activation link
                _backgroundJobs.Enqueue<IEmailService>(x => x.SendEmailAsync(
                    Input.Email,
                    "Bem-vindo ao Sistema de Fretamento - Ative sua Conta",
                    $"<p>Olá <strong>{Input.Nome}</strong>,</p>" +
                    $"<p>Uma conta de operador foi criada para você acessar o sistema de Fretamento Eventual.</p>" +
                    $"<p>Para ativar sua conta e definir sua senha de acesso, clique no link abaixo:</p>" +
                    $"<p><a href='{callbackUrl}'>Definir Minha Senha e Ativar Conta</a></p>" +
                    $"<p><em>Este link é válido por 48 horas.</em></p>"
                ));

                TempData["SuccessMessage"] = $"Colaborador {novoUsuario.Nome} cadastrado com sucesso! Um e-mail com as instruções de acesso foi enviado.";
                return RedirectToPage("./MeusUsuarios");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Erro interno ao processar cadastro do colaborador. Tente novamente mais tarde.");
                return Page();
            }
        }
    }
}
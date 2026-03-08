using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using Hangfire;
using System.Security.Cryptography;

namespace Eva.Pages
{
    public class CadastroEmpresaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ITurnstileService _turnstile;

        public CadastroEmpresaModel(EvaDbContext context, ITurnstileService turnstile)
        {
            _context = context;
            _turnstile = turnstile;
        }

        [BindProperty]
        public CadastroEmpresaVM Input { get; set; } = new();

        public async Task<IActionResult> OnPostAsync()
        {
            // 1. Turnstile Bot Check
            // Explicitly convert to string? to satisfy the compiler
            string? turnstileResponse = Request.Form["cf-turnstile-response"].ToString();

            if (!await _turnstile.VerifyTokenAsync(turnstileResponse))
            {
                ModelState.AddModelError(string.Empty, "Falha na verificação de segurança. Por favor, tente novamente.");
                return Page();
            }

            if (!ModelState.IsValid) return Page();

            var cleanCnpj = Input.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "");

            var userExists = await _context.Usuarios
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email == Input.Email || u.EmpresaCnpj == cleanCnpj);

            if (userExists)
            {
                ModelState.AddModelError(string.Empty, "Este CNPJ ou E-mail já possui um cadastro ativo ou pendente no sistema.");
                return Page();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var empresaGeral = await _context.Empresas
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Cnpj == cleanCnpj);

                if (empresaGeral != null)
                {
                    if (empresaGeral.Email != Input.Email)
                    {
                        ModelState.AddModelError("Input.Email", "O e-mail informado não coincide com o e-mail cadastrado na Metroplan para este CNPJ.");
                        return Page();
                    }
                }
                else
                {
                    empresaGeral = new Eva.Models.Empresa
                    {
                        Cnpj = cleanCnpj,
                        Email = Input.Email,
                        NomeFantasia = "Nova Empresa",
                        Nome = "Empresa em Validação"
                    };

                    _context.Empresas.Add(empresaGeral);
                    await _context.SaveChangesAsync();
                }

                var novoUsuario = new Usuario
                {
                    Nome = empresaGeral.NomeFantasia ?? "Conta Principal",
                    Email = Input.Email,
                    EmpresaCnpj = cleanCnpj,
                    PapelNome = "EMPRESA",
                    Ativo = true,
                    EmailValidado = false,
                    Senha = Guid.NewGuid().ToString(),
                    CriadoEm = DateTime.UtcNow
                };

                _context.Usuarios.Add(novoUsuario);
                await _context.SaveChangesAsync();

                // Generate the token so it IS in the current context
                string secureToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

                var validacao = new TokenValidacaoEmail
                {
                    UsuarioId = novoUsuario.Id,
                    Token = secureToken,
                    CriadoEm = DateTime.UtcNow,
                    ExpiraEm = DateTime.UtcNow.AddHours(24)
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

                // ENQUEUE THE HANGFIRE JOB (Instead of Console Logging)
                BackgroundJob.Enqueue<IEmailService>(x => x.SendEmailAsync(
                    Input.Email,
                    "Ativação de Conta - Fretamento Eventual",
                    $"<h1>Bem-vindo ao Fretamento Eventual</h1><p>Clique no link abaixo para criar sua senha e ativar sua conta:</p><a href='{callbackUrl}'>Confirmar E-mail</a>"
                ));

                return RedirectToPage("/CadastroEmpresaSucesso", new { email = Input.Email });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Erro interno ao processar cadastro. Tente novamente mais tarde.");
                return Page();
            }
        }
    }
}
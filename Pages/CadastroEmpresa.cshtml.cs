using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using System.Security.Cryptography;

namespace Eva.Pages
{
    public class CadastroEmpresaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ILogger<CadastroEmpresaModel> _logger;

        public CadastroEmpresaModel(EvaDbContext context, ILogger<CadastroEmpresaModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public CadastroEmpresaVM Input { get; set; } = new();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var cleanCnpj = Input.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "");

            // Pre-check: Prevent double registration if user already started the process
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
                // 1. Check if Company exists in Geral
                var empresaGeral = await _context.Empresas
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(e => e.Cnpj == cleanCnpj);

                if (empresaGeral != null)
                {
                    // It exists: enforce email match to prevent hijacking
                    if (empresaGeral.Email != Input.Email)
                    {
                        ModelState.AddModelError("Input.Email", "O e-mail informado não coincide com o e-mail cadastrado na Metroplan para este CNPJ.");
                        return Page();
                    }
                }
                else
                {
                    // OPTION 1: The "Lazy Insert"
                    // It doesn't exist. We insert a skeleton record to satisfy the foreign key constraint.
                    empresaGeral = new Eva.Models.Empresa
                    {
                        Cnpj = cleanCnpj,
                        Email = Input.Email,
                        NomeFantasia = "Nova Empresa", // Placeholder until they update their profile
                        Nome = "Empresa em Validação"
                        // If your model has other strict [Required] fields, they would get dummy data here.
                    };

                    _context.Empresas.Add(empresaGeral);
                    await _context.SaveChangesAsync(); // Saves Empresa so the CNPJ exists for the next step
                }

                // 2. Create the 'Pending' User
                var novoUsuario = new Usuario
                {
                    Nome = empresaGeral.NomeFantasia ?? "Conta Principal",
                    Email = Input.Email,
                    EmpresaCnpj = cleanCnpj,
                    PapelNome = "EMPRESA",
                    Ativo = true,
                    EmailValidado = false, // Locked until the link is clicked
                    Senha = Guid.NewGuid().ToString(), // Temporary dummy password
                    CriadoEm = DateTime.UtcNow
                };

                _context.Usuarios.Add(novoUsuario);
                await _context.SaveChangesAsync();

                // 3. Generate and store the Validation Token
                string secureToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

                var validacao = new TokenValidacaoEmail
                {
                    UsuarioId = novoUsuario.Id,
                    Token = secureToken,
                    CriadoEm = DateTime.UtcNow,
                    ExpiraEm = DateTime.UtcNow.AddHours(24) // Token valid for 24h
                };

                _context.TokensValidacaoEmail.Add(validacao);
                await _context.SaveChangesAsync();

                // Commit everything (Empresa + Usuario + Token)
                await transaction.CommitAsync();

                // 4. THE MOCK DELIVERY: Print the link to the console
                var callbackUrl = Url.Page(
                    "/ConfirmarAcesso",
                    pageHandler: null,
                    values: new { token = secureToken },
                    protocol: Request.Scheme);

                _logger.LogCritical("==========================================================");
                _logger.LogCritical("MOCK EMAIL SENT TO: {Email}", Input.Email);
                _logger.LogCritical("VERIFICATION LINK: {Link}", callbackUrl);
                _logger.LogCritical("==========================================================");

                return RedirectToPage("/CadastroEmpresaSucesso", new { email = Input.Email });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erro ao processar pré-cadastro.");
                ModelState.AddModelError(string.Empty, "Erro interno ao processar cadastro. Tente novamente mais tarde.");
                return Page();
            }
        }
    }
}
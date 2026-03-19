using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Eva.Data;
using Eva.Models;

namespace Eva.Pages
{
    public class LoginModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ILogger<LoginModel> _logger;
        private readonly PasswordHasher<Usuario> _passwordHasher;

        public LoginModel(EvaDbContext context, ILogger<LoginModel> logger)
        {
            _context = context;
            _logger = logger;
            _passwordHasher = new PasswordHasher<Usuario>();
        }

        [BindProperty]
        public LoginVM Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class LoginVM
        {
            [Required(ErrorMessage = "O E-mail é obrigatório.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "A Senha é obrigatória.")]
            [DataType(DataType.Password)]
            public string Senha { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("ADMIN"))
                {
                    return RedirectToPage("/Metroplan/Admin/Index");
                }

                if (User.IsInRole("ANALISTA"))
                {
                    return RedirectToPage("/Metroplan/Analista/Index");
                }

                return RedirectToPage("/Empresa/MinhaEmpresa");
            }

            ReturnUrl = returnUrl;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var cleanEmail = Input.Email.ToLower().Trim();

            var user = await _context.Usuarios
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail);

            if (user == null || string.IsNullOrEmpty(user.Senha))
            {
                ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
                return Page();
            }

            // Secure Verification: Only allow hashed passwords
            var result = _passwordHasher.VerifyHashedPassword(user, user.Senha, Input.Senha);

            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
                return Page();
            }

            // Re-hash if the algorithm has been upgraded
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.Senha = _passwordHasher.HashPassword(user, Input.Senha);
                await _context.SaveChangesAsync();
            }

            bool isAdmin = user.PapelNome?.ToUpper() == "ADMIN";
            bool isAnalista = user.PapelNome?.ToUpper() == "ANALISTA";
            bool isInternalStaff = isAdmin || isAnalista;

            if (!user.EmailValidado && !isInternalStaff)
            {
                ModelState.AddModelError(string.Empty, "Esta conta ainda não foi confirmada. Verifique seu e-mail e clique no link de ativação.");
                return Page();
            }

            if (!user.Ativo)
            {
                ModelState.AddModelError(string.Empty, "Esta conta está inativa. Entre em contato com o suporte.");
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Nome ?? user.Email)
            };

            if (!string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                claims.Add(new Claim("EmpresaCnpj", user.EmpresaCnpj));
            }

            if (!string.IsNullOrEmpty(user.PapelNome))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.PapelNome.ToUpper()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
                });

            _logger.LogInformation("Usuário {Email} autenticado com sucesso.", user.Email);

            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl);
            }

            if (isAdmin)
            {
                return RedirectToPage("/Metroplan/Admin/Index");
            }

            if (isAnalista)
            {
                return RedirectToPage("/Metroplan/Analista/Index");
            }

            return RedirectToPage("/Empresa/MinhaEmpresa");
        }
    }
}
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

        public LoginModel(EvaDbContext context, ILogger<LoginModel> logger)
        {
            _context = context;
            _logger = logger;
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
                if (User.IsInRole("ANALISTA") || User.IsInRole("ADMIN"))
                {
                    // Fixed: Pointing to the correct Metroplan folder structure
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

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
                return Page();
            }

            // --- THE SMART PASSWORD VERIFIER ---
            bool isPasswordValid = false;

            // 1. Check if it matches plain text (for the new Empresas we just registered)
            if (user.Senha == Input.Senha)
            {
                isPasswordValid = true;
            }
            else
            {
                // 2. Check if it matches the ASP.NET Core Identity Hash (AQAAAA...)
                var hasher = new PasswordHasher<Usuario>();
                try
                {
                    var result = hasher.VerifyHashedPassword(user, user.Senha ?? "", Input.Senha);
                    if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        isPasswordValid = true;
                    }
                }
                catch
                {
                    // Failsafe in case the string in DB isn't a valid base64 hash at all
                }
            }

            if (!isPasswordValid)
            {
                ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
                return Page();
            }

            // Identify if the user is internal staff
            bool isInternalStaff = user.PapelNome?.ToUpper() == "ANALISTA" || user.PapelNome?.ToUpper() == "ADMIN";

            // Block unverified accounts (bypass for internal staff)
            if (!user.EmailValidado && !isInternalStaff)
            {
                ModelState.AddModelError(string.Empty, "Esta conta ainda não foi confirmada. Verifique seu e-mail e clique no link de ativação.");
                return Page();
            }

            // Block manually deactivated accounts
            if (!user.Ativo)
            {
                ModelState.AddModelError(string.Empty, "Esta conta está inativa. Entre em contato com o suporte.");
                return Page();
            }

            // --- BUILD THE USER SESSION ---
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Nome ?? user.Email)
            };

            // Only add the EmpresaCnpj claim if the user actually belongs to one
            if (!string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                claims.Add(new Claim("EmpresaCnpj", user.EmpresaCnpj));
            }

            // Assign standard role for Authorize attributes
            if (!string.IsNullOrEmpty(user.PapelNome))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.PapelNome.ToUpper()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // Log the user in
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

            if (isInternalStaff)
            {
                // Fixed: Pointing to the correct Metroplan folder structure
                return RedirectToPage("/Metroplan/Analista/Index");
            }

            return RedirectToPage("/Empresa/MinhaEmpresa");
        }
    }
}
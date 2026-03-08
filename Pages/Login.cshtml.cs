using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Eva.Data;

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
            [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "A Senha é obrigatória.")]
            [DataType(DataType.Password)]
            public string Senha { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            // If already logged in, redirect straight to the dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("ANALISTA") || User.IsInRole("ADMIN"))
                {
                    return RedirectToPage("/Analista/Dashboard"); // Or your specific internal dashboard path
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

            // IgnoreQueryFilters() allows us to search the entire user base
            // regardless of the session context (since the user isn't logged in yet).
            var user = await _context.Usuarios
                .IgnoreQueryFilters()
                .Include(u => u.Papel)
                .FirstOrDefaultAsync(u => u.Email == Input.Email);

            // Verify credentials
            if (user == null || user.Senha != Input.Senha)
            {
                ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
                return Page();
            }

            // Identify if the user is internal staff
            bool isInternalStaff = user.PapelNome == "ANALISTA" || user.PapelNome == "ADMIN";

            // Block unverified accounts (BUT bypass for internal staff since they are seeded directly)
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

            // FIX: Only add the EmpresaCnpj claim if the user actually belongs to one! (Analistas do not)
            if (!string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                claims.Add(new Claim("EmpresaCnpj", user.EmpresaCnpj));
            }

            // Assign standard role for Authorize attributes
            if (!string.IsNullOrEmpty(user.PapelNome))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.PapelNome));
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
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) // Standard 12-hour session
                });

            _logger.LogInformation("Usuário {Email} autenticado com sucesso.", user.Email);

            // Redirect logic
            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl);
            }

            // Route based on role
            if (isInternalStaff)
            {
                return RedirectToPage("/Analista/Dashboard"); // Adjust this path if your Analista home page is different
            }

            return RedirectToPage("/Empresa/MinhaEmpresa");
        }
    }
}
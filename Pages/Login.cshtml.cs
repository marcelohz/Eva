using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Eva.Models;

namespace Eva.Pages
{
    public class LoginModel : PageModel
    {
        private readonly EvaDbContext _context;

        public LoginModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public LoginInput Input { get; set; } = new();

        public class LoginInput
        {
            [Required(ErrorMessage = "O email é obrigatório")]
            [EmailAddress]
            public string Email { get; set; } = "";

            [Required(ErrorMessage = "A senha é obrigatória")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = "";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _context.Usuarios
                .Include(u => u.Papel)
                .FirstOrDefaultAsync(u => u.Email == Input.Email);

            var hasher = new PasswordHasher<Usuario>();

            if (user == null || string.IsNullOrEmpty(user.Senha))
            {
                ModelState.AddModelError(string.Empty, "Email ou senha inválidos.");
                return Page();
            }

            var result = hasher.VerifyHashedPassword(user, user.Senha, Input.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Email ou senha inválidos.");
                return Page();
            }

            if (!user.EmailValidado && user.PapelNome.ToUpper() == "EMPRESA")
            {
                return RedirectToPage("/CadastroEmpresa/Sucesso");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Nome),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("UserId", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.PapelNome)
            };

            if (!string.IsNullOrEmpty(user.EmpresaCnpj))
            {
                claims.Add(new Claim("EmpresaCnpj", user.EmpresaCnpj));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            var papel = user.PapelNome.ToUpper();
            if (papel == "EMPRESA") return RedirectToPage("/Empresa/MinhaEmpresa");
            if (papel == "ANALISTA") return RedirectToPage("/Metroplan/Analista/Index");
            if (papel == "ADMIN") return RedirectToPage("/Metroplan/Admin/Index");

            return RedirectToPage("/Index");
        }
    }
}
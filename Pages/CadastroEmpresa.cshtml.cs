using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;

namespace Eva.Pages
{
    public class CadastroEmpresaModel : PageModel
    {
        private readonly EvaDbContext _context;

        public CadastroEmpresaModel(EvaDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public CadastroEmpresaVM Input { get; set; } = new();

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var cleanCnpj = Input.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "");

            if (await _context.Empresas.AnyAsync(e => e.Cnpj == cleanCnpj))
            {
                ModelState.AddModelError("Input.Cnpj", "Este CNPJ já está cadastrado.");
                return Page();
            }

            if (await _context.Usuarios.AnyAsync(u => u.Email == Input.Email))
            {
                ModelState.AddModelError("Input.Email", "Este Email já está em uso.");
                return Page();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Create Empresa (geral.empresa)
                var empresa = new Eva.Models.Empresa
                {
                    Cnpj = cleanCnpj,
                    Nome = Input.RazaoSocial,
                    NomeFantasia = Input.NomeFantasia,
                    Email = Input.Email
                };
                _context.Empresas.Add(empresa);

                // 2. Create Usuario (web.usuario)
                var usuario = new Usuario
                {
                    Nome = Input.NomeResponsavel,
                    Email = Input.Email,
                    EmpresaCnpj = empresa.Cnpj,
                    PapelNome = "EMPRESA", // Matches uppercase DB constraint
                    EmailValidado = true
                };

                // 3. Hash Password using .NET native hasher
                var hasher = new PasswordHasher<Usuario>();
                usuario.Senha = hasher.HashPassword(usuario, Input.Senha);

                _context.Usuarios.Add(usuario);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToPage("/Login");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Erro ao salvar: " + ex.Message);
                return Page();
            }
        }
    }
}   
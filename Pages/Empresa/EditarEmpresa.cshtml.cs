using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models.ViewModels;
using Eva.Services;
using System.Security.Claims;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA")]
    public class EditarEmpresaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;

        public EditarEmpresaModel(EvaDbContext context, PendenciaService pendenciaService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
        }

        [BindProperty]
        public EmpresaVM Input { get; set; } = new();

        public string? PendenciaStatus { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userCnpj = User.FindFirstValue("EmpresaCnpj");
            if (string.IsNullOrEmpty(userCnpj)) return RedirectToPage("/Login");

            var empresaInDb = await _context.Empresas.FirstOrDefaultAsync(e => e.Cnpj == userCnpj);
            if (empresaInDb == null) return NotFound();

            Input = new EmpresaVM
            {
                Cnpj = empresaInDb.Cnpj,
                Nome = empresaInDb.Nome,
                NomeFantasia = empresaInDb.NomeFantasia,
                Endereco = empresaInDb.Endereco,
                EnderecoNumero = empresaInDb.EnderecoNumero,
                EnderecoComplemento = empresaInDb.EnderecoComplemento,
                Bairro = empresaInDb.Bairro,
                Cidade = empresaInDb.Cidade,
                Estado = empresaInDb.Estado,
                Cep = empresaInDb.Cep,
                Email = empresaInDb.Email,
                Telefone = empresaInDb.Telefone
            };

            PendenciaStatus = await _pendenciaService.GetStatusAsync("EMPRESA", empresaInDb.Cnpj);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var userCnpj = User.FindFirstValue("EmpresaCnpj");
            if (string.IsNullOrEmpty(userCnpj)) return RedirectToPage("/Login");

            // Workflow Safety Lock
            var status = await _pendenciaService.GetStatusAsync("EMPRESA", userCnpj);
            if (status == "EM_ANALISE")
            {
                PendenciaStatus = status;
                ModelState.AddModelError(string.Empty, "O perfil da sua empresa está em análise e não pode ser alterado no momento.");
                return Page();
            }

            var empresaInDb = await _context.Empresas.FirstOrDefaultAsync(e => e.Cnpj == userCnpj);
            if (empresaInDb == null) return NotFound();

            // Apply modifications
            empresaInDb.Nome = Input.Nome;
            empresaInDb.NomeFantasia = Input.NomeFantasia;
            empresaInDb.Endereco = Input.Endereco;
            empresaInDb.EnderecoNumero = Input.EnderecoNumero;
            empresaInDb.EnderecoComplemento = Input.EnderecoComplemento;
            empresaInDb.Bairro = Input.Bairro;
            empresaInDb.Cidade = Input.Cidade;
            empresaInDb.Estado = Input.Estado;
            empresaInDb.Cep = Input.Cep;
            empresaInDb.Email = Input.Email;
            empresaInDb.Telefone = Input.Telefone;

            // Dirty Checking
            bool hasChanges = _context.ChangeTracker.HasChanges();

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
                await _pendenciaService.AvancarEntidadeAsync("EMPRESA", empresaInDb.Cnpj);
            }

            return RedirectToPage("./MinhaEmpresa");
        }
    }
}
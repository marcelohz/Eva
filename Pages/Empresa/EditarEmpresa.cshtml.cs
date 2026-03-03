using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
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
        private readonly ArquivoService _arquivoService;

        public EditarEmpresaModel(EvaDbContext context, PendenciaService pendenciaService, ArquivoService arquivoService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
            _arquivoService = arquivoService;
        }

        [BindProperty]
        public EmpresaVM Input { get; set; } = new();

        [BindProperty]
        public IFormFile? UploadArquivo { get; set; }

        // FIXED: Made nullable to prevent ModelState invalidation during main save
        [BindProperty]
        public string? TipoDocumentoUpload { get; set; }

        public string? PendenciaStatus { get; set; }

        public List<Documento> Contratos { get; set; } = new();
        public List<Documento> IdentidadesSocios { get; set; } = new();
        public List<Documento> CartoesCnpj { get; set; } = new();
        public List<Documento> Alvaras { get; set; } = new();
        public List<Documento> Cnaes { get; set; } = new();

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

            var docs = await _context.DocumentoEmpresas
                .Where(de => de.EmpresaCnpj == userCnpj)
                .Include(de => de.Documento)
                .Select(de => de.Documento)
                .ToListAsync();

            Contratos = docs.Where(d => d.DocumentoTipoNome == "CONTRATO_SOCIAL").ToList();
            IdentidadesSocios = docs.Where(d => d.DocumentoTipoNome == "IDENTIDADE_SOCIO").ToList();
            CartoesCnpj = docs.Where(d => d.DocumentoTipoNome == "CARTAO_CNPJ").ToList();
            Alvaras = docs.Where(d => d.DocumentoTipoNome == "ALVARA").ToList();
            Cnaes = docs.Where(d => d.DocumentoTipoNome == "CNAE_FISCAL").ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // This will now pass because TipoDocumentoUpload is optional
            if (!ModelState.IsValid) return await ReloadPage();

            var userCnpj = User.FindFirstValue("EmpresaCnpj");
            if (string.IsNullOrEmpty(userCnpj)) return RedirectToPage("/Login");

            var status = await _pendenciaService.GetStatusAsync("EMPRESA", userCnpj);
            if (status == "EM_ANALISE") return await ReloadPageWithLockError();

            var empresaInDb = await _context.Empresas.FirstOrDefaultAsync(e => e.Cnpj == userCnpj);
            if (empresaInDb == null) return NotFound();

            // Update Fields
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

            bool hasChanges = _context.ChangeTracker.HasChanges();
            if (hasChanges)
            {
                await _context.SaveChangesAsync();
                await _pendenciaService.AvancarEntidadeAsync("EMPRESA", empresaInDb.Cnpj);
            }

            // FIXED: Redirect to Dashboard instead of reloading page
            return RedirectToPage("/Empresa/MinhaEmpresa");
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            var userCnpj = User.FindFirstValue("EmpresaCnpj");
            if (string.IsNullOrEmpty(userCnpj)) return RedirectToPage("/Login");

            var status = await _pendenciaService.GetStatusAsync("EMPRESA", userCnpj);
            if (status == "EM_ANALISE") return await ReloadPageWithLockError();

            if (UploadArquivo != null && UploadArquivo.Length > 0 && !string.IsNullOrEmpty(TipoDocumentoUpload))
            {
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, TipoDocumentoUpload, "EMPRESA", userCnpj);
            }

            // Uploads still reload the current page to show the file
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int id)
        {
            var userCnpj = User.FindFirstValue("EmpresaCnpj");
            if (string.IsNullOrEmpty(userCnpj)) return RedirectToPage("/Login");

            var status = await _pendenciaService.GetStatusAsync("EMPRESA", userCnpj);
            if (status == "EM_ANALISE") return await ReloadPageWithLockError();

            var link = await _context.DocumentoEmpresas
                .FirstOrDefaultAsync(de => de.Id == id && de.EmpresaCnpj == userCnpj);

            if (link != null)
            {
                await _arquivoService.DeletarDocumentoAsync(id, "EMPRESA", userCnpj);
            }

            return RedirectToPage();
        }

        private async Task<IActionResult> ReloadPage()
        {
            await OnGetAsync();
            return Page();
        }

        private async Task<IActionResult> ReloadPageWithLockError()
        {
            ModelState.AddModelError(string.Empty, "O cadastro da sua empresa está em análise e não pode ser alterado.");
            return await ReloadPage();
        }
    }
}
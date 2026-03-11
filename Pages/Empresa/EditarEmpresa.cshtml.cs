using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using Eva.Workflow;
using System.Security.Claims;
using System.Text.Json;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class EditarEmpresaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly ArquivoService _arquivoService;

        public EditarEmpresaModel(EvaDbContext context, PendenciaService pendenciaService, ArquivoService arquivoService)
        {
            _context = context; _pendenciaService = pendenciaService; _arquivoService = arquivoService;
        }

        [BindProperty] public EmpresaVM Input { get; set; } = new();
        [BindProperty] public IFormFile? UploadArquivo { get; set; }
        [BindProperty] public string? TipoDocumentoUpload { get; set; }

        public string? PendenciaStatus { get; set; }
        public string? RejeicaoMotivo { get; set; }

        public List<Documento> Contratos { get; set; } = new();
        public List<Documento> IdentidadesSocios { get; set; } = new();
        public List<Documento> CartoesCnpj { get; set; } = new();
        public List<Documento> Alvaras { get; set; } = new();
        public List<Documento> Cnaes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var cnpj = User.FindFirstValue("EmpresaCnpj");
            if (string.IsNullOrEmpty(cnpj)) return RedirectToPage("/Login");

            var ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == cnpj);

            if (ticket != null && (ticket.Status == WorkflowValidator.AguardandoAnalise || ticket.Status == WorkflowValidator.EmAnalise || ticket.Status == WorkflowValidator.Rejeitado) && !string.IsNullOrEmpty(ticket.DadosPropostos))
            {
                Input = JsonSerializer.Deserialize<EmpresaVM>(ticket.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new EmpresaVM();
                Input.Cnpj = cnpj;
            }
            else
            {
                var e = await _context.Empresas.FirstOrDefaultAsync(e => e.Cnpj == cnpj);
                if (e == null) return NotFound();

                Input = new EmpresaVM
                {
                    Cnpj = e.Cnpj,
                    Nome = e.Nome,
                    NomeFantasia = e.NomeFantasia,
                    Endereco = e.Endereco,
                    EnderecoNumero = e.EnderecoNumero,
                    EnderecoComplemento = e.EnderecoComplemento,
                    Bairro = e.Bairro,
                    Cidade = e.Cidade,
                    Estado = e.Estado,
                    Cep = e.Cep,
                    Email = e.Email,
                    Telefone = e.Telefone
                };
            }

            await LoadAuxiliaryData(cnpj);
            return Page();
        }

        private async Task LoadAuxiliaryData(string cnpj)
        {
            var ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == cnpj);
            PendenciaStatus = ticket?.Status;
            RejeicaoMotivo = ticket?.Motivo;

            var docs = await _context.DocumentoEmpresas.Where(de => de.EmpresaCnpj == cnpj).Include(de => de.Documento).Select(de => de.Documento).ToListAsync();
            Contratos = docs.Where(d => d.DocumentoTipoNome == "CONTRATO_SOCIAL").ToList();
            IdentidadesSocios = docs.Where(d => d.DocumentoTipoNome == "IDENTIDADE_SOCIO").ToList();
            CartoesCnpj = docs.Where(d => d.DocumentoTipoNome == "CARTAO_CNPJ").ToList();
            Alvaras = docs.Where(d => d.DocumentoTipoNome == "ALVARA").ToList();
            Cnaes = docs.Where(d => d.DocumentoTipoNome == "CNAE_FISCAL").ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { await LoadAuxiliaryData(Input.Cnpj); return Page(); }

            var status = await _pendenciaService.GetStatusAsync("EMPRESA", Input.Cnpj);
            if (status == WorkflowValidator.EmAnalise)
            {
                ModelState.AddModelError("", "Este registro está em análise e não pode ser alterado no momento.");
                await LoadAuxiliaryData(Input.Cnpj);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _pendenciaService.SalvarDadosPropostosAsync("EMPRESA", Input.Cnpj, dadosPropostos);

            return RedirectToPage("/Empresa/MinhaEmpresa");
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            var cnpj = User.FindFirstValue("EmpresaCnpj")!;
            var status = await _pendenciaService.GetStatusAsync("EMPRESA", cnpj);

            // If locked, return the partial immediately with no changes
            if (status == WorkflowValidator.EmAnalise)
            {
                Input.Cnpj = cnpj;
                await LoadAuxiliaryData(cnpj);
                return Partial("_EmpresaDocs", this);
            }

            if (UploadArquivo != null && !string.IsNullOrEmpty(TipoDocumentoUpload))
            {
                if (TipoDocumentoUpload != "IDENTIDADE_SOCIO")
                {
                    var existingId = await _context.DocumentoEmpresas.Where(de => de.EmpresaCnpj == cnpj && de.Documento.DocumentoTipoNome == TipoDocumentoUpload).Select(de => de.Documento.Id).FirstOrDefaultAsync();
                    if (existingId > 0) await _arquivoService.DeletarDocumentoAsync(existingId, "EMPRESA", cnpj);
                }
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, TipoDocumentoUpload, "EMPRESA", cnpj);
            }

            // SAFETY LOCK 3: Model Refill & Partial Return
            Input.Cnpj = cnpj;
            await LoadAuxiliaryData(cnpj);
            return Partial("_EmpresaDocs", this);
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int id)
        {
            var cnpj = User.FindFirstValue("EmpresaCnpj")!;
            var status = await _pendenciaService.GetStatusAsync("EMPRESA", cnpj);

            // If locked, return the partial immediately with no changes
            if (status == WorkflowValidator.EmAnalise)
            {
                Input.Cnpj = cnpj;
                await LoadAuxiliaryData(cnpj);
                return Partial("_EmpresaDocs", this);
            }

            await _arquivoService.DeletarDocumentoAsync(id, "EMPRESA", cnpj);

            // SAFETY LOCK 3: Model Refill & Partial Return
            Input.Cnpj = cnpj;
            await LoadAuxiliaryData(cnpj);
            return Partial("_EmpresaDocs", this);
        }
    }
}
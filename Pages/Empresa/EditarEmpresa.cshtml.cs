using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using Eva.Workflow;
using System.Text.Json;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class EditarEmpresaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;
        private readonly ArquivoService _arquivoService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmpresaEntityEditGuardService _editGuardService;

        public EditarEmpresaModel(
            EvaDbContext context,
            PendenciaService pendenciaService,
            ArquivoService arquivoService,
            ICurrentUserService currentUserService,
            IEmpresaEntityEditGuardService editGuardService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
            _arquivoService = arquivoService;
            _currentUserService = currentUserService;
            _editGuardService = editGuardService;
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
            var cnpj = _currentUserService.GetCurrentEmpresaCnpj();
            if (string.IsNullOrEmpty(cnpj)) return RedirectToPage("/Login");

            var ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == "EMPRESA" && p.EntidadeId == cnpj);

            if (ticket != null && (ticket.Status == WorkflowValidator.Incompleto || ticket.Status == WorkflowValidator.AguardandoAnalise || ticket.Status == WorkflowValidator.EmAnalise || ticket.Status == WorkflowValidator.Rejeitado) && !string.IsNullOrEmpty(ticket.DadosPropostos))
            {
                Input = JsonSerializer.Deserialize<EmpresaVM>(ticket.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new EmpresaVM();
                Input.Cnpj = cnpj;
            }
            else
            {
                var empresa = await _context.Empresas.FirstOrDefaultAsync(e => e.Cnpj == cnpj);
                if (empresa == null) return NotFound();

                Input = new EmpresaVM
                {
                    Cnpj = empresa.Cnpj,
                    Nome = empresa.Nome,
                    NomeFantasia = empresa.NomeFantasia,
                    Endereco = empresa.Endereco,
                    EnderecoNumero = empresa.EnderecoNumero,
                    EnderecoComplemento = empresa.EnderecoComplemento,
                    Bairro = empresa.Bairro,
                    Cidade = empresa.Cidade,
                    Estado = empresa.Estado,
                    Cep = empresa.Cep,
                    Email = empresa.Email,
                    Telefone = empresa.Telefone
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

            var guard = await _editGuardService.CheckEmpresaAsync(Input.Cnpj);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();
            if (guard.IsLocked)
            {
                ModelState.AddModelError("", EmpresaEntityEditGuardService.LockedMessage);
                await LoadAuxiliaryData(Input.Cnpj);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _pendenciaService.SalvarDadosPropostosAsync("EMPRESA", Input.Cnpj, dadosPropostos);

            return RedirectToPage("/Empresa/MinhaEmpresa");
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            var cnpj = _currentUserService.GetCurrentEmpresaCnpj();
            if (string.IsNullOrEmpty(cnpj)) return RedirectToPage("/Login");

            var guard = await _editGuardService.CheckEmpresaAsync(cnpj);
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            if (guard.IsLocked)
            {
                Input.Cnpj = cnpj;
                await LoadAuxiliaryData(cnpj);
                return Partial("_EmpresaDocs", this);
            }

            if (UploadArquivo != null && !string.IsNullOrEmpty(TipoDocumentoUpload))
            {
                if (TipoDocumentoUpload != "IDENTIDADE_SOCIO")
                {
                    var existingId = await _context.DocumentoEmpresas
                        .Where(de => de.EmpresaCnpj == cnpj && de.Documento.DocumentoTipoNome == TipoDocumentoUpload)
                        .Select(de => de.Documento.Id)
                        .FirstOrDefaultAsync();

                    if (existingId > 0) await _arquivoService.DeletarDocumentoAsync(existingId, "EMPRESA", cnpj);
                }

                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, TipoDocumentoUpload, "EMPRESA", cnpj);
            }

            Input.Cnpj = cnpj;
            await LoadAuxiliaryData(cnpj);
            return Partial("_EmpresaDocs", this);
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int id)
        {
            var cnpj = _currentUserService.GetCurrentEmpresaCnpj();
            if (string.IsNullOrEmpty(cnpj)) return RedirectToPage("/Login");

            var guard = await _editGuardService.CheckEmpresaAsync(cnpj);
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            if (guard.IsLocked)
            {
                Input.Cnpj = cnpj;
                await LoadAuxiliaryData(cnpj);
                return Partial("_EmpresaDocs", this);
            }

            await _arquivoService.DeletarDocumentoAsync(id, "EMPRESA", cnpj);

            Input.Cnpj = cnpj;
            await LoadAuxiliaryData(cnpj);
            return Partial("_EmpresaDocs", this);
        }
    }
}

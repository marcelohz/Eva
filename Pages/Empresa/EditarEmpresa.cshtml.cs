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
        private readonly ISubmissaoService _submissaoService;
        private readonly ArquivoService _arquivoService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmpresaEntityEditGuardService _editGuardService;

        public EditarEmpresaModel(
            EvaDbContext context,
            ISubmissaoService submissaoService,
            ArquivoService arquivoService,
            ICurrentUserService currentUserService,
            IEmpresaEntityEditGuardService editGuardService)
        {
            _context = context;
            _submissaoService = submissaoService;
            _arquivoService = arquivoService;
            _currentUserService = currentUserService;
            _editGuardService = editGuardService;
        }

        [BindProperty] public EmpresaVM Input { get; set; } = new();
        [BindProperty] public IFormFile? UploadArquivo { get; set; }
        [BindProperty] public string? TipoDocumentoUpload { get; set; }

        public string? PendenciaStatus { get; set; }
        public string? ObservacaoGeralRejeicao { get; set; }
        public string? MotivoRejeicaoDados { get; set; }
        public string? FeedbackSucesso { get; set; }

        public List<DocumentoEdicaoItemVm> Contratos { get; set; } = new();
        public List<DocumentoEdicaoItemVm> IdentidadesSocios { get; set; } = new();
        public List<DocumentoEdicaoItemVm> CartoesCnpj { get; set; } = new();
        public List<DocumentoEdicaoItemVm> Alvaras { get; set; } = new();
        public List<DocumentoEdicaoItemVm> Cnaes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var cnpj = (await _currentUserService.GetCurrentUserAsync())?.EmpresaCnpj;
            if (string.IsNullOrEmpty(cnpj)) return RedirectToPage("/Login");

            var draft = await _submissaoService.GetDraftAsync("EMPRESA", cnpj);

            if (draft != null)
            {
                Input = JsonSerializer.Deserialize<EmpresaVM>(draft.Dados.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new EmpresaVM();
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
            var snapshot = await _submissaoService.GetStatusSnapshotAsync("EMPRESA", cnpj);
            PendenciaStatus = snapshot.Status;
            ObservacaoGeralRejeicao = snapshot.ObservacaoGeralRejeicao;
            MotivoRejeicaoDados = snapshot.MotivoRejeicaoDados;

            var docs = await _submissaoService.GetDocumentosParaEdicaoAsync("EMPRESA", cnpj);

            Contratos = docs.Where(d => d.Documento.DocumentoTipoNome == "CONTRATO_SOCIAL").OrderByDescending(d => d.Documento.DataUpload).ThenByDescending(d => d.Documento.Id).ToList();
            IdentidadesSocios = docs.Where(d => d.Documento.DocumentoTipoNome == "IDENTIDADE_SOCIO").OrderByDescending(d => d.Documento.DataUpload).ThenByDescending(d => d.Documento.Id).ToList();
            CartoesCnpj = docs.Where(d => d.Documento.DocumentoTipoNome == "CARTAO_CNPJ").OrderByDescending(d => d.Documento.DataUpload).ThenByDescending(d => d.Documento.Id).ToList();
            Alvaras = docs.Where(d => d.Documento.DocumentoTipoNome == "ALVARA").OrderByDescending(d => d.Documento.DataUpload).ThenByDescending(d => d.Documento.Id).ToList();
            Cnaes = docs.Where(d => d.Documento.DocumentoTipoNome == "CNAE_FISCAL").OrderByDescending(d => d.Documento.DataUpload).ThenByDescending(d => d.Documento.Id).ToList();
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
            await _submissaoService.SalvarDadosPropostosAsync("EMPRESA", Input.Cnpj, dadosPropostos, _currentUserService.GetCurrentEmail());
            FeedbackSucesso = "AlteraÃ§Ãµes salvas em ediÃ§Ã£o. Quando terminar, clique em \"Enviar para anÃ¡lise\".";
            await LoadAuxiliaryData(Input.Cnpj);
            return Page();
        }

        public async Task<IActionResult> OnPostEnviarAsync()
        {
            var cnpj = (await _currentUserService.GetCurrentUserAsync())?.EmpresaCnpj;
            if (string.IsNullOrEmpty(cnpj)) return RedirectToPage("/Login");
            Input.Cnpj = cnpj;

            if (!ModelState.IsValid)
            {
                await LoadAuxiliaryData(cnpj);
                return Page();
            }

            var guard = await _editGuardService.CheckEmpresaAsync(cnpj);
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();
            if (guard.IsLocked)
            {
                await LoadAuxiliaryData(cnpj);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _submissaoService.SalvarDadosPropostosAsync("EMPRESA", cnpj, dadosPropostos, _currentUserService.GetCurrentEmail());

            var result = await _submissaoService.EnviarParaAnaliseAsync("EMPRESA", cnpj, _currentUserService.GetCurrentEmail());
            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                var draft = await _submissaoService.GetDraftAsync("EMPRESA", cnpj);
                if (draft != null)
                {
                    Input = JsonSerializer.Deserialize<EmpresaVM>(draft.Dados.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new EmpresaVM();
                    Input.Cnpj = cnpj;
                }

                await LoadAuxiliaryData(cnpj);
                return Page();
            }

            return RedirectToPage("/Empresa/MinhaEmpresa");
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            var cnpj = (await _currentUserService.GetCurrentUserAsync())?.EmpresaCnpj;
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
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, TipoDocumentoUpload, "EMPRESA", cnpj);
            }

            Input.Cnpj = cnpj;
            await LoadAuxiliaryData(cnpj);
            return Partial("_EmpresaDocs", this);
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int id)
        {
            var cnpj = (await _currentUserService.GetCurrentUserAsync())?.EmpresaCnpj;
            if (string.IsNullOrEmpty(cnpj)) return RedirectToPage("/Login");

            var guard = await _editGuardService.CheckEmpresaAsync(cnpj);
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            if (guard.IsLocked)
            {
                Input.Cnpj = cnpj;
                await LoadAuxiliaryData(cnpj);
                return Partial("_EmpresaDocs", this);
            }

            await _submissaoService.RemoverDocumentoDoDraftAsync("EMPRESA", cnpj, id, _currentUserService.GetCurrentEmail());

            Input.Cnpj = cnpj;
            await LoadAuxiliaryData(cnpj);
            return Partial("_EmpresaDocs", this);
        }
    }
}

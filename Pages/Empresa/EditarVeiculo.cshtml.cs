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
    public class EditarVeiculoModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ISubmissaoService _submissaoService;
        private readonly ArquivoService _arquivoService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmpresaEntityEditGuardService _editGuardService;
        private readonly IEntityStatusService _entityStatusService;

        public EditarVeiculoModel(
            EvaDbContext context,
            ISubmissaoService submissaoService,
            ArquivoService arquivoService,
            ICurrentUserService currentUserService,
            IEmpresaEntityEditGuardService editGuardService,
            IEntityStatusService entityStatusService)
        {
            _context = context;
            _submissaoService = submissaoService;
            _arquivoService = arquivoService;
            _currentUserService = currentUserService;
            _editGuardService = editGuardService;
            _entityStatusService = entityStatusService;
        }

        [BindProperty] public VeiculoVM Input { get; set; } = new();
        [BindProperty] public IFormFile? UploadArquivo { get; set; }
        [BindProperty] public string? TipoDocumentoUpload { get; set; }

        public string? PendenciaStatus { get; set; }
        public string OperationalStatus { get; set; } = WorkflowStatus.Incompleto;
        public string? ObservacaoGeralRejeicao { get; set; }
        public string? MotivoRejeicaoDados { get; set; }
        public string? FeedbackSucesso { get; set; }

        public List<DocumentoEdicaoItemVm> Crlvs { get; set; } = new();
        public List<DocumentoEdicaoItemVm> Laudos { get; set; } = new();
        public List<DocumentoEdicaoItemVm> Apolices { get; set; } = new();
        public List<DocumentoEdicaoItemVm> Comprovantes { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var normalizedId = id.ToUpper().Trim();
            var guard = await _editGuardService.CheckVeiculoAsync(normalizedId);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            var draft = await _submissaoService.GetDraftAsync("VEICULO", normalizedId);

            if (draft != null)
            {
                Input = JsonSerializer.Deserialize<VeiculoVM>(draft.Dados.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new VeiculoVM();
                Input.Placa = normalizedId;
            }
            else
            {
                var veiculo = await _context.Veiculos.FirstOrDefaultAsync(v => v.Placa.ToUpper() == normalizedId && v.EmpresaCnpj == guard.CurrentEmpresaCnpj);
                if (veiculo == null) return NotFound();

                Input = new VeiculoVM
                {
                    Placa = veiculo.Placa,
                    Modelo = veiculo.Modelo ?? "",
                    ChassiNumero = veiculo.ChassiNumero,
                    Renavan = veiculo.Renavan,
                    PotenciaMotor = veiculo.PotenciaMotor,
                    VeiculoCombustivelNome = veiculo.VeiculoCombustivelNome,
                    NumeroLugares = veiculo.NumeroLugares,
                    AnoFabricacao = veiculo.AnoFabricacao,
                    ModeloAno = veiculo.ModeloAno
                };
            }

            await LoadAuxiliaryData(normalizedId);
            return Page();
        }

        private async Task LoadAuxiliaryData(string id)
        {
            var health = await _entityStatusService.GetHealthAsync("VEICULO", id);
            OperationalStatus = health.OperationalStatus;

            var snapshot = await _submissaoService.GetStatusSnapshotAsync("VEICULO", id);
            PendenciaStatus = snapshot.Status;
            ObservacaoGeralRejeicao = snapshot.ObservacaoGeralRejeicao;
            MotivoRejeicaoDados = snapshot.MotivoRejeicaoDados;

            var docs = await _submissaoService.GetDocumentosParaEdicaoAsync("VEICULO", id);

            Crlvs = docs.Where(d => d.Documento.DocumentoTipoNome == "CRLV").OrderByDescending(d => d.Documento.DataUpload).ThenByDescending(d => d.Documento.Id).ToList();
            Laudos = docs.Where(d => d.Documento.DocumentoTipoNome == "LAUDO_INSPECAO").OrderByDescending(d => d.Documento.DataUpload).ThenByDescending(d => d.Documento.Id).ToList();
            Apolices = docs.Where(d => d.Documento.DocumentoTipoNome == "APOLICE_SEGURO").OrderByDescending(d => d.Documento.DataUpload).ThenByDescending(d => d.Documento.Id).ToList();
            Comprovantes = docs.Where(d => d.Documento.DocumentoTipoNome == "COMPROVANTE_PAGAMENTO").OrderByDescending(d => d.Documento.DataUpload).ThenByDescending(d => d.Documento.Id).ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { await LoadAuxiliaryData(Input.Placa); return Page(); }

            var guard = await _editGuardService.CheckVeiculoAsync(Input.Placa);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();
            if (guard.IsLocked)
            {
                ModelState.AddModelError("", EmpresaEntityEditGuardService.LockedMessage);
                await LoadAuxiliaryData(Input.Placa);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _submissaoService.SalvarDadosPropostosAsync("VEICULO", Input.Placa, dadosPropostos, _currentUserService.GetCurrentEmail());
            FeedbackSucesso = "Alterações salvas em edição. Quando terminar, clique em \"Enviar para análise\".";
            await LoadAuxiliaryData(Input.Placa);
            return Page();
        }

        public async Task<IActionResult> OnPostEnviarAsync([FromRoute] string id)
        {
            Input.Placa = id;
            if (!ModelState.IsValid)
            {
                await LoadAuxiliaryData(id);
                return Page();
            }

            var guard = await _editGuardService.CheckVeiculoAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();
            if (guard.IsLocked)
            {
                await LoadAuxiliaryData(id);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _submissaoService.SalvarDadosPropostosAsync("VEICULO", id, dadosPropostos, _currentUserService.GetCurrentEmail());

            var result = await _submissaoService.EnviarParaAnaliseAsync("VEICULO", id, _currentUserService.GetCurrentEmail());
            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                var draft = await _submissaoService.GetDraftAsync("VEICULO", id);
                if (draft != null)
                {
                    Input = JsonSerializer.Deserialize<VeiculoVM>(draft.Dados.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new VeiculoVM();
                    Input.Placa = id;
                }

                await LoadAuxiliaryData(id);
                return Page();
            }

            return RedirectToPage("/Empresa/MeusVeiculos");
        }

        public async Task<IActionResult> OnPostUploadAsync([FromRoute] string id)
        {
            var guard = await _editGuardService.CheckVeiculoAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();
            if (guard.IsLocked) return RedirectToPage(new { id });

            if (UploadArquivo != null && !string.IsNullOrEmpty(TipoDocumentoUpload))
            {
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, TipoDocumentoUpload, "VEICULO", id);
            }

            Input.Placa = id;
            await LoadAuxiliaryData(id);
            return Partial("_VeiculoDocs", this);
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int docId, [FromRoute] string id)
        {
            var guard = await _editGuardService.CheckVeiculoAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();
            if (guard.IsLocked) return RedirectToPage(new { id });

            await _submissaoService.RemoverDocumentoDoDraftAsync("VEICULO", id, docId, _currentUserService.GetCurrentEmail());

            Input.Placa = id;
            await LoadAuxiliaryData(id);
            return Partial("_VeiculoDocs", this);
        }
    }
}

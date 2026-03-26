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
    public class EditarMotoristaModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ISubmissaoService _submissaoService;
        private readonly ArquivoService _arquivoService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmpresaEntityEditGuardService _editGuardService;

        public EditarMotoristaModel(
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

        [BindProperty] public MotoristaVM Input { get; set; } = new();
        [BindProperty] public IFormFile? UploadArquivo { get; set; }

        public string? PendenciaStatus { get; set; }
        public string? ObservacaoGeralRejeicao { get; set; }
        public string? MotivoRejeicaoDados { get; set; }
        public string? FeedbackSucesso { get; set; }
        public List<DocumentoEdicaoItemVm> Cnhs { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var guard = await _editGuardService.CheckMotoristaAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            var draft = await _submissaoService.GetDraftAsync("MOTORISTA", id.ToString());

            if (draft != null)
            {
                Input = JsonSerializer.Deserialize<MotoristaVM>(draft.Dados.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new MotoristaVM();
                Input.Id = id;
            }
            else
            {
                var dbMotorista = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == id && m.EmpresaCnpj == guard.CurrentEmpresaCnpj);
                if (dbMotorista == null) return NotFound();

                Input = new MotoristaVM
                {
                    Id = dbMotorista.Id,
                    Nome = dbMotorista.Nome,
                    Cpf = dbMotorista.Cpf,
                    Cnh = dbMotorista.Cnh,
                    Email = dbMotorista.Email
                };
            }

            await LoadAuxiliaryData(id);
            return Page();
        }

        private async Task LoadAuxiliaryData(int id)
        {
            var idStr = id.ToString();
            var snapshot = await _submissaoService.GetStatusSnapshotAsync("MOTORISTA", idStr);
            PendenciaStatus = snapshot.Status;
            ObservacaoGeralRejeicao = snapshot.ObservacaoGeralRejeicao;
            MotivoRejeicaoDados = snapshot.MotivoRejeicaoDados;

            var docs = await _submissaoService.GetDocumentosParaEdicaoAsync("MOTORISTA", idStr);
            Cnhs = docs.Where(d => d.Documento.DocumentoTipoNome == "CNH")
                .OrderByDescending(d => d.Documento.DataUpload)
                .ThenByDescending(d => d.Documento.Id)
                .ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { await LoadAuxiliaryData(Input.Id); return Page(); }

            var guard = await _editGuardService.CheckMotoristaAsync(Input.Id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();
            if (guard.IsLocked)
            {
                ModelState.AddModelError("", EmpresaEntityEditGuardService.LockedMessage);
                await LoadAuxiliaryData(Input.Id);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _submissaoService.SalvarDadosPropostosAsync("MOTORISTA", Input.Id.ToString(), dadosPropostos, _currentUserService.GetCurrentEmail());
            FeedbackSucesso = "AlteraÃ§Ãµes salvas em ediÃ§Ã£o. Quando terminar, clique em \"Enviar para anÃ¡lise\".";
            await LoadAuxiliaryData(Input.Id);
            return Page();
        }

        public async Task<IActionResult> OnPostEnviarAsync([FromRoute] int id)
        {
            Input.Id = id;
            if (!ModelState.IsValid)
            {
                await LoadAuxiliaryData(id);
                return Page();
            }

            var guard = await _editGuardService.CheckMotoristaAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            if (guard.IsLocked)
            {
                Input.Id = id;
                await LoadAuxiliaryData(id);
                return Page();
            }

            var dadosPropostos = JsonSerializer.Serialize(Input);
            await _submissaoService.SalvarDadosPropostosAsync("MOTORISTA", id.ToString(), dadosPropostos, _currentUserService.GetCurrentEmail());

            var result = await _submissaoService.EnviarParaAnaliseAsync("MOTORISTA", id.ToString(), _currentUserService.GetCurrentEmail());
            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                var draft = await _submissaoService.GetDraftAsync("MOTORISTA", id.ToString());
                if (draft != null)
                {
                    Input = JsonSerializer.Deserialize<MotoristaVM>(draft.Dados.DadosPropostos, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new MotoristaVM();
                    Input.Id = id;
                }

                await LoadAuxiliaryData(id);
                return Page();
            }

            return RedirectToPage("./MeusMotoristas");
        }

        public async Task<IActionResult> OnPostUploadAsync([FromRoute] int id)
        {
            var guard = await _editGuardService.CheckMotoristaAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            if (guard.IsLocked)
            {
                Input.Id = id;
                await LoadAuxiliaryData(id);
                return Partial("_MotoristaDocs", this);
            }

            if (UploadArquivo != null && UploadArquivo.Length > 0)
            {
                await _arquivoService.SalvarDocumentoAsync(UploadArquivo, "CNH", "MOTORISTA", id.ToString());
            }

            Input.Id = id;
            await LoadAuxiliaryData(id);
            return Partial("_MotoristaDocs", this);
        }

        public async Task<IActionResult> OnPostDeleteDocAsync(int docId, [FromRoute] int id)
        {
            var guard = await _editGuardService.CheckMotoristaAsync(id);
            if (!guard.HasCurrentEmpresa) return RedirectToPage("/Login");
            if (!guard.ExistsAndBelongsToCurrentEmpresa) return NotFound();

            if (guard.IsLocked)
            {
                Input.Id = id;
                await LoadAuxiliaryData(id);
                return Partial("_MotoristaDocs", this);
            }

            await _submissaoService.RemoverDocumentoDoDraftAsync("MOTORISTA", id.ToString(), docId, _currentUserService.GetCurrentEmail());

            Input.Id = id;
            await LoadAuxiliaryData(id);
            return Partial("_MotoristaDocs", this);
        }
    }
}

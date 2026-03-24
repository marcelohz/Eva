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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eva.Pages.Metroplan.Analista
{
    [Authorize(Policy = "AcessoAnalista")]
    public class RevisaoModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAnalystReviewService _reviewService;
        private readonly PendenciaService _pendenciaService;

        public RevisaoModel(
            EvaDbContext context,
            ICurrentUserService currentUserService,
            IAnalystReviewService reviewService,
            PendenciaService pendenciaService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _reviewService = reviewService;
            _pendenciaService = pendenciaService;
        }

        [BindProperty(SupportsGet = true)] public string Tipo { get; set; } = string.Empty;
        [BindProperty(SupportsGet = true)] public string Id { get; set; } = string.Empty;
        [BindProperty] public string MotivoRejeicao { get; set; } = string.Empty;

        [BindProperty] public Dictionary<int, DateOnly?> Validades { get; set; } = new();

        public VPendenciaAtual? Ticket { get; set; }
        public List<FluxoPendencia> Historico { get; set; } = new();
        public bool IsLockedByMe { get; set; }
        public bool IsLockedByOther { get; set; }

        public Veiculo? Veiculo { get; set; }
        public Motorista? Motorista { get; set; }
        public Eva.Models.Empresa? Empresa { get; set; }

        public VeiculoVM? VeiculoDraft { get; set; }
        public EmpresaVM? EmpresaDraft { get; set; }
        public MotoristaVM? MotoristaDraft { get; set; }

        public List<Documento> Documentos { get; set; } = new();
        public List<DocumentoRevisaoVM> DocumentosRevisao { get; set; } = new();

        public class DocumentoRevisaoVM
        {
            public string TipoNome { get; set; } = string.Empty;
            public bool Obrigatorio { get; set; }
            public Documento? Documento { get; set; }
        }

        private async Task LoadDataAsync()
        {
            Ticket = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == Tipo && p.EntidadeId == Id);
            if (Ticket == null) return;

            var email = _currentUserService.GetCurrentEmail();
            IsLockedByMe = Ticket.Status == WorkflowStatus.EmAnalise && Ticket.Analista == email;
            IsLockedByOther = Ticket.Status == WorkflowStatus.EmAnalise && Ticket.Analista != email;
            Historico = await _pendenciaService.GetHistoricoAsync(Tipo, Id);

            var hasDraft = WorkflowStatus.IsPending(Ticket.Status) &&
                           !string.IsNullOrEmpty(Ticket.DadosPropostos);
            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (Tipo == "VEICULO")
            {
                Veiculo = await _context.Veiculos.IgnoreQueryFilters()
                    .Include(v => v.Empresa)
                    .FirstOrDefaultAsync(v => v.Placa == Id);

                Documentos = await _context.DocumentoVeiculos
                    .Where(dv => dv.VeiculoPlaca == Id && dv.Documento != null)
                    .Select(dv => dv.Documento!)
                    .OrderByDescending(d => d.DataUpload)
                    .ToListAsync();

                if (hasDraft)
                {
                    VeiculoDraft = JsonSerializer.Deserialize<VeiculoVM>(Ticket.DadosPropostos!, jsonOpts);
                }
            }
            else if (Tipo == "MOTORISTA" && int.TryParse(Id, out var motoristaId))
            {
                Motorista = await _context.Motoristas.IgnoreQueryFilters()
                    .Include(m => m.Empresa)
                    .FirstOrDefaultAsync(m => m.Id == motoristaId);

                Documentos = await _context.DocumentoMotoristas
                    .Where(dm => dm.MotoristaId == motoristaId && dm.Documento != null)
                    .Select(dm => dm.Documento!)
                    .OrderByDescending(d => d.DataUpload)
                    .ToListAsync();

                if (hasDraft)
                {
                    MotoristaDraft = JsonSerializer.Deserialize<MotoristaVM>(Ticket.DadosPropostos!, jsonOpts);
                }
            }
            else if (Tipo == "EMPRESA")
            {
                Empresa = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == Id);

                Documentos = await _context.DocumentoEmpresas
                    .Where(de => de.EmpresaCnpj == Id && de.Documento != null)
                    .Select(de => de.Documento!)
                    .OrderByDescending(d => d.DataUpload)
                    .ToListAsync();

                if (hasDraft)
                {
                    EmpresaDraft = JsonSerializer.Deserialize<EmpresaVM>(Ticket.DadosPropostos!, jsonOpts);
                }
            }

            var tipoSafe = Tipo.Trim().ToUpper();

            var regrasEsperadas = await _context.DocumentoTipoVinculos
                .Include(v => v.DocumentoTipo)
                .Where(v => v.EntidadeTipo.Trim().ToUpper() == tipoSafe)
                .ToListAsync();

            DocumentosRevisao = regrasEsperadas.Select(regra => new DocumentoRevisaoVM
            {
                TipoNome = regra.TipoNome,
                Obrigatorio = regra.DocumentoTipo?.Obrigatorio ?? false,
                Documento = Documentos.FirstOrDefault(d => d.DocumentoTipoNome == regra.TipoNome)
            }).OrderByDescending(d => d.Obrigatorio).ThenBy(d => d.TipoNome).ToList();

            var expectedTypeNames = regrasEsperadas.Select(r => r.TipoNome).ToList();
            var unexpectedDocs = Documentos.Where(d => !expectedTypeNames.Contains(d.DocumentoTipoNome));
            foreach (var documento in unexpectedDocs)
            {
                DocumentosRevisao.Add(new DocumentoRevisaoVM
                {
                    TipoNome = documento.DocumentoTipoNome,
                    Obrigatorio = false,
                    Documento = documento
                });
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Tipo) || string.IsNullOrEmpty(Id))
            {
                return RedirectToPage("./Index");
            }

            await LoadDataAsync();
            if (Ticket == null)
            {
                return RedirectToPage("./Index");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostIniciarAsync()
        {
            var email = _currentUserService.GetCurrentEmail();
            if (email == null)
            {
                return RedirectToPage("/Login");
            }

            var result = await _reviewService.IniciarAnaliseAsync(Tipo, Id, email);
            if (result.Success)
            {
                return RedirectToPage(new { tipo = Tipo, id = Id });
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "NÃ£o foi possÃ­vel iniciar a anÃ¡lise.");
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAprovarAsync()
        {
            var email = _currentUserService.GetCurrentEmail();
            if (email == null)
            {
                return RedirectToPage("/Login");
            }

            var result = await _reviewService.AprovarAsync(Tipo, Id, email, Validades);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.SuccessMessage;
                return RedirectToPage("./Index");
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "NÃ£o foi possÃ­vel aprovar o item.");
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostRejeitarAsync()
        {
            var email = _currentUserService.GetCurrentEmail();
            if (email == null)
            {
                return RedirectToPage("/Login");
            }

            var result = await _reviewService.RejeitarAsync(Tipo, Id, email, MotivoRejeicao);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.SuccessMessage;
                return RedirectToPage("./Index");
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "NÃ£o foi possÃ­vel rejeitar o item.");
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnGetVisualizarAsync(int docId)
        {
            var doc = await _context.Documentos.FindAsync(docId);
            if (doc == null)
            {
                return NotFound();
            }

            return File(doc.Conteudo, doc.ContentType);
        }
    }
}

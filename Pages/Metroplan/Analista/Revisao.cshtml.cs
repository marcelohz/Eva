using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using System;
using System.Security.Claims;
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
        private readonly PendenciaService _pendenciaService;
        private readonly IEntityStatusService _statusService;

        public RevisaoModel(EvaDbContext context, PendenciaService pendenciaService, IEntityStatusService statusService)
        {
            _context = context;
            _pendenciaService = pendenciaService;
            _statusService = statusService;
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

            var email = User.FindFirstValue(ClaimTypes.Email);
            IsLockedByMe = Ticket.Status == "EM_ANALISE" && Ticket.Analista == email;
            IsLockedByOther = Ticket.Status == "EM_ANALISE" && Ticket.Analista != email;
            Historico = await _pendenciaService.GetHistoricoAsync(Tipo, Id);

            bool hasDraft = (Ticket.Status == "AGUARDANDO_ANALISE" || Ticket.Status == "EM_ANALISE") && !string.IsNullOrEmpty(Ticket.DadosPropostos);
            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (Tipo == "VEICULO")
            {
                Veiculo = await _context.Veiculos.IgnoreQueryFilters().Include(v => v.Empresa).FirstOrDefaultAsync(v => v.Placa == Id);
                Documentos = await _context.DocumentoVeiculos.Where(dv => dv.VeiculoPlaca == Id && dv.Documento != null).Select(dv => dv.Documento!).OrderByDescending(d => d.DataUpload).ToListAsync();
                if (hasDraft) VeiculoDraft = JsonSerializer.Deserialize<VeiculoVM>(Ticket.DadosPropostos!, jsonOpts);
            }
            else if (Tipo == "MOTORISTA" && int.TryParse(Id, out int mId))
            {
                Motorista = await _context.Motoristas.IgnoreQueryFilters().Include(m => m.Empresa).FirstOrDefaultAsync(m => m.Id == mId);
                Documentos = await _context.DocumentoMotoristas.Where(dm => dm.MotoristaId == mId && dm.Documento != null).Select(dm => dm.Documento!).OrderByDescending(d => d.DataUpload).ToListAsync();
                if (hasDraft) MotoristaDraft = JsonSerializer.Deserialize<MotoristaVM>(Ticket.DadosPropostos!, jsonOpts);
            }
            else if (Tipo == "EMPRESA")
            {
                Empresa = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == Id);
                Documentos = await _context.DocumentoEmpresas.Where(de => de.EmpresaCnpj == Id && de.Documento != null).Select(de => de.Documento!).OrderByDescending(d => d.DataUpload).ToListAsync();
                if (hasDraft) EmpresaDraft = JsonSerializer.Deserialize<EmpresaVM>(Ticket.DadosPropostos!, jsonOpts);
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
            foreach (var un in unexpectedDocs)
            {
                DocumentosRevisao.Add(new DocumentoRevisaoVM { TipoNome = un.DocumentoTipoNome, Obrigatorio = false, Documento = un });
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Tipo) || string.IsNullOrEmpty(Id)) return RedirectToPage("./Index");
            await LoadDataAsync();
            if (Ticket == null) return RedirectToPage("./Index");
            return Page();
        }

        public async Task<IActionResult> OnPostIniciarAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (email == null) return RedirectToPage("/Login");

            try
            {
                await _pendenciaService.IniciarAnaliseAsync(Tipo, Id, email);
                return RedirectToPage(new { tipo = Tipo, id = Id });
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await LoadDataAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAprovarAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (email == null) return RedirectToPage("/Login");

            try
            {
                foreach (var kvp in Validades)
                {
                    if (kvp.Value.HasValue)
                    {
                        var doc = await _context.Documentos.FindAsync(kvp.Key);
                        if (doc != null) doc.Validade = kvp.Value.Value;
                    }
                }
                await _context.SaveChangesAsync();

                var health = await _statusService.GetHealthAsync(Tipo, Id);
                if (health.MissingMandatoryDocs.Any())
                {
                    ModelState.AddModelError(string.Empty, $"Faltam documentos obrigatórios: {string.Join(", ", health.MissingMandatoryDocs)}.");
                    await LoadDataAsync();
                    return Page();
                }

                await _pendenciaService.AprovarAsync(Tipo, Id, email);
                TempData["SuccessMessage"] = "Aprovação concluída com sucesso.";
                return RedirectToPage("./Index");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await LoadDataAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostRejeitarAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (email == null) return RedirectToPage("/Login");

            try
            {
                if (string.IsNullOrWhiteSpace(MotivoRejeicao))
                {
                    throw new ArgumentException("O motivo é obrigatório para rejeições.");
                }

                await _pendenciaService.RejeitarAsync(Tipo, Id, email, MotivoRejeicao);
                TempData["SuccessMessage"] = "Rejeição registrada com sucesso.";
                return RedirectToPage("./Index");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is UnauthorizedAccessException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await LoadDataAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnGetVisualizarAsync(int docId)
        {
            var doc = await _context.Documentos.FindAsync(docId);
            if (doc == null) return NotFound();
            return File(doc.Conteudo, doc.ContentType);
        }
    }
}
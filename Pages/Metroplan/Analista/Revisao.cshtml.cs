using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using System.Security.Claims;
using System.Text.Json;

namespace Eva.Pages.Metroplan.Analista
{
    [Authorize(Roles = "ANALISTA")]
    public class RevisaoModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly PendenciaService _pendenciaService;

        public RevisaoModel(EvaDbContext context, PendenciaService pendenciaService)
        {
            _context = context; _pendenciaService = pendenciaService;
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
        public Motorista? MotoristaDraft { get; set; }

        public List<Documento> Documentos { get; set; } = new();

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
                Documentos = await _context.DocumentoVeiculos.Where(dv => dv.VeiculoPlaca == Id).Include(dv => dv.Documento).Select(dv => dv.Documento).OrderByDescending(d => d.DataUpload).ToListAsync();
                if (hasDraft) VeiculoDraft = JsonSerializer.Deserialize<VeiculoVM>(Ticket.DadosPropostos!, jsonOpts);
            }
            else if (Tipo == "MOTORISTA" && int.TryParse(Id, out int mId))
            {
                Motorista = await _context.Motoristas.IgnoreQueryFilters().Include(m => m.Empresa).FirstOrDefaultAsync(m => m.Id == mId);
                Documentos = await _context.DocumentoMotoristas.Where(dm => dm.MotoristaId == mId).Include(dm => dm.Documento).Select(dm => dm.Documento).OrderByDescending(d => d.DataUpload).ToListAsync();
                if (hasDraft) MotoristaDraft = JsonSerializer.Deserialize<Motorista>(Ticket.DadosPropostos!, jsonOpts);
            }
            else if (Tipo == "EMPRESA")
            {
                Empresa = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == Id);
                Documentos = await _context.DocumentoEmpresas.Where(de => de.EmpresaCnpj == Id).Include(de => de.Documento).Select(de => de.Documento).OrderByDescending(d => d.DataUpload).ToListAsync();
                if (hasDraft) EmpresaDraft = JsonSerializer.Deserialize<EmpresaVM>(Ticket.DadosPropostos!, jsonOpts);
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
            if (email != null) await _pendenciaService.IniciarAnaliseAsync(Tipo, Id, email);
            return RedirectToPage(new { tipo = Tipo, id = Id });
        }

        public async Task<IActionResult> OnPostAprovarAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (email == null) return RedirectToPage("/Login");

            foreach (var kvp in Validades)
            {
                if (kvp.Value.HasValue)
                {
                    var doc = await _context.Documentos.FindAsync(kvp.Key);
                    if (doc != null) doc.Validade = kvp.Value.Value;
                }
            }
            await _context.SaveChangesAsync();
            await _pendenciaService.AprovarAsync(Tipo, Id, email);
            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnPostRejeitarAsync()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (email != null && !string.IsNullOrWhiteSpace(MotivoRejeicao)) await _pendenciaService.RejeitarAsync(Tipo, Id, email, MotivoRejeicao);
            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnGetVisualizarAsync(int docId)
        {
            var doc = await _context.Documentos.FindAsync(docId);
            if (doc == null) return NotFound();
            return File(doc.Conteudo, doc.ContentType);
        }
    }
}
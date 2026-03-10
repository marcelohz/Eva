using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Roles = "EMPRESA")]
    public class NovaViagemModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly IEntityStatusService _statusService;
        private readonly ArquivoService _arquivoService;

        public NovaViagemModel(EvaDbContext context, IEntityStatusService statusService, ArquivoService arquivoService)
        {
            _context = context;
            _statusService = statusService;
            _arquivoService = arquivoService;
        }

        [BindProperty]
        public NovaViagemVM Input { get; set; } = new();

        [BindProperty]
        [Required(ErrorMessage = "A Nota Fiscal é obrigatória.")]
        public IFormFile? UploadNotaFiscal { get; set; }

        public SelectList ViagemTipos { get; set; } = default!;
        public SelectList VeiculosValidos { get; set; } = default!;
        public SelectList MotoristasValidos { get; set; } = default!;
        public SelectList Regioes { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDropdownsAsync();

            Input.IdaEm = System.DateTime.Now.AddDays(1).Date.AddHours(8);
            Input.VoltaEm = System.DateTime.Now.AddDays(1).Date.AddHours(18);

            return Page();
        }

        // --- AJAX HANDLER FOR MUNICIPALITIES ---
        public async Task<JsonResult> OnGetMunicipiosAsync(string regiaoCodigo)
        {
            if (string.IsNullOrEmpty(regiaoCodigo))
                return new JsonResult(new List<object>());

            var municipios = await _context.Municipios
                .Where(m => m.RegiaoCodigo == regiaoCodigo)
                .OrderBy(m => m.Nome)
                .Select(m => new { value = m.Nome, text = m.Nome })
                .ToListAsync();

            return new JsonResult(municipios);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            if (Input.VoltaEm <= Input.IdaEm)
                ModelState.AddModelError("Input.VoltaEm", "A Data/Hora de Retorno deve ser posterior à Saída.");

            if (Input.MotoristaId != 0 && Input.MotoristaAuxId != 0 && Input.MotoristaId == Input.MotoristaAuxId)
                ModelState.AddModelError("Input.MotoristaAuxId", "O motorista auxiliar não pode ser o mesmo que o principal.");

            if (!Input.Passageiros.Any())
                ModelState.AddModelError("Input.Passageiros", "A lista de passageiros não pode estar vazia.");

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return Page();
            }

            var veiculoHealth = await _statusService.GetHealthAsync("VEICULO", Input.VeiculoPlaca);
            if (!veiculoHealth.IsLegal)
            {
                ModelState.AddModelError("Input.VeiculoPlaca", "O veículo selecionado não está legalizado.");
                await LoadDropdownsAsync();
                return Page();
            }

            var motoristaHealth = await _statusService.GetHealthAsync("MOTORISTA", Input.MotoristaId.ToString());
            if (!motoristaHealth.IsLegal)
            {
                ModelState.AddModelError("Input.MotoristaId", "O motorista principal selecionado não está legalizado.");
                await LoadDropdownsAsync();
                return Page();
            }

            if (Input.MotoristaAuxId.HasValue && Input.MotoristaAuxId.Value > 0)
            {
                var auxHealth = await _statusService.GetHealthAsync("MOTORISTA", Input.MotoristaAuxId.Value.ToString());
                if (!auxHealth.IsLegal)
                {
                    ModelState.AddModelError("Input.MotoristaAuxId", "O motorista auxiliar selecionado não está legalizado.");
                    await LoadDropdownsAsync();
                    return Page();
                }
            }

            var viagem = new Viagem
            {
                EmpresaCnpj = user.EmpresaCnpj,
                ViagemTipoNome = Input.ViagemTipoNome,
                NomeContratante = Input.NomeContratante,
                CpfCnpjContratante = Input.CpfCnpjContratante,
                RegiaoCodigo = Input.RegiaoCodigo,
                IdaEm = Input.IdaEm,
                VoltaEm = Input.VoltaEm,
                MunicipioOrigem = Input.MunicipioOrigem,
                MunicipioDestino = Input.MunicipioDestino,
                VeiculoPlaca = Input.VeiculoPlaca,
                MotoristaId = Input.MotoristaId,
                MotoristaAuxId = Input.MotoristaAuxId > 0 ? Input.MotoristaAuxId : null,
                Descricao = Input.Descricao
            };

            foreach (var p in Input.Passageiros)
            {
                viagem.Passageiros.Add(new Passageiro
                {
                    Nome = p.Nome,
                    Cpf = p.Documento // THE FIX: Assign the ViewModel's 'Documento' to the Entity's 'Cpf'
                });
            }

            _context.Viagens.Add(viagem);
            await _context.SaveChangesAsync();

            if (UploadNotaFiscal != null)
            {
                await _arquivoService.SalvarDocumentoAsync(UploadNotaFiscal, "NOTA_FISCAL", "VIAGEM", viagem.Id.ToString());
            }

            return RedirectToPage("/Empresa/MinhaEmpresa");
        }

        private async Task LoadDropdownsAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var cnpj = (await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail))?.EmpresaCnpj;

            var tipos = await _context.ViagemTipos.OrderBy(t => t.Nome).ToListAsync();
            ViagemTipos = new SelectList(tipos, "Nome", "Nome");

            // Load Regioes
            var regioes = await _context.Regioes.OrderBy(r => r.Ordem).ThenBy(r => r.Nome).ToListAsync();
            Regioes = new SelectList(regioes, "Codigo", "Nome");

            var veiculos = await _context.Veiculos.Where(v => v.EmpresaCnpj == cnpj).ToListAsync();
            var veiculoIds = veiculos.Select(v => v.Placa).ToList();
            var veiculosHealth = await _statusService.GetBulkHealthAsync("VEICULO", veiculoIds);

            var veiculosLegais = veiculos
                .Where(v => veiculosHealth.ContainsKey(v.Placa) && veiculosHealth[v.Placa].IsLegal)
                .Select(v => new { v.Placa, Descricao = $"{v.Placa} - {v.Modelo} ({v.NumeroLugares} lugares)" })
                .ToList();

            VeiculosValidos = new SelectList(veiculosLegais, "Placa", "Descricao");

            var motoristas = await _context.Motoristas.Where(m => m.EmpresaCnpj == cnpj).ToListAsync();
            var motoristaIds = motoristas.Select(m => m.Id.ToString()).ToList();
            var motoristasHealth = await _statusService.GetBulkHealthAsync("MOTORISTA", motoristaIds);

            var motoristasLegais = motoristas
                .Where(m => motoristasHealth.ContainsKey(m.Id.ToString()) && motoristasHealth[m.Id.ToString()].IsLegal)
                .Select(m => new { m.Id, m.Nome })
                .ToList();

            MotoristasValidos = new SelectList(motoristasLegais, "Id", "Nome");
        }
    }
}
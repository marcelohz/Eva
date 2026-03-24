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
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class NovaViagemModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly IEntityStatusService _statusService;

        public NovaViagemModel(EvaDbContext context, IEntityStatusService statusService)
        {
            _context = context;
            _statusService = statusService;
        }

        [BindProperty]
        public NovaViagemVM Input { get; set; } = new();

        [BindProperty]
        public string AcaoSubmit { get; set; } = string.Empty; // Mapeia o botão clicado

        public SelectList ViagemTipos { get; set; } = default!;
        public SelectList Regioes { get; set; } = default!;

        // Alterado de SelectList para IEnumerable<SelectListItem> para permitir a propriedade Disabled
        public IEnumerable<SelectListItem> VeiculosValidos { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> MotoristasValidos { get; set; } = new List<SelectListItem>();

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDropdownsAsync();

            Input.IdaEm = System.DateTime.Now.AddDays(1).Date.AddHours(8);
            Input.VoltaEm = System.DateTime.Now.AddDays(1).Date.AddHours(18);

            return Page();
        }

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

            // --- 1. VALIDAÇÃO DE STATUS DA EMPRESA ---
            var empresaHealth = await _statusService.GetHealthAsync("EMPRESA", user.EmpresaCnpj);
            if (!empresaHealth.IsLegal)
            {
                ModelState.AddModelError(string.Empty, "Sua empresa possui pendências documentais ou de análise e não está autorizada a criar novas viagens.");
                await LoadDropdownsAsync();
                return Page();
            }

            // --- 2. VALIDAÇÃO DE PERTENCIMENTO E STATUS DO VEÍCULO ---
            var veiculo = await _context.Veiculos.FirstOrDefaultAsync(v => v.Placa == Input.VeiculoPlaca && v.EmpresaCnpj == user.EmpresaCnpj);
            if (veiculo == null)
            {
                ModelState.AddModelError("Input.VeiculoPlaca", "O veículo informado é inválido ou não pertence à sua empresa.");
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

            // --- 3. VALIDAÇÃO DE PERTENCIMENTO E STATUS DO MOTORISTA PRINCIPAL ---
            var motorista = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == Input.MotoristaId && m.EmpresaCnpj == user.EmpresaCnpj);
            if (motorista == null)
            {
                ModelState.AddModelError("Input.MotoristaId", "O motorista informado é inválido ou não pertence à sua empresa.");
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

            // --- 4. VALIDAÇÃO DE PERTENCIMENTO E STATUS DO MOTORISTA AUXILIAR (SE HOUVER) ---
            if (Input.MotoristaAuxId.HasValue && Input.MotoristaAuxId.Value > 0)
            {
                var motoristaAux = await _context.Motoristas.FirstOrDefaultAsync(m => m.Id == Input.MotoristaAuxId.Value && m.EmpresaCnpj == user.EmpresaCnpj);
                if (motoristaAux == null)
                {
                    ModelState.AddModelError("Input.MotoristaAuxId", "O motorista auxiliar informado é inválido ou não pertence à sua empresa.");
                    await LoadDropdownsAsync();
                    return Page();
                }

                var auxHealth = await _statusService.GetHealthAsync("MOTORISTA", Input.MotoristaAuxId.Value.ToString());
                if (!auxHealth.IsLegal)
                {
                    ModelState.AddModelError("Input.MotoristaAuxId", "O motorista auxiliar selecionado não está legalizado.");
                    await LoadDropdownsAsync();
                    return Page();
                }
            }

            // --- MOCK DE CÁLCULO DE TARIFA ---
            // Simula uma matriz de distância. Valor base + acréscimo se for para outra cidade.
            decimal valorCalculado = 150.00m;
            if (Input.MunicipioOrigem.Trim().ToLower() != Input.MunicipioDestino.Trim().ToLower())
            {
                valorCalculado += 235.50m; // Acréscimo intermunicipal mockado
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
                Descricao = Input.Descricao,
                Valor = valorCalculado, // Valor setado via Mock
                Pago = false // Nasce sempre como não paga
            };

            foreach (var p in Input.Passageiros)
            {
                viagem.Passageiros.Add(new Passageiro
                {
                    Nome = p.Nome,
                    Cpf = p.Documento
                });
            }

            _context.Viagens.Add(viagem);
            await _context.SaveChangesAsync();

            // Roteamento baseado no botão clicado
            if (AcaoSubmit == "PagarDepois")
            {
                return RedirectToPage("/Empresa/MinhasViagens");
            }

            // Se clicou em "Revisao" (ou qualquer outra coisa) avança para o checkout
            return RedirectToPage("/Empresa/PagamentoViagem", new { id = viagem.Id });
        }

        private async Task LoadDropdownsAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var cnpj = (await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == userEmail))?.EmpresaCnpj;

            var tipos = await _context.ViagemTipos.OrderBy(t => t.Nome).ToListAsync();
            ViagemTipos = new SelectList(tipos, "Nome", "Nome");

            var regioes = await _context.Regioes.OrderBy(r => r.Ordem).ThenBy(r => r.Nome).ToListAsync();
            Regioes = new SelectList(regioes, "Codigo", "Nome");

            // Fetch and map Vehicles
            var veiculos = await _context.Veiculos.Where(v => v.EmpresaCnpj == cnpj).ToListAsync();
            var veiculoIds = veiculos.Select(v => v.Placa).ToList();
            var veiculosHealth = await _statusService.GetBulkHealthAsync("VEICULO", veiculoIds);

            VeiculosValidos = veiculos.Select(v =>
            {
                var isLegal = veiculosHealth.ContainsKey(v.Placa) && veiculosHealth[v.Placa].IsLegal;
                return new SelectListItem
                {
                    Value = v.Placa,
                    Text = isLegal ? $"{v.Placa} - {v.Modelo} ({v.NumeroLugares} lugares)" : $"{v.Placa} - {v.Modelo} (Bloqueado - Ver Pendências)",
                    Disabled = !isLegal
                };
            })
            .OrderBy(v => v.Disabled) // Valid ones on top
            .ThenBy(v => v.Text)
            .ToList();

            // Fetch and map Drivers
            var motoristas = await _context.Motoristas.Where(m => m.EmpresaCnpj == cnpj).ToListAsync();
            var motoristaIds = motoristas.Select(m => m.Id.ToString()).ToList();
            var motoristasHealth = await _statusService.GetBulkHealthAsync("MOTORISTA", motoristaIds);

            MotoristasValidos = motoristas.Select(m =>
            {
                var isLegal = motoristasHealth.ContainsKey(m.Id.ToString()) && motoristasHealth[m.Id.ToString()].IsLegal;
                return new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = isLegal ? m.Nome : $"{m.Nome} (Bloqueado - Ver Pendências)",
                    Disabled = !isLegal
                };
            })
            .OrderBy(m => m.Disabled) // Valid ones on top
            .ThenBy(m => m.Text)
            .ToList();
        }
    }
}
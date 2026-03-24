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
using System.Threading.Tasks;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class NovaViagemModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEntityStatusService _statusService;
        private readonly IViagemCreationService _viagemCreationService;
        private readonly IViagemRulesService _viagemRulesService;

        public NovaViagemModel(
            EvaDbContext context,
            ICurrentUserService currentUserService,
            IEntityStatusService statusService,
            IViagemCreationService viagemCreationService,
            IViagemRulesService viagemRulesService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _statusService = statusService;
            _viagemCreationService = viagemCreationService;
            _viagemRulesService = viagemRulesService;
        }

        [BindProperty]
        public NovaViagemVM Input { get; set; } = new();

        [BindProperty]
        public string AcaoSubmit { get; set; } = string.Empty;

        public SelectList ViagemTipos { get; set; } = default!;
        public SelectList Regioes { get; set; } = default!;
        public IEnumerable<SelectListItem> VeiculosValidos { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> MotoristasValidos { get; set; } = new List<SelectListItem>();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _currentUserService.GetCurrentUserAsync();
            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            var empresaHealth = await _statusService.GetHealthAsync("EMPRESA", user.EmpresaCnpj);
            if (!empresaHealth.IsLegal)
            {
                TempData["MensagemAviso"] = "Sua empresa precisa estar com o cadastro e a documentacao regularizados para cadastrar novas viagens.";
                return RedirectToPage("/Empresa/MinhasViagens");
            }

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
            var user = await _currentUserService.GetCurrentUserAsync();

            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return Page();
            }

            var eligibility = await _viagemRulesService.ValidateEligibilityAsync(new ViagemEligibilityRequest
            {
                EmpresaCnpj = user.EmpresaCnpj,
                IdaEm = Input.IdaEm,
                VoltaEm = Input.VoltaEm,
                VeiculoPlaca = Input.VeiculoPlaca,
                MotoristaId = Input.MotoristaId,
                MotoristaAuxId = Input.MotoristaAuxId,
                PassageiroCount = Input.Passageiros.Count
            });

            if (!eligibility.IsAllowed)
            {
                AddEligibilityError(eligibility);
                await LoadDropdownsAsync();
                return Page();
            }

            var creationResult = await _viagemCreationService.CreateAsync(new ViagemCreationRequest
            {
                EmpresaCnpj = user.EmpresaCnpj,
                Input = Input
            });

            if (AcaoSubmit == "PagarDepois")
            {
                return RedirectToPage("/Empresa/MinhasViagens");
            }

            return RedirectToPage("/Empresa/PagamentoViagem", new { id = creationResult.ViagemId });
        }

        private void AddEligibilityError(ViagemEligibilityResult eligibility)
        {
            switch (eligibility.Failure)
            {
                case ViagemEligibilityFailure.ReturnBeforeDeparture:
                    ModelState.AddModelError("Input.VoltaEm", "A Data/Hora de Retorno deve ser posterior à Saída.");
                    break;
                case ViagemEligibilityFailure.DuplicateDriver:
                    ModelState.AddModelError("Input.MotoristaAuxId", "O motorista auxiliar não pode ser o mesmo que o principal.");
                    break;
                case ViagemEligibilityFailure.EmptyPassengers:
                    ModelState.AddModelError("Input.Passageiros", "A lista de passageiros não pode estar vazia.");
                    break;
                case ViagemEligibilityFailure.CompanyNotLegal:
                    ModelState.AddModelError(string.Empty, "Sua empresa possui pendências documentais ou de análise e não está autorizada a criar novas viagens.");
                    break;
                case ViagemEligibilityFailure.VehicleNotFound:
                    ModelState.AddModelError("Input.VeiculoPlaca", "O veículo informado é inválido ou não pertence à sua empresa.");
                    break;
                case ViagemEligibilityFailure.VehicleNotLegal:
                    ModelState.AddModelError("Input.VeiculoPlaca", "O veículo selecionado não está legalizado.");
                    break;
                case ViagemEligibilityFailure.DriverNotFound:
                    ModelState.AddModelError("Input.MotoristaId", "O motorista informado é inválido ou não pertence à sua empresa.");
                    break;
                case ViagemEligibilityFailure.DriverNotLegal:
                    ModelState.AddModelError("Input.MotoristaId", "O motorista principal selecionado não está legalizado.");
                    break;
                case ViagemEligibilityFailure.AuxDriverNotFound:
                    ModelState.AddModelError("Input.MotoristaAuxId", "O motorista auxiliar informado é inválido ou não pertence à sua empresa.");
                    break;
                case ViagemEligibilityFailure.AuxDriverNotLegal:
                    ModelState.AddModelError("Input.MotoristaAuxId", "O motorista auxiliar selecionado não está legalizado.");
                    break;
            }
        }

        private async Task LoadDropdownsAsync()
        {
            var cnpj = _currentUserService.GetCurrentEmpresaCnpj() ??
                       (await _currentUserService.GetCurrentUserAsync())?.EmpresaCnpj;

            var tipos = await _context.ViagemTipos.OrderBy(t => t.Nome).ToListAsync();
            ViagemTipos = new SelectList(tipos, "Nome", "Nome");

            var regioes = await _context.Regioes.OrderBy(r => r.Ordem).ThenBy(r => r.Nome).ToListAsync();
            Regioes = new SelectList(regioes, "Codigo", "Nome");

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
            .OrderBy(v => v.Disabled)
            .ThenBy(v => v.Text)
            .ToList();

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
            .OrderBy(m => m.Disabled)
            .ThenBy(m => m.Text)
            .ToList();
        }
    }
}

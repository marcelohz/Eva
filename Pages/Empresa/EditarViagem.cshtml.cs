using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Eva.Pages.Empresa
{
    [Authorize(Policy = "AcessoEmpresa")]
    public class EditarViagemModel : PageModel
    {
        private readonly EvaDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IViagemManagementService _viagemManagementService;

        public EditarViagemModel(
            EvaDbContext context,
            ICurrentUserService currentUserService,
            IViagemManagementService viagemManagementService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _viagemManagementService = viagemManagementService;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty]
        public NovaViagemVM Input { get; set; } = new();

        public SelectList ViagemTipos { get; set; } = default!;
        public SelectList Regioes { get; set; } = default!;
        public IEnumerable<SelectListItem> Veiculos { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Motoristas { get; set; } = new List<SelectListItem>();

        public ViagemEditMode EditMode { get; set; }
        public string ActionLabel { get; set; } = "Detalhes";
        public string? ModeMessage { get; set; }

        public bool CanEditFull => EditMode == ViagemEditMode.Full;
        public bool IsReadOnly => EditMode == ViagemEditMode.ReadOnly;

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _currentUserService.GetCurrentUserAsync();
            if (user == null || string.IsNullOrEmpty(user.EmpresaCnpj))
                return RedirectToPage("/Login");

            var viagem = await LoadOwnedViagemAsync(user.EmpresaCnpj, Id);
            if (viagem == null)
                return NotFound();

            ApplyAccess(_viagemManagementService.GetAccess(viagem));
            MapViagemToInput(viagem);
            await LoadDropdownsAsync(user.EmpresaCnpj);

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

            var viagem = await LoadOwnedViagemAsync(user.EmpresaCnpj, Id);
            if (viagem == null)
                return NotFound();

            var access = _viagemManagementService.GetAccess(viagem);
            ApplyAccess(access);

            if (access.IsReadOnly)
            {
                TempData["MensagemAviso"] = access.Message ?? "Esta viagem não pode mais ser alterada.";
                return RedirectToPage("/Empresa/MinhasViagens");
            }

            NormalizeInputForMode(viagem, access);

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(user.EmpresaCnpj);
                return Page();
            }

            if (!Input.Passageiros.Any())
            {
                ModelState.AddModelError("Input.Passageiros", "A lista de passageiros não pode estar vazia.");
            }

            if (access.CanEditFull)
            {
                if (Input.VoltaEm <= Input.IdaEm)
                {
                    ModelState.AddModelError("Input.VoltaEm", "A Data/Hora de Retorno deve ser posterior à Saída.");
                }

                if (Input.MotoristaId != 0 &&
                    Input.MotoristaAuxId.HasValue &&
                    Input.MotoristaAuxId.Value != 0 &&
                    Input.MotoristaId == Input.MotoristaAuxId.Value)
                {
                    ModelState.AddModelError("Input.MotoristaAuxId", "O motorista auxiliar não pode ser o mesmo que o principal.");
                }

                if (!await _context.Veiculos.AnyAsync(v => v.Placa == Input.VeiculoPlaca && v.EmpresaCnpj == user.EmpresaCnpj))
                {
                    ModelState.AddModelError("Input.VeiculoPlaca", "O veículo informado é inválido ou não pertence à sua empresa.");
                }

                if (!await _context.Motoristas.AnyAsync(m => m.Id == Input.MotoristaId && m.EmpresaCnpj == user.EmpresaCnpj))
                {
                    ModelState.AddModelError("Input.MotoristaId", "O motorista informado é inválido ou não pertence à sua empresa.");
                }

                if (Input.MotoristaAuxId.HasValue &&
                    Input.MotoristaAuxId.Value > 0 &&
                    !await _context.Motoristas.AnyAsync(m => m.Id == Input.MotoristaAuxId.Value && m.EmpresaCnpj == user.EmpresaCnpj))
                {
                    ModelState.AddModelError("Input.MotoristaAuxId", "O motorista auxiliar informado é inválido ou não pertence à sua empresa.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(user.EmpresaCnpj);
                return Page();
            }

            if (access.CanEditFull)
            {
                viagem.ViagemTipoNome = Input.ViagemTipoNome;
                viagem.NomeContratante = Input.NomeContratante;
                viagem.CpfCnpjContratante = Input.CpfCnpjContratante;
                viagem.RegiaoCodigo = Input.RegiaoCodigo;
                viagem.IdaEm = Input.IdaEm;
                viagem.VoltaEm = Input.VoltaEm;
                viagem.MunicipioOrigem = Input.MunicipioOrigem;
                viagem.MunicipioDestino = Input.MunicipioDestino;
                viagem.VeiculoPlaca = Input.VeiculoPlaca;
                viagem.MotoristaId = Input.MotoristaId;
                viagem.MotoristaAuxId = Input.MotoristaAuxId > 0 ? Input.MotoristaAuxId : null;
                viagem.Descricao = Input.Descricao;
            }

            ReplacePassengers(viagem, Input.Passageiros);
            await _context.SaveChangesAsync();

            TempData["MensagemSucesso"] = access.CanEditFull
                ? $"Viagem #{viagem.Id:D5} atualizada com sucesso."
                : $"Lista de passageiros da viagem #{viagem.Id:D5} atualizada com sucesso.";

            return RedirectToPage("/Empresa/MinhasViagens");
        }

        private async Task<Viagem?> LoadOwnedViagemAsync(string empresaCnpj, int viagemId)
        {
            return await _context.Viagens
                .Include(v => v.Passageiros)
                .FirstOrDefaultAsync(v => v.Id == viagemId && v.EmpresaCnpj == empresaCnpj);
        }

        private void ApplyAccess(ViagemManagementAccessResult access)
        {
            EditMode = access.EditMode;
            ActionLabel = access.ActionLabel;
            ModeMessage = access.Message;
        }

        private void MapViagemToInput(Viagem viagem)
        {
            Input = new NovaViagemVM
            {
                ViagemTipoNome = viagem.ViagemTipoNome,
                NomeContratante = viagem.NomeContratante,
                CpfCnpjContratante = viagem.CpfCnpjContratante,
                RegiaoCodigo = viagem.RegiaoCodigo,
                IdaEm = viagem.IdaEm.ToLocalTime(),
                VoltaEm = viagem.VoltaEm.ToLocalTime(),
                MunicipioOrigem = viagem.MunicipioOrigem,
                MunicipioDestino = viagem.MunicipioDestino,
                VeiculoPlaca = viagem.VeiculoPlaca,
                MotoristaId = viagem.MotoristaId,
                MotoristaAuxId = viagem.MotoristaAuxId,
                Descricao = viagem.Descricao,
                Passageiros = viagem.Passageiros
                    .OrderBy(p => p.Id)
                    .Select(p => new PassageiroVM
                    {
                        Nome = p.Nome,
                        Documento = p.Cpf
                    })
                    .ToList()
            };
        }

        private void NormalizeInputForMode(Viagem viagem, ViagemManagementAccessResult access)
        {
            if (access.EditMode != ViagemEditMode.PassengersOnly)
            {
                return;
            }

            Input.ViagemTipoNome = viagem.ViagemTipoNome;
            Input.NomeContratante = viagem.NomeContratante;
            Input.CpfCnpjContratante = viagem.CpfCnpjContratante;
            Input.RegiaoCodigo = viagem.RegiaoCodigo;
            Input.IdaEm = viagem.IdaEm.ToLocalTime();
            Input.VoltaEm = viagem.VoltaEm.ToLocalTime();
            Input.MunicipioOrigem = viagem.MunicipioOrigem;
            Input.MunicipioDestino = viagem.MunicipioDestino;
            Input.VeiculoPlaca = viagem.VeiculoPlaca;
            Input.MotoristaId = viagem.MotoristaId;
            Input.MotoristaAuxId = viagem.MotoristaAuxId;
            Input.Descricao = viagem.Descricao;

            ModelState.Remove("Input.ViagemTipoNome");
            ModelState.Remove("Input.NomeContratante");
            ModelState.Remove("Input.CpfCnpjContratante");
            ModelState.Remove("Input.RegiaoCodigo");
            ModelState.Remove("Input.IdaEm");
            ModelState.Remove("Input.VoltaEm");
            ModelState.Remove("Input.MunicipioOrigem");
            ModelState.Remove("Input.MunicipioDestino");
            ModelState.Remove("Input.VeiculoPlaca");
            ModelState.Remove("Input.MotoristaId");
        }

        private static void ReplacePassengers(Viagem viagem, List<PassageiroVM> passageiros)
        {
            viagem.Passageiros.Clear();

            foreach (var passageiro in passageiros)
            {
                viagem.Passageiros.Add(new Passageiro
                {
                    Nome = passageiro.Nome,
                    Cpf = passageiro.Documento
                });
            }
        }

        private async Task LoadDropdownsAsync(string empresaCnpj)
        {
            var tipos = await _context.ViagemTipos.OrderBy(t => t.Nome).ToListAsync();
            ViagemTipos = new SelectList(tipos, "Nome", "Nome");

            var regioes = await _context.Regioes.OrderBy(r => r.Ordem).ThenBy(r => r.Nome).ToListAsync();
            Regioes = new SelectList(regioes, "Codigo", "Nome");

            Veiculos = await _context.Veiculos
                .Where(v => v.EmpresaCnpj == empresaCnpj)
                .OrderBy(v => v.Placa)
                .Select(v => new SelectListItem
                {
                    Value = v.Placa,
                    Text = $"{v.Placa} - {v.Modelo} ({v.NumeroLugares} lugares)"
                })
                .ToListAsync();

            Motoristas = await _context.Motoristas
                .Where(m => m.EmpresaCnpj == empresaCnpj)
                .OrderBy(m => m.Nome)
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = m.Nome
                })
                .ToListAsync();
        }
    }
}

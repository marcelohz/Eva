using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Models.ViewModels;
using Eva.Workflow;
using System.Linq;
using System.Text.Json;

namespace Eva.Services
{
    public class PendenciaService
    {
        private readonly EvaDbContext _context;

        public PendenciaService(EvaDbContext context) { _context = context; }

        public async Task SalvarDadosPropostosAsync(string tipo, string id, string dadosPropostos)
        {
            tipo = tipo.ToUpperInvariant();
            var atual = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == tipo && p.EntidadeId == id);

            if (atual?.Status == WorkflowValidator.AguardandoAnalise)
            {
                var ticket = await _context.FluxoPendencias
                    .Where(f => f.EntidadeTipo == tipo && f.EntidadeId == id && f.Status == WorkflowValidator.AguardandoAnalise)
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefaultAsync();

                if (ticket != null)
                {
                    ticket.DadosPropostos = dadosPropostos;
                    ticket.CriadoEm = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return;
                }
            }

            await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.AguardandoAnalise, null, null, dadosPropostos);
        }

        public async Task AvancarEntidadeAsync(string tipo, string id) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.AguardandoAnalise);
        public async Task IniciarAnaliseAsync(string tipo, string id, string analista) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.EmAnalise, analista);
        public async Task AprovarAsync(string tipo, string id, string analista) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.Aprovado, analista);
        public async Task RejeitarAsync(string tipo, string id, string analista, string motivo) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.Rejeitado, analista, motivo);

        private async Task ProcessarTransicaoAsync(string entidadeTipo, string entidadeId, string novoStatus, string? analistaEmail = null, string? motivo = null, string? novosDadosPropostos = null)
        {
            entidadeTipo = entidadeTipo.ToUpperInvariant();
            analistaEmail = analistaEmail?.ToLowerInvariant();

            var atual = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == entidadeTipo && p.EntidadeId == entidadeId);

            WorkflowValidator.ValidateTransition(atual?.Status, novoStatus, atual?.Analista, analistaEmail, motivo);

            if (atual?.Status == novoStatus && novosDadosPropostos == null) return;

            string? dadosPropostosFinais = novosDadosPropostos ?? atual?.DadosPropostos;

            var novaPendencia = new FluxoPendencia
            {
                EntidadeTipo = entidadeTipo,
                EntidadeId = entidadeId,
                Status = novoStatus,
                Analista = analistaEmail,
                Motivo = motivo,
                DadosPropostos = dadosPropostosFinais,
                CriadoEm = DateTime.Now
            };

            _context.FluxoPendencias.Add(novaPendencia);

            if (novoStatus == WorkflowValidator.Aprovado && !string.IsNullOrEmpty(dadosPropostosFinais))
            {
                await AplicarDadosAprovadosAsync(entidadeTipo, entidadeId, dadosPropostosFinais);
            }

            await _context.SaveChangesAsync();

            if (novoStatus == WorkflowValidator.Aprovado)
                await LinkApprovedDocumentsAsync(entidadeTipo, entidadeId, novaPendencia.Id);
        }

        private async Task AplicarDadosAprovadosAsync(string tipo, string id, string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (tipo == "VEICULO")
            {
                var live = await _context.Veiculos.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Placa == id);
                var draft = JsonSerializer.Deserialize<VeiculoVM>(json, options);
                if (live != null && draft != null)
                {
                    live.ChassiNumero = draft.ChassiNumero;
                    live.Renavan = draft.Renavan;
                    live.Modelo = draft.Modelo;
                    live.PotenciaMotor = draft.PotenciaMotor;
                    live.NumeroLugares = draft.NumeroLugares;
                    live.AnoFabricacao = draft.AnoFabricacao;
                    live.ModeloAno = draft.ModeloAno;
                    live.VeiculoCombustivelNome = draft.VeiculoCombustivelNome;
                }
            }
            else if (tipo == "EMPRESA")
            {
                var live = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == id);
                var draft = JsonSerializer.Deserialize<EmpresaVM>(json, options);
                if (live != null && draft != null)
                {
                    live.Nome = draft.Nome;
                    live.NomeFantasia = draft.NomeFantasia;
                    live.Email = draft.Email;
                    live.Telefone = draft.Telefone;
                    live.Endereco = draft.Endereco;
                    live.Cep = draft.Cep;
                    live.EnderecoNumero = draft.EnderecoNumero;
                    live.EnderecoComplemento = draft.EnderecoComplemento;
                    live.Bairro = draft.Bairro;
                    live.Cidade = draft.Cidade;
                    live.Estado = draft.Estado;
                }
            }
            else if (tipo == "MOTORISTA" && int.TryParse(id, out int mId))
            {
                var live = await _context.Motoristas.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == mId);
                var draft = JsonSerializer.Deserialize<Motorista>(json, options);
                if (live != null && draft != null)
                {
                    live.Cpf = draft.Cpf;
                    live.Cnh = draft.Cnh;
                    live.Nome = draft.Nome;
                    live.Email = draft.Email;
                }
            }
        }

        private async Task LinkApprovedDocumentsAsync(string tipo, string id, int fluxoId)
        {
            IQueryable<Documento> docs = _context.Documentos.Where(d => d.FluxoPendenciaId == null);
            if (tipo == "EMPRESA") docs = docs.Where(d => _context.DocumentoEmpresas.Any(de => de.Id == d.Id && de.EmpresaCnpj == id));
            else if (tipo == "VEICULO") docs = docs.Where(d => _context.DocumentoVeiculos.Any(dv => dv.Id == d.Id && dv.VeiculoPlaca == id));
            else if (tipo == "MOTORISTA" && int.TryParse(id, out int mId)) docs = docs.Where(d => _context.DocumentoMotoristas.Any(dm => dm.Id == d.Id && dm.MotoristaId == mId));

            foreach (var doc in await docs.ToListAsync()) { doc.FluxoPendenciaId = fluxoId; doc.AprovadoEm = DateTime.Now; }
            await _context.SaveChangesAsync();
        }

        public async Task<List<FluxoPendencia>> GetHistoricoAsync(string tipo, string id) => await _context.FluxoPendencias.Where(p => p.EntidadeTipo == tipo.ToUpperInvariant() && p.EntidadeId == id).OrderByDescending(p => p.CriadoEm).ToListAsync();
        public async Task<string?> GetStatusAsync(string tipo, string id) => (await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == tipo.ToUpperInvariant() && p.EntidadeId == id))?.Status;
    }
}
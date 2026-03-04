using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
using Eva.Workflow;
using System.Linq;

namespace Eva.Services
{
    public class PendenciaService
    {
        private readonly EvaDbContext _context;

        public PendenciaService(EvaDbContext context) { _context = context; }

        public async Task AvancarEntidadeAsync(string tipo, string id) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.AguardandoAnalise);
        public async Task IniciarAnaliseAsync(string tipo, string id, string analista) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.EmAnalise, analista);
        public async Task AprovarAsync(string tipo, string id, string analista) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.Aprovado, analista);
        public async Task RejeitarAsync(string tipo, string id, string analista, string motivo) => await ProcessarTransicaoAsync(tipo, id, WorkflowValidator.Rejeitado, analista, motivo);

        private async Task ProcessarTransicaoAsync(string entidadeTipo, string entidadeId, string novoStatus, string? analistaEmail = null, string? motivo = null)
        {
            entidadeTipo = entidadeTipo.ToUpperInvariant();
            analistaEmail = analistaEmail?.ToLowerInvariant();

            var atual = await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == entidadeTipo && p.EntidadeId == entidadeId);

            WorkflowValidator.ValidateTransition(atual?.Status, novoStatus, atual?.Analista, analistaEmail, motivo);

            if (atual?.Status == novoStatus) return;

            var novaPendencia = new FluxoPendencia
            {
                EntidadeTipo = entidadeTipo,
                EntidadeId = entidadeId,
                Status = novoStatus,
                Analista = analistaEmail,
                Motivo = motivo,
                CriadoEm = DateTime.Now // Fixes Year 0001
            };

            _context.FluxoPendencias.Add(novaPendencia);
            await SyncEntityStatusAsync(entidadeTipo, entidadeId, novoStatus);
            await _context.SaveChangesAsync();

            if (novoStatus == WorkflowValidator.Aprovado)
                await LinkApprovedDocumentsAsync(entidadeTipo, entidadeId, novaPendencia.Id);
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

        private async Task SyncEntityStatusAsync(string tipo, string id, string status)
        {
            if (tipo == "VEICULO") { var v = await _context.Veiculos.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Placa == id); if (v != null) v.EventualStatus = status; }
            else if (tipo == "MOTORISTA" && int.TryParse(id, out int mId)) { var m = await _context.Motoristas.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == mId); if (m != null) m.EventualStatus = status; }
            else if (tipo == "EMPRESA") { var e = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == id); if (e != null) e.EventualStatus = status; }
        }

        public async Task<List<FluxoPendencia>> GetHistoricoAsync(string tipo, string id) => await _context.FluxoPendencias.Where(p => p.EntidadeTipo == tipo.ToUpperInvariant() && p.EntidadeId == id).OrderByDescending(p => p.CriadoEm).ToListAsync();
        public async Task<string?> GetStatusAsync(string tipo, string id) => (await _context.VPendenciasAtuais.FirstOrDefaultAsync(p => p.EntidadeTipo == tipo.ToUpperInvariant() && p.EntidadeId == id))?.Status;
    }
}
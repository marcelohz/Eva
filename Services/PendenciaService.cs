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

        public PendenciaService(EvaDbContext context)
        {
            _context = context;
        }

        // --- BACKWARD COMPATIBILITY FACADE ---
        // These keep your Razor Pages from breaking, but route everything through the strict pipeline.

        public async Task AvancarEntidadeAsync(string entidadeTipo, string entidadeId)
        {
            await ProcessarTransicaoAsync(entidadeTipo, entidadeId, WorkflowValidator.AguardandoAnalise, null, null);
        }

        public async Task IniciarAnaliseAsync(string entidadeTipo, string entidadeId, string analistaEmail)
        {
            await ProcessarTransicaoAsync(entidadeTipo, entidadeId, WorkflowValidator.EmAnalise, analistaEmail, null);
        }

        public async Task AprovarAsync(string entidadeTipo, string entidadeId, string analistaEmail)
        {
            await ProcessarTransicaoAsync(entidadeTipo, entidadeId, WorkflowValidator.Aprovado, analistaEmail, null);
        }

        public async Task RejeitarAsync(string entidadeTipo, string entidadeId, string analistaEmail, string motivo)
        {
            await ProcessarTransicaoAsync(entidadeTipo, entidadeId, WorkflowValidator.Rejeitado, analistaEmail, motivo);
        }

        // --- THE CORE PIPELINE ---

        /// <summary>
        /// The single entry point for ALL workflow transitions.
        /// </summary>
        private async Task ProcessarTransicaoAsync(
            string entidadeTipo,
            string entidadeId,
            string novoStatus,
            string? analistaEmail = null,
            string? motivo = null)
        {
            // 1. Fetch current state
            var atual = await _context.VPendenciasAtuais
                .FirstOrDefaultAsync(p => p.EntidadeTipo == entidadeTipo && p.EntidadeId == entidadeId);

            string? statusAtual = atual?.Status;
            string? analistaAtual = atual?.Analista;

            // 2. The Brain takes over: Validate the transition
            WorkflowValidator.ValidateTransition(
                currentState: statusAtual,
                nextState: novoStatus,
                currentAnalistaEmail: analistaAtual,
                nextAnalistaEmail: analistaEmail,
                motivo: motivo
            );

            // 3. Idempotency Check: If the state is exactly the same and the Brain allowed it, 
            // we return silently to avoid tripping the database's fn_evitar_status_repetido trigger.
            if (statusAtual == novoStatus && analistaAtual == analistaEmail)
            {
                return;
            }

            // 4. Brawn: Execute database operations
            var novaPendencia = new FluxoPendencia
            {
                EntidadeTipo = entidadeTipo,
                EntidadeId = entidadeId,
                Status = novoStatus,
                Analista = analistaEmail,
                Motivo = motivo
            };

            _context.FluxoPendencias.Add(novaPendencia);
            await SyncEntityStatusAsync(entidadeTipo, entidadeId, novoStatus);
            await _context.SaveChangesAsync();
        }

        // --- UNCHANGED HELPER METHODS ---

        public async Task<List<FluxoPendencia>> GetHistoricoAsync(string entidadeTipo, string entidadeId)
        {
            return await _context.FluxoPendencias
                .Where(p => p.EntidadeTipo == entidadeTipo && p.EntidadeId == entidadeId)
                .OrderByDescending(p => p.CriadoEm)
                .ToListAsync();
        }

        public async Task<string?> GetStatusAsync(string entidadeTipo, string entidadeId)
        {
            var atual = await _context.VPendenciasAtuais
                .FirstOrDefaultAsync(p => p.EntidadeTipo == entidadeTipo && p.EntidadeId == entidadeId);
            return atual?.Status;
        }

        private async Task SyncEntityStatusAsync(string entidadeTipo, string entidadeId, string status)
        {
            if (entidadeTipo == "VEICULO")
            {
                var veiculo = await _context.Veiculos.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Placa == entidadeId);
                if (veiculo != null) veiculo.EventualStatus = status;
            }
            else if (entidadeTipo == "MOTORISTA")
            {
                if (int.TryParse(entidadeId, out int id))
                {
                    var motorista = await _context.Motoristas.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == id);
                    if (motorista != null) motorista.EventualStatus = status;
                }
            }
            else if (entidadeTipo == "EMPRESA")
            {
                var empresa = await _context.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Cnpj == entidadeId);
                if (empresa != null) empresa.EventualStatus = status;
            }
        }
    }
}
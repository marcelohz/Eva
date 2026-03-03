using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;
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

        public async Task AvancarEntidadeAsync(string entidadeTipo, string entidadeId)
        {
            var atual = await _context.VPendenciasAtuais
                .FirstOrDefaultAsync(p => p.EntidadeTipo == entidadeTipo && p.EntidadeId == entidadeId);

            if (atual != null && atual.Status == "EM_ANALISE")
            {
                throw new InvalidOperationException("Não é possível alterar uma entidade que está em análise.");
            }

            if (atual == null || atual.Status == "APROVADO" || atual.Status == "REJEITADO")
            {
                var novaPendencia = new FluxoPendencia
                {
                    EntidadeTipo = entidadeTipo,
                    EntidadeId = entidadeId,
                    Status = "AGUARDANDO_ANALISE"
                };

                _context.FluxoPendencias.Add(novaPendencia);
                await SyncEntityStatusAsync(entidadeTipo, entidadeId, "AGUARDANDO_ANALISE");
                await _context.SaveChangesAsync();
            }
        }

        // --- NEW ANALYST METHODS ---

        public async Task IniciarAnaliseAsync(string entidadeTipo, string entidadeId, string analista)
        {
            var novaPendencia = new FluxoPendencia { EntidadeTipo = entidadeTipo, EntidadeId = entidadeId, Status = "EM_ANALISE", Analista = analista };
            _context.FluxoPendencias.Add(novaPendencia);
            await SyncEntityStatusAsync(entidadeTipo, entidadeId, "EM_ANALISE");
            await _context.SaveChangesAsync();
        }

        public async Task AprovarAsync(string entidadeTipo, string entidadeId, string analista)
        {
            var novaPendencia = new FluxoPendencia { EntidadeTipo = entidadeTipo, EntidadeId = entidadeId, Status = "APROVADO", Analista = analista };
            _context.FluxoPendencias.Add(novaPendencia);
            await SyncEntityStatusAsync(entidadeTipo, entidadeId, "APROVADO");
            await _context.SaveChangesAsync();
        }

        public async Task RejeitarAsync(string entidadeTipo, string entidadeId, string analista, string motivo)
        {
            var novaPendencia = new FluxoPendencia { EntidadeTipo = entidadeTipo, EntidadeId = entidadeId, Status = "REJEITADO", Analista = analista, Motivo = motivo };
            _context.FluxoPendencias.Add(novaPendencia);
            await SyncEntityStatusAsync(entidadeTipo, entidadeId, "REJEITADO");
            await _context.SaveChangesAsync();
        }

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
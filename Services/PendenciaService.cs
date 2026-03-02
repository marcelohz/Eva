using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Models;

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
                throw new InvalidOperationException("Não é possível avançar uma entidade que está em análise.");
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

                // Keep the UI fast by syncing the status directly to the entity
                await SyncEntityStatusAsync(entidadeTipo, entidadeId, "AGUARDANDO_ANALISE");

                await _context.SaveChangesAsync();
            }
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
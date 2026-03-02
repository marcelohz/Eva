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

            // If it doesn't exist, or if it was previously approved/rejected, we restart the flow.
            if (atual == null || atual.Status == "APROVADO" || atual.Status == "REJEITADO")
            {
                var novaPendencia = new FluxoPendencia
                {
                    EntidadeTipo = entidadeTipo,
                    EntidadeId = entidadeId,
                    Status = "AGUARDANDO_ANALISE"
                };

                _context.FluxoPendencias.Add(novaPendencia);
                await _context.SaveChangesAsync();
            }
        }
    }
}
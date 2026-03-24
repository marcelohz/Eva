using System.Collections.Generic;
using System.Linq;

namespace Eva.Models.ViewModels
{
    public class ConformidadeDashboardVM
    {
        public int TotalPendenciasCriticas { get; set; }
        public bool TemPendenciasCriticas => TotalPendenciasCriticas > 0;

        public ConformidadeEntidadeVM? Empresa { get; set; }
        public List<ConformidadeEntidadeVM> Veiculos { get; set; } = new List<ConformidadeEntidadeVM>();
        public List<ConformidadeEntidadeVM> Motoristas { get; set; } = new List<ConformidadeEntidadeVM>();
    }

    public class ConformidadeEntidadeVM
    {
        public string Id { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;

        // Expected values: APROVADO, REJEITADO, INCOMPLETO, EM_ANALISE
        public string StatusGeral { get; set; } = string.Empty;

        // Populated if an analyst explicitly rejected the entity
        public string? MotivoRejeicao { get; set; }

        public List<string> DocumentosFaltantes { get; set; } = new List<string>();
        public List<string> DocumentosVencidos { get; set; } = new List<string>();
        public List<string> DocumentosEmAnalise { get; set; } = new List<string>();

        // Determines if this entity is prevented from participating in new trips
        public bool IsBlocked => StatusGeral == "REJEITADO" ||
                                 StatusGeral == "INCOMPLETO" ||
                                 DocumentosFaltantes.Any() ||
                                 DocumentosVencidos.Any();
    }
}
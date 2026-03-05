using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("fluxo_pendencia", Schema = "eventual")]
    public class FluxoPendencia
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("criado_em")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Let PostgreSQL handle DEFAULT now()
        public DateTime CriadoEm { get; set; }

        [Column("entidade_tipo")]
        public string EntidadeTipo { get; set; } = string.Empty;

        [Column("entidade_id")]
        public string EntidadeId { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("analista")]
        public string? Analista { get; set; }

        [Column("motivo")]
        public string? Motivo { get; set; }

        [Column("dados_propostos", TypeName = "jsonb")]
        public string? DadosPropostos { get; set; }

    }
}
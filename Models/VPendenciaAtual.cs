using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("v_pendencia_atual", Schema = "eventual")]
    public class VPendenciaAtual
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("entidade_tipo")]
        public string EntidadeTipo { get; set; } = string.Empty;

        [Column("entidade_id")]
        public string EntidadeId { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("analista")]
        public string? Analista { get; set; }

        [Column("criado_em")]
        public DateTime CriadoEm { get; set; }

        [Column("motivo")]
        public string? Motivo { get; set; }

        [Column("dados_propostos")]
        public string? DadosPropostos { get; set; }
    }
}
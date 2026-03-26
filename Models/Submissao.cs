using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("submissao", Schema = "eventual")]
    public class Submissao
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

        [Column("submissao_origem_id")]
        public int? SubmissaoOrigemId { get; set; }

        [Column("criado_em")]
        public DateTime CriadoEm { get; set; }

        [Column("atualizado_em")]
        public DateTime AtualizadoEm { get; set; }

        [Column("submetido_em")]
        public DateTime? SubmetidoEm { get; set; }

        [Column("finalizado_em")]
        public DateTime? FinalizadoEm { get; set; }

        [Column("criado_por")]
        public string? CriadoPor { get; set; }

        [Column("analista_atual")]
        public string? AnalistaAtual { get; set; }

        [Column("observacao_analista")]
        public string? ObservacaoAnalista { get; set; }
    }
}

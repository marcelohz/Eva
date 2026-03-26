using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("submissao_dados", Schema = "eventual")]
    public class SubmissaoDados
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("submissao_id")]
        public int SubmissaoId { get; set; }

        [Column("dados_propostos", TypeName = "jsonb")]
        public string DadosPropostos { get; set; } = "{}";

        [Column("hash_dados")]
        public string? HashDados { get; set; }

        [Column("carregado_do_live")]
        public bool CarregadoDoLive { get; set; }

        [Column("status_revisao")]
        public string StatusRevisao { get; set; } = string.Empty;

        [Column("motivo_rejeicao")]
        public string? MotivoRejeicao { get; set; }

        [Column("revisado_por")]
        public string? RevisadoPor { get; set; }

        [Column("revisado_em")]
        public DateTime? RevisadoEm { get; set; }

        [Column("criado_em")]
        public DateTime CriadoEm { get; set; }

        [Column("atualizado_em")]
        public DateTime AtualizadoEm { get; set; }
    }
}

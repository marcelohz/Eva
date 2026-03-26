using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("submissao_documento", Schema = "eventual")]
    public class SubmissaoDocumento
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("submissao_id")]
        public int SubmissaoId { get; set; }

        [Column("documento_id")]
        public int DocumentoId { get; set; }

        [Column("documento_tipo_nome")]
        public string DocumentoTipoNome { get; set; } = string.Empty;

        [Column("obrigatorio_snapshot")]
        public bool ObrigatorioSnapshot { get; set; }

        [Column("validade_informada")]
        public DateOnly? ValidadeInformada { get; set; }

        [Column("status_revisao")]
        public string StatusRevisao { get; set; } = string.Empty;

        [Column("motivo_rejeicao")]
        public string? MotivoRejeicao { get; set; }

        [Column("revisado_por")]
        public string? RevisadoPor { get; set; }

        [Column("revisado_em")]
        public DateTime? RevisadoEm { get; set; }

        [Column("ativo_na_submissao")]
        public bool AtivoNaSubmissao { get; set; }

        [Column("carregado_de_documento_atual")]
        public bool CarregadoDeDocumentoAtual { get; set; }

        [Column("substitui_submissao_documento_id")]
        public int? SubstituiSubmissaoDocumentoId { get; set; }

        [Column("criado_em")]
        public DateTime CriadoEm { get; set; }
    }
}

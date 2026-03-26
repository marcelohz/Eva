using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("entidade_documento_atual", Schema = "eventual")]
    public class EntidadeDocumentoAtual
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("entidade_tipo")]
        public string EntidadeTipo { get; set; } = string.Empty;

        [Column("entidade_id")]
        public string EntidadeId { get; set; } = string.Empty;

        [Column("documento_tipo_nome")]
        public string DocumentoTipoNome { get; set; } = string.Empty;

        [Column("documento_id")]
        public int DocumentoId { get; set; }

        [Column("submissao_documento_id")]
        public int SubmissaoDocumentoId { get; set; }

        [Column("submissao_id")]
        public int SubmissaoId { get; set; }

        [Column("definido_em")]
        public DateTime DefinidoEm { get; set; }
    }
}

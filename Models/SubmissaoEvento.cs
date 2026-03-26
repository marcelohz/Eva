using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("submissao_evento", Schema = "eventual")]
    public class SubmissaoEvento
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("submissao_id")]
        public int SubmissaoId { get; set; }

        [Column("tipo_evento")]
        public string TipoEvento { get; set; } = string.Empty;

        [Column("descricao")]
        public string? Descricao { get; set; }

        [Column("usuario_email")]
        public string? UsuarioEmail { get; set; }

        [Column("criado_em")]
        public DateTime CriadoEm { get; set; }
    }
}

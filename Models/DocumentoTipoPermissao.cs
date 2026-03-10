using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("documento_tipo_permissao", Schema = "eventual")]
    public class DocumentoTipoPermissao
    {
        [Column("tipo_nome")]
        public string TipoNome { get; set; } = string.Empty;

        [Column("entidade_tipo")]
        public string EntidadeTipo { get; set; } = string.Empty;

        [ForeignKey("TipoNome")]
        public virtual DocumentoTipo? DocumentoTipo { get; set; }
    }
}
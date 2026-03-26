using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("documento_tipo", Schema = "eventual")]
    public class DocumentoTipo
    {
        [Key]
        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Column("descricao")]
        public string? Descricao { get; set; }

        [Column("obrigatorio")]
        public bool Obrigatorio { get; set; }

        [Column("permite_multiplos")]
        public bool PermiteMultiplos { get; set; }

        public virtual ICollection<DocumentoTipoVinculo> Vinculos { get; set; } = new List<DocumentoTipoVinculo>();
    }
}

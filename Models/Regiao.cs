using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("regiao", Schema = "geral")]
    public class Regiao
    {
        [Key]
        [Column("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [Column("nome")]
        public string? Nome { get; set; }

        [Column("ordem")]
        public int? Ordem { get; set; }

        public virtual ICollection<Municipio> Municipios { get; set; } = new List<Municipio>();
    }
}
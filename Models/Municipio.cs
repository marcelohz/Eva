using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("municipio", Schema = "geral")]
    public class Municipio
    {
        [Key]
        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Column("regiao_codigo")]
        public string? RegiaoCodigo { get; set; }

        [ForeignKey("RegiaoCodigo")]
        public virtual Regiao? Regiao { get; set; }
    }
}
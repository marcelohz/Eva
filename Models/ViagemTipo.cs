using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("viagem_tipo", Schema = "eventual")]
    public class ViagemTipo
    {
        [Key]
        [Column("nome")]
        public string Nome { get; set; } = string.Empty;
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("papel", Schema = "web")]
    public class Papel
    {
        [Key]
        [Column("nome")] // This maps the C# 'Nome' to the database 'nome'
        public string Nome { get; set; } = string.Empty;
    }
}
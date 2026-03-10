using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Eva.Models
{
    [Table("passageiro", Schema = "eventual")]
    public class Passageiro
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("viagem_id")]
        public int ViagemId { get; set; }

        [Required]
        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [Column("cpf")] // THE FIX: Mapped to 'cpf' instead of 'documento'
        public string Cpf { get; set; } = string.Empty;

        [ValidateNever]
        [ForeignKey("ViagemId")]
        public virtual Viagem? Viagem { get; set; }
    }
}
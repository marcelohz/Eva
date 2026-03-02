using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("empresa", Schema = "geral")]
    public class Empresa
    {
        [Key]
        [Column("cnpj")]
        public string Cnpj { get; set; } = string.Empty;

        [Required]
        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Column("nome_fantasia")]
        public string? NomeFantasia { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("telefone")]
        public string? Telefone { get; set; }

        // ADDED: The missing property so PendenciaService can sync the status
        [Column("eventual_status")]
        public string? EventualStatus { get; set; }

        // Navigation properties
        public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
        public virtual ICollection<Veiculo> Veiculos { get; set; } = new List<Veiculo>();
        public virtual ICollection<Motorista> Motoristas { get; set; } = new List<Motorista>();
    }
}
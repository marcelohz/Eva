using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Eva.Models
{
    [Table("motorista", Schema = "eventual")]
    public class Motorista
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [ValidateNever]
        [Column("empresa_cnpj")]
        public string? EmpresaCnpj { get; set; }

        [Required(ErrorMessage = "O CPF é obrigatório")]
        [RegularExpression(@"^\d{3}\.?\d{3}\.?\d{3}-?\d{2}$", ErrorMessage = "Formato de CPF inválido")]
        [Column("cpf")]
        public string Cpf { get; set; } = string.Empty;

        [Required(ErrorMessage = "A CNH é obrigatória")]
        [Column("cnh")]
        public string Cnh { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email em formato inválido")]
        [Column("email")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "O Nome é obrigatório")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 100 caracteres")]
        [Column("nome")]
        public string? Nome { get; set; }

        [ValidateNever]
        [Column("criado_em")] // CHANGED FROM data_cadastro
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CriadoEm { get; set; }

        [ValidateNever]
        [ForeignKey("EmpresaCnpj")]
        public virtual Empresa? Empresa { get; set; }
    }
}
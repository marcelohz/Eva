using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("usuario", Schema = "web")]
    public class Usuario
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("senha")]
        public string? Senha { get; set; }

        [Column("cpf")]
        public string? Cpf { get; set; }

        [Column("data_nascimento")]
        public DateOnly? DataNascimento { get; set; }

        [Column("telefone")]
        public string? Telefone { get; set; }

        [Column("papel_nome")]
        public string PapelNome { get; set; } = string.Empty;

        [ForeignKey("PapelNome")]
        public virtual Papel Papel { get; set; } = null!;

        [Column("empresa_cnpj")]
        public string? EmpresaCnpj { get; set; }

        [ForeignKey("EmpresaCnpj")]
        public virtual Empresa? Empresa { get; set; }

        [Column("email_validado")]
        public bool EmailValidado { get; set; } = false;
    }
}
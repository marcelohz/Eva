using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Eva.Models
{
    [Table("usuario", Schema = "web")]
    public class Usuario
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("papel_nome")]
        public string PapelNome { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Column("cpf")]
        public string? Cpf { get; set; }

        [Column("data_nascimento")]
        public DateOnly? DataNascimento { get; set; }

        [Column("telefone")]
        public string? Telefone { get; set; }

        [Column("senha")]
        public string? Senha { get; set; }

        [Column("empresa_cnpj")]
        public string? EmpresaCnpj { get; set; }

        [Column("criado_em")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CriadoEm { get; set; }

        [Column("atualizado_em")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime AtualizadoEm { get; set; }

        [Column("ativo")]
        public bool Ativo { get; set; } = true;

        [Column("email_validado")]
        public bool EmailValidado { get; set; } = false;

        [ValidateNever]
        [ForeignKey("PapelNome")]
        public virtual Papel? Papel { get; set; }

        [ValidateNever]
        [ForeignKey("EmpresaCnpj")]
        public virtual Empresa? Empresa { get; set; }
    }
}
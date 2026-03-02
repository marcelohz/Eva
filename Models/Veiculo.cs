using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Eva.Models
{
    [Table("veiculo", Schema = "geral")]
    public class Veiculo
    {
        [Key]
        [Required(ErrorMessage = "A Placa é obrigatória")]
        [StringLength(7, MinimumLength = 7, ErrorMessage = "A placa deve ter 7 caracteres")]
        [Column("placa")]
        public string Placa { get; set; } = string.Empty;

        [Column("chassi_numero")]
        public string? ChassiNumero { get; set; }

        [Column("renavan")]
        public string? Renavan { get; set; }

        [Required(ErrorMessage = "O Modelo é obrigatório")]
        [Column("modelo")]
        public string? Modelo { get; set; }

        [Column("fretamento_veiculo_tipo_nome")]
        public string? FretamentoVeiculoTipoNome { get; set; }

        [Column("potencia_motor")]
        public int? PotenciaMotor { get; set; }

        [Column("cor_principal_nome")]
        public string? CorPrincipalNome { get; set; }

        [Range(1, 100, ErrorMessage = "O número de lugares deve ser entre 1 e 100")]
        [Column("numero_lugares")]
        public int? NumeroLugares { get; set; }

        [ValidateNever]
        [Column("empresa_cnpj")]
        public string? EmpresaCnpj { get; set; }

        [ValidateNever]
        [ForeignKey("EmpresaCnpj")]
        public virtual Empresa? Empresa { get; set; }

        [Range(1900, 2100, ErrorMessage = "Ano inválido")]
        [Column("ano_fabricacao")]
        public int? AnoFabricacao { get; set; }

        [Range(1900, 2100, ErrorMessage = "Ano inválido")]
        [Column("modelo_ano")]
        public int? ModeloAno { get; set; }

        [Column("veiculo_combustivel_nome")]
        public string? VeiculoCombustivelNome { get; set; }

        [ValidateNever]
        [Column("data_inclusao_eventual")]
        public DateOnly DataInclusaoEventual { get; set; }
    }
}
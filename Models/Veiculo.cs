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
        [Column("placa")]
        public string Placa { get; set; } = string.Empty;

        [Column("chassi_numero")]
        public string? ChassiNumero { get; set; }

        [Column("renavan")]
        public string? Renavan { get; set; }

        // REMOVED [Required] so EF Core allows NULLs from the database
        [Column("modelo")]
        public string? Modelo { get; set; }

        [Column("fretamento_veiculo_tipo_nome")]
        public string? FretamentoVeiculoTipoNome { get; set; }

        [Column("potencia_motor")]
        public int? PotenciaMotor { get; set; }

        [Column("cor_principal_nome")]
        public string? CorPrincipalNome { get; set; }

        [Column("numero_lugares")]
        public int? NumeroLugares { get; set; }

        [Column("empresa_cnpj")]
        public string? EmpresaCnpj { get; set; }

        [ValidateNever]
        [ForeignKey("EmpresaCnpj")]
        public virtual Empresa? Empresa { get; set; }

        [Column("ano_fabricacao")]
        public int? AnoFabricacao { get; set; }

        [Column("modelo_ano")]
        public int? ModeloAno { get; set; }

        [Column("veiculo_combustivel_nome")]
        public string? VeiculoCombustivelNome { get; set; }

        [Column("data_inclusao_eventual")]
        public DateOnly? DataInclusaoEventual { get; set; }
    }
}
using System;
using System.ComponentModel.DataAnnotations;

namespace Eva.Models.ViewModels
{
    public class VeiculoVM
    {
        [Required(ErrorMessage = "A Placa é obrigatória")]
        [StringLength(7, MinimumLength = 7, ErrorMessage = "A placa deve ter 7 caracteres")]
        public string Placa { get; set; } = string.Empty;

        [Required(ErrorMessage = "O Modelo é obrigatório")]
        public string Modelo { get; set; } = string.Empty;

        public string? ChassiNumero { get; set; }

        public string? Renavan { get; set; }

        public int? PotenciaMotor { get; set; }

        public string? VeiculoCombustivelNome { get; set; }

        [Range(1, 100, ErrorMessage = "O número de lugares deve ser entre 1 e 100")]
        public int? NumeroLugares { get; set; }

        [Range(1900, 2100, ErrorMessage = "Ano inválido")]
        public int? AnoFabricacao { get; set; }

        [Range(1900, 2100, ErrorMessage = "Ano inválido")]
        public int? ModeloAno { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eva.Models.ViewModels
{
    public class NovaViagemVM
    {
        [Required(ErrorMessage = "O Tipo de Viagem é obrigatório.")]
        public string ViagemTipoNome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O Nome do Contratante é obrigatório.")]
        public string NomeContratante { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF/CNPJ do Contratante é obrigatório.")]
        public string CpfCnpjContratante { get; set; } = string.Empty;

        [Required(ErrorMessage = "O Código da Região é obrigatório.")]
        public string RegiaoCodigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "A Data/Hora de Saída é obrigatória.")]
        public DateTime IdaEm { get; set; }

        [Required(ErrorMessage = "A Data/Hora de Retorno é obrigatória.")]
        public DateTime VoltaEm { get; set; }

        [Required(ErrorMessage = "O Município de Origem é obrigatório.")]
        public string MunicipioOrigem { get; set; } = string.Empty;

        [Required(ErrorMessage = "O Município de Destino é obrigatório.")]
        public string MunicipioDestino { get; set; } = string.Empty;

        [Required(ErrorMessage = "O Veículo é obrigatório.")]
        public string VeiculoPlaca { get; set; } = string.Empty;

        [Required(ErrorMessage = "O Motorista Principal é obrigatório.")]
        public int MotoristaId { get; set; }

        public int? MotoristaAuxId { get; set; }

        public string? Descricao { get; set; }

        // Dynamic Passenger List
        public List<PassageiroVM> Passageiros { get; set; } = new List<PassageiroVM>();
    }

    public class PassageiroVM
    {
        [Required(ErrorMessage = "O Nome do passageiro é obrigatório.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O Documento do passageiro é obrigatório.")]
        public string Documento { get; set; } = string.Empty;
    }
}
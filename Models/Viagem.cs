using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Eva.Models
{
    [Table("viagem", Schema = "eventual")]
    public class Viagem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("empresa_cnpj")]
        public string EmpresaCnpj { get; set; } = string.Empty;

        [Required]
        [Column("nome_contratante")]
        public string NomeContratante { get; set; } = string.Empty;

        [Required]
        [Column("cpf_cnpj_contratante")]
        public string CpfCnpjContratante { get; set; } = string.Empty;

        [Required]
        [Column("regiao_codigo")]
        public string RegiaoCodigo { get; set; } = string.Empty;

        [Required]
        [Column("municipio_origem")]
        public string MunicipioOrigem { get; set; } = string.Empty;

        [Required]
        [Column("municipio_destino")]
        public string MunicipioDestino { get; set; } = string.Empty;

        [Column("ida_em")]
        public DateTime IdaEm { get; set; }

        [Column("volta_em")]
        public DateTime VoltaEm { get; set; }

        [Required]
        [Column("viagem_tipo")]
        public string ViagemTipoNome { get; set; } = string.Empty;

        [Required]
        [Column("veiculo_placa")]
        public string VeiculoPlaca { get; set; } = string.Empty;

        [Column("motorista_id")]
        public int MotoristaId { get; set; }

        [Column("motorista_aux_id")]
        public int? MotoristaAuxId { get; set; }

        [Column("descricao")]
        public string? Descricao { get; set; }

        // Navigation properties
        [ValidateNever]
        [ForeignKey("EmpresaCnpj")]
        public virtual Empresa? Empresa { get; set; }

        [ValidateNever]
        [ForeignKey("ViagemTipoNome")]
        public virtual ViagemTipo? ViagemTipo { get; set; }

        [ValidateNever]
        [ForeignKey("VeiculoPlaca")]
        public virtual Veiculo? Veiculo { get; set; }

        [ValidateNever]
        [ForeignKey("MotoristaId")]
        public virtual Motorista? Motorista { get; set; }

        [ValidateNever]
        [ForeignKey("MotoristaAuxId")]
        public virtual Motorista? MotoristaAux { get; set; }

        public virtual ICollection<Passageiro> Passageiros { get; set; } = new List<Passageiro>();
    }
}
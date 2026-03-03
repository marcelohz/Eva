using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    // Links a Document directly to an Empresa (e.g., Contrato Social)
    [Table("documento_empresa", Schema = "eventual")]
    public class DocumentoEmpresa
    {
        [Key]
        [Column("id")]
        [ForeignKey("Documento")]
        public int Id { get; set; } // Points to Documento.Id

        [Column("empresa_cnpj")]
        public string EmpresaCnpj { get; set; } = string.Empty;

        public virtual Documento Documento { get; set; } = null!;
    }

    // Links a Document to a Vehicle (e.g., CRLV)
    [Table("documento_veiculo", Schema = "eventual")]
    public class DocumentoVeiculo
    {
        [Key]
        [Column("id")]
        [ForeignKey("Documento")]
        public int Id { get; set; }

        [Column("veiculo_placa")]
        public string VeiculoPlaca { get; set; } = string.Empty;

        public virtual Documento Documento { get; set; } = null!;
    }

    // Links a Document to a Driver (e.g., CNH)
    [Table("documento_motorista", Schema = "eventual")]
    public class DocumentoMotorista
    {
        [Key]
        [Column("id")]
        [ForeignKey("Documento")]
        public int Id { get; set; }

        [Column("motorista_id")]
        public int MotoristaId { get; set; }

        public virtual Documento Documento { get; set; } = null!;
    }
}
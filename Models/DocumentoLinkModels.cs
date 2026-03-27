using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    // Links a document to a trip (e.g., Nota Fiscal).
    [Table("documento_viagem", Schema = "eventual")]
    public class DocumentoViagem
    {
        [Key]
        [Column("id")]
        [ForeignKey("Documento")]
        public int Id { get; set; }

        [Column("viagem_id")]
        public int ViagemId { get; set; }

        public virtual Documento Documento { get; set; } = null!;
    }
}

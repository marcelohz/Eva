using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("documento", Schema = "eventual")]
    public class Documento
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("documento_tipo_nome")]
        public string DocumentoTipoNome { get; set; } = string.Empty;

        // CHANGED: Storing the file directly in the DB
        [Required]
        [Column("conteudo")]
        public byte[] Conteudo { get; set; } = Array.Empty<byte>();

        [Required]
        [Column("nome_arquivo")]
        public string NomeArquivo { get; set; } = string.Empty;

        [Required]
        [Column("content_type")]
        public string ContentType { get; set; } = "application/pdf";

        [Column("tamanho")]
        public long? Tamanho { get; set; }

        [Column("hash")]
        public string? Hash { get; set; }

        [Column("data_upload")]
        public DateTime DataUpload { get; set; } = DateTime.Now;

        [Column("validade")]
        public DateOnly? Validade { get; set; }

        [Column("fluxo_pendencia_id")]
        public int? FluxoPendenciaId { get; set; }

        [Column("aprovado_em")]
        public DateTime? AprovadoEm { get; set; }
    }
}
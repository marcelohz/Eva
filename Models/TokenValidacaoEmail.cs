using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eva.Models
{
    [Table("token_validacao_email", Schema = "web")]
    public class TokenValidacaoEmail
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Required]
        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Required]
        [Column("criado_em")]
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("expira_em")]
        public DateTime ExpiraEm { get; set; }

        // Navigation property
        [ForeignKey("UsuarioId")]
        public virtual Usuario? Usuario { get; set; }
    }
}
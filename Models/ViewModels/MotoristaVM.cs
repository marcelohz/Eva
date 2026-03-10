using System.ComponentModel.DataAnnotations;

namespace Eva.Models.ViewModels
{
    public class MotoristaVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O CPF é obrigatório")]
        [RegularExpression(@"^\d{3}\.?\d{3}\.?\d{3}-?\d{2}$", ErrorMessage = "Formato de CPF inválido")]
        public string Cpf { get; set; } = string.Empty;

        [Required(ErrorMessage = "A CNH é obrigatória")]
        public string Cnh { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email em formato inválido")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "O Nome é obrigatório")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 100 caracteres")]
        public string? Nome { get; set; }
    }
}
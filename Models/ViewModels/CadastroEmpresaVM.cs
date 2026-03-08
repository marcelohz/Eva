using System.ComponentModel.DataAnnotations;

namespace Eva.Models.ViewModels
{
    public class CadastroEmpresaVM
    {
        [Required(ErrorMessage = "O CNPJ é obrigatório para validação.")]
        public string Cnpj { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório para o envio do link de acesso.")]
        [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
        public string Email { get; set; } = string.Empty;
    }
}
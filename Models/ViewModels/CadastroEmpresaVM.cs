using System.ComponentModel.DataAnnotations;

namespace Eva.Models.ViewModels
{
    public class CadastroEmpresaVM
    {
        [Required(ErrorMessage = "CNPJ é obrigatório")]
        public string Cnpj { get; set; } = string.Empty;

        [Required(ErrorMessage = "Razão Social é obrigatória")]
        public string RazaoSocial { get; set; } = string.Empty;

        public string? NomeFantasia { get; set; }

        [Required(ErrorMessage = "Email é obrigatório")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha é obrigatória")]
        [MinLength(6, ErrorMessage = "A senha deve ter pelo menos 6 caracteres")]
        [DataType(DataType.Password)]
        public string Senha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirmação de senha é obrigatória")]
        [Compare("Senha", ErrorMessage = "As senhas não conferem")]
        [DataType(DataType.Password)]
        public string ConfirmarSenha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nome do responsável é obrigatório")]
        public string NomeResponsavel { get; set; } = string.Empty;
    }
}
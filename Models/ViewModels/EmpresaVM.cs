using System.ComponentModel.DataAnnotations;

namespace Eva.Models.ViewModels
{
    public class EmpresaVM
    {
        public string Cnpj { get; set; } = string.Empty; // Read-only on the form

        [Required(ErrorMessage = "A Razão Social é obrigatória")]
        public string Nome { get; set; } = string.Empty;

        public string? NomeFantasia { get; set; }

        public string? Endereco { get; set; }
        public string? EnderecoNumero { get; set; }
        public string? EnderecoComplemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string? Cep { get; set; }

        [EmailAddress(ErrorMessage = "E-mail em formato inválido")]
        public string? Email { get; set; }

        public string? Telefone { get; set; }
    }
}
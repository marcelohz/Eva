using System.ComponentModel.DataAnnotations;

namespace Eva.Configuration
{
    public class DatabaseSettings
    {
        [Required(ErrorMessage = "The DefaultConnection string is strictly required for database access.")]
        public string DefaultConnection { get; set; } = string.Empty;
    }
}
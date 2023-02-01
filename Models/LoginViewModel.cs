using System.ComponentModel.DataAnnotations;

namespace picoblog.Models
{
    public class LoginViewModel
    {
        [Required]
        public string Password { get; set; } = "";
    }
}
namespace picoblog.Models
{
    public class LoginViewModel
    {
        [Required]
        public string Password { get; set; } = "";

        [BindProperty]
        public string? ReturnURL { get; set; }
    }
}

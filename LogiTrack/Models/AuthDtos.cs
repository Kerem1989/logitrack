using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models ;

    public class RegisterDto
    {
        [Required] public string UserName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Password { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public class LoginDto
    {
        [Required] public string UserName { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

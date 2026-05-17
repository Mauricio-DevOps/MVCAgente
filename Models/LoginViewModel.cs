using System.ComponentModel.DataAnnotations;

namespace Mvc.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o usuario.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

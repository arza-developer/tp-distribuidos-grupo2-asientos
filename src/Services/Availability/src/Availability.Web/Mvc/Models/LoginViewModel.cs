using System.ComponentModel.DataAnnotations;

namespace Availability.Web.Mvc.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Ingresá el usuario.")]
    [StringLength(100)]
    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá la contraseña.")]
    [StringLength(200)]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Solo se respeta si es una URL local (evita redirecciones abiertas).</summary>
    public string? ReturnUrl { get; set; }
}

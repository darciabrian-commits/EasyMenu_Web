using System.ComponentModel.DataAnnotations;

namespace EasyMenu.Web.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Ingresa tu correo.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresa tu clave.")]
    [DataType(DataType.Password)]
    public string Clave { get; set; } = string.Empty;
}

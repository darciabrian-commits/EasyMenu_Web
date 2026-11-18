using System.ComponentModel.DataAnnotations;

namespace EasyMenu.Web.Models;

public sealed class MesasAdminViewModel
{
    public List<MesaResponse> Mesas { get; init; } = [];
    public bool PuedeModificar { get; init; }
    public string? Mensaje { get; init; }
}

public sealed class MesaDetalleViewModel
{
    public MesaResponse? Mesa { get; init; }
    public string? Mensaje { get; init; }
}

public sealed class MesaFormularioViewModel
{
    [Required(ErrorMessage = "Ingresa el número de mesa.")]
    [Range(1, int.MaxValue, ErrorMessage = "El número de mesa debe ser mayor que cero.")]
    public int? Numero { get; set; }

    [Required(ErrorMessage = "Ingresa la capacidad de la mesa.")]
    [Range(1, int.MaxValue, ErrorMessage = "La capacidad debe ser mayor que cero.")]
    public int? Capacidad { get; set; }

    public bool Activa { get; set; } = true;
}

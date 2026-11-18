using System.ComponentModel.DataAnnotations;

namespace EasyMenu.Web.Models;

public sealed class ReservasListaViewModel
{
    public IReadOnlyList<ReservaPresentacionViewModel> Reservas { get; init; } = [];
    public bool PuedeGestionar { get; init; }
    public bool PuedeMarcarAtendida { get; init; }
    public bool MostrarDatosCliente { get; init; }
    public DateOnly? FechaFiltro { get; init; }
    public string? Mensaje { get; init; }
}

public sealed class ReservaDetalleViewModel
{
    public ReservaPresentacionViewModel? Reserva { get; init; }
    public bool PuedeGestionar { get; init; }
    public bool MostrarDatosCliente { get; init; }
    public string? Mensaje { get; init; }
}

public sealed class ReservaPresentacionViewModel
{
    public required int Id { get; init; }
    public string? NombreCliente { get; init; }
    public string? Telefono { get; init; }
    public required DateOnly FechaReserva { get; init; }
    public required TimeOnly HoraReserva { get; init; }
    public required int CantidadPersonas { get; init; }
    public required int MesaId { get; init; }
    public required int NumeroMesa { get; init; }
    public required string Estado { get; init; }
    public string? Observaciones { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
}

public sealed class ReservaFormularioViewModel
{
    [Required(ErrorMessage = "Ingresa el nombre del cliente.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    public string? NombreCliente { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "Selecciona la fecha de la reserva.")]
    public DateOnly? FechaReserva { get; set; }

    [Required(ErrorMessage = "Selecciona la hora de la reserva.")]
    public TimeOnly? HoraReserva { get; set; }

    [Required(ErrorMessage = "Ingresa la cantidad de personas.")]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad de personas debe ser mayor que cero.")]
    public int? CantidadPersonas { get; set; }

    [Required(ErrorMessage = "Selecciona una mesa.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una mesa válida.")]
    public int? MesaId { get; set; }

    [StringLength(300, ErrorMessage = "Las observaciones no pueden superar 300 caracteres.")]
    public string? Observaciones { get; set; }

    public IReadOnlyList<MesaResponse> MesasDisponibles { get; set; } = [];
    public string? MensajeMesas { get; set; }
}

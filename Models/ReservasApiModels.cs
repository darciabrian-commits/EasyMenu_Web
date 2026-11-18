namespace EasyMenu.Web.Models;

public sealed class MesaCreate
{
    public required int Numero { get; init; }
    public required int Capacidad { get; init; }
}

public sealed class MesaUpdate
{
    public required int Numero { get; init; }
    public required int Capacidad { get; init; }
    public required bool Activa { get; init; }
}

public sealed class MesaResponse
{
    public required int Id { get; init; }
    public required int Numero { get; init; }
    public required int Capacidad { get; init; }
    public required bool Activa { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
}

public sealed class ReservaCreate
{
    public required string NombreCliente { get; init; }
    public required string Telefono { get; init; }
    public required DateOnly FechaReserva { get; init; }
    public required TimeOnly HoraReserva { get; init; }
    public required int CantidadPersonas { get; init; }
    public required int MesaId { get; init; }
    public string? Observaciones { get; init; }
}

public sealed class ReservaUpdate
{
    public required string NombreCliente { get; init; }
    public required string Telefono { get; init; }
    public required DateOnly FechaReserva { get; init; }
    public required TimeOnly HoraReserva { get; init; }
    public required int CantidadPersonas { get; init; }
    public required int MesaId { get; init; }
    public string? Observaciones { get; init; }
}

public sealed class ReservaEstado
{
    public required string Estado { get; init; }
}

public sealed class ReservaResponse
{
    public required int Id { get; init; }
    public required string NombreCliente { get; init; }
    public required string Telefono { get; init; }
    public required DateOnly FechaReserva { get; init; }
    public required TimeOnly HoraReserva { get; init; }
    public required int CantidadPersonas { get; init; }
    public required int MesaId { get; init; }
    public required int NumeroMesa { get; init; }
    public required string Estado { get; init; }
    public string? Observaciones { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
}

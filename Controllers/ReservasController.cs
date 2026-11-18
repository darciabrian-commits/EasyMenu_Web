using EasyMenu.Web.Filters;
using EasyMenu.Web.Models;
using EasyMenu.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Web.Controllers;

[RequireRole("ADMINISTRADOR", "CAJERO", "COCINA")]
public sealed class ReservasController(ReservasApiClient reservasApiClient) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Lista(DateOnly? fecha, CancellationToken cancellationToken)
    {
        var resultado = fecha.HasValue
            ? await reservasApiClient.ObtenerReservasPorFechaAsync(fecha.Value, cancellationToken)
            : await reservasApiClient.ObtenerReservasAsync(cancellationToken);
        var puedeVerDatosCliente = PuedeVerDatosCliente();

        return PartialView("_ListaReservas", new ReservasListaViewModel
        {
            Reservas = resultado.IsSuccess
                ? resultado.Value!.Select(reserva => Proyectar(reserva, puedeVerDatosCliente)).ToArray()
                : [],
            PuedeGestionar = PuedeGestionar(),
            PuedeMarcarAtendida = EsAdministrador(),
            MostrarDatosCliente = puedeVerDatosCliente,
            FechaFiltro = fecha,
            Mensaje = resultado.IsSuccess ? null : resultado.Mensaje
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return View(new ReservaDetalleViewModel
            {
                PuedeGestionar = PuedeGestionar(),
                MostrarDatosCliente = PuedeVerDatosCliente(),
                Mensaje = "El número de reserva solicitado no es válido."
            });
        }

        var resultado = await reservasApiClient.ObtenerReservaAsync(id, cancellationToken);
        var puedeVerDatosCliente = PuedeVerDatosCliente();
        return View(new ReservaDetalleViewModel
        {
            Reserva = resultado.IsSuccess
                ? Proyectar(resultado.Value!, puedeVerDatosCliente)
                : null,
            PuedeGestionar = PuedeGestionar(),
            MostrarDatosCliente = puedeVerDatosCliente,
            Mensaje = resultado.IsSuccess ? null : resultado.Mensaje
        });
    }

    [HttpGet]
    [RequireRole("ADMINISTRADOR", "CAJERO")]
    public async Task<IActionResult> Crear(CancellationToken cancellationToken)
    {
        var modelo = new ReservaFormularioViewModel();
        await CargarMesasAsync(modelo, cancellationToken);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole("ADMINISTRADOR", "CAJERO")]
    public async Task<IActionResult> Crear(
        ReservaFormularioViewModel modelo,
        CancellationToken cancellationToken)
    {
        ValidarFechaFutura(modelo);
        var mesas = await CargarMesasAsync(modelo, cancellationToken);
        ValidarMesa(modelo, mesas);

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var resultado = await reservasApiClient.CrearReservaAsync(CrearDtoCreate(modelo), cancellationToken);
        if (!resultado.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No fue posible crear la reserva.");
            return View(modelo);
        }

        TempData["MensajeExito"] = "La reserva se creó correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequireRole("ADMINISTRADOR", "CAJERO")]
    public async Task<IActionResult> Editar(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            TempData["MensajeError"] = "El número de reserva solicitado no es válido.";
            return RedirectToAction(nameof(Index));
        }

        var resultado = await reservasApiClient.ObtenerReservaAsync(id, cancellationToken);
        if (!resultado.IsSuccess)
        {
            TempData["MensajeError"] = resultado.Mensaje ?? "No fue posible consultar la reserva.";
            return RedirectToAction(nameof(Index));
        }

        var reserva = resultado.Value!;
        var modelo = new ReservaFormularioViewModel
        {
            NombreCliente = reserva.NombreCliente,
            Telefono = reserva.Telefono,
            FechaReserva = reserva.FechaReserva,
            HoraReserva = reserva.HoraReserva,
            CantidadPersonas = reserva.CantidadPersonas,
            MesaId = reserva.MesaId,
            Observaciones = reserva.Observaciones
        };
        await CargarMesasAsync(modelo, cancellationToken);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole("ADMINISTRADOR", "CAJERO")]
    public async Task<IActionResult> Editar(
        int id,
        ReservaFormularioViewModel modelo,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            ModelState.AddModelError(string.Empty, "El número de reserva solicitado no es válido.");
        }

        ValidarFechaFutura(modelo);
        var mesas = await CargarMesasAsync(modelo, cancellationToken);
        ValidarMesa(modelo, mesas);

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var resultado = await reservasApiClient.ActualizarReservaAsync(id, CrearDtoUpdate(modelo), cancellationToken);
        if (!resultado.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No fue posible actualizar la reserva.");
            return View(modelo);
        }

        TempData["MensajeExito"] = "La reserva se actualizó correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole("ADMINISTRADOR", "CAJERO")]
    public Task<IActionResult> CambiarEstado(int id, string estado, CancellationToken cancellationToken) =>
        estado == "CONFIRMADA"
            ? ActualizarEstadoAsync(id, "CONFIRMADA", cancellationToken)
            : Task.FromResult<IActionResult>(Forbid());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole("ADMINISTRADOR", "CAJERO")]
    public Task<IActionResult> Cancelar(int id, CancellationToken cancellationToken) =>
        ActualizarEstadoAsync(id, "CANCELADA", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole("ADMINISTRADOR")]
    public Task<IActionResult> Atender(int id, CancellationToken cancellationToken) =>
        ActualizarEstadoAsync(id, "ATENDIDA", cancellationToken);

    private async Task<IActionResult> ActualizarEstadoAsync(
        int id,
        string estado,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            TempData["MensajeError"] = "La reserva o el estado solicitado no es válido.";
            return RedirectToAction(nameof(Index));
        }

        var reservaActual = await reservasApiClient.ObtenerReservaAsync(id, cancellationToken);
        if (!reservaActual.IsSuccess)
        {
            TempData["MensajeError"] = reservaActual.Mensaje ?? "No fue posible consultar la reserva.";
            return RedirectToAction(nameof(Index));
        }

        if (reservaActual.Value!.Estado == "CANCELADA" && estado == "ATENDIDA")
        {
            TempData["MensajeError"] = "Una reserva cancelada no puede marcarse como atendida.";
            return RedirectToAction(nameof(Index));
        }

        var resultado = await reservasApiClient.CambiarEstadoReservaAsync(
            id,
            new ReservaEstado { Estado = estado },
            cancellationToken);
        TempData[resultado.IsSuccess ? "MensajeExito" : "MensajeError"] = resultado.IsSuccess
            ? estado == "CANCELADA"
                ? "La reserva se canceló correctamente."
                : "El estado de la reserva se actualizó correctamente."
            : resultado.Mensaje ?? "No fue posible actualizar el estado de la reserva.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<MesaResponse>> CargarMesasAsync(
        ReservaFormularioViewModel modelo,
        CancellationToken cancellationToken)
    {
        var resultado = await reservasApiClient.ObtenerMesasAsync(cancellationToken);
        if (!resultado.IsSuccess)
        {
            modelo.MesasDisponibles = [];
            modelo.MensajeMesas = resultado.Mensaje ?? "No fue posible cargar las mesas.";
            ModelState.AddModelError(string.Empty, modelo.MensajeMesas);
            return [];
        }

        var mesas = resultado.Value!.Where(mesa => mesa.Activa).ToList();
        modelo.MesasDisponibles = mesas;
        modelo.MensajeMesas = mesas.Count == 0 ? "No hay mesas activas disponibles." : null;
        return mesas;
    }

    private void ValidarFechaFutura(ReservaFormularioViewModel modelo)
    {
        if (modelo.FechaReserva is not DateOnly fecha || modelo.HoraReserva is not TimeOnly hora)
        {
            return;
        }

        var ahora = DateTime.Now;
        var hoy = DateOnly.FromDateTime(ahora);
        if (fecha < hoy || (fecha == hoy && hora <= TimeOnly.FromDateTime(ahora)))
        {
            ModelState.AddModelError(
                nameof(modelo.FechaReserva),
                "La fecha y hora de la reserva deben ser futuras.");
        }
    }

    private void ValidarMesa(ReservaFormularioViewModel modelo, IReadOnlyList<MesaResponse> mesas)
    {
        if (modelo.MesaId is not int mesaId || modelo.CantidadPersonas is not int cantidad)
        {
            return;
        }

        var mesa = mesas.FirstOrDefault(item => item.Id == mesaId);
        if (mesa is null)
        {
            ModelState.AddModelError(nameof(modelo.MesaId), "Selecciona una mesa activa válida.");
        }
        else if (cantidad > mesa.Capacidad)
        {
            ModelState.AddModelError(
                nameof(modelo.CantidadPersonas),
                "La cantidad de personas supera la capacidad de la mesa seleccionada.");
        }
    }

    private static ReservaCreate CrearDtoCreate(ReservaFormularioViewModel modelo) => new()
    {
        NombreCliente = modelo.NombreCliente!.Trim(),
        Telefono = modelo.Telefono?.Trim() ?? string.Empty,
        FechaReserva = modelo.FechaReserva!.Value,
        HoraReserva = modelo.HoraReserva!.Value,
        CantidadPersonas = modelo.CantidadPersonas!.Value,
        MesaId = modelo.MesaId!.Value,
        Observaciones = modelo.Observaciones?.Trim()
    };

    private static ReservaUpdate CrearDtoUpdate(ReservaFormularioViewModel modelo) => new()
    {
        NombreCliente = modelo.NombreCliente!.Trim(),
        Telefono = modelo.Telefono?.Trim() ?? string.Empty,
        FechaReserva = modelo.FechaReserva!.Value,
        HoraReserva = modelo.HoraReserva!.Value,
        CantidadPersonas = modelo.CantidadPersonas!.Value,
        MesaId = modelo.MesaId!.Value,
        Observaciones = modelo.Observaciones?.Trim()
    };

    private static ReservaPresentacionViewModel Proyectar(
        ReservaResponse reserva,
        bool mostrarDatosCliente) => new()
    {
        Id = reserva.Id,
        NombreCliente = mostrarDatosCliente ? reserva.NombreCliente : null,
        Telefono = mostrarDatosCliente ? reserva.Telefono : null,
        FechaReserva = reserva.FechaReserva,
        HoraReserva = reserva.HoraReserva,
        CantidadPersonas = reserva.CantidadPersonas,
        MesaId = reserva.MesaId,
        NumeroMesa = reserva.NumeroMesa,
        Estado = reserva.Estado,
        Observaciones = reserva.Observaciones,
        FechaCreacion = reserva.FechaCreacion
    };

    private bool PuedeGestionar() =>
        HttpContext.Session.GetString("Rol") is "ADMINISTRADOR" or "CAJERO";

    private bool PuedeVerDatosCliente() => PuedeGestionar();

    private bool EsAdministrador() =>
        HttpContext.Session.GetString("Rol") == "ADMINISTRADOR";
}

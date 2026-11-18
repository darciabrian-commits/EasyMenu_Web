using EasyMenu.Web.Filters;
using EasyMenu.Web.Models;
using EasyMenu.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyMenu.Web.Controllers;

[RequireRole("ADMINISTRADOR", "CAJERO", "COCINA")]
public sealed class MesasController(ReservasApiClient reservasApiClient) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> ListaMesas(CancellationToken cancellationToken)
    {
        var resultado = await reservasApiClient.ObtenerMesasAsync(cancellationToken);
        return PartialView("_ListaMesas", new MesasAdminViewModel
        {
            PuedeModificar = EsAdministrador(),
            Mesas = resultado.IsSuccess ? resultado.Value! : [],
            Mensaje = resultado.IsSuccess ? null : resultado.Mensaje
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return PartialView("_DetalleMesa", new MesaDetalleViewModel
            {
                Mensaje = "El número de mesa solicitado no es válido."
            });
        }

        var resultado = await reservasApiClient.ObtenerMesaAsync(id, cancellationToken);
        return PartialView("_DetalleMesa", new MesaDetalleViewModel
        {
            Mesa = resultado.IsSuccess ? resultado.Value : null,
            Mensaje = resultado.IsSuccess ? null : resultado.Mensaje
        });
    }

    [HttpGet]
    [RequireRole("ADMINISTRADOR")]
    public IActionResult Crear() => View(new MesaFormularioViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole("ADMINISTRADOR")]
    public async Task<IActionResult> Crear(
        MesaFormularioViewModel modelo,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var resultado = await reservasApiClient.CrearMesaAsync(new MesaCreate
        {
            Numero = modelo.Numero!.Value,
            Capacidad = modelo.Capacidad!.Value
        }, cancellationToken);

        if (!resultado.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No fue posible crear la mesa.");
            return View(modelo);
        }

        TempData["MensajeExito"] = "La mesa se creó correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequireRole("ADMINISTRADOR")]
    public async Task<IActionResult> Editar(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            TempData["MensajeError"] = "El número de mesa solicitado no es válido.";
            return RedirectToAction(nameof(Index));
        }

        var resultado = await reservasApiClient.ObtenerMesaAsync(id, cancellationToken);
        if (!resultado.IsSuccess)
        {
            TempData["MensajeError"] = resultado.Mensaje ?? "No fue posible consultar la mesa.";
            return RedirectToAction(nameof(Index));
        }

        return View(new MesaFormularioViewModel
        {
            Numero = resultado.Value!.Numero,
            Capacidad = resultado.Value.Capacidad,
            Activa = resultado.Value.Activa
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole("ADMINISTRADOR")]
    public async Task<IActionResult> Editar(
        int id,
        MesaFormularioViewModel modelo,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            ModelState.AddModelError(string.Empty, "El número de mesa solicitado no es válido.");
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var resultado = await reservasApiClient.ActualizarMesaAsync(id, new MesaUpdate
        {
            Numero = modelo.Numero!.Value,
            Capacidad = modelo.Capacidad!.Value,
            Activa = modelo.Activa
        }, cancellationToken);

        if (!resultado.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No fue posible actualizar la mesa.");
            return View(modelo);
        }

        TempData["MensajeExito"] = "La mesa se actualizó correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole("ADMINISTRADOR")]
    public async Task<IActionResult> CambiarEstado(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            TempData["MensajeError"] = "El número de mesa solicitado no es válido.";
            return RedirectToAction(nameof(Index));
        }

        var mesaActual = await reservasApiClient.ObtenerMesaAsync(id, cancellationToken);
        if (!mesaActual.IsSuccess)
        {
            TempData["MensajeError"] = mesaActual.Mensaje ?? "No fue posible consultar la mesa.";
            return RedirectToAction(nameof(Index));
        }

        var mesa = mesaActual.Value!;
        var resultado = await reservasApiClient.ActualizarMesaAsync(id, new MesaUpdate
        {
            Numero = mesa.Numero,
            Capacidad = mesa.Capacidad,
            Activa = !mesa.Activa
        }, cancellationToken);

        TempData[resultado.IsSuccess ? "MensajeExito" : "MensajeError"] = resultado.IsSuccess
            ? $"La mesa se {(mesa.Activa ? "desactivó" : "activó")} correctamente."
            : resultado.Mensaje ?? "No fue posible cambiar el estado de la mesa.";

        return RedirectToAction(nameof(Index));
    }

    private bool EsAdministrador() =>
        HttpContext.Session.GetString("Rol") == "ADMINISTRADOR";
}
